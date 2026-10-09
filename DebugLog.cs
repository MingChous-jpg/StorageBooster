using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace StorageBooster
{
    /// <summary>
    /// Debug 模式专用日志。
    ///
    /// 开启方式（任一满足即可，文件夹优先）：
    ///   1. mod 目录下存在 _debug 文件夹（不用动配置，玩家自助开关）
    ///   2. config.ini 里 debug = true
    ///   3. 装了 ModSetting 时，设置页里的 "Debug 模式" 开关
    ///
    /// 开启后：
    ///   - 本 mod 的所有日志额外落到 _debug/StorageBooster.log（只含本 mod，不用去 Player.log 里捞）
    ///   - 启动时自动从 Player.log 抓一遍历史本 mod 日志，存成 _debug/PlayerLog_*.log
    ///   - 可随时导出一份诊断快照 _debug/diagnostics_*.txt
    /// </summary>
    public static class DebugLog
    {
        /// <summary>开关文件夹名。改这里记得同步 README。</summary>
        public const string FolderName = "_debug";

        private const string MainLogName = "StorageBooster.log";
        private const string Marker = "[StorageBooster]";
        private const long RotateSize = 2L * 1024 * 1024;

        private static readonly object Sync = new object();
        private static string _directory = ".";
        private static bool _enabled;
        private static bool _enabledByFolder;
        private static StreamWriter? _writer;
        private static bool _headerWritten;

        // OnEnable 早于 OnAfterSetup，Harmony 加载等最早几条日志会赶在 debug 就绪之前打出来。
        // 先缓存下来，等开启时补写进文件，避免"最关键的那几行反而没有"。
        private const int PendingLimit = 256;
        private static readonly System.Collections.Generic.List<string> Pending = new System.Collections.Generic.List<string>();

        /// <summary>Debug 模式当前是否开启。</summary>
        public static bool Enabled => _enabled;

        /// <summary>是否是由 _debug 文件夹触发的（config 里可能还是 false）。</summary>
        public static bool EnabledByFolder => _enabledByFolder;

        /// <summary>_debug 文件夹的完整路径。</summary>
        public static string DebugDirectory => _directory;

        /// <summary>
        /// 第一步：确定目录并探测 _debug 文件夹是否存在。
        /// 必须在 OnAfterSetup 之后调（那时才知道 mod 目录），且要在读 config 之前 ——
        /// 否则读配置时的报错日志会漏掉，没法写进 debug 文件。
        /// </summary>
        public static void Prepare(string modDirectory)
        {
            _directory = Path.Combine(modDirectory ?? ".", FolderName);
            _enabledByFolder = Directory.Exists(_directory);
        }

        /// <summary>第二步：读完 config 后决定最终开关。文件夹存在优先于配置。</summary>
        public static void Init(bool configEnabled)
        {
            SetEnabled(_enabledByFolder || configEnabled);
        }

        /// <summary>运行中开关（ModSetting 的 toggle 调这个）。</summary>
        public static void SetEnabled(bool value)
        {
            if (value && !_enabled)
            {
                try
                {
                    if (!Directory.Exists(_directory))
                        Directory.CreateDirectory(_directory);

                    lock (Sync)
                    {
                        OpenWriter();
                        _enabled = true;
                    }

                    if (!_headerWritten) WriteHeader();
                    FlushPending();
                    Info($"Debug 模式已开启（由{(_enabledByFolder ? " _debug 文件夹" : " 设置项")}触发）");
                }
                catch (Exception e)
                {
                    _enabled = false;
                    Debug.LogWarning($"{Marker} 无法开启 Debug 模式: {e.Message}");
                }
            }
            else if (!value && _enabled)
            {
                Info("Debug 模式已关闭");
                lock (Sync)
                {
                    _enabled = false;
                    CloseWriter();
                }
            }
        }

        public static void Info(string message) => Write("INFO ", message);
        public static void Warn(string message) => Write("WARN ", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

            // 游戏控制台照常输出，不开 debug 也能在 Player.log 里看到
            if (level == "ERROR") Debug.LogError($"{Marker} {message}");
            else if (level == "WARN") Debug.LogWarning($"{Marker} {message}");
            else Debug.Log($"{Marker} {message}");

            if (!_enabled)
            {
                // 还没就绪（或压根没开）：留一份在内存里，开启时可以补写
                lock (Sync)
                {
                    if (Pending.Count >= PendingLimit) Pending.RemoveAt(0);
                    Pending.Add(line);
                }
                return;
            }

            lock (Sync)
            {
                StreamWriter? writer = _writer;
                if (writer == null) return;
                try
                {
                    writer.WriteLine(line);
                    writer.Flush();
                    RotateIfNeeded();
                }
                catch (Exception e)
                {
                    // 写日志失败绝不能反过来崩掉 mod
                    Debug.LogWarning($"{Marker} 写日志文件失败: {e.Message}");
                }
            }
        }

        /// <summary>把 debug 就绪前缓存的日志补写进文件。</summary>
        private static void FlushPending()
        {
            string[] lines;
            lock (Sync)
            {
                if (Pending.Count == 0) return;
                lines = Pending.ToArray();
                Pending.Clear();
            }

            StreamWriter? writer = _writer;
            if (writer == null) return;

            try
            {
                writer.WriteLine("# 以下为 debug 就绪前缓冲的日志（OnEnable 阶段）");
                foreach (string line in lines)
                    writer.WriteLine(line);
                writer.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Marker} 补写缓冲日志失败: {e.Message}");
            }
        }

        /// <summary>
        /// 从 Player.log 里把本 mod 的历史日志捞出来，单独存一份。
        /// 省得玩家在几千行游戏日志里手动翻。
        /// </summary>
        public static string? CapturePlayerLog()
        {
            string? playerLog = FindPlayerLog();
            if (playerLog == null)
            {
                Warn("找不到 Player.log，跳过抓取。");
                return null;
            }

            string outPath = Path.Combine(_directory, $"PlayerLog_{DateTime.Now:yyyyMMdd-HHmmss}.log");
            try
            {
                int total = 0, hit = 0;
                using (StreamWriter w = new StreamWriter(outPath, false, Encoding.UTF8))
                {
                    w.WriteLine($"# 从 {playerLog} 抓取，筛选包含 \"{Marker}\" 的行");
                    w.WriteLine($"# 抓取时间 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    w.WriteLine();

                    // Player.log 可能正被游戏占用，用只读共享方式打开
                    using (FileStream fs = new FileStream(playerLog, FileMode.Open,
                               FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader r = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            total++;
                            if (line.IndexOf(Marker, StringComparison.Ordinal) >= 0)
                            {
                                w.WriteLine(line);
                                hit++;
                            }
                        }
                    }

                    w.WriteLine();
                    w.WriteLine($"# 共扫描 {total} 行，命中 {hit} 行");
                }

                Info($"已从 Player.log 抓取 {hit}/{total} 行 -> {outPath}");
                PruneOldFiles("PlayerLog_*.log", 5);
                return outPath;
            }
            catch (Exception e)
            {
                Warn($"抓取 Player.log 失败: {e.Message}");
                return null;
            }
        }

        /// <summary>写一份诊断快照，报 bug 时贴这个就够。</summary>
        public static string? DumpDiagnostics()
        {
            string outPath = Path.Combine(_directory, $"diagnostics_{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# StorageBooster 诊断快照");
                sb.AppendLine($"# 生成时间 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();

                sb.AppendLine("## 环境");
                sb.AppendLine($"Unity         : {Application.unityVersion}");
                sb.AppendLine($"Product       : {Application.productName}");
                sb.AppendLine($"Company       : {Application.companyName}");
                sb.AppendLine($"Platform      : {Application.platform} / {SystemInfo.operatingSystem}");
                sb.AppendLine($"DataPath      : {Application.dataPath}");
                sb.AppendLine($"ConsoleLogPath: {SafeConsoleLogPath()}");
                sb.AppendLine();

                sb.AppendLine("## 本 mod");
                sb.AppendLine($"程序集        : {typeof(DebugLog).Assembly.GetName().Version}");
                sb.AppendLine($"目录          : {Path.GetDirectoryName(_directory)}");
                sb.AppendLine($"Debug 目录    : {_directory}");
                sb.AppendLine($"Debug 状态    : {(_enabled ? "开启" : "关闭")}（来源: {(_enabledByFolder ? "_debug 文件夹" : "设置项")}）");
                sb.AppendLine();

                sb.AppendLine("## 配置");
                Config s = ModBehaviour.Settings;
                sb.AppendLine($"storageCapacity = {s.StorageCapacity}");
                sb.AppendLine($"stackMultiplier = {s.StackMultiplier}");
                sb.AppendLine($"debug           = {s.Debug}");
                sb.AppendLine();

                sb.AppendLine("## 运行状态");
                sb.AppendLine($"Harmony 已加载 : {HarmonyLoad.HarmonyVersion}");
                sb.AppendLine($"Patch 已应用   : {ModBehaviour.IsPatched}");
                sb.AppendLine($"ModSetting 可用: {ModSettingBridge.Available}");

                try
                {
                    PlayerStorage? storage = PlayerStorage.Instance;
                    bool hasInstance = storage != null;
                    sb.AppendLine($"PlayerStorage  : {(hasInstance ? "已就绪" : "未就绪（还没进基地）")}");
                    if (storage != null)
                    {
                        sb.AppendLine($"  官方默认容量 : {storage.DefaultCapacity}");
                        ItemStatsSystem.Inventory inv = PlayerStorage.Inventory;
                        sb.AppendLine($"  当前仓库容量 : {(inv != null ? inv.Capacity.ToString(CultureInfo.InvariantCulture) : "n/a")}");
                    }
                }
                catch (Exception e)
                {
                    sb.AppendLine($"PlayerStorage  : 读取失败 {e.Message}");
                }

                sb.AppendLine();
                sb.AppendLine("## 已加载程序集中的 mod");
                foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = asm.GetName().Name;
                    if (string.IsNullOrEmpty(n)) continue;
                    // mod 程序集的特征：名字里带 Mod / Duckov 相关且不是官方那几个
                    if (n.IndexOf("TeamSoda", StringComparison.Ordinal) >= 0) continue;
                    if (n.IndexOf("Unity", StringComparison.Ordinal) >= 0) continue;
                    if (n.IndexOf("System", StringComparison.Ordinal) == 0) continue;
                    if (n == "0Harmony" || n == "ItemStatsSystem" || n == "Assembly-CSharp") continue;
                    sb.AppendLine($"  - {n} {asm.GetName().Version}");
                }

                File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
                Info($"诊断快照已导出 -> {outPath}");
                PruneOldFiles("diagnostics_*.txt", 5);
                return outPath;
            }
            catch (Exception e)
            {
                Warn($"导出诊断快照失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 只留最近的 keep 个快照，避免每次启动都堆一份、文件夹越滚越大。
        /// 文件名带 yyyyMMdd-HHmmss，字典序就是时间序，直接排序删最旧的。
        /// </summary>
        private static void PruneOldFiles(string pattern, int keep)
        {
            try
            {
                string[] files = Directory.GetFiles(_directory, pattern);
                if (files.Length <= keep) return;

                Array.Sort(files, StringComparer.Ordinal);
                for (int i = 0; i < files.Length - keep; i++)
                    File.Delete(files[i]);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Marker} 清理旧日志失败: {e.Message}");
            }
        }

        private static string SafeConsoleLogPath()
        {
            try { return Application.consoleLogPath ?? "(n/a)"; }
            catch { return "(n/a)"; }
        }

        private static string? FindPlayerLog()
        {
            // Unity 2018.3+ 有 consoleLogPath，最准
            string direct = SafeConsoleLogPath();
            if (!string.IsNullOrEmpty(direct) && direct[0] != '(' && File.Exists(direct))
                return direct;

            // 兜底：LocalLow/<Company>/<Product>/Player.log
            try
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string candidate = Path.Combine(local, "..", "LocalLow",
                    Application.companyName, Application.productName, "Player.log");
                candidate = Path.GetFullPath(candidate);
                if (File.Exists(candidate)) return candidate;
            }
            catch { /* 忽略 */ }

            return null;
        }

        private static void OpenWriter()
        {
            string path = Path.Combine(_directory, MainLogName);
            _writer = new StreamWriter(path, true, Encoding.UTF8) { AutoFlush = false };
        }

        private static void CloseWriter()
        {
            try { _writer?.Flush(); } catch { /* 忽略 */ }
            try { _writer?.Dispose(); } catch { /* 忽略 */ }
            _writer = null;
        }

        private static void RotateIfNeeded()
        {
            if (_writer == null) return;
            try
            {
                if (_writer.BaseStream.Length < RotateSize) return;

                CloseWriter();
                string path = Path.Combine(_directory, MainLogName);
                string archived = Path.Combine(_directory, $"StorageBooster_{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.Move(path, archived);
                OpenWriter();
                _writer?.WriteLine($"# 日志已轮转，旧文件 -> {archived}");
                _writer?.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Marker} 日志轮转失败: {e.Message}");
            }
        }

        private static void WriteHeader()
        {
            if (_headerWritten) return;

            StreamWriter? writer = _writer;
            if (writer == null) return;
            _headerWritten = true;

            writer.WriteLine();
            writer.WriteLine("========== StorageBooster debug 日志 ==========");
            writer.WriteLine($"启动时间 {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Unity {Application.unityVersion} / {Application.productName}");
            writer.WriteLine($"日志目录 {_directory}");
            writer.WriteLine("==============================================");
            writer.Flush();
        }
    }
}

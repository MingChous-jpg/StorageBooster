using System;
using System.Globalization;
using System.IO;

namespace StorageBooster
{
    /// <summary>
    /// 设置项。两份入口都写同一份 config.ini：
    /// 1) 直接编辑 mod 目录下的 config.ini（不装任何前置 mod 也能用）
    /// 2) 装了 ModSetting 时，游戏内滑块改动会回写到这里
    /// </summary>
    public sealed class Config
    {
        private const string FileName = "config.ini";

        /// <summary>仓库目标格数。实际生效值 = max(官方算出来的容量, 这个值)，只扩不缩。</summary>
        public int StorageCapacity { get; set; } = 400;

        /// <summary>堆叠倍率。1 = 不改。不可堆叠物品（MaxStackCount &lt;= 1）不受影响。</summary>
        public int StackMultiplier { get; set; } = 2;

        /// <summary>
        /// Debug 模式。开启后本 mod 的日志会额外写到 _debug 文件夹。
        /// 注意：mod 目录下只要存在 _debug 文件夹就会强制开启，优先级高于这里。
        /// </summary>
        public bool Debug { get; set; }

        private const int StorageCapacityMin = 32;
        private const int StorageCapacityMax = 5000;
        private const int StackMultiplierMin = 1;
        private const int StackMultiplierMax = 100;

        public void Clamp()
        {
            StorageCapacity = Math.Min(Math.Max(StorageCapacity, StorageCapacityMin), StorageCapacityMax);
            StackMultiplier = Math.Min(Math.Max(StackMultiplier, StackMultiplierMin), StackMultiplierMax);
        }

        /// <summary>宽松解析布尔：1/true/yes/on 都算开，其它算关。</summary>
        private static bool ParseBool(string value)
        {
            switch (value.ToUpperInvariant())
            {
                case "1":
                case "TRUE":
                case "YES":
                case "ON":
                case "ENABLED":
                    return true;
                default:
                    return false;
            }
        }

        public static Config Load(string directory)
        {
            Config config = new Config();
            try
            {
                string path = Path.Combine(directory, FileName);
                if (!File.Exists(path))
                {
                    config.Save(directory);   // 首次运行生成一份，方便用户改
                    return config;
                }

                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    // 用 char 比较而不是 StartsWith(string) —— 后者受当前区域设置影响（CA1310）
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                        continue;

                    int sep = line.IndexOf('=', StringComparison.Ordinal);
                    if (sep <= 0) continue;

                    // 用 ToUpperInvariant + Ordinal 比较：小写化在部分区域设置下有坑（土耳其语 i），
                    // 大写 + 序数比较才是做 key 归一化的安全写法（CA1308 / CA1307）。
                    string key = line.Substring(0, sep).Trim().ToUpperInvariant();
                    string value = line.Substring(sep + 1).Trim();

                    if (string.Equals(key, "STORAGECAPACITY", StringComparison.Ordinal)
                        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int capacity))
                        config.StorageCapacity = capacity;
                    else if (string.Equals(key, "STACKMULTIPLIER", StringComparison.Ordinal)
                             && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int multiplier))
                        config.StackMultiplier = multiplier;
                    else if (string.Equals(key, "DEBUG", StringComparison.Ordinal))
                        config.Debug = ParseBool(value);
                }
            }
            catch (Exception e)
            {
                DebugLog.Warn($"读取配置失败，使用默认值: {e.Message}");
            }

            config.Clamp();
            return config;
        }

        public void Save(string directory)
        {
            try
            {
                Clamp();
                string path = Path.Combine(directory, FileName);
                File.WriteAllText(path,
                    "# StorageBooster 设置\n" +
                    "# 仓库目标格数（实际生效 = max(官方容量, 本值)，只扩不缩，避免丢物品）\n" +
                    $"storageCapacity = {StorageCapacity}\n" +
                    "# 堆叠倍率（1 = 不改；不可堆叠物品不受影响）\n" +
                    $"stackMultiplier = {StackMultiplier}\n" +
                    "# Debug 模式：把本 mod 的日志单独写到 _debug 文件夹（报 bug 时很有用）\n" +
                    "# 也可以直接在 mod 目录下建一个名为 _debug 的文件夹来开启，不用改这里\n" +
                    "# 开启后需要重启游戏才会重新读取本项\n" +
                    $"debug = {(Debug ? "true" : "false")}\n");
            }
            catch (Exception e)
            {
                DebugLog.Warn($"保存配置失败: {e.Message}");
            }
        }
    }
}

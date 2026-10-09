using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using ItemStatsSystem;
using UnityEngine;

namespace StorageBooster
{
    /// <summary>
    /// 仓库扩容 + 堆叠翻倍。
    ///
    /// 加载规则：info.ini 里 name=StorageBooster -> 游戏加载 StorageBooster.dll 中的
    /// StorageBooster.ModBehaviour，并挂到一个新建的 GameObject 上。
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string HarmonyId = "com.storagebooster";

        /// <summary>全局设置，patch 里直接读这里的实时值，所以改完立刻生效。</summary>
        public static Config Settings { get; private set; } = new Config();

        private static string _configDirectory = ".";

        // 静态事件要取消订阅，必须留住同一个委托实例
        private static readonly Action<PlayerStorage.StorageCapacityCalculationHolder> CapacityHandler = OnRecalculateCapacity;

        private static bool _patched;
        private bool _uiRegistered;

        /// <summary>
        /// 官方在 AddComponent 之后、才会调 Setup(this, info)，Setup 内部转调 OnAfterSetup。
        /// 所以 Unity 的 Awake/OnEnable 里 info 还是空结构体，读不到 info.path —— 配置必须在
        /// OnAfterSetup 里加载，不能放 Awake。
        /// </summary>
        protected override void OnAfterSetup()
        {
            string directory = info.path;
            if (string.IsNullOrEmpty(directory))
                directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";

            _configDirectory = directory;

            // Debug 模式要在打第一条日志之前就绪，否则头几条会漏。
            // 分两步：先靠 _debug 文件夹定目录（不依赖 config），读完 config 再合并开关状态。
            DebugLog.Prepare(directory);
            Settings = Config.Load(directory);
            DebugLog.Init(Settings.Debug);

            DebugLog.Info($"已加载。仓库容量={Settings.StorageCapacity}，堆叠倍率={Settings.StackMultiplier}，Debug={DebugLog.Enabled}");
            DebugLog.Info($"配置文件：{Path.Combine(_configDirectory, "config.ini")}");
            DebugLog.Info($"Debug 目录：{DebugLog.DebugDirectory}");

            if (DebugLog.Enabled)
            {
                // 顺手把上次运行留在 Player.log 里的本 mod 日志捞出来
                DebugLog.CapturePlayerLog();
                DebugLog.DumpDiagnostics();
            }
        }

// CA1822：OnEnable 不访问实例数据，分析器建议标 static。
// 但 Unity 是靠反射查找**实例**方法来回调生命周期的，改成 static 后就再也不会被调用 —— 这里必须保留实例方法。
#pragma warning disable CA1822
        void OnEnable()
        {
            // 第一步：把嵌入的 0Harmony.dll 载进进程，必须早于任何 patch
            HarmonyLoad.Load0Harmony();

            // patch 放 OnEnable 而不是 Start：Unity 的 Start 一个组件只会跑一次，
            // 而玩家在 mod 管理界面里可以反复禁用/启用 —— 若 patch 只在 Start 里做，
            // 禁用（OnDisable 里 UnpatchAll）之后再次启用就再也补不回来了。
            EnsurePatched();

            // 官方给的容量计算钩子：每次重算都会先填默认值再广播这个事件，
            // 在这里改 holder.capacity 就能改仓库格数，且不会和官方升级逻辑打架。
            PlayerStorage.OnRecalculateStorageCapacity += CapacityHandler;

            // 玩家在 mod 管理界面禁用再启用时，Start 不会重跑，得在这里把设置 UI 补回来。
            // 首次启用时 info 还没赋值，这里会直接跳过，交给 Start 去做。
            TryRegisterSettingsUI();
        }
#pragma warning restore CA1822

        void Start()
        {
            EnsurePatched();          // 兜底：万一 OnEnable 时 Harmony 还没准备好
            TryRegisterSettingsUI();  // 需要 info，只能在 Setup 之后做

            // 让玩家已经打开的仓库立刻按新容量重算一次；
            // 若此刻还没进基地，进入基地时 PlayerStorage.Load() 也会重算，同样会走我们的事件。
            PlayerStorage.NotifyCapacityDirty();
        }

        void OnDisable()
        {
            PlayerStorage.OnRecalculateStorageCapacity -= CapacityHandler;

            if (_uiRegistered && !string.IsNullOrEmpty(info.name))
                ModSettingBridge.RemoveMod(info);
            _uiRegistered = false;

            Unpatch();
        }

        /// <summary>幂等地应用 patch。Harmony 的 PatchAll 重复调用会叠加，所以要有 _patched 闸门。</summary>
        private static void EnsurePatched()
        {
            if (_patched) return;

            if (HarmonyLoad.Load0Harmony() == null)
            {
                DebugLog.Error("Harmony 未就绪，堆叠 patch 不会生效。");
                return;
            }

            try
            {
                new Harmony(HarmonyId).PatchAll();
                _patched = true;
                DebugLog.Info($"patches applied: {HarmonyId}");
            }
            catch (Exception e)
            {
                DebugLog.Error($"patch 失败: {e}");
            }
        }

        private static void Unpatch()
        {
            if (!_patched) return;

            try { new Harmony(HarmonyId).UnpatchAll(HarmonyId); }
            catch (Exception e) { DebugLog.Warn($"unpatch 失败: {e.Message}"); }
            finally { _patched = false; }
        }

        /// <summary>给诊断快照用的 patch 状态。</summary>
        public static bool IsPatched => _patched;

        /// <summary>
        /// 只扩不缩：官方算出来是 200 格、配置写 400 就取 400；
        /// 反过来官方已经 600 格也不会给你砍回 400 —— 缩容会挤出物品，宁可不缩。
        /// </summary>
        private static void OnRecalculateCapacity(PlayerStorage.StorageCapacityCalculationHolder holder)
        {
            int target = Settings.StorageCapacity;
            if (holder.capacity < target)
                holder.capacity = target;
        }

        /// <summary>
        /// 幂等地注册设置 UI。
        /// 两个入口：Start（首次，此时 info 已就绪）和 OnEnable（禁用再启用时补回）。
        /// </summary>
        private void TryRegisterSettingsUI()
        {
            if (_uiRegistered) return;
            if (string.IsNullOrEmpty(info.name)) return;   // 首次 OnEnable 时 info 还是空的

            // ModSetting 可能比本 mod 晚加载，此时 AppDomain 里还查不到它。
            // 这里退避重试几次，查到了就注册，一直查不到就静默用 config.ini。
            if (!ModSettingBridge.Init())
            {
                StartCoroutine(RetryRegisterSettingsUI(10, 2f));
                return;
            }

            DoRegisterSettingsUI();
            _uiRegistered = true;
        }

        private System.Collections.IEnumerator RetryRegisterSettingsUI(int attempts, float interval)
        {
            for (int i = 0; i < attempts; i++)
            {
                yield return new WaitForSeconds(interval);
                if (_uiRegistered) yield break;            // 已经被别的入口注册过了

                if (ModSettingBridge.Init())
                {
                    DoRegisterSettingsUI();
                    _uiRegistered = true;
                    yield break;
                }
            }

            if (!_uiRegistered)
                DebugLog.Info("始终未检测到 ModSetting，游戏内设置页不可用，设置请改 config.ini。");
        }

        private void DoRegisterSettingsUI()
        {
            ModSettingBridge.AddIntSlider(info, "storageCapacity", "仓库容量（格）",
                Settings.StorageCapacity, 32, 2000, value =>
                {
                    // 滑块拖动过程中可能被高频回调，值没变就别重复写盘
                    if (value == Settings.StorageCapacity) return;

                    Settings.StorageCapacity = value;
                    Settings.Save(_configDirectory);
                    PlayerStorage.NotifyCapacityDirty();
                    DebugLog.Info($"仓库容量 -> {value}");
                });

            ModSettingBridge.AddIntSlider(info, "stackMultiplier", "堆叠倍率（1 = 不改）",
                Settings.StackMultiplier, 1, 20, value =>
                {
                    if (value == Settings.StackMultiplier) return;

                    Settings.StackMultiplier = value;
                    Settings.Save(_configDirectory);
                    DebugLog.Info($"堆叠倍率 -> {value}");
                });

            // ---- Debug 模式 ----
            ModSettingBridge.AddToggle(info, "debug", "Debug 模式（日志写到 _debug 文件夹）",
                Settings.Debug || DebugLog.EnabledByFolder, enabled =>
                {
                    Settings.Debug = enabled;
                    Settings.Save(_configDirectory);
                    DebugLog.SetEnabled(enabled);

                    // 文件夹开关优先级更高，关掉开关但文件夹还在的话下次启动又会开，得说清楚
                    if (!enabled && DebugLog.EnabledByFolder)
                        DebugLog.Warn("已关闭 Debug，但 _debug 文件夹仍然存在，下次启动会重新开启；" +
                                      "要彻底关闭请删除该文件夹。");
                });

            ModSettingBridge.AddButton(info, "dumpDiagnostics", "导出诊断快照与 Player.log 抓取结果",
                "立即导出", () =>
                {
                    // 按钮点下去时可能还没开 debug，这里保证目录存在再写
                    if (!DebugLog.Enabled) DebugLog.SetEnabled(true);
                    DebugLog.CapturePlayerLog();
                    DebugLog.DumpDiagnostics();
                });
        }
    }

    /// <summary>
    /// 堆叠上限翻倍：patch Item.MaxStackCount 的 getter。
    /// 走 getter 而不是去改每个物品实例，好处是新生成的物品自动生效、局内局外一视同仁，
    /// 且 UI 显示、合并逻辑、StackCount 的上限裁剪全都读到同一个值。
    ///
    /// 不可堆叠物品（MaxStackCount &lt;= 1，游戏里 Stackable 属性就是这么判断的）原样返回，不受倍率影响。
    /// </summary>
    [HarmonyPatch(typeof(Item), nameof(Item.MaxStackCount), MethodType.Getter)]
    public static class PatchMaxStackCount
    {
        private const int HardCap = 99999;

        static void Postfix(ref int __result)
        {
            if (__result <= 1) return;   // 不可堆叠，跳过

            long scaled = (long)__result * ModBehaviour.Settings.StackMultiplier;
            __result = (int)Math.Min(scaled, HardCap);
        }
    }
}

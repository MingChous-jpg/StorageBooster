using System;
using System.Linq;
using System.Reflection;
using Duckov.Modding;

namespace StorageBooster
{
    /// <summary>
    /// 用反射调用 ModSetting（创意工坊 id 3595729494）的公开静态方法。
    ///
    /// 之所以反射而不是直接引用 dll：反射属于弱依赖——玩家没装 ModSetting 时本 mod 照样能跑，
    /// 只是少了游戏内滑块（此时改 config.ini 即可）。
    /// 直接引用 dll 的话，ModSetting 没启用会导致本 mod 直接加载失败。
    /// </summary>
    public static class ModSettingBridge
    {
        private const string TypeName = "ModSetting.ModBehaviour";

        private static bool _warned;
        private static MethodInfo? _addSliderInt;
        private static MethodInfo? _addToggle;
        private static MethodInfo? _addButton;
        private static MethodInfo? _removeMod;

        public static bool Available { get; private set; }

        /// <summary>
        /// 探测 ModSetting 是否已加载。
        /// 注意：这里**不能**用 "只初始化一次" 的写法缓存失败结果 —— ModSetting 可能比本 mod 晚加载，
        /// 那时 AppDomain 里还没有它的程序集，必须允许重复探测，否则退避重试会永远失败。
        /// 每次都重新扫一遍（几十个 assembly 的 GetType，很轻），成功即短路。
        /// </summary>
        public static bool Init()
        {
            if (Available) return true;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? type;
                try { type = assembly.GetType(TypeName); }
                catch { continue; }

                if (type == null) continue;

                // AddSlider 有两个重载（float 版 8 参、int 版 8 参），靠第 4 个参数类型区分
                _addSliderInt = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "AddSlider"
                                         && m.GetParameters().Length == 8
                                         && m.GetParameters()[3].ParameterType == typeof(int));
                _addToggle = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "AddToggle" && m.GetParameters().Length == 5);
                _addButton = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "AddButton" && m.GetParameters().Length == 5);
                _removeMod = type.GetMethod("RemoveMod", BindingFlags.Public | BindingFlags.Static);
                Available = _addSliderInt != null;
                break;
            }

            if (!Available && !_warned)
            {
                _warned = true;
                DebugLog.Info("暂未检测到 ModSetting（可能还没加载完），稍后重试；最终不可用则请改 config.ini。");
            }

            return Available;
        }

        /// <summary>
        /// 添加整数滑块。签名：AddSlider(ModInfo, string key, string description,
        /// int defaultValue, int minValue, int maxValue, Action&lt;int&gt; onValueChange, int characterLimit)
        /// </summary>
        public static bool AddIntSlider(ModInfo info, string key, string description,
            int defaultValue, int minValue, int maxValue, Action<int> onValueChange)
        {
            if (!Init() || _addSliderInt == null) return false;

            try
            {
                _addSliderInt.Invoke(null, new object[]
                {
                    info, key, description, defaultValue, minValue, maxValue, onValueChange, 5
                });
                return true;
            }
            catch (Exception e)
            {
                DebugLog.Warn($"添加设置项 {key} 失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 添加开关。签名：AddToggle(ModInfo, string key, string description, bool enable, Action&lt;bool&gt; onValueChange)
        /// </summary>
        public static bool AddToggle(ModInfo info, string key, string description,
            bool defaultValue, Action<bool> onValueChange)
        {
            if (!Init() || _addToggle == null) return false;

            try
            {
                _addToggle.Invoke(null, new object[] { info, key, description, defaultValue, onValueChange });
                return true;
            }
            catch (Exception e)
            {
                DebugLog.Warn($"添加开关 {key} 失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 添加按钮。签名：AddButton(ModInfo, string key, string description, string buttonText, Action onClickButton)
        /// </summary>
        public static bool AddButton(ModInfo info, string key, string description,
            string buttonText, Action onClick)
        {
            if (!Init() || _addButton == null) return false;

            try
            {
                _addButton.Invoke(null, new object[] { info, key, description, buttonText, onClick });
                return true;
            }
            catch (Exception e)
            {
                DebugLog.Warn($"添加按钮 {key} 失败: {e.Message}");
                return false;
            }
        }

        /// <summary>禁用 mod 时把本 mod 的设置 UI 一并移除。</summary>
        public static void RemoveMod(ModInfo info)
        {
            if (!Init() || _removeMod == null) return;

            try
            {
                _removeMod.Invoke(null, new object[]
                {
                    info,
                    (Action<bool>)(ok => DebugLog.Info($"设置 UI 移除结果: {ok}"))
                });
            }
            catch (Exception e) { DebugLog.Warn($"移除设置 UI 失败: {e.Message}"); }
        }
    }
}

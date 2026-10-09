using System.IO;
using System.Reflection;
using UnityEngine;

namespace StorageBooster
{
    /// <summary>
    /// 从嵌入资源加载 0Harmony.dll。
    ///
    /// 游戏本体不带 Harmony，mod 必须自带。把 0Harmony.dll 嵌进程序集（而不是随 dll 一起丢文件夹）
    /// 可以让 mod 保持单文件分发。
    ///
    /// 注意：Assembly.Load(byte[]) 进的是同一个 AppDomain，所以这招只解决"分发"，
    /// 不解决"版本隔离"——多个 mod 带不同版本 Harmony 时，先加载进来的那个胜出。
    /// 因此请与社区统一版本（目前主流是 Harmony 2.4.1）。
    /// </summary>
    public static class HarmonyLoad
    {
        private static Assembly? _harmonyAssembly;

        /// <summary>已加载的 Harmony 版本（未加载时为 "未加载"），诊断快照用。</summary>
        public static string HarmonyVersion { get; private set; } = "未加载";

        /// <summary>资源名约定：{命名空间}.0Harmony.dll，与 csproj 里的 LogicalName 对应。</summary>
        public static string ResourceName => $"{typeof(HarmonyLoad).Namespace}.0Harmony.dll";

        /// <returns>加载失败（嵌入资源缺失或镜像损坏）时返回 null。</returns>
        public static Assembly? Load0Harmony()
        {
            if (_harmonyAssembly != null)
                return _harmonyAssembly;

            Assembly self = Assembly.GetExecutingAssembly();
            using (Stream stream = self.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    DebugLog.Error($"找不到嵌入资源 {ResourceName}。" +
                                   "检查 csproj 里的 EmbeddedResource / LogicalName 是否与 RootNamespace 一致。");
                    return null;
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    _harmonyAssembly = Assembly.Load(ms.ToArray());
                }
            }

            HarmonyVersion = _harmonyAssembly.GetName().Version?.ToString() ?? "未知";
            DebugLog.Info($"0Harmony 已加载，版本 {HarmonyVersion}");
            return _harmonyAssembly;
        }
    }
}

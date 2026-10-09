# lib/0Harmony.dll

游戏本体不带 Harmony，所以构建本 mod 必须有一份 `0Harmony.dll`。

## 这份文件的来源

- **版本**：HarmonyX **2.4.1.0**
- **取自**：Steam 创意工坊《逃离鸭科夫》的一个 mod（`workshop/content/3167020/3588386576`），
  即社区已经在用的同一份，不是从 NuGet 拉的。
- **许可**：HarmonyX 沿用 Harmony 的 **MIT** 许可，随项目分发没问题。

## 为什么不走 NuGet

NuGet 上正确的包名是 **`HarmonyX`**（不是 `HarmonyLib`，`HarmonyLib` 装不上）。
但游戏运行时是 Mono/Unity 环境，直接用本地这份 dll 更贴近实际运行条件，也让仓库开箱即可构建，
不必配 NuGet 源。

## 运行时怎么用

`0Harmony.dll` 不是作为散文件放进 mod 目录，而是被 **嵌进程序集**：

- `StorageBooster.csproj` 里同时声明了 `<Reference Include="lib\0Harmony.dll" Private="False" />`
  和 `<EmbeddedResource Include="lib\0Harmony.dll" LogicalName="StorageBooster.0Harmony.dll" />`。
- 运行时由 `HarmonyLoad.cs` 用 `Assembly.Load(byte[])` 从自身资源里加载。

所以最终分发给用户的是**单个 dll**，mod 目录里不会出现 `0Harmony.dll`。

## 版本共存

`Assembly.Load(byte[])` 进的是同一个 AppDomain，**先加载者胜出** —— 这招解决"分发"，
不解决"版本隔离"。好在 0Harmony 是**非强名**程序集，按简单名绑定、不校验版本，
而 2.3.x 与 2.4.x 在本 mod 用到的 API 范围内完全一致，混装不会出问题。

> 注意：`LogicalName` 里的 `StorageBooster.` 前缀取自 `$(RootNamespace)`。
> 如果你改了 csproj 的 `RootNamespace`，必须同步改 `LogicalName`，否则运行时找不到资源。

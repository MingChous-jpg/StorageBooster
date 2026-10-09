# 鸭窝扩容 (StorageBooster) — 开发文档

面向**开发者**的实现说明。玩家请看 [README.md](README.md)。

《逃离鸭科夫》(Escape from Duckov, appid 3167020) 的 mod：仓库扩容 400 格 + 物品堆叠上限 ×2，均可配置。

---

## 两种配置方式

### 1. config.ini（无前置依赖）

首次启动游戏后，mod 目录会自动生成 `config.ini`：

```ini
# StorageBooster 设置
# 仓库目标格数（实际生效 = max(官方容量, 本值)，只扩不缩，避免丢物品）
storageCapacity = 400
# 堆叠倍率（1 = 不改；不可堆叠物品不受影响）
stackMultiplier = 2
# Debug 模式：把本 mod 的日志单独写到 _debug 文件夹（报 bug 时很有用）
# 也可以直接在 mod 目录下建一个名为 _debug 的文件夹来开启，不用改这里
# 开启后需要重启游戏才会重新读取本项
debug = false
```

改完 **重启游戏** 生效。范围：`storageCapacity` 32–5000，`stackMultiplier` 1–100，
超出会自动夹取。`debug` 是宽松布尔，认 `1/true/yes/on/enabled`（大写不敏感）。

### 2. ModSetting 游戏内滑块（可选）

装了 **ModSetting**（创意工坊 id `3595729494`）后，游戏内 mod 设置页会出现两个滑块，
拖动即刻生效，改动会回写到 `config.ini`。

没装 ModSetting 也完全能跑，只是少了滑块。本 mod 通过**反射**调用 ModSetting，
属于弱依赖，不会因为没装而加载失败。

### 3. Debug 模式（`_debug` 文件夹）

三种开启方式，**文件夹优先级最高**：

| 来源 | 判定 | 生效时机 |
| --- | --- | --- |
| `_debug` 文件夹存在 | `Directory.Exists(mod/_debug)` | 启动时，重启生效 |
| `config.ini` 的 `debug = true` | 宽松解析 `1/true/yes/on/enabled` | 启动时，重启生效 |
| ModSetting 的 toggle | `DebugLog.SetEnabled(bool)` | 即时 |

关掉 toggle 但文件夹还在时，会打一条 WARN 提示"下次启动会重新开启，要彻底关闭请删文件夹"，
不静默。

---

## 实现要点

### 仓库容量：用官方钩子，不硬改私有字段

反编译 `PlayerStorage` 可见官方每次重算容量的流程：

```csharp
public static int RecalculateStorageCapacity()
{
    if (Instance == null) return 0;
    var holder = new StorageCapacityCalculationHolder();
    holder.capacity = Instance.DefaultCapacity;   // 先填默认
    OnRecalculateStorageCapacity?.Invoke(holder); // 再广播给 mod 改
    Instance.SetCapacity(holder.capacity);
    needRecalculateCapacity = false;
    return holder.capacity;
}
```

所以只要订阅 `PlayerStorage.OnRecalculateStorageCapacity` 改 `holder.capacity` 就行，
不用去反射改 `Inventory.defaultCapacity`（社区里 BackpackBooster 是这么做的），
也不会和官方的仓库升级逻辑互相覆盖。

修改策略是**只扩不缩**：`holder.capacity < 配置值` 时才抬上去。
官方已经算出 600 格时不会砍回 400 —— 缩容会挤出物品，宁可不缩。

> `PlayerStorage` 在**全局命名空间**，不在 `Duckov` 下。写 `Duckov.PlayerStorage` 会报 CS0234。

### 堆叠上限：patch `Item.MaxStackCount` 的 getter

```csharp
[HarmonyPatch(typeof(Item), nameof(Item.MaxStackCount), MethodType.Getter)]
static void Postfix(ref int __result)
{
    if (__result <= 1) return;              // 不可堆叠，跳过
    long scaled = (long)__result * Settings.StackMultiplier;
    __result = (int)Math.Min(scaled, 99999);
}
```

patch getter 而不是遍历改每个物品实例的好处：

- 新生成的物品自动生效
- 局内 / 局外一视同仁（同一个属性）
- UI 显示、合并上限裁剪（`StackCount` setter 里 `num = MaxStackCount`）、
  `Stackable` 判定全都读到同一个值，不会出现"能堆 100 但 UI 只让堆 50"的错位

已确认游戏里**没有**任何路径会把 `StackCount` 初始化成 `MaxStackCount`，
所以翻倍只是抬高天花板，不会顺带把刷出来的数量也翻倍。

### 生命周期坑：`info` 在 Awake 里是空的

`ModManager.ActivateMod` 的顺序是：

```
gameObject.AddComponent(type)   // ← Awake / OnEnable 在这里同步跑完
modBehaviour.Setup(this, info)  // ← 这里才赋值 info，并转调 OnAfterSetup()
```

所以 `Awake()` / `OnEnable()` 里读 `info.path` 拿到的是空字符串。
读配置、注册设置 UI 这类需要 `info` 的操作，必须放到 **`OnAfterSetup()`** 或 `Start()` 里。

### Harmony 分发

`lib/0Harmony.dll`（HarmonyX 2.4.1，从创意工坊现有 mod 拷的）作为
`EmbeddedResource` 嵌进程序集，运行时 `Assembly.Load(byte[])` 载入，mod 保持单文件分发。

注意 `Assembly.Load(byte[])` 进的是同一个 AppDomain，**先加载者胜出** ——
这招只解决"分发"，不解决"版本隔离"。好消息是 0Harmony 非强名，按简单名绑定，
且 2.3.x 与 2.4.x 的 API 在我们用到的范围内完全一致，实际不会出问题。

### Debug 日志子系统（`DebugLog.cs`）

单一出口：全项目**除 `DebugLog.cs` 自身外不允许出现 `Debug.Log*`**（`grep -n "Debug\.Log" *.cs`
应只命中 `DebugLog.cs`）。`DebugLog.cs` 内部保留原始 `Debug.Log*` 是因为它就是漏斗本身
——先写 Unity 控制台（不开 debug 也能在 `Player.log` 里看到），再按开关写文件；
把它自己转成 `DebugLog.*` 会无限递归。

几个不那么显然的设计点：

- **`Prepare` / `Init` 两步初始化。** `OnEnable` 早于 `OnAfterSetup`（见上文生命周期坑），
  所以目录只能等到 `OnAfterSetup` 才知道；但 `Config.Load` 的失败日志又必须能进调试文件。
  于是拆成 `Prepare(dir)`（定目录 + 探测文件夹，不写盘）→ `Config.Load` → `Init(config.Debug)`（合并开关）。
- **启动缓冲。** `OnEnable` 里 `HarmonyLoad.Load0Harmony()` 打的日志早于 debug 就绪，
  会被 `Pending` 列表（上限 256 行）缓存，`SetEnabled(true)` 时 `FlushPending()` 补写进文件，
  避免"最关键的那几行反而没有"。
- **`FileShare.ReadWrite` 读 `Player.log`。** 游戏进程持有该文件的写句柄，
  默认 `File.OpenRead` 会抛 `IOException`。
- **轮转 + 清理。** 主日志超过 2MB 归档为 `StorageBooster_{ts}.log`；
  `diagnostics_*.txt` 与 `PlayerLog_*.log` 各只留最近 5 份（名字带 `yyyyMMdd-HHmmss`，
  字典序即时间序，排序后删最旧的），否则每次启动都堆一份会滚很大。
- **写日志绝不反向崩 mod。** 所有文件操作都在 `try/catch` 里，失败只打 WARN 到控制台。

诊断快照 `DumpDiagnostics()` 的内容：Unity / 产品 / 平台 / `dataPath` / `consoleLogPath`、
mod 目录、配置实际值、Harmony 版本、`IsPatched`、`ModSettingBridge.Available`、
`PlayerStorage.Instance` 是否就绪及其 `DefaultCapacity` 与 `Inventory.Capacity`、
以及过滤掉官方程序集后的已加载程序集列表（看同进程里还跑了哪些 mod）。

---

## 构建与部署

```bash
cd "F:/Vibe Coding/workbuddy/duckov-mod/StorageBooster"
dotnet build StorageBooster.csproj -c Release
```

csproj 里的 `PackModFolder` target 会自动整理出
`bin/Release/Mod/StorageBooster/{StorageBooster.dll, info.ini, preview.png}`，
整个文件夹复制到游戏 `Duckov_Data\Mods\` 下即可。

Mac 路径为 `Duckov/Duckov.app/Contents/Mods/`。

默认 `DuckovPath` 指向 `E:\SteamLibrary\steamapps\common\Escape from Duckov`，
你的游戏装在别处就覆盖一下：

```bash
dotnet build -c Release -p:DuckovPath="D:\Games\Escape from Duckov"
```

## 仓库结构

```
.
├── ModBehaviour.cs       # mod 入口 + 容量钩子 + MaxStackCount patch
├── Config.cs             # config.ini 读写（含 debug 项）
├── DebugLog.cs           # Debug 模式：单一日志出口、日志抓取、诊断快照
├── HarmonyLoad.cs        # 从嵌入资源加载 0Harmony.dll
├── ModSettingBridge.cs   # 反射调用 ModSetting（弱依赖）
├── StorageBooster.csproj
├── info.ini              # 游戏读取的 mod 元数据
├── preview.png           # 创意工坊封面
├── lib/
│   ├── 0Harmony.dll      # 编译引用 + 嵌入资源（HarmonyX 2.4.1，MIT）
│   └── README.md         # 这份 dll 的来源与用法说明
└── tools/                # 封面图生成脚本，与 mod 运行无关
    ├── make_preview.py
    ├── make_preview.ps1
    └── make_preview_ai.py
```

> `lib/0Harmony.dll` 是**必须**的，没有它编不过；它被嵌进程序集，所以发布时不需要单独分发。

## 与其它 mod 的兼容性（本机 43 个订阅 + 2 个本地，已逐个扫过）

扫描方式：扫每个 mod 主程序集的元数据字符串（成员名 UTF-8 / 字面量 UTF-16），
找出碰 `PlayerStorage` 容量或 `Item.MaxStackCount` 的，再反编译确认具体做法。

| mod | 它做了什么 | 是否冲突 |
|---|---|---|
| **BackpackBooster** 背包增强 | 反射改**背包物品**的 `Inventory.defaultCapacity` ×2 | ❌ 不冲突。改的是背包容器，不是玩家仓库 |
| **IndependentStash** 独立仓库 | 自建 `Inventory`（容量 5000），只把交互组挂到 PlayerStorage 旁边 | ❌ 不冲突。只读 PlayerStorage，不改其容量 |
| **ItemFram** 自定义物品框架 | `SetPrivateField(item, "maxStackCount", N)` 给自定义物品设值；另有 `Item.Combine` 的 Prefix | ❌ 不冲突。它设字段值，我们的 getter 在其之上 ×2；它的 Combine Prefix 读的也是 patched 后的 `MaxStackCount`，合并上限一致 |
| **VAE 武器改装** | 自建隐藏 `Inventory.SetCapacity(5000)` | ❌ 不冲突 |
| **DuckovCustomMap / 生化泄露区 / 学园终端** | patch 目标都是 AI、Buff、场景、UI 之类 | ❌ 不冲突 |
| 仙人资源 / 武器战利品箱 / 莉可丽丝等物品包 | 给自定义物品设 `maxStackCount` 字段 | ❌ 不冲突，会被一视同仁地 ×2 |
| **ModSetting** | 提供设置 UI | ✅ 依赖它（弱依赖，可选） |

**结论：没有任何其它 mod 订阅 `PlayerStorage.OnRecalculateStorageCapacity`，也没有任何其它 mod patch `Item.MaxStackCount` 的 getter。** 两条改动都是独占的。

**0Harmony 版本**：本机 13 份，2.4.1 ×11 / 2.3.3 ×2，全部**非强名**（无 public key token），
按简单名绑定不校验版本，且我们用到的 API 在 2.3.x 与 2.4.x 完全一致 → 混装无风险。

### 一个需要知道的行为影响（不是 bug）

`DuckovCustomMap` 等 mod 生成物品时写 `StackCount = Mathf.Min(count, MaxStackCount)`。
我们的翻倍会**抬高这个 clamp 的天花板**，所以这类来源的一次性掉落数量可能变多
（原本一次最多给 60 发、上限翻倍后可能给满 100）。这是"堆叠上限翻倍"的必然推论，
不是冲突。若不想让掉落变多，只能改这些 mod 的生成逻辑。

## 静态检查

`dotnet build -c Release -p:AnalysisMode=All -p:WarningLevel=9999` → **0 警告 0 错误**。
（`CA1031` 捕获通用异常、`CA1822` Unity 生命周期方法、`CA1707` patch 类名下划线 属有意保留，已在代码中注释说明。）

期间修掉 3 个真问题：

1. **`ModSettingBridge.Init()` 缓存了失败结果** —— 原本用 `_initDone` 一次性置位，
   导致后来加的"晚加载退避重试"永远失败（第二次调用直接返回缓存的 false）。
   改成每次重新探测、成功即短路。
2. **禁用再启用后 patch 丢失** —— `PatchAll()` 原本只在 `Start()` 里做，而 Unity 的 `Start`
   一个组件只跑一次；`OnDisable` 里 `UnpatchAll` 之后再次启用就补不回来了。
   改为 `OnEnable` 里走 `EnsurePatched()` 幂等闸门（`PatchAll` 重复调用会叠加，必须加闸门）。
3. **禁用再启用后设置 UI 丢失** —— 同理，`Start` 不重跑导致 UI 注册不回来。
   改为 `OnEnable` 也尝试注册，用 `_uiRegistered` 做幂等。
4. **DebugLog 的 nullable 报警**（`CS8602` / `CS8603`）—— `Nullable=enable` 下
   `PlayerStorage.Instance`、`Application.consoleLogPath`、`_writer` 都可能为 null，
   已全部改成可空类型 + 局部空判断（诊断快照里读 `Instance` 时先存局部变量再判空，
   否则编译器不认 `if (Instance != null)` 之后的 `Instance.Xxx`）。

## 验证

### 常规

进游戏后看 `Player.log`（`C:\Users\<你>\AppData\LocalLow\Team Soda\Escape from Duckov\Player.log`），
搜 `StorageBooster`，正常应该看到：

```
[StorageBooster] 0Harmony 已加载，版本 2.4.1.0
[StorageBooster] 已加载。仓库容量=400，堆叠倍率=2
[StorageBooster] patches applied: com.storagebooster
```

进基地打开仓库，格数应为 400；物品悬停时应显示翻倍后的堆叠上限。

### Debug 模式

1. 在 mod 目录建 `_debug` 空文件夹 → 重启游戏
2. `_debug/StorageBooster.log` 应包含（且含 `OnEnable` 阶段补写的 Harmony 那行）：

```
# 以下为 debug 就绪前缓冲的日志（OnEnable 阶段）
2026-10-09 22:33:11.204 [INFO ] 0Harmony 已加载，版本 2.4.1.0
2026-10-09 22:33:11.208 [INFO ] Debug 模式已开启（由 _debug 文件夹触发）
2026-10-09 22:33:11.209 [INFO ] 已加载。仓库容量=400，堆叠倍率=2，Debug=True
```

3. `diagnostics_*.txt` 里 `Patch 已应用` 应为 `True`，`PlayerStorage` 在进基地后应为 `已就绪`
4. 装了 ModSetting 时点「立即导出」，应立刻新生成一份 `PlayerLog_*.log` + `diagnostics_*.txt`
5. 连续启动 6 次后，`_debug` 里最多只剩 5 份 `diagnostics_*` 和 5 份 `PlayerLog_*`（清理生效）

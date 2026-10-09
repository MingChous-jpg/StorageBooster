<div align="center">

# 鸭窝扩容

### Storage Booster

搜刮回来的每一块电路板，都该有个塞得下的窝。

[![Game](https://img.shields.io/badge/game-Escape%20from%20Duckov-4A9E5C)](https://store.steampowered.com/app/3167020)
[![Version](https://img.shields.io/badge/compatible-v2.3.30-4A9E5C)](#)
[![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS-blue)](#安装)
[![Requires](https://img.shields.io/badge/requires-none%20(Harmony%20bundled)-brightgreen)](#安装)
[![License](https://img.shields.io/badge/license-MIT-green)](#许可)

<img src="preview.png" alt="鸭窝扩容" width="256">

</div>

---

## 它做什么

| 项目 | 原版 | 装了这个 mod | 可调范围 |
| --- | --- | --- | --- |
| 基地仓库容量 | 32 格起 | **400 格** | 32 – 5000 |
| 物品堆叠上限 | 原值 | **×2** | ×1 – ×100 |

- **局内局外一视同仁。** 背包、仓库、战利品箱读的是同一个属性，不会出现「仓库里能堆 100、背包里只能堆 50」的错位。
- **不可堆叠物品完全不受影响。** 枪械、护甲这类 `MaxStackCount ≤ 1` 的东西原样返回，不会被强行改成可堆叠。
- **容量只扩不缩。** 如果你已经通过官方升级把仓库撑到 600 格，mod 不会把它砍回 400 —— 缩容会挤出物品，宁可不缩。

---

## 安装

**方式 A：Steam 创意工坊（推荐）**

订阅后自动生效，在游戏主菜单的 Mod 管理里勾选启用即可。

**方式 B：手动安装**

1. 下载 Release 包，解压出 `StorageBooster` 文件夹。
2. 整个文件夹放进游戏的 Mods 目录：

   | 系统 | 路径 |
   | --- | --- |
   | Windows | `Steam\steamapps\common\Escape from Duckov\Duckov_Data\Mods\` |
   | macOS | `~/Library/Application Support/Steam/steamapps/common/Escape from Duckov/Duckov.app/Contents/Mods/` |

3. 启动游戏，在 Mod 管理里启用。

最终目录结构应该是这样，缺一不可：

```
Duckov_Data/Mods/StorageBooster/
├── StorageBooster.dll
├── info.ini
└── preview.png
```

> **无需任何前置 mod。** Harmony 已打包进 dll，不污染游戏目录，也不和其他 mod 抢版本。

---

## 配置

### 方式 A：改配置文件（无前置依赖，推荐）

首次启动后，mod 目录会自动生成 `config.ini`，直接用记事本改：

```ini
# 仓库目标格数（实际生效 = max(官方容量, 本值)）
storageCapacity = 400
# 堆叠倍率（1 = 不改；不可堆叠物品不受影响）
stackMultiplier = 2
# Debug 模式（日志写到 _debug 文件夹）
debug = false
```

改完**重启游戏**生效。

| 键名 | 默认值 | 合法范围 | 说明 |
| --- | --- | --- | --- |
| `storageCapacity` | `400` | 32 – 5000 | 仓库格数。超出范围会自动夹取 |
| `stackMultiplier` | `2` | 1 – 100 | 堆叠倍率。`1` = 不改；`2` = 翻倍 |
| `debug` | `false` | `true` / `false` | Debug 模式。也认 `1` / `yes` / `on` / `enabled` |

### 方式 B：游戏内滑块（可选）

装了 [ModSetting](https://steamcommunity.com/sharedfiles/filedetails/?id=3595729494) 后，Mod 设置页会出现两个滑块，拖动**即时生效**，改动自动回写到 `config.ini`。

没装 ModSetting 也完全能跑，只是少了滑块 —— 本 mod 通过反射调用它，属于弱依赖，不会因为没装而加载失败。

---

## Debug 模式（排错用）

出问题时开这个，比在几千行 `Player.log` 里翻要快得多。开了之后 mod 会在自己的目录下建一个 **`_debug`** 文件夹，把**只属于本 mod 的日志**单独写进去。

**开启方式**（任选其一，第一种最省事）：

| 方式 | 操作 | 是否需要重启 |
| --- | --- | --- |
| 建文件夹 | 在 mod 目录下新建一个名为 `_debug` 的空文件夹 | 是 |
| 改配置 | `config.ini` 里写 `debug = true` | 是 |
| 游戏内开关 | 装了 ModSetting 后，设置页里打开「Debug 模式」 | 否，即时生效 |

> 三种方式里**文件夹优先级最高**：只要 `_debug` 文件夹存在，就会被强制开启，不用管配置里写的是什么。
> 反过来，关掉开关但文件夹还在，下次启动又会开 —— 要彻底关闭请删掉该文件夹。

**关闭方式**：删掉 `_debug` 文件夹，并确保 `config.ini` 里 `debug = false`。

### 开启后能得到什么

```
Duckov_Data/Mods/StorageBooster/
├── _debug/
│   ├── StorageBooster.log              ← 本 mod 的完整日志（滚动追加，超过 2MB 自动归档）
│   ├── PlayerLog_20261009-223311.log   ← 从 Player.log 里筛出来的本 mod 历史日志
│   └── diagnostics_20261009-223311.txt ← 诊断快照：环境 / 配置 / 运行状态 / 已加载 mod
├── StorageBooster.dll
├── info.ini
└── preview.png
```

- **`StorageBooster.log`** —— 本 mod 自己打的每一条日志，含加载顺序、patch 结果、每次改设置。
- **`PlayerLog_*.log`** —— 启动时自动从 `Player.log` 抓一遍含 `[StorageBooster]` 的行，省得手动翻。
- **`diagnostics_*.txt`** —— 一份快照，报 bug 时贴这个就够：Unity 版本、游戏目录、配置实际值、Harmony 版本、patch 是否生效、ModSetting 是否可用、当前仓库实际格数、以及同进程里加载了哪些 mod。

每次启动最多各留 5 份快照 / 抓取结果，旧的自动清理，不会越滚越大。

没装 ModSetting 的话，改完 `config.ini` 或建好文件夹后**重启一次游戏**，文件就会出现在 `_debug` 里；装了 ModSetting 的话，设置页还有个「立即导出」按钮，不用重启就能现场导出一份。

---

## 兼容性

已在 43 个创意工坊 mod + 2 个本地 mod 的环境下逐个排查，**无冲突**：

| mod | 是否冲突 |
| --- | --- |
| BackpackBooster（背包增强） | 否，它改的是背包容器，不是玩家仓库 |
| IndependentStash（独立仓库） | 否，它自建 Inventory，只读 PlayerStorage |
| ItemFram（自定义物品框架） | 否，它的合并逻辑读的也是翻倍后的值，行为一致 |
| VAE 武器改装 / 各种物品包 / 地图 mod | 否，改的不是同一处 |

技术上值得放心的两点：

- 容量走的是**官方提供的钩子** `PlayerStorage.OnRecalculateStorageCapacity`，不是反射硬改私有字段，不会和游戏的仓库升级逻辑互相覆盖。
- 堆叠改的是 `Item.MaxStackCount` 的**属性 getter**，新生成的物品自动生效，UI 显示、合并上限、`Stackable` 判定全都读到同一个值。

### 一个需要知道的行为变化

部分 mod / 游戏逻辑在生成物品时会写成 `StackCount = Min(数量, MaxStackCount)`。堆叠上限翻倍后，这个上限也被抬高，所以**一次性掉落的数量可能变多**（例如原本一次最多给 60 发子弹，翻倍后可能给满 100）。

这是「堆叠上限翻倍」的必然推论，不是 bug。如果你只想要格子变多、不想让掉落变多，把 `stackMultiplier` 设为 `1` 即可。

---

## 常见问题

<details>
<summary>改了 config.ini 没生效</summary>

需要**重启游戏**。配置文件只在 mod 加载时读取一次。

</details>

<details>
<summary>装了之后仓库还是 32 格</summary>

更省事的办法是开 [Debug 模式](#debug-模式排错用)：在 mod 目录下建一个 `_debug` 文件夹再重启游戏，`_debug/diagnostics_*.txt` 里会直接告诉你 Harmony 版本、patch 是否生效、当前仓库实际格数。

想手动看的话，`Player.log`（Windows 在 `C:\Users\<用户名>\AppData\LocalLow\Team Soda\Escape from Duckov\Player.log`）里搜 `StorageBooster`，正常应该有三行：

```
[StorageBooster] 0Harmony 已加载，版本 2.4.1.0
[StorageBooster] 已加载。仓库容量=400，堆叠倍率=2
[StorageBooster] patches applied: com.storagebooster
```

如果第一行缺失，说明 Harmony 没载入；如果第三行缺失，说明 patch 失败。把日志（或诊断快照）贴到 issue 里。

</details>

<details>
<summary>怎么上报 bug / 该贴什么</summary>

最有用的是这三样，按顺序贴：

1. `_debug/diagnostics_*.txt`（开 Debug 模式后自动生成）
2. `_debug/StorageBooster.log`
3. 复现步骤 + 游戏版本（`Info.ini` 里的 `version`）

没有 Debug 文件也行，把 `Player.log` 里搜 `StorageBooster` 得到的行贴出来即可。

</details>

<details>
<summary>怎么卸载 / 停用</summary>

**先把超出的物品取出来再停用。** 仓库容量回到官方值后，超出格子的物品会被挤出，这是游戏本身的行为，mod 无法拦截。

配置文件 `config.ini` 不会被自动删除，删掉它下次启动会重新生成一份默认值。

</details>

<details>
<summary>会不会影响存档 / 成就</summary>

不影响。mod 只改内存中的容量与堆叠上限计算，不碰存档格式。卸载后存档照常读取。

</details>

<details>
<summary>可以和其他扩容 mod 一起用吗</summary>

可以，但容量取的是「最大值」—— 别人的 mod 给 500、你设 400，最终是 500。本 mod 不会把别人的覆盖掉。

</details>

---

## 更新日志

### 1.1.0

- 新增 **Debug 模式**：在 mod 目录下建 `_debug` 文件夹即开启，日志与诊断快照单独落盘
- 自动从 `Player.log` 抓取本 mod 的历史日志，不用手动翻几千行
- 新增诊断快照导出（ModSetting 下有「立即导出」按钮）
- 全部日志统一走单一出口，不开 Debug 时行为不变

### 1.0.0

- 仓库容量默认扩至 400 格（可配置 32–5000）
- 物品堆叠上限翻倍（可配置 ×1–×100），不可堆叠物品不受影响
- 支持 `config.ini` 与 ModSetting 游戏内滑块两种配置方式
- 容量只扩不缩，避免缩容挤出物品

---

## 许可

MIT。Harmony（HarmonyX 2.4.1）随 dll 内置分发，遵循其自身许可。

---

## 附：Steam 创意工坊描述

创意工坊不支持 Markdown，工坊描述可直接粘贴下面这段（BBCode）：

```bbcode
[h1]鸭窝扩容 | Storage Booster[/h1]
仓库不够放？东西堆不下？这个 mod 把基地仓库扩到 400 格，并把所有可堆叠物品的上限翻倍。

[quote]
[list]
[*]仓库容量：32 格 → [b]400 格[/b]（可设置 32–5000）
[*]堆叠上限：[b]×2[/b]（可设置 ×1–×100），枪械、护甲等不可堆叠物品不受影响
[*]局内局外一视同仁，不会出现仓库能堆、背包不能堆的错位
[*]容量只扩不缩，不会把你已有的东西挤出去
[/list]
[/quote]

[h2]怎么改设置[/h2]
两种办法，任选其一：
[list]
[*]改 mod 目录下的 config.ini（改完重启游戏）
[*]装了 ModSetting 后，在游戏内 Mod 设置页拖动滑块，即时生效
[/list]

[h2]出问题了怎么办[/h2]
在 mod 目录下新建一个名为 [b]_debug[/b] 的空文件夹，重启游戏。
mod 会把只属于自己的日志写进 _debug/StorageBooster.log，
并导出一份诊断快照 _debug/diagnostics_*.txt（含 Harmony 版本、patch 状态、当前仓库格数等），
报 bug 时把这两个文件贴出来就行，不用去几千行的 Player.log 里翻。

[h2]依赖[/h2]
[b]无。[/b] Harmony 已内置，不需要装任何前置 mod。
ModSetting 是可选的，装了才有游戏内滑块，不装也能正常用。

[h2]兼容性[/h2]
已在 43 个创意工坊 mod 的环境下排查，与背包增强、独立仓库、自定义物品框架等均无冲突。
容量走的是官方提供的钩子，不与其他扩容 mod 互相覆盖（取最大值）。

[h2]注意[/h2]
停用前请先把超出 32 格的物品取出来 —— 容量回到官方值后，超出的物品会被挤出，这是游戏本身的行为。
```

---

<div align="center">

开发者向的实现说明见 [DEVELOPMENT.md](DEVELOPMENT.md)。

</div>

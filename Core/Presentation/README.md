# 表现层（Core/Presentation）约定

> **战斗挂载点总表**（本体节点树 ↔ 我们挂了什么 ↔ 每处位置怎么调）见 [战斗挂载点.md](战斗挂载点.md)。

## 目录与职责

| 目录 | 放什么 |
|---|---|
| `Support/` | 跨文件小工具：`NodeSearch`（找后代节点）/ `UiFactory`（Label·扁平按钮）/ `GodotWait`（等待）/ `NodeLifetime`（判活）/ `UiGeometry`（缩放换算） |
| `Combat/` | 战斗内表现：领域层、意图卡带（`IntentBelt/`）、时空裂隙按键与动画（`Gap/`）、能量计数器（`EnergyCounter/`）、刷新入口（`CombatUiRefresh` / `ExtraHandRefresh` / `DeckButtonIcons` / `AeonglassStasisPileFlash`） |
| `SecondaryResource/` | 沙砾计数器 / 5 层沙漏艺术 / 卡面沙砾费用 |
| ~~`ControlPanel/`~~ | **2026-10-16 已整块删除**（战斗内调试面板：外壳 + `ControlPage` 基类 + 按机制分页） |
| `Character/` | 角色外观：战斗 / 商店 / 火堆 / 选人背景 / 骨架特效驱动 |
| `Art/` | 卡框材质与调色板（改色只改 `AeonglassFramePalette`） |
| `Animations/` | 骨架动画名 / trigger 名常量表 |

## 命名规则

- **与本角色 / 本 mod 绑定的运行期脚本**：`Aeonglass*` 前缀（节点脚本、机制 UI）。
- **通用件**：职责名 + 固定后缀 —— `*Host`（往场景插节点的静态挂载）、`*Refresh`（主动刷新入口）、
  `*Art`（纯美术拼装）、`*Vfx`（特效驱动）、`*Ui`（挂在 `.tscn` 上的 Control 脚本）；基类/工具（`ControlPage`、`Support/*`）不加前缀。
- **类名 = 文件名**（Godot 约定，一脚本一文件）；同一个类的分册用 `ClassName.Role.cs`（如 `AeonglassIntentBelt.PileChips.cs`），
  且该类的声明必须是 `partial`。
- **控制面板已于 2026-10-16 删除**（12 个文件 + `CombatUiMountPatch` 的挂载行）。原 9 页能力 =
  各机制手动测试、外观实时调参、详细日志开关；删除后的替代：**机制测试走控制台**、
  **日志开关改设置 json**（`DebugVerboseLogs` / `DebugIntentBeltLog`，改完立刻生效）、外观调参回落到常量/资源文件。
  `AeonglassSetting.ControlPanel` 与 `Scale` / `ScaleXPositive` 仍是**已存盘的键**（属性保留、代码里已无读取处）。

## 注释骨架（每个文件顶部，**≤15 行**）

```
/// <summary>【一行定位】这个文件是什么。</summary>
/// <remarks>
/// ① 职责：它做什么（不做什么）。
/// ② 挂点与时机：挂在哪个 tscn / 谁在什么时候调用。
/// ③ 依赖：场景节点路径、原版控件、Const.Paths、机制层入口。
/// ④ ⚠️ 注意：时序 / 生命周期 / 重入；"调参改这里"的入口；用户口径沿革压成一行（带日期）。
/// </remarks>
```

## 待办（从代码注释里搬出来的 ⚠️ 条目，信息不丢）

### 意图卡带（2026-10-15 改造，已落地）

- **刷新模型**：系统层 `EnemyIntentState.Revision`（写入口 `IntentOps.Touch`，粗粒度：一个 public 写方法一次）
  \+ 表现层廉价指纹（计数 / 手牌哈希 / pending / 选中 / 指向模式，零字符串零 LINQ）；
  两者任一变化才走**增量更新**：按 identity 复用真卡视图（同一张只刷数字，换牌才 `Rebind` 重设 `NCard.Model`，
  只换顺序时连 Rebind 都不用），多余槽位 `Park()`（留在树里、占位归零，不产生孤儿节点）。
- **每帧成本**：`SyncPosition` 只在锚点位置/尺寸变化时才写，行宽缓存到内容变化后；卡带不可见时整段跳过；
  另有 1 s 一次的"只刷数字"兜底（力量/易伤这类加成改数字但状态没变，指纹抓不到）。
- **悬停（全套真卡式）**：金色背光（复制一份 `%Highlight`，材质**深拷贝**）+ 放大 1.10 + 上抬 6 + 轻微倾斜 ±3°，
  进 0.08 s / 出 0.5 s Expo-out（照本体 `NPreviewCardHolder`）；红=将打出、青=我们选中、金=悬停三档互不覆盖；
  悬停调参全在 `Combat/IntentBelt/AeonglassIntentCardView.cs` 顶部常量。
- **调试日志**：卡带刷新日志默认关，需要时把设置 json 里的 `DebugIntentBeltLog` 打开（设置页目前只留「关于」）。

- **坍缩 / 唤回按键（时空裂隙）**（用户 2026-10-13 记录）：① 补按钮**图标**；② 加**按下特效**；③ 加**"可用态"特效**
  （现在只有原版"禁用 / 可用"两档）。调参入口：`Combat/Gap/AeonglassGapActionTexts.cs`。
- **领域层**：贴图 = `STS2_Aeonglass/images/ui/domain/aeonglass_domain_layer.png`（**2768×1296**）。
  来历（2026-10-15 用户口径更正）：原本就是接 `images/combat/vantom_bg_bottom.png` 那张（不是误接），
  现在把它**改名**成领域层的文件名搬了过来（`images/combat/` 已空），并已 `--import` + 重导 PCK。
  要换图：同名覆盖即可（`Layer` 节点是全屏 `anchors_preset = 15`；蒙版几何在场景 shader 里按 `canvas_size` 算，与贴图分辨率无关）。
- **沙砾计数器 / 卡框 / 各场景空槽图**：一律"透明空槽 + 同名覆盖"，不需要改代码。

  **美术空槽一览**（2026-10-15 把 `Const.cs` 里 0 引用的路径常量删掉、清单搬到这里 —— 这些文件由
  `.tscn` / 动画直接引用，代码里不再出现路径）：

  | 文件 | 用途 |
  |---|---|
  | `images/character/aeonglass_select_background.png` | 选人界面背景（1920×1080） |
  | `images/ui/aeonglass_transition.png` | 转场遮罩（2560×1200 透明空槽） |
  | `images/ui/sand_counter/aeonglass_sand_counter_layer_4_stream.png` | 沙砾计数器 L4 颈部流沙（靠 Y 向滚动做"流沙"） |
  | `images/vfx/aeonglass/aeonglass_shard_particles.png` | 碎片粒子 |
  | `scenes/combat/spine/aeonglass_data.tres` | 战斗 Spine 骨架数据（`.skel` + `.atlas`） |
  | `images/character/spine/` | Skeleton 源文件目录（只看路径，代码不引用） |
  | `localization/`（= `res://STS2_Aeonglass/localization`） | 本地化根目录（由 RitsuLib 按约定加载） |
- **意图卡带 · 牌堆小卡的 popup**：目前用原版外观拼装；要自定义样式再单开。

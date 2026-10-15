# 表现层（Core/Presentation）约定

## 目录与职责

| 目录 | 放什么 |
|---|---|
| `Support/` | 跨文件小工具：`NodeSearch`（找后代节点）/ `UiFactory`（Label·扁平按钮）/ `GodotWait`（等待）/ `NodeLifetime`（判活）/ `UiGeometry`（缩放换算） |
| `Combat/` | 战斗内表现：领域层、意图卡带（`IntentBelt/`）、M2 按键与动画（`Gap/`）、能量计数器（`EnergyCounter/`）、刷新入口（`CombatUiRefresh` / `ExtraHandRefresh` / `DeckButtonIcons` / `AeonglassStasisPileFlash`） |
| `SecondaryResource/` | 沙砾计数器 / 5 层沙漏艺术 / 卡面沙砾费用 |
| `ControlPanel/` | 战斗内调试面板（外壳 + `ControlPage` 基类 + 按机制分页） |
| `Character/` | 角色外观：战斗 / 商店 / 火堆 / 选人背景 / 骨架特效驱动 |
| `Art/` | 卡框材质与调色板（改色只改 `AeonglassFramePalette`） |
| `Animations/` | 骨架动画名 / trigger 名常量表 |

## 命名规则

- **与本角色 / 本 mod 绑定的运行期脚本**：`Aeonglass*` 前缀（节点脚本、机制 UI）。
- **通用件**：职责名 + 固定后缀 —— `*Host`（往场景插节点的静态挂载）、`*Refresh`（主动刷新入口）、
  `*Art`（纯美术拼装）、`*Vfx`（特效驱动）、`*Ui`（挂在 `.tscn` 上的 Control 脚本）；基类/工具（`ControlPage`、`Support/*`）不加前缀。
- **类名 = 文件名**（Godot 约定，一脚本一文件）；同一个类的分册用 `ClassName.Role.cs`（如 `AeonglassIntentBelt.PileChips.cs`），
  且该类的声明必须是 `partial`。
- 控制面板的页子类保持 `*ControlPage`（目录 + 后缀已表明归属），只有面板本体带前缀。

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

- **M2 坍缩 / 唤回按键**（用户 2026-10-13 记录）：① 补按钮**图标**；② 加**按下特效**；③ 加**"可用态"特效**
  （现在只有原版"禁用 / 可用"两档）。调参入口：`Combat/Gap/AeonglassGapActionTexts.cs`。
- **领域层**：贴图现在是透明空槽（与第三章背景同画布），出图后同名覆盖。
- **沙砾计数器 / 卡框 / 各场景空槽图**：一律"透明空槽 + 同名覆盖"，不需要改代码。
- **意图卡带 · 牌堆小卡的 popup**：目前用原版外观拼装；要自定义样式再单开。

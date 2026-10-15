# 系统层（Core/Systems）约定

> 2026-10-15 起：**按功能命名**，不再用 M1–M7 编号（设置标题、代码注释、日志前缀都已清）。

## 目录（按功能域）

| 目录 | 放什么 |
|---|---|
| `Bootstrap/` | 总线与横切件：`MechanismBootstrap`（逐条注册机制）、`CharacterGate`（角色门）、`OperationBarrier`（演出期间挡操作）、`EndOfCombatSequencer`（战斗结束顺序链）、`ExtraPileModules`（额外牌堆注册） |
| `Sand/` | 时之沙：`SandResourceRegistry`（次级资源 + UI 注册）、`SandGapInvariant`（花沙砾必生空隙）、`SandInvertCmd`（倒置）、`SandPhase(s)`、`SandSpend`、`SandTurnCounters` |
| `Gap/` | 时空裂隙：`GapStore`（存储/容量/读数）、`GapActions`（坍缩/回归动作）、`GapViewPile`（显示镜像）、`GapAlternation`（必须交替的闸）、`GapRecallGuard`/`GapEtherealGuard`/`GapCopyRestore` |
| `Stasis/` | 搁浅：`StasisZone`、`StasisRunHook`、`StasisRewindHooks`、`IStasisCard`、`IStasisReturnListener` |
| `Intent/` | 操作时间（意图）：`IntentDeck`（三堆状态）、`IntentTurnFlow`/`IntentDecision`/`IntentMoves`（回合流程 / 判定 / 动作）、`IntentOps`（状态机唯一写入口）、`IntentCard(Binder)`、`IntentTargeting`/`IntentTargetRegistry`、`IntentBeltBridge`（→表现层接缝）、`IntentSelector`、`MonsterAccess`、`IntentModule` |
| `Wither/` | 凋萎：`WitherPool`（注入 / 计数 / 清除） |
| `Rewind/` | 回溯引擎：`Kernel/`（事务与快照环）、`StateCapture/`（模型/牌堆/RNG/UI 的抓取与恢复，含 `Guards/` 八个前置闸）、`Graph/`（字段图策略）、`Policy/`（扩展点）、`Bridges/`（搁浅/间隙/延宕/α or β 接缝）、`Host/`（门面与补丁） |
| `Decks/` | 双卡组：`DeckModule`、`DeckState`、`DeckPiles`（堆访问）+ `DeckSwitchService`（切换）、`DeckTwins`、`DeckBSeed`、`DeckVariant(s)`、`DeckSwitchLimit`/`DeckSwitchCost`、`DeckAddHooks`/`DeckRunHooks` |
| `Deferral/` | 延宕：`DeferralLedger`（记账）+ `DeferralSettlement`（到期结算） |
| `History/` | 历史层：`CombatHistory`（战斗内）、`RunHistory`（本局） |
| `Cards/` | 卡牌侧公共件：`CardTextArgs`、`HandDiscard`、`StatusSweeper`、`Hysteresis`/`IHysteresisCard` |
| `Riot/` | 乱流：`RiotSwap`（判定 + 字段对调核心）+ `RiotPileWatch`（接线/取证）+ `RiotDrawWindow`（抽取窗口冻结） |
| `AlphaBeta/` | α or β 的两条被动 |
| `Persistence/` | 「当时的当前卡组」等跨战斗读取 |

## 命名规则

- **按功能命名**：类名 = 它做的事（`GapStore` / `DeckSwitchService` / `WitherPool`），不要用 M 编号或旧机制名（`滞留X` → 搁浅）。
- 域内**不再带 `Aeonglass` 前缀**（目录已自明）；对外接缝/表现层引用时才需要区分。
- 巨类按 **存储 / 规则 / 流程** 三轴拆成独立类；拆完"存储"那一半持有共享状态，另两半通过它访问。
- 日志前缀用机制名：`[沙砾]`/`[间隙]`/`[意图]`/`[回溯]`/`[凋萎]`/`[双卡组]`/`[搁浅]`。

## 注释骨架（每个文件顶部，≤15 行）

```
/// <summary>【一行定位】这个文件是什么。</summary>
/// <remarks>
/// ① 职责 ② 挂点与时机 ③ 依赖 ④ ⚠️ 注意（时序 / 生命周期 / 口径沿革一行带日期）
/// </remarks>
```

## 进度与待办（2026-10-15）

**已完成**：目录按功能重排、类名按功能改准、M 编号全清（含日志前缀）、上轮 5 组巨类拆分、
多类型文件里的 14 个类型已独立成文件、`GapActions`(442) 再拆为 `GapCollapseActions` + `GapRecallActions`；
**设置页瘦身**：`AeonglassModSettingsPage` 701 → 45 行，界面只剩「关于」（`AeonglassSetting` 的 28 个属性与全部读取代码保留，键名未动）。

**待办**（同一套口径）：
1. **还剩 13 个 >250 行的文件**要按"存储 / 规则 / 流程"三轴继续拆：`IntentTurnFlow`(373)、`RiotSwap`(351)、`CombatStateSnapshot`(287)、`SandResourceRegistry`(287)、`StasisZone`(281)、`IntentDeck`(279)、`DeckSwitchService`(277)、`RewindFacade`(277)、`FieldGraphSnapshot`(274)、`RewindModule`(272)、`DeckPiles`(271)、`WitherPool`(270)、`DeckTwins`(266)（`GapRecallActions` 256 只差 6 行，可顺手把 `PlanRecall` 挪走或并入 `GapStore`）。
2. **四段注释 + 死代码清扫**：系统层约 119 个文件还没补骨架（表现层已 100%）。
3. 顺手清掉空目录 `EnemyTime/`、`SandResource/`、`Zones/`（删除命令曾被安全策略拦下）。

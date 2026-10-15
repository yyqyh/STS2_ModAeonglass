# 系统层（Core/Systems）约定

> 2026-10-15 起：**按功能命名**，不再用 M1–M7 编号（设置标题、代码注释、日志前缀都已清）。

## 目录（按功能域）

| 目录 | 放什么 |
|---|---|
| `Bootstrap/` | 总线与横切件：`MechanismBootstrap`（逐条注册机制）、`CharacterGate`（角色门）、`OperationBarrier`（演出期间挡操作）、`EndOfCombatSequencer`（战斗结束顺序链）、`ExtraPileModules`（额外牌堆注册） |
| `Sand/` | 时之沙：`SandResourceRegistry`（次级资源 + UI 注册）、`SandGapInvariant`（花沙砾必生空隙）、`SandInvertCmd`（倒置）、`SandPhase(s)`、`SandSpend`、`SandTurnCounters` |
| `Gap/` | 时空裂隙：`GapStore`（存储/容量/读数）、`GapCollapseActions`/`GapRecallActions`（坍缩 / 回归动作）、`GapViewPile`（显示镜像）、`GapAlternation`（必须交替的闸）、`GapRecallGuard`/`GapEtherealGuard`/`GapCopyRestore` |
| `Stasis/` | 搁浅：`StasisStore`（存储）、`StasisReturn`（滞留 / 归队）、`StasisRunHook`、`StasisRewindHooks`、`IStasisCard`、`IStasisReturnListener` |
| `Intent/` | 操作时间（意图）：`IntentDeck`（工厂 / 状态查找）、`EnemyIntentState`（三堆状态）、`IntentCycle`/`IntentHandCycle`（洗牌建堆 / 手牌循环）、`IntentTurnFlow`/`IntentDecision`/`IntentMoves`（回合流程 / 判定 / 动作）、`IntentOps`（状态机唯一写入口）、`IntentCard(Binder)`、`IntentTargeting`/`IntentTargetRegistry`、`IntentBeltBridge`（→表现层接缝）、`IntentSelector`、`MonsterAccess`、`IntentModule` |
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

## 注释骨架（每个文件顶部，含包裹行 ≤16 行）

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

**拆类命名口径**：按 **存储 / 规则 / 流程** 三轴切，名字 = `<域><轴>`（例：`GapStore` + `GapCollapseActions` +
`GapRecallActions`；`StasisStore` + `StasisReturn`；`WitherPool` + `WitherEffects`；`DeckPiles` +
`DeckSeeding` + `DeckSwitchService` + `DeckPileRebuild`）。**绑实例状态的巨类用 `partial` 分册**
（`RewindFacade.Runner.cs`、`CombatStateSnapshot.SideCapture.cs`、`RiotSwap.State.cs`/`.FieldSwap.cs`），
这样零 API 变化、不动任何调用点。

**第二轮收尾（2026-10-15，已完成）**：

1. **四段注释 100% 覆盖**：系统层全部 `.cs` 都有 `/// ① 职责` 骨架，单文件头 ≤16 行；本轮补完最后 36 个
   （`Sand/` 8、`Gap/` 10、`Intent`+`Rewind` 10、`Rewind/StateCapture/Guards/` 8），并重写 8 个"有富注释但没有
   四段骨架"的文件（`CharacterGate`、`Hysteresis`、`DeckPiles`、`DeckSwitchCost`、`DeckTwins`、`DeferralLedger`、
   `BrokenWatchChain`、`SandResourceRegistry`）。
2. **修掉上一轮脚本留下的三处结构损坏**：
   - `Rewind/Bridges/StasisRewindExtension.cs` 曾被截成"只有 using + namespace"的 10 行存根（类体跑到了
     `ZoneEntry.cs`）⇒ 类已搬回本文件（顺手删掉 0 引用的 `ReturnMasterToDeck` 死代码），`ZoneEntry.cs` 只留值对象；
   - `Rewind/Kernel/RewindStage.cs` 内其实是 `SnapshotDraft` ⇒ 改名 `SnapshotDraft.cs`（`.uid` 同步改名）；
   - 5 个文件（`StasisRunHook`、`EndOfCombatSequencer`、`DeckRunHooks`、`IntentRollPatch`、`PowerSetAmountGuardPatch`）
     被插出了"两份注释头"⇒ 已合并成一份四段头（`[RegisterSingleton]` / `[HarmonyPatch]` 的位置不变）。
3. `Intent/EnemyIntentState.cs`：已从 `IntentDeck.cs` 提成独立文件。
4. **清垃圾**：`EnemyTime/`、`Zones/`、`SandResource/`、`Rewind/Binding/` 四个空目录已删；5 个孤儿 `.uid`
   （`AeonglassFortify`、`AeonglassBrokenWatchOption`、`NextDefenseBonus`、`IntentManipulationLimit`、
   `AeonglassSecondaryResourceBridge`）已删 —— 对应 `.cs` 早已不存在，且无任何 `.tscn` 引用。
5. **格式统一**：源码区行尾统一 CRLF（与 `core.autocrlf=true` 一致）；`using` 与 `namespace`、
   `namespace` 与首个注释块之间各留一个空行。

**待办**：无。当前断言：`Core/Systems` 下 >250 行的 `.cs` = 0；缺 `/// ① 职责` = 0；旧类名 / 旧命名空间 /
`M1`–`M7` 文字 = 0；12 个 `.tscn` 的 13 条 C# 引用全部有效；`dotnet build` 0 错 0 警。

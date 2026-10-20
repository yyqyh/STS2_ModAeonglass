# 补丁（Harmony）约定与台账

> 2026-10-15 起：**任何打进本体共享路径的补丁，都必须"带门 + 带兜底 + 日志条件化"**。
> 本文件是补丁的**唯一台账**：加补丁时照第 1 节的模板写、在第 2 节的表里登记一行、跑第 4 节的自检。

## 1. 补丁纪律（三条）

1. **带运行时段**：打在共享路径上（`MegaCrit.*` 的类型 / 方法）的补丁，方法体第一件事必须是判定
   "这件事跟我们有没有关系"——角色门 `CharacterGate.IsActive`、机制开关（`IntentRules.MasterEnabled` /
   `RiotSwap.Enabled` / `DeckModule.Enabled` / `RestoreGuard.IsRestoring`…）、状态门（`OperationBarrier.IsBusy`、
   `HasPair`、`IsManaged`、`TwinOf`、`ShouldApply`…）。判定为假就**原样放行**，不做任何多余动作。
2. **带异常兜底，且只降级不改本体结果**：兜底里绝不"猜一个结果"，而是**保持本体原值**——
   改 `__result` 的补丁异常时不动 `__result`；替换返回 Task 的补丁异常时保留原 Task；
   前缀补丁异常时返回 `true` 让本体照常执行。异常一律记日志，**绝不外泄到本体的热路径**。
3. **日志条件化**：只在"确实发生了我们的事"时写日志，且必须带机制前缀（`Const.Prefix.*`）。
   别的角色 / 别的 mod 的局里，`godot.log` 里除了 mod 初始化那几行，**不允许出现任何我们的机制行**。

**口径：为什么补丁一律在启动时装、靠运行时门早退，而不是按开关装/卸。**
Harmony 的装卸要改方法裸指针、有并发与时机风险，RitsuLib 也是"一次装好、运行时判定"的口径；
再加上机制开关在游戏里可热改（设置页），"装/卸"根本跟不上。所以：**补丁常驻，代价压在第一句的门上**
（读一个 bool 或做一次弱表查询），非本角色的局几乎零成本。

**乱流的字段对调契约（2026-10-16 明确）**：乱流生效期间，占据抽 / 弃**槽位**的那张 `CardPile` 报的是它
**构造时的 `Type`（物理身份）**（即 `state.DrawPile.Type == PileType.Discard`，反之亦然）—— 本体按
`pile.Type` 分支的 20+ 处（`CardPileCmd` / `CardSelectCmd` / `SfxCmd` / `ForgeCmd` / `CardCmd`）看到的都是
"物理堆"，这与既定观感一致（卡跟着物理堆走、无额外动画）。**不要**把它改成"逻辑身份"（连 `Type` 的后备字段
一起换），那会改动动画 / 音效落点，属于观感改动，需先与用户对齐口径。只读断言见
`Core/Systems/Riot/RiotPileWatch.cs` 的回合开始行（打印两个槽位的 `Type` 与期望值）。

**目录归属**：跨机制、只做挂载/接线的通用补丁放 `Core/Patches/`；只服务某个机制的补丁跟机制走
（如 `Core/Systems/Intent/Patches/`、`Core/Systems/Rewind/Host/Patches/`）。

## 2. 补丁台账

| 补丁类 | 目标（本体） | 路径归属 | 生效范围门 | 异常策略 | 日志前缀 |
|---|---|---|---|---|---|
| `CombatUiMountPatch` | `NCombatRoom._Ready` (postfix) | 共享（战斗房初始化） | `CharacterGate.IsActive` | `TryAttach` 逐个 try/catch，挂不上只记日志、不影响其余 | `[HOST]`（2026-10-16 起只挂：坍缩/唤回按键 · 意图卡带 · 传送门 · 领域特效层；控制面板已删） |
| `StasisNumberDescriptionPatch` | `CardModel.GetDescriptionForPile(PileType, Creature)` (postfix) | 共享（每次描述构建） | `CharacterGate.IsActive` + 卡类型判断 | try/catch，失败保持本体原文 | `[搁浅]`（照本体 `REPLAY.extraText` 追加 `[gold]搁浅[/gold]{Times}。`） |
| `DeckTipSwapPatch` | `NHoverTipSet.CreateAndShow` (prefix) | 共享（全局悬浮提示） | `CharacterGate` + `DeckModule.Enabled` + 只认那两颗按钮 | 整流 try/catch，异常原样放行 | `[双卡组]` |
| `IntentTargetingPatches.IsValidTargetPatch` | `CardModel.IsValidTarget` (postfix) | 共享热 | `IIntentTargetCard` | try/catch，异常不动 `__result` | `[意图]` |
| …`.AllowedToTargetNodePatch` | `NTargetManager.AllowedToTargetNode` (postfix) | 共享热 | `AimingOurCard` / `IsIntentNode` | 同上 | `[意图]` |
| …`.NodeHoveredPatch` | `NTargetManager.OnNodeHovered` (postfix) | 共享热 | `IsIntentNode`（记 hovered 本身是幂等写入） | try/catch | `[意图]` |
| …`.FinishTargetingPatch` | `NTargetManager.FinishTargeting` (postfix) | 共享 | `AimingOurCard` / `HasPending`（**日志判据**） | try/catch + finally 复位 `AimingOurCard` | `[意图]` |
| …`.SelectionFinishedPatch` | `NTargetManager.SelectionFinished` (postfix) | 共享 | `HasNodes`（没登记意图卡就连包装都不做） | try/catch，异常保持原 Task / 原节点 | `[意图]` |
| …`.SingleCreatureTargetingPatch` | `NMouseCardPlay.SingleCreatureTargeting` (prefix) | 共享 | `IsOurActionCard`（结果写入 `AimingOurCard`） | try/catch（按"不是我们的卡"处理） | `[意图]` |
| `OperationBarrierPatch.OnPress` / `.ReleaseLogic` | `NEndTurnButton.OnPress` / `CallReleaseLogic` (prefix) | 共享 UI | `OperationBarrier.IsBusy`（**每个 Prefix 第一句**） | 无需（只读一个 bool） | `[间隙]` |
| `RiotDrawWindowPatch` / `RiotAutoPlayWindowPatch` | `CardPileCmd.Draw`(4 参) / `AutoPlayFromDrawPile` | 共享极热 | `RiotSwap.HasPair`（Prefix/Postfix 第一句） | try/catch，异常保持本体原 Task | `[乱流]`（字段方案生效时槽位 `Type` = 物理身份，见 §1 契约） |
| `RiotDrawPileRedirectPatch` / `RiotDiscardPileRedirectPatch` | `PlayerCombatState.DrawPile` / `DiscardPile` (getter) | 共享极热 | `RiotSwap.HasPair`（第一句） | 共用 helper try/catch，异常保持本体原堆 | `[乱流]`（字段方案生效时槽位 `Type` = 物理身份，见 §1 契约） |
| `BrokenWatchEventChainPatch` | `EventRoom.OnEventStateChanged` (postfix) | 共享 | `CanonicalEvent is Neow` + `BrokenWatchChain` 内的"起始遗物 + 待办标记 + 进程去重" | try/catch | `[回溯]` |
| `SilentCardPoolBorrowPatch` | `CardPoolModel.get_AllCards` (postfix) | 共享 | `SilentCardPoolBorrow.ShouldApply`（我们的卡池 + 调试开关） | try/catch，吞掉并记日志 | `[HOST]` |
| `IntentRollPatch` | `MonsterModel.RollMove` (prefix) | 共享极热 | `IntentRules.MasterEnabled` + `ManagedRollMove` + `CharacterGate` + `IsManaged` | try/catch，异常放行 | `[意图]` |
| `PowerSetAmountGuardPatch` | `PowerModel.SetAmount` (prefix) | 共享极热 | `RestoreGuard.IsRestoring` | try/catch，异常返回 `true`（放行本体） | `[回溯]` |
| `VfxTrackingPatches`（动态装 12 个目标） | 本体 12 个异步卡 VFX 方法 (postfix) | 共享 | `CharacterGate.IsActive`（**不是我们连 Task 都不包装**） | 委派 `VfxBarrier.Track` 内 try/finally + 吞异常 | 不写日志 |

> `DeckTwinMirrorPatch`（升级 / 降级 / 附魔 / 清附魔 / 移牌，共 5 个）已于 **2026-10-16 退出台账**：
> 孪生镜像改为**事件驱动** —— `CardModel.Upgraded`（升级与降级共用同一个事件，方向看等级是否归 0）、
> `CardModel.EnchantmentChanged`、`CardPile.CardRemoved`（只在**非静默**移牌时发；本体没有任何静默移牌，
> 我们自己的换副搬运走静默 ⇒ 不再被误判成"删牌"，这正是改事件驱动顺带修掉的旧隐患）。
> 订阅 / 退订 / 自检都在 `Core/Systems/Decks/DeckTwins.cs`（`Link` / `Unlink` / `EnsurePileWatch` / `ReleaseAll`，
> 兜底 `DeckTwinMirrors.Safe`；换 run 时由 `DeckModule` 的 `RunEndedEvent` 统一释放）。补丁总数 23 → 18。

**装补丁口径与观测**：`Main.InstallHarmonyPatches` 按 `[HarmonyPatch]` 扫描（类级或方法级），
单个类失败不影响其他类；启动日志里的 `patch classes applied=N failed=0` 是这条的验收口径
（`VfxTrackingPatches` 的 12 个目标由 `RewindModule` 单独装，日志里报"VFX 屏障补丁 12 成功 / 0 失败"）。

## 3. 新增补丁的自检清单

1. 目标方法写**全限定签名**（含参数类型数组），别只写方法名——重载会歧义。
2. 共享路径补丁：第一句必须是门；改 `__result` / 替换 Task 的补丁必须带兜底，且兜底"保持本体原值"。
3. 日志带机制前缀；这条日志只在"我们的事"发生时写。
4. 在本文件第 2 节补一行台账。
5. 跑第 4 节的三条自检 + 实机对照（非本角色一局，日志里不得出现我们的机制行）。

## 4. 自检断言（可直接复制运行）

> **2026-10-16 起**：上面这几条（①② + `[HarmonyPatch]` 计数 + 文件规模 + 实机 4.1 + PCK 一致性）已收进
> `tools\check.ps1`，一条命令跑完：`powershell -NoProfile -File tools\check.ps1`（只读、不改仓库）。
> 配套 `tools\deploy.ps1` 负责"编译 → 按需重导 PCK → 自动跑自检"，游戏在跑时会拒绝动 PCK。
> 下面三段保留作"口径说明"，脚本里是同一套判据。

```powershell
# ① 每个补丁类都必须"有门 + 有兜底"（兜底/门可以委托给已带 try 的 helper）
$root='E:\game_part2\mod_c\sts2\mod\remake\STS2_Aeonglass\'
Set-Location $root
$gates='CharacterGate|MasterEnabled|RiotEnabled|DecksEnabled|SandEnabled|IntentEnabled|RewindEnabled|RestoreGuard|OperationBarrier|IsManaged|TwinOf|ShouldApply|HasPair|HasNodes|IsOurActionCard|IsIntentNode|AimingOurCard|CanonicalEvent'
$guards='ShouldBlock\(|\.Redirect\(|MirrorGuard\.Safe|SubstituteAsync'
$bad=0
foreach($f in @(rg -l 'HarmonyPatch' --glob '*.cs' Core)){
  $t=[System.IO.File]::ReadAllText($root+$f)
  foreach($m in [regex]::Matches($t,'\[HarmonyPatch(?:\(|\])(?:.|\n)*?\n(?:\s*\[[^\]]*\]\s*\n)*\s*(?:internal|public)\s+static\s+class\s+([A-Za-z_][A-Za-z0-9_]*)')){
    $seg=$t.Substring($m.Index)
    $ok = ([regex]::IsMatch($seg,'try\s*\r?\n?\s*\{') -or [regex]::IsMatch($seg,$guards)) -and [regex]::IsMatch($seg,$gates)
    if(-not $ok){ $bad++; $m.Groups[1].Value } } }
"不合格补丁 = $bad"   # 期望 0

# ② Core 下所有日志都必须带机制前缀
$none=@()
foreach($f in (Get-ChildItem -Recurse ($root+'Core') -Filter *.cs -File)){
  $t=[System.IO.File]::ReadAllText($f.FullName)
  foreach($m in [regex]::Matches($t,'Logger\.(?:Info|Warn|Error)\(')){
    $seg=$t.Substring($m.Index,[Math]::Min(700,$t.Length-$m.Index))
    if($seg -notmatch 'Const\.Prefix\.'){ $none += $f.FullName.Substring($root.Length) } } }
"无前缀日志 = " + @($none).Count   # 期望 0

# ③ 实机对照（非本角色）：以 Run started 为界，之后不得再出现我们的日志
$log='C:\Users\yyqyhyue\AppData\Roaming\SlayTheSpire2\logs\godot.log'
$lines=Get-Content $log
$idx=($lines | Select-String 'Run started' | Select-Object -Last 1).LineNumber
@($lines[$idx..($lines.Count-1)] | Select-String '\[STS2_Aeonglass\]').Count   # 期望 0
```

## 5. 待办

- 无。`M1`–`M7` 时期的旧补丁（如 `ModExtraHandCard*Patch` 那类）属于 RitsuLib，不在本台账内。

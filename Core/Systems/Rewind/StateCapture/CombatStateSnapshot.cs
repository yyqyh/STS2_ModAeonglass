using System.Diagnostics;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Rewind.Graph;
using STS2_Aeonglass.Core.Systems.Rewind.Kernel;
using STS2_Aeonglass.Core.Systems.Rewind.Policy;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>战斗态快照（我方）：模型字段（power / 遗物）+ 战斗牌堆 + 能量星星 + 回合号轮次 + 战斗历史。</summary>
/// <remarks>
/// ① 职责：<c>Capture</c> / <c>TryRestore</c> / <c>Release</c>；两侧模型清单在 <c>CombatStateSnapshot.SideCapture.cs</c> 分册。
/// ② 挂点与时机：作为 <c>ISnapshot</c> 被 <c>RewindFacade</c> 抓拍出、由 runner 恢复。
/// ③ 依赖：<c>FieldGraphSnapshot</c>、<c>PileOps</c>、<c>PowerOps</c>、<c>RewindScope</c>（哪些不回档）。
/// ④ ⚠️ <b>不回档</b>：血量、金币、遗物列表、药水、敌人、卡组（见 <see cref="RewindScope" />）—— 别顺手加进来。
/// </remarks>
internal sealed partial class CombatStateSnapshot : ISnapshot
{
    private const string PowerKind = "power";

    private const string RelicKind = "relic";

    /// <summary>战斗堆里的卡（2026-10-16 · B4）。</summary>
    private const string CardKind = "card";

    /// <summary>卡身上的附魔 / 苦痛（与 <see cref="CardKind" /> 同一个模型族；<c>AbstractModel</c> ⇒ 按引用保留，必须单独抓）。</summary>
    private const string EnchantmentKind = "enchantment";

    /// <inheritdoc cref="EnchantmentKind" />
    private const string AfflictionKind = "affliction";

    private readonly RewindScope _scope;
    private readonly List<Entry> _models = [];
    private readonly List<CombatHistoryEntry> _historyEntries = [];

    /// <summary>本快照抓过的战斗卡（恢复时判"这张卡还在不在战斗里"、自比对时按它对答案）。</summary>
    private readonly List<CardModel> _cards = [];

    private Dictionary<PileType, List<CardModel>> _piles = [];
    private Dictionary<Creature, Vitals> _allies = [];
    private Dictionary<Creature, Vitals> _enemies = [];
    private Dictionary<string, int> _secondaryResources = [];
    private RngSnapshot? _rng;
    private TurnFlagOps.TurnFlags? _turnFlags;
    private int _energy;
    private int _stars;
    private int _turnNumber;
    private int _roundNumber;
    private PlayerTurnPhase _phase;

    private CombatStateSnapshot(SnapshotId id, string summary, RewindScope scope)
    {
        Id = id;
        Summary = summary;
        _scope = scope;
    }

    public SnapshotId Id { get; }

    public string Summary { get; }

    internal static CombatStateSnapshot? Capture(SnapshotId id, RewindScope scope)
    {
        var player = CombatAccess.LocalPlayer;
        var state = CombatAccess.State;
        if (player is null || state is null || player.PlayerCombatState is not { } combat)
        {
            return null;
        }

        var snapshot = new CombatStateSnapshot(id, CombatAccess.Describe(), scope);

        foreach (var power in player.Creature.Powers)
        {
            snapshot.AddModel(PowerKind, power);
        }

        foreach (var relic in player.Relics)
        {
            snapshot.AddModel(RelicKind, relic);
        }

        if (scope.RestorePiles)
        {
            snapshot._piles = PileOps.Capture(player);

            // 2026-10-16（B4）：卡级状态也进快照 —— 五堆里每张卡各一条；卡身上的附魔 / 苦痛再各一条
            //（它们是 `AbstractModel` ⇒ 在图里按引用保留，只抓卡的话只能还原"引用"、抓不到层数）。
            //
            // ⚠️ 这里抓的是**战斗副本**（`combat.CloneCard` 出来的那些），不是牌组母本 ⇒
            //    回档写回卡级字段**不会**牵动 `DeckTwins` 的孪生镜像（配对表里挂的是母本）。
            // ⚠️ 不抓 `DynamicVarSet` 内部数值（策略里按引用保留）：卡面派生数值靠恢复后的重算 + UI 刷新补齐，
            //    这条边界记在体检报告 §8。
            var stopwatch = Stopwatch.StartNew();
            foreach (var card in snapshot._piles.Values.SelectMany(pile => pile))
            {
                snapshot._cards.Add(card);
                snapshot.AddModel(CardKind, card);

                if (card.Enchantment is { } enchantment)
                {
                    snapshot.AddModel(EnchantmentKind, enchantment);
                }

                if (card.Affliction is { } affliction)
                {
                    snapshot.AddModel(AfflictionKind, affliction);
                }
            }

            stopwatch.Stop();
            // 耗时只在"详细诊断日志"开着时报（每次快照一行；用来实测 B4 的开销）。
            if (Core.Config.AeonglassSettingStore.DebugVerboseLogs)
            {
                Const.Logger.Info(
                    $"[{Const.ModId}] {Const.Prefix.Rewind} 快照抓卡：{snapshot._cards.Count} 张"
                    + $"（模型条目共 {snapshot._models.Count}，含附魔/苦痛）｜耗时 {stopwatch.Elapsed.TotalMilliseconds:0.##} ms");
            }
        }

        if (scope.RestoreCombatHistory)
        {
            snapshot._historyEntries.AddRange(CombatManager.Instance.History.Entries);
        }

        if (scope.RestorePlayerHp || scope.RestorePlayerBlock)
        {
            snapshot._allies = CreatureVitals.Capture(CreaturesOnSide(state, CombatSide.Player));
        }

        if (scope.RestoreEnemyHp || scope.RestoreEnemyBlock)
        {
            snapshot._enemies = CreatureVitals.Capture(CreaturesOnSide(state, CombatSide.Enemy));
        }

        if (scope.RestoreSecondaryResources && SecondaryResourceRuntime.Bridge.Available)
        {
            snapshot._secondaryResources =
                new Dictionary<string, int>(SecondaryResourceRuntime.Bridge.Capture(player));
        }

        // 回溯 唯一补齐的规格缺口：RNG（run Rng 集合 + 每只怪的 Rng + mod 自己的 StsRng）。
        if (scope.RestoreRng)
        {
            snapshot._rng = RngOps.Capture();
        }

        if (Reflect.TryGet<int>(combat, Coordinates.PlayerCombatState.TurnNumber, out var turn))
        {
            snapshot._turnNumber = turn;
        }

        snapshot._energy = combat.Energy;
        snapshot._stars = combat.Stars;
        snapshot._roundNumber = state.RoundNumber;
        snapshot._phase = combat.Phase;

        // 回合级私有标记（怪的本回合标记 / 首动 / 招式已执行）—— 规格 §2.6.2 最后一行。
        if (scope.RestoreTurnCounters)
        {
            snapshot._turnFlags = TurnFlagOps.Capture();
        }

        return snapshot;
    }

}

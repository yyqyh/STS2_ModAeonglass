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

    private readonly RewindScope _scope;
    private readonly List<Entry> _models = [];
    private readonly List<CombatHistoryEntry> _historyEntries = [];

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

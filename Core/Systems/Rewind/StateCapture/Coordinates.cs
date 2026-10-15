namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 0.111 反射坐标表。<b>整个项目里只有这个文件允许出现字段名 / 方法名字面量。</b>
/// 换游戏版本时只改这里；其余层不动。
/// </summary>
internal static class Coordinates
{
    internal static class CombatManager
    {
        public const string TurnState = "_turnState";
        public const string CardOrPotionEffectDepth = "_cardOrPotionEffectDepth";
    }

    /// <summary>CombatTurnState：0.110 起回合态在这里，不再是 CombatManager 的字段。</summary>
    internal static class TurnState
    {
        public const string State = "<State>k__BackingField";
        public const string PendingLoss = "<PendingLoss>k__BackingField";
        public const string EndingPlayerTurnPhaseOne = "<EndingPlayerTurnPhaseOne>k__BackingField";
        public const string EndingPlayerTurnPhaseTwo = "<EndingPlayerTurnPhaseTwo>k__BackingField";
        public const string PlayersReadyToEndTurn = "<PlayersReadyToEndTurn>k__BackingField";
        public const string PlayersReadyToBeginEnemyTurn = "<PlayersReadyToBeginEnemyTurn>k__BackingField";
    }

    internal static class PlayerCombatState
    {
        public const string Energy = "_energy";
        public const string Stars = "_stars";
        public const string Phase = "_phase";
        public const string TurnNumber = "<TurnNumber>k__BackingField";
    }

    internal static class CardModel
    {
        public const string Owner = "_owner";
    }

    internal static class PowerModel
    {
        public const string Owner = "_owner";

        /// <summary><c>int</c>，power 的当前层数（恢复期绕过 <c>SetAmount</c> 时直接写它）。</summary>
        public const string Amount = "_amount";

        /// <summary><c>int</c>，回合开始时的层数。</summary>
        public const string AmountOnTurnStart = "_amountOnTurnStart";
    }

    internal static class CombatState
    {
        public const string AllCards = "_allCards";
        public const string Allies = "_allies";
        public const string Enemies = "_enemies";
        public const string Escaped = "_escapedCreatures";
        public const string NextCreatureId = "_nextCreatureId";
    }

    /// <summary>回合级私有标记：怪"本回合才召唤出来"（<c>private set</c>，只能反射写）。</summary>
    internal static class MonsterModel
    {
        public const string SpawnedThisTurn = "_spawnedThisTurn";
    }

    /// <summary>回合级私有标记：这台状态机是否执行过第一个动作。</summary>
    internal static class MonsterMoveStateMachine
    {
        /// <summary><c>MonsterState</c>：状态机当前的内部状态（用来对照 <c>MonsterModel.NextMove</c>）。</summary>
        public const string CurrentState = "_currentState";

        /// <summary>bool：是否已经执行过一次移动（接管 <c>RollMove</c> 后会一直是 false）。</summary>
        public const string PerformedFirstMove = "_performedFirstMove";
    }

    /// <summary>回合级私有标记：这个招式是否至少执行过一次（<c>MustPerformOnceBeforeTransitioning</c> 用它）。</summary>
    internal static class MoveState
    {
        public const string PerformedAtLeastOnce = "_performedAtLeastOnce";
    }

    internal static class History
    {
        public const string Entries = "_entries";
        public const string Changed = "Changed";
    }

    internal static class ActionQueueSet
    {
        public const string WaitingForResumption = "_actionsWaitingForResumption";
        public const string NextId = "_nextId";
    }

    internal static class ActionQueueSynchronizer
    {
        public const string HookActions = "_hookActions";
        public const string RequestedWaitingPlayerTurn = "_requestedActionsWaitingForPlayerTurn";
        public const string NextHookId = "_nextHookId";
    }

    internal static class PlayerChoiceSynchronizer
    {
        public const string ReceivedChoices = "_receivedChoices";
        public const string ChoiceIds = "_choiceIds";
    }
}

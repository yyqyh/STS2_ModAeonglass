using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems;

namespace STS2_Aeonglass.Core.Systems.EnemyTime;

/// <summary>
/// M3「操作时间」+ M5「凋萎」模块入口：订阅回合事件驱动解析，并提供玩家的意图操纵入口。
/// </summary>
/// <remarks>
/// <para>
/// 时机：<b>敌人回合开始前</b>标记"上一张真的被执行过"；<b>玩家回合开始</b>做整轮解析
/// （结算 → 清手牌 → 抽满 N → 判定 → 写 <c>NextMove</c>）。敌人的回合只负责执行我们设好的那张。
/// </para>
/// <para>
/// C11 接管原生掷招的补丁在 <c>Patches/IntentRollPatch.cs</c>；本模块只负责"什么时候解析、谁有额度"。
/// </para>
/// </remarks>
internal static class EnemyTimeModule
{
    /// <summary>日志 / 设置页里显示的机制名。</summary>
    public const string DisplayName = "M3/M5 敌人时间";

    private static bool _registered;

    /// <summary>M3 是否启用。</summary>
    public static bool Enabled => AeonglassSettingStore.Current.IntentEnabled;

    /// <summary>由 <c>MechanismBootstrap</c> 逐条注册调用。</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(_ => ResetRuntimeState());
        RitsuLibFramework.SubscribeLifecycle<CombatEndedEvent>(_ => ResetRuntimeState());
        RitsuLibFramework.SubscribeLifecycle<SideTurnStartingEvent>(OnSideTurnStarting);
        RitsuLibFramework.SubscribeLifecycle<SideTurnStartedEvent>(OnSideTurnStarted);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Intent} 敌人时间已就绪：{EnemyTimeScope.Describe()}"
            + $"｜抽牌数 N = 每只怪的意图卡总数；虚拟牌堆 = 抽牌堆 / 手牌 / 弃牌堆（无消耗堆）");
    }

    /// <summary>每场战斗重置（意图牌堆不跨战斗）。</summary>
    public static void ResetRuntimeState()
    {
        IntentDeck.Reset();
    }

    /// <summary>敌人回合即将开始 ⇒ 我们上一回合设进去的那张（如果有）确实被执行过了。</summary>
    private static void OnSideTurnStarting(SideTurnStartingEvent evt)
    {
        // 角色门：别的角色（含本体）回合里一律不碰敌人的意图。
        if (!Enabled || !CharacterGate.IsActive || evt.Side != CombatSide.Enemy)
        {
            return;
        }

        foreach (var enemy in MonsterAccess.LivingEnemies)
        {
            if (IntentDeck.TryGet(enemy, out var state))
            {
                state.EnemyTurnSeen = true;
            }
        }
    }

    /// <summary>玩家回合开始 ⇒ 给每只活着的怪做整轮解析。</summary>
    private static void OnSideTurnStarted(SideTurnStartedEvent evt)
    {
        if (!Enabled || !CharacterGate.IsActive || evt.Side != CombatSide.Player)
        {
            return;
        }

        foreach (var enemy in MonsterAccess.LivingEnemies)
        {
            try
            {
                IntentResolver.PrepareTurn(enemy);
            }
            catch (Exception ex)
            {
                Const.Logger.Error(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 解析 {MonsterAccess.IdOf(enemy)} 失败（跳过未连坐）："
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // ============================ 玩家动词（受每场上限约束）============================

    /// <summary>「跳过」：把手牌第一张洗回抽牌堆。</summary>
    /// <summary>「推迟」：把第一张"可以行动的"意图移到手牌最后。</summary>
    public static string Postpone(Player player, Creature target) =>
        Manipulate(player, target, "推迟", IntentResolver.Postpone);

    /// <summary>「提前」：把最后一张"可以行动的"意图移到手牌最前。</summary>
    public static string Advance(Player player, Creature target) =>
        Manipulate(player, target, "提前", IntentResolver.Advance);

    /// <summary>「剥夺」：丢弃第一张"可以行动的"意图（进弃牌堆）。</summary>
    public static string Deprive(Player player, Creature target) =>
        Manipulate(player, target, "剥夺", IntentResolver.Deprive);

    /// <summary>「塞虚无」：往目标的弃牌堆塞虚无（洗回后可能被抽到）。</summary>
    public static string InjectVoid(Player player, Creature target) =>
        Manipulate(player, target, "塞虚无",
            creature => VoidPollution.Inject(creature, EnemyTimeScope.InjectPerUse));

    /// <summary>调试用：切换手牌第一张意图的三属性（构造"条件不满足 / 强制 / 消耗"剧本）。</summary>
    public static string CycleFlag(Player player, Creature target, string which) =>
        Manipulate(player, target, $"改属性({which})",
            creature => IntentResolver.CycleFrontFlag(creature, which));

    private static string Manipulate(
        Player player,
        Creature? target,
        string verb,
        Func<Creature, string> action)
    {
        if (target is null || !MonsterAccess.IsManageable(target))
        {
            return $"{verb}：没有可操作的目标怪";
        }

        var text = action(target);
        // 用户口径（上级 2026-10-12 决策）：**取消每场次数上限** —— 正式路径不再拦次数。
        // `IntentManipulationLimit` 保留为"默认关闭的调试闸"（要拦时在调试面板里显式打开）。
        // 战斗历史：记一次"操作了意图"（INT-6 高维观测读它）。
        Systems.History.CombatHistory.RecordIntentManipulation(player);
        return $"{verb} → {text}";
    }

    /// <summary>一行状态（调试面板用）。</summary>
    public static string Describe(Player? player, Creature? target)
    {
        if (!Enabled)
        {
            return "M3 未启用";
        }

        var limit = player is null
            ? "-"
            : "不限";
        return $"目标 {MonsterAccess.IdOf(target)}｜操纵 {limit}（规则层已取消每场上限）｜{IntentDeck.Describe(target)}";
    }
}

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 指向模式：动作卡打出后进入"选意图卡"状态。
/// </summary>
/// <remarks>
/// ① 职责：开/关指向，通知卡带切可点。
/// ② 调用时机：卡牌指向流程。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class IntentTargeting
{
    private static Player? _player;
    private static string _verb = string.Empty;
    private static Func<Player, Creature, IntentCard, string>? _action;

    /// <summary>
    /// 「这次点完别退出指向模式」—— <b>多点选取</b>用（例：INT-5 定序要按顺序点完整手）。
    /// </summary>
    /// <remarks>
    /// 由 <c>action</c> 自己决定要不要续：它在结算末尾调 <see cref="KeepAlive" />，
    /// <see cref="TrySubmit" /> 看到标记就保留模式（否则照旧一次结算即退出）。
    /// </remarks>
    private static bool _keepAlive;

    /// <summary>正在等玩家点一张意图。</summary>
    public static bool IsActive => _action is not null;

    /// <summary>当前动词（面板 / 日志用）。</summary>
    public static string Verb => _verb;

    /// <summary>进入指向模式：卡带变可点，点中的那一张交给 <paramref name="action" /> 执行。</summary>
    public static void Begin(Player player, string verb, Func<Player, Creature, IntentCard, string> action)
    {
        _player = player;
        _verb = verb;
        _action = action;
        _keepAlive = false;
        IntentBeltBridge.SetTargeting(true);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Intent} 进入指向模式：{verb} —— 点卡带上的意图卡（可行动的那几张）");
    }

    /// <summary>多点选取：本次结算完**不退出**，等下一次点击（由 <c>action</c> 在末尾调）。</summary>
    public static void KeepAlive() => _keepAlive = true;

    /// <summary>退出指向模式（不动任何牌）。</summary>
    public static void Cancel(string why = "取消")
    {
        if (!IsActive)
        {
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 退出指向模式（{why}）：{_verb}");
        _player = null;
        _action = null;
        _verb = string.Empty;
        _keepAlive = false;
        IntentBeltBridge.SetTargeting(false);
    }

    /// <summary>玩家点了某只怪的某张意图 —— 执行并退出指向模式。</summary>
    public static bool TrySubmit(Creature owner, IntentCard card)
    {
        if (_player is not { } player || _action is not { } action)
        {
            return false;
        }

        try
        {
            var text = action(player, owner, card);
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 指向结算（{_verb}）→ {text}");
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Intent} 指向结算抛异常（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }

        // 多点选取：action 说了"还要继续"就留在模式里（下一次点击照旧走同一个 action）。
        if (_keepAlive)
        {
            _keepAlive = false;
            return true;
        }

        Cancel("已结算");
        return true;
    }
}

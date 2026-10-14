using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

using STS2_Aeonglass.Core.Content.Cards.Token;

namespace STS2_Aeonglass.Core.Systems.EnemyTime;

/// <summary>
/// 一张"意图卡"：<b>一个 <c>MoveState</c> 的元数据</b>
/// （<c>MoveState</c> 不是模型：没有卡面、没有 hook、不进存档）。
/// </summary>
/// <remarks>
/// 三属性 <c>消耗 / 条件意图 / 条件是否满足</c> 与"被虚无吃掉"标记都挂在元数据上。
/// ⚠️ <b>三属性只能挂在携带 <c>MoveState</c> 的意图卡上</b>：虚无是纯元数据卡（<see cref="IsVoid" /> = true），
/// 给它挂三属性没有意义，UI / 面板都要跳过它（Lab C 第一轮实测踩过这个坑）。
/// </remarks>
internal sealed class IntentCard(string moveId, bool isVoid)
{
    /// <summary>对应 <c>MoveState</c> 的 Id；虚无卡没有对应的 MoveState。</summary>
    internal string MoveId { get; } = moveId;

    /// <summary>凋萎（虚无）卡：不携带可执行的 MoveState。</summary>
    internal bool IsVoid { get; } = isVoid;

    /// <summary>构建时抓到的 <c>MoveState</c> 引用（"承载方式 = 引用"时用它）。</summary>
    internal MoveState? State { get; init; }

    /// <summary>
    /// 凋萎这一项的**真卡实例**（用户 2026-10-12：凋萎要做成卡牌实例）。
    /// </summary>
    /// <remarks>
    /// 只有 <see cref="IsVoid" /> 的项会带它；卡带用它渲染真卡面，结算用它读**当前**掉血量（可多次强化）。
    /// </remarks>
    internal MegaCrit.Sts2.Core.Models.CardModel? WitherCard { get; set; }

    /// <summary>这一项要造成的掉血（凋萎读卡上的值；没有实例就退回基础值）。</summary>
    internal int HitPointsOf() =>
        WitherCard is AeonglassWitherCard wither ? wither.HitPoints : AeonglassWitherCard.BaseHitPoints;

    /// <summary>三属性之一：打出后从循环里永久移除。</summary>
    internal bool Consume { get; set; }

    /// <summary>
    /// 三属性之一：这是一张**条件意图** —— 条件已满足时**插队优先**（跳过它之前的所有牌）。
    /// </summary>
    /// <remarks>
    /// 2026-10-14 规则层：<b>「条件」是插队优先权，不是"能不能打"的限制</b> ——
    /// 条件未满足时它退化成一张普通意图（照样会被打出，只是没有插队权）。
    /// </remarks>
    internal bool Forced { get; set; }

    /// <summary>三属性之一：条件**是否已满足** —— 只决定插队优先权，不再决定"能不能打"。</summary>
    internal bool ConditionMet { get; set; } = true;

    /// <summary>副惩戒：本回合被虚无吃掉的意图位。</summary>
    internal bool Eaten { get; set; }

    /// <summary>
    /// <b>迟滞</b>（敌人侧）：这张意图<b>不参与洗牌</b> —— 洗回时留在弃牌堆（DLY-4 静域用）。
    /// </summary>
    /// <remarks>
    /// 口径与玩家侧迟滞一致：只挡"随机洗回"，不挡任何主动指定（例：剥夺照样能点它）。
    /// 战斗内状态（意图牌堆本身就是战斗级的）。
    /// </remarks>
    internal bool Stuck { get; set; }

    internal int TimesDrawn { get; set; }

    internal int TimesPlayed { get; set; }

    /// <summary>
    /// 复制一份元数据（<b>同一个 <c>MoveState</c></b>，但属性、次数、标记各自独立）。
    /// 卡组"补足到 x 张"时用它 —— 于是重复的意图能同时出现在手上，而"消耗"只影响被消耗的那一份。
    /// </summary>
    internal IntentCard Clone() => new(MoveId, IsVoid)
    {
        State = State,
        Consume = Consume,
        Forced = Forced,
        ConditionMet = ConditionMet,
    };

    /// <summary>
    /// 可打 = **非虚无 + 没被虚无吃掉**。
    /// </summary>
    /// <remarks>
    /// 2026-10-14 规则层：**「条件」不再是打出的门槛**（见 <see cref="Forced" />）。
    /// 这个属性同时被卡带的"可选中"与三个动词（推迟 / 提前 / 剥夺）复用。
    /// </remarks>
    internal bool Playable => !IsVoid && !Eaten;

    internal string Describe()
    {
        if (IsVoid)
        {
            return $"{MoveId}·虚无";
        }

        var tags = string.Empty;
        if (Consume)
        {
            tags += "·消耗";
        }

        if (Forced)
        {
            tags += ConditionMet ? "·条件✓(插队)" : "·条件✗";
        }

        if (Eaten)
        {
            tags += "·被吃";
        }

        return MoveId + tags;
    }

    internal static IntentCard Void() => new(Const.Intent.VoidCardId, isVoid: true);
}

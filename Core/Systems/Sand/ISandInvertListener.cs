using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace STS2_Aeonglass.Core.Systems.Sand;

/// <summary>"沙漏被翻转了"的监听口（能力牌用；例：INV-3 惯性 = 每翻转一次抽 1 张）。</summary>
/// <remarks>
/// <para>
/// 翻转的入口只有一个（<see cref="SandInvertCmd.TryInvert" />）⇒ 通知也只在那一处发，
/// 不做钩子、不做补丁：<c>TryInvert</c> 成功后遍历该玩家的能力，逐个调
/// <see cref="OnSandInverted" />。
/// </para>
/// <para>实现方自己负责"每回合 1 次"这类闸门（本类只转发事件）。</para>
/// </remarks>
internal interface ISandInvertListener
{
    /// <param name="choiceContext">当前出牌/动作上下文。</param>
    /// <param name="player">翻转的人。</param>
    /// <param name="invertsThisTurn">含这一次在内，本回合共翻转了几次。</param>
    Task OnSandInverted(PlayerChoiceContext choiceContext, Player player, int invertsThisTurn);
}

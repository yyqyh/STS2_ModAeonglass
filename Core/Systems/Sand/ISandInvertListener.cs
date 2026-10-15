using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace STS2_Aeonglass.Core.Systems.Sand;

/// <summary>"沙漏被翻转了"的监听口（能力牌用；例：每翻转一次抽 1 张）。</summary>
/// <remarks>
/// ① 职责：声明 <c>OnSandInverted</c> 回调。
/// ② 挂点与时机：翻转的入口只有 <see cref="SandInvertCmd.TryInvert" /> ⇒ 通知只在那一处发（不做钩子、不做补丁）。
/// ③ 依赖：无。
/// ④ ⚠️ "每回合 1 次"这类闸门由实现方自己负责，本接口只转发事件。
/// </remarks>
internal interface ISandInvertListener
{
    /// <param name="choiceContext">当前出牌/动作上下文。</param>
    /// <param name="player">翻转的人。</param>
    /// <param name="invertsThisTurn">含这一次在内，本回合共翻转了几次。</param>
    Task OnSandInverted(PlayerChoiceContext choiceContext, Player player, int invertsThisTurn);
}

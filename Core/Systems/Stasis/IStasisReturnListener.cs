using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>"有牌被搁浅了"的监听口（STA 通道的收益线卡用；例：STA-2 压舱 = 每搁浅一张 +6 挡）。</summary>
/// <remarks>
/// 搁浅只有一个入口（<c>StasisZone.SendToZone</c>）⇒ 通知就在那一处发，不做钩子、不做补丁。
/// 实现方自己负责"每回合 1 次"这类闸门。
/// </remarks>
internal interface IStasisReturnListener
{
    /// <param name="player">搁浅动作的归属玩家。</param>
    /// <param name="card">被搁浅的那张牌（牌组里的母本）。</param>
    /// <param name="combatsLeft">它还要在外面待几场战斗。</param>
    Task OnCardStranded(Player player, CardModel card, int combatsLeft);
}

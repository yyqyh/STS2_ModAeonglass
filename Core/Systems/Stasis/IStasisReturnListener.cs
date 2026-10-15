using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>
/// 标记接口：<b>关心"搁浅牌归队"的监听者</b>（给收益类卡/能力用）。
/// </summary>
/// <remarks>
/// ① 职责：归队回调。
/// ② 调用时机：<c>StasisReturn</c> 归队时逐个通知。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal interface IStasisReturnListener
{
    /// <param name="player">搁浅动作的归属玩家。</param>
    /// <param name="card">被搁浅的那张牌（牌组里的母本）。</param>
    /// <param name="combatsLeft">它还要在外面待几场战斗。</param>
    Task OnCardStranded(Player player, CardModel card, int combatsLeft);
}

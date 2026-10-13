using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;

namespace STS2_Aeonglass.Core.Systems.EnemyTime;

/// <summary>
/// **自动选择器**（用户 2026-10-12 从群友那学来的用法）：挂在选牌调用外面，作用域内的选牌**不弹界面**、直接返回。
/// </summary>
/// <remarks>
/// <para>
/// 用法：<c>using (CardSelectCmd.PushSelector(new AeonglassIntentSelector())) { … FromSimpleGrid(…) … }</c>。
/// 多层嵌套要用 <c>PushSelector</c>（<c>UseSelector</c> 在已有选择器时会抛）。
/// </para>
/// <para>
/// <b>联机注意</b>：选择器非空时框架不调 <c>ReserveChoiceId</c> / <c>SignalPlayerChoiceBegun</c>，
/// 这条链路只有本地判定 ⇒ 只用于单机 / 测试；要联机就得两端同规则或回到真界面。
/// </para>
/// <para>
/// 现在的规则：**取前 maxSelect 张**（群友那套同款）。以后要"按可行动挑"在这里改：
/// 候选是 <see cref="CardModel" />，认哪张是意图靠 <c>IntentCardBinder</c> 的 <c>IntentKey</c>。
/// </para>
/// </remarks>
internal sealed class AeonglassIntentSelector : ICardSelector
{
    private readonly int _take;

    internal AeonglassIntentSelector(int take = 1) => _take = Math.Max(1, take);

    public Task<IEnumerable<CardModel>> GetSelectedCards(
        IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
        Task.FromResult<IEnumerable<CardModel>>(options.Take(Math.Min(_take, Math.Max(1, maxSelect))).ToList());

    public CardRewardSelection GetSelectedCardReward(
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> alternatives) =>
        new() { card = options.FirstOrDefault()?.Card };
}

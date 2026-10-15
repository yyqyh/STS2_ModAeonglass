using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 自动选择器：挂在选牌调用上，让"我们要选的牌"直接返回。
/// </summary>
/// <remarks>
/// ① 职责：<c>GetSelectedCards</c> 按规则挑。
/// ② 调用时机：用 <c>CardSelectCmd.PushSelector</c> 包一层时。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal sealed class IntentSelector : ICardSelector
{
    private readonly int _take;

    internal IntentSelector(int take = 1) => _take = Math.Max(1, take);

    public Task<IEnumerable<CardModel>> GetSelectedCards(
        IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
        Task.FromResult<IEnumerable<CardModel>>(options.Take(Math.Min(_take, Math.Max(1, maxSelect))).ToList());

    public CardRewardSelection GetSelectedCardReward(
        IReadOnlyList<CardCreationResult> options,
        IReadOnlyList<CardRewardAlternative> alternatives) =>
        new() { card = options.FirstOrDefault()?.Card };
}

using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>沙漏倾倒</b>（CardDRP1）：1 费 · 从弃牌堆选最多 3 张牌，放到抽牌堆顶（升级 ⇒ 最多 4 张）。
/// </summary>
/// <remarks>
/// <b>迟滞的必要出口</b>：迟滞只拒绝"随机洗牌"，不拒绝被指定 —— 这张卡就是"指定取回"。
/// 取回顺序 = 你的选择顺序（本体的多选界面按选择先后返回）；
/// 逐张放到抽牌堆顶 ⇒ 后选的会压在更上面（与"任意顺序"等价到可接受程度）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassHourglassPour() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private static readonly LocString PourPrompt =
        new("static_hover_tips", "STS2_AEONGLASS_PROMPT_POUR_TOP");

    private const decimal PickBase = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Pick", PickBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (PileType.Discard.GetPile(Owner) is not { } discard || discard.Cards.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] 沙漏倾倒：弃牌堆是空的");
            return;
        }

        var pick = (int)DynamicVars["Pick"].BaseValue;
        var picked = (await CardSelectCmd.FromCombatPile(
                choiceContext,
                discard,
                Owner,
                new CardSelectorPrefs(PourPrompt, pick)))
            .ToList();

        foreach (var card in picked)
        {
            await CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top, null, skipVisuals: false);
        }

        Const.Logger.Info($"[{Const.ModId}] 沙漏倾倒：从弃牌堆取回 {picked.Count} 张放到抽牌堆顶");
    }

    protected override void OnUpgrade() => DynamicVars["Pick"].UpgradeValueBy(1m);
}

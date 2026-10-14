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

/// <summary><b>回沙</b>（CardUTL3）：1 费 · 抽 2 张牌，然后把 1 张手牌放到抽牌堆顶（升级 ⇒ 放 2 张）。</summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDrawBack() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    /// <summary>选择提示的本地化键（写在 <c>static_hover_tips.json</c>）。</summary>
    private static readonly LocString PutBackPrompt =
        new("static_hover_tips", "STS2_AEONGLASS_PROMPT_PUT_BACK_TOP");

    private const decimal DrawCount = 2m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("PutBack", 1m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, (int)DrawCount, Owner);

        var putBack = (int)DynamicVars["PutBack"].BaseValue;
        for (var i = 0; i < putBack; i++)
        {
            var picked = (await CardSelectCmd.FromHand(
                    choiceContext,
                    Owner,
                    new CardSelectorPrefs(PutBackPrompt, 1),
                    null,
                    this))
                .FirstOrDefault();

            if (picked is null)
            {
                return;
            }

            await CardPileCmd.Add(picked, PileType.Draw, CardPilePosition.Top, null, skipVisuals: false);
        }
    }

    protected override void OnUpgrade() => DynamicVars["PutBack"].UpgradeValueBy(1m);
}

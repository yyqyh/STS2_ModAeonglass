using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>沙漏倾倒</b>（CardDRP1，v2）· <b>1 费 · Uncommon · 技能</b> —— 从[弃牌堆]选最多 3 张牌，放到[抽牌堆]顶；升级最多 4 张。
/// </summary>
/// <remarks>
/// ① 效果：开一次"弃牌堆选牌"界面（0 ~ 上限，可少选/取消），选中的牌按<b>选择顺序</b>压到抽牌堆顶。
/// ② 差分：无差分。
/// ③ 依赖：本体选牌链路（<c>CardSelectCmd.FromCombatPile</c> + <c>CardSelectorPrefs</c>）+ 牌堆搬运（<c>CardPileCmd.Add(..., PileType.Draw, Top)</c>）；
///    提示文案取本卡的 <c>selectionScreenPrompt</c> 本地化键。
/// ④ ⚠️ 它是「迟滞」的<b>必要出口</b>（迟滞牌留在弃牌堆里出不来，必须有人能把它捞回去）；
///    乱流生效期间"弃牌堆"是按对调后的那张堆取（本卡不特殊处理，与全池口径一致）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassHourglassPour() : ModCardTemplate(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal PickBase = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Pick", PickBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var discard = PileType.Discard.GetPile(Owner);
        if (discard.Cards.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Riot} 沙漏倾倒：弃牌堆是空的");
            return;
        }

        var max = (int)DynamicVars["Pick"].BaseValue;
        var picked = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            discard,
            Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 0, max))).ToList();

        if (picked.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Riot} 沙漏倾倒：没有选牌");
            return;
        }

        await CardPileCmd.Add(picked, PileType.Draw, CardPilePosition.Top);
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Riot} 沙漏倾倒：{picked.Count} 张 → 抽牌堆顶"
            + $"（{string.Join(",", picked.Select(card => card.Id.Entry))}）");
    }

    /// <summary>升级只抬上限（3 → 4）。</summary>
    protected override void OnUpgrade() => DynamicVars["Pick"].UpgradeValueBy(1m);
}

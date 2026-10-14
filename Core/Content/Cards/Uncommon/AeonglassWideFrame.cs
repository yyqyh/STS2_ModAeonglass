using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>宽幅</b>（GRW-1，能力）：1 费 · 你的手牌上限 +2。
/// </summary>
/// <remarks>
/// <para>
/// <b>升级 = 把结构换成流量</b>：手牌上限 2 → 1，并<b>每回合开始时抽 1 张</b>。
/// </para>
/// <para>
/// ⚠️ 卡表 CSV 的 <c>desc_upgraded_zhs</c> 只写"手牌上限 +1"（漏了"每回合抽 1 张"）——
/// 这里以升级档的设计原文为准，已列入待卡池侧确认的清单。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassWideFrame() : ModCardTemplate(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    private const decimal HandLimitBase = 2m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("HandLimit", HandLimitBase)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<AeonglassWideFramePower>(
            choiceContext, Owner.Creature, DynamicVars["HandLimit"].BaseValue, Owner.Creature, this);

        if (power is not null)
        {
            power.DrawEachTurn = IsUpgraded;
        }

        Const.Logger.Info(
            $"[{Const.ModId}] 宽幅：手牌上限 +{DynamicVars["HandLimit"].BaseValue}"
            + (IsUpgraded ? "，且每回合开始时抽 1 张" : string.Empty));
    }

    protected override void OnUpgrade() => DynamicVars["HandLimit"].UpgradeValueBy(-1m);

    /// <summary>升级档的"每回合开始时抽 1 张"是额外句 ⇒ 同「容器」，用注入的文本段。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("UpgradeText", IsUpgraded ? ReadUpgradeClause() : string.Empty);
    }

    private string ReadUpgradeClause()
    {
        var key = Id.Entry + ".upgraded";
        try
        {
            return new LocString("cards", key).GetFormattedText();
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] 宽幅：升级追加句取不到（{key}）：{ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }
}

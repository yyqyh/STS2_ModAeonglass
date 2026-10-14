using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>容器</b>（GRW-4，能力）：3 费 · 你的手牌上限 +3；打出时立刻抽 2 张牌。
/// </summary>
/// <remarks>
/// <para>
/// <b>升级 = 用一点结构换一段即时防线</b>（卡表 batch6 的升级档）：手牌上限 3 → 2，
/// 抽 2 张不变，另获得 3 点格挡。
/// </para>
/// <para>
/// ⚠️ 卡表 CSV 的 <c>desc_upgraded_zhs</c> 只写到手牌上限 +2（漏了"并获得 3 挡"），
/// 这里以升级档的设计原文为准 —— 已列入待卡池侧确认的清单。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassVessel() : ModCardTemplate(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    private const decimal HandLimitBase = 3m;
    private const decimal DrawOnPlay = 2m;
    private const decimal UpgradeBlock = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [
            new DynamicVar("HandLimit", HandLimitBase),
            new DynamicVar("Draw", DrawOnPlay),
            new DynamicVar("UpgradeBlock", UpgradeBlock),
        ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["Draw"].BaseValue, Owner);

        await PowerCmd.Apply<AeonglassVesselPower>(
            choiceContext, Owner.Creature, DynamicVars["HandLimit"].BaseValue, Owner.Creature, this);

        if (IsUpgraded)
        {
            await CreatureCmd.GainBlock(Owner.Creature, UpgradeBlock, ValueProp.Move, cardPlay);
        }

        Const.Logger.Info(
            $"[{Const.ModId}] 容器：手牌上限 +{DynamicVars["HandLimit"].BaseValue}"
            + $"（抽 {DynamicVars["Draw"].BaseValue}{(IsUpgraded ? $"，+{UpgradeBlock} 挡" : string.Empty)}）");
    }

    protected override void OnUpgrade() => DynamicVars["HandLimit"].UpgradeValueBy(-1m);

    /// <summary>升级档的"并获得 3 点格挡"是**额外句**，卡面数值位装不下 ⇒ 用注入的文本段（写法同差分文案）。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("UpgradeText", IsUpgraded ? ReadUpgradeClause() : string.Empty);
    }

    /// <summary>升级档追加句（本地化键 <c>&lt;卡id&gt;.upgraded</c>；取不到就留空，卡面不会露出占位符）。</summary>
    private string ReadUpgradeClause()
    {
        var key = Id.Entry + ".upgraded";
        try
        {
            return new LocString("cards", key).GetFormattedText();
        }
        catch (Exception ex)
        {
            Const.Logger.Warn($"[{Const.ModId}] 容器：升级追加句取不到（{key}）：{ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }
}

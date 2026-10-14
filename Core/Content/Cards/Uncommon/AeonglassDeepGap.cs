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
/// <b>深隙</b>（CardGRW2，能力）：2 费 · 间隙容量 +3。
/// </summary>
/// <remarks>
/// <para>
/// <b>升级档 = 用一点结构换一段流量</b>（卡表 desc_up 写"间隙容量 +2"，与同族的宽幅 / 摆幅 / 容器同一套口径）：
/// 容量 3 → 2，且<b>每当你坍缩时额外抽 1 张</b>。
/// </para>
/// <para>
/// ⚠️ 之前这里写成"升级 +2 容量"（3 → 5），与卡表 desc_up 不符 ⇒ 已按卡表口径修正。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDeepGap() : ModCardTemplate(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("GapCapacity", 3m)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<AeonglassGapCapacityPower>(
            choiceContext, Owner.Creature, DynamicVars["GapCapacity"].BaseValue, Owner.Creature, this);

        if (power is not null)
        {
            power.DrawOnCollapse = IsUpgraded;
        }
    }

    /// <summary>升级：容量 3 → 2（见类注释；升级档的"坍缩时抽 1 张"写在卡面上）。</summary>
    protected override void OnUpgrade() => DynamicVars["GapCapacity"].UpgradeValueBy(-1m);

    /// <summary>升级档的额外句（卡面数值位装不下，用注入文本段，写法同差分文案）。</summary>
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
            Const.Logger.Warn($"[{Const.ModId}] 深隙：升级追加句取不到（{key}）：{ex.GetType().Name}: {ex.Message}");
            return string.Empty;
        }
    }
}

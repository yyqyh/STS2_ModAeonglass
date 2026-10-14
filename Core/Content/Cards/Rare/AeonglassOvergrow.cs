using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>蔓生</b>（CardWTH4，v2）：<b>X 费 · 技能</b> · 塞 2×X 张凋萎进怪物的弃牌堆；
/// 升级把 X 抬 2（即 2×(X+2) 张）。
/// </summary>
/// <remarks>
/// v2 两处改动：① 类型从「能力」改成「技能」（v1 借能力牌的 Limbo 做过一次性，v2 明确就是普通技能牌）；
/// ② 系数 1 → 2、升级不再"不变"（每点能量换的凋萎翻倍）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassOvergrow() : ModCardTemplate(-1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    /// <summary>每点能量换几张凋萎（v2 定值 2）。</summary>
    private const decimal PerEnergy = 2m;

    /// <summary>升级：把公式里的 X 抬 2（2×(X+2)）。</summary>
    private const int UpgradeEnergyBonus = 2;

    /// <summary>X 费：本体只在子类上开这个开关，X 值用 <c>ResolveEnergyXValue()</c> 取。</summary>
    protected override bool HasEnergyCostX => true;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("PerEnergy", PerEnergy)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var x = ResolveEnergyXValue() + (IsUpgraded ? UpgradeEnergyBonus : 0);
        var count = x * (int)DynamicVars["PerEnergy"].BaseValue;
        Const.Logger.Info($"[{Const.ModId}] 蔓生：X={x} ⇒ {VoidPollution.Inject(cardPlay.Target, count)}");
        return Task.CompletedTask;
    }

    /// <summary>升级写在公式里（X+2），不改 DynamicVars。</summary>
    protected override void OnUpgrade()
    {
    }

    /// <summary>升级改的是公式 ⇒ 卡面用「升级追加句」写清楚（键见 cards.json 的 <c>.upgraded</c>）。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        CardTextArgs.InjectUpgradeClause(description, this, IsUpgraded);
    }
}

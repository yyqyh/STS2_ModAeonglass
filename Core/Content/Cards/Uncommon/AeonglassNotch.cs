using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.History;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>刻痕</b>（CardANN2，v2）：1 费 · 8 伤（+3）；上回合你每打出 1 张牌：+3 伤（<b>无上限、不限攻击牌</b>；
/// 升级每张 +4）。
/// </summary>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassNotch() : ModCardTemplate(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    private const decimal BaseDamage = 8m;
    private const decimal PerAttack = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DamageVar(BaseDamage, ValueProp.Move),
        new DynamicVar("PerAttack", PerAttack),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        // v2：口径从"上回合的攻击牌数 + 硬上限"改成"上回合打出的**任意**牌数、无上限"。
        var played = CombatHistory.CardsPlayedLastTurn(Owner);
        var bonus = played * DynamicVars["PerAttack"].BaseValue;
        var damage = DynamicVars.Damage.BaseValue + bonus;

        Const.Logger.Info($"[{Const.ModId}] 刻痕：上回合打出 {played} 张 ⇒ +{bonus}（合计 {damage}，v2：无上限）");
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3m);
        DynamicVars["PerAttack"].UpgradeValueBy(1m);
    }
}

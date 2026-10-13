using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>打击 —— 基础 · 1 费 · 攻击 · 造成 6 点伤害。</summary>
/// <remarks>
/// <para>
/// <b>① 的占位基础牌</b>：没有它，「进战斗能站住 + 攻击动画能播」这两条验收就没法完成 ——
/// 本体 <c>AttackCommand.FromCard</c> 会把 trigger 固定成 <c>"Attack"</c>，
/// 状态机再把它映射到沙漏的 <c>attack_heavy</c>。
/// </para>
/// <para>
/// 卡面走 RitsuLib 的内嵌占位图（<c>ModCardTemplate.AssetProfile</c> 默认 <c>CardAssetProfile.Empty</c>
/// → <c>RitsuLibEmbeddedPngAssets.CardArtPlaceholder</c>），① 不要求美术。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
[RegisterCharacterStarterCard(typeof(Aeonglass), 4)]
public sealed class AeonglassStrike()
    : ModCardTemplate(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3m);
}

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// <b>打击</b>（起始基础牌 ×4）· <b>1 费 · Basic · 攻击</b> · <c>CardTag.Strike</c> —— 造成 6 点伤害（升级 9）。
/// </summary>
/// <remarks>
/// ① 效果：一次普通攻击；升级 +3 伤。
/// ② 差分：无差分（两副共用，起始各 4 张）。
/// ③ 依赖：起始牌组注册（<c>[RegisterCharacterStarterCard(..., 4)]</c>）；本体 <c>DamageCmd.Attack</c> 的 trigger 固定为 <c>"Attack"</c>，由沙漏的状态机映射到 <c>attack_heavy</c> 动画。
/// ④ ⚠️ 卡面走 RitsuLib 的内嵌占位图（<c>CardAssetProfile.Empty</c> ⇒ <c>CardArtPlaceholder</c>），不要求美术；数值按 v2 施工。
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

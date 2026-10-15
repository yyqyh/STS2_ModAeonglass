using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Intent;

using STS2_Aeonglass.Core.Systems.Wither;
namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>播种</b>（CardWTH1，差分 · v2）· <b>2 费 · Uncommon · 攻击</b> —— 两副走<b>完全不同</b>的两条路：
/// <b>永时副</b>造成 18 伤（升级 24）；<b>消逝副</b>塞 6 张凋萎（升级 8）。
/// </summary>
/// <remarks>
/// ① 效果：消逝副只塞凋萎、不造成伤害；永时副按正常攻击结算。
/// ② 差分：永时副（A）= 伤害路线（升级 +6）；消逝副（B）= 污染路线（升级 +2 张）。
/// ③ 依赖：差分判定（本卡不用差分块，直接读 <c>IsVariantB</c> 走两条路）、凋萎注入（<c>WitherEffects.Inject</c>）。
/// ④ ⚠️ v2 重写：v1 的"伤害 + 凋萎各来一点"已废；升级同时抬两副各自那半边。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSowing()
    : AeonglassVariantCardTemplate(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    /// <summary>永时副的伤害（升级 +6）。</summary>
    private const decimal DamageA = 18m;

    /// <summary>消逝副塞的凋萎张数（升级 +2）。</summary>
    private const decimal WitherB = 6m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(DamageA, ValueProp.Move), new DynamicVar("WitherB", WitherB)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        if (IsVariantB)
        {
            var wither = (int)DynamicVars["WitherB"].BaseValue;
            Const.Logger.Info($"[{Const.ModId}] 播种（消逝副）：{WitherEffects.Inject(cardPlay.Target, wither)}");
            return;
        }

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        Const.Logger.Info($"[{Const.ModId}] 播种（永时副）：{DynamicVars.Damage.BaseValue} 伤");
    }

    /// <summary>升级：两副各自那半边都抬（伤害 +6 / 凋萎 +2）。</summary>
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6m);
        DynamicVars["WitherB"].UpgradeValueBy(2m);
    }
}

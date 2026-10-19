using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;
using STS2_Aeonglass.Core.Systems.Wither;
namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// <b>潮汐</b>（CardB2，起始牌 · v2）· <b>1 费 · Basic · 攻击</b> —— 基础 9 伤（升级 12）；
/// <b>永时副</b>：改为 3 伤 + 向怪物弃牌堆塞 2 张凋萎。
/// </summary>
/// <remarks>
/// ① 效果：伤害走差分攻击模板；永时副把伤害换成定值 3，并在主伤害之后塞凋萎。
/// ② 差分：永时副（A）= 3 伤 + 2 张凋萎（升级 +1 张）；消逝副（B）= 9 伤（升级 12）。
/// ③ 依赖：差分判定（<c>DeckVariants</c> → <c>DeckPiles.DeckIndexOf</c>）、
///    凋萎注入（<c>WitherEffects.Inject</c>）、起始牌组注册（<c>[RegisterCharacterStarterCard(...,1)]</c>）。
/// ④ ⚠️ 永时副那段伤害是<b>定值、不随升级涨</b>（卡表口径）；两副共用同一张牌（孪生镜像，按"此刻在哪一副"走档）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
[RegisterCharacterStarterCard(typeof(Aeonglass), 1)]
public sealed class AeonglassTide() : AeonglassCardTemplate(
    AeonglassCardSpec.Attack(1, CardRarity.Basic, 9m, 3m)
        .Var("VariantDamage", VariantDamage)
        .Var("VariantWither", VariantWitherBase, upgradeDelta: 1m)
        .Feature(new VariantFeature((side, context) =>
        {
            if (side != DeckSide.A || context.Play.Target is not { } target)
            {
                return [];
            }

            var count = (int)context.Card.DynamicVars["VariantWither"].BaseValue;
            return
            [
                new DeckEffectBlock(
                    $"wither+{count}",
                    _ =>
                    {
                        Const.Logger.Info(
                            $"[{Const.ModId}] {Const.Prefix.Wither} 潮汐（永时副）：{WitherEffects.Inject(target, count)}");
                        return Task.CompletedTask;
                    }),
            ];
        })))
{
    /// <summary>永时副的伤害（定值 3，不随升级变 —— 卡表口径）。</summary>
    private const decimal VariantDamage = 3m;

    /// <summary>永时副塞几张凋萎（升级 +1）。</summary>
    private const decimal VariantWitherBase = 2m;

    /// <summary>永时副走定值伤害；消逝副走模板的基础伤害。</summary>
    protected override decimal DamageThisPlay => Side == DeckSide.A ? VariantDamage : base.DamageThisPlay;
}

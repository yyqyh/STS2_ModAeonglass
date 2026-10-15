using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Content.Cards.Basic;

/// <summary>
/// <b>堤岸</b>（CardB4，起始牌 · v2）· <b>1 费 · Basic · 技能</b> —— 基础 6 挡（升级 9）；
/// <b>消逝副</b>：改为 7 挡 + 本回合你受到的伤害<b>延宕 1</b>。
/// </summary>
/// <remarks>
/// ① 效果：格挡走差分格挡模板；消逝副的格挡换成定值 7，并在主格挡之后新开一个延宕实例。
/// ② 差分：永时副（A）= 6 挡（升级 9）；消逝副（B）= 7 挡（升级不变）+ 延宕 1（升级延宕 2）。
/// ③ 依赖：差分判定；延宕账本（<c>DeferralLedger</c> / <c>AeonglassDeferralPower</c>，每次获得＝一个独立实例，规则 5c）；
///    起始牌组注册（第 1 张）；与「潮汐」对称（一张数值档、一张机制档）。
/// ④ ⚠️ 全池唯一用「延宕」的牌 ⇒ 延宕管线必须先在场（2026-10-15 已改成实例列表）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
[RegisterCharacterStarterCard(typeof(Aeonglass), 1)]
public sealed class AeonglassBank() : AeonglassVariantBlockTemplate(1, CardRarity.Basic, 6m, 3m)
{
    /// <summary>消逝副的格挡（升级不变）。</summary>
    private const decimal VariantBlock = 7m;

    /// <summary>消逝副的延宕回合数（升级 +1）。</summary>
    private const decimal DelayBase = 1m;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(Block, ValueProp.Move),
        new DynamicVar("VariantBlock", VariantBlock),
        new DynamicVar("Delay", DelayBase),
    ];

    /// <summary>消逝副走定值格挡；永时副走模板的基础格挡。</summary>
    protected override decimal BlockThisPlay => IsVariantB ? VariantBlock : base.BlockThisPlay;

    /// <summary>
    /// 消逝副：<b>新增一个延宕实例</b>（规则 5c，2026-10-15 用户口径）—— 不再撤掉旧实例，直接叠上去：
    /// 每个实例各带自己的到期回合与待结算量，各自独立结算；"不可叠加"指的是同一笔伤害不会被二次延宕。
    /// </summary>
    public override IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context)
    {
        if (side != DeckSide.B)
        {
            return [];
        }

        var turns = DynamicVars["Delay"].BaseValue;
        return
        [
            new DeckEffectBlock(
                $"delay+{turns}",
                ctx => PowerCmd.Apply<AeonglassDeferralPower>(
                    ctx.ChoiceContext, Owner.Creature, turns, Owner.Creature, this)),
        ];
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3m);
        DynamicVars["Delay"].UpgradeValueBy(1m);
    }
}

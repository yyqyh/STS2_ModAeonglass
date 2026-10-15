using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Token;

/// <summary>
/// <b>时之魔法</b>（CardNEW15，delta 20261015 · <b>彩蛋，仅用于展示</b>）· <b>0 费 · 状态 · token 池</b> —— 卡面只有「虚无」2.14!。
/// </summary>
/// <remarks>
/// ① 效果：<b>没有任何效果</b>（不可打出）；用户 2026-10-15 口径："彩蛋仅用于展示"。
/// ② 差分：无差分。
/// ③ 依赖：只挂 <c>AeonglassTokenCardPool</c>（不进角色卡池，拿不到奖励 / 商店）。
/// ④ ⚠️ "游戏开始后累计 214 回合进手牌"那条触发<b>未做</b>（需要 run 级总回合计数，且效果内容本身未定）；定位是先古彩蛋，不参与强度评估。
/// </remarks>
[RegisterCard(typeof(AeonglassTokenCardPool))]
public sealed class AeonglassTimeMagic : ModCardTemplate
{
    public AeonglassTimeMagic() : base(0, CardType.Status, CardRarity.Rare, TargetType.Self)
    {
    }

    /// <summary>不可打出（展示件）。</summary>
    protected override bool IsPlayable => false;

    protected override HashSet<CardTag> CanonicalTags => [];
}

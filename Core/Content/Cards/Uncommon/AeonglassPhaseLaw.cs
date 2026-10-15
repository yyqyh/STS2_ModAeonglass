using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>相位律</b>（CardPH7，delta 20261015）· <b>1 费 · Uncommon · 能力</b> —— 每回合开始时按相位给收益：沙砾侧 <b>+1 能量</b> / 平衡档 <b>3 点格挡</b> / 空隙侧<b>抽 1 张</b>；消逝副三档镜像换位。
/// </summary>
/// <remarks>
/// ① 效果：打出即挂 <c>AeonglassPhaseLawPower</c>（点数只当"在场"标记）；升级不变（升级档与基础档同文）。
/// ② 差分：<b>消逝副（B）</b> = 沙砾侧抽 1 / 平衡档 3 挡 / 空隙侧 +1 能量。
/// ③ 依赖：相位读口 <c>SandPhases.Of</c>（沙砾 ≥4 / =3 / ≤2 三档）+ 差分判定。
/// ④ ⚠️ 1 费每回合白拿 1 费，三档都在给东西、没有空档 ⇒ 卡池侧标"极强引擎"；要削只改能力里那三个常量。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassPhaseLaw() : AeonglassPowerCardTemplate(1, CardRarity.Uncommon)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
        => await PowerCmd.Apply<AeonglassPhaseLawPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

    /// <summary>升级不变（delta 20261015：升级档与基础档同文）。</summary>
    protected override void OnUpgrade()
    {
    }
}

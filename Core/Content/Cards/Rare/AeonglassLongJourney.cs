using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>远行</b>（NEW-21）· <b>1 费 · Rare · 技能 · 搁浅 10</b> —— 打出后自身搁浅 10 场；
/// <b>若在 Boss 层回归</b>，进卡组前随机升级卡组里 3 张。升级：费用变为 0。
/// </summary>
/// <remarks>
/// <para>
/// ① 落点与倒计时照 <c>AeonglassStasis</c> 那套：打出后不回任何牌堆（<c>PileType.None</c>），
/// 把牌组母本交给搁浅区，倒计时存在卡上的 <c>[SavedProperty] CombatsLeft</c>（随存档走）。
/// </para>
/// <para>
/// ② "在 Boss 层回归"= 刚打完的那场是 Boss 房（用户 2026-10-18 口径）；判定与升级逻辑在
/// <c>StasisReturn.TickAfterCombatEnd</c>（<c>LastCombatWasBoss</c> 由 <c>StasisRunHook</c> 在每次战斗结束时写入）。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassLongJourney() : AeonglassStasisCardTemplate(
    new AeonglassCardSpec(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
        .UpgradeEnergy(-1),
    StrandCombatsConst)
{
    /// <summary>搁浅几场（台账 strand=10；超出已定的 2/3/4 档，按原文保留）。</summary>
    private const int StrandCombatsConst = 10;

}

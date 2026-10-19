using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>破片</b>（CardNEW10，delta 20261015）· <b>1 费 · Common · 攻击</b> —— 造成 12 点伤害（升级 18）· 搁浅 1。
/// </summary>
/// <remarks>
/// ① 效果：基础 12 伤；升级 +6 ⇒ 18 伤。
/// ② 差分：无差分。
/// ③ 依赖：搁浅区 + 搁浅关键词；打出后不回牌堆，1 场战斗后归队。费用列原为空，按 1 费落地。
/// ④ ✅ 合规：1 费基线 8 × 搁浅 1 的 1.5 ＝ 12（卡池侧结论）；若用户别给数值会追发。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassFragment()
    : AeonglassStasisCardTemplate(
        AeonglassCardSpec.Attack(1, CardRarity.Common, 12m, 6m),
        strandCombats: 1);

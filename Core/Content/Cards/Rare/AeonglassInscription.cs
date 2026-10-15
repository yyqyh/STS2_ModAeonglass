using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>铭刻</b>（CardSTA3，v2 + delta 20261015）· <b>2 费 · Rare · 攻击</b> —— 造成 32 点伤害（升级 38）· 搁浅 3。
/// </summary>
/// <remarks>
/// ① 效果：基础 32 伤；升级 +6 ⇒ 38 伤。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：搁浅区（<c>ExtraPileModules</c> / <c>StasisStore</c>）+ 搁浅关键词；打出后不回牌堆，3 场战斗后归队。
/// ④ ⚠️ 口径：2026-10-14 费用 3→2；2026-10-15 delta 伤害 27/33 → <b>32/38</b>；
///    2 费基线 15 × 搁浅 3 溢价 1.8 ＝ 27 ⇒ 32 已到 <b>×2.13</b>（卡池侧风险清单）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassInscription()
    : AeonglassStasisAttackTemplate(2, CardRarity.Rare, 32m, 6m, strandCombats: 3);

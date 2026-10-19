using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>封沙</b>（CardSTA4，v2 + delta 20261015）· <b>2 费 · Rare · 技能</b> —— 获得 30 点格挡（升级 34）· 搁浅 3。
/// </summary>
/// <remarks>
/// ① 效果：基础 30 挡；升级 +4 ⇒ 34 挡。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：搁浅区（<c>ExtraPileModules</c> / <c>StasisStore</c>）+ 搁浅关键词；打出后不回牌堆，3 场战斗后归队。
/// ④ ⚠️⚠️ 口径：2026-10-14 费用 3→2；2026-10-15 delta 格挡 16/20 → <b>30/34</b>；
///    2 费基线 11 挡 × 1.8 ＝ 19.8 ⇒ 30 ＝ <b>×2.7 明显超模</b>（按用户数字先做，风险清单已记）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSandSeal()
    : AeonglassStasisCardTemplate(
        AeonglassCardSpec.Block(2, CardRarity.Rare, 30m, 4m),
        strandCombats: 3);

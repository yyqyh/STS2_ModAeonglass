using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>泊岸</b>（新卡 2026-10-19）· <b>0 费 · Uncommon · 技能</b> · <b>搁浅 1 · 固有</b> ——
/// 获得 10 点格挡（升级 13）。
/// </summary>
/// <remarks>
/// ① 效果：纯格挡 + 搁浅 1（打出后离队 1 场，之后归队）。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：<see cref="AeonglassStasisCardTemplate" />（<c>IStasisCard</c> 的存档倒计时）+ 本体固有。
/// ④ ⚠️ 0 费 10 挡属于"搁浅"的预付收益；数值按设计表原样。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDock()
    : AeonglassStasisCardTemplate(
        AeonglassCardSpec.Block(0, CardRarity.Uncommon, 10m, 3m)
            .Keyword(CardKeyword.Innate),
        strandCombats: 1);

using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>定潮</b>（新卡 2026-10-19）· <b>1 费 · Uncommon · 攻击</b> · <b>迟滞 · 固有</b> ——
/// 造成 12 点伤害（升级 18）。
/// </summary>
/// <remarks>
/// ① 效果：纯数值攻击，走模板声明。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：<see cref="IHysteresisCard" /> + 本体固有（起手必抽）。
/// ④ ⚠️ 数值按设计表原样：12 / 18；1 费 12 伤略高于同档基准（11），作为"固有 + 迟滞"的补偿。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassTideLock()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Attack(1, CardRarity.Uncommon, 12m, 6m)
            .Keyword(HysteresisKeyword.Value)
            .Keyword(CardKeyword.Innate)),
    IHysteresisCard;

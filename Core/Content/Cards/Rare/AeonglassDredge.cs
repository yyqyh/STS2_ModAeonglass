using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>沉底淘牌</b>（NEW-34，2026-10-20）· <b>1 费 · Rare · 能力</b> ——
/// 回合开始时，丢弃抽牌堆底部 3 张牌；打出其中带[迟滞]的牌，消耗其中状态牌（升级 4 张）。
/// </summary>
/// <remarks>
/// ① 效果：底部 N 张逐张分流 —— 迟滞牌自动打出 / 状态牌消耗 / 其余进弃牌堆。
/// ② 交接单未写"其余牌"的落点 ⇒ 按现有牌堆规则落到弃牌堆（已在交接单备注要求记录）。
/// ③ 依赖：<c>AeonglassDredgePower</c>（<c>BeforeHandDraw</c> 在常规抽牌之前处理）。
/// ④ ⚠️ 自动打出的迟滞牌若本身不可打出，本体 <c>CardCmd.AutoPlay</c> 会按既有规则处理；
///    底部张数不足时按实际张数处理。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDredge()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Power(1, CardRarity.Rare)
            .Var("Pick", 3m, upgradeDelta: 1m)
            .Effect(CardEffects.ApplyPower<AeonglassDredgePower>("Pick")));

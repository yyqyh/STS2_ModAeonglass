using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>无锚漂流</b>（NEW-36，2026-10-20）· <b>1 费 · Rare · 能力</b> ——
/// 回合开始时随机生成一张[搁浅]牌。升级：获得[固有]。
/// </summary>
/// <remarks>
/// ① 效果：能力在玩家回合开始生成一张随机搁浅牌（手牌满则进弃牌堆）。
/// ② 随机池 = 本模组所有实现 <c>IStasisCard</c> 的可获取牌；费用/稀有度按模型原值。
/// ③ 依赖：<c>AeonglassDriftwoodPower</c> + RitsuLib 自动注册 + <c>CardPileCmd.AddGeneratedCardToCombat</c>。
/// ④ ⚠️ 生成池在首次使用时缓存；新增搁浅卡后需重启游戏刷新。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDriftwood()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Power(1, CardRarity.Rare)
            .UpgradeAddKeyword(CardKeyword.Innate)
            .Effect(CardEffects.ApplyPower<AeonglassDriftwoodPower>()));

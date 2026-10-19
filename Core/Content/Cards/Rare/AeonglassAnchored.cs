using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>锚定之物</b>（NEW-8）· <b>6 费 · Rare · 技能</b> —— ［保留］［迟滞］；战斗开始时直接进入弃牌堆；
/// 进入手牌时对所有敌人造成 90 点伤害。
/// </summary>
/// <remarks>
/// <para>
/// ① 三个机制：<b>保留</b> = 本体 <c>CardKeyword.Retain</c>（升级去掉它）；<b>迟滞</b> = 本模组的
/// <see cref="IHysteresisCard" /> 标记接口（卡定义级，见 <c>Hysteresis</c>：洗牌时跳过、留在弃牌堆）；
/// <b>进手牌触发</b> = <c>AnchoredTrigger</c> 订阅手牌堆的 <c>CardAdded</c>（任何方式进手牌都算）。
/// </para>
/// <para>
/// ② 起手进弃牌堆也由 <c>AnchoredTrigger.PlaceStartInDiscard</c> 在初始洗牌那一刻做（见其注释）。
/// </para>
/// <para>
/// ③ ⚠️ 用户 2026-10-16 已确认的连锁后果：它自带迟滞 + 起手就在弃牌堆 ⇒ <b>战斗里不会被抽到</b>，
/// 只能靠主动捞回（例：同批的「返潮」）才进手牌 ⇒ 90 伤不是每回合可打的。
/// </para>
/// <para>
/// ④ 打出它本身没有效果（表里只写了"起手进弃牌堆 / 进手牌触发"两件事）—— 这里故意留空实现，不擅自加内容。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAnchored() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(6, CardRarity.Rare, TargetType.AllEnemies)
        .Keyword(CardKeyword.Retain)
        .Keyword(HysteresisKeyword.Value)
        .UpgradeRemoveKeyword(CardKeyword.Retain)),
    IHysteresisCard
{
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 锚定之物：打出（本身无额外效果 —— 伤害在「进入手牌」时结算）");
        return Task.CompletedTask;
    }

}

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>终章</b>（RW-5）—— <b>占位卡</b>：按 P9 挂起、不细化（费用与效果都待定）。
/// </summary>
/// <remarks>
/// <para>
/// 设计原文：回溯后<b>同时回归手牌和卡组</b>。这条先不动，因为 P9 的"回溯产物归属"本身还没拍。
/// </para>
/// <para>
/// <b>注册在 token 池</b>（<see cref="AeonglassTokenCardPool" />，共享池 = 世界里可解析、
/// 但不进任何角色的奖励池）⇒ 它<b>拿不到</b>，只在 ModelDb 里占着这个卡位、留着给调试台生成。
/// 等 P9 定了再把它移进正式卡池并补费用与效果。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassTokenCardPool))]
public sealed class AeonglassFinalChapter() : ModCardTemplate(0, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Placeholder", 0m)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Const.Logger.Warn($"[{Const.ModId}] 终章：占位卡被打出（效果未定，按 P9 挂起）");
        return Task.CompletedTask;
    }
}

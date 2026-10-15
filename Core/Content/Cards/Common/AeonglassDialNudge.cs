using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.Sand;

namespace STS2_Aeonglass.Core.Content.Cards.Common;

/// <summary>
/// <b>拨针</b>（CardUTL1，v2）· <b>0 费 · Common · 技能</b> —— 抽 1 弃 1；<b>平衡档</b>（沙砾＝3）改为抽 2 弃 1。
/// </summary>
/// <remarks>
/// ① 效果：基础 抽 1 弃 1；平衡档 抽 2 弃 1。升级：抽 2 弃 1，平衡档 抽 3 弃 2（数值写在 <c>OnPlay</c> 里，不走 DynamicVars）。
/// ② 差分：无差分（"平衡档"是相位条件，不是副差分）。
/// ③ 依赖：相位读口 <c>SandPhases.IsBalanced</c>（沙砾＝3）+ 弃牌助手 <c>HandDiscard.FromHandAsync</c>。
/// ④ ⚠️ 升级档把"平衡档"那半边一起抬（抽 3 弃 2）⇒ 升级增量比其他卡大；改口径只动 <c>OnPlay</c> 里那两行。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassDialNudge() : ModCardTemplate(0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // v2 口径：
        //   基础 抽 1 弃 1；平衡档（沙砾＝3）抽 2 弃 1。
        //   升级 抽 2 弃 1；平衡档抽 3 弃 2。
        var up = IsUpgraded ? 1 : 0;
        var balanced = SandPhases.IsBalanced(Owner);

        var draw = 1 + up + (balanced ? 1 : 0);
        var discard = 1 + (up + (balanced ? 1 : 0) >= 2 ? 1 : 0);

        await CardPileCmd.Draw(choiceContext, draw, Owner);
        await HandDiscard.FromHandAsync(choiceContext, this, discard);
    }

    /// <summary>升级内容写在 <c>OnPlay</c> 里（抽/弃两半边各抬 1），所以这里不需要改 DynamicVars。</summary>
    protected override void OnUpgrade()
    {
    }

    /// <summary>升级档的抽/弃两档卡面装不下 ⇒ 用「升级追加句」注入（键见 cards.json 的 <c>.upgraded</c>）。</summary>
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        CardTextArgs.InjectUpgradeClause(description, this, IsUpgraded);
    }
}

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Keywords;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.Wither;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>倒影</b>（新卡 2026-10-19）· <b>0 费 · Uncommon · 技能</b> · <b>迟滞</b> ——
/// 当你[倒置]沙漏时，立刻进入手牌；将 8 张[凋萎]放入该敌人的弃牌堆（升级 12）。
/// </summary>
/// <remarks>
/// ① 效果：打出时塞凋萎；之后每次沙砾倒置，<c>InvertRecall</c> 把在抽/弃牌堆里的它拉回手牌。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：<c>SandInvertCmd</c> 的翻转通知（<c>InvertRecall.PullAsync</c>）+ 凋萎注入口。
/// ④ ⚠️ 从消耗堆不回手（消耗是永久移除）；已在手牌时不重复搬运。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassReflection()
    : AeonglassCardTemplate(
        AeonglassCardSpec.Skill(0, CardRarity.Uncommon, TargetType.AnyEnemy)
            .Keyword(HysteresisKeyword.Value)
            .Var("Wither", WitherBase, upgradeDelta: 4m)),
    IHysteresisCard
{
    private const decimal WitherBase = 8m;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Target is not { } target)
        {
            return;
        }

        var count = (int)DynamicVars["Wither"].BaseValue;
        var injected = WitherEffects.Inject(target, count);
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} 倒影：{injected}");

        await Task.CompletedTask;
    }
}

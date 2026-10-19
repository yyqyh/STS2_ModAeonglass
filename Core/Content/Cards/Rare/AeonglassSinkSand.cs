using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>沉沙</b>（新卡 2026-10-19）· <b>1 费 · Rare · 技能</b> ——
/// 选择抽牌堆中的一张牌，将其变为[凋萎]并给予其[迟滞]；抽 3 张牌。升级：费用 −1。
/// </summary>
/// <remarks>
/// ① 效果：选牌 → 用本体 <c>CardCmd.Transform</c> 原地换成一张真的凋萎卡 → 给新卡打迟滞 → 抽 3。
/// ② 差分：无差分（两副同文）。
/// ③ 依赖：<c>CardSelectCmd.FromCombatPile</c>（抽牌堆选牌）+ <c>CardCmd.Transform</c> +
///    <c>Hysteresis.Mark</c>（凋萎随后不再参与洗牌，等于把一张牌永久沉进弃牌堆）。
/// ④ ⚠️ 需要选牌界面 ⇒ headless 只能验到"抽牌堆为空/取消"分支，主路径实机验
///    （与拨针 / 沙漏倾倒 / 复写同类）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassSinkSand() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Rare)
        .UpgradeEnergy(-1))
{
    private const string PromptKey = "STS2_AEONGLASS_PROMPT_SINK_SAND";

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState is not { } piles)
        {
            return;
        }

        // 乱流生效时 DrawPile 属性已经对调 ⇒ 玩家看到的"抽牌堆"就是这里选的这张。
        var draw = piles.DrawPile;
        if (draw.Cards.Count == 0)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} 沉沙：抽牌堆是空的（只抽 3）");
            await CardPileCmd.Draw(choiceContext, 3, Owner);
            return;
        }

        var picked = (await CardSelectCmd.FromCombatPile(
            choiceContext,
            draw,
            Owner,
            new CardSelectorPrefs(new LocString("static_hover_tips", PromptKey), 1),
            card => card is not AeonglassWitherCard)).ToList();

        if (picked.FirstOrDefault() is not { } chosen)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Wither} 沉沙：没有选中牌（只抽 3）");
            await CardPileCmd.Draw(choiceContext, 3, Owner);
            return;
        }

        if (chosen.CombatState is not { } combat)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Wither} 沉沙：选中牌没有战斗态 ⇒ 跳过变换");
            await CardPileCmd.Draw(choiceContext, 3, Owner);
            return;
        }

        var wither = combat.CreateCard(ModelDb.Card<AeonglassWitherCard>(), Owner);
        await CardCmd.Transform(chosen, wither);
        Hysteresis.Mark(wither);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Wither} 沉沙：{chosen.Id.Entry} → 凋萎（已打迟滞）");

        await CardPileCmd.Draw(choiceContext, 3, Owner);
    }
}

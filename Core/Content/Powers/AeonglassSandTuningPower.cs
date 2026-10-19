using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Cards;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>调沙</b>（能力本体）：你的回合结束时，从手牌选一张[迟滞]牌置于抽牌堆顶。
/// </summary>
/// <remarks>
/// ① 职责：只在<b>玩家方</b>回合结束触发一次；手牌里没有迟滞牌就静默跳过。
/// ② 挂点：<see cref="AfterSideTurnEnd" />（本体在弃手牌之前调用）。
/// ③ 依赖：<c>Hysteresis.IsStuck</c> 判迟滞 + 手牌选牌界面。
/// ④ ⚠️ 叠层只增加图标数字，不增加每回合的选择次数（口径：一张能力一回合一次）。
/// </remarks>
[RegisterPower]
public sealed class AeonglassSandTuningPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private const string PromptKey = "STS2_AEONGLASS_PROMPT_SAND_TUNING";

    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player
            || Owner.Player is not { } player
            || player.PlayerCombatState is not { } piles)
        {
            return;
        }

        if (!piles.Hand.Cards.Any(Hysteresis.IsStuck))
        {
            return;
        }

        var picked = (await CardSelectCmd.FromHand(
            choiceContext,
            player,
            new CardSelectorPrefs(new LocString("static_hover_tips", PromptKey), 1),
            Hysteresis.IsStuck,
            this)).ToList();

        if (picked.FirstOrDefault() is not { } chosen)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 调沙：没有选中迟滞牌");
            return;
        }

        await CardPileCmd.Add(chosen, PileType.Draw, CardPilePosition.Top);
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 调沙：{chosen.Id.Entry} → 抽牌堆顶");
    }
}

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>高维观测</b>（CardINT6，v2）· <b>2 费 · Rare · 能力</b> —— 每回合开始时：若你<b>上回合操作过意图</b>，抽 1 张牌；
/// <b>消逝副</b>：改为获得 1 点能量。升级不变。
/// </summary>
/// <remarks>
/// ① 效果：打出即挂 <c>AeonglassHighObservePower</c>（点数 1 = 抽几张 / 给几点能量），并把"哪一副"写进能力。
/// ② 差分：永时副（A）= 抽 1 张；消逝副（B）= 获得 1 点能量。
/// ③ 依赖：能力层 <c>AeonglassHighObservePower</c> 读 <c>CombatHistory.IntentManipulationsLastTurn</c>（推迟 / 提前 / 剥夺都算）；
///    <b>副在打出那一刻定型</b>（<c>EnergyVariant</c>），因为能力会跨副存留。
/// ④ ⚠️ "上回合操作过"只看**上**回合的计数；本回合操作 ⇒ 下回合吃收益（与「年轮 / 刻痕」同一套历史层口径）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassHighObserve() : AeonglassCardTemplate(AeonglassCardSpec.Power(2, CardRarity.Rare))
{
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AeonglassHighObservePower>(
            choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        // 副在打出这一刻写进能力（能力会跨副存留，不能等触发时再读"现在在哪一副"）。
        var variantB = DeckVariants.SideOf(Owner, this) == DeckSide.B;
        foreach (var power in Owner.Creature.Powers.OfType<AeonglassHighObservePower>())
        {
            power.EnergyVariant = variantB;
        }

        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 高维观测：挂上（{(variantB ? "消逝副 ⇒ 给能量" : "永时副 ⇒ 抽牌")}）");
    }
}

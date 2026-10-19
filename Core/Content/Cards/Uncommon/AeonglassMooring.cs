using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Stasis;

namespace STS2_Aeonglass.Core.Content.Cards.Uncommon;

/// <summary>
/// <b>停泊</b>（NEW-15）· <b>1 费 · Uncommon · 技能</b> —— 给予一张手牌「搁浅 2」。
/// </summary>
/// <remarks>
/// <para>
/// ① X 取值：台账 `strand_x=2`（罕见档默认 2）⇒ 搁浅 2 场（内部按 X+1=3 存，打出这一场不计）。
/// </para>
/// <para>
/// ② 「给予一张手牌搁浅」= 把这张手牌的<b>牌组母本</b>交给搁浅区（它因此离队），手牌里那张战斗副本同时撤掉。
/// 任意卡都能被搁浅靠的是 2026-10-18 新加的 <c>StasisStore</c> 倒计时侧表（不实现 IStasisCard 的卡也能倒计时）。
/// </para>
/// <para>
/// ③ ⚠️ 需要选牌界面 ⇒ headless 测试覆盖不到（与拨针 / 沙漏倾倒 / 复写同类，已进 sweep 的"需选择界面"跳过表）。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassMooring() : AeonglassCardTemplate(
    AeonglassCardSpec.Skill(1, CardRarity.Uncommon)
        .UpgradeEnergy(-1))
{
    /// <summary>搁浅几场（台账 strand_x）。</summary>
    private const int StrandX = 2;

    /// <summary>选择界面的提示文案（我们自己的表，与既有 PROMPT_* 同命名）。</summary>
    private const string PromptKey = "STS2_AEONGLASS_PROMPT_MOORING";

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var prefs = new CardSelectorPrefs(new LocString("static_hover_tips", PromptKey), 1);
        var picked = await CardSelectCmd.FromHand(
            choiceContext, Owner, prefs, card => !ReferenceEquals(card, this), this);

        if (picked.FirstOrDefault() is not { } chosen)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 停泊：没有选中任何手牌（什么都不做）");
            return;
        }

        if (chosen.DeckVersion is not { } master)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Stasis} 停泊：{chosen.Id.Entry} 没有牌组母本（战斗内造的牌）⇒ 搁浅不生效");
            return;
        }

        chosen.DeckVersion = null;   // 断链：它已经不是"哪张母本的战斗副本"了
        var ok = await StasisReturn.SendToZone(Owner, master, StrandX);

        if (ok)
        {
            try
            {
                await CardPileCmd.RemoveFromCombat(chosen);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Stasis} 停泊：撤下手牌副本失败（忽略）：{ex.GetType().Name}: {ex.Message}");
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Stasis} 停泊：{master.Id.Entry} 搁浅 {StrandX} 场（成功={ok}）");
    }

}

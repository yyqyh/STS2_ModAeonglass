using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.AlphaBeta;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>α or β</b>（CardNEW14，delta 20261015 · 正式名由用户 2026-10-15 给定）· <b>0 费 · Rare · 技能</b> ——
/// 回溯后进入手牌（卡组保留一份）；若未使用回溯：永时 / 流逝各给一条被动。
/// </summary>
/// <remarks>
/// ① 效果：本卡自身没有立即效果（<c>OnPlay</c> 只记一行日志）；升级 = 获得[固有]。
/// ② 差分：<b>永时副（A）</b> = 给予凋萎时额外 +1 张；<b>流逝副（B）</b> = 「坍缩」「唤回」只消耗 1 沙砾。
/// ③ 依赖：<c>AlphaBetaPassive</c>（带着这张牌 + 本局未用过回溯才生效，读 <c>RunHistory.Rewinds</c>）、
///    <c>WitherPool.Inject</c> / <c>GapStore.ActionCostFor</c>；回溯 接缝 <c>AlphaBetaRewindExtension</c>（order 260：
///    每次回溯把散落实例收回卡组并补发一张进手牌，手牌已有就不叠）。
/// ④ ⚠️ 踩红线：「流逝」＝给 时空裂隙 降门槛（P11 守则 4），卡池侧已标红；替代原「唯一时间线」（RW-5 占位已关）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassAlphaBeta()
    : ModCardTemplate(0, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    /// <summary>0 费、无立即效果（效果全是"带着它就有的"被动 + 回溯接缝）。</summary>
    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        Const.Logger.Info(
            $"[{Const.ModId}] α or β：打出（被动照旧由 α/β 台账判定；回溯接缝见风险清单）");
        return Task.CompletedTask;
    }

    /// <summary>升级 = 获得[固有]（本体关键词）。</summary>
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}

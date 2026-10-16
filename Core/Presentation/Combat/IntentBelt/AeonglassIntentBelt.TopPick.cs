using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Presentation.Support;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
/// <summary>「拆解」用的<b>顶上选牌面板</b>（<c>AeonglassIntentBelt</c> 的 partial 部分）。</summary>
/// <remarks>
/// ① 职责：把某只怪抽牌堆顶的几张意图画成真卡横排，点一张回调一次并关闭。
/// ② 调用时机：卡牌指向流程调 <c>BeginTopPick</c>；<c>CloseTopPick</c> 收尾。
/// ③ 依赖：<c>AeonglassIntentCardView</c> + 意图 意图状态。
/// ④ ⚠️ 面板挂在卡带上（坐标同源、怪死一并释放），所以生命周期跟着卡带而不是卡牌。
/// </remarks>

public partial class AeonglassIntentBelt : Control
{

    /// <summary>
    /// 接缝实现：按节点名找到 <paramref name="creature" /> 的卡带，把选牌面板开在它身上。
    /// </summary>
    /// <remarks>找不到那只怪的卡带（怪死了 / 还没挂）就返回 false —— 卡那一侧会按"没选到"处理。</remarks>
    private static bool FindAndBeginTopPick(
        Creature creature,
        IReadOnlyList<IntentCard> cards,
        Func<IntentCard, string> onPick)
    {
        var anchor = MonsterAccess.NodeOf(creature)?.IntentContainer;
        if (anchor?.GetParent() is not { } parent)
        {
            return false;
        }

        var belt = parent.GetNodeOrNull<AeonglassIntentBelt>(NodeName(creature));
        return belt is not null && belt.BeginTopPick(cards, onPick);
    }

    /// <summary>
    /// <b>「拆解」用的选牌面板</b>（INT-4）：把这只怪<b>抽牌堆顶</b>的几张意图画成真卡横排，点一张回调一次并关闭。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 挂在卡带上的理由：卡带本来就在这只怪的意图区上方（坐标同源），怪死了会连带释放，不用另做生命周期。
    /// </para>
    /// <para>
    /// 面板**不自己改牌堆**：点中的那张交给回调（卡 → <c>IntentMoves.Dismantle</c>），那边会再确认一次
    /// "这张还在抽牌堆里"—— 面板开着的这段时间里，回合推进 / 洗牌都可能把它挪走。
    /// </para>
    /// </remarks>
    /// <returns>面板是否真的打开了（拿不到玩家 / 没有可画的卡都返回 false）。</returns>
    internal bool BeginTopPick(IReadOnlyList<IntentCard> cards, Func<IntentCard, string> onPick)
    {
        CloseTopPick();
        if (_creature is null || PlayerOf() is not { } player || cards.Count == 0)
        {
            return false;
        }

        try
        {
            var panel = new PanelContainer
            {
                Name = "AeonglassTopPick",
                MouseFilter = MouseFilterEnum.Stop,
                ZIndex = 210,
                Modulate = new Color(1f, 1f, 1f, 0.97f),
            };

            var box = new VBoxContainer();
            box.AddChild(UiFactory.MakeLabel($"拆解 · 抽牌堆顶 {cards.Count} 张（点一张丢进它的弃牌堆）", 11, Colors.White));

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", (int)SlotGap);

            var shown = 0;
            foreach (var card in cards)
            {
                var view = AeonglassIntentCardView.Create(
                    row, new Vector2(SlotWidth, SlotHeight),
                    IntentCardBinder.InstanceFor(player, _creature, card));
                if (view is null)
                {
                    continue;
                }

                view.Bind(card, _creature, card.IsVoid ? $"凋萎 {card.HitPointsOf()}" : null);
                view.SetDim(!card.Playable);

                var picked = card;
                view.SetTargeting(() =>
                {
                    var text = onPick(picked);
                    Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Intent} 拆解点击：{picked.Describe()} ⇒ {text}");
                    CloseTopPick();
                });

                shown++;
            }

            if (shown == 0)
            {
                panel.QueueFreeSafely();
                return false;
            }

            box.AddChild(row);
            panel.AddChild(box);
            AddChild(panel);
            // 与牌堆小卡同口径：减掉"视觉保持量"，面板留在玩家原来看到的位置。
            panel.Position = TopPickOffset - CardVisualHold;
            _picker = panel;

            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Intent} 拆解：打开抽牌堆顶选牌面板（{shown} 张，可点选）");
            return true;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 拆解面板打开失败：{ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>关掉「拆解」面板（没有就什么都不做）。</summary>
    internal void CloseTopPick()
    {
        if (_picker is { } existing && IsInstanceValid(existing))
        {
            existing.QueueFree();
        }

        _picker = null;
    }
}

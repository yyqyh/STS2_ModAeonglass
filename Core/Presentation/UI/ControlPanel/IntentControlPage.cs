using Godot;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Presentation.UI.ControlPanel;

/// <summary>M3/M5「操作时间 + 凋萎」页：三个动词 + 塞虚无 + 剧本按钮。</summary>
/// <remarks>
/// <para>
/// 意图操纵的<b>正式</b>入口是卡牌（动作卡指向某一张意图）；这一页只是把机制跑通的临时代理，
/// 所以「推迟 / 提前 / 剥夺」作用在"第一张 / 最后一张可行动的意图"上（卡牌走的是"指定某一张"）。
/// </para>
/// <para>
/// 下半排是<b>剧本按钮</b>（条件✗ / 强制 / 消耗 / 守恒）：用来现场构造"条件未满足""条件意图"这类手牌，
/// 验证判定优先级与守恒式。它们会改怪的手牌元数据，只该在测试里用。
/// </para>
/// </remarks>
internal sealed partial class IntentControlPage : ControlPage
{
    internal override string Title => "意图";

    internal override bool IsAvailable => true;

    protected override void BuildBody(VBoxContainer body)
    {
        var verbs = Grid(3);
        verbs.AddChild(Btn("推迟", () => Run("推迟", EnemyTimeModule.Postpone)));
        verbs.AddChild(Btn("提前", () => Run("提前", EnemyTimeModule.Advance)));
        verbs.AddChild(Btn("剥夺", () => Run("剥夺", EnemyTimeModule.Deprive)));
        verbs.AddChild(Btn("塞虚无", () => Run("塞虚无", EnemyTimeModule.InjectVoid)));
        verbs.AddChild(Btn("切换目标", CycleTarget));
        verbs.AddChild(Btn("守恒", CheckConservation));

        Section("剧本（改手牌元数据）");
        var flags = Grid(3);
        flags.AddChild(Btn("条件✗", () => Run("条件✗", (p, c) => EnemyTimeModule.CycleFlag(p, c, "condition"))));
        flags.AddChild(Btn("条件意图", () => Run("条件意图", (p, c) => EnemyTimeModule.CycleFlag(p, c, "forced"))));
        flags.AddChild(Btn("消耗", () => Run("消耗", (p, c) => EnemyTimeModule.CycleFlag(p, c, "consume"))));

        Text("判定优先级（三档）：条件已满足的条件意图 → 第一张意图（不再看条件）→ 眩晕。");
        Text("动作都作用在当前焦点怪（「切换目标」换焦点）。");
    }

    protected override string Status()
    {
        var player = Player();
        if (player is null)
        {
            return "不在战斗";
        }

        return EnemyTimeModule.Describe(player, MonsterAccess.Focus);
    }

    private void Run(string label, Func<Player, Creature, string> action)
    {
        LogPress(label);

        if (Player() is not { } player)
        {
            Show("不在战斗", true);
            return;
        }

        if (MonsterAccess.Focus is not { } target)
        {
            Show("没有目标怪", true);
            return;
        }

        try
        {
            Show(action(player, target));
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Intent} 面板动作抛异常：{ex.GetType().Name}: {ex.Message}");
            Show($"error: {ex.GetType().Name}: {ex.Message}", true);
        }
    }

    private void CycleTarget()
    {
        LogPress("切换目标");

        var target = MonsterAccess.CycleFocus();
        Show($"目标 → {MonsterAccess.IdOf(target)}");
        Refresh();
    }

    private void CheckConservation()
    {
        LogPress("守恒");

        if (MonsterAccess.Focus is not { } target)
        {
            Show("没有目标怪", true);
            return;
        }

        var text = IntentDeck.ConservationCheck(target);
        Show(text, text.Contains("BROKEN"));
    }
}

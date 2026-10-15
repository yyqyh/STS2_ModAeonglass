using Godot;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Presentation.ControlPanel;
/// <summary>意图页：读数 + 洗乱 / 推迟 / 提前 / 剥夺的验证按钮。</summary>
/// <remarks>
/// ① 职责：显示某只怪的意图手牌与判定，提供四个动词按钮。
/// ② 调用时机：页签打开与按钮点击。
/// ③ 依赖：意图 敌人时间（<c>IntentTurnFlow</c> / <c>IntentDeck</c>）。
/// ④ ⚠️ 只是验证入口；玩法侧仍走卡牌（提前 / 威吓 / 思维剥夺）。
/// </remarks>

internal sealed partial class IntentControlPage : ControlPage
{
    internal override string Title => "意图";

    internal override bool IsAvailable => true;

    protected override void BuildBody(VBoxContainer body)
    {
        var verbs = Grid(3);
        verbs.AddChild(Btn("推迟", () => Run("推迟", IntentModule.Postpone)));
        verbs.AddChild(Btn("提前", () => Run("提前", IntentModule.Advance)));
        verbs.AddChild(Btn("剥夺", () => Run("剥夺", IntentModule.Deprive)));
        verbs.AddChild(Btn("塞虚无", () => Run("塞虚无", IntentModule.InjectVoid)));
        verbs.AddChild(Btn("切换目标", CycleTarget));
        verbs.AddChild(Btn("守恒", CheckConservation));

        Section("剧本（改手牌元数据）");
        var flags = Grid(3);
        flags.AddChild(Btn("条件✗", () => Run("条件✗", (p, c) => IntentModule.CycleFlag(p, c, "condition"))));
        flags.AddChild(Btn("条件意图", () => Run("条件意图", (p, c) => IntentModule.CycleFlag(p, c, "forced"))));
        flags.AddChild(Btn("消耗", () => Run("消耗", (p, c) => IntentModule.CycleFlag(p, c, "consume"))));

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

        return IntentModule.Describe(player, MonsterAccess.Focus);
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

using Godot;

using STS2_Aeonglass.Core.Presentation.Combat;
using STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Stasis;
namespace STS2_Aeonglass.Core.Presentation.ControlPanel;
/// <summary>搁浅页：读数 + "闪一次顶栏搁浅按键"的验证。</summary>
/// <remarks>
/// ① 职责：显示搁浅区状态，提供闪烁验证按钮。
/// ② 调用时机：页签打开与按钮点击。
/// ③ 依赖：<c>StasisStore</c> + <c>AeonglassStasisPileFlash</c>。
/// ④ ⚠️ 搁浅 没有玩家主动动作（入口是卡牌本身），这页只有读数与验证。
/// </remarks>

internal sealed partial class StasisControlPage : ControlPage
{
    internal override string Title => "搁浅";

    internal override bool IsAvailable => StasisStore.IsReady;

    protected override void BuildBody(VBoxContainer body)
    {
        var grid = Grid(2);
        grid.AddChild(Btn("打印状态", PrintStatus));
        grid.AddChild(Btn("闪一下搁浅按键", Flash));

        Text("滞留卡打出后进搁浅，过 X 场战斗归队（X 每张自己记，打在当场不计一场）。");
        Text("归队时机固定为「双卡组 轮换 → 递减 → 归队」，同场上限 4。");
    }

    protected override string Status()
    {
        var player = Player();
        return player is null ? "不在战斗" : StasisStore.Describe(player);
    }

    private void PrintStatus()
    {
        var text = Player() is { } player ? StasisStore.Describe(player) : "不在战斗";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 面板状态：{text}");
        Show(text);
    }

    private void Flash()
    {
        AeonglassStasisPileFlash.Flash(1);
        Show("搁浅按键闪一次");
    }
}

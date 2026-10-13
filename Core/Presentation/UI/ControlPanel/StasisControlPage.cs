using Godot;

using STS2_Aeonglass.Core.Systems.Zones;

namespace STS2_Aeonglass.Core.Presentation.UI.ControlPanel;

/// <summary>M7「搁浅（原滞留）」页：搁浅读数 + 两个只读 / 验证按钮。</summary>
/// <remarks>
/// M7 的入口是<b>卡牌本身</b>（滞留卡打出后自己进搁浅），机制上没有玩家主动动作，
/// 所以这一页只有读数与验证按钮（"闪一下顶栏搁浅按键"走的正是归队提示用的那条特效）。
/// </remarks>
internal sealed partial class StasisControlPage : ControlPage
{
    internal override string Title => "搁浅";

    internal override bool IsAvailable => StasisZone.IsReady;

    protected override void BuildBody(VBoxContainer body)
    {
        var grid = Grid(2);
        grid.AddChild(Btn("打印状态", PrintStatus));
        grid.AddChild(Btn("闪一下搁浅按键", Flash));

        Text("滞留卡打出后进搁浅，过 X 场战斗归队（X 每张自己记，打在当场不计一场）。");
        Text("归队时机固定为「M6 轮换 → 递减 → 归队」，同场上限 4。");
    }

    protected override string Status()
    {
        var player = Player();
        return player is null ? "不在战斗" : StasisZone.Describe(player);
    }

    private void PrintStatus()
    {
        var text = Player() is { } player ? StasisZone.Describe(player) : "不在战斗";
        Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 面板状态：{text}");
        Show(text);
    }

    private void Flash()
    {
        StasisPileFlash.Flash(1);
        Show("搁浅按键闪一次");
    }
}

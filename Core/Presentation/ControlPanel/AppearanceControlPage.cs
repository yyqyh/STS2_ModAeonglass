using System.Globalization;

using Godot;

using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Framework.Diagnostics;
using STS2_Aeonglass.Core.Presentation.Animations;
using STS2_Aeonglass.Core.Presentation.Character;

namespace STS2_Aeonglass.Core.Presentation.ControlPanel;
/// <summary>外观页：战斗外观 / 位置 / 朝向 / 特效的实时调参。</summary>
/// <remarks>
/// ① 职责：读改 <c>AeonglassCreatureVisuals</c> 与 <c>AeonglassVfx</c> 的显示参数（含朝向翻转）。
/// ② 调用时机：页签打开与按钮点击。
/// ③ 依赖：战斗房间的 creature 节点 + 设置项（正面视图等）。
/// ④ ⚠️ 改动<b>只影响本场显示</b>，不写资源文件；调好的值再回填到设置/常量里。
/// </remarks>

internal sealed partial class AppearanceControlPage : ControlPage
{
    private Label _anims = null!;
    private Label _anchors = null!;
    private Label _vfx = null!;

    internal override string Title => "外观";

    internal override bool IsAvailable => true;

    protected override void BuildBody(VBoxContainer body)
    {
        // 三条只读信息行：动画名 / 锚点 / 特效状态（内容在 Status() 里更新）。
        _anims = Info(10);
        _anchors = Info(10);
        _vfx = Info(10);

        Section("触发动画（走真正的状态机 trigger）");
        var anims = Grid(3);
        anims.AddChild(Btn("Idle", () => Trigger(CreatureAnimator.idleTrigger, 0f)));
        anims.AddChild(Btn("Attack", () => Trigger(CreatureAnimator.attackTrigger, 0.25f)));
        anims.AddChild(Btn("AttackHeavy", () => Trigger(AeonglassAnimNames.AttackHeavyTrigger, 0.25f)));
        anims.AddChild(Btn("AttackDouble", () => Trigger(AeonglassAnimNames.AttackDoubleTrigger, 0.4f)));
        anims.AddChild(Btn("Cast", () => Trigger(CreatureAnimator.castTrigger, 0.3f)));
        anims.AddChild(Btn("PowerUp", () => Trigger(CreatureAnimator.powerUpTrigger, 0.3f)));
        anims.AddChild(Btn("Hit", () => Trigger(CreatureAnimator.hitTrigger, 0f)));
        anims.AddChild(Btn("Dead", () => Trigger(CreatureAnimator.deathTrigger, 0f)));
        anims.AddChild(Btn("Relaxed", () => Trigger("Relaxed", 0f)));

        Section("轨道 1（外环旋转）");
        var rings = Grid(2);
        rings.AddChild(Btn("重播 rings_normal", () =>
        {
            if (FindVisuals() is not { } visuals)
            {
                Show("找不到外观", true);
                return;
            }

            visuals.PlayTrack1Rings();
            Show("轨道 1 已重新播放");
        }));
        rings.AddChild(Btn("动画快照", () =>
        {
            var visuals = FindVisuals();
            Show(visuals is null
                ? "找不到外观"
                : $"轨道0={visuals.CurrentTrack0Animation ?? "-"} / 轨道1={visuals.CurrentTrack1Animation ?? "-"}");
        }));

        Section("外观（调准后抄进 tscn）");
        var look = Grid(3);
        look.AddChild(Btn("缩放 -0.01", () => NudgeScale(-0.01f)));
        look.AddChild(Btn("缩放 +0.01", () => NudgeScale(0.01f)));
        look.AddChild(Btn("镜像 ±", () =>
        {
            AeonglassSettingStore.ToggleScaleXPositive();
            Show($"镜像 = {!AeonglassSettingStore.Current.ScaleXPositive}（已存盘）");
        }));
        look.AddChild(Btn("当前值", () => Show(
            $"Visuals.scale = {CurrentScaleText()}；镜像 = {!AeonglassSettingStore.Current.ScaleXPositive}")));

        Section("占位资源（仍是铁甲战士的照片）");
        Text("· " + string.Join("\n· ", PlaceholderAssetRegistry.FromVanillaCharacter));
        var more = Grid(1);
        more.AddChild(Btn("把完整清单打进日志", () =>
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} {PlaceholderAssetRegistry.Summary}");
            Show("已写入 godot.log");
        }));
    }

    protected override string Status()
    {
        var visuals = FindVisuals();
        if (visuals is null)
        {
            _anims.Text = "-";
            _anchors.Text = "-";
            _vfx.Text = "-";
            return "玩家位不是永世沙漏外观（不在战斗 / 外观未生效）";
        }

        ApplyAppearance();

        var spine = visuals.GetNodeOrNull<Node2D>("%Visuals");
        _anims.Text = string.Join(
            " ｜ ",
            $"轨道0={visuals.CurrentTrack0Animation ?? "-"}",
            $"轨道1={visuals.CurrentTrack1Animation ?? "-"}");
        _anchors.Text = string.Join(
            " ｜ ",
            $"Center={Fmt(visuals.VfxSpawnPosition.Position)}",
            $"Intent={Fmt(visuals.IntentPosition.Position)}",
            $"Bounds={Fmt(visuals.Bounds.Position)} {Fmt(visuals.Bounds.Size)}");

        var vfx = visuals.GetNodeOrNull<AeonglassVfx>("Visuals/AeonglassVfx");
        _vfx.Text = vfx is null ? "特效节点缺失（Visuals/AeonglassVfx）" : $"特效：{vfx.StatusLine}";

        return string.Join(
            " ｜ ",
            $"骨架={(visuals.HasSpineAnimation ? "ok" : "缺失")}",
            $"SpineBody={(visuals.SpineBody is null ? "null" : "ok")}",
            $"scale={(spine is null ? "?" : $"{spine.Scale.X:0.###},{spine.Scale.Y:0.###}")}");
    }

    private void Trigger(string trigger, float waitTime)
    {
        if (FindCreatureNode()?.Entity is not { } creature)
        {
            Show("找不到玩家生物节点", true);
            return;
        }

        TaskHelper.RunSafely(CreatureCmd.TriggerAnim(creature, trigger, waitTime));
        Show($"trigger = {trigger}");
    }

    private void NudgeScale(float delta)
    {
        var next = Mathf.Max(0.02f, ParseScale(AeonglassSettingStore.Current.Scale) + delta);
        var text = next.ToString("0.###", CultureInfo.InvariantCulture);
        AeonglassSettingStore.SetScale(text);
        Show($"缩放 = {text}（已存盘）");
    }

    /// <summary>把设置里的朝向 / 缩放应用到当前外观（每次刷新都应用）。</summary>
    private void ApplyAppearance()
    {
        if (FindVisuals() is not { } visuals
            || visuals.GetNodeOrNull<Node2D>("%Visuals") is not { } spine)
        {
            return;
        }

        var setting = AeonglassSettingStore.Current;
        var value = ParseScale(setting.Scale);
        spine.Scale = new Vector2(setting.ScaleXPositive ? value : -value, value);
    }

    private string CurrentScaleText()
        => FindVisuals()?.GetNodeOrNull<Node2D>("%Visuals") is { } spine
            ? $"{spine.Scale.X:0.###},{spine.Scale.Y:0.###}"
            : "?";

    private static float ParseScale(string? text)
        => float.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
           && parsed > 0f
            ? parsed
            : 0.12f;

    private static NCreature? FindCreatureNode()
    {
        if (NCombatRoom.Instance is not { } room)
        {
            return null;
        }

        foreach (var node in room.CreatureNodes)
        {
            if (node?.Visuals is AeonglassCreatureVisuals)
            {
                return node;
            }
        }

        return null;
    }

    private static AeonglassCreatureVisuals? FindVisuals()
        => FindCreatureNode()?.Visuals as AeonglassCreatureVisuals;

    private static string Fmt(Vector2 v) => $"({v.X:0.#},{v.Y:0.#})";
}

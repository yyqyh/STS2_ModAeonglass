using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Presentation.SecondaryResource;


namespace STS2_Aeonglass.Core.Systems.Sand;

/// <summary>
/// 沙砾<b>计数器与卡费的界面接线</b>：注册计数器/卡费样式，并把"点计数器 = 倒置"接上。
/// </summary>
/// <remarks>
/// ① 职责：<c>RegisterSandCounterUi</c>/<c>RegisterSandCardCostUi</c> + <c>WireInvertClick(Tree)</c>。
/// ② 调用时机：<c>SandResourceRegistry.Register</c> 内（次级资源框架注册时）。
/// ③ 依赖：RitsuLib 次级资源 UI 注册口 + 表现层的 <c>AeonglassSandCounterUi</c>。
/// ④ ⚠️ 点计数器会直接执行一次倒置（等同玩家主动操作），接线时别重复挂。
/// </remarks>
internal static class SandCounterUiWiring
{

    /// <summary>
    /// 战斗计数器：<b>固定只绑沙砾</b>。
    /// </summary>
    /// <remarks>
    /// 第一版按 <c>ctx.VisibleDefinitions</c> 绑定，结果把沙砾与空隙<b>两个</b>都画进了同一行
    /// （用户实测："分叉出两个文本"）。按 2026-10-11 的决定**只做一个计数器**，
    /// 所以这里显式只给沙砾那一个定义 —— 空隙只在悬浮说明与（后续的）卡面费用里出现。
    /// </remarks>
    internal static void RegisterSandCounterUi(ModSecondaryResourceRegistry registry)
    {
        registry.RegisterCombatUi(
            "sand_counter",
            factory: _ =>
            {
                var row = PreloadManager.Cache
                    .GetScene(Const.Paths.Ui.SandCounterScene)
                    .Instantiate<NSecondaryResourceCounterRow>(PackedScene.GenEditState.Disabled);

                // Step 1：**沙砾计数器本体可点 = 倒置**（唯一入口仍是 SandInvertCmd.TryInvert）。
                // ⚠️ 点击**不能挂在这一行上**：框架的 `NSecondaryResourceCounterRow._Ready()` 会无条件把自己设成
                //    `MouseFilter.Ignore`（RitsuLib `SecondaryResourceCounterNodes.cs:1557`），我们在这里设的值会被覆盖；
                //    真正能命中的是它内层那个 `NSecondaryResourceCounter`（框架把它设成 `Stop`）
                //    ⇒ 点击一律挂内层，见 `WireInvertClick`（在 update 里做，那时才建好子树）。

                return row;
            },
            update: ctx =>
            {
                SandResourceRegistry.CounterPlayer = ctx.Player;
                ctx.Node.Bind(
                    ctx.Player,
                    [SandResourceRegistry.Sand],
                    autoRefresh: true);

                // 5 层沙漏艺术按沙砾值换档：上腔 = 沙砾、下腔 = 6 − 沙砾（同一个 level）。
                // 框架那张空槽贴图已经被 AeonglassSandCounterUi 藏掉，位置/尺寸/悬浮仍由框架管。
                if (ctx.Node is AeonglassSandCounterUi sandUi && ctx.Player is { } sandPlayer)
                {
                    sandUi.PushLevel(SecondaryResourceCmd.Get(sandPlayer, SandResourceRegistry.SandId));
                }

                // ⚠️ 要**递归**：计数器在内层的 HBoxContainer 里（框架 `_Ready` 建 `_row`，再把计数器塞进它），
                //    只扫直接子节点只会扫到那个 HBox，真正的命中面（计数器）挂不上 ⇒ 点了没反应。
                WireInvertClickTree(ctx.Node);
            },
            options: new NodeAttachmentOptions
            {
                Name = "AeonglassSandCounter",
                // 挂进能量计数器容器（与本体能量球同一片区域）。
                AttachParentSelector = parent => (parent as NCombatUi)?.EnergyCounterContainer,
            });
    }

    /// <summary>给"沙砾计数器"的可命中节点挂上"点击 = 倒置"（幂等：用 Godot 的 meta 打标）。</summary>
    /// <remarks>
    /// <para>
    /// <b>为什么挂内层而不是外层</b>：框架 <c>NSecondaryResourceCounterRow._Ready()</c> 把 Row 自己设成
    /// <c>MouseFilter.Ignore</c>、内部容器也是 <c>Ignore</c>；能收到鼠标的是它内层那个
    /// <c>NSecondaryResourceCounter</c>（框架把它设成 <c>Stop</c>）。2026-10-13 实测"点计数器没反应"就是这个原因。
    /// </para>
    /// <para>放在 <c>update</c> 里调：那时子树才建好；meta 打标保证只挂一次。</para>
    /// </remarks>
    internal static void WireInvertClick(Control control)
    {
        if (control.HasMeta(SandResourceRegistry.InvertClickMetaKey))
        {
            return;
        }

        control.SetMeta(SandResourceRegistry.InvertClickMetaKey, true);
        control.MouseFilter = Control.MouseFilterEnum.Stop;

        // 悬停说明走**官方**那套 hover tip：框架已经给计数器绑了一个提示集合（沙砾的 title/description 就在
        // 我们自己的 static_hover_tips.json 里），`AddTipToOwner` 是**往同一个集合里追加一条** ——
        // 于是"点击 = 倒置"这一行和官方说明长在同一个框里。
        // ⚠️ 2026-10-13：这里**不要**用 `TooltipText`（Godot 原生 tooltip）—— 那是另一个黑框，用户实测要的是官方样式。
        // 追加失败（该节点没有官方提示集合）时才退回原生 tooltip，保证"至少看得到"。
        if (!HoverTipHelper.AddTipToOwner(control, SandResourceRegistry.InvertTipTitle, SandResourceRegistry.InvertTipText))
        {
            control.TooltipText = $"{SandResourceRegistry.InvertTipTitle}：{SandResourceRegistry.InvertTipText}";
        }

        control.GuiInput += evt =>
        {
            if (evt is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                && SandResourceRegistry.CounterPlayer is { } player)
            {
                TaskHelper.RunSafely(SandInvertCmd.TryInvert(player));
            }
        };
    }

    /// <summary>
    /// 递归遍历（计数器在内层容器里），但**不给外层的 <c>NSecondaryResourceCounterRow</c> 挂**
    /// —— 那一层是整行的背景，挂上会把周围一片都变成"可点"。
    /// </summary>
    internal static void WireInvertClickTree(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is NSecondaryResourceCounterRow)
            {
                WireInvertClickTree(child);
                continue;
            }

            if (child is Control control)
            {
                WireInvertClick(control);
            }

            WireInvertClickTree(child);
        }
    }

    internal static void RegisterSandCardCostUi(ModSecondaryResourceRegistry registry)
    {
        registry.RegisterCardUi(
            "sand_card_cost",
            factory: _ => PreloadManager.Cache
                .GetScene(Const.Paths.Ui.SandCardCostScene)
                .Instantiate<NSecondaryResourceCardCostUi>(PackedScene.GenEditState.Disabled),
            update: ctx => ctx.Node.Refresh(ctx),
            options: new NodeAttachmentOptions
            {
                Name = "AeonglassSandCardCost",
            });
    }
}

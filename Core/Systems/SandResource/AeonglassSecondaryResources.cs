using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Presentation.UI;

namespace STS2_Aeonglass.Core.Systems.SandResource;

/// <summary>
/// M1「时之沙」的次级资源定义与 UI 注册入口。
/// </summary>
/// <remarks>
/// <para>
/// 用 RitsuLib 的次级资源系统实现两个资源：<b>沙砾</b>（可花的时间）与<b>空隙</b>（已流走的时间），
/// 恒定 <c>沙砾 + 空隙 = 容量</c>。
/// </para>
/// <para>
/// 完整资源 ID 由框架生成：<c>STS2_AEONGLASS_SECONDARY_RESOURCE_SAND</c> / <c>..._VOID</c>；
/// 悬浮提示默认读 <c>static_hover_tips</c> 表里的 <c>{完整ID}.title</c> / <c>.description</c>。
/// </para>
/// <para>
/// <b>参数（Step 1 起已改口径）</b>：容量 6（硬顶）、初始 3/3、<c>turnStartPolicy = None</c>、
/// <c>persistencePolicy = <b>Run</b></c>（<b>跨战斗保留</b>，不再每场重置 —— 见 Step 1 的"地基"改动）。
/// </para>
/// <para>
/// <b>不变式</b>：<c>沙砾 + 空隙 = 容量</c>。它由三件事共同保证 ——
/// ① 只有<see cref="AeonglassSandSubscription" /> 这一条"支付 → 生成空隙"的写入口；
/// ② <see cref="SandInvertCmd" /> 只交换两者（总量不变）；
/// ③ 控制面板「沙砾」页（原 <c>SandKeypad</c>）的验证按钮也只做"支付 / 重置"这两种不破坏总量的操作。
/// <b>不要</b>再加"凭空 +1 沙砾"这类入口。
/// </para>
/// </remarks>
internal static class AeonglassSecondaryResources
{
    /// <summary>模组内 ID（完整 ID = <c>STS2_AEONGLASS_SECONDARY_RESOURCE_&lt;本地ID&gt;</c>）。</summary>
    public const string SandLocalId = "sand";

    /// <inheritdoc cref="SandLocalId" />
    public const string VoidLocalId = "void";

    /// <summary>容量（硬顶）—— 沙砾与空隙共用，且两者之和恒等于它。</summary>
    public const int Capacity = 6;

    /// <summary>战斗开始时的初始沙砾。</summary>
    public const int InitialSand = 3;

    /// <summary>战斗开始时的初始空隙。</summary>
    public const int InitialVoid = 3;

    /// <summary>注册后的沙砾定义（含完整 ID）。</summary>
    public static SecondaryResourceDefinition Sand { get; private set; } = null!;

    /// <summary>注册后的空隙定义（含完整 ID）。</summary>
    public static SecondaryResourceDefinition Void { get; private set; } = null!;

    /// <summary>沙砾的完整资源 ID（卡牌费用 / 命令都用它）。</summary>
    public static string SandId => Sand.Id;

    /// <summary>空隙的完整资源 ID。</summary>
    public static string VoidId => Void.Id;

    /// <summary>计数器样式（照堕化拉撒路的 <c>ChargeCounterStyle</c>，配色换成沙漏的青色系）。</summary>
    /// <remarks>
    /// 尺寸口径（用户 2026-10-13 定）：先按<b>本体能量计数器(128×128) 的 50% = 64×64</b> 做，
    /// 实机看过后要求再 <b>+50% ⇒ 96×96</b>，用来放那套 5 层沙漏艺术（见 <c>Const.Paths.Ui.SandCounter</c>）。
    /// </remarks>
    public static readonly SecondaryResourceCounterStyle SandCounterStyle = new()
    {
        CounterSize = new Vector2(96f, 96f),
        IconSize = new Vector2(96f, 96f),
        RowSeparation = 6,

        FontSize = 28,
        OutlineSize = 7,
        PositiveColor = new Color("EFFBF8"),
        ZeroColor = new Color("FF6B5A"),
        OutlineColor = new Color("0E3A38"),

        AnimateAmountGain = true,
        AmountGainSmoothTime = 0.1f,
    };

    /// <summary>卡面费用样式。</summary>
    public static readonly SecondaryResourceCardCostUiStyle SandCardCostStyle = new()
    {
        SlotSize = new Vector2(48f, 48f),
        IconSize = new Vector2(46f, 46f),

        FontSize = 28,
        OutlineSize = 7,

        AffordableColor = new Color("EFFBF8"),
        UnaffordableColor = new Color("FF6B5A"),
    };

    /// <summary>在 <c>Main.Initialize()</c> 里调用一次。</summary>
    public static void Register()
    {
        var registry = RitsuLibFramework.GetSecondaryResourceRegistry(Const.ModId);

        Sand = registry.Register(SandLocalId, new SecondaryResourceDefinition(
            defaultAmount: InitialSand,
            baseMaxAmount: Capacity,
            // M1 没有"每回合回复"：沙砾只由卡牌效果 / 倒置产生。
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            // Step 1 起：run 级持久化 —— 沙砾 / 空隙**跨战斗保留**（本体在战斗开始时会把它注入本场），
            // 于是 M2 换回 / M4 回溯的代价才是真的"跨战斗成本"，而不是每场白拿 3 点。
            persistencePolicy: SecondaryResourcePersistencePolicy.Run,
            smallIconPath: Const.Paths.SecondaryResource.SandSmall,
            largeIconPath: Const.Paths.SecondaryResource.SandLarge)
        {
            // 硬顶：单个资源都不能越过 6。
            ClampToMaxAmount = true,
        });

        Void = registry.Register(VoidLocalId, new SecondaryResourceDefinition(
            defaultAmount: InitialVoid,
            baseMaxAmount: Capacity,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Run,
            smallIconPath: Const.Paths.SecondaryResource.VoidSmall,
            largeIconPath: Const.Paths.SecondaryResource.VoidLarge)
        {
            ClampToMaxAmount = true,
        });

        // 只对本角色常显（否则这两个资源会出现在所有人的战斗界面上）。
        registry.AlwaysShowInCombatUiForCharacter<Aeonglass>(SandLocalId);

        RegisterSandCounterUi(registry);
        RegisterSandCardCostUi(registry);

        Const.Logger.Info(
            $"[{Const.ModId}] [M1] 次级资源注册完成：{SandId} / {VoidId}"
            + $"（容量 {Capacity}，初始 {InitialSand}/{InitialVoid}）");
    }

    /// <summary>
    /// 战斗计数器：<b>固定只绑沙砾</b>。
    /// </summary>
    /// <remarks>
    /// 第一版按 <c>ctx.VisibleDefinitions</c> 绑定，结果把沙砾与空隙<b>两个</b>都画进了同一行
    /// （用户实测："分叉出两个文本"）。按 2026-10-11 的决定**只做一个计数器**，
    /// 所以这里显式只给沙砾那一个定义 —— 空隙只在悬浮说明与（后续的）卡面费用里出现。
    /// </remarks>
    private static void RegisterSandCounterUi(ModSecondaryResourceRegistry registry)
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
                _counterPlayer = ctx.Player;
                ctx.Node.Bind(
                    ctx.Player,
                    [AeonglassSecondaryResources.Sand],
                    autoRefresh: true);

                // 5 层沙漏艺术按沙砾值换档：上腔 = 沙砾、下腔 = 6 − 沙砾（同一个 level）。
                // 框架那张空槽贴图已经被 SandCounterUi 藏掉，位置/尺寸/悬浮仍由框架管。
                if (ctx.Node is SandCounterUi sandUi && ctx.Player is { } sandPlayer)
                {
                    sandUi.PushLevel(SecondaryResourceCmd.Get(sandPlayer, AeonglassSecondaryResources.SandId));
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

    /// <summary>计数器当前绑定的玩家（点击倒置时要用它）。</summary>
    private static Player? _counterPlayer;

    /// <summary>给"沙砾计数器"的可命中节点挂上"点击 = 倒置"（幂等：用 Godot 的 meta 打标）。</summary>
    /// <remarks>
    /// <para>
    /// <b>为什么挂内层而不是外层</b>：框架 <c>NSecondaryResourceCounterRow._Ready()</c> 把 Row 自己设成
    /// <c>MouseFilter.Ignore</c>、内部容器也是 <c>Ignore</c>；能收到鼠标的是它内层那个
    /// <c>NSecondaryResourceCounter</c>（框架把它设成 <c>Stop</c>）。2026-10-13 实测"点计数器没反应"就是这个原因。
    /// </para>
    /// <para>放在 <c>update</c> 里调：那时子树才建好；meta 打标保证只挂一次。</para>
    /// </remarks>
    private static void WireInvertClick(Control control)
    {
        if (control.HasMeta(InvertClickMetaKey))
        {
            return;
        }

        control.SetMeta(InvertClickMetaKey, true);
        control.MouseFilter = Control.MouseFilterEnum.Stop;

        // 悬停说明走**官方**那套 hover tip：框架已经给计数器绑了一个提示集合（沙砾的 title/description 就在
        // 我们自己的 static_hover_tips.json 里），`AddTipToOwner` 是**往同一个集合里追加一条** ——
        // 于是"点击 = 倒置"这一行和官方说明长在同一个框里。
        // ⚠️ 2026-10-13：这里**不要**用 `TooltipText`（Godot 原生 tooltip）—— 那是另一个黑框，用户实测要的是官方样式。
        // 追加失败（该节点没有官方提示集合）时才退回原生 tooltip，保证"至少看得到"。
        if (!HoverTipHelper.AddTipToOwner(control, InvertTipTitle, InvertTipText))
        {
            control.TooltipText = $"{InvertTipTitle}：{InvertTipText}";
        }

        control.GuiInput += evt =>
        {
            if (evt is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }
                && _counterPlayer is { } player)
            {
                TaskHelper.RunSafely(SandInvertCmd.TryInvert(player));
            }
        };
    }

    /// <summary>「点击 = 倒置」这条说明的标题 / 正文（追加到官方 hover tip 集合里）。</summary>
    private const string InvertTipTitle = "倒置";

    /// <summary>同上的正文。</summary>
    private static readonly string InvertTipText =
        $"点击：沙砾与空隙对调（{SandInvertCmd.EnergyCost} 费）";

    /// <summary>
    /// 递归遍历（计数器在内层容器里），但**不给外层的 <c>NSecondaryResourceCounterRow</c> 挂**
    /// —— 那一层是整行的背景，挂上会把周围一片都变成"可点"。
    /// </summary>
    private static void WireInvertClickTree(Node node)
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

    /// <summary>"这一格已经挂过点击"的标记（幂等用）。</summary>
    private const string InvertClickMetaKey = "aeonglass_sand_invert_click";

    private static void RegisterSandCardCostUi(ModSecondaryResourceRegistry registry)
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

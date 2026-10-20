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

/// <summary>时之沙的次级资源定义与 UI 注册入口（沙砾 / 空隙两个资源）。</summary>
/// <remarks>
/// ① 职责：<c>Register</c> 定义两个资源与 <c>SandId</c> / <c>VoidId</c> 常量；UI 接线在 <c>SandCounterUiWiring</c>。
/// ② 挂点与时机：由 <c>SandResourceModule.Register</c> 在机制注册时调。
/// ③ 依赖：RitsuLib <c>SecondaryResources</c>；悬浮提示读 <c>static_hover_tips</c> 的 <c>{完整ID}.title</c> / <c>.description</c>。
/// ④ ⚠️ 参数口径（Step 1 起）：容量 6（硬顶）、初始 3/3、<c>turnStartPolicy = None</c>、<c>persistencePolicy = Run</c>（跨战斗保留，不再每场重置）；
/// 资源 ID 由框架生成 <c>STS2_AEONGLASS_SECONDARY_RESOURCE_SAND</c> / <c>..._VOID</c>。
/// 不变式 <c>沙砾 + 空隙 = 容量</c> 由三件事共同保证：只有 <c>SandGapInvariant</c> 这一条"支付 → 生成空隙"的写入口、
/// <c>SandInvertCmd</c> 只交换两者（总量不变）、控制面板「沙砾」页只做"支付 / 重置" ⇒ <b>不要</b>再加"凭空 +1 沙砾"这类入口。
/// </remarks>
internal static class SandResourceRegistry
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
        // 2026-10-20：与本体 StarIcon 同尺寸/同位置（0.5 锚点，58×58）。
        SlotSize = new Vector2(58f, 58f),
        IconSize = new Vector2(58f, 58f),
        ReserveVanillaStarCostSlot = true,

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
            // 时之沙 没有"每回合回复"：沙砾只由卡牌效果 / 倒置产生。
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            // Step 1 起：run 级持久化 —— 沙砾 / 空隙**跨战斗保留**（本体在战斗开始时会把它注入本场），
            // 于是 时空裂隙 换回 / 回溯的代价才是真的"跨战斗成本"，而不是每场白拿 3 点。
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

        SandCounterUiWiring.RegisterSandCounterUi(registry);
        SandCounterUiWiring.RegisterSandCardCostUi(registry);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Sand} 次级资源注册完成：{SandId} / {VoidId}"
            + $"（容量 {Capacity}，初始 {InitialSand}/{InitialVoid}）");
    }

    /// <summary>计数器当前绑定的玩家（点击倒置时要用它）。</summary>
    internal static Player? CounterPlayer;

    /// <summary>「点击 = 倒置」这条说明的标题 / 正文（追加到官方 hover tip 集合里）。</summary>
    internal const string InvertTipTitle = "倒置";

    /// <summary>同上的正文。</summary>
    internal static readonly string InvertTipText =
        $"点击：沙砾与空隙对调（{SandInvertCmd.EnergyCost} 费）";

    /// <summary>"这一格已经挂过点击"的标记（幂等用）。</summary>
    internal const string InvertClickMetaKey = "aeonglass_sand_invert_click";

}

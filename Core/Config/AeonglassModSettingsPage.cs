using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

using STS2_Aeonglass.Core.Framework.Diagnostics;

namespace STS2_Aeonglass.Core.Config;

/// <summary>
/// 永世沙漏的设置页。
/// </summary>
/// <remarks>
/// <para>
/// 第 ① 步只有两组：<b>调试</b>（总开关 + 面板）与<b>外观调参</b>（朝向 / 缩放）。
/// 骨架照 <c>together/Core/Settings/TogetherModSettingsPage.cs</c>（完整范例），
/// 数据存储照 <c>STS2_WhiteAlbum2/Core/Framework/Settings</c>（骨架范例）。
/// </para>
/// <para>
/// <b>注意</b>：together 给设置小节加了 <c>WithReadOnlyOnHostSurfaces(...)</c>（「局内改了不生效」的项
/// 在局内只读）。我们的调试项<b>恰恰是局内必须能改</b> —— 所以这里<b>不加</b>那个标记。
/// </para>
/// <para>
/// 文案一律用 <see cref="ModSettingsText.Literal" />，不依赖本地化表（调试项不该进正式文案）。
/// </para>
/// </remarks>
internal static class AeonglassModSettingsPage
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        // 2026-10-13：原来的 7 个面板开关（调试总开关 + 外观面板 + M1/M2/M3/M4/M6 各一个按键面板）
        // 全部收成这一个 —— 战斗内只有一个控制面板，按机制分页。
        var controlPanel = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings is null || settings.ControlPanel,
            (settings, value) => settings.ControlPanel = value);

        var debugBorrowSilentPool = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings is null || settings.DebugBorrowSilentCardPool,
            (settings, value) => settings.DebugBorrowSilentCardPool = value);

        var debugUnlimited = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings is null || settings.DebugUnlimitedPerCombat,
            (settings, value) => settings.DebugUnlimitedPerCombat = value);

        var scaleXPositive = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings is not null && settings.ScaleXPositive,
            (settings, value) => settings.ScaleXPositive = value);

        var scale = new ModSettingsValueBinding<AeonglassSetting, string>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings?.Scale ?? "0.12",
            (settings, value) => settings.Scale = value ?? "0.12");

        var m1Enabled = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings is null || settings.SandEnabled,
            (settings, value) => settings.SandEnabled = value);

        var m7Enabled = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings is null || settings.StasisEnabled,
            (settings, value) => settings.StasisEnabled = value);

        var m7Combats = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings?.StasisCombats ?? 2,
            (settings, value) => settings.StasisCombats = value);

        var m7MaxStasis = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings?.StasisMaxStasis ?? 4,
            (settings, value) => settings.StasisMaxStasis = value);

        var m7ReturnBeforeSwitch = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings is not null && settings.StasisReturnBeforeSwitch,
            (settings, value) => settings.StasisReturnBeforeSwitch = value);

        var m7Flash = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId,
            AeonglassSettingStore.DataKey,
            SaveScope.Global,
            settings => settings is null || settings.StasisFlash,
            (settings, value) => settings.StasisFlash = value);

        var m2Enabled = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.GapEnabled,
            (settings, value) => settings.GapEnabled = value);

        // 「乱流」抽弃互换（单列机制，默认关：实验件）
        var riotEnabled = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is not null && settings.RiotEnabled,
            (settings, value) => settings.RiotEnabled = value);

        var riotForceSwap = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is not null && settings.RiotForceSwap,
            (settings, value) => settings.RiotForceSwap = value);

        var m2Capacity = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.GapCapacity ?? 5,
            (settings, value) => settings.GapCapacity = value);

        var m2ActionButtons = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.GapActionButtons,
            (settings, value) => settings.GapActionButtons = value);

        var m2OneShot = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.GapOneShot,
            (settings, value) => settings.GapOneShot = value);

        var m2Ethereal = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.GapEthereal ?? true,
            (settings, value) => settings.GapEthereal = value);

        var m2CollapseCost = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.GapActionCost ?? 2,
            (settings, value) => settings.GapActionCost = value);

        var m2RefillDraw = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is not null && settings.GapRefillDraw,
            (settings, value) => settings.GapRefillDraw = value);

        var m6HotSand = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.DecksHotSwitchSandCost ?? 2,
            (settings, value) => settings.DecksHotSwitchSandCost = value);

        var m4Enabled = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.RewindEnabled,
            (settings, value) => settings.RewindEnabled = value);

        var m4CostAllSand = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.RewindCostAllSand,
            (settings, value) => settings.RewindCostAllSand = value);

        var m6Enabled = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.DecksEnabled,
            (settings, value) => settings.DecksEnabled = value);

        var m6AutoSwitch = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.DecksAutoSwitch,
            (settings, value) => settings.DecksAutoSwitch = value);

        var m6ManualSwitch = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.DecksManualSwitch,
            (settings, value) => settings.DecksManualSwitch = value);

        var m6HotSwitch = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.DecksHotSwitch,
            (settings, value) => settings.DecksHotSwitch = value);

        var m6CostSource = new ModSettingsValueBinding<AeonglassSetting, string>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.DecksCostSource ?? "none",
            (settings, value) => settings.DecksCostSource = value ?? "none");

        var m6CostAmount = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.DecksCostAmount ?? 0,
            (settings, value) => settings.DecksCostAmount = value);

        var m6SwitchesPerCombat = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.DecksSwitchesPerCombat ?? 1,
            (settings, value) => settings.DecksSwitchesPerCombat = value);

        var m6Cooldown = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.DecksManualCooldownCombats ?? 2,
            (settings, value) => settings.DecksManualCooldownCombats = value);

        var m3Enabled = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.IntentEnabled,
            (settings, value) => settings.IntentEnabled = value);

        var m3ManagedRollMove = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.IntentManagedRollMove,
            (settings, value) => settings.IntentManagedRollMove = value);

        var m3VoidMode = new ModSettingsValueBinding<AeonglassSetting, string>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.IntentVoidMode ?? "once",
            (settings, value) => settings.IntentVoidMode = value ?? "once");

        var m3HandEnd = new ModSettingsValueBinding<AeonglassSetting, string>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.IntentHandEndOfTurn ?? "discard",
            (settings, value) => settings.IntentHandEndOfTurn = value ?? "discard");

        var m3IntentBelt = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is null || settings.IntentIntentBelt,
            (settings, value) => settings.IntentIntentBelt = value);

        var m5VoidDamage = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.WitherVoidDamage ?? 3,
            (settings, value) => settings.WitherVoidDamage = value);

        var m5EatsIntent = new ModSettingsValueBinding<AeonglassSetting, bool>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings is not null && settings.WitherEatsIntent,
            (settings, value) => settings.WitherEatsIntent = value);

        var m5InjectPerUse = new ModSettingsValueBinding<AeonglassSetting, int>(
            Const.ModId, AeonglassSettingStore.DataKey, SaveScope.Global,
            settings => settings?.WitherInjectPerUse ?? 1,
            (settings, value) => settings.WitherInjectPerUse = value);

        RitsuLibFramework.RegisterModSettings(Const.ModId, page => page
            .WithTitle(ModSettingsText.Literal($"{Const.DisplayName} · {Const.ModId}"))
            .WithModDisplayName(ModSettingsText.Literal(Const.ModId))
            .AddSection("debug", section => section
                .WithTitle(ModSettingsText.Literal("控制面板与调试"))
                .AddToggle(
                    "debug_control_panel",
                    ModSettingsText.Literal("战斗内控制面板（唯一）"),
                    controlPanel,
                    ModSettingsText.Literal(
                        "进战斗后左上角显示<b>一个</b>控制面板，按机制分页：沙砾 / 搁浅 / 间隙 / 意图 / 回溯 / 卡组 / 外观。\n"
                        + "页签按各机制的<b>总开关</b>出现（M1 关掉就没有「沙砾」页）；按 F8 收起 / 展开。\n"
                        + "关掉本项 = 一个节点都不建。\n"
                        + "⚠️ 面板按钮是<b>测试入口</b>：正式入口是卡牌 / 遗物 / 按键（见「触发按键方案」），"
                        + "那三条接上之后整块面板可以关掉。"))
                .AddParagraph(
                    "debug_note",
                    ModSettingsText.Literal(
                        "2026-10-13 合并：原来 M1 / M2 / M3 / M4 / M6 各挂一个按键面板 + 一个外观面板（7 个开关），"
                        + "现在只剩这一个。面板只在战斗房间存在期间挂载，随房间销毁。"))
                .AddToggle(
                    "debug_borrow_silent_pool",
                    ModSettingsText.Literal("借用静默猎手卡池（测试用）"),
                    debugBorrowSilentPool,
                    ModSettingsText.Literal(
                        "<b>测试期临时设施</b>：现在是开着的。\n"
                        + "开启：把本体静默猎手的 91 张牌并进永世沙漏的卡池（卡牌奖励 / 商店能发出来），"
                        + "只为「自己的牌太少、凑不出样本」这一个问题。\n"
                        + "关闭：卡池回到只有我们自己的卡。想彻底断开就删掉 "
                        + "Core/Content/Cards/SilentCardPoolBorrow.cs 与 Core/Patches/SilentCardPoolBorrowPatch.cs。\n"
                        + "⚠️ 改动需要重启游戏生效（卡池列表只算一次并缓存）。"))
                .AddToggle(
                    "debug_unlimited_per_combat",
                    ModSettingsText.Literal("【测试】解除所有每场 / 每回合次数限制"),
                    debugUnlimited,
                    ModSettingsText.Literal(
                        "默认<b>开</b>（用户 2026-10-11 要求「测试解放每场额度，所有功能都不带限制」）。\n"
                        + "打开后不再拦截：M3 每场意图操纵上限、M4 每场 1 次回溯、M6 每场切换上限与冷却。\n"
                        + "⚠️ M2 的「每回合 1 次激活」上限**已取消**（2026-10-14 用户口径）⇒ 与本开关无关。\n"
                        + "⚠️ M2 的「必须交替」**不受**本开关影响，永远生效。\n"
                        + "⚠️ <b>代价照旧收</b>，只是不限次数；这是测试开关，正式发布前应关掉。"))
                .AddButton(
                    "debug_dump_placeholder",
                    ModSettingsText.Literal("打印占位资源清单"),
                    ModSettingsText.Literal("写入日志"),
                    () => Const.Logger.Info($"[{Const.ModId}] {PlaceholderAssetRegistry.Summary}"),
                    description: ModSettingsText.Literal("把「当前仍是铁甲战士的资源」与「空槽图」逐条打进 godot.log。")))
            .AddSection("mechanism_riot", section => section
                .WithTitle(ModSettingsText.Literal("机制 · 乱流（抽弃互换）"))
                .AddToggle(
                    "mechanism_riot_enabled",
                    ModSettingsText.Literal("乱流总开关（默认关）"),
                    riotEnabled,
                    ModSettingsText.Literal(
                        "开启：诅咒「乱流」进入弃牌堆时点亮 —— 本场之内**抽牌堆与弃牌堆互换**"
                        + "（抽 / 弃 / 检索 / 洗回 / 使用，全部按类型换到对面那张堆上）。\n"
                        + "口径（用户 2026-10-14）：**持续生效**（本场锁存）、**战斗结束复位**、"
                        + "迟滞在互换期间算作「永久滞留」（这是迟滞玩法之一，刻意不管）。\n"
                        + "关闭：两个 getter 的后置改写直接返回原值，这张诅咒就只是一张不可打出的牌。\n"
                        + "⚠️ 实现是「读取重定向」：牌堆内容一卡都不动 ⇒ 存档 / 读档天然一致。"))
                .AddToggle(
                    "mechanism_riot_force_swap",
                    ModSettingsText.Literal("【调试】强制互换（不等乱流）"),
                    riotForceSwap,
                    ModSettingsText.Literal(
                        "打开后立刻一直互换，用来单独验这条链路，或排查「是没点亮还是改写没生效」。\n"
                        + "⚠️ 会连抽牌堆按钮里的内容一起换（那就是这个机制本身的样子）。")))
            .AddSection("appearance", section => section
                .WithTitle(ModSettingsText.Literal("外观（= 控制面板「外观」页）"))
                .AddToggle(
                    "scale_x_positive",
                    ModSettingsText.Literal("保持骨架原始朝向（不镜像）"),
                    scaleXPositive,
                    ModSettingsText.Literal(
                        "关闭（默认）：把 Visuals.scale.x 取负 —— boss 骨架按面向左绘制，玩家位要面向右。\n"
                        + "开启：保持原始朝向。这一项必须实测确认。"))
                .AddString(
                    "scale",
                    ModSettingsText.Literal("战斗外观缩放"),
                    scale,
                    placeholder: ModSettingsText.Literal("例如 0.12"),
                    maxLength: 8,
                    description: ModSettingsText.Literal(
                        "起点 0.12 ≈ 原版 boss 的 0.2 ×（玩家位宽度 244 / boss 位宽度 428）。\n"
                        + "用控制面板「外观」页的 ± 按钮量准之后，把它抄进 aeonglass_combat.tscn 的 Visuals.scale。"),
                    valueValidationVisual: IsPositiveFloat))
            .AddSection("about", section => section
                .WithTitle(ModSettingsText.Literal("关于"))
                .AddParagraph(
                    "about_note",
                    ModSettingsText.Literal(
                        $"v{Const.Version} — 永世沙漏（STS2_Aeonglass）。\n"
                        + "已接入：人物模型与特效、能量计数器、M1 时之沙、M2 时空间隙、M3 操作时间、"
                        + "M4 回溯、M5 凋萎、M6 双卡组、M7 搁浅（滞留）。\n"
                        + "战斗内的测试入口统一在<b>控制面板</b>（本页第一段）；"
                        + "机制动作的正式按键表见《永世沙漏 · 控制面板与触发按键方案》。")))
            .AddSection("mechanism_m1", section => section
                .WithTitle(ModSettingsText.Literal("机制 · M1 时之沙"))
                .AddToggle(
                    "mechanism_m1_enabled",
                    ModSettingsText.Literal("M1 总开关"),
                    m1Enabled,
                    ModSettingsText.Literal(
                        "开启：注册「沙砾 / 空隙」两个次级资源、战斗计数器、卡面沙砾费用显示，"
                        + "以及「花掉沙砾 → 生成等量空隙」的订阅。\n"
                        + "关闭：以上全部不注册，回到只有卡牌数值的状态。\n"
                        + "⚠️ 改动需要重启游戏生效（资源注册只在初始化时做一次）。"))
                .AddParagraph(
                    "m1_note",
                    ModSettingsText.Literal(
                        "参数（已定稿）：容量 6（硬顶）、初始沙砾 3 / 空隙 3；倒置 = 2 费，<b>已取消每回合次数上限</b>。\n"
                        + "空隙只在「支付沙砾」时产生；倒置交换两者但不改变总量。\n"
                        + "沙砾 / 空隙是 run 级资源、**不参与 M4 回溯**（回档后保持当前值），所以没有「参与回溯」这种开关。\n"
                        + "倒置的入口：战斗内控制面板「沙砾」页，或<b>直接点战斗计数器本体</b>。")))
            .AddSection("mechanism_m7", section => section
                .WithTitle(ModSettingsText.Literal("机制 · M7 滞留X"))
                .AddToggle(
                    "mechanism_m7_enabled",
                    ModSettingsText.Literal("M7 总开关"),
                    m7Enabled,
                    ModSettingsText.Literal(
                        "开启：在右上角「牌组」按钮旁注册「搁浅」牌堆；滞留卡打出后离队、过 X 场战斗归队。\n"
                        + "关闭：不注册搁浅牌堆，滞留卡退化成一张普通技能牌（打出后进弃牌堆）。\n"
                        + "⚠️ 改动需要重启游戏生效（牌堆注册只在初始化时做一次）。"))
                .AddIntSlider(
                    "mechanism_m7_combats",
                    ModSettingsText.Literal("X：滞留几场战斗后归队"),
                    m7Combats,
                    1,
                    5,
                    1,
                    value => $"{value} 场",
                    ModSettingsText.Literal(
                        "打出<b>当场</b>不计一场；归队目标是「归队那一刻的当前卡组」。\n"
                        + "改这个数只影响之后新滞留的牌（已经在外面的牌保留自己卡上的倒计时）。"))
                .AddIntSlider(
                    "mechanism_m7_max_stasis",
                    ModSettingsText.Literal("搁浅同时滞留上限"),
                    m7MaxStasis,
                    1,
                    8,
                    1,
                    value => $"{value} 张",
                    ModSettingsText.Literal(
                        "Step 3 定稿 4。超过上限时新的滞留会被**拒绝**（不丢牌、日志写明原因）。"))
                .AddToggle(
                    "mechanism_m7_return_before_switch",
                    ModSettingsText.Literal("回退：归队回到「轮换之前」（旧口径）"),
                    m7ReturnBeforeSwitch,
                    ModSettingsText.Literal(
                        "**默认关** = 新口径：一局结束的固定顺序是「**M6 轮换 → M7 递减 → M7 归队**」"
                        + "（由 `EndOfCombatSequencer` 显式驱动，不赌钩子注册顺序）。\n"
                        + "打开 = 回到旧行为（`AfterCombatEnd` 里先归队、之后才轮换），仅供对照排查。"))
                .AddToggle(
                    "mechanism_m7_flash",
                    ModSettingsText.Literal("归队时闪一下「搁浅」按钮"),
                    m7Flash,
                    ModSettingsText.Literal(
                        "默认开。**只**在「有牌归队」时闪：顶栏「搁浅」按钮 白→青→白 来回 3 次（约 0.9s），"
                        + "按钮在闪烁期间照常可点。\n"
                        + "新牌入堆不闪（那是玩家自己刚打出的）；找不到按钮时静默跳过，不影响机制。")))
            .AddSection("mechanism_m2", section => section
                .WithTitle(ModSettingsText.Literal("机制 · M2 时空间隙"))
                .AddToggle(
                    "mechanism_m2_enabled",
                    ModSettingsText.Literal("M2 总开关"),
                    m2Enabled,
                    ModSettingsText.Literal(
                        "开启：在抽牌堆右侧注册「间隙」按钮（点开是原版牌堆界面，只读查看），并启用复制/换回。\n"
                        + "定档口径：**坍缩 2 沙砾 · 唤回 2 沙砾 · 每回合不限次数 · 但必须交替**（同一种不能连做两次）。\n"
                        + "关闭：不注册牌堆与存档槽，激活/换回直接拒绝，不留坏状态。\n"
                        + "⚠️ 改动需要重启游戏生效（牌堆与数据槽只在初始化时注册一次）。"))
                .AddIntSlider(
                    "mechanism_m2_capacity",
                    ModSettingsText.Literal("间隙容量"),
                    m2Capacity,
                    1,
                    10,
                    1,
                    value => $"{value} 张",
                    ModSettingsText.Literal(
                        "定档 **10 = 本体手牌上限**（坍缩复制的是整手牌，容量低于手牌上限就会截断）。"))
                .AddToggle(
                    "mechanism_m2_action_buttons",
                    ModSettingsText.Literal("M2 战斗按键（坍缩 / 唤回 · 能量计数器下方）"),
                    m2ActionButtons,
                    ModSettingsText.Literal(
                        "默认开。战斗里在<b>能量计数器正下方</b>显示两个按钮：「坍缩」「唤回」（悬停有说明）。\n"
                        + "全部用原版资源（原版按钮控件 / 底板 / 字体 / 悬停说明）；不可用时会灰掉"
                        + "（判定复用机制自己的预测：两道闸 + 沙砾 + 手牌 / 间隙内容）。\n"
                        + "⚠️ 位置微调在 `Core/Presentation/UI/GapActionButtons.cs` 顶部的 "
                        + "`GapBelowCounter` / `XOffset` / `ButtonSize` 三个常量。"))
                .AddIntSlider(
                    "mechanism_m2_action_cost",
                    ModSettingsText.Literal("M2 动作代价（沙砾 · 坍缩 / 唤回统一）"),
                    m2CollapseCost,
                    0,
                    6,
                    1,
                    value => $"{value} 沙砾",
                    ModSettingsText.Literal(
                        "定档 **2**：坍缩与唤回**各收这一笔**（一次动作固定收一笔，**不按张数乘**）。\n"
                        + "2026-10-13 用户口径「M2 统一 2 沙砾」—— 原先是「坍缩 2 / 唤回按张计费」。\n"
                        + "不足 ⇒ 整次动作被拒（不搬牌、不弃手牌）。付出去的沙砾会**等量生成空隙**（M1 的不变式）。"))
                .AddToggle(
                    "mechanism_m2_refill_draw",
                    ModSettingsText.Literal("坍缩后补抽（旧口径）"),
                    m2RefillDraw,
                    ModSettingsText.Literal(
                        "**默认关**（Step 5）。新口径：坍缩的代价就是「这回合手牌变少」，不再从抽牌堆补回来。\n"
                        + "打开 = 回到旧行为（补抽原手牌数量），仅供对照。"))
                .AddToggle(
                    "mechanism_m2_ethereal",
                    ModSettingsText.Literal("副本带虚无（回合末消耗）"),
                    m2Ethereal,
                    ModSettingsText.Literal(
                        "**定档开**（2026-10-14 规则层）：坍缩出来的副本带「虚无」，回合末没打掉就被消耗。\n"
                        + "关掉 = 副本可以跨回合留着（仅供对照实验）。"))
                .AddToggle(
                    "mechanism_m2_one_shot",
                    ModSettingsText.Literal("副本一次性（换回后清空间隙）"),
                    m2OneShot,
                    ModSettingsText.Literal(
                        "定档开，<b>硬闸</b>。\n"
                        + "⚠️ 当前实现（间隙里放<b>真实卡牌实例</b>）下这一项<b>已无实际差异</b>："
                        + "换回就是把实例搬进手牌，搬走后间隙自然归空，副本在结构上就是一次性的。\n"
                        + "保留开关只为将来若改回「记录式」实现时重新启用这条硬闸。")))
            .AddSection("mechanism_m4", section => section
                .WithTitle(ModSettingsText.Literal("机制 · M4 回溯"))
                .AddToggle(
                    "mechanism_m4_enabled",
                    ModSettingsText.Literal("M4 总开关"),
                    m4Enabled,
                    ModSettingsText.Literal(
                        "开启：装配回溯引擎（回合开始自动拍快照），并接上两条接缝：\n"
                        + "① M7 搁浅（真回档：撤销滞留 + 归位）；② M2 间隙（**刻意不回档**，这是设计收益）。\n"
                        + "另外两条是**明确不做**的：M1 沙砾 / 空隙是 run 级资源、回档后保持当前值（资源桥已退场）；"
                        + "敌人时间<b>只被污染、不能回退</b>（武装回退那条代码已删）。\n"
                        + "关闭：以上全部不装配。\n"
                        + "⚠️ 改动需要重启游戏生效（引擎与补丁只在初始化时装一次）。"))
                .AddToggle(
                    "mechanism_m4_cost_all_sand",
                    ModSettingsText.Literal("回溯代价：消耗全部沙砾"),
                    m4CostAllSand,
                    ModSettingsText.Literal(
                        "定档开。注意它是在**回档成功之后**扣的 —— 沙砾属于 M1 的回档范围，"
                        + "先扣会被恢复动作还回来，代价就白收了。\n"
                        + "关掉 = 回溯免费（仅供实验；「每场战斗 1 次」那条硬闸仍然生效）。")))
            .AddSection("mechanism_m6", section => section
                .WithTitle(ModSettingsText.Literal("机制 · M6 双卡组"))
                .AddToggle(
                    "mechanism_m6_enabled",
                    ModSettingsText.Literal("M6 总开关"),
                    m6Enabled,
                    ModSettingsText.Literal(
                        "开启：注册「第二副牌组」牌堆（顶栏按键，点开是本体的牌堆界面 = 免费预览）、"
                        + "战斗胜利自动轮换、调试按键可主动切换/热切换。\n"
                        + "关闭：上述全部不注册，<c>player.Deck</c> 就是唯一那副。\n"
                        + "⚠️ 需重启游戏生效；⚠️ 不要在已经切过卡组的局里关它（另一副会留在牌堆里不再轮换，不丢牌但也不会回来）。"))
                .AddToggle(
                    "mechanism_m6_auto_switch",
                    ModSettingsText.Literal("每场战斗胜利后自动轮换"),
                    m6AutoSwitch,
                    ModSettingsText.Literal(
                        "默认开。自动轮换是设计节奏、<b>不收费</b>；收费只针对主动切换。\n"
                        + "归队顺序：M7 的滞留倒计时先归队（`AfterCombatEnd`），轮换发生在后面的"
                        + " `AfterCombatVictory`，所以滞留卡稳定回到它待过的那一副。"))
                .AddToggle(
                    "mechanism_m6_manual_switch",
                    ModSettingsText.Literal("允许主动切换（控制面板「卡组」页）"),
                    m6ManualSwitch,
                    ModSettingsText.Literal(
                        "默认开。现在唯一入口是战斗内控制面板的「卡组」页；正式入口以后再定。\n"
                        + "⚠️ 将来若支持多人，这个入口必须换成 RitsuLib 的同步动作（右键或等价），"
                        + "否则单端改卡组会让校验和分化、直接终止 run。"))
                .AddToggle(
                    "mechanism_m6_hot_switch",
                    ModSettingsText.Literal("战斗内热切换（重铺本场牌堆）"),
                    m6HotSwitch,
                    ModSettingsText.Literal(
                        "默认开。做法照堕化拉撒路已验证的路径：只清 `PlayerCombatState.AllPiles`（不含主卡组）、"
                        + "旧手牌<b>移出战斗</b>（不是弃牌、不是消耗）、用 `PopulateCombatState` 重铺并补抽一手。\n"
                        + "关掉它 = 战斗中主动切换只换主卡组，本场牌堆不动，下一场才生效。"))
                .AddChoice(
                    "mechanism_m6_cost_source",
                    ModSettingsText.Literal("切换代价来源"),
                    m6CostSource,
                    [
                        new ModSettingsChoiceOption<string>("none", ModSettingsText.Literal("免费（测试期）")),
                        new ModSettingsChoiceOption<string>("sand", ModSettingsText.Literal("沙砾")),
                        new ModSettingsChoiceOption<string>("energy", ModSettingsText.Literal("能量")),
                    ],
                    ModSettingsText.Literal(
                        "**定价数值之后再定**（用户 2026-10-11 口径），本轮只做结构。\n"
                        + "⚠️ 为什么必须定价：切换太便宜 ⇒ 两副牌退化成「一副大牌组」、且 M1 翻转≈每回合回满资源；"
                        + "太贵 ⇒ M1 的日常价值被换组频率卡死。\n"
                        + "⚠️ 原来的「消耗空隙」这一档已在 Step 1 删除 —— 花空隙会破坏 M1 的硬不变式"
                        + "（沙砾 + 空隙 = 容量），是计划里点名的「高风险 1」。"),
                    ModSettingsChoicePresentation.Dropdown)
                .AddIntSlider(
                    "mechanism_m6_cost_amount",
                    ModSettingsText.Literal("切换代价数量"),
                    m6CostAmount,
                    0,
                    9,
                    1,
                    value => value == 0 ? "免费" : value.ToString(),
                    ModSettingsText.Literal(
                        "默认 0 = 免费（测试期）。扣费只有<b>一个统一入口</b>（`DeckSwitchCost.PayAsync`），"
                        + "将来改数值只改这里。"))
                .AddIntSlider(
                    "mechanism_m6_hot_switch_sand",
                    ModSettingsText.Literal("战斗内热切换的沙砾（定档 2）"),
                    m6HotSand,
                    0,
                    5,
                    1,
                    value => value == 0 ? "0（免费）" : $"{value} 沙砾",
                    ModSettingsText.Literal(
                        "「把整副牌换掉并重铺这一场」比场间换副更值钱 ⇒ 只对**战斗内切换**额外收这笔沙砾。\n"
                        + "走同一个扣费入口（`DeckSwitchCost`），不另开路径。**定档 2**（2026-10-13）。"))
                .AddIntSlider(
                    "mechanism_m6_switches_per_combat",
                    ModSettingsText.Literal("每场战斗主动切换上限（定档 1）"),
                    m6SwitchesPerCombat,
                    0,
                    3,
                    1,
                    value => value == 0 ? "不限" : $"{value} 次",
                    ModSettingsText.Literal(
                        "**定档 1**（2026-10-13）。这条是堵「两副牌变一副大牌组」的第二道闸，"
                        + "与下面的冷却一起构成「切换权」：每场最多 1 次、且两次之间至少隔 2 场。"))
                .AddIntSlider(
                    "mechanism_m6_manual_cooldown",
                    ModSettingsText.Literal("主动切换冷却（每 N 场可 1 次）"),
                    m6Cooldown,
                    0,
                    5,
                    1,
                    value => value == 0 ? "无冷却（测试期）" : $"{value} 场",
                    ModSettingsText.Literal(
                        "**定档 2**（2026-10-13：「切换权 = 每 2 场 1 次」）。0 = 无冷却，仅供测试。")))
            .AddSection("mechanism_m3", section => section
                .WithTitle(ModSettingsText.Literal("机制 · M3 操作时间 + M5 凋萎"))
                .AddToggle(
                    "mechanism_m3_enabled",
                    ModSettingsText.Literal("M3 总开关"),
                    m3Enabled,
                    ModSettingsText.Literal(
                        "开启：怪物像玩家一样拥有三堆（抽牌堆 / 手牌 / 弃牌堆，无消耗堆），"
                        + "每回合抽满 N = 意图卡总数（抽牌堆空则把弃牌堆洗回，模拟真实循环），每回合只打一张。\n"
                        + "关闭：完全回到原版掷招。\n"
                        + "⚠️ 需重启游戏生效。"))
                .AddToggle(
                    "mechanism_m3_managed_rollmove",
                    ModSettingsText.Literal("C11：接管原生 RollMove（已定稿 on）"),
                    m3ManagedRollMove,
                    ModSettingsText.Literal(
                        "开着 = 被托管的怪不再走原生掷招（否则它会推进 StateLog / 消耗怪物 RNG，悄悄改掉牌堆语义）。\n"
                        + "⚠️ 这是 Lab C 里**唯一会用但未实测**的路径 ⇒ 请人工过 2–3 回合，"
                        + "确认日志出现 `C11 接管 RollMove：跳过 …` 且 `log=` 不再每回合 +1。"))
                .AddChoice(
                    "mechanism_m3_void_mode",
                    ModSettingsText.Literal("虚无被抽到时的口径（三选一）"),
                    m3VoidMode,
                    [
                        new ModSettingsChoiceOption<string>("once", ModSettingsText.Literal("once：惩戒一次后直接移除（默认）")),
                        new ModSettingsChoiceOption<string>("recycle", ModSettingsText.Literal("recycle：每次抽到都惩戒")),
                        new ModSettingsChoiceOption<string>("nopenalty", ModSettingsText.Literal("nopenalty：不惩戒，只占手牌位")),
                    ],
                    ModSettingsText.Literal(
                        "虚无进的是**弃牌堆** ⇒ 要等「抽牌堆空 → 洗回 → 被抽到」才生效。\n"
                        + "once（默认，已按用户 2026-10-11 定稿）：抽到 → **进手牌占一个位**（先不扣血）→ "
                        + "**怪物行动之后**掉血惩戒 + 消耗掉 ⇒ 只生效一次；\n"
                        + "recycle：污染变持续，强度大涨，要重平衡；\n"
                        + "nopenalty：只挡一次手牌位。"),
                    ModSettingsChoicePresentation.Dropdown)
                .AddToggle(
                    "mechanism_m3_intent_belt",
                    ModSettingsText.Literal("怪物头顶的「意图卡带」（档 2）"),
                    m3IntentBelt,
                    ModSettingsText.Literal(
                        "默认开。把怪物的意图**手牌**画成一条只读卡带（空槽卡框 + 意图图标 + 角标："
                        + "虚无 / 耗 / 强 / ✗ / ×N），最多 5 张 + 「…+N」，挂在意图行上方并自动跟随怪物。\n"
                        + "原版意图图标（只显示这一回合要打的那张）照常显示，两者不冲突。"))
                .AddChoice(
                    "mechanism_m3_hand_end",
                    ModSettingsText.Literal("回合结束未打出的意图卡去哪"),
                    m3HandEnd,
                    [
                        new ModSettingsChoiceOption<string>("discard", ModSettingsText.Literal("进弃牌堆（默认，循环闭环）")),
                        new ModSettingsChoiceOption<string>("deckbottom", ModSettingsText.Literal("回抽牌堆底（旧行为）")),
                        new ModSettingsChoiceOption<string>("deckshuffle", ModSettingsText.Literal("随机洗回抽牌堆")),
                    ],
                    ModSettingsText.Literal(
                        "默认 discard ⇒「抽牌堆→手牌→弃牌堆→洗回」才闭环；"
                        + "另两档是 Lab C 的旧行为（回牌堆），留作对照。"),
                    ModSettingsChoicePresentation.Dropdown)
                .AddParagraph(
                    "m3_no_limit",
                    ModSettingsText.Literal(
                        "意图操纵（推迟 / 提前 / 剥夺 / 塞虚无）**不设每场次数上限**（规则层 2026-10-14 定档）。\n"
                        + "历史理由备查：Lab C 实测不限次可以把怪手牌灌到 22 张并直接把怪扣死 —— "
                        + "现在的约束改由「判定三档 + 只能打第一张」与卡牌自身费用承担。"))
                .AddIntSlider(
                    "mechanism_m5_void_damage",
                    ModSettingsText.Literal("M5：虚无惩戒扣血"),
                    m5VoidDamage,
                    0,
                    9,
                    1,
                    value => value == 0 ? "不扣血" : $"{value} 点（Unpowered）",
                    ModSettingsText.Literal(
                        "定稿 **3**（可多次强化 ⇒ 卡面数值会变，这里只是新卡的基础值）。\n"
                        + "怪物没有能量系统，所以原版 Void 的「扣 1 能量」换成扣血；"
                        + "结算点是**怪物行动之后**：先掉血、再消耗掉那张虚无，然后才抽牌。"))
                .AddToggle(
                    "mechanism_m5_eats_intent",
                    ModSettingsText.Literal("M5：虚无额外吃掉一张意图"),
                    m5EatsIntent,
                    ModSettingsText.Literal("副惩戒（默认关）。开着 = 被抽到的虚无会顺手让一张意图本回合不可打。"))
                .AddIntSlider(
                    "mechanism_m5_inject_per_use",
                    ModSettingsText.Literal("M5：一次塞几张虚无"),
                    m5InjectPerUse,
                    1,
                    5,
                    1,
                    value => $"{value} 张",
                    ModSettingsText.Literal("调试按键一次塞几张（默认 1）。正式卡牌的塞入张数由卡面决定。"))));
    }

    /// <summary>文本框的「输入是否合法」提示：只认 &gt; 0 的浮点数。</summary>
    private static bool IsPositiveFloat(string? value)
    {
        return float.TryParse(
                   value?.Trim(),
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var parsed)
               && parsed > 0f;
    }
}

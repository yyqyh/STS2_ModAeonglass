namespace STS2_Aeonglass.Core.Systems.Bootstrap;

using STS2_Aeonglass.Core.Systems.Bootstrap;
/// <summary>
/// 机制注册<b>总线</b>：逐条按开关注册各机制模块，单条失败不影响其它。
/// </summary>
/// <remarks>
/// ① 职责：<c>Register</c> 里按顺序调各模块入口。
/// ② 调用时机：<c>Main</c> 启动一次。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class MechanismBootstrap
{
    /// <summary>在 <c>Main.Initialize()</c> 里调用一次。</summary>
    public static void RegisterAll()
    {
        // 顺序 = 依赖顺序：Sand（时之沙）→ Stasis / Gap（搁浅 / 时空裂隙）→ Rewind（回溯）→ Intent / Wither（操作时间 / 凋萎）。
        Register(
            Systems.Sand.SandResourceModule.DisplayName,
            Systems.Sand.SandResourceModule.Enabled,
            Systems.Sand.SandResourceModule.Register);

        // 牌区模块里的两个机制各自一条（一个机制一个开关）。
        // ⚠️ 顺序有意义：时之沙 先注册，间隙的"沙砾费"卡才能在构造函数里拿到沙砾的完整 id。
        Register(ExtraPileModules.StasisDisplayName, ExtraPileModules.StasisEnabled, ExtraPileModules.RegisterStasis);
        Register(ExtraPileModules.TimeGapDisplayName, ExtraPileModules.TimeGapEnabled, ExtraPileModules.RegisterTimeGap);

        // 回溯最后注册：它的 时之沙 接缝要等沙砾 / 空隙已注册（读 SandResourceRegistry.Sand），
        // 搁浅 / 时空裂隙 接缝也要等两个牌堆已注册。
        Register(Rewind.RewindModule.DisplayName, Rewind.RewindModule.Enabled, Rewind.RewindModule.Register);

        // 双卡组：注册第二副牌组的 RunPersistent 牌堆 + 战斗开始补播种。
        // 与前面几条无依赖（它只碰 player.Deck 与自己的牌堆），放最后只是为了日志顺序好读。
        Register(Decks.DeckModule.DisplayName, Decks.DeckModule.Enabled, Decks.DeckModule.Register);

        // 操作时间+ 凋萎：订阅回合事件驱动意图手牌解析（C11 补丁随 Main 的补丁扫描一起装）。
        Register(Intent.IntentModule.DisplayName, Intent.IntentModule.Enabled, Intent.IntentModule.Register);

        // 「乱流」抽弃互换（不是 时之沙–搁浅 里的某一条，单列）。
        // ⚠️ **无条件注册**：接线（生命周期订阅 + 盯弃牌堆）永远装，总开关只 gate 运行时行为 ——
        // 否则"先在设置页打开开关"不会生效（实测：调用卡牌测试没效果）。
        Register("乱流（抽弃互换）", enabled: true, Riot.RiotPileWatch.Register);
    }

    /// <summary>注册单个机制模块（带开关判断与独立的错误隔离）。</summary>
    private static void Register(string name, bool enabled, Action register)
    {
        if (!enabled)
        {
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Host} 机制「{name}」已关闭（设置页里可开）→ 跳过注册。");
            return;
        }

        try
        {
            register();
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] {Const.Prefix.Host} 机制「{name}」注册失败：{ex.GetType().Name}: {ex.Message}");
        }
    }
}

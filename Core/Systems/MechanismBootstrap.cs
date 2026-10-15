namespace STS2_Aeonglass.Core.Systems;

using STS2_Aeonglass.Core.Systems.Zones;

/// <summary>
/// 机制注册总线：<b>逐条</b>按设置开关注册各机制模块，单条失败不影响其它条。
/// </summary>
/// <remarks>
/// <para>
/// 这一层的存在是为了让 <c>Main</c> 保持"薄"：Main 只调一次 <see cref="RegisterAll" />，
/// 不认识任何机制细节。新增机制 = 加一行 <see cref="Register(string, System.Func{bool}, System.Action)" />。
/// </para>
/// <para>
/// 纪律：① <b>一条机制一个开关</b>（关掉即整块不注册，回到"只有卡牌数值"）；
/// ② 每条独立 try/catch，一条炸了不影响其它条；③ 注册顺序 = 依赖顺序（目前 M1 无前置）。
/// </para>
/// <para>
/// 注意：次级资源 / 牌区 / 卡池这类东西**只能在初始化时注册一次**，
/// 所以开关改动要**重启游戏**才生效（设置页里已写明）。
/// </para>
/// </remarks>
internal static class MechanismBootstrap
{
    /// <summary>在 <c>Main.Initialize()</c> 里调用一次。</summary>
    public static void RegisterAll()
    {
        // 顺序 = 依赖顺序。后续：Zones（M7/M2）→ Rewind（M4）→ EnemyTime（M3/M5）。
        Register(
            Systems.SandResource.SandResourceModule.DisplayName,
            Systems.SandResource.SandResourceModule.Enabled,
            Systems.SandResource.SandResourceModule.Register);

        // 牌区模块里的两个机制各自一条（一个机制一个开关）。
        // ⚠️ 顺序有意义：M1 先注册，M2 的"沙砾费"卡才能在构造函数里拿到沙砾的完整 id。
        Register(ZonesModule.StasisDisplayName, ZonesModule.StasisEnabled, ZonesModule.RegisterStasis);
        Register(ZonesModule.TimeGapDisplayName, ZonesModule.TimeGapEnabled, ZonesModule.RegisterTimeGap);

        // M4「回溯」最后注册：它的 M1 接缝要等沙砾 / 空隙已注册（读 AeonglassSecondaryResources.Sand），
        // M7 / M2 接缝也要等两个牌堆已注册。
        Register(Rewind.RewindModule.DisplayName, Rewind.RewindModule.Enabled, Rewind.RewindModule.Register);

        // M6「双卡组」：注册第二副牌组的 RunPersistent 牌堆 + 战斗开始补播种。
        // 与前面几条无依赖（它只碰 player.Deck 与自己的牌堆），放最后只是为了日志顺序好读。
        Register(Decks.DecksModule.DisplayName, Decks.DecksModule.Enabled, Decks.DecksModule.Register);

        // M3「操作时间」+ M5「凋萎」：订阅回合事件驱动意图手牌解析（C11 补丁随 Main 的补丁扫描一起装）。
        Register(EnemyTime.EnemyTimeModule.DisplayName, EnemyTime.EnemyTimeModule.Enabled, EnemyTime.EnemyTimeModule.Register);

        // 「乱流」抽弃互换（不是 M1–M7 里的某一条，单列）。
        // ⚠️ **无条件注册**：接线（生命周期订阅 + 盯弃牌堆）永远装，总开关只 gate 运行时行为 ——
        // 否则"先在设置页打开开关"不会生效（实测：调用卡牌测试没效果）。
        Register("乱流（抽弃互换）", enabled: true, Riot.RiotSwap.Register);
    }

    /// <summary>注册单个机制模块（带开关判断与独立的错误隔离）。</summary>
    private static void Register(string name, bool enabled, Action register)
    {
        if (!enabled)
        {
            Const.Logger.Info($"[{Const.ModId}] 机制「{name}」已关闭（设置页里可开）→ 跳过注册。");
            return;
        }

        try
        {
            register();
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] 机制「{name}」注册失败：{ex.GetType().Name}: {ex.Message}");
        }
    }
}

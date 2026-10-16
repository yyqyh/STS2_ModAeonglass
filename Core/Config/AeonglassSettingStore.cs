using STS2RitsuLib;
using STS2RitsuLib.Utils.Persistence;

namespace STS2_Aeonglass.Core.Config;

/// <summary>设置的持久化入口（RitsuLib 数据存储）。</summary>
/// <remarks>
/// <para>
/// 读设置的人不该关心文件在哪，统一走 <see cref="Current" /> —— 它每次都会去数据存储里取当前值，
/// 所以<b>设置界面一改就立刻生效</b>（不需要重启游戏）。骨架照 <c>STS2_WhiteAlbum2</c>。
/// </para>
/// <para>
/// 存储注册必须在 <c>BeginModDataRegistration</c> 作用域里做（RitsuLib 用这个把它归到本 mod 名下），
/// 并且只做一次；<see cref="Initialize" /> 是幂等的。
/// </para>
/// </remarks>
internal static class AeonglassSettingStore
{
    internal const string DataKey = "settings";

    private const string FileName = "STS2_Aeonglass_settings.json";

    private static bool _initialized;

    /// <summary>当前设置（每次现取，改完立刻生效）。</summary>
    public static AeonglassSetting Current
    {
        get
        {
            Initialize();

            return RitsuLibFramework.GetDataStore(Const.ModId).Get<AeonglassSetting>(DataKey)
                   ?? new AeonglassSetting();
        }
    }

    /// <summary>
    /// <b>测试期：解除所有"每场 / 每回合次数"限制</b>（默认开）。
    /// </summary>
    /// <remarks>
    /// 各机制的次数闸门读这里：为真时"已用次数"一律按 0 处理（<b>代价照旧收</b>，只是不限次数）。
    /// 用户 2026-10-11 明确要求测试期所有功能都不带限制。
    /// </remarks>
    public static bool DebugUnlimited => Current.DebugUnlimitedPerCombat;

    /// <summary>
    /// <b>详细诊断日志开关</b>（默认关）：卡面对齐槽位 / 回溯 capture / 盯堆 / 乱流每回合读数 /
    /// 卡带就绪·发牌特效 这些"每次都打"的行读它。见 <see cref="AeonglassSetting.DebugVerboseLogs" />。
    /// </summary>
    public static bool DebugVerboseLogs => Current.DebugVerboseLogs;

    /// <summary>翻转"详细诊断日志"并落盘（控制面板「调试」页走这里；改完立刻生效，不需重启）。</summary>
    public static bool ToggleVerboseLogs()
    {
        Initialize();

        var store = RitsuLibFramework.GetDataStore(Const.ModId);
        store.Modify<AeonglassSetting>(DataKey, settings => settings.DebugVerboseLogs = !settings.DebugVerboseLogs);
        store.Save(DataKey);
        return Current.DebugVerboseLogs;
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        using (RitsuLibFramework.BeginModDataRegistration(Const.ModId, false))
        {
            RitsuLibFramework.GetDataStore(Const.ModId).Register(
                DataKey,
                FileName,
                SaveScope.Global,
                defaultFactory: () => new AeonglassSetting(),
                autoCreateIfMissing: true);
        }

        RitsuLibFramework.GetDataStore(Const.ModId).InitializeGlobal();
        _initialized = true;
    }
}

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

    /// <summary>把外观缩放写进设置并落盘（调试面板的「缩放 ±」按钮走这里）。</summary>
    /// <remarks>
    /// 设置页走的是 <c>ModSettingsValueBinding</c> 自己的写入通道；这里是<b>代码侧</b>的最小写入路径
    /// （照 together 的 <c>SetSymbiosisEnabled</c>）：改内存模型 + 落盘，两边读的是同一份，所以界面会跟着变。
    /// </remarks>
    public static void SetScale(string? value)
    {
        Initialize();

        var store = RitsuLibFramework.GetDataStore(Const.ModId);
        store.Modify<AeonglassSetting>(DataKey, settings => settings.Scale = NormalizeScale(value));
        store.Save(DataKey);
    }

    /// <summary>翻转外观朝向并落盘。</summary>
    public static void ToggleScaleXPositive()
    {
        Initialize();

        var store = RitsuLibFramework.GetDataStore(Const.ModId);
        store.Modify<AeonglassSetting>(DataKey, settings => settings.ScaleXPositive = !settings.ScaleXPositive);
        store.Save(DataKey);
    }

    /// <summary>把任意输入规整成合法的缩放值（非法一律回落到 0.12）。</summary>
    public static string NormalizeScale(string? value)
    {
        if (float.TryParse(
                value?.Trim(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed)
            && parsed > 0f)
        {
            return parsed.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        return "0.12";
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

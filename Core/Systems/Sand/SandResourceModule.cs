using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Sand;

/// <summary>时之沙的<b>模块入口</b>：一个 <c>Register()</c> 注册本机制的全部件。</summary>
/// <remarks>
/// ① 职责：注册两个次级资源与两处 UI，并注册"支付 → 生成空隙"的进程级订阅。
/// ② 挂点与时机：<c>Main</c> 只调 <c>MechanismBootstrap.RegisterAll()</c>，由它按开关逐条调用各模块的 <c>Register</c>。
/// ③ 依赖：<c>SandResourceRegistry</c>、<c>SandCounterUiWiring</c>、<c>SandGapInvariant</c>。
/// ④ ⚠️ 模块化约定：新增一个机制的代价 = 加一个 <c>Systems/&lt;模块&gt;/</c> 目录 + 在 bootstrap 里加一行；
/// 入口只做注册，机制逻辑放各自文件。
/// </remarks>
internal static class SandResourceModule
{
    /// <summary>日志 / 设置页里显示的机制名。</summary>
    public const string DisplayName = "时之沙";

    /// <summary>本模块是否启用（读设置；用户关掉时整块不注册）。</summary>
    public static bool Enabled => AeonglassSettingStore.Current.SandEnabled;

    /// <summary>注册本模块的一切（资源 / UI / 订阅）。</summary>
    public static void Register()
    {
        SandResourceRegistry.Register();

        // "支付沙砾 → 生成等量空隙"。走进程级监听器 + 角色过滤，原因见 SandGapInvariant 的注释。
        SecondaryResourceHook.RegisterGlobalListener(new SandGapInvariant());
    }
}

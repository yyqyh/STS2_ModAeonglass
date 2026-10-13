using STS2RitsuLib.Combat.SecondaryResources;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.SandResource;

/// <summary>
/// M1「时之沙」的<b>模块入口</b>（模块化约定：每个机制模块自己暴露一个 <c>Register()</c>）。
/// </summary>
/// <remarks>
/// <para>
/// <c>Main</c> 不认识 M1 的任何细节，只通过 <c>MechanismBootstrap.RegisterAll()</c> 按开关逐条调用各模块的
/// <see cref="Register" />；所以"新增一个机制"= 加一个 <c>Systems/&lt;模块&gt;/</c> 目录 + 在 bootstrap 里加一行。
/// </para>
/// <para>模块内部的三件事：① 注册两个次级资源与两处 UI；② 注册"支付 → 生成空隙"的进程级订阅。</para>
/// </remarks>
internal static class SandResourceModule
{
    /// <summary>日志 / 设置页里显示的机制名。</summary>
    public const string DisplayName = "M1 时之沙";

    /// <summary>本模块是否启用（读设置；用户关掉时整块不注册）。</summary>
    public static bool Enabled => AeonglassSettingStore.Current.SandEnabled;

    /// <summary>注册本模块的一切（资源 / UI / 订阅）。</summary>
    public static void Register()
    {
        AeonglassSecondaryResources.Register();

        // "支付沙砾 → 生成等量空隙"。走进程级监听器 + 角色过滤，原因见 AeonglassSandSubscription 的注释。
        SecondaryResourceHook.RegisterGlobalListener(new AeonglassSandSubscription());
    }
}

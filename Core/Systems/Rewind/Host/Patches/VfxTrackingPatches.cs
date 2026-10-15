using System.Reflection;

using HarmonyLib;

using STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

using STS2_Aeonglass.Core.Systems.Bootstrap;
namespace STS2_Aeonglass.Core.Systems.Rewind.Host.Patches;

/// <summary>
/// 给异步卡牌 VFX 挂"在飞"计数。
/// 不用 <c>[HarmonyPatch]</c> 属性扫描：这里逐个目标手动装，单个失败只记日志，
/// 不会因为一个方法改名就让整组补丁全废。
/// </summary>
internal static class VfxTrackingPatches
{
    /// <summary>(类型全名, 方法名, 计数标签)。只收无参重载，避免歧义。</summary>
    private static readonly (string TypeName, string Method, string Label)[] Targets =
    [
        ("MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NCardExhaustQuickVfx", "PlayAnimation", "quickExhaust"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NCardExhaustQuickVfx", "DelayedFree", "quickExhaustFree"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NCardExhaustVfx", "PlayAnimation", "exhaust"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NCardExhaustVfx", "DelayedFree", "exhaustFree"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NCardRemoveVfx", "PlayAnimation", "remove"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.Cards.NCardRemoveVfx", "DelayedFree", "removeFree"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.NCardFlyVfx", "PlayAnim", "fly"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.NCardFlyPowerVfx", "PlayAnim", "flyPower"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.NCardTrailVfx", "FadeOut", "trailFade"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.NCardTransformVfx", "PlayAnimation", "transform"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.NCardUpgradeVfx", "PlayAnimation", "upgrade"),
        ("MegaCrit.Sts2.Core.Nodes.Vfx.NCardEnchantVfx", "PlayAnimation", "enchant"),
    ];

    private static readonly MethodInfo PostfixMethod =
        typeof(VfxTrackingPatches).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>返回 (成功数, 失败数)。</summary>
    internal static (int Patched, int Failed) Install(Harmony harmony, Action<string> warn)
    {
        var patched = 0;
        var failed = 0;

        foreach (var (typeName, methodName, _) in Targets)
        {
            var type = AccessTools.TypeByName(typeName);
            var method = type is null ? null : AccessTools.Method(type, methodName, []);
            if (method is null || method.ReturnType != typeof(Task))
            {
                failed++;
                warn($"VFX 目标未找到或签名不符：{typeName}.{methodName}");
                continue;
            }

            try
            {
                harmony.Patch(method, postfix: new HarmonyMethod(PostfixMethod));
                patched++;
            }
            catch (Exception ex)
            {
                failed++;
                warn($"VFX 补丁失败 {typeName}.{methodName}: {ex.GetType().Name}");
            }
        }

        return (patched, failed);
    }

    /// <summary>把返回的 Task 换成"计数的 Task"：调用方 await 我们这份，动画真正结束才减一。</summary>
    /// <remarks>
    /// ⚠️ <b>这是全模组唯一"无条件挂在共享路径上"的补丁</b>（12 个本体异步卡 VFX，任何角色都会走到）。
    /// 所以这里先过 <see cref="CharacterGate" />：<b>不是我们的角色就直接 return，连包装都不做</b>
    /// ⇒ 原版 / 其他 mod 的角色拿到的还是本体原来那个 Task，行为与未装本 mod 时逐字一致。
    /// （屏障本身只被 回溯读取，而回溯只对我们的角色开 —— 所以过滤掉没有损失。）
    /// </remarks>
    private static void Postfix(MethodBase __originalMethod, ref Task __result)
    {
        if (!CharacterGate.IsActive)
        {
            return;
        }

        var label = LabelFor(__originalMethod);
        __result = VfxBarrier.Track(label, __result);
    }

    private static string LabelFor(MethodBase method)
    {
        var typeName = method.DeclaringType?.Name ?? "unknown";
        foreach (var (_, methodName, label) in Targets)
        {
            if (method.Name == methodName && typeName.Contains(label, StringComparison.OrdinalIgnoreCase))
            {
                return label;
            }
        }

        return typeName + "." + method.Name;
    }
}

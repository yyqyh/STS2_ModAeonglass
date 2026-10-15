using System.Reflection;

using HarmonyLib;

using MegaCrit.Sts2.Core.Modding;

using STS2RitsuLib;
using STS2RitsuLib.Interop;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Basic;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Relics;
using STS2_Aeonglass.Core.Framework.Diagnostics;
using STS2_Aeonglass.Core.Systems;

namespace STS2_Aeonglass;

/// <summary>STS2_Aeonglass 入口。</summary>
/// <remarks>
/// <para>
/// 内容（角色 / 卡牌 / 卡池）全部走 RitsuLib 的自动注册注解，这里只负责四件事：
/// <b>① 让 RitsuLib 发现本程序集；② 初始化设置存储与设置页；③ 装 Harmony 补丁；
/// ④ 把内容 id 打进日志</b>；外加一条纯表现层的接线 —— ⑤ 注册卡框（
/// <c>Core/Presentation/Art/AeonglassFrameMaterial</c>，双色对角分割 + [永时]/[消逝] 换色）。
/// </para>
/// <para>
/// 第 ① 步只有一条补丁（战斗房间挂调试面板）。补丁骨架保留在这里是<b>刻意的</b> ——
/// 它是「patch 零 failed」这条验收的观测口径，后续机制阶段还要往上加。
/// </para>
/// </remarks>
[ModInitializer(nameof(Initialize))]
public static class Main
{
    public static void Initialize()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 必须先让 RitsuLib 认识本程序集，注解注册才会生效。
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, RitsuLibFramework.CreateLogger(Const.ModId));
        ModTypeDiscoveryHub.RegisterModAssembly(Const.ModId, assembly);

        // 卡牌「来源副」记录（M2 间隙副本用）：走 RitsuLib 的 SavedAttachedState，
        // **必须在"存档属性名冻结"之前注册**（本体的序列化缓存是进跑局时才建的）⇒ 放在最前面。
        try
        {
            Core.Systems.Decks.DeckOrigin.Register();
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] 卡牌「来源副」记录注册失败（间隙副本会退回「跟当前生效副」）：{Describe(ex)}");
        }

        // 设置必须在任何「读设置」的代码之前就位（调试面板在战斗里才读，但补丁装完就可能被调用）。
        try
        {
            AeonglassSettingStore.Initialize();
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] 设置存储初始化失败（不影响装补丁）：{Describe(ex)}");
        }

        try
        {
            AeonglassModSettingsPage.Register();
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] 设置页注册失败：{Describe(ex)}");
        }

        // 卡框（纯表现层，无开关）：双色对角分割 + [永时]/[消逝] 换色。
        // 走 RitsuLib 的"外部卡框材质注册表"，一个入口管全部卡，不必给每张卡加基类。
        try
        {
            Core.Presentation.Art.AeonglassFrameMaterial.Register();
        }
        catch (Exception ex)
        {
            Const.Logger.Error($"[{Const.ModId}] 卡框注册失败（卡面按本体默认框走）：{Describe(ex)}");
        }

        // 机制接入：Main 不认识任何机制细节，只按开关逐条注册（见 MechanismBootstrap 的纪律）。
        MechanismBootstrap.RegisterAll();

        // M5 凋萎：注册 mod 关键词「凋萎」（必须在内容冻结之前 ⇒ 就放这儿）。
        Core.Content.Keywords.WitherKeyword.Register();

        // 迟滞：注册 mod 关键词「迟滞」（同上，必须在内容冻结之前）。
        Core.Content.Keywords.HysteresisKeyword.Register();

        // 搁浅（M7）：注册 mod 关键词「搁浅」（同上）。
        Core.Content.Keywords.StasisKeyword.Register();
        Core.Content.Keywords.EternalKeyword.Register();

        var (applied, failed) = InstallHarmonyPatches(assembly);

        // 本地化键对不上时看这几行最快。
        ContentIdLogger.LogAll(Const.ModId, ContentTypes);

        Const.Logger.Info(
            $"[{Const.ModId}] initialized v{Const.Version}; patch classes applied={applied} failed={failed}; "
            + $"harmony patched {new Harmony(Const.ModId).GetPatchedMethods().Count()} method(s).");
    }

    /// <summary>逐个类安装 Harmony 补丁，单个类失败不影响其它类。</summary>
    private static (int Applied, int Failed) InstallHarmonyPatches(Assembly assembly)
    {
        var harmony = new Harmony(Const.ModId);
        var applied = 0;
        var failed = 0;

        foreach (var type in assembly.GetTypes())
        {
            if (!HasHarmonyPatches(type))
            {
                continue;
            }

            try
            {
                harmony.CreateClassProcessor(type).Patch();
                applied++;
            }
            catch (Exception ex)
            {
                failed++;
                Const.Logger.Error($"[{Const.ModId}] 补丁类 {type.Name} 应用失败：{Describe(ex)}");
            }
        }

        return (applied, failed);
    }

    /// <summary>要在启动日志里打印 id 的内容类型（新增内容时记得加进来）。</summary>
    private static Type[] ContentTypes =>
    [
        typeof(Aeonglass),
        typeof(AeonglassCardPool), typeof(AeonglassRelicPool), typeof(AeonglassPotionPool),
        typeof(AeonglassStrike), typeof(AeonglassDefend),
        typeof(AeonglassCore),
    ];

    /// <summary>类级或方法级带 [HarmonyPatch] 的类型都要装。</summary>
    private static bool HasHarmonyPatches(Type type)
    {
        if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
        {
            return true;
        }

        foreach (var method in type.GetMethods(
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (method.GetCustomAttributes(typeof(HarmonyPatch), true).Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string Describe(Exception ex)
    {
        var parts = new List<string>();

        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            parts.Add($"{current.GetType().Name}: {current.Message}");
        }

        return string.Join("  ←  ", parts);
    }
}

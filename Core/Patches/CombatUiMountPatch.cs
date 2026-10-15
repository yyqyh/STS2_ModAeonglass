using HarmonyLib;

using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2_Aeonglass.Core.Presentation.Combat;
using STS2_Aeonglass.Core.Presentation.Combat.Gap;
using STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
using STS2_Aeonglass.Core.Presentation.ControlPanel;
using STS2_Aeonglass.Core.Systems;

using STS2_Aeonglass.Core.Systems.Bootstrap;
namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// 战斗房间就绪时，把"挂在战斗场景上的 UI"挨个试挂一遍。
/// </summary>
/// <remarks>
/// <para>
/// <b>补丁纪律</b>（2026-10-11 定的结构约定）：
/// ① <b>通用补丁</b>（跨机制、只做挂载/接线）统一放 <c>Core/Patches/</c> —— 本文件就是；
/// ② <b>只服务某个机制的补丁</b>跟机制走，放 <c>Core/Systems/&lt;模块&gt;/Patches/</c>
/// （例如 意图 接管 <c>RollMove</c> 的那个补丁）。
/// </para>
/// <para>
/// 本补丁不关心任何机制逻辑：它只调各处的 <c>Host.Attach</c>，
/// 而每个 <c>Host</c> 自己判断开关（统一控制面板看 <c>ControlPanel</c>，
/// 意图卡带看 <c>IntentIntentBelt</c>）—— 所以"关掉"时这个补丁什么也不做。
/// </para>
/// <para>
/// <b>2026-10-13 收口</b>：沙砾 / 时空裂隙 / 意图与凋萎 / 回溯 / 双卡组 各自的按键面板 + 外观调试面板（6 个 Host）
/// 已经并成一个 <see cref="ControlPanelHost" />；战斗内现在只挂"统一控制面板 + 意图卡带 + 传送门"三样。
/// </para>
/// <para>
/// ⚠️ <b>角色门（2026-10-12 补）</b>：
/// 这条补丁打在<b>本体的 <c>NCombatRoom._Ready</c></b> 上 —— 玩任何角色都会走到这里。
/// 所以第一件事就是问 <see cref="CharacterGate" />：不是永世沙漏就<b>一个面板都不挂</b>，
/// 原版 / 其他 mod 的角色连节点都不会多出一个。
/// （各 Host 只判断"开关"，不判断"是不是我们的角色"——那是这里的职责。）
/// </para>
/// </remarks>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom._Ready))]
internal static class CombatUiMountPatch
{
    [HarmonyPostfix]
    private static void Postfix(NCombatRoom __instance)
    {
        if (!CharacterGate.IsActive)
        {
            return;
        }

        ControlPanelHost.Attach(__instance);
        AeonglassGapActionRowHost.Attach(__instance);
        AeonglassIntentBeltHost.Attach(__instance);
        AeonglassGapPortals.Attach(__instance);

        // 「领域」战斗特效层：插在背景与人物之间（CombatSceneContainer 第 1 位），目前常启。
        AeonglassDomainLayerHost.Attach(__instance);
    }
}

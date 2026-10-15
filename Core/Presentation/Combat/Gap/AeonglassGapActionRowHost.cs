using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Presentation.Character;
using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Presentation.Combat.Gap;
/// <summary>把 时空裂隙 按键挂进战斗房间（<b>能量计数器正下方</b>）。</summary>
/// <remarks>
/// ① 职责：只负责挂载与定位基准（能量计数器节点）。
/// ② 调用时机：<c>CombatUiMountPatch</c> 战斗房间 <c>_Ready</c>。
/// ③ 依赖：能量计数器节点 + <c>AeonglassGapActionRow</c>。
/// ④ ⚠️ 位置微调改 <c>AeonglassGapActionTexts</c>（设置页提示指向那里）。
/// </remarks>

internal static class AeonglassGapActionRowHost
{
    internal static void Attach(Node parent)
    {
        AeonglassSettingStore.Initialize();

        // ⚠️ 配置项名保持 GapActionButtons（改名会让已存盘的设置读不到），它和本类名 AeonglassGapActionTexts 不是一回事。
        if (!AeonglassSettingStore.Current.GapEnabled || !AeonglassSettingStore.Current.GapActionButtons)
        {
            return;
        }

        // ⚠️ 挂载点历史（别再来回改）：
        //   ① 原来挂 `NCombatRoom`（房间）下 —— 不在战斗 UI 子树里，UI 隐藏它不跟着隐藏、
        //      暂停界面的压暗也管不到它（会显得"高亮"）。
        //   ② 中途曾塞进 `EnergyCounterContainer` 最前面 —— 成了 UI 里画得最早的东西，
        //      点击被后面的牌堆按钮全吃掉（"点不了了"）。已废弃。
        //   ③ **现在：挂 `NCombatUi` 下、并追加为它的最后一个子节点**（用户 2026-10-14 口径）：
        //      同子树 ⇒ 生命周期与战斗 UI 完全一致（隐藏/压暗都跟着）；
        //      最后一个 ⇒ 画在牌堆 / 手牌之上，点击不会被它们吃。
        //
        //   ⚠️ 2026-10-15 用户口径（恢复原顺序）：**画在"能量计数器之上、回合按键之下"** ⇒
        //      改成插到 `NCombatUi.%EndTurnButton` 之前（子节点序决定绘制序），并把 ZIndex 归 0，
        //      否则 ZIndex=213 会越过回合按键、与"之下"矛盾。
        //      代价：与后面那些兄弟（牌堆按钮 / 手牌 / 预览容器）重叠的地方，点击仍会被它们吃掉 ——
        //      本行现在贴在计数器正下方、体积很小，实测不重叠；若哪天点不动了，先看是不是这里。
        var host = parent is NCombatRoom { Ui: { } combatUi } ? combatUi : parent;

        foreach (var child in host.GetChildren())
        {
            if (child is AeonglassGapActionRow)
            {
                return;
            }
        }

        var row = new AeonglassGapActionRow
        {
            Name = AeonglassGapActionRow.NodeName,
            UniqueNameInOwner = true,
            ZIndex = 0,
        };

        host.AddChild(row);

        // 插到"回合按键"之前 ⇒ 画在能量计数器（更早的兄弟）之上、回合按键之下。
        // 找不到回合按键时退回"最后一个子节点"（宁可层级高一点，也不要被牌堆按钮吃掉点击）。
        if (host is NCombatUi ui
            && ui.EndTurnButton is { } endTurn
            && GodotObject.IsInstanceValid(endTurn)
            && endTurn.GetParent() == host)
        {
            host.MoveChild(row, endTurn.GetIndex());
        }
        else
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Gap} 找不到「回合按键」⇒ 坍缩/唤回按键行退到最后一个子节点（层级会偏高）");
            host.MoveChild(row, -1);
        }
    }
}

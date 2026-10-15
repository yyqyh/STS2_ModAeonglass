using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace STS2_Aeonglass.Core.Presentation.Combat;
/// <summary>把「领域」层插进战斗房间（<c>CombatSceneContainer</c> 的第 1 位）。</summary>
/// <remarks>
/// ① 职责：只负责挂载与插入位置。
/// ② 调用时机：<c>CombatUiMountPatch</c> 在战斗房间 <c>_Ready</c> 时调。
/// ③ 依赖：场景 <c>aeonglass_domain.tscn</c>；父节点 <c>CombatSceneContainer</c>。
/// ④ ⚠️ 插入位置 = 绘制序（背景之后、其余之前）；改 index 会盖住或被盖住。
/// </remarks>

internal static class AeonglassDomainLayerHost
{
    /// <summary>插入位置：<c>CombatSceneContainer</c> 的第 1 位 = BgContainer 之后、其余之前。</summary>
    private const int InsertIndex = 1;

    internal static void Attach(Node room)
    {
        if (room.GetNodeOrNull<Control>("CombatSceneContainer") is not { } sceneContainer)
        {
            Const.Logger.Warn($"[{Const.ModId}] {Const.Prefix.Host} 找不到 CombatSceneContainer ⇒「领域」层没挂上。");
            return;
        }

        if (sceneContainer.GetNodeOrNull<AeonglassDomainLayer>(AeonglassDomainLayer.NodeName) is not null)
        {
            return;
        }

        var layer = PreloadManager.Cache
            .GetScene(Const.Paths.Ui.DomainScene)
            .Instantiate<AeonglassDomainLayer>(PackedScene.GenEditState.Disabled);

        layer.Name = AeonglassDomainLayer.NodeName;
        sceneContainer.AddChild(layer);
        sceneContainer.MoveChild(layer, InsertIndex);

        // 用户口径 2026-10-14：**进战斗就自动播** —— wipe 把领域擦出来（progress 0 → 0.5），
        // 走完自动 Queue 到 breathe，在"过下边中点 ↔ 过上边中点"之间正逆循环。
        layer.PlayWipe();

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} [领域] 特效层已挂入 CombatSceneContainer 第 {InsertIndex} 位"
            + "（BgContainer 之后、BackCombatVfxContainer / 人物之前）；已自动播 wipe → breathe 循环");
    }
}

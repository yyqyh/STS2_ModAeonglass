using Godot;

using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
/// <summary>意图卡带的<b>调度器</b>（随战斗房间存在）。</summary>
/// <remarks>
/// ① 职责：巡一遍战斗里的怪，给"被托管的"挂卡带，并清理不该留的。
/// ② 调用时机：战斗房间 <c>_Ready</c> 之后持续运行。
/// ③ 依赖：<c>NCreature</c> 节点 + 意图 意图状态。
/// ④ ⚠️ 怪死亡 / 换房间必须清理，避免悬挂节点（卡带挂在意图容器父节点上）。
/// </remarks>

public partial class AeonglassIntentBeltManager : Control
{
    internal const string NodeName = "AeonglassIntentBeltManager";

    private const float ScanInterval = 0.3f;

    private float _timer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _timer += (float)delta;
        if (_timer < ScanInterval)
        {
            return;
        }

        _timer = 0f;

        var setting = AeonglassSettingStore.Current;
        if (!setting.IntentEnabled || !setting.IntentIntentBelt || !Systems.Bootstrap.CharacterGate.IsActive)
        {
            return;
        }

        if (NCombatRoom.Instance is not { } room)
        {
            return;
        }

        foreach (var node in room.CreatureNodes)
        {
            var creature = node?.Entity;
            if (creature is null || creature.Monster is null || creature.IsDead)
            {
                continue;
            }

            // 意图牌堆还没建（这只怪还没轮到解析）就不挂 —— 避免空卡带闪一下。
            if (!IntentDeck.TryGet(creature, out var state) || state.Hand.Count == 0)
            {
                continue;
            }

            AeonglassIntentBelt.Ensure(node!, creature);
        }
    }
}

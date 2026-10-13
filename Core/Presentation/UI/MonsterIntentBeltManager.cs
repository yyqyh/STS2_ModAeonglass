using Godot;

using MegaCrit.Sts2.Core.Nodes.Rooms;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Presentation.UI;

/// <summary>
/// 给战斗里每只"被托管的怪"挂上意图卡带（档 2）的调度器，随战斗房间存在。
/// </summary>
/// <remarks>
/// 只做三件事：找敌人节点 → 给还没建堆的怪跳过 → 保证它有卡带。
/// 卡带自己负责刷新与跟随（它是怪物节点的子节点）。
/// </remarks>
public partial class MonsterIntentBeltManager : Control
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
        if (!setting.IntentEnabled || !setting.IntentIntentBelt || !Systems.CharacterGate.IsActive)
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

            MonsterIntentBelt.Ensure(node!, creature);
        }
    }
}

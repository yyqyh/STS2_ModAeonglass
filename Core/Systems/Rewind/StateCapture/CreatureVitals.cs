using MegaCrit.Sts2.Core.Entities.Creatures;

using STS2_Aeonglass.Core.Systems.Rewind.Kernel;

namespace STS2_Aeonglass.Core.Systems.Rewind.StateCapture;

/// <summary>
/// 生物<b>血量快照</b>（回档时按它写回）。
/// </summary>
/// <remarks>
/// ① 职责：记录当前/最大生命与格挡。
/// ② 调用时机：抓取与恢复。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal readonly record struct Vitals(int Hp, int MaxHp, int Block);

/// <summary>
/// 血量与格挡的捕获 / 恢复。
/// 走**属性 setter**（private set 也走），因此 <c>CurrentHpChanged</c> / <c>MaxHpChanged</c> /
/// <c>BlockChanged</c> 会照常触发，血条数字自己就更新了。
/// </summary>
internal static class CreatureVitals
{
    private const string MaxHp = "MaxHp";

    private const string CurrentHp = "CurrentHp";

    private const string Block = "Block";

    internal static Dictionary<Creature, Vitals> Capture(IEnumerable<Creature> creatures)
    {
        var result = new Dictionary<Creature, Vitals>();
        foreach (var creature in creatures)
        {
            result[creature] = new Vitals(creature.CurrentHp, creature.MaxHp, creature.Block);
        }

        return result;
    }

    /// <summary>先恢复上限再恢复当前值（上限变小时不会出现越界），最后恢复格挡。返回处理的生物数。</summary>
    internal static int Restore(
        Dictionary<Creature, Vitals> snapshot,
        IRewindLog log,
        bool restoreHp,
        bool restoreBlock)
    {
        var applied = 0;

        foreach (var (creature, vitals) in snapshot)
        {
            var touched = false;

            if (restoreHp)
            {
                Reflect.SetProperty(creature, MaxHp, vitals.MaxHp);
                if (!Reflect.SetProperty(creature, CurrentHp, Math.Max(0, vitals.Hp)))
                {
                    log.Warn($"{creature.LogName}: 恢复 CurrentHp 失败");
                }

                touched = true;
            }

            if (restoreBlock)
            {
                Reflect.SetProperty(creature, Block, Math.Max(0, vitals.Block));
                touched = true;
            }

            if (touched)
            {
                applied++;
            }
        }

        return applied;
    }
}

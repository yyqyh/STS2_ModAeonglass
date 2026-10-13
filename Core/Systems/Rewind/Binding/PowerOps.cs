using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Rewind.Binding;

/// <summary>力量对齐结果：补回多少、移除多少。</summary>
internal readonly record struct PowerAlignment(int ReAdded, int Removed);

/// <summary>
/// power 增删对齐（规格 §2.6.3 第 5 步）：先移除"快照后新增的"，再补回"快照里被移除的"，
/// 最后才逐字段写回层数。三步顺序不能换 —— 否则会出现同类 power 重复。
/// </summary>
internal static class PowerOps
{
    internal static PowerAlignment Align(
        Creature owner,
        IReadOnlyCollection<PowerModel> snapshotPowers,
        IEnumerable<PowerModel> livePowers)
    {
        var removed = 0;
        foreach (var power in livePowers.ToList())
        {
            if (snapshotPowers.Contains(power))
            {
                continue;
            }

            try
            {
                // 走公开 API：会触发 Removed / PowerRemoved，界面上的 power 图标随之消失。
                power.RemoveInternal();
                removed++;
            }
            catch
            {
                // 单个 power 移除失败不影响其它
            }
        }

        var reAdded = 0;
        foreach (var power in snapshotPowers)
        {
            if (owner.Powers.Contains(power))
            {
                continue;
            }

            try
            {
                Reflect.Set(power, Coordinates.PowerModel.Owner, owner);
                owner.ApplyPowerInternal(power);
                reAdded++;
            }
            catch
            {
                // 单个 power 补回失败不影响其它
            }
        }

        return new PowerAlignment(reAdded, removed);
    }
}

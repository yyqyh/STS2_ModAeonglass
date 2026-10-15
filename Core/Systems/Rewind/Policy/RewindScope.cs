namespace STS2_Aeonglass.Core.Systems.Rewind.Policy;

/// <summary>
/// 回溯的<b>作用域开关</b>：决定这次回档回退哪些范围（战斗 / 玩家 / 只读探针）。
/// </summary>
/// <remarks>
/// ① 职责：作用域取值与判定。
/// ② 调用时机：抓取与恢复前。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

internal sealed class RewindScope
{
    /// <summary>我方 power：回档。</summary>
    public bool RestorePlayerPowers { get; init; } = true;

    /// <summary>敌人 power：不回档（只能污染敌人的时间，不能撤回）。</summary>
    public bool RestoreEnemyPowers { get; init; }

    /// <summary>玩家侧血量。设计上不回档（死亡不可逆）；实现保留，默认关，需要时置 true。</summary>
    public bool RestorePlayerHp { get; init; }

    /// <summary>玩家侧格挡。实现保留，默认关（设计口径：格挡属"永久资源"一侧的外围，先不开）。</summary>
    public bool RestorePlayerBlock { get; init; }

    /// <summary>敌人血量：不回档（只能污染敌人的时间）。</summary>
    public bool RestoreEnemyHp { get; init; }

    /// <summary>敌人格挡：默认不回档，理由同上。</summary>
    public bool RestoreEnemyBlock { get; init; }

    public bool RestoreGold { get; init; }

    public bool RestoreRelics { get; init; }

    public bool RestorePotions { get; init; }

    public bool RestoreRunState { get; init; }

    /// <summary>战斗牌堆：回档。</summary>
    public bool RestorePiles { get; init; } = true;

    /// <summary>战斗历史：回档（否则"本回合"类计数会双重计数）。</summary>
    public bool RestoreCombatHistory { get; init; } = true;

    /// <summary>二级资源（RitsuLib 沙砾 / 空隙）：回档。</summary>
    public bool RestoreSecondaryResources { get; init; } = true;

    /// <summary>
    /// RNG（run Rng 集合 + 每只怪的 Rng + mod 自己的 StsRng）：回档。
    /// <b>恢复顺序上必须最先做</b>（规格 §2.6.3 第 3 步），否则后面的恢复动作一消耗 RNG 就再也对不上了。
    /// </summary>
    public bool RestoreRng { get; init; } = true;

    /// <summary>VFX 屏障严格模式：有异步卡 VFX 在跑时拒绝回档。</summary>
    public bool VfxBarrierStrict { get; init; } = true;

    /// <summary>回合号 / 轮次 / 相位：回档。</summary>
    public bool RestoreTurnCounters { get; init; } = true;

    public static RewindScope Default { get; } = new();

    /// <summary>作用域一行摘要（面板显示，便于确认哪些项开着）。</summary>
    internal string Describe()
    {
        static string Mark(bool on) => on ? "√" : "x";

        return $"牌堆{Mark(RestorePiles)} 能量{Mark(RestoreTurnCounters)} 历史{Mark(RestoreCombatHistory)} "
            + $"RNG{Mark(RestoreRng)} 二级资源{Mark(RestoreSecondaryResources)} "
            + $"玩家HP{Mark(RestorePlayerHp)} 玩家格挡{Mark(RestorePlayerBlock)} "
            + $"敌人HP{Mark(RestoreEnemyHp)} 敌人格挡{Mark(RestoreEnemyBlock)} "
            + $"VFX屏障{Mark(VfxBarrierStrict)}";
    }
}

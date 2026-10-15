namespace STS2_Aeonglass.Core.Presentation.Animations;
/// <summary>永世沙漏骨架的<b>动画名 / 状态机 trigger 名</b>常量表（避免各处裸字符串拼错）。</summary>
/// <remarks>
/// ① 职责：动画名与 trigger 名的唯一常量表。
/// ② 调用时机：角色状态机把"攻击/受击/施法"等事件映射到骨架动画时读。
/// ③ 依赖：<c>aeonglass.skel</c> 里真实存在的动画名（美术导出时定）。
/// ④ ⚠️ 名字与骨架不一致 ⇒ 动画<b>静默不播</b>（不抛错）；改名必须与美术同步。
/// </remarks>

public static class AeonglassAnimNames
{
    // ================== 轨道 0：身体动画（骨架里真实存在的名字）==================

    /// <summary>待机（循环）。</summary>
    public const string Idle = "idle_loop";

    /// <summary>施法（原版「渐增强度」动作）—— 玩家侧 <c>Cast</c> / <c>PowerUp</c> 降级到它。</summary>
    public const string Wither = "wither";

    /// <summary>重击（原版「退潮」）—— 玩家侧默认 <c>Attack</c> 降级到它。</summary>
    public const string AttackHeavy = "attack_heavy";

    /// <summary>双击 2 段（原版「眼棱」）—— 留给后续卡牌点名。</summary>
    public const string AttackDouble = "attack_double";

    /// <summary>受击。</summary>
    public const string Hurt = "hurt";

    /// <summary>死亡。</summary>
    public const string Die = "die";

    // ================== 轨道 1：环旋转叠加（外环在转靠它）==================

    /// <summary>外环持续旋转（循环，轨道 1）。</summary>
    public const string RingsNormal = "_track1/rings_normal";

    /// <summary>重击时的环表现（轨道 1；① 未接）。</summary>
    public const string RingsAttackHeavy = "_track1/rings_attack_heavy";

    /// <summary>双击时的环表现（轨道 1；① 未接）。</summary>
    public const string RingsAttackDouble = "_track1/rings_attack_double";

    /// <summary>死亡时的环表现（轨道 1；① 未接）。</summary>
    public const string RingsDie = "_track1/rings_die";

    // ================== 状态机 trigger 名 ==================

    /// <summary>本体 <c>CreatureAnimator</c> 没有这两个常量，只有它们对应的动画名要自己写。</summary>
    public const string AttackHeavyTrigger = "AttackHeavy";

    /// <inheritdoc cref="AttackHeavyTrigger" />
    public const string AttackDoubleTrigger = "AttackDouble";

    /// <summary>轨道 1 的触发名（调试面板用）。</summary>
    public const string RingsNormalTrigger = "RingsNormal";
}

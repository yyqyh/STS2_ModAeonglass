namespace STS2_Aeonglass.Core.Presentation.Animations;

/// <summary>
/// 永世沙漏骨架里的**动画名 / 状态机 trigger 名**，集中在这里，避免各处的裸字符串拼错。
/// </summary>
/// <remarks>
/// <para>
/// 骨架 <c>STS2_Aeonglass/images/character/spine/aeonglass.skel</c>（原版 Act 3 boss「永世沙漏」，
/// 直接从 <c>orign/animations/monsters/aeonglass/</c> 搬来，未做任何转换）。
/// </para>
/// <para>
/// <b>这套骨架只有 6 个身体动画</b>：<c>idle_loop</c> / <c>wither</c> / <c>attack_heavy</c> /
/// <c>attack_double</c> / <c>hurt</c> / <c>die</c>。玩家角色需要的 <c>attack</c> / <c>cast</c> /
/// <c>low_health_loop</c> / <c>relaxed_loop</c> <b>不存在</b>，在角色类的状态机里做降级映射
/// （见 <c>Aeonglass.SetupCustomCreatureAnimator</c> 与计划文档 §2.2.2 的「待填充清单」）。
/// </para>
/// <para>
/// <b>轨道 1</b>（<c>_track1/rings_*</c>）是"外环旋转"的叠加动画，与轨道 0 上的身体动画并行播放 ——
/// 由 <c>AeonglassCreatureVisuals._Ready</c> 播起来。
/// </para>
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

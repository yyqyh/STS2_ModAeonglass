namespace STS2_Aeonglass.Core.Systems.EnemyTime;

/// <summary>
/// 标记接口：**这张卡允许"指向怪物意图卡"**（不是指向生物）。
/// </summary>
/// <remarks>
/// <para>
/// 2026-10-13：原来 <c>IntentTargetingPatches</c> 是按三张具体的测试卡（推迟 / 提前 / 剥夺）判的；
/// 那三张测试卡已按用户口径删除 ⇒ 改成这个**标记接口**，卡表落地时让真正的"动作卡"实现它即可，
/// 指向机器（<c>IntentTargeting</c> + 侧表 <c>IntentTargetRegistry</c> + 三个补丁）原样保留。
/// </para>
/// <para>
/// 用法（卡表阶段）：动作卡的 <c>TargetType</c> 用 <c>TargetedNoCreature</c>，类声明上写
/// <c>: ModCardTemplate, IIntentTargetCard</c>，出牌时调 <c>IntentActionCard.ApplyPointed(...)</c>。
/// </para>
/// </remarks>
internal interface IIntentTargetCard;

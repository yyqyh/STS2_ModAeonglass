namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 标记接口：<b>这张卡允许指向怪物意图卡</b>（不是指向生物）。
/// </summary>
/// <remarks>
/// ① 职责：类型标记。
/// ② 调用时机：指向链路判定。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal interface IIntentTargetCard;

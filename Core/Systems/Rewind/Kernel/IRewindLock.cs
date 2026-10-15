namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回溯的<b>外锁契约</b>：回档期间禁输入 / 禁手牌 / 暂停队列。
/// </summary>
/// <remarks>
/// ① 职责：<c>IRewindLock</c>（锁）+ <c>IRewindValidator</c>（恢复后不变量校验）。
/// ② 调用时机：恢复前后。
/// ④ ⚠️ 见 README 的命名与注释口径；口径沿革以注释里带日期的那行为准。
/// </remarks>

public interface IRewindLock
{
    void Engage(string reason);

    void Release();
}

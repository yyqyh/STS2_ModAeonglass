namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>
/// 回档期的外锁：禁输入 / 禁手牌 / 暂停执行器与玩家队列。由 L2 实现。
/// </summary>
public interface IRewindLock
{
    void Engage(string reason);

    void Release();
}

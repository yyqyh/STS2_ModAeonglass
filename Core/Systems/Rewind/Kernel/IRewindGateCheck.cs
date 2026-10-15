namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>单项闸门。加一条判据 = 加一个实现并注册，不改其它闸门。</summary>
public interface IRewindGateCheck
{
    string Name { get; }

    GateCheck Check();
}

namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>恢复后不变量校验。由 L2 实现。</summary>
public interface IRewindValidator
{
    bool Validate(out string reason);
}

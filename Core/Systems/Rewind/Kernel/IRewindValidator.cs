namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>恢复后的不变量校验，由 <c>RewindValidator</c> 实现。</summary>
/// <remarks>
/// ① 职责：声明 <c>Validate(out reason)</c>。
/// ② 挂点与时机：回档恢复结束后由引擎调用；返回 false 即视为这次回档不合法。
/// ③ 依赖：无。
/// ④ ⚠️ 只做校验、不修状态。
/// </remarks>
public interface IRewindValidator
{
    bool Validate(out string reason);
}

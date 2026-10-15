namespace STS2_Aeonglass.Core.Systems.Rewind.Kernel;

/// <summary>单项闸门结果。</summary>
public readonly record struct GateCheck(string Name, bool Ok, string Reason)
{
    public string Display => Ok ? $"ok {Name}" : $"x  {Name}：{Reason}";
}

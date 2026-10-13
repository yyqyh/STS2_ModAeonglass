using MegaCrit.Sts2.Core.Entities.Players;

using STS2RitsuLib.Combat.SecondaryResources;

namespace STS2_Aeonglass.Core.Systems.SandResource;

/// <summary>M1 的<b>相位三档</b>（规则层 v1.1）：沙砾侧 / 平衡 / 空隙侧。</summary>
/// <remarks>
/// <para>
/// 判据只看<b>当前沙砾</b>（<c>G</c>）：<c>G ≥ 4</c> 沙砾侧、<c>G = 3</c> 平衡、<c>G ≤ 2</c> 空隙侧。
/// 容量 6、初始 3/3，而倒置是"两边对调" ⇒ <c>G = 3</c> 是唯一不动点。
/// </para>
/// <para>
/// 规则层口径：三档<b>不做成通用卡牌条件</b>，只做 <b>卡牌特化</b>（先在初始牌组试水）。
/// 所以这里是**唯一读口** —— 卡牌/差分牌要判相位一律调它，不要自己拿数字跟阈值比。
/// </para>
/// </remarks>
internal enum SandPhase
{
    /// <summary>沙砾侧：<c>G ≥ 4</c>。</summary>
    Sand,

    /// <summary>平衡：<c>G = 3</c>（倒置的不动点）。</summary>
    Balanced,

    /// <summary>空隙侧：<c>G ≤ 2</c>。</summary>
    Void,
}

/// <summary>相位三档的读口（<b>卡牌只准通过它判相位</b>）。</summary>
internal static class SandPhases
{
    /// <summary>沙砾侧的阈值：<c>G ≥ 4</c>。</summary>
    public const int SandThreshold = 4;

    /// <summary>空隙侧的阈值：<c>G ≤ 2</c>。</summary>
    public const int VoidThreshold = 2;

    /// <summary>按沙砾点数判相位。</summary>
    public static SandPhase Of(int sand)
        => sand >= SandThreshold ? SandPhase.Sand
            : sand <= VoidThreshold ? SandPhase.Void
            : SandPhase.Balanced;

    /// <summary>取某玩家当前的相位（拿不到资源时按初始值 3 算 = 平衡）。</summary>
    public static SandPhase Of(Player player)
        => Of(SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId));

    /// <summary>是不是沙砾侧。</summary>
    public static bool IsSandSide(Player player) => Of(player) == SandPhase.Sand;

    /// <summary>是不是平衡（<c>G = 3</c>）。</summary>
    public static bool IsBalanced(Player player) => Of(player) == SandPhase.Balanced;

    /// <summary>是不是空隙侧。</summary>
    public static bool IsVoidSide(Player player) => Of(player) == SandPhase.Void;

    /// <summary>给日志 / 面板用的一行字。</summary>
    public static string Describe(Player player)
        => $"{Of(player)}（沙砾 {SecondaryResourceCmd.Get(player, AeonglassSecondaryResources.SandId)}）";
}

using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Content.Relics;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 双卡组的状态读写单点：全部落在初始遗物「沙漏之心」的 <c>[SavedProperty]</c> 上。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么挂遗物</b>：本体的局内可存字段只认 <c>[SavedProperty]</c>，而 <c>CharacterModel</c>
/// 不在钩子分发里、也不随局内数据走（堕化拉撒路的实测结论；本项目的《工作约定》也记了这条）。
/// 遗物是这局真正持有的对象 —— 双卡组 只借它当"状态载体"，机制逻辑一律在 <c>Core/Systems/Decks/</c>。
/// </para>
/// <para>
/// 冷却是"主动切换"的节奏闸：每场战斗结束 +1，主动切换成功时清零。默认 0 = 无冷却（测试期）。
/// </para>
/// </remarks>
internal static class DeckState
{
    /// <summary>把冷却计数钳在这个上限内，免得长局里数字无限涨。</summary>
    private const int CooldownCap = 99;

    /// <summary>这一局的状态载体（找不到就返回 null —— 例如 双卡组 关掉、或遗物被移除）。</summary>
    public static AeonglassCore? RelicOf(Player player) =>
        player.Relics.OfType<AeonglassCore>().FirstOrDefault();

    /// <summary>当前生效的卡组（0 = A，1 = B）。没有载体时按 A 处理。</summary>
    public static int ActiveIndex(Player player) => RelicOf(player)?.ActiveDeckIndex ?? 0;

    /// <summary>写回当前生效的卡组。</summary>
    public static void SetActiveIndex(Player player, int index)
    {
        if (RelicOf(player) is { } relic)
        {
            relic.ActiveDeckIndex = Math.Clamp(index, 0, 1);
        }
    }

    /// <summary>非生效那一副是否已经播种过。</summary>
    public static bool DecksInitialized(Player player) => RelicOf(player)?.DecksInitialized ?? false;

    /// <summary>标记"已经播种过"。</summary>
    public static void MarkInitialized(Player player)
    {
        if (RelicOf(player) is { } relic)
        {
            relic.DecksInitialized = true;
        }
    }

    /// <summary>距上次主动切换过了几场战斗。</summary>
    public static int CombatsSinceManualSwitch(Player player) =>
        RelicOf(player)?.CombatsSinceManualSwitch ?? 0;

    /// <summary>每场战斗结束 +1（主动切换的冷却计时）。</summary>
    public static void NoteCombatEnded(Player player)
    {
        if (RelicOf(player) is { } relic && relic.CombatsSinceManualSwitch < CooldownCap)
        {
            relic.CombatsSinceManualSwitch++;
        }
    }

    /// <summary>主动切换成功 ⇒ 冷却清零。</summary>
    public static void NoteManualSwitch(Player player)
    {
        if (RelicOf(player) is { } relic)
        {
            relic.CombatsSinceManualSwitch = 0;
        }
    }
}

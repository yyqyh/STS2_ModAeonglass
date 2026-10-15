using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Content.Cards;

/// <summary>
/// 【测试期临时设施 · 可一键断开】把本体的「静默猎手」卡池借给永世沙漏。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要它</b>：永世沙漏自己的卡池现在只有 5 张牌（打击 / 防御 / 滞留 / 时间印记 / 沙砾刻度），
/// 卡牌奖励与商店根本凑不出可测的样本。测试期先把静默猎手的 91 张借过来凑数。
/// </para>
/// <para>
/// <b>怎么断开</b>（用户 2026-10-11：「之后结束测试，需要断掉这里」）—— 三选一即可：
/// ① 设置页 · 调试 · 关掉「借用静默猎手卡池」（<b>需重启游戏</b>，卡池列表只算一次并缓存）；
/// ② 删掉本文件 + <c>Core/Patches/SilentCardPoolBorrowPatch.cs</c>；
/// ③ 顺手删掉 <c>AeonglassSetting.DebugBorrowSilentCardPool</c> 与设置页那一项。
/// </para>
/// <para>
/// <b>作用范围</b>：只往 <see cref="AeonglassCardPool" /> 里追加成员；本体静默猎手自己的卡池一行不动，
/// 起始卡组也不动（改的只是「奖励 / 商店能发什么」）。
/// </para>
/// </remarks>
internal static class SilentCardPoolBorrow
{
    /// <summary>设置页里的开关键（文案在 <c>AeonglassModSettingsPage</c>）。</summary>
    public const string SettingKey = "debug_borrow_silent_pool";

    /// <summary>是否启用（设置页控制，默认开）。</summary>
    public static bool Enabled => AeonglassSettingStore.Current.DebugBorrowSilentCardPool;

    /// <summary>
    /// 合并结果缓存。
    /// </summary>
    /// <remarks>
    /// <c>CardPoolModel.AllCards</c> 会被反复读，缓存一份，免得每次访问都重新拼一遍列表。
    /// 用弱引用表是因为每次开局都会克隆出新的可变卡池实例，别把旧实例一直拽着。
    /// </remarks>
    private static readonly ConditionalWeakTable<CardPoolModel, List<CardModel>> Merged = new();

    /// <summary>这个卡池要不要借？（只认我们自己的卡池，且开关开着）</summary>
    public static bool ShouldApply(CardPoolModel pool) => pool is AeonglassCardPool && Enabled;

    /// <summary>把静默猎手的卡并进 <paramref name="original" />（按 id 去重，幂等）。</summary>
    public static IEnumerable<CardModel> Merge(CardPoolModel pool, IEnumerable<CardModel> original)
    {
        if (Merged.TryGetValue(pool, out var cached))
        {
            return cached;
        }

        var merged = original.ToList();
        var own = merged.Count;
        var seen = merged.Select(card => card.Id).ToHashSet();

        var borrowed = 0;
        foreach (var card in ModelDb.CardPool<SilentCardPool>().AllCards)
        {
            if (seen.Add(card.Id))
            {
                merged.Add(card);
                borrowed++;
            }
        }

        Merged.Add(pool, merged);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} [调试] 卡池借用：{pool.Title} 原 {own} 张 + 静默猎手 {borrowed} 张 = {merged.Count} 张"
            + "（关掉设置页的「借用静默猎手卡池」并重启游戏即可断开）");

        return merged;
    }
}

using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Content.Cards.Basic;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 第二副牌组（B）的**测试期**初始内容。
/// </summary>
/// <remarks>
/// <para>
/// 上级派单口径：双卡组 本轮只做"结构 + 切换机制"，两副各放几张**可观察**的测试卡够验证即可，
/// 规模（A + B ≈ 24–28，各 12–14）留到**阶段 ④ 卡池**。
/// </para>
/// <para>
/// 这里刻意用"一眼能认出是哪副"的组合：留 3 张基础牌，再塞 3 张**角色特色牌**
/// （滞留 / 时间印记 / 沙砾刻度）—— 只要看到手牌里有滞留或沙砾费图标，就知道当前生效的是 B。
/// </para>
/// <para>
/// 建卡走 <c>player.RunState.CreateCard&lt;T&gt;(player)</c>：与本体生成卡牌同一条路，
/// 只会登记进这一局，不会凭空进任何牌堆。
/// </para>
/// </remarks>
internal static class DeckBSeed
{
    /// <summary>
    /// 造出第二副牌组（用户 2026-10-13 口径：**初始卡只留 4 打击 + 4 防御**，与 A 副的基础牌一一对应）。
    /// </summary>
    /// <remarks>
    /// 这批牌是"共用池基础牌"：它们会被 <c>DeckPiles.LinkSeededTwins</c> 与 A 里**同名牌按顺序**配成孪生，
    /// 所以**这个清单就是"哪些牌是共用的"的定义**。要让某张牌进共用池，就加到这里（并保证 A 的起始牌组里也有一张）。
    /// </remarks>
    public static List<CardModel> Create(Player player)
    {
        var cards = new List<CardModel>();

        Add<AeonglassStrike>(player, cards, 4);
        Add<AeonglassDefend>(player, cards, 4);
        Add<AeonglassTide>(player, cards, 1);
        // 堤岸（2026-10-14 落地）：延宕管线（Core/Systems/Deferral）已就位 ⇒ 起始牌组补齐到 10 张。
        Add<AeonglassBank>(player, cards, 1);

        return cards;
    }

    /// <summary>一行摘要（日志用）。</summary>
    public static string Describe() => "打击×4 防御×4 潮汐×1 堤岸×1（共 10 张）";

    private static void Add<TCard>(Player player, List<CardModel> cards, int count)
        where TCard : CardModel
    {
        for (var i = 0; i < count; i++)
        {
            cards.Add(player.RunState.CreateCard<TCard>(player));
        }
    }
}

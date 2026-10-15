using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>两副基础牌的<b>孪生实例</b>：链接关系与"改一份、另一份跟着变"的镜像。</summary>
/// <remarks>
/// ① 职责：<c>Link</c> / <c>TwinOf</c> / <c>IsLinked</c> / <c>Describe</c>；各条镜像动作在 <c>DeckTwinMirrors</c>。
/// ② 挂点与时机：<c>DeckSeeding</c> 建牌时链接；镜像出口挂在原版"改升级等级"的属性 setter（<c>DeckTwinMirrorPatch</c>）。
/// ③ 依赖：<c>CardModel.CurrentUpgradeLevel</c>、<c>DeckPiles.DeckIndexOf</c>（差分判定）、<c>DeckAddHooks</c>。
/// ④ ⚠️ 两副共用同一批基础牌、但<b>不能是同一个对象</b>：差分判定看 <c>DeckVersion</c> 指向的母本躺在哪一副，同一对象会让判定失效。
/// 镜像只有一个 choke point（升级 setter）：火堆 / 商店 / 事件 / "升级全部攻击牌"全覆盖；
/// 附魔、变换、复制三条路还没接（它们不走 upgrade setter，等卡表落地再接，位置已留）。
/// </remarks>
internal static class DeckTwins
{
    /// <summary>孪生对照表（双向：a→b 与 b→a 都在里面）。</summary>
    private static readonly Dictionary<CardModel, CardModel> Pairs = [];

    /// <summary>镜像进行中（防止"镜像 → 触发 setter → 再镜像回去"的回环）。</summary>
    internal static bool Mirroring;

    /// <summary>配一对孪生（双向登记）。</summary>
    public static void Link(CardModel a, CardModel b)
    {
        Pairs[a] = b;
        Pairs[b] = a;
    }

    /// <summary>这张牌的孪生（没配对返回 null）。</summary>
    public static CardModel? TwinOf(CardModel card) => Pairs.TryGetValue(card, out var twin) ? twin : null;

    /// <summary>这张牌有没有孪生。</summary>
    public static bool IsLinked(CardModel card) => Pairs.ContainsKey(card);

    /// <summary>拆掉一对孪生（删牌 / 变换时用）。</summary>
    public static void Unlink(CardModel card)
    {
        if (Pairs.Remove(card, out var twin))
        {
            Pairs.Remove(twin);
        }
    }

    /// <summary>清空（换 run / 调试用）。</summary>
    public static void Clear() => Pairs.Clear();

    /// <summary>配了对的孪生有几对。</summary>
    public static int Count => Pairs.Count / 2;

    /// <summary>一行状态（面板 / 日志用）。</summary>
    public static string Describe()
        => Pairs.Count == 0
            ? "孪生：还没有配对的牌"
            : $"孪生：{Count} 对（例：{Pairs.First().Key.Id.Entry} ↔ {Pairs.First().Value.Id.Entry}）";

    /// <summary>两个孪生现在是不是分处两副（正常应为"分处两副"）。</summary>
    internal static string DescribePlacement(CardModel a, CardModel b)
        => ReferenceEquals(a.Pile, b.Pile) ? "⚠ 同一牌堆（异常）" : "分处两副 ✔";

    // ============================ 附魔镜像（P3-3 的第二条路）============================
    // 附魔不走 `UpgradeInternal`，它有自己的单点：`CardModel.EnchantInternal(...)` /
    // `ClearEnchantmentInternal()`（本体的 `CardCmd.Enchant<T>` / `ClearEnchantment` 都走它们）。

    // ============================ 删除 / 变换镜像（P3-3 的第三条路）============================
    // 本体所有"把一张牌从牌堆里拿掉"都走 `CardPile.RemoveInternal(card, silent)`（商店删牌、事件删牌、
    // 变换的前半"删一张"、清空牌堆…）。只对**牌组**这一层的离开做镜像 —— 手牌 / 抽牌堆的走动不该带走孪生。

    /// <summary>这个牌堆是不是"牌组"那一层（生效副 = <c>player.Deck</c>，另一副 = 模组牌堆）。</summary>
    internal static bool IsDeckPile(CardPile pile, Player player)
        => pile.Type == PileType.Deck || ReferenceEquals(pile, DeckPiles.InactivePileOf(player));
}

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>两副基础牌的<b>孪生实例 + 状态镜像</b>（执行计划 Step 4 · P3-2 / P3-3）。</summary>
/// <remarks>
/// <para>
/// <b>需求</b>：两副共用同一批基础牌 ⇒ 敲牌 / 商店删牌 / "升级全部攻击牌" / 附魔 / 变换 / 复制，
/// 任一路径改了其中一份，另一份必须跟着变。而且**不能是同一个对象** ——
/// 本体的差分判定（<see cref="DeckPiles.DeckIndexOf" />）看的是 <c>DeckVersion</c> 指向的母本躺在哪一副，
/// 同一个对象会让判定直接失效。
/// </para>
/// <para>
/// <b>单一 choke point</b>：出口挂在原版"改升级等级"的**那一个属性 setter**
/// （<c>CardModel.CurrentUpgradeLevel</c>，见 <see cref="Core.Patches.DeckTwinMirrorPatch" />）。
/// 火堆升级、商店、事件、"升级全部攻击牌"全都走它 ⇒ 不需要给每个入口各写一遍镜像。
/// </para>
/// <para>
/// ⚠️ <b>还没接的三条路</b>（它们不走 upgrade setter，等卡表落地、有真卡可对照时再接）：
/// ① <b>附魔</b>（<c>CardCmd</c> 的附魔入口）；② <b>变换</b>（删一张 + 生成一张，靠"加牌"钩子覆盖一半）；
/// ③ <b>复制</b>（= 加牌，走 <see cref="DeckAddHooks" />）。三条都留了位置，见方案文档待办。
/// </para>
/// </remarks>
internal static class DeckTwins
{
    /// <summary>孪生对照表（双向：a→b 与 b→a 都在里面）。</summary>
    private static readonly Dictionary<CardModel, CardModel> Pairs = [];

    /// <summary>镜像进行中（防止"镜像 → 触发 setter → 再镜像回去"的回环）。</summary>
    private static bool _mirroring;

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

    /// <summary>
    /// <b>choke point 的镜像动作（升级）</b>：某张牌升级了 ⇒ 把孪生也升到同一级。
    /// </summary>
    /// <remarks>
    /// 由 <c>CardModel.UpgradeInternal()</c> 的 postfix 调用（本体所有升级入口都走它）。
    /// 幂等 + 防回环（<c>_mirroring</c>）：孪生自己再触发一次 patch 时也不会镜像回去。
    /// </remarks>
    public static void MirrorUpgrade(CardModel card)
    {
        if (_mirroring)
        {
            return;
        }

        if (TwinOf(card) is not { } twin || twin.CurrentUpgradeLevel >= card.CurrentUpgradeLevel)
        {
            return;
        }

        if (!twin.IsUpgradable)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像跳过：{twin.Id.Entry} 已满级（上限 {twin.MaxUpgradeLevel}）");
            return;
        }

        _mirroring = true;
        try
        {
            twin.UpgradeInternal();
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像：{card.Id.Entry} +{card.CurrentUpgradeLevel} ⇒ "
                + $"{twin.Id.Entry} 同步到 +{twin.CurrentUpgradeLevel}（{DescribePlacement(card, twin)}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像失败（{twin.Id.Entry}）：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _mirroring = false;
        }
    }

    /// <summary>镜像动作（降级）：某张牌被降回 0 级 ⇒ 孪生也降回去。</summary>
    public static void MirrorDowngrade(CardModel card)
    {
        if (_mirroring)
        {
            return;
        }

        if (TwinOf(card) is not { } twin || twin.CurrentUpgradeLevel == 0)
        {
            return;
        }

        _mirroring = true;
        try
        {
            twin.DowngradeInternal();
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像（降级）：{card.Id.Entry} 回 0 级 ⇒ {twin.Id.Entry} 同步");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生降级镜像失败（{twin.Id.Entry}）：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _mirroring = false;
        }
    }

    /// <summary>一行状态（面板 / 日志用）。</summary>
    public static string Describe()
        => Pairs.Count == 0
            ? "孪生：还没有配对的牌"
            : $"孪生：{Count} 对（例：{Pairs.First().Key.Id.Entry} ↔ {Pairs.First().Value.Id.Entry}）";

    /// <summary>两个孪生现在是不是分处两副（正常应为"分处两副"）。</summary>
    private static string DescribePlacement(CardModel a, CardModel b)
        => ReferenceEquals(a.Pile, b.Pile) ? "⚠ 同一牌堆（异常）" : "分处两副 ✔";

    // ============================ 附魔镜像（P3-3 的第二条路）============================
    // 附魔不走 `UpgradeInternal`，它有自己的单点：`CardModel.EnchantInternal(...)` /
    // `ClearEnchantmentInternal()`（本体的 `CardCmd.Enchant<T>` / `ClearEnchantment` 都走它们）。

    /// <summary><b>镜像动作（附魔）</b>：某张牌被附魔 ⇒ 给孪生附同一个附魔（克隆一份，不共享实例）。</summary>
    public static void MirrorEnchant(CardModel card)
    {
        if (_mirroring)
        {
            return;
        }

        if (TwinOf(card) is not { } twin || card.Enchantment is not { } enchantment)
        {
            return;
        }

        if (twin.Enchantment is not null)
        {
            // 已经有附魔了：不叠加（本体的同名额外交互由它自己管），只记一笔。
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 孪生附魔跳过：{twin.Id.Entry} 已经有附魔");
            return;
        }

        _mirroring = true;
        try
        {
            // 照本体自己的做法：附魔实例要**克隆**（同一个模型实例不能挂在两张卡上）。
            var clone = (EnchantmentModel)enchantment.ClonePreservingMutability();
            twin.EnchantInternal(clone, enchantment.Amount);
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像（附魔）：{card.Id.Entry} ← {enchantment.Id.Entry}"
                + $" ⇒ {twin.Id.Entry} 同步（{DescribePlacement(card, twin)}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生附魔镜像失败（{twin.Id.Entry}）：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _mirroring = false;
        }
    }

    /// <summary><b>镜像动作（清附魔）</b>：某张牌的附魔被清掉 ⇒ 孪生也清掉。</summary>
    public static void MirrorClearEnchant(CardModel card)
    {
        if (_mirroring || TwinOf(card) is not { } twin || twin.Enchantment is null)
        {
            return;
        }

        _mirroring = true;
        try
        {
            twin.ClearEnchantmentInternal();
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像（清附魔）：{card.Id.Entry} ⇒ {twin.Id.Entry} 同步");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生清附魔镜像失败（{twin.Id.Entry}）：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _mirroring = false;
        }
    }

    // ============================ 删除 / 变换镜像（P3-3 的第三条路）============================
    // 本体所有"把一张牌从牌堆里拿掉"都走 `CardPile.RemoveInternal(card, silent)`（商店删牌、事件删牌、
    // 变换的前半"删一张"、清空牌堆…）。只对**牌组**这一层的离开做镜像 —— 手牌 / 抽牌堆的走动不该带走孪生。

    /// <summary><b>镜像动作（离开牌堆）</b>：某张孪生离开了"牌组"⇒ 把另一副里的那一份也移出牌组。</summary>
    public static void MirrorRemoval(CardPile pile, CardModel card)
    {
        if (_mirroring)
        {
            return;
        }

        if (TwinOf(card) is not { } twin || card.Owner is not { } player || !IsDeckPile(pile, player))
        {
            return;
        }

        if (twin.Pile is not { } twinPile || !twinPile.Cards.Contains(twin))
        {
            // 孪生已经不在牌堆里（例如它自己先被移走了）⇒ 只把配对拆掉。
            Unlink(card);
            return;
        }

        _mirroring = true;
        try
        {
            twinPile.RemoveInternal(twin, true);
            Unlink(card);
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像（删除）：{card.Id.Entry} 离开牌组 ⇒ "
                + $"{twin.Id.Entry} 也从另一副移出（配对已拆）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生删除镜像失败（{twin.Id.Entry}）：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _mirroring = false;
        }
    }

    /// <summary>这个牌堆是不是"牌组"那一层（生效副 = <c>player.Deck</c>，另一副 = 模组牌堆）。</summary>
    private static bool IsDeckPile(CardPile pile, Player player)
        => pile.Type == PileType.Deck || ReferenceEquals(pile, DeckPiles.InactivePileOf(player));
}

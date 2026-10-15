using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 双卡组的<b>孪生镜像</b>：一处改动（升级 / 附魔 / 移除）同步到另一副的同名牌。
/// </summary>
/// <remarks>
/// ① 职责：把对母本的改动镜像给孪生（含"降级/清附魔"的反向同步）。
/// ② 调用时机：卡牌升级/附魔/被移除时由对应系统调。
/// ③ 依赖：<c>DeckTwins</c>（配对表）+ <c>DeckPiles</c>（判断是不是牌组堆）。
/// ④ ⚠️ 镜像期间带防重入标记，别在回调里再改同一对牌。
/// </remarks>
internal static class DeckTwinMirrors
{

    /// <summary>
    /// <b>choke point 的镜像动作（升级）</b>：某张牌升级了 ⇒ 把孪生也升到同一级。
    /// </summary>
    /// <remarks>
    /// 由 <c>CardModel.UpgradeInternal()</c> 的 postfix 调用（本体所有升级入口都走它）。
    /// 幂等 + 防回环（<c>DeckTwins.Mirroring</c>）：孪生自己再触发一次 patch 时也不会镜像回去。
    /// </remarks>
    public static void MirrorUpgrade(CardModel card)
    {
        if (DeckTwins.Mirroring)
        {
            return;
        }

        if (DeckTwins.TwinOf(card) is not { } twin || twin.CurrentUpgradeLevel >= card.CurrentUpgradeLevel)
        {
            return;
        }

        if (!twin.IsUpgradable)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像跳过：{twin.Id.Entry} 已满级（上限 {twin.MaxUpgradeLevel}）");
            return;
        }

        DeckTwins.Mirroring = true;
        try
        {
            twin.UpgradeInternal();
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像：{card.Id.Entry} +{card.CurrentUpgradeLevel} ⇒ "
                + $"{twin.Id.Entry} 同步到 +{twin.CurrentUpgradeLevel}（{DeckTwins.DescribePlacement(card, twin)}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像失败（{twin.Id.Entry}）：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            DeckTwins.Mirroring = false;
        }
    }

    /// <summary>镜像动作（降级）：某张牌被降回 0 级 ⇒ 孪生也降回去。</summary>
    public static void MirrorDowngrade(CardModel card)
    {
        if (DeckTwins.Mirroring)
        {
            return;
        }

        if (DeckTwins.TwinOf(card) is not { } twin || twin.CurrentUpgradeLevel == 0)
        {
            return;
        }

        DeckTwins.Mirroring = true;
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
            DeckTwins.Mirroring = false;
        }
    }

    /// <summary><b>镜像动作（附魔）</b>：某张牌被附魔 ⇒ 给孪生附同一个附魔（克隆一份，不共享实例）。</summary>
    public static void MirrorEnchant(CardModel card)
    {
        if (DeckTwins.Mirroring)
        {
            return;
        }

        if (DeckTwins.TwinOf(card) is not { } twin || card.Enchantment is not { } enchantment)
        {
            return;
        }

        if (twin.Enchantment is not null)
        {
            // 已经有附魔了：不叠加（本体的同名额外交互由它自己管），只记一笔。
            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 孪生附魔跳过：{twin.Id.Entry} 已经有附魔");
            return;
        }

        DeckTwins.Mirroring = true;
        try
        {
            // 照本体自己的做法：附魔实例要**克隆**（同一个模型实例不能挂在两张卡上）。
            var clone = (EnchantmentModel)enchantment.ClonePreservingMutability();
            twin.EnchantInternal(clone, enchantment.Amount);
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像（附魔）：{card.Id.Entry} ← {enchantment.Id.Entry}"
                + $" ⇒ {twin.Id.Entry} 同步（{DeckTwins.DescribePlacement(card, twin)}）");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生附魔镜像失败（{twin.Id.Entry}）：{ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            DeckTwins.Mirroring = false;
        }
    }

    /// <summary><b>镜像动作（清附魔）</b>：某张牌的附魔被清掉 ⇒ 孪生也清掉。</summary>
    public static void MirrorClearEnchant(CardModel card)
    {
        if (DeckTwins.Mirroring || DeckTwins.TwinOf(card) is not { } twin || twin.Enchantment is null)
        {
            return;
        }

        DeckTwins.Mirroring = true;
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
            DeckTwins.Mirroring = false;
        }
    }

    /// <summary><b>镜像动作（离开牌堆）</b>：某张孪生离开了"牌组"⇒ 把另一副里的那一份也移出牌组。</summary>
    public static void MirrorRemoval(CardPile pile, CardModel card)
    {
        if (DeckTwins.Mirroring)
        {
            return;
        }

        if (DeckTwins.TwinOf(card) is not { } twin || card.Owner is not { } player || !DeckTwins.IsDeckPile(pile, player))
        {
            return;
        }

        if (twin.Pile is not { } twinPile || !twinPile.Cards.Contains(twin))
        {
            // 孪生已经不在牌堆里（例如它自己先被移走了）⇒ 只把配对拆掉。
            DeckTwins.Unlink(card);
            return;
        }

        DeckTwins.Mirroring = true;
        try
        {
            twinPile.RemoveInternal(twin, true);
            DeckTwins.Unlink(card);
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
            DeckTwins.Mirroring = false;
        }
    }
}

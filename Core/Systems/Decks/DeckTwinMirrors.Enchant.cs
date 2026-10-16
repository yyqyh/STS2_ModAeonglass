using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>孪生镜像的<b>附魔 / 清附魔 / 离组</b>分册（主文件 = 事件入口 + 升级/降级）。</summary>
/// <remarks>
/// ① 职责：MirrorEnchant / MirrorClearEnchant / MirrorRemoval。
/// ② 挂点与时机：OnEnchantmentChanged（附魔事件）与 DeckTwins 的 CardRemoved 订阅（离组）。
/// ③ 依赖：DeckTwins（配对表 + Mirroring 防重入）、DeckPiles。
/// ④ ⚠️ 只由事件回调触发；异常由同目录主文件的 Safe 兜住，绝不允许抛回本体。
/// </remarks>
internal static partial class DeckTwinMirrors
{
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

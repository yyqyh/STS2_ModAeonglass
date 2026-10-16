using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 双卡组的<b>孪生镜像</b>：一处改动（升级 / 附魔 / 移除）同步到另一副的同名牌。
/// </summary>
/// <remarks>
/// ① 职责：把对母本的改动镜像给孪生（含"降级/清附魔"的反向同步）。
/// ② 挂点与时机（2026-10-16 起**事件驱动**）：<c>DeckTwins</c> 订阅 <c>CardModel.Upgraded</c> /
///    <c>CardModel.EnchantmentChanged</c> / <c>CardPile.CardRemoved</c>，回调进这里的
///    <c>OnUpgradeChanged</c> / <c>OnEnchantmentChanged</c> / <c>MirrorRemoval</c>。
/// ③ 依赖：<c>DeckTwins</c>（配对表 + 订阅）+ <c>DeckPiles</c>（判断是不是牌组堆）。
/// ④ ⚠️ 事件在**本体方法体内同步触发** ⇒ 所有入口必须包在 <see cref="Safe" /> 里，异常绝不允许抛回本体；
///    镜像期间带防重入标记（<c>DeckTwins.Mirroring</c>），别在回调里再改同一对牌。
/// </remarks>
internal static partial class DeckTwinMirrors
{

    /// <summary>
    /// 事件回调的共用兜底：异常只记 <c>[双卡组]</c> Warn。
    /// </summary>
    /// <remarks>
    /// 本体在 <c>UpgradeInternal</c> / <c>DowngradeInternal</c> / <c>EnchantInternal</c> /
    /// <c>ClearEnchantmentInternal</c> 的末尾<b>同步</b>发事件、在 <c>CardPile.RemoveInternal</c> 里同步发
    /// <c>CardRemoved</c> ⇒ 我们抛异常会打断本体的升级 / 移除流程，并连带影响原版与其他 mod。
    /// </remarks>
    internal static void Safe(string what, Action mirror)
    {
        try
        {
            mirror();
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 孪生镜像异常（{what}，已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// <b>事件入口（升级 / 降级）</b>：本体在升级与降级的末尾发的是**同一个** <c>Upgraded</c>，
    /// 所以方向看状态：等级归 0 = 降级（本体就是这么实现的），否则 = 升级。
    /// </summary>
    internal static void OnUpgradeChanged(CardModel card)
    {
        if (card.CurrentUpgradeLevel == 0)
        {
            MirrorDowngrade(card);
            return;
        }

        MirrorUpgrade(card);
    }

    /// <summary>
    /// <b>事件入口（附魔 / 清附魔）</b>：<c>EnchantmentChanged</c> 两个方向共用，方向看"现在有没有附魔"。
    /// </summary>
    internal static void OnEnchantmentChanged(CardModel card)
    {
        if (card.Enchantment is null)
        {
            MirrorClearEnchant(card);
            return;
        }

        MirrorEnchant(card);
    }

    /// <summary>
    /// <b>choke point 的镜像动作（升级）</b>：某张牌升级了 ⇒ 把孪生也升到同一级。
    /// </summary>
    /// <remarks>
    /// 由 <c>CardModel.Upgraded</c> 事件调（本体所有升级入口都走 <c>UpgradeInternal</c> ⇒ 都发这个事件）。
    /// 幂等 + 防回环（<c>DeckTwins.Mirroring</c>）：孪生自己再触发一次事件时也不会镜像回去。
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

}

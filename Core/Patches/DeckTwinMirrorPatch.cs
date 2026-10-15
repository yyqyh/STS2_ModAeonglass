using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// 孪生镜像的<b>单一 choke point</b>：本体的"改一张牌的升级等级"。
/// </summary>
/// <remarks>
/// <para>
/// 本体把"升级一张牌"收在<b>一个方法</b>上：<c>CardModel.UpgradeInternal()</c>
/// （注释原话："Upgrade this card. This does not run any hooks."），
/// 火堆升级 / 商店 / 事件 / "升级全部攻击牌" 全都经由 <c>CardCmd.Upgrade(...)</c> 走到它 ——
/// 所以镜像挂在这里就等于覆盖了所有入口，不需要给每个玩法各写一遍。降级同理（<c>DowngradeInternal</c>）。
/// </para>
/// <para>
/// 只挂 postfix、不改原行为；对照表里没有孪生的牌（别的角色、临时牌）直接返回 ⇒ 对本体零影响。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.UpgradeInternal))]
internal static class DeckTwinMirrorPatch
{
    [HarmonyPostfix]
    private static void UpgradePostfix(CardModel __instance) => DeckTwinMirrors.MirrorUpgrade(__instance);
}

/// <summary>孪生镜像：降级（变换 / 重置）时把孪生一起降回去。</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.DowngradeInternal))]
internal static class DeckTwinDowngradePatch
{
    [HarmonyPostfix]
    private static void DowngradePostfix(CardModel __instance) => DeckTwinMirrors.MirrorDowngrade(__instance);
}

/// <summary>孪生镜像：<b>附魔</b>（本体的所有附魔入口都走 <c>EnchantInternal</c>）。</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.EnchantInternal))]
internal static class DeckTwinEnchantPatch
{
    [HarmonyPostfix]
    private static void EnchantPostfix(CardModel __instance) => DeckTwinMirrors.MirrorEnchant(__instance);
}

/// <summary>孪生镜像：<b>清附魔</b>。</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.ClearEnchantmentInternal))]
internal static class DeckTwinClearEnchantPatch
{
    [HarmonyPostfix]
    private static void ClearEnchantPostfix(CardModel __instance) => DeckTwinMirrors.MirrorClearEnchant(__instance);
}

/// <summary>孪生镜像：<b>离开牌组</b>（删牌 / 变换的"删一张" / 清空牌堆）。</summary>
/// <remarks>
/// <c>CardPile.RemoveInternal</c> 是本体"把一张牌从某个牌堆拿走"的**唯一底层入口**
/// （<c>Clear()</c> 自己也调它）⇒ 删牌、变换的前半、事件删牌全都能被它捞到。
/// 镜像只在"牌组那一层"生效（见 <c>DeckTwins.IsDeckPile</c>），手牌 / 抽牌堆的走动不受影响。
/// </remarks>
[HarmonyPatch(typeof(CardPile), nameof(CardPile.RemoveInternal))]
internal static class DeckTwinRemovalPatch
{
    [HarmonyPostfix]
    private static void RemovePostfix(CardPile __instance, CardModel card) => DeckTwinMirrors.MirrorRemoval(__instance, card);
}

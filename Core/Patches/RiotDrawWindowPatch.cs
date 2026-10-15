using HarmonyLib;

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Systems.Riot;

namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// <b>乱流 · 抽取窗口</b>：包住 <c>CardPileCmd.Draw</c> 返回的 <c>Task</c>，给"正在抽牌"这件事
/// 一个明确的起止 ⇒ <see cref="RiotSwap" /> 在这段时间里<b>冻结抽弃对调的状态</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要它</b>：本体 <c>DrawInternal</c> 是
/// <c>CardPile drawPile = PileType.Draw.GetPile(player);</c> 只拿一次局部变量，但每抽一张都会先
/// <c>ShuffleIfNecessary</c> 重新按类型取堆。抽到最后一张（例如乱流本身）时如果状态被切回去，
/// 就会出现"牌其实在另一张堆里、而局部变量抱住的这张已经空了 ⇒ 不触发洗牌 ⇒ 断抽"。
/// ⇒ 抽取期间不切换，抽完再判定。
/// </para>
/// <para>
/// 只包"抽牌"这一个窗口，不碰 <c>Shuffle</c>：洗牌是先把要洗的牌抓成列表再一次性搬，中途切换不会错位。
/// </para>
/// <para>
/// 挂的是 4 参数重载（<c>Draw(PlayerChoiceContext, decimal, Player, bool)</c>）：本体所有抽牌
/// （含 2 参数重载、<c>DrawWithoutBlockingOnOtherPlayers</c>、回合开始的固定抽牌）都收敛到它。
/// </para>
/// </remarks>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.Draw),
    new[] { typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool) })]
internal static class RiotDrawWindowPatch
{
    [HarmonyPrefix]
    private static void Prefix(Player player) => RiotDrawWindow.EnterDraw(player);

    [HarmonyPostfix]
    private static void Postfix(Player player, ref Task<IEnumerable<CardModel>> __result)
        => __result = RiotDrawWindow.TrackDraw(player, __result);
}

/// <summary>
/// 同上：<c>AutoPlayFromDrawPile</c> 也是"局部变量抱住一张堆 + 每张重新 <c>ShuffleIfNecessary</c>"
/// 的写法（本体给遗物/能力用的"从抽牌堆自动打出 N 张"），中途切换同样会错位 ⇒ 一起冻住。
/// </summary>
[HarmonyPatch(typeof(CardPileCmd), nameof(CardPileCmd.AutoPlayFromDrawPile),
    new[]
    {
        typeof(PlayerChoiceContext), typeof(Player), typeof(int), typeof(CardPilePosition), typeof(bool),
    })]
internal static class RiotAutoPlayWindowPatch
{
    [HarmonyPrefix]
    private static void Prefix(Player player) => RiotDrawWindow.EnterDraw(player);

    [HarmonyPostfix]
    private static void Postfix(Player player, ref Task __result)
        => __result = RiotDrawWindow.TrackDraw(player, __result);
}

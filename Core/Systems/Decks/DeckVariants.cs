using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>这张牌此刻属于哪一副（0 = A / 1 = B）。</summary>
public enum DeckSide
{
    /// <summary>起始那一副。</summary>
    A = 0,

    /// <summary>第二副。</summary>
    B = 1,
}

/// <summary>差分块执行时要用的上下文（卡、出牌、玩家、出牌上下文）。</summary>
public readonly record struct DeckEffectContext(
    Player Player,
    CardModel Card,
    CardPlay Play,
    PlayerChoiceContext ChoiceContext);

/// <summary>
/// 一个<b>效果块</b>：差分牌按副返回的、可组合的效果段。
/// </summary>
/// <param name="Key">稳定 id（日志 / 去重 / 将来卡表对文案都用它），例如 <c>gain-sand-1</c>。</param>
/// <param name="Apply">真正执行这一段效果。</param>
/// <remarks>
/// 规则层 P3 的口径是"**机制差分**"：同一张牌在两副返回不同的**效果段**（不是只换数字）——
/// 所以这里返回的是"块"，由卡自己决定挂哪些、以什么顺序执行。
/// </remarks>
public sealed record DeckEffectBlock(string Key, Func<DeckEffectContext, Task> Apply);

/// <summary>差分牌：同一张卡在两副下给出不同的效果块与文案 stem。</summary>
/// <remarks>
/// 参考对象是小丑 mod 的"道途卡"（同一张卡按"道途遗物"换效果与文案）：形状一致 ——
/// **卡自己读状态、自己拼效果段与文本**，机制侧只提供"我在哪一副"这一个判定单点。
/// </remarks>
public interface IDeckVariantCard
{
    /// <summary>当前这副下的差分效果块（按顺序执行；空数组 = 这一副没有额外效果）。</summary>
    IReadOnlyList<DeckEffectBlock> VariantBlocks(DeckSide side, DeckEffectContext context);

    /// <summary>
    /// 当前这副对应的**文案 stem**（卡表落地后由它拼本地化键，例如 <c>{Id}.variant.{stem}</c>）。
    /// 现在只用于日志 / 面板自检。
    /// </summary>
    string? VariantTextStem(DeckSide side);
}

/// <summary>差分的执行口：判定副 → 取块 → 跑块。</summary>
public static class DeckVariants
{
    /// <summary>这张卡此刻属于哪一副（判定单点在 <see cref="DoubleDeckService.DeckIndexOf" />）。</summary>
    public static DeckSide SideOf(Player player, CardModel card)
        => (DeckSide)DoubleDeckService.DeckIndexOf(card, player);

    /// <summary>取这张卡在当前副下的差分块（不是差分牌就返回空）。</summary>
    public static IReadOnlyList<DeckEffectBlock> BlocksOf(DeckEffectContext context)
        => context.Card is IDeckVariantCard variant
            ? variant.VariantBlocks(SideOf(context.Player, context.Card), context) ?? []
            : [];

    /// <summary>取当前副对应的文案 stem（不是差分牌返回 null）。</summary>
    public static string? TextStemOf(Player player, CardModel card)
        => card is IDeckVariantCard variant ? variant.VariantTextStem(SideOf(player, card)) : null;

    /// <summary>
    /// 跑差分块（**通用效果由卡自己先跑**，这里只跑"因副而异"的那部分）。
    /// </summary>
    /// <remarks>块抛异常就停下并如实打日志：宁可这一块不生效，也不要让出牌流程带崩。</remarks>
    public static async Task PlayVariantAsync(DeckEffectContext context)
    {
        var side = SideOf(context.Player, context.Card);
        var blocks = BlocksOf(context);
        if (blocks.Count == 0)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Decks} 差分：{context.Card.Id.Entry} 在 {side} 副无额外块");
            return;
        }

        var ran = new List<string>();
        foreach (var block in blocks)
        {
            try
            {
                await block.Apply(context);
                ran.Add(block.Key);
            }
            catch (Exception ex)
            {
                Const.Logger.Error(
                    $"[{Const.ModId}] {Const.Prefix.Decks} 差分块 {block.Key} 执行失败"
                    + $"（{context.Card.Id.Entry} @ {side}）：{ex.GetType().Name}: {ex.Message}");
                break;
            }
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Decks} 差分：{context.Card.Id.Entry} @ {side} 副"
            + $"（stem={TextStemOf(context.Player, context.Card) ?? "-"}）跑了 {string.Join(" + ", ran)}");
    }
}

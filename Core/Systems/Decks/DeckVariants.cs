using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 差分（永时副 / 消逝副）的<b>执行口</b>：判定副 → 取块 → 跑块；<c>DeckSide</c>/<c>IDeckVariantCard</c> 也在这里。
/// </summary>
/// <remarks>
/// ① 职责：<c>SideOf</c>（判定单点）/ <c>BlocksOf</c> / <c>PlayVariantAsync</c> / 文案注入。
/// ② 调用时机：差分卡打出与卡面渲染。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

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
    /// <summary>这张卡是否真的带差分（非差分卡即使实现了接口也应返回 false）。</summary>
    bool HasVariant => true;

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
    /// <summary>这张卡此刻属于哪一副（判定单点在 <see cref="DeckPiles.DeckIndexOf" />）。</summary>
    public static DeckSide SideOf(Player player, CardModel card)
        => (DeckSide)DeckPiles.DeckIndexOf(card, player);

    /// <summary>取这张卡在当前副下的差分块（不是差分牌就返回空）。</summary>
    public static IReadOnlyList<DeckEffectBlock> BlocksOf(DeckEffectContext context)
        => context.Card is IDeckVariantCard { HasVariant: true } variant
            ? variant.VariantBlocks(SideOf(context.Player, context.Card), context) ?? []
            : [];

    /// <summary>取当前副对应的文案 stem（不是差分牌返回 null）。</summary>
    public static string? TextStemOf(Player player, CardModel card)
        => card is IDeckVariantCard { HasVariant: true } variant
            ? variant.VariantTextStem(SideOf(player, card))
            : null;

    /// <summary>差分文案在 <c>cards</c> 表里的键后缀：<c>&lt;卡id&gt;.variant.a</c> / <c>.variant.b</c>。</summary>
    private const string VariantKeySuffix = ".variant.";

    /// <summary>
    /// 把"当前这副"的整段差分文案注入卡面（卡的描述写成 <c>…{Variant}</c> 即可）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>为什么用注入</b>：本体的 <c>CardModel.Description</c> 不是 virtual，改不了；
    /// 而 <c>AddExtraArgsToDescription(LocString)</c> 是 protected virtual —— 官方就是用它往卡面塞动态内容的。
    /// </para>
    /// <para>
    /// <b>占位符</b>：注入进去的文本不会被本体再解析一次 ⇒ 我们自己把 <c>{名字}</c> / <c>{名字:diff()}</c>
    /// 解析成该卡 <c>DynamicVars</c> 的**当前值**再注入（见 <see cref="ResolvePlaceholders" />）。
    /// 所以变体段可以照常写 <c>{VariantDamage}</c> 这类占位符，升级后数字会跟着变。
    /// </para>
    /// <para>取不到条目 / 该副就是基础效果时注入空串（卡面不会露出 <c>{Variant}</c>）。</para>
    /// </remarks>
    public static void InjectVariantText(LocString description, CardModel card)
    {
        if (card is IDeckVariantCard { HasVariant: false })
        {
            return;
        }

        // ⚠️ 图鉴 / 卡池预览拿到的这张卡是 **canonical 实例**，在它上面**读 Owner 会直接抛**
        // （`CanonicalModelException: Canonical model ... used in incorrect place`，get_Owner 里就 AssertMutable）。
        // 实测：图鉴网格 `NCardLibraryGrid.InitGrid` 会因此整块初始化失败，表现是"图鉴打不开 / 卡片数还少"。
        // 所以先问 `IsCanonical`（安全），再决定走哪条路。
        // 没有归属玩家 ⇒ 没有"当前在哪一副"这回事 ⇒ 两副的文案一起注入
        // （用户 2026-10-14：图鉴里要么同时显示两副效果、要么只显示一端）。
        if (card.IsCanonical || card.Owner is not { } owner)
        {
            description.Add("Variant", ReadAllVariants(card, description));
            return;
        }

        var side = SideOf(owner, card);
        var key = card.Id.Entry + VariantKeySuffix + (side == DeckSide.B ? "b" : "a");

        try
        {
            // 用 LocString 自己格式化：把这张卡的 DynamicVars 作为参数传进去，
            // 这样 {Damage:diff()} / {VariantWither:diff()} 会走本体的 diff formatter，
            // 升级预览时保留绿色/红色，而不是被替换成死数字。
            // ⚠️ 必须复用**主描述已经装好的变量表**（DynamicVars + IfUpgraded + energyPrefix …）：
            // 差分文本是二级 LocString，本体不会再替它解析一次；只塞 DynamicVars 时，
            // `{IfUpgraded:…}` / 能量图标这类占位符会原样露出，升级预览的 diff 上下文也会与主文本分叉。
            description.Add("Variant", FormatVariant(card, key, description));
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 差分文案取不到（{key}）：{ex.GetType().Name}: {ex.Message}");
            description.Add("Variant", string.Empty);
        }
    }

    /// <summary>把两副（a / b）的文案都读出来拼成一段（空的那端跳过；图鉴用）。</summary>
    private static string ReadAllVariants(CardModel card, LocString? source)
    {
        var parts = new List<string>();
        foreach (var stem in new[] { "a", "b" })
        {
            var key = card.Id.Entry + VariantKeySuffix + stem;
            try
            {
                var text = FormatVariant(card, key, source);
                if (!string.IsNullOrWhiteSpace(text)) parts.Add(text);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Decks} 图鉴差分文案取不到（{key}）：{ex.GetType().Name}: {ex.Message}");
            }
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// 把变体原文按主描述的同一份变量表格式化（保留 <c>:diff()</c> 的升级变色）。
    /// </summary>
    /// <param name="source">
    /// 主描述（<c>GetDescriptionForPile</c> 里已由本体装好 DynamicVars / IfUpgraded / OnTable / energyPrefix …）。
    /// 为 null 时退回只装 DynamicVars（图鉴的极早期调用 / 单测）。
    /// </param>
    private static string FormatVariant(CardModel card, string key, LocString? source = null)
    {
        try
        {
            var text = new LocString("cards", key);
            if (source is not null)
            {
                foreach (var pair in source.Variables)
                {
                    text.AddObj(pair.Key, pair.Value);
                }
            }
            else
            {
                foreach (var pair in card.DynamicVars)
                {
                    text.Add(pair.Value);
                }
            }

            return text.GetFormattedText();
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Decks} 差分文案格式化失败（{key}）："
                + $"{ex.GetType().Name}: {ex.Message} ⇒ 退回原文");
            try
            {
                return new LocString("cards", key).GetRawText();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

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

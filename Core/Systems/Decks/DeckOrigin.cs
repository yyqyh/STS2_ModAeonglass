using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Utils;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 卡牌实例上的「<b>我来自哪一副</b>」记录。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要它</b>：间隙的间隙副本是 <c>CombatState.CloneCard</c> 造出来的真实实例，而本体在
/// <c>CardModel.AfterCloned</c> 里把 <c>DeckVersion</c> 清空 ⇒ 副本一出生就<b>认不出自己原属哪一副</b>，
/// 于是差分判定（卡框配色 / 差分文案 / 差分效果）只能回退成"当前生效那一副"。
/// 用户 2026-10-14 口径：<b>间隙卡回到手牌时要保持它原本的差分</b>。
/// </para>
/// <para>
/// <b>为什么用 RitsuLib 的 <see cref="SavedAttachedState{TKey,TValue}" /></b>：它把值挂在实例上、
/// 并通过 <c>SavedProperties</c> 接进原版存档流程 ⇒ 间隙是 <c>RunPersistent</c> 牌堆，
/// 跨战斗、跨读档都还认得出这一张的来源副。自己拿 <c>ConditionalWeakTable</c> 会在读档后丢
/// （读档是从 <c>SerializableCard</c> 重建实例的）。
/// </para>
/// <para>
/// ⚠️ <b>注册必须发生在"存档属性名冻结"之前</b>（RitsuLib 在原生序列化缓存初始化时才冻结）
/// ⇒ 在 <c>Main.Initialize()</c> 里调一次 <see cref="Register" />。
/// </para>
/// </remarks>
internal static class DeckOrigin
{
    /// <summary>挂载值的名字（会进存档属性表，必须唯一）。</summary>
    private const string StateName = "aeonglass_deck_side";

    /// <summary>没记过来源副（也是"读档后默认值"）。</summary>
    /// <remarks>
    /// ⚠️ <b>必须用 -1 当哨兵，不能用 <c>DeckSide.A</c></b>：RitsuLib 的 <c>Export</c> 会对
    /// <b>每一张</b>被序列化的卡都 <c>GetOrCreate</c> 一个值（默认值也会被写进存档），
    /// 读档时再 <c>Set</c> 回来 —— 拿 <c>A</c> 当默认就等于"读一次档，所有卡都被记成 A 副"。
    /// </remarks>
    private const int Unset = -1;

    private static readonly SavedAttachedState<CardModel, int> Side =
        new(StateName, () => Unset);

    /// <summary>在 <c>Main.Initialize()</c> 里调一次（触发静态注册）。</summary>
    public static void Register()
    {
        _ = Side;
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Decks} 卡牌「来源副」记录已注册（{StateName}）："
            + "间隙副本回到手牌后仍按原副上色 / 走原副差分");
    }

    /// <summary>记下这张卡来自哪一副。</summary>
    public static void Stamp(CardModel card, DeckSide side) => Side.Set(card, (int)side);

    /// <summary>
    /// 取这张卡（或它 <c>DeckVersion</c> 指向的那一张）记下的来源副。
    /// </summary>
    /// <remarks>
    /// 顺着 <c>DeckVersion</c> 再查一层是刻意的：间隙的<b>显示投影</b>是"存储卡的克隆"
    /// （<c>GapViewPile</c> 里 <c>clone.DeckVersion = 存储卡</c>），克隆体自己没有记录
    /// —— 值挂在实例上，只有它指向的那张存储卡有。
    /// </remarks>
    public static bool TryResolve(CardModel card, out DeckSide side)
    {
        if (TryRead(card, out side))
        {
            return true;
        }

        if (card.DeckVersion is { } master && TryRead(master, out side))
        {
            return true;
        }

        side = DeckSide.A;
        return false;
    }

    /// <summary>读一个实例上的记录（没记过 / 是哨兵值都算"没有"）。</summary>
    private static bool TryRead(CardModel card, out DeckSide side)
    {
        if (Side.TryGetValue(card, out var raw) && raw != Unset)
        {
            side = raw == (int)DeckSide.B ? DeckSide.B : DeckSide.A;
            return true;
        }

        side = DeckSide.A;
        return false;
    }
}

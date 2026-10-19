using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Rare;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Systems.FrozenForm;

/// <summary>
/// 「冻结形态」的<b>记录侧</b>：订阅本体的 <c>CardPlayedEvent</c>，把"冻结形态在场时打出的其他能力"
/// 克隆进 <see cref="FrozenFormStore" />。
/// </summary>
/// <remarks>
/// <para>
/// ① 判定：卡牌类型是 <see cref="CardType.Power" />、不是冻结形态自己、不是回放中的牌，
/// 且打出者身上有 <see cref="AeonglassFrozenFormPower" />。
/// </para>
/// <para>
/// ② 事件时机：<c>CardPlayedEvent</c> 在本体 <c>Hook.AfterCardPlayed</c> 之后发布，此时原卡还在
/// <c>Play</c> 堆（尚未落到结果堆）⇒ <c>CreateClone()</c> 的"必须在战斗牌堆里"前置条件成立。
/// </para>
/// <para>
/// ③ 异常只降级（记录失败不影响出牌）；回放中的卡用 <see cref="MarkReplaying" /> 排除，
/// 避免"自动打出的能力又被记回去"。
/// </para>
/// </remarks>
internal static class FrozenFormRecorder
{
    private static readonly HashSet<CardModel> Replaying = [];

    /// <summary>这张卡是不是正在被 <see cref="FrozenFormReplay" /> 自动打出（记录时要跳过）。</summary>
    public static bool IsReplaying(CardModel card) => Replaying.Contains(card);

    /// <summary>回放开始前标记。</summary>
    public static void MarkReplaying(CardModel card) => Replaying.Add(card);

    /// <summary>回放结束（无论成败）取消标记。</summary>
    public static void UnmarkReplaying(CardModel card) => Replaying.Remove(card);

    /// <summary>由 <see cref="FrozenFormModule" /> 装配一次。</summary>
    public static void Register()
    {
        RitsuLibFramework.SubscribeLifecycle<CardPlayedEvent>(evt =>
        {
            try
            {
                OnCardPlayed(evt.CardPlay);
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.FrozenForm} 记录失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    private static void OnCardPlayed(CardPlay play)
    {
        var card = play.Card;
        if (card.Type != CardType.Power || card is AeonglassFrozenForm || IsReplaying(card))
        {
            return;
        }

        if (!CharacterGate.IsActive)
        {
            return;
        }

        var player = play.Player;
        if (!player.Creature.Powers.OfType<AeonglassFrozenFormPower>().Any())
        {
            return;
        }

        var clone = CloneForRecord(card, player);
        if (clone is null)
        {
            return;
        }

        // 记录的是"下一场要打出的战斗副本"：不能带母本指针，否则孪生 / 搁浅会把它当母本处理。
        clone.DeckVersion = null;

        if (!FrozenFormStore.Add(player, clone))
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.FrozenForm} 记录 {card.Id.Entry} 失败：记录牌堆不可用");
            return;
        }

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.FrozenForm} 记录 {card.Id.Entry}"
            + $"（+{card.CurrentUpgradeLevel}）⇒ 下场自动打出（当前 {FrozenFormStore.CountOf(player)} 张）");
    }

    /// <summary>
    /// 克隆一张战斗副本：优先 <c>CreateClone()</c>（标准战斗克隆）；它要求卡在战斗堆里，
    /// 若某条路径不满足就退回 run 级克隆（仍然只影响记录，不碰原卡）。
    /// </summary>
    private static CardModel? CloneForRecord(CardModel card, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        try
        {
            return card.CreateClone();
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.FrozenForm} {card.Id.Entry} 战斗克隆失败 ⇒ 退回 run 级克隆："
                + $"{ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            return player.RunState.CloneCard(card);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.FrozenForm} {card.Id.Entry} 无法克隆（放弃记录）："
                + $"{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}

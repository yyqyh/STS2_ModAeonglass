using MegaCrit.Sts2.Core.Entities.Players;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Systems.Persistence;

namespace STS2_Aeonglass.Core.Systems.Stasis;

/// <summary>
/// 搁浅归队的<b>Boss 层分支</b>（「远行」NEW-21）：在 Boss 层回归时随机升级卡组里 3 张。
/// </summary>
/// <remarks>
/// ① 职责：<c>LastCombatWasBoss</c>（由 <c>StasisRunHook</c> 每场战斗结束写入）+ <c>UpgradeRandomDeckCards</c>。
/// ② 调用时机：<c>StasisReturn.TickAfterCombatEnd</c> 里、某张「远行」倒计时归零且上一场是 Boss 房时。
/// ③ 依赖：<c>ActiveDeckProvider</c>（当前生效卡组）+ 卡组的 <c>UpgradeInternal</c>（照「荒漠化」的既有写法）。
/// </remarks>
internal static partial class StasisReturn
{
    /// <summary>上一场刚打完的是不是 Boss 房（由 <c>StasisRunHook.AfterCombatEnd</c> 在每次战斗结束时写入）。</summary>
    internal static bool LastCombatWasBoss { get; set; }

    /// <summary>随机升级当前卡组里的 N 张（不可升级的牌不占名额，直接跳过）。</summary>
    private static void UpgradeRandomDeckCards(Player player, int count)
    {
        try
        {
            var candidates = ActiveDeckProvider.DeckOf(player).Cards.Where(card => card.IsUpgradable).ToList();
            if (candidates.Count == 0)
            {
                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 远行：卡组里没有可升级的牌 ⇒ 只进卡组");
                return;
            }

            var upgraded = 0;
            for (var i = 0; i < count && candidates.Count > 0; i++)
            {
                var index = player.RunState?.Rng.CombatCardSelection is { } rng
                    ? rng.NextInt(candidates.Count)
                    : 0;
                var card = candidates[index];
                candidates.RemoveAt(index);
                if (!card.IsUpgradable)
                {
                    continue;
                }

                card.UpgradeInternal();
                upgraded++;
            }

            Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Stasis} 远行：Boss 层回归 ⇒ 随机升级卡组 {upgraded}/{count} 张");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Stasis} 远行：随机升级失败（忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }
}

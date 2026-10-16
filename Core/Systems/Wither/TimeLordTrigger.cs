using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Systems.Bootstrap;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Systems.Wither;

/// <summary>
/// 「时间主宰」（NEW-5 彩蛋卡）的<b>发放条件</b>：与本体第三幕 Boss「永世沙漏」同场、且己方凋萎数更多 ⇒ 进手牌。
/// </summary>
/// <remarks>
/// <para>
/// ① 判据一：当前遭遇是不是 <c>AeonglassBoss</c>（<c>Core/Models/Encounters/AeonglassBoss.cs</c>；
/// 在 <c>Core/Models/Acts/Glory.cs</c> 的 BossDiscoveryOrder 里，Glory 的 ChestOpenSfx = treasure_act3）。
/// 用类型判定而不是写死"第几幕"—— 幕序 / 双 Boss 模式都不会让它失效。
/// </para>
/// <para>
/// ② 判据二：己方凋萎数（StatusSweeper.Count，玩家战斗牌堆里的 AeonglassWitherCard）多于敌方凋萎数
/// （每只活怪的 WitherPool.WithersTotal 求和）。口径由用户 2026-10-16 确认（"按照原方案"）。
/// </para>
/// <para>
/// ③ 时机：本场第一次"玩家侧回合开始"，不是 CombatStartingEvent —— 敌方凋萎数读的是它的意图牌堆，
/// 而意图牌堆是惰性建的（log 里的"意图牌堆构建 #1（惰性）"）⇒ 战斗刚开始那一刻数不准。
/// </para>
/// <para>
/// ④ 回调用 fire-and-forget 启动、整段自带 try/catch：塞牌失败只记日志，绝不影响回合流程。
/// </para>
/// <para>
/// ⑤ <b>测试接缝</b>见 <see cref="TryGrantForTestsAsync" />（只为 headless 测试，带清理指引）。
/// </para>
/// </remarks>
internal static class TimeLordTrigger
{
    private static bool _registered;

    /// <summary>本场是否已经判定过（战斗开始清掉）。</summary>
    private static bool _checkedThisCombat;

    /// <summary>由 <c>Main.Initialize</c> 装配一次。</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(_ => _checkedThisCombat = false);

        RitsuLibFramework.SubscribeLifecycle<SideTurnStartedEvent>(evt =>
        {
            if (evt.Side != CombatSide.Player || _checkedThisCombat)
            {
                return;
            }

            _checkedThisCombat = true;
            _ = TryPlaceAsync(evt);
        });
    }

    private static async Task TryPlaceAsync(SideTurnStartedEvent evt)
    {
        try
        {
            if (!CharacterGate.IsActive)
            {
                return;
            }

            foreach (var player in evt.CombatState.Players)
            {
                await TryGrantAsync(player);
            }
        }
        catch (Exception ex)
        {
            Const.Logger.Error(
                $"[{Const.ModId}] {Const.Prefix.Wither} 时间主宰发放异常（已忽略）：{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 判据 + 发放（正式路径与测试接缝<b>共用这一份实现</b>，改动只需改这里）。
    /// </summary>
    /// <remarks>
    /// 返回是否真的发了牌。判据：当前遭遇是 <c>AeonglassBoss</c>，且己方凋萎数大于敌方。
    /// </remarks>
    private static async Task<bool> TryGrantAsync(Player player)
    {
        if (MonsterAccess.Combat?.Encounter is not AeonglassBoss || player.PlayerCombatState is null)
        {
            return false;
        }

        var mine = StatusSweeper.Count(player, card => card is AeonglassWitherCard);
        var theirs = MonsterAccess.Enemies.Sum(WitherPool.WithersTotal);
        if (mine <= theirs)
        {
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Wither} 时间主宰未触发：己方凋萎 {mine} ≤ 敌方 {theirs}");
            return false;
        }

        await GiveToHandAsync(player, mine, theirs);
        return true;
    }

    /// <summary>
    /// ⚠️ <b>测试接缝（2026-10-16 加，不是正式路径）</b>：给 headless 测试一个"模拟本场第一次玩家回合开始"的入口。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 为什么需要：正式发放由上面的 <c>SideTurnStartedEvent</c> 订阅驱动，而那个窗口在测试把战斗建好
    /// 之前就过去了 ⇒ 测试没法从外部复现"第一回合开始"这一拍。
    /// </para>
    /// <para>
    /// <b>清理指引（将来要删就按这三步）</b>：
    /// ① 删本方法；
    /// ② 删测试工程里 <c>TimeLordBossTests</c> 整个文件 + <c>TimeLordTests</c> 里那条
    /// <c>Refuses_when_the_other_side_has_at_least_as_many_withers</c>；
    /// ③ 若测试工程整体删掉，再把 <c>STS2_Aeonglass.csproj</c> 里的 <c>InternalsVisibleTo</c> 一起删。
    /// 删完正式发放路径不受影响（它走的是上面的订阅）。
    /// </para>
    /// </remarks>
    internal static Task<bool> TryGrantForTestsAsync(Player player) => TryGrantAsync(player);

    private static async Task GiveToHandAsync(Player player, int mine, int theirs)
    {
        var card = player.RunState.CreateCard(ModelDb.Card<AeonglassTimeLord>(), player);
        var result = await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom, null, skipVisuals: false);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Wither} 时间主宰触发：己方凋萎 {mine} 多于敌方 {theirs} ⇒ 进手牌（成功={result.success}）");
    }
}

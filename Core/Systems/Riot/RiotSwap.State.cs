using System.Reflection;
using System.Runtime.CompilerServices;

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Curse;


namespace STS2_Aeonglass.Core.Systems.Riot;

/// <summary>
/// 乱流的<b>共享状态</b>：两张堆的真身缓存与"属性字段"解析（<c>RiotSwap</c> 的分册）。
/// </summary>
/// <remarks>
/// ① 职责：<c>PilePair</c>、`Piles` 弱表、字段解析与缓存；对调核心与日志分别在主文件与 <c>RiotSwapLog</c>。
/// ② 调用时机：战斗开始缓存一次；之后每次判定/对调读它。
/// ③ 依赖：<c>PlayerCombatState</c> 的两个属性字段（编译器生成物）。
/// ④ ⚠️ 抓"真身"必须在 <c>Suppressed</c> 下读，否则读到的是被换过的那张。
/// </remarks>
internal static partial class RiotSwap
{

    internal static FieldInfo? FindPileField(string propertyName)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var direct = typeof(PlayerCombatState).GetField($"<{propertyName}>k__BackingField", Flags);
        if (direct is not null)
        {
            return direct;
        }

        // 编译器生成物名字万一变了：退一步按"类型是 CardPile + 名字里带属性名"找。
        return typeof(PlayerCombatState)
            .GetFields(Flags)
            .FirstOrDefault(f => f.FieldType == typeof(CardPile)
                && f.Name.Contains(propertyName, StringComparison.Ordinal));
    }

    internal static void CachePiles(PlayerCombatState state)
    {
        if (Piles.TryGetValue(state, out _))
        {
            return;
        }

        // 抓"真身"时必须把对调关掉：属性可能已被换过 ⇒ 两张堆的名字会记反。
        Suppressed = true;
        try
        {
            Piles.Add(state, new PilePair
            {
                Draw = state.DrawPile,
                Discard = state.DiscardPile,
                Hand = state.Hand,
            });
        }
        finally
        {
            Suppressed = false;
        }
    }
}

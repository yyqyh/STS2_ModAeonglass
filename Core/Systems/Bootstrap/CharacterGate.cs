using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Systems.Bootstrap;

/// <summary><b>角色门</b>：本 mod 的所有机制只在"本地玩家是永世沙漏"时生效。</summary>
/// <remarks>
/// ① 职责：<c>IsActive</c> 判定 —— 本地这个玩家是不是永世沙漏。
/// ② 挂点与时机：所有订阅 / 补丁的第一道判断（掷招接管、每回合快照、战斗结束轮换、UI 挂载等）。
/// ③ 依赖：<c>LocalContext.GetMe</c>（战斗内最准）；拿不到时退回"这一局的第一个玩家"（单人局等价）。
/// ④ ⚠️ 为什么必须有：我们改了<b>本体共享路径</b>（<c>MonsterModel.RollMove</c> 补丁、<c>SideTurnStarted</c> /
/// <c>CombatManager.TurnStarted</c> 订阅）⇒ 不做过滤，玩别的角色时也会给怪建意图牌堆、拍回溯快照、轮换双卡组。
/// 任何一步异常都当作"不是我们"：宁可不生效，也不要乱改。
/// </remarks>
internal static class CharacterGate
{
    /// <summary>本地玩家是不是永世沙漏。</summary>
    public static bool IsActive
    {
        get
        {
            try
            {
                if (CombatManager.Instance.DebugOnlyGetState() is { } combat
                    && LocalContext.GetMe(combat) is { } me)
                {
                    return me.Character is Aeonglass;
                }
            }
            catch
            {
                // 不在战斗 / 取不到本地上下文：退回 run 级判断。
            }

            try
            {
                return RunManager.Instance.DebugOnlyGetState()?.Players.FirstOrDefault()?.Character is Aeonglass;
            }
            catch
            {
                return false;
            }
        }
    }
}

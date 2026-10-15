using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;

using STS2_Aeonglass.Core.Content.Characters;

namespace STS2_Aeonglass.Core.Systems.Bootstrap;

/// <summary>
/// <b>角色门</b>：本 mod 的所有机制只在"本地玩家是永世沙漏"时生效。
/// </summary>
/// <remarks>
/// <para>
/// 为什么必须有：我们改了**本体共享路径** —— 补丁拦了 <c>MonsterModel.RollMove</c>、
/// 订阅了 <c>SideTurnStarted</c> / <c>CombatManager.TurnStarted</c>。
/// 若不做角色过滤，玩别的角色（本体或其他 mod 的角色）时也会：
/// ① 给怪建意图牌堆并接管掷招；② 每回合拍回溯快照；③ 在战斗结束轮换"双卡组"。
/// </para>
/// <para>
/// 读法：优先问本体"本地玩家是谁"（<c>LocalContext.GetMe</c>，战斗内最准），
/// 拿不到时退回"这一局的第一个玩家"（单人局等价）。任何一步异常都当作"不是我们"，宁可不生效也不要乱改。
/// </para>
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

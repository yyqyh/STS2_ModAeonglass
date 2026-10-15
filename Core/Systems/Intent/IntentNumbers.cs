using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 意图数字的<b>口径</b>：从本体招式取出"要打多少"。
/// </summary>
/// <remarks>
/// ① 职责：读数值/派生显示。
/// ② 调用时机：结算与卡面渲染。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
/// </remarks>

internal static class IntentNumbers
{
    /// <summary>把这一组意图要显示的数字拼成一行（多个意图用空格分隔；没有数字返回空串）。</summary>
    internal static string Text(
        IReadOnlyList<AbstractIntent>? intents,
        IReadOnlyList<Creature> targets,
        Creature owner)
    {
        if (intents is null || intents.Count == 0)
        {
            return string.Empty;
        }

        var found = new List<string>();
        foreach (var intent in intents)
        {
            try
            {
                // DamageCalc 为空的自定义攻击意图读不了数值 ⇒ 退回"取文案数字"那条路。
                if (intent is AttackIntent { DamageCalc: not null } attack)
                {
                    var single = attack.GetSingleDamage(targets, owner);
                    var repeats = attack.Repeats;
                    found.Add(repeats > 1 ? $"{single}×{repeats}" : $"{single}");
                    continue;
                }

                var digits = Digits(intent.GetIntentLabel(targets, owner).GetFormattedText());
                if (digits.Length > 0)
                {
                    found.Add(digits);
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Intent} 取意图数字失败（跳过这张）：{ex.GetType().Name}");
            }
        }

        return string.Join(" ", found);
    }

    /// <summary>
    /// 文案里的数字：按"连续数字"分组后用空格连接。
    /// </summary>
    /// <remarks>逐字符扫而不是"只留数字再拼"——否则 <c>8 与 2</c> 这种会出现 <c>"82"</c> 这种假数。</remarks>
    private static string Digits(string label)
    {
        var groups = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in label)
        {
            if (char.IsDigit(ch))
            {
                current.Append(ch);
                continue;
            }

            if (current.Length > 0)
            {
                groups.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            groups.Add(current.ToString());
        }

        return string.Join(" ", groups);
    }
}

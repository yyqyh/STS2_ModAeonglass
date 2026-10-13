using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace STS2_Aeonglass.Core.Systems.EnemyTime;

/// <summary>意图卡面上要显示的数字（攻击 = 伤害；多段 = <c>单段×段数</c>；其它意图取文案里的数字）。</summary>
/// <remarks>
/// <para>
/// <b>2026-10-13 修 bug（用户实测：多段攻击显示错误）</b>：原来一律"把文案里的非数字字符删掉"，
/// 而本体的多段攻击文案是 <c>FORMAT_DAMAGE_MULTI = "{Damage}[font_size=18]×{Repeat}[/font_size]"</c>
/// ⇒ 删除非数字后 <c>3×2</c> 被压成 <b>"32"</b>。
/// </para>
/// <para>
/// 现在攻击类走<b>数值 API</b>（<see cref="AttackIntent.GetSingleDamage" /> + <see cref="AttackIntent.Repeats" />），
/// 多段输出 <c>单段×段数</c> —— 与本体意图图标/悬浮说明里的写法一致；
/// 非攻击意图（防御值、状态张数这类）仍然只取文案里的数字。
/// </para>
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

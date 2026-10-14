using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 卡面文案的共用拼装件：**占位符解析** + **「升级档追加句」注入**。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么要自己解析占位符</b>：注入进卡面的文本<b>不会被本体再解析一次</b>
/// （本体的格式化只走一遍），所以 <c>{Damage}</c> 这类占位符必须在注入前自己换成当前值，
/// 否则卡面上会直接露出大括号。
/// </para>
/// <para>
/// 只认「名字在 <c>DynamicVars</c> 里存在」的占位符，其余原样留着（不猜、不吞）——
/// 与差分文案（<c>DeckVariants</c>）同一套口径，实现也共用这一份。
/// </para>
/// </remarks>
internal static class CardTextArgs
{
    /// <summary>把 <c>{名字}</c> / <c>{名字:任意后缀}</c> 换成该卡 <c>DynamicVars</c> 的当前值。</summary>
    public static string ResolvePlaceholders(CardModel card, string text)
    {
        if (!text.Contains('{'))
        {
            return text;
        }

        try
        {
            foreach (var (name, variable) in card.DynamicVars)
            {
                var value = variable.BaseValue.ToString("0.###");
                text = text.Replace("{" + name + "}", value);

                // {名字:任意后缀} —— 逐个把带后缀的写法也换掉（值同上）。
                var prefix = "{" + name + ":";
                var start = text.IndexOf(prefix, StringComparison.Ordinal);
                while (start >= 0)
                {
                    var end = text.IndexOf('}', start);
                    if (end < 0)
                    {
                        break;
                    }

                    text = string.Concat(text.AsSpan(0, start), value, text.AsSpan(end + 1));
                    start = text.IndexOf(prefix, start, StringComparison.Ordinal);
                }
            }
        }
        catch (Exception ex)
        {
            // ⚠️ 图鉴 / 卡池预览里这张卡是 **canonical 实例**，本体禁止在上面读 DynamicVars
            // （`CanonicalModelException: Canonical model ... used in incorrect place`）。
            // 这时**原样返回**：卡面 description 的格式化会带着这张卡自己的变量再解析一遍，
            // 那些占位符在那边是能解析的（这也是本体自己的卡面路径）。
            Const.Logger.Info(
                $"[{Const.ModId}] 占位符解析跳过（{ex.GetType().Name}）⇒ 交回本体的卡面格式化");
        }

        return text;
    }

    /// <summary>
    /// 往卡面注入「升级档追加句」：本地化键 <c>&lt;卡id&gt;.upgraded</c>，只在<b>已升级</b>时出现。
    /// </summary>
    /// <remarks>
    /// 卡面描述里写一个 <c>{UpgradeText}</c> 即可；没升级 / 取不到条目都注入空串（卡面不会留占位符）。
    /// 追加句里的数值占位符会按该卡的 <c>DynamicVars</c> 当前值解析（见 <see cref="ResolvePlaceholders" />）。
    /// </remarks>
    public static void InjectUpgradeClause(LocString description, CardModel card, bool isUpgraded)
    {
        var text = string.Empty;
        if (isUpgraded)
        {
            var key = card.Id.Entry + ".upgraded";
            try
            {
                // 同 DeckVariants：用 GetRawText 拿原文，占位符自己解析（否则本体格式化会在自定义选择器上报错）。
                text = ResolvePlaceholders(card, new LocString("cards", key).GetRawText());
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] 升级追加句取不到（{key}）：{ex.GetType().Name}: {ex.Message}");
            }
        }

        description.Add("UpgradeText", text);
    }
}

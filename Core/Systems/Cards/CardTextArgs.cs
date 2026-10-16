using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace STS2_Aeonglass.Core.Systems.Cards;

/// <summary>
/// 卡面文案的<b>共用拼装件</b>：占位符解析 + "升级档追加句"注入。
/// </summary>
/// <remarks>
/// ① 职责：把 DynamicVars 与差分文案拼进描述。
/// ② 调用时机：卡面渲染。
/// ④ ⚠️ 口径沿革以带日期的那行为准；命名/注释规范见 Core/Systems/README.md。
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
                $"[{Const.ModId}] {Const.Prefix.Host} 占位符解析跳过（{ex.GetType().Name}）⇒ 交回本体的卡面格式化");
        }

        return text;
    }

    /// <summary>
    /// 往卡面注入「升级档追加句」：本地化键 <c>&lt;卡id&gt;.upgraded</c>，只在<b>已升级</b>时出现。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>2026-10-16 起本方法 0 调用（保留作接缝，不要照它写新卡）</b>：升级追加句已统一改成
    /// <b>本体写法</b> —— 直接在 <c>cards.json</c> 的 <c>description</c> 里写
    /// <c>{IfUpgraded:show:升级文本|未升级文本}</c>（见本体 <c>ShowIfUpgradedFormatter</c>）。
    /// 原因：注入式写法要求"卡面渲染时有人调用本方法"，漏调一次就会在卡面上报
    /// <c>No source extension could handle the selector named "UpgradeText"</c>（实测踩过，两张卡中招）；
    /// 本体写法不依赖调用时机，升级预览时还会自动把升级句加绿。
    /// </para>
    /// <para>
    /// 卡面描述里写一个 <c>{UpgradeText}</c> 即可；没升级 / 取不到条目都注入空串（卡面不会留占位符）。
    /// 追加句里的数值占位符会按该卡的 <c>DynamicVars</c> 当前值解析（见 <see cref="ResolvePlaceholders" />）。
    /// </para>
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
                    $"[{Const.ModId}] {Const.Prefix.Host} 升级追加句取不到（{key}）：{ex.GetType().Name}: {ex.Message}");
            }
        }

        description.Add("UpgradeText", text);
    }
}

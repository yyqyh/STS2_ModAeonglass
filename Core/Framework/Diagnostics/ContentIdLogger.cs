using MegaCrit.Sts2.Core.Logging;

using STS2RitsuLib.Content;

namespace STS2_Aeonglass.Core.Framework.Diagnostics;

/// <summary>把 RitsuLib 给每个内容类型算出来的<b>真实 public entry</b> 打进日志。</summary>
/// <remarks>
/// 本地化表里的键（<c>cards.json</c> / <c>characters.json</c> / …）必须与这里打印的 entry 完全一致，
/// 差一个字母就会在启动时冒 <c>[LocValidation]</c> 警告。新增内容类型后加进 <c>Main.ContentTypes</c> 即可。
/// </remarks>
internal static class ContentIdLogger
{
    public static void LogAll(string modId, IEnumerable<Type> contentTypes)
    {
        foreach (var type in contentTypes)
        {
            try
            {
                Log.Info($"[{modId}] 内容 id：{type.Name} = {ModContentRegistry.GetFixedPublicEntry(modId, type)}");
            }
            catch (Exception ex)
            {
                Log.Warn($"[{modId}] 取内容 id 失败：{type.Name}（{ex.Message}）");
            }
        }
    }
}

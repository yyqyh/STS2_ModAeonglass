using STS2RitsuLib;
using STS2RitsuLib.Settings;

namespace STS2_Aeonglass.Core.Config;

/// <summary>
/// 永世沙漏的<b>设置页</b>：现在只剩「关于」一段（测试与调参项已于 2026-10-15 全部下架）。
/// </summary>
/// <remarks>
/// ① 职责：向 RitsuLib 注册本 mod 的设置页；页面只放版本与说明。
/// ② 调用时机：<c>Main</c> 启动时调一次（<see cref="Register" />，幂等）。
/// ③ 依赖：<see cref="AeonglassSetting" />（数据模型仍在，键名与读取代码一个没动）+ 常量（显示名 / 版本）。
/// ④ ⚠️ 要改这些值请<b>直接编辑设置文件</b>
///    （<c>&lt;user&gt;://steam/&lt;id&gt;/mod_data/STS2_Aeonglass/STS2_Aeonglass_settings.json</c>）；
///    以后想恢复某一项，把它的 <c>AddToggle / AddChoice / AddIntSlider …</c> 加回本页即可（属性与读取代码都还在）。
/// </remarks>
internal static class AeonglassModSettingsPage
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        RitsuLibFramework.RegisterModSettings(Const.ModId, page => page
            .WithTitle(ModSettingsText.Literal($"{Const.DisplayName} · {Const.ModId}"))
            .WithModDisplayName(ModSettingsText.Literal(Const.ModId))
            .AddSection("about", section => section
                .WithTitle(ModSettingsText.Literal("关于"))
                .AddParagraph(
                    "about_note",
                    ModSettingsText.Literal(
                        $"v{Const.Version} — 永世沙漏（STS2_Aeonglass）。\n"
                        + "已接入：人物模型与特效、能量计数器、时之沙（沙砾 / 空隙）、时空裂隙、操作时间（意图）、"
                        + "回溯、凋萎、双卡组、搁浅。\n"
                        + "战斗内的测试入口统一在<b>控制面板</b>；机制动作的正式按键表见《永世沙漏 · 控制面板与触发按键方案》。\n"
                        + "⚠️ 设置页的测试与调参项已全部下架（数值取定档值）；要改回请直接编辑 "
                        + "mod_data/STS2_Aeonglass/STS2_Aeonglass_settings.json。"))));
    }
}

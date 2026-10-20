using HarmonyLib;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Systems.Bootstrap;

namespace STS2_Aeonglass.Core.Patches;

/// <summary>
/// 「搁浅」的数字行：照本体「重放」的写法，在描述末尾追加
/// <c>static_hover_tips/STS2_AEONGLASS_STASIS.extraText</c>（<c>[gold]搁浅[/gold]{Times}。</c>）。
/// </summary>
/// <remarks>
/// ① 本体 <c>REPLAY.extraText</c> 就是用 <c>Times</c> 变量追加一行；这里完全同构。
/// ② 数字取母本的 <see cref="AeonglassStasisCardTemplate.StrandLevel" />（设计 X，而不是内部 X+1）。
/// ③ 关键词已经改为不注入裸标题（见 <c>StasisKeyword</c> 的 <c>None</c> 放置）⇒ 卡面只出现带数字的一行。
/// ④ 纯表现补丁：角色门 + try/catch，异常不影响本体描述。
/// </remarks>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForPile), typeof(PileType), typeof(Creature))]
internal static class StasisNumberDescriptionPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref string __result)
    {
        Append(__instance, ref __result);
    }

    internal static void Append(CardModel card, ref string result)
    {
        try
        {
            if (!CharacterGate.IsActive
                || card is not AeonglassStasisCardTemplate stasis
                || stasis.StrandLevel <= 0)
            {
                return;
            }

            var line = new LocString("static_hover_tips", "STS2_AEONGLASS_STASIS.extraText");
            line.Add("Times", stasis.StrandLevel);
            var text = line.GetFormattedText();
            result = string.IsNullOrEmpty(result) ? text : result + "\n" + text;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Stasis} 搁浅数字行追加失败（已忽略）："
                + $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>升级预览 / 卡牌库走的是另一个入口，同样补上搁浅数字行。</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.GetDescriptionForUpgradePreview))]
internal static class StasisNumberUpgradePreviewPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref string __result)
    {
        try
        {
            if (!CharacterGate.IsActive)
            {
                return;
            }

            StasisNumberDescriptionPatch.Append(__instance, ref __result);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Stasis} 升级预览搁浅数字行追加失败（已忽略）："
                + $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

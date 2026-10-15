using System;
using System.Collections.Generic;

using Godot;

using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

using STS2RitsuLib.Scaffolding.Content.Patches;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.Decks;

namespace STS2_Aeonglass.Core.Presentation.Art;
/// <summary>永世沙漏的<b>卡框材质</b>：双色对角分割，按"这张卡在哪一副"换色。</summary>
/// <remarks>
/// ① 职责：生成并登记卡框 <c>ShaderMaterial</c>（按副 → 两套颜色）。
/// ② 调用时机：<c>Main</c> 启动时 <c>Register()</c>，本体加载卡框材质时用。
/// ③ 依赖：<see cref="AeonglassFramePalette" />（唯一色源）+ 本体卡框 shader 的参数名。
/// ④ ⚠️ 换色只改 Palette；本体若改 shader 参数名，这里<b>静默失色</b>（卡框变默认色）。
/// </remarks>

internal static class AeonglassFrameMaterial
{
    /// <summary>外部注册表的键（<c>RegisterFrameProvider</c> / <c>UnregisterFrameProvider</c> 认它）。</summary>
    private const string ProviderKey = "STS2_Aeonglass.CardFrame";

    /// <summary>染色材质缓存。键 =（卡框材质路径, 是否互换）⇒ 最多 3 × 2 = 6 份。</summary>
    private static readonly Dictionary<(string Path, bool Swapped), Material> Cache = [];

    /// <summary>在 <c>Main.Initialize()</c> 里注册一次。</summary>
    public static void Register()
    {
        ExternalCardMaterialOverrideRegistry.RegisterFrameProvider(ProviderKey, For);
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Host} 卡框（双色对角分割）已接线："
            + $"上{Describe(AeonglassFramePalette.DeepBlue)}／下{Describe(AeonglassFramePalette.AmberGold)}，"
            + "[消逝]上下互换，图鉴默认[永时]");
    }

    /// <summary>给这张卡取当前该用的卡框材质；不是我们的卡就返回 <c>null</c>（交回本体默认）。</summary>
    private static Material? For(CardModel card)
    {
        // 先按程序集认领：本体和别的 mod 的卡在这一次判断里就返回 null（"null = 不覆盖"是这一级的语义）。
        if (card.GetType().Assembly != typeof(AeonglassFrameMaterial).Assembly)
        {
            return null;
        }

        // 先古卡框是**另一张图集**（card_atlas/beta.tres），uv_rect 对不上会整块塌成一个色块 ⇒ 不接。
        if (card.Rarity == CardRarity.Ancient || !IsCharacterCard(card))
        {
            return null;
        }

        return For(card.Type, IsFadingDeck(card));
    }

    /// <summary>按类型 + 状态取一份（已缓存、已染色）的卡框材质。</summary>
    public static Material? For(CardType type, bool swapped)
    {
        var path = FrameMaterialPathFor(type);
        var key = (path, swapped);
        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // 拿不到就返回 null —— 让本体回退到默认框，不要抛（资源缺失按美术缺失处理）。
        if (PreloadManager.Cache.GetMaterial(path) is not { } baseMaterial)
        {
            return null;
        }

        var material = (Material)baseMaterial.Duplicate();
        if (material is ShaderMaterial shader)
        {
            shader.SetShaderParameter(AeonglassFramePalette.TopUniform, AeonglassFramePalette.TopFor(swapped));
            shader.SetShaderParameter(AeonglassFramePalette.BottomUniform, AeonglassFramePalette.BottomFor(swapped));
        }

        Cache[key] = material;
        return material;
    }

    /// <summary>按卡牌类型挑卡框材质（本体给状态 / 诅咒牌用的也是技能框）。</summary>
    private static string FrameMaterialPathFor(CardType type) => type switch
    {
        CardType.Attack => Const.Paths.CardFrame.Attack,
        CardType.Power => Const.Paths.CardFrame.Power,
        _ => Const.Paths.CardFrame.Skill,
    };

    /// <summary>
    /// 这张卡此刻在 [消逝]（副卡组 B）里吗？
    /// </summary>
    /// <remarks>
    /// <c>IsCanonical</c> 必须<b>先</b>问：图鉴 / 卡池预览手里的是规范实例，在它上面读 <c>Owner</c>
    /// 会抛 <c>CanonicalModelException</c>（本体那个 getter 里就 <c>AssertMutable</c>）。
    /// </remarks>
    private static bool IsFadingDeck(CardModel card)
    {
        if (card.IsCanonical || card.Owner is not { } owner)
        {
            return false;
        }

        return DeckPiles.DeckIndexOf(card, owner) == 1;
    }

    /// <summary>
    /// 只给<b>角色卡池</b>的卡上色。
    /// </summary>
    /// <remarks>
    /// token 池（<c>AeonglassIntentCard</c> / <c>AeonglassWitherCard</c>）是**怪物侧**的表征 ——
    /// 怪物意图卡带的高光语义（"将要打出 = 红"、"选中 = 琴瑟"）本来就压在原版卡框上，
    /// 那里不掺双色；而 [永时] / [消逝] 说的也只是玩家这两副牌组。
    /// </remarks>
    private static bool IsCharacterCard(CardModel card)
    {
        try
        {
            return card.Pool is AeonglassCardPool;
        }
        catch (Exception)
        {
            // 卡池还没冻结 / 这张卡不在任何池里（理论上不会）⇒ 当作"不是我们的卡"，下次再问。
            return false;
        }
    }

    /// <summary>日志用的色值短描述。</summary>
    private static string Describe(Color color) => "#" + color.ToHtml(false).ToUpperInvariant();
}

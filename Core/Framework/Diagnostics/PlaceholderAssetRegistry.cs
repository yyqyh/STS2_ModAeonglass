namespace STS2_Aeonglass.Core.Framework.Diagnostics;

/// <summary>
/// <b>占位资源登记表</b>：把「当前哪些资源其实还是本体铁甲战士 / 空槽」逐条记在代码里。
/// </summary>
/// <remarks>
/// <para>
/// 为什么要有这张表：<c>PlaceholderCharacterId = "ironclad"</c> 会静默补齐一大票字段，
/// 不登记的话「商店里站着铁甲战士」这种事只能靠肉眼发现。这张表同时喂给调试面板（§6.1）。
/// </para>
/// <para>
/// 出正式美术时逐条替换，并把对应条目从这里删掉。完整清单见计划文档附录 C。
/// </para>
/// </remarks>
internal static class PlaceholderAssetRegistry
{
    /// <summary>由 <c>PlaceholderCharacterId</c> 补齐、<b>仍然是铁甲战士</b>的资源。</summary>
    public static readonly string[] FromVanillaCharacter =
    [
        "战斗能量计数器场景 → ironclad_energy_counter.tscn",
        "商店形象场景 → ironclad_merchant.tscn",
        "火堆形象场景 → ironclad_rest_site.tscn",
        "卡牌轨迹特效场景 → card_trail_ironclad.tscn",
        "音效 ×5（选人 / 转场 / 攻击 / 施法 / 死亡）→ event:/sfx/characters/ironclad/*",
        "多人手势图 ×4（指向 / 石头 / 布 / 剪刀）→ multiplayer_hand_ironclad_*.png",
    ];

    /// <summary>已用<b>透明空槽图</b>覆盖、等着被替换的图（文件名即最终规格）。</summary>
    public static readonly string[] EmptySlots =
    [
        "顶栏图标 88×88 → images/ui/aeonglass_icon.png",
        "顶栏图标描边 88×88 → images/ui/aeonglass_icon_outline.png",
        "选人立绘 132×195 → images/ui/aeonglass_select.png",
        "选人未解锁立绘 132×195 → images/ui/aeonglass_select_locked.png",
        "选人背景 1920×1080 → images/ui/aeonglass_select_background.png",
        "地图标记 49×64 → images/packed/map/icons/map_marker_aeonglass.png",
        "初始遗物图标 128×128 → images/relics/aeonglass_core.png",
    ];

    /// <summary>多行摘要（日志 / 调试面板用）。</summary>
    public static string Summary =>
        "占位（本体铁甲战士）：" + string.Join("；", FromVanillaCharacter)
        + "\n空槽（等你替换图）：" + string.Join("；", EmptySlots);
}

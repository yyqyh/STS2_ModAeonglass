using Godot;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Characters;

/// <summary>永世沙漏的卡池。</summary>
/// <remarks>
/// <para>
/// <see cref="TypeListCardPoolModel" /> <b>不需要手写列表</b>：本 mod 的卡各自用
/// <c>[RegisterCard(typeof(AeonglassCardPool))]</c> 注解注册进来，RitsuLib 启动时收进这个池子。
/// </para>
/// <para><b>第 ① 步内容为空</b>（只有两张占位基础牌，由注解注入）。正式的卡池在阶段 ④。</para>
/// <para>
/// 能量图标已换成沙漏自己的（见 <see cref="Const.Paths.Ui.Energy.Icon" />）：
/// <see cref="Const.EnergyColorName" /> 是自定义名字，本体图集里没有对应的
/// <c>energy_aeonglass.tres</c>，真正接住解析的是 RitsuLib 的两个补丁 ——
/// 池上的 <c>BigEnergyIconPath</c> / <c>TextEnergyIconPath</c>。
/// <b>四个池必须同时填这两个路径，漏一个该池的图标就没了</b>。
/// </para>
/// </remarks>
public sealed class AeonglassCardPool : TypeListCardPoolModel
{
    public override string Title => "aeonglass";

    public override string EnergyColorName => Const.EnergyColorName;

    /// <summary>卡面费用宝石 ＋ 卡池大图（RitsuLib 让这两处共用同一张）。</summary>
    public override string? BigEnergyIconPath => Const.Paths.Ui.Energy.Icon;

    /// <summary>卡面文案行内的能量小图标（24×24）。</summary>
    public override string? TextEnergyIconPath => Const.Paths.Ui.Energy.TextIcon;

    /// <summary>本体卡框材质：<c>res://materials/cards/frames/card_frame_blue_mat.tres</c>（① 占位）。</summary>
    public override string CardFrameMaterialPath => "card_frame_blue";

    /// <summary>跑局历史里卡背的颜色（① 用沙漏的青色系占位）。</summary>
    public override Color DeckEntryCardColor => new("4FD1C5");

    public override Color EnergyOutlineColor => new("1A5E5A");

    /// <summary>角色专属卡池（不是无色）。</summary>
    public override bool IsColorless => false;
}

/// <summary>
/// **token 池**：只放"不是给玩家拿的"衍生卡（目前只有 <c>AeonglassIntentCard</c> —— 意图卡带的外壳）。
/// </summary>
/// <remarks>
/// 单独开一个池、不并入 <see cref="AeonglassCardPool" /> 的理由：**避免这些卡被奖励 / 商店 / 随机生成抽到**。
/// 卡框与能量色沿用角色池的占位设置。
/// </remarks>
/// <remarks>
/// ⚠️ <b>必须挂 <c>[RegisterSharedCardPool]</c></b>（2026-10-13 修 bug）：不挂的话这个池**不进**
/// <c>ModelDb.AllCardPools</c>，而本体 <c>CardModel.Pool</c> 是"在所有卡池里找含有这张卡的池，
/// 找不到就抛 <c>InvalidProgramException: Card … is not in any card pool!</c>"
/// ⇒ 卡带里 <c>NCard.Create</c> 全部失败（log 里那一串"意图卡视图创建失败"就是这个）。
/// 共享池的语义正好：**在世界里可被解析，但不属于任何角色的奖励池**。
/// </remarks>
[RegisterSharedCardPool]
public sealed class AeonglassTokenCardPool : TypeListCardPoolModel
{
    public override string Title => "aeonglass_token";

    public override string EnergyColorName => Const.EnergyColorName;

    /// <inheritdoc cref="AeonglassCardPool.BigEnergyIconPath" />
    public override string? BigEnergyIconPath => Const.Paths.Ui.Energy.Icon;

    /// <inheritdoc cref="AeonglassCardPool.TextEnergyIconPath" />
    public override string? TextEnergyIconPath => Const.Paths.Ui.Energy.TextIcon;

    public override string CardFrameMaterialPath => "card_frame_blue";

    public override Color DeckEntryCardColor => new("4FD1C5");

    public override Color EnergyOutlineColor => new("1A5E5A");

    public override bool IsColorless => false;
}

/// <summary>永世沙漏的遗物池。</summary>
/// <remarks>
/// 与卡池同理，靠 <c>[RegisterRelic(typeof(AeonglassRelicPool))]</c> 注解收集，不手写列表。<b>① 为空</b>。
/// </remarks>
public sealed class AeonglassRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => Const.EnergyColorName;

    /// <inheritdoc cref="AeonglassCardPool.BigEnergyIconPath" />
    public override string? BigEnergyIconPath => Const.Paths.Ui.Energy.Icon;

    /// <inheritdoc cref="AeonglassCardPool.TextEnergyIconPath" />
    public override string? TextEnergyIconPath => Const.Paths.Ui.Energy.TextIcon;
}

/// <summary>永世沙漏的药水池。</summary>
/// <remarks>
/// 与卡池同理，靠 <c>[RegisterPotion(typeof(AeonglassPotionPool))]</c> 注解收集。<b>① 为空</b>。
/// </remarks>
public sealed class AeonglassPotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => Const.EnergyColorName;

    /// <inheritdoc cref="AeonglassCardPool.BigEnergyIconPath" />
    public override string? BigEnergyIconPath => Const.Paths.Ui.Energy.Icon;

    /// <inheritdoc cref="AeonglassCardPool.TextEnergyIconPath" />
    public override string? TextEnergyIconPath => Const.Paths.Ui.Energy.TextIcon;
}

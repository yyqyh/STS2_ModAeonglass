using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>蔓生</b>（WTH-4）：<b>X 费</b> · 塞 X 张凋萎进怪物的弃牌堆（全池唯一的 X 费牌）。
/// </summary>
/// <remarks>
/// <para>
/// <b>类型是"能力"，但它是"一次性"的</b>：本体的能力牌打出后进 Limbo（<c>PileType.None</c>，
/// 见 <c>CardModel.GetResultLocationForCardPlay</c>）⇒ 不会进弃牌堆参与循环，正好是这张牌要的语义。
/// </para>
/// <para>
/// 卡表给它的升级是"不变"（X 费牌按 X 缩放，没有可升的固定数值）。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassOvergrow() : ModCardTemplate(-1, CardType.Power, CardRarity.Rare, TargetType.AnyEnemy)
{
    /// <summary>X 费：本体只在子类上开这个开关，X 值用 <c>ResolveEnergyXValue()</c> 取。</summary>
    protected override bool HasEnergyCostX => true;

    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("WitherPerEnergy", 1m)];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var x = ResolveEnergyXValue();
        var count = x * (int)DynamicVars["WitherPerEnergy"].BaseValue;
        Const.Logger.Info($"[{Const.ModId}] 蔓生：X={x} ⇒ {VoidPollution.Inject(cardPlay.Target, count)}");
        return Task.CompletedTask;
    }
}

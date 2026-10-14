using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>净化</b>（WTH-3）：1 费 · 移除怪物弃牌堆里最多 3 张凋萎（升级 ⇒ 4）；<b>每移除 1 张，你获得 3 点格挡</b>。
/// </summary>
/// <remarks>
/// 「把自己的武器换掉」：凋萎本是长线伤害，这张牌把它换成当下的防线（WTH 线唯一的自洽退路）。
/// 只动<b>弃牌堆</b>里还没被抽到的那部分（手牌里的凋萎走另一条：怪物行动后自行结算）。
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassPurify() : ModCardTemplate(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
{
    /// <summary>每清掉 1 张凋萎给多少格挡（卡表定值 3；升级只加"最多几张"）。</summary>
    private const decimal BlockPerWither = 3m;

    protected override HashSet<CardTag> CanonicalTags => [];

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Purify", 3m), new DynamicVar("BlockPerWither", BlockPerWither)];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");

        var purged = VoidPollution.PurgeFromDiscard(cardPlay.Target, (int)DynamicVars["Purify"].BaseValue);
        if (purged <= 0)
        {
            Const.Logger.Info($"[{Const.ModId}] 净化：{MonsterAccess.IdOf(cardPlay.Target)} 弃牌堆里没有凋萎 ⇒ 0 挡");
            return;
        }

        var block = purged * BlockPerWither;
        Const.Logger.Info($"[{Const.ModId}] 净化：清掉 {purged} 张凋萎 ⇒ +{block} 格挡");
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Move, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars["Purify"].UpgradeValueBy(1m);
}

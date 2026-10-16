using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Config;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;
using STS2_Aeonglass.Core.Systems.Cards;
using STS2_Aeonglass.Core.Systems.Intent;
using STS2_Aeonglass.Core.Systems.Wither;

namespace STS2_Aeonglass.Core.Content.Cards.Token;

/// <summary>
/// <b>时间主宰</b>（NEW-5）· <b>0 费 · 能力</b> · token 池 —— 清掉场上所有[凋萎]，且本场不再被塞[凋萎]。
/// </summary>
/// <remarks>
/// <para>
/// ① 获取：它<b>不进角色卡池</b>（挂 token 池）。入口见 <c>TimeLordTrigger</c>：与本体第三幕 Boss「永世沙漏」
/// （<c>AeonglassBoss</c>）同场、且己方凋萎数多于敌方时，本场第一回合直接进手牌（彩蛋卡）。
/// </para>
/// <para>
/// ② 外观：稀有度取 <c>Ancient</c>（表里给的是 <c>Event</c>）。本体按 <c>Rarity == Ancient</c> 切先古外观
/// （<c>NCard</c> 的 738 / 899、<c>CardModel.cs:184</c> 的 beta 图集），而我们的卡框 provider
/// <c>AeonglassFrameMaterial.For</c> 对 Ancient 本来就返回 <c>null</c>（等于不覆盖、交回本体默认）
/// —— 所以"先古的 power 框"零美术新增即可得到（用户 2026-10-16 拍板）。
/// </para>
/// <para>
/// ③ 效果分两半：打出立刻清（每只活怪的意图三堆 + 己方战斗牌堆，与「净化」同一套，只是不打伤害）；
/// 本场免疫由能力 <c>AeonglassTimeLordPower</c> 表达，闸门加在唯一收口点 <c>WitherEffects.Inject</c>。
/// </para>
/// <para>
/// ④ 口径（用户 2026-10-16 确认"按原方案"）："不会再被给予凋萎"实现成<b>塞凋萎这件事本场停摆</b>
/// （玩家侧与怪侧都停），因为全模组的塞凋萎都过 <c>Inject</c> 这一个口子。若原意是"只保护玩家侧"，说一声。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassTokenCardPool))]
public sealed class AeonglassTimeLord() : ModCardTemplate(0, CardType.Power, CardRarity.Ancient, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [];

    protected override IEnumerable<DynamicVar> CanonicalVars => [];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<AeonglassTimeLordPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, this);

        var purged = MonsterAccess.LivingEnemies.Sum(WitherEffects.PurgeAll);
        purged += await StatusSweeper.ConsumeAsync(choiceContext, Owner, card => card is AeonglassWitherCard);

        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Wither} 时间主宰：清凋萎 {purged} 张 ⇒ 本场不再被塞（闸门已挂）");
    }

    /// <summary>升级档：表里是「—」⇒ 不变。</summary>
    protected override void OnUpgrade()
    {
    }
}

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>「下回合少抽 N 张」（ETR-1 预支的挂账）。</summary>
/// <remarks>
/// <para>
/// 挂点：本体的 <c>BeforeHandDraw</c> 管线问 <c>ModifyHandDraw(player, count)</c> ⇒ 返回 <c>count - Amount</c>。
/// </para>
/// <para>
/// <b>用掉即移除</b>：抽牌修正之后本体紧接着调 <c>AfterModifyingHandDraw()</c>（只在真的改过时调）
/// ⇒ 在那里把自己按负量移除，保证只影响**下一次**抽牌。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassDrawDebtPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private bool _applied;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (Amount <= 0m || !ReferenceEquals(player.Creature, Owner))
        {
            return count;
        }

        _applied = true;
        var modified = Math.Max(0m, count - Amount);
        Const.Logger.Info($"[{Const.ModId}] 预支：本回合抽牌 {count} → {modified}（挂账 -{Amount}）");
        return modified;
    }

    public override async Task AfterModifyingHandDraw()
    {
        if (!_applied)
        {
            return;
        }

        _applied = false;
        var debt = Amount;
        await PowerCmd.Apply<AeonglassDrawDebtPower>(
            new MegaCrit.Sts2.Core.GameActions.Multiplayer.BlockingPlayerChoiceContext(),
            Owner,
            -debt,
            Owner,
            null);
        Const.Logger.Info($"[{Const.ModId}] 预支：挂账已还清（-{debt}）");
    }
}

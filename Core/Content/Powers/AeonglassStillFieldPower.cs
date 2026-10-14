using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

using STS2_Aeonglass.Core.Systems.EnemyTime;

namespace STS2_Aeonglass.Core.Content.Powers;

/// <summary>
/// <b>静域</b>（DLY-4 挂的能力）：每回合开始时给怪物手牌里的第一张意图打上<b>迟滞</b>。
/// </summary>
/// <remarks>
/// <para>
/// <b>额度就写在 <c>Amount</c> 上</b>（卡表：每场最多 2 次）—— 用一次减一次，减到 0 能力自然消失。
/// </para>
/// <para>
/// <b>标记权不在能力这里</b>：手牌是在玩家回合开始、意图解析时才重抽的
/// （<c>IntentResolver.PrepareTurn</c>），比能力钩子晚一步；若在钩子里标"当时那手牌"，
/// 玩家在本回合卡带上根本看不到被标记的那张。所以能力只回答"我还有几次额度"，
/// 由解析器在<b>新抽好的手牌</b>上执行标记（见 <see cref="IIntentHysteresisSource" />）。
/// </para>
/// </remarks>
[RegisterPower]
public sealed class AeonglassStillFieldPower : ModPowerTemplate, IIntentHysteresisSource
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>每回合最多打几张（卡表定值 1）。</summary>
    private const int PerTurn = 1;

    /// <inheritdoc />
    public int PendingIntentHysteresis => Amount > 0m ? Math.Min(PerTurn, (int)Amount) : 0;

    /// <inheritdoc />
    public void ConsumeIntentHysteresis(int used)
    {
        if (used <= 0)
        {
            return;
        }

        var spent = Math.Min(used, (int)Amount);
        if (spent <= 0)
        {
            return;
        }

        Const.Logger.Info($"[{Const.ModId}] 静域：钉住 {spent} 张意图（额度 −{spent}）");

        // 标记发生在解析器里（同步流程）⇒ 扣额度只能"发了不管"，异常自己吞掉打日志。
        MegaCrit.Sts2.Core.Helpers.TaskHelper.RunSafely(ConsumeAsync(spent));
    }

    private async Task ConsumeAsync(int spent)
    {
        try
        {
            await PowerCmd.Apply<AeonglassStillFieldPower>(
                new BlockingPlayerChoiceContext(), Owner, -spent, Owner, null);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] 静域：扣额度失败（{ex.GetType().Name}: {ex.Message}）");
        }
    }
}

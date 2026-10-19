using MegaCrit.Sts2.Core.Entities.Cards;

using STS2RitsuLib.Interop.AutoRegistration;

using STS2_Aeonglass.Core.Content.Cards.Templates;
using STS2_Aeonglass.Core.Content.Characters;
using STS2_Aeonglass.Core.Content.Powers;

namespace STS2_Aeonglass.Core.Content.Cards.Rare;

/// <summary>
/// <b>冻结形态</b>（NEW-4）· <b>3 费 · Rare · 能力</b> —— ［虚无］。
/// 记录接下来打出的其他能力；下场战斗开始时自动打出。（不包括自己）
/// </summary>
/// <remarks>
/// <para>
/// ① 这张卡只负责挂 <see cref="AeonglassFrozenFormPower" />：Power 在场 = 本场处于"记录状态"。
/// 真正记录/回放在卡外（<c>FrozenFormRecorder</c> / <c>FrozenFormReplay</c>）。
/// </para>
/// <para>
/// ② 记录存在 run 持久的隐藏牌堆（<c>frozen_form_store</c>）里 ⇒ 本场能力消失后记录仍在；
/// 无条数上限（用户 2026-10-18 口径）。下一次战斗的第一个玩家回合开始回放，回放后清空
/// ⇒ 记录不跨场累积（同一天口径）。
/// </para>
/// <para>
/// ③ 升级去掉［虚无］（<see cref="CardKeyword.Ethereal" />）。
/// </para>
/// </remarks>
[RegisterCard(typeof(AeonglassCardPool))]
public sealed class AeonglassFrozenForm() : AeonglassCardTemplate(
    AeonglassCardSpec.Power(3, CardRarity.Rare)
        .Keyword(CardKeyword.Ethereal)
        .UpgradeRemoveKeyword(CardKeyword.Ethereal)
        .Effect(CardEffects.ApplyPower<AeonglassFrozenFormPower>())
        .Effect(CardEffects.Log(
            "frozen-form",
            _ => "冻结形态：挂上（本场后续打出的其他能力会记入下场）",
            Const.Prefix.FrozenForm)));

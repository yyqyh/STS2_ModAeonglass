using Godot;

using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;
/// <summary>意图卡的高光档位。</summary>
/// <remarks>
/// ① 职责：枚举"无 / 将要打出（红）/ 我们选中（青）"。
/// ② 调用时机：卡带每次刷新、指向模式切换时判档。
/// ③ 依赖：无（纯枚举）。
/// ④ ⚠️ 口径（用户 2026-10-12）：红 = <b>怪物将要打出的那张</b>；青 = <b>我们选中的那张</b>。
/// </remarks>

internal enum IntentHighlight
{
    /// <summary>不高光。</summary>
    None,

    /// <summary><b>红色</b>：怪物**将要打出的**那一张（本回合判定结果）。</summary>
    Pending,

    /// <summary><b>青色</b>：**我们选中的**那一张（动作卡"指向模式"里点的）。</summary>
    Selected,
}

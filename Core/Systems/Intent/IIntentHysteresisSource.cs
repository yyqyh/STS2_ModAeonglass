namespace STS2_Aeonglass.Core.Systems.Intent;

/// <summary>
/// 「敌人侧迟滞」的额度来源（DLY-4 静域）：**每回合给怪物手牌里的第一张意图打上迟滞**。
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么要有这个接口</b>：怪物手牌是<b>在玩家回合开始、意图解析时才重抽的</b>
/// （<c>IntentTurnFlow.PrepareTurn</c> → <c>DrawHand</c>），而能力钩子（<c>AfterPlayerTurnStartEarly</c>）
/// 跑在那之前 ⇒ 若在钩子里直接给"当时的手牌"打标记，玩家在本回合看到的卡带上<b>看不到</b>被标记的那张
/// （它马上就被清进弃牌堆了）。所以标记权交给解析器，能力只提供"还有多少额度"。
/// </para>
/// <para>
/// <b>接口只用公有类型</b>（<see cref="EnemyIntentState" /> 是 internal）—— 公有能力类实现 internal 接口时，
/// 成员签名里出现 internal 类型会直接编译不过（CS0051）。
/// </para>
/// </remarks>
internal interface IIntentHysteresisSource
{
    /// <summary>本次最多能打几张迟滞（0 = 不介入；调用方只会消费"真的标记成功"的数量）。</summary>
    int PendingIntentHysteresis { get; }

    /// <summary>结算：实际用掉 <paramref name="used" /> 张额度（实现方自己扣）。</summary>
    void ConsumeIntentHysteresis(int used);
}

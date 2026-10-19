using STS2RitsuLib;
using STS2RitsuLib.CardPiles;

namespace STS2_Aeonglass.Core.Systems.FrozenForm;

/// <summary>
/// 「冻结形态」（NEW-4）的模块入口：注册记录牌堆 + 装配记录 / 回放两条订阅。
/// </summary>
/// <remarks>
/// ① 无条件注册（接线永远装，角色门在运行时判）——与 <c>AnchoredTrigger</c> / <c>RiotPileWatch</c> 同一口径。
/// ② 这里没有设置开关：卡牌本身就在池里，关掉记录会让卡变成"3 费空牌"。
/// </remarks>
internal static class FrozenFormModule
{
    /// <summary>日志 / 机制总线里显示的名字。</summary>
    public const string DisplayName = "冻结形态（NEW-4 记录 / 回放）";

    private static bool _registered;

    /// <summary>由 <c>MechanismBootstrap.RegisterAll</c> 调用一次。</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        var zone = RitsuLibFramework.GetCardPileRegistry(Const.ModId).RegisterOwned(
            Const.Paths.CardPiles.FrozenFormStem,
            new ModCardPileSpec
            {
                Scope = ModCardPileScope.RunPersistent,
                Style = ModCardPileUiStyle.Headless,
            });

        FrozenFormStore.Configure(zone);
        FrozenFormRecorder.Register();
        FrozenFormReplay.Register();
    }
}

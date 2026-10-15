using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

using STS2RitsuLib;
using STS2RitsuLib.CardPiles;

using STS2_Aeonglass.Core.Config;

namespace STS2_Aeonglass.Core.Systems.Decks;

/// <summary>
/// 双卡组模块入口：注册第二副牌组的牌堆 + 战斗开始时补播种。
/// </summary>
/// <remarks>
/// <para>
/// <b>与 Zones 分模块</b>（派单口径）：牌区能力在 <c>Core/Systems/Zones/</c>，
/// 双卡组在 <c>Core/Systems/Decks/</c>；只服务 双卡组的补丁（目前<b>不需要</b>）
/// 才放 <c>Core/Systems/Decks/Patches/</c>。
/// </para>
/// <para>
/// <b>关掉 双卡组 时不留坏状态</b>：总开关关掉 ⇒ 这个牌堆根本不注册、run 钩子直接返回、
/// 调试按键不挂；<c>player.Deck</c> 就是本体那一副，一切照旧。
/// （已经切过、再关掉的情况：非生效的那副会留在 <c>deck_b</c> 牌堆里不再轮换 —— 不会丢牌，
/// 但也不会自动回主卡组，所以正式发布前不要在局中关这个开关。）
/// </para>
/// </remarks>
internal static class DeckModule
{
    /// <summary>日志 / 设置页里显示的机制名。</summary>
    public const string DisplayName = "双卡组";

    private static bool _registered;

    /// <summary>双卡组 是否启用。</summary>
    public static bool Enabled => AeonglassSettingStore.Current.DecksEnabled;

    /// <summary>由 <c>MechanismBootstrap</c> 逐条注册调用。</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;

        // 第二副牌组 = 一个 RunPersistent 牌堆里的真母本（照 搁浅 搁浅的形状）。
        // 样式 TopBarDeck + 锚点 TopBarBeforeModifiers：避开 搁浅 搁浅占的
        // "牌组按钮之后"（TopBarDeck + TopBarAfterDeck），两者不会叠在一起。
        // OnOpen 留空 ⇒ 点开就是原版牌堆界面（零副作用、**预览免费**，不需要造临时实例）。
        var pile = RitsuLibFramework.GetCardPileRegistry(Const.ModId).RegisterOwned(
            Const.Paths.CardPiles.DeckBStem,
            new ModCardPileSpec
            {
                Scope = ModCardPileScope.RunPersistent,
                Style = ModCardPileUiStyle.TopBarDeck,
                Anchor = new ModCardPileAnchor(ModCardPileAnchorKind.TopBarBeforeModifiers),
                IconPath = Const.Paths.Ui.PileIcon(Const.Paths.CardPiles.DeckBStem),
                // 角色门：别的角色的顶栏上不出现"另一副牌组"按钮。
                VisibleWhen = ctx => ctx.Player != null && Systems.Bootstrap.CharacterGate.IsActive,
            });

        DeckPiles.Configure(pile);

        // 进战斗时把"非生效那一副"补齐（第一次播种），这样顶栏按键一开始就有内容可看。
        // 2026-10-14 用户口径：**进游戏（跑局开始）就立刻播种**，不要等到第一场战斗、更不要等到先古之后。
        // 两个订阅都留着：跑局开始那次是主路径，战斗开始那次只是兜底（EnsureInitialized 自带幂等闸）。
        RitsuLibFramework.SubscribeLifecycle<RunStartedEvent>(evt =>
        {
            try
            {
                if (!Enabled)
                {
                    return;
                }

                foreach (var player in evt.RunState.Players)
                {
                    DeckPiles.EnsureInitialized(player);
                }

                Const.Logger.Info($"[{Const.ModId}] {Const.Prefix.Decks} 跑局开始：第二副牌组已就位（早于第一场战斗）");
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Decks} 跑局开始时播种失败（已忽略）：{ex.GetType().Name}: {ex.Message}");
            }
        });

        RitsuLibFramework.SubscribeLifecycle<CombatStartingEvent>(_ =>
        {
            try
            {
                if (!Enabled)
                {
                    return;
                }

                foreach (var player in Players())
                {
                    DeckPiles.EnsureInitialized(player);
                }
            }
            catch (Exception ex)
            {
                Const.Logger.Warn(
                    $"[{Const.ModId}] {Const.Prefix.Decks} 战斗开始时补播种失败（已忽略）：{ex.GetType().Name}");
            }
        });

        var setting = AeonglassSettingStore.Current;
        Const.Logger.Info(
            $"[{Const.ModId}] {Const.Prefix.Decks} 第二副牌组已注册：{pile.Id}"
            + $"（style=TopBarDeck，anchor=TopBarBeforeModifiers，scope={pile.Scope}）"
            + $"｜自动轮换={setting.DecksAutoSwitch}，主动切换={setting.DecksManualSwitch}"
            + $"（热切换={setting.DecksHotSwitch}）"
            + $"｜代价={DeckSwitchCost.Current.Describe()}"
            + $"｜每场上限={(setting.DecksSwitchesPerCombat > 0 ? setting.DecksSwitchesPerCombat.ToString() : "不限")}"
            + $"，冷却={(setting.DecksManualCooldownCombats > 0 ? setting.DecksManualCooldownCombats.ToString() : "无")}");
    }

    /// <summary>一行状态（调试面板用）。</summary>
    public static string Describe(Player? player)
    {
        if (player is null)
        {
            return "不在战斗";
        }

        if (!IsReady)
        {
            return "双卡组 未启用（第二副牌组牌堆没注册）";
        }

        var setting = AeonglassSettingStore.Current;
        var used = DeckSwitchLimit.UsedCountThisCombat(player);
        var limitText = setting.DecksSwitchesPerCombat > 0
            ? $"{used}/{setting.DecksSwitchesPerCombat}"
            : $"{used}/不限";

        return DeckPiles.DescribeNext(player)
               + $"｜本场已切 {limitText}"
               + $"｜代价 {DeckSwitchCost.Current.Describe()}"
               + (setting.DecksManualCooldownCombats > 0
                   ? $"｜冷却 {DeckState.CombatsSinceManualSwitch(player)}/{setting.DecksManualCooldownCombats}"
                   : string.Empty);
    }

    /// <summary>第二副牌组的牌堆是否已经注册。</summary>
    public static bool IsReady => DeckPiles.IsReady;

    private static IEnumerable<Player> Players() =>
        RunManager.Instance.DebugOnlyGetState()?.Players ?? [];
}

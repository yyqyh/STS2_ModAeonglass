using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;

using STS2RitsuLib;

namespace STS2_Aeonglass.Core.Content.Keywords;

/// <summary>机制词条：空间间隙 / 坍缩 / 唤回 / 回溯（只做卡面 tips，不挂到卡的关键词槽）。</summary>
internal sealed class MechanicKeyword(string stem)
{
    public string Stem { get; } = stem;

    public CardKeyword Value { get; private set; }

    public IHoverTip HoverTip => HoverTipFactory.FromKeyword(Value);

    public void Register()
    {
        try
        {
            var definition = RitsuLibFramework
                .GetKeywordRegistry(Const.ModId)
                .RegisterCardKeywordOwnedByLocNamespace(
                    Stem,
                    iconPath: null,
                    cardDescriptionPlacement:
                        STS2RitsuLib.Keywords.ModKeywordCardDescriptionPlacement.None,
                    includeInCardHoverTip: true);

            Value = definition.CardKeywordValue;
            Const.Logger.Info(
                $"[{Const.ModId}] {Const.Prefix.Host} 机制词条「{definition.Id}」已注册");
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Host} 机制词条 {Stem} 注册失败（不影响机制）："
                + $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

internal static class MechanicKeywords
{
    internal static readonly MechanicKeyword SpaceGap = new("space_gap");
    internal static readonly MechanicKeyword Collapse = new("collapse");
    internal static readonly MechanicKeyword Recall = new("recall");
    internal static readonly MechanicKeyword Rewind = new("rewind");

    public static void RegisterAll()
    {
        SpaceGap.Register();
        Collapse.Register();
        Recall.Register();
        Rewind.Register();
    }
}

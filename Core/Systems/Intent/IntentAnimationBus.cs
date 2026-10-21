using MegaCrit.Sts2.Core.Entities.Creatures;

namespace STS2_Aeonglass.Core.Systems.Intent;

internal enum IntentAnimationKind
{
    Drawn,
    Played,
    Discarded,
    WithersInjected,
}

/// <summary>纯表现 cue：机制层只广播，不读取动画结果、不等待动画完成。</summary>
internal readonly record struct IntentAnimationCue(
    IntentAnimationKind Kind,
    Creature Owner,
    IntentCard? Card,
    int Index,
    int Count);

internal static class IntentAnimationBus
{
    public static event Action<IntentAnimationCue>? Cue;

    public static void Drawn(Creature owner, IntentCard card, int handIndex) =>
        Emit(new IntentAnimationCue(IntentAnimationKind.Drawn, owner, card, handIndex, 1));

    public static void Played(Creature owner, IntentCard card, int handIndex) =>
        Emit(new IntentAnimationCue(IntentAnimationKind.Played, owner, card, handIndex, 1));

    public static void Discarded(Creature owner, IntentCard card, int handIndex) =>
        Emit(new IntentAnimationCue(IntentAnimationKind.Discarded, owner, card, handIndex, 1));

    public static void WithersInjected(Creature owner, int count) =>
        Emit(new IntentAnimationCue(IntentAnimationKind.WithersInjected, owner, null, -1, count));

    private static void Emit(IntentAnimationCue cue)
    {
        try
        {
            Cue?.Invoke(cue);
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 动画 cue 监听失败（已忽略）："
                + $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}

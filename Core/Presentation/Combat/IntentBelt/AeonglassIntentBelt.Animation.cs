using Godot;

using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;

using STS2_Aeonglass.Core.Content.Cards.Token;
using STS2_Aeonglass.Core.Systems.Intent;

namespace STS2_Aeonglass.Core.Presentation.Combat.IntentBelt;

/// <summary>意图卡四段位移动画（抽 / 打 / 弃 / 塞凋萎），纯表现、不阻塞状态。</summary>
public partial class AeonglassIntentBelt : Control
{
    private const float DrawSeconds = 0.30f;
    private const float DrawStagger = 0.04f;
    private const float PlaySeconds = 0.32f;
    private const float DiscardSeconds = 0.26f;
    private const float DiscardStagger = 0.03f;
    private const float WitherSeconds = 0.34f;
    private const float WitherStagger = 0.06f;
    private const int MaxWitherGhosts = 8;

    private readonly record struct DrawCue(IntentCard Card, int Index);
    private readonly record struct MoveCue(IntentCard Card, Vector2 Source);

    private readonly Queue<DrawCue> _drawCues = [];
    private readonly Queue<MoveCue> _discardCues = [];
    private readonly Queue<MoveCue> _playCues = [];
    private readonly Queue<int> _witherCues = [];

    /// <summary>只有刚刷完新槽位的那一帧，draw cue 才能找到正确的目标槽。</summary>
    private bool _animationsRefreshed;

    private void SubscribeIntentAnimations() => IntentAnimationBus.Cue += OnIntentAnimationCue;

    private void UnsubscribeIntentAnimations() => IntentAnimationBus.Cue -= OnIntentAnimationCue;

    private void OnIntentAnimationCue(IntentAnimationCue cue)
    {
        if (_creature is null || !ReferenceEquals(cue.Owner, _creature))
        {
            return;
        }

        switch (cue.Kind)
        {
            case IntentAnimationKind.Drawn when cue.Card is { } drawn:
                _drawCues.Enqueue(new DrawCue(drawn, cue.Index));
                break;
            case IntentAnimationKind.Played when cue.Card is { } played
                                                   && TryGetSlotCenter(played, cue.Index, out var playSource):
                _playCues.Enqueue(new MoveCue(played, playSource));
                break;
            case IntentAnimationKind.Discarded when cue.Card is { } discarded
                                                     && TryGetSlotCenter(discarded, cue.Index, out var discardSource):
                _discardCues.Enqueue(new MoveCue(discarded, discardSource));
                break;
            case IntentAnimationKind.WithersInjected when cue.Count > 0:
                _witherCues.Enqueue(cue.Count);
                break;
        }
    }

    /// <summary>每帧消费 cue；没有 UI / 处于首次发牌时直接丢弃，绝不阻塞机制。</summary>
    private void DrainAnimationCues()
    {
        if (TestMode.IsOn)
        {
            _drawCues.Clear();
            _discardCues.Clear();
            _playCues.Clear();
            _witherCues.Clear();
            return;
        }

        if (_dealing)
        {
            _drawCues.Clear();
            _discardCues.Clear();
            _playCues.Clear();
            _witherCues.Clear();
            _animationsRefreshed = false;
            return;
        }

        if (TryGetOverlay() is not { } overlay)
        {
            return;
        }

        var drawDelay = 0f;
        if (_animationsRefreshed)
        {
            while (_drawCues.TryPeek(out var draw))
            {
                if (!TryGetSlotCenter(draw.Card, draw.Index, out var target)
                    || !TryGetPileCenter(isDraw: true, out var source))
                {
                    _drawCues.Dequeue();
                    continue;
                }

                _drawCues.Dequeue();
                var ghost = TryCreateGhost(draw.Card, wither: false);
                if (ghost is null)
                {
                    continue;
                }

                var view = FindView(draw.Card, draw.Index);
                view?.SetReveal(0f);
                IntentCardMotionVfx.Play(
                    overlay, ghost, source, target, DrawSeconds, drawDelay,
                    () => view?.SetReveal(1f));
                drawDelay += DrawStagger;
            }

            _animationsRefreshed = false;
        }

        var discardDelay = 0f;
        while (_discardCues.TryDequeue(out var discard))
        {
            if (!TryGetPileCenter(isDraw: false, out var target))
            {
                continue;
            }

            var ghost = TryCreateGhost(discard.Card, wither: false);
            if (ghost is null)
            {
                continue;
            }

            IntentCardMotionVfx.Play(
                overlay, ghost, discard.Source, target, DiscardSeconds, discardDelay);
            discardDelay += DiscardStagger;
        }

        while (_playCues.TryDequeue(out var play))
        {
            if (!TryGetMonsterCenter(out var target))
            {
                continue;
            }

            var ghost = TryCreateGhost(play.Card, wither: false);
            if (ghost is null)
            {
                continue;
            }

            FindView(play.Card, -1)?.SetReveal(0f);
            IntentCardMotionVfx.Play(
                overlay, ghost, play.Source, target, PlaySeconds);
        }

        while (_witherCues.TryDequeue(out var count))
        {
            if (!TryGetPileCenter(isDraw: false, out var target)
                || !TryGetPlayerCenter(out var source))
            {
                continue;
            }

            var shown = Math.Min(count, MaxWitherGhosts);
            for (var i = 0; i < shown; i++)
            {
                var ghost = TryCreateGhost(null, wither: true);
                if (ghost is null)
                {
                    break;
                }

                IntentCardMotionVfx.Play(
                    overlay, ghost, source, target, WitherSeconds, i * WitherStagger);
            }
        }
    }

    private Control? TryGetOverlay() => NCombatRoom.Instance?.Ui;

    private AeonglassIntentCardView? FindView(IntentCard? card, int index)
    {
        if (index >= 0 && index < _views.Count
            && (card is null || ReferenceEquals(_views[index].Identity, card)))
        {
            return _views[index];
        }

        return card is null
            ? null
            : _views.FirstOrDefault(view => ReferenceEquals(view.Identity, card));
    }

    private bool TryGetSlotCenter(IntentCard? card, int index, out Vector2 center)
    {
        center = default;
        var view = FindView(card, index);
        if (view is not { IsAlive: true } || !GodotObject.IsInstanceValid(view.Wrapper))
        {
            return false;
        }

        center = view.Wrapper.GlobalPosition + view.Wrapper.Size * 0.5f;
        return true;
    }

    private bool TryGetPileCenter(bool isDraw, out Vector2 center)
    {
        center = default;
        var slot = isDraw ? _drawChip : _discardChip;
        if (slot?.Cell is not { } cell || !GodotObject.IsInstanceValid(cell))
        {
            return false;
        }

        center = cell.GlobalPosition + cell.Size * 0.5f;
        return true;
    }

    private bool TryGetMonsterCenter(out Vector2 center)
    {
        center = MonsterAccess.NodeOf(_creature)?.VfxSpawnPosition ?? default;
        return MonsterAccess.NodeOf(_creature) is not null;
    }

    private bool TryGetPlayerCenter(out Vector2 center)
    {
        center = default;
        if (PlayerOf() is not { } player
            || NCombatRoom.Instance?.GetCreatureNode(player.Creature) is not { } node)
        {
            return false;
        }

        center = node.VfxSpawnPosition;
        return true;
    }

    private AeonglassIntentCardView? TryCreateGhost(IntentCard? card, bool wither)
    {
        if (PlayerOf() is not { } player || TryGetOverlay() is not { } overlay)
        {
            return null;
        }

        try
        {
            CardModel? model;
            if (wither)
            {
                model = player.RunState.CreateCard(ModelDb.Card<AeonglassWitherCard>(), player);
            }
            else
            {
                if (card is null)
                {
                    return null;
                }

                model = IntentCardBinder.InstanceFor(player, _creature!, card);
            }

            if (model is null
                || AeonglassIntentCardView.Create(overlay, new Vector2(SlotWidth, SlotHeight), model) is not { } ghost)
            {
                return null;
            }

            ghost.SetGhostMode();
            ghost.Wrapper.Visible = false;
            return ghost;
        }
        catch (Exception ex)
        {
            Const.Logger.Warn(
                $"[{Const.ModId}] {Const.Prefix.Intent} 移动动画 ghost 创建失败（跳过）："
                + $"{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}

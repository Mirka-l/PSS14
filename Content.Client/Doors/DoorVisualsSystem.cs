using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.SprayPainter.Prototypes;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;

namespace Content.Client.Doors;

/// <summary>
/// Owns the client-side visual timeline for doors.
/// Server door state remains authoritative for gameplay, while stable states do not interrupt
/// an opening or closing animation that is already playing on the client.
/// </summary>
public sealed partial class DoorVisualsSystem : EntitySystem
{
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private const string OpeningAnimationKey = nameof(DoorVisualsSystem) + "." + nameof(DoorState.Opening);
    private const string ClosingAnimationKey = nameof(DoorVisualsSystem) + "." + nameof(DoorState.Closing);
    private const string DenyAnimationKey = nameof(DoorVisualsSystem) + "." + nameof(DoorDenyVisualEvent);

    private readonly HashSet<EntityUid> _emaggingVisuals = new();
    private readonly Dictionary<EntityUid, GameTick> _predictedDenyVisuals = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoorComponent, ComponentInit>(OnDoorInit);
        SubscribeLocalEvent<DoorVisualsComponent, ComponentInit>(OnVisualsInit);
        SubscribeLocalEvent<DoorVisualsComponent, ComponentRemove>(OnVisualsRemove);
        SubscribeLocalEvent<DoorVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<DoorVisualsComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<DoorComponent, DoorDenyVisualEvent>(OnDenyVisual);
        SubscribeNetworkEvent<DoorDenyVisualMessage>(OnDenyVisualMessage);
    }

    private void OnDoorInit(Entity<DoorComponent> ent, ref ComponentInit args)
    {
        EnsureComp<DoorVisualsComponent>(ent);
    }

    private void OnVisualsInit(Entity<DoorVisualsComponent> ent, ref ComponentInit args)
    {
        ent.Comp.LayerKeys.Clear();

        foreach (var rawLayerKey in ent.Comp.Layers)
        {
            if (!_reflection.TryParseEnumReference(rawLayerKey, out var layerKey))
            {
                Log.Error($"Door visuals on {ToPrettyString(ent)} contain an invalid layer key: {rawLayerKey}");
                continue;
            }

            ent.Comp.LayerKeys.Add(layerKey);
        }
    }

    private void OnVisualsRemove(Entity<DoorVisualsComponent> ent, ref ComponentRemove args)
    {
        _emaggingVisuals.Remove(ent);
        _predictedDenyVisuals.Remove(ent);
    }

    private void OnAppearanceChange(Entity<DoorVisualsComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null || !HasComp<DoorComponent>(ent))
            return;

        if (!_appearance.TryGetData<DoorState>(ent, DoorVisuals.State, out var state, args.Component))
            state = DoorState.Closed;

        _appearance.TryGetData<bool>(ent, DoorVisuals.Emagging, out var isEmagging, args.Component);

        if (_appearance.TryGetData<string>(ent, PaintableVisuals.Prototype, out var prototype, args.Component))
            UpdateSpriteLayers((ent.Owner, args.Sprite), prototype);

        RestartEmagEffectOnRisingEdge(ent, args.Sprite, isEmagging);
        UpdateDoorState(ent, args.Sprite, state);
    }

    private void UpdateDoorState(
        Entity<DoorVisualsComponent> ent,
        SpriteComponent sprite,
        DoorState state)
    {
        _sprite.SetDrawDepth(
            (ent.Owner, sprite),
            state == DoorState.Open ? ent.Comp.OpenDrawDepth : ent.Comp.ClosedDrawDepth);

        switch (state)
        {
            case DoorState.Opening:
                ent.Comp.PendingState = null;
                StopTransition(ent, ClosingAnimationKey);
                PlayTransition(ent, sprite, DoorState.Opening, OpeningAnimationKey);
                break;

            case DoorState.Closing:
                ent.Comp.PendingState = null;
                StopTransition(ent, OpeningAnimationKey);
                PlayTransition(ent, sprite, DoorState.Closing, ClosingAnimationKey);
                break;

            case DoorState.Open:
                if (_animation.HasRunningAnimation(ent, OpeningAnimationKey))
                {
                    ent.Comp.PendingState = DoorState.Open;
                    return;
                }

                StopTransition(ent, ClosingAnimationKey);
                SetLayerStates(ent, sprite, DoorState.Open);
                break;

            case DoorState.Closed:
                if (_animation.HasRunningAnimation(ent, ClosingAnimationKey))
                {
                    ent.Comp.PendingState = DoorState.Closed;
                    return;
                }

                StopTransition(ent, OpeningAnimationKey);
                SetLayerStates(ent, sprite, DoorState.Closed);
                break;
        }
    }

    private void PlayTransition(
        Entity<DoorVisualsComponent> ent,
        SpriteComponent sprite,
        DoorState state,
        string animationKey)
    {
        if (_animation.HasRunningAnimation(ent, animationKey))
            return;

        var stateName = GetStateName(ent.Comp, state);
        var animation = new Animation();
        var fallbackLength = 0f;
        float? baseLength = null;

        foreach (var layerKey in ent.Comp.LayerKeys)
        {
            if (!TryGetLayerState((ent.Owner, sprite), layerKey, stateName, out var rsiState))
                continue;

            fallbackLength = Math.Max(fallbackLength, rsiState.AnimationLength);
            if (layerKey.Equals(DoorVisualLayers.Base))
                baseLength = rsiState.AnimationLength;

            animation.AnimationTracks.Add(new AnimationTrackSpriteFlick
            {
                LayerKey = layerKey,
                KeyFrames =
                {
                    new AnimationTrackSpriteFlick.KeyFrame(rsiState.StateId, 0f),
                },
            });
        }

        var animationLength = baseLength ?? fallbackLength;
        if (animation.AnimationTracks.Count == 0 || animationLength <= 0f)
        {
            SetLayerStates(ent, sprite, state);
            return;
        }

        animation.Length = TimeSpan.FromSeconds(animationLength);
        _animation.Play(ent, animation, animationKey);
    }

    private void StopTransition(EntityUid uid, string animationKey)
    {
        if (_animation.HasRunningAnimation(uid, animationKey))
            _animation.Stop(uid, null, animationKey);
    }

    private void OnAnimationCompleted(Entity<DoorVisualsComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key == DenyAnimationKey)
        {
            FinishDenyVisual(ent);
            return;
        }

        if (!args.Finished || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var expectedState = args.Key switch
        {
            OpeningAnimationKey => DoorState.Open,
            ClosingAnimationKey => DoorState.Closed,
            _ => (DoorState?) null,
        };

        if (expectedState == null)
            return;

        var finalState = ent.Comp.PendingState;
        ent.Comp.PendingState = null;

        if (finalState == expectedState)
            SetLayerStates(ent, sprite, finalState.Value);
    }

    private void SetLayerStates(
        Entity<DoorVisualsComponent> ent,
        SpriteComponent sprite,
        DoorState state)
    {
        var stateName = GetStateName(ent.Comp, state);

        foreach (var layerKey in ent.Comp.LayerKeys)
        {
            if (!TryGetLayerState((ent.Owner, sprite), layerKey, stateName, out _))
                continue;

            _sprite.LayerSetAutoAnimated((ent.Owner, sprite), layerKey, true);
            _sprite.LayerSetRsiState((ent.Owner, sprite), layerKey, new RSI.StateId(stateName));
        }
    }

    private bool TryGetLayerState(
        Entity<SpriteComponent> sprite,
        Enum layerKey,
        string stateName,
        out RSI.State state)
    {
        if (_sprite.TryGetLayer(sprite.AsNullable(), layerKey, out var layer, false)
            && layer.ActualRsi != null
            && layer.ActualRsi.TryGetState(new RSI.StateId(stateName), out var rsiState))
        {
            state = rsiState;
            return true;
        }

        state = default!;
        return false;
    }

    private static string GetStateName(DoorVisualsComponent visuals, DoorState state)
    {
        return state switch
        {
            DoorState.Closed => visuals.ClosedState,
            DoorState.Closing => visuals.ClosingState,
            DoorState.Open => visuals.OpenState,
            DoorState.Opening => visuals.OpeningState,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
    }

    private void RestartEmagEffectOnRisingEdge(
        Entity<DoorVisualsComponent> ent,
        SpriteComponent sprite,
        bool isEmagging)
    {
        if (!_sprite.TryGetLayer((ent.Owner, sprite), DoorVisualLayers.BaseEmagging, out _, false))
            return;

        if (isEmagging && _emaggingVisuals.Add(ent))
        {
            _sprite.LayerSetAnimationTime((ent.Owner, sprite), DoorVisualLayers.BaseEmagging, 0f);
            _sprite.LayerSetAutoAnimated((ent.Owner, sprite), DoorVisualLayers.BaseEmagging, true);
        }
        else if (!isEmagging)
        {
            _emaggingVisuals.Remove(ent);
        }
    }

    private void OnDenyVisual(Entity<DoorComponent> ent, ref DoorDenyVisualEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        _predictedDenyVisuals[ent] = args.Tick;
        PlayDenyVisual(ent);
    }

    private void OnDenyVisualMessage(DoorDenyVisualMessage args)
    {
        var uid = GetEntity(args.Door);
        if (_predictedDenyVisuals.Remove(uid, out var predictedTick) && predictedTick == args.Tick)
            return;

        if (TryComp<DoorComponent>(uid, out var door))
            PlayDenyVisual((uid, door));
    }

    private void PlayDenyVisual(Entity<DoorComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite)
            || !TryComp<AnimationPlayerComponent>(ent, out var animationPlayer)
            || !_sprite.TryGetLayer((ent.Owner, sprite), DoorVisualLayers.BaseDeny, out var layer, false)
            || layer.ActualRsi == null
            || !layer.ActualRsi.TryGetState(layer.State, out var state)
            || state.AnimationLength <= 0f)
        {
            return;
        }

        if (_animation.HasRunningAnimation(animationPlayer, DenyAnimationKey))
            _animation.Stop(ent.Owner, animationPlayer, DenyAnimationKey);

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(state.AnimationLength),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = DoorVisualLayers.BaseDeny,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(state.StateId, 0f),
                    },
                },
            },
        };

        if (_sprite.TryGetLayer((ent.Owner, sprite), DoorVisualLayers.BasePowered, out _, false))
            _sprite.LayerSetVisible((ent.Owner, sprite), DoorVisualLayers.BasePowered, false);

        _sprite.LayerSetVisible((ent.Owner, sprite), DoorVisualLayers.BaseDeny, true);
        _animation.Play((ent.Owner, animationPlayer), animation, DenyAnimationKey);
    }

    private void FinishDenyVisual(Entity<DoorVisualsComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite)
            || !_sprite.TryGetLayer((ent.Owner, sprite), DoorVisualLayers.BaseDeny, out _, false))
        {
            return;
        }

        _sprite.LayerSetVisible((ent.Owner, sprite), DoorVisualLayers.BaseDeny, false);
        _sprite.LayerSetAutoAnimated((ent.Owner, sprite), DoorVisualLayers.BaseDeny, true);

        _appearance.TryGetData<bool>(ent, DoorVisuals.PoweredVisible, out var poweredVisible);
        if (_sprite.TryGetLayer((ent.Owner, sprite), DoorVisualLayers.BasePowered, out _, false))
            _sprite.LayerSetVisible((ent.Owner, sprite), DoorVisualLayers.BasePowered, poweredVisible);
    }

    private void UpdateSpriteLayers(Entity<SpriteComponent> sprite, string targetProto)
    {
        if (!ProtoMan.Resolve(targetProto, out var target))
            return;

        if (!target.TryComp(out SpriteComponent? targetSprite, Factory))
            return;

        _sprite.SetBaseRsi(sprite.AsNullable(), targetSprite.BaseRSI);
    }
}

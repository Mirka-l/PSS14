using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.SprayPainter.Prototypes;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Reflection;

namespace Content.Client.Doors;

public sealed partial class DoorSystem : SharedDoorSystem
{
    [Dependency] private AnimationPlayerSystem _animationSystem = default!;
    [Dependency] private IReflectionManager _reflection = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly HashSet<EntityUid> _emaggingVisuals = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoorComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<DoorComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    protected override void OnRemove(Entity<DoorComponent> ent, ref ComponentRemove args)
    {
        base.OnRemove(ent, ref args);
        _emaggingVisuals.Remove(ent);
    }

    protected override void OnComponentInit(Entity<DoorComponent> ent, ref ComponentInit args)
    {
        var comp = ent.Comp;
        comp.OpenSpriteStates = new List<(Enum, string)>(2);
        comp.ClosedSpriteStates = new List<(Enum, string)>(2);

        comp.OpenSpriteStates.Add((DoorVisualLayers.Base, comp.OpenSpriteState));
        comp.ClosedSpriteStates.Add((DoorVisualLayers.Base, comp.ClosedSpriteState));

        comp.OpeningAnimation = new Animation
        {
            Length = comp.OpeningAnimationTime,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = DoorVisualLayers.Base,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(comp.OpeningSpriteState, 0f),
                    },
                },
            },
        };

        comp.ClosingAnimation = new Animation
        {
            Length = comp.ClosingAnimationTime,
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = DoorVisualLayers.Base,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(comp.ClosingSpriteState, 0f),
                    },
                },
            },
        };

        AddGenericVisualizerTracks(ent);
    }

    /// <summary>
    /// Adds every enum-mapped sprite layer configured for <see cref="DoorVisuals.State"/> in GenericVisualizer
    /// to the same client-side opening and closing animations as the door body.
    /// This keeps RSI paths, layer selection, and state names declarative in YAML while restoring
    /// the shared local animation clock previously used by airlock overlays.
    /// </summary>
    private void AddGenericVisualizerTracks(Entity<DoorComponent> ent)
    {
        if (!TryComp<GenericVisualizerComponent>(ent, out var visualizer))
            return;

        var visuals = visualizer.Visuals;
        if (!visuals.TryGetValue(DoorVisuals.State, out var layers))
            return;

        foreach (var (rawLayerKey, states) in layers)
        {
            if (!_reflection.TryParseEnumReference(rawLayerKey, out var layerKey)
                || layerKey.Equals(DoorVisualLayers.Base))
            {
                continue;
            }

            AddAnimationTrack((Animation)ent.Comp.OpeningAnimation, layerKey, states, DoorState.Opening);
            AddAnimationTrack((Animation)ent.Comp.ClosingAnimation, layerKey, states, DoorState.Closing);
            AddFinalState(ent.Comp.OpenSpriteStates, layerKey, states, DoorState.Open);
            AddFinalState(ent.Comp.ClosedSpriteStates, layerKey, states, DoorState.Closed);
        }
    }

    private static void AddAnimationTrack(
        Animation animation,
        Enum layerKey,
        Dictionary<string, PrototypeLayerData> states,
        DoorState doorState)
    {
        if (!states.TryGetValue(doorState.ToString(), out var layerData)
            || string.IsNullOrWhiteSpace(layerData.State))
        {
            return;
        }

        animation.AnimationTracks.Add(new AnimationTrackSpriteFlick
        {
            LayerKey = layerKey,
            KeyFrames =
            {
                new AnimationTrackSpriteFlick.KeyFrame(layerData.State, 0f),
            },
        });
    }

    private static void AddFinalState(
        List<(Enum Layer, string State)> finalStates,
        Enum layerKey,
        Dictionary<string, PrototypeLayerData> states,
        DoorState doorState)
    {
        if (states.TryGetValue(doorState.ToString(), out var layerData)
            && !string.IsNullOrWhiteSpace(layerData.State))
        {
            finalStates.Add((layerKey, layerData.State));
        }
    }

    private void OnAnimationCompleted(Entity<DoorComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != DoorComponent.OpenKey && args.Key != DoorComponent.CloseKey)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        switch (ent.Comp.State)
        {
            case DoorState.Open:

                foreach (var (layer, layerState) in ent.Comp.OpenSpriteStates)
                {
                    _sprite.LayerSetAutoAnimated((ent.Owner, sprite), layer, true);
                    _sprite.LayerSetRsiState((ent.Owner, sprite), layer, layerState);
                }

                break;
            case DoorState.Closed:

                foreach (var (layer, layerState) in ent.Comp.ClosedSpriteStates)
                {
                    _sprite.LayerSetAutoAnimated((ent.Owner, sprite), layer, true);
                    _sprite.LayerSetRsiState((ent.Owner, sprite), layer, layerState);
                }

                break;
        }
    }

    private void OnAppearanceChange(Entity<DoorComponent> entity, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<DoorState>(entity, DoorVisuals.State, out var state, args.Component))
            state = DoorState.Closed;

        AppearanceSystem.TryGetData<bool>(entity, DoorVisuals.Emagging, out var isEmagging, args.Component);

        if (AppearanceSystem.TryGetData<string>(entity, PaintableVisuals.Prototype, out var prototype, args.Component))
            UpdateSpriteLayers((entity.Owner, args.Sprite), prototype);

        // GenericVisualizer owns visibility. This system only restarts the one-shot effect on its rising edge.
        if (_sprite.TryGetLayer(entity.Owner, DoorVisualLayers.BaseEmagging, out _, false))
        {
            if (isEmagging && _emaggingVisuals.Add(entity))
            {
                _sprite.LayerSetAnimationTime((entity.Owner, args.Sprite), DoorVisualLayers.BaseEmagging, 0f);
                _sprite.LayerSetAutoAnimated((entity.Owner, args.Sprite), DoorVisualLayers.BaseEmagging, true);
            }
            else if (!isEmagging)
            {
                _emaggingVisuals.Remove(entity);
            }
        }

        UpdateAppearanceForDoorState(entity, args.Sprite, state);
    }

    private void UpdateAppearanceForDoorState(Entity<DoorComponent> entity, SpriteComponent sprite, DoorState state)
    {
        _sprite.SetDrawDepth((entity.Owner, sprite), state is DoorState.Open ? entity.Comp.OpenDrawDepth : entity.Comp.ClosedDrawDepth);

        switch (state)
        {
            case DoorState.Open:
                if (_animationSystem.HasRunningAnimation(entity, DoorComponent.OpenKey))
                    return;

                if (_animationSystem.HasRunningAnimation(entity, DoorComponent.CloseKey))
                {
                    _animationSystem.Stop(entity, null, DoorComponent.CloseKey);
                    _animationSystem.Play(entity, (Animation)entity.Comp.OpeningAnimation, DoorComponent.OpenKey);
                }

                foreach (var (layer, layerState) in entity.Comp.OpenSpriteStates)
                {
                    // Allow animations to play while it's open (e.g., pinion);
                    // the animation unsets this so we gotta set it again.
                    _sprite.LayerSetAutoAnimated((entity.Owner, sprite), layer, true);
                    _sprite.LayerSetRsiState((entity.Owner, sprite), layer, layerState);
                }

                return;
            case DoorState.Closed:
                if (_animationSystem.HasRunningAnimation(entity, DoorComponent.CloseKey))
                    return;

                if (_animationSystem.HasRunningAnimation(entity, DoorComponent.OpenKey))
                {
                    _animationSystem.Stop(entity, null, DoorComponent.OpenKey);
                    _animationSystem.Play(entity, (Animation)entity.Comp.OpeningAnimation, DoorComponent.CloseKey);
                }

                foreach (var (layer, layerState) in entity.Comp.ClosedSpriteStates)
                {
                    _sprite.LayerSetAutoAnimated((entity.Owner, sprite), layer, true);
                    _sprite.LayerSetRsiState((entity.Owner, sprite), layer, layerState);
                }

                return;
            case DoorState.Opening:
                if (entity.Comp.OpeningAnimationTime == TimeSpan.Zero)
                    return;

                if (_animationSystem.HasRunningAnimation(entity, DoorComponent.OpenKey))
                    return;

                _animationSystem.Play(entity, (Animation)entity.Comp.OpeningAnimation, DoorComponent.OpenKey);

                return;
            case DoorState.Closing:
                if (entity.Comp.ClosingAnimationTime == TimeSpan.Zero)
                    return;

                if (_animationSystem.HasRunningAnimation(entity, DoorComponent.CloseKey))
                    return;

                _animationSystem.Play(entity, (Animation)entity.Comp.ClosingAnimation, DoorComponent.CloseKey);

                return;
            case DoorState.Denying:
                if (_animationSystem.HasRunningAnimation(entity, DoorComponent.DenyKey))
                    return;

                _animationSystem.Play(entity, (Animation)entity.Comp.DenyingAnimation, DoorComponent.DenyKey);

                return;
        }
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

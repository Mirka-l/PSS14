using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Power;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.Doors;

public sealed partial class AirlockSystem : SharedAirlockSystem
{
    [Dependency] private AppearanceSystem _appearanceSystem = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirlockComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<AirlockComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnComponentStartup(EntityUid uid, AirlockComponent comp, ComponentStartup args)
    {
        // Has to be on component startup because we don't know what order components initialize in and running this before DoorComponent inits _will_ crash.
        if (!TryComp<DoorComponent>(uid, out var door))
            return;

        door.DenyingAnimation = new Animation()
        {
            Length = TimeSpan.FromSeconds(comp.DenyAnimationTime),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick()
                {
                    LayerKey = DoorVisualLayers.BaseDeny,
                    KeyFrames = { new AnimationTrackSpriteFlick.KeyFrame("closed", 0f) },
                }
            }
        };
    }

    private void OnAppearanceChange(EntityUid uid, AirlockComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var boltedVisible = false;
        var emergencyLightsVisible = false;
        var accessGrantedVisible = false;
        var poweredVisible = false;
        var denyVisible = false;

        if (!_appearanceSystem.TryGetData<DoorState>(uid, DoorVisuals.State, out var state, args.Component))
            state = DoorState.Closed;

        if (_appearanceSystem.TryGetData<bool>(uid, PowerDeviceVisuals.Powered, out var powered, args.Component)
            && powered)
        {
            boltedVisible = _appearanceSystem.TryGetData<bool>(uid, DoorVisuals.BoltLights, out var lights, args.Component)
                            && lights && state == DoorState.Closed;

            emergencyLightsVisible = _appearanceSystem.TryGetData<bool>(uid, DoorVisuals.EmergencyLights, out var eaLights, args.Component) && eaLights;
            denyVisible = state == DoorState.Denying;
            accessGrantedVisible =
                    (state == DoorState.Closing
                ||  state == DoorState.Opening
                || (state == DoorState.Open && comp.OpenAccessGrantedVisible)
                || (_appearanceSystem.TryGetData<bool>(uid, DoorVisuals.ClosedLights, out var closedLights, args.Component) && closedLights))
                    && !boltedVisible && !emergencyLightsVisible && !denyVisible;
            poweredVisible = !accessGrantedVisible && !boltedVisible && !emergencyLightsVisible && !denyVisible;
        }

        _sprite.LayerSetVisible((uid, args.Sprite), DoorVisualLayers.BasePowered, poweredVisible);
        _sprite.LayerSetVisible((uid, args.Sprite), DoorVisualLayers.BaseAccessGranted, accessGrantedVisible);
        _sprite.LayerSetVisible((uid, args.Sprite), DoorVisualLayers.BaseDeny, denyVisible);

        _sprite.LayerSetVisible((uid, args.Sprite), DoorVisualLayers.BaseBolted, boltedVisible);
        if (comp.EmergencyAccessLayer)
        {
            _sprite.LayerSetVisible(
                (uid, args.Sprite),
                DoorVisualLayers.BaseEmergencyAccess,
                    emergencyLightsVisible
                &&  state != DoorState.Open
                &&  state != DoorState.Opening
                &&  state != DoorState.Closing
                && !boltedVisible
            );
        }

    }
}

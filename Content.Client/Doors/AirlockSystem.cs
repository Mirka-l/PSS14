using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Client.Animations;

namespace Content.Client.Doors;

public sealed partial class AirlockSystem : SharedAirlockSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirlockComponent, ComponentStartup>(OnComponentStartup);
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

}

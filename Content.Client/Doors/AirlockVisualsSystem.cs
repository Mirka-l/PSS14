using Content.Client.Wires.Visualizers;
using Content.Shared.Doors.Components;
using Content.Shared.Tools.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Doors;

/// <summary>
/// Applies the declarative RSI configuration from <see cref="AirlockVisualsComponent"/>
/// to the mapped sprite layers. Runtime state and visibility are handled separately
/// by GenericVisualizer.
/// </summary>
public sealed partial class AirlockVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AirlockVisualsComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<AirlockVisualsComponent> ent, ref ComponentStartup args)
    {
        DebugTools.Assert(HasComp<SpriteComponent>(ent), "AirlockVisualsComponent requires a SpriteComponent to function.");
        var sprite = Comp<SpriteComponent>(ent);
        Entity<SpriteComponent?> spriteEnt = (ent, sprite);

        SetLayer(spriteEnt, DoorVisualLayers.BasePowered, ent.Comp.Powered);
        SetLayer(spriteEnt, DoorVisualLayers.BaseUnlit, ent.Comp.Unlit);
        SetLayer(spriteEnt, WeldableLayers.BaseWelded, ent.Comp.Welded);
        SetLayer(spriteEnt, DoorVisualLayers.BaseBolted, ent.Comp.Bolted);
        SetLayer(spriteEnt, DoorVisualLayers.BaseEmergencyAccess, ent.Comp.EmergencyAccess);
        SetLayer(spriteEnt, WiresVisualLayers.MaintenancePanel, ent.Comp.Panel);
        SetLayer(spriteEnt, DoorVisualLayers.BaseDeny, ent.Comp.Deny);
        SetLayer(spriteEnt, DoorVisualLayers.BaseEmagging, ent.Comp.Emagging);
    }

    private void SetLayer(Entity<SpriteComponent?> spriteEnt, Enum layerKey, SpriteSpecifier.Rsi sprite)
    {
        DebugTools.Assert(_sprite.LayerMapTryGet(spriteEnt, layerKey, out var layer, logMissing: true));
        _sprite.LayerSetRsi(spriteEnt, layer, sprite.RsiPath, sprite.RsiState);
    }
}

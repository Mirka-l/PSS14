using Content.Shared.Doors.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using ContentDrawDepth = Content.Shared.DrawDepth.DrawDepth;
using DrawDepthTag = Robust.Shared.GameObjects.DrawDepth;

namespace Content.Client.Doors;

/// <summary>
/// Client-side presentation policy for a door.
/// Gameplay owns the physical <c>DoorState</c>; this component describes how that state is rendered.
/// </summary>
[RegisterComponent]
[Access(typeof(DoorVisualsSystem))]
public sealed partial class DoorVisualsComponent : Component
{
    [DataField]
    public string ClosedState = "closed";

    [DataField]
    public string ClosingState = "closing";

    [DataField]
    public string OpenState = "open";

    [DataField]
    public string OpeningState = "opening";

    /// <summary>
    /// Mapped sprite layers that share the physical door-state timeline.
    /// </summary>
    [DataField]
    public List<string> Layers = new()
    {
        "enum.DoorVisualLayers.Base",
    };

    [DataField(customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>))]
    public int OpenDrawDepth = (int) ContentDrawDepth.Doors;

    [DataField(customTypeSerializer: typeof(ConstantSerializer<DrawDepthTag>))]
    public int ClosedDrawDepth = (int) ContentDrawDepth.Doors;

    /// <summary>
    /// Parsed layer keys cached by <see cref="DoorVisualsSystem"/>.
    /// </summary>
    public readonly List<Enum> LayerKeys = new();

    /// <summary>
    /// A stable server state received while its visual transition is still playing.
    /// </summary>
    public DoorState? PendingState;
}

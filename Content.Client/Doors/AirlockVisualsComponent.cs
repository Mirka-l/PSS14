using Robust.Shared.Utility;

namespace Content.Client.Doors;

/// <summary>
/// Defines the RSI resources used by the logical overlay layers of an airlock.
/// Child prototypes can override a single resource without replacing Sprite.layers.
/// </summary>
[RegisterComponent]
[Access(typeof(AirlockVisualsSystem))]
public sealed partial class AirlockVisualsComponent : Component
{
    [DataField(required: true)]
    public SpriteSpecifier.Rsi Powered = default!;

    [DataField(required: true)]
    public SpriteSpecifier.Rsi AccessGranted = default!;

    [DataField(required: true)]
    public SpriteSpecifier.Rsi Welded = default!;

    [DataField(required: true)]
    public SpriteSpecifier.Rsi Bolted = default!;

    [DataField(required: true)]
    public SpriteSpecifier.Rsi EmergencyAccess = default!;

    [DataField(required: true)]
    public SpriteSpecifier.Rsi Panel = default!;

    [DataField(required: true)]
    public SpriteSpecifier.Rsi Deny = default!;

    [DataField(required: true)]
    public SpriteSpecifier.Rsi Emagging = default!;
}

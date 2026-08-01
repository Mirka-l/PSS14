using Content.Shared.DeviceLinking;
using Content.Shared.Doors.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Doors.Components;

/// <summary>
/// Companion component to DoorComponent that handles airlock-specific behavior -- wires, requiring power to operate, bolts, and allowing automatic closing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedAirlockSystem), Friend = AccessPermissions.ReadWriteExecute, Other = AccessPermissions.Read)]
public sealed partial class AirlockComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Powered;

    // Need to network airlock safety state to avoid mis-predicts when a door auto-closes as the client walks through the door.
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public bool Safety = true;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public bool EmergencyAccess = false;

    /// <summary>
    /// Sound to play when the airlock emergency access is turned on.
    /// </summary>
    [DataField]
    public SoundSpecifier EmergencyOnSound = new SoundPathSpecifier("/Audio/Machines/airlock_emergencyon.ogg");

    /// <summary>
    /// Sound to play when the airlock emergency access is turned off.
    /// </summary>
    [DataField]
    public SoundSpecifier EmergencyOffSound = new SoundPathSpecifier("/Audio/Machines/airlock_emergencyoff.ogg");

    /// <summary>
    /// Pry modifier for a powered airlock.
    /// Most anything that can pry powered has a pry speed bonus,
    /// so this default is closer to 6 effectively on e.g. jaws (9 seconds when applied to other default.)
    /// </summary>
    [DataField]
    public float PoweredPryModifier = 9f;

    /// <summary>
    /// Whether the maintenance panel should be visible even if the airlock is opened.
    /// </summary>
    [DataField]
    public bool OpenPanelVisible = false;

    /// <summary>
    /// Whether the airlock should stay open if the airlock was clicked.
    /// If the airlock was bumped into it will still auto close.
    /// </summary>
    [DataField]
    public bool KeepOpenIfClicked = false;

    /// <summary>
    /// Whether the airlock should auto close. This value is reset every time the airlock closes.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AutoClose = true;

    /// <summary>
    /// Delay until an open door automatically closes.
    /// </summary>
    [DataField]
    public TimeSpan AutoCloseDelay = TimeSpan.FromSeconds(5f);

    /// <summary>
    /// Multiplicative modifier for the auto-close delay. Can be modified by hacking the airlock wires. Setting to
    /// zero will disable auto-closing.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public float AutoCloseDelayModifier = 1.0f;

    /// <summary>
    /// The receiver port for turning off automatic closing.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<SinkPortPrototype>))]
    public string AutoClosePort = "AutoClose";

    [DataField]
    public LocId PryFailedPopup = "airlock-component-cannot-pry-is-powered-message";

    #region Graphics

    /// <summary>
    /// Whether the access-granted lights should remain visible while the door is open.
    /// </summary>
    [DataField]
    public bool OpenAccessGrantedVisible = false;

    /// <summary>
    /// Whether the door should display emergency access lights.
    /// </summary>
    [DataField]
    public bool EmergencyAccessLayer = true;

    /// <summary>
    /// How long the animation played when the airlock denies access is in seconds.
    /// </summary>
    [DataField]
    public float DenyAnimationTime = 0.3f;

    /// <summary>
    /// Pry modifier for a bolted airlock.
    /// Currently only zombies can pry bolted airlocks.
    /// </summary>
    [DataField]
    public float BoltedPryModifier = 3f;

    #endregion Graphics
}

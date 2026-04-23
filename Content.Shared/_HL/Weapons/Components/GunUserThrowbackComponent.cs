namespace Content.Shared._HL.Weapons.Components;

/// <summary>
/// Throws the shooter backwards after this gun fires.
/// </summary>
[RegisterComponent]
public sealed partial class GunUserThrowbackComponent : Component
{
    /// <summary>
    /// How strongly the shooter is thrown backwards.
    /// </summary>
    [DataField("strength")]
    public float Strength = 5f;

    /// <summary>
    /// Whether throwing should compensate for floor friction.
    /// </summary>
    [DataField("compensateFriction")]
    public bool CompensateFriction = true;
}
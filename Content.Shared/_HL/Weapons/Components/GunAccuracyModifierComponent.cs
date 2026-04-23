using Robust.Shared.Maths;

namespace Content.Shared._HL.Weapons.Components;

/// <summary>
/// Modifies gun spread values when the attached entity participates in a gun shot.
/// This works on either the shooter or the gun because <see cref="Content.Shared.Weapons.Ranged.Events.GunRefreshModifiersEvent"/>
/// is raised on both.
/// </summary>
[RegisterComponent]
public sealed partial class GunAccuracyModifierComponent : Component
{
    /// <summary>
    /// Multiplies the gun's spread values.
    /// Values below 1 improve accuracy, values above 1 worsen it.
    /// </summary>
    [DataField("spreadMultiplier")]
    public float SpreadMultiplier = 1f;

    /// <summary>
    /// Adds a flat offset to the gun's max spread after the multiplier is applied.
    /// Negative values improve accuracy.
    /// </summary>
    [DataField("maxAngleOffset")]
    public Angle MaxAngleOffset = Angle.Zero;

    /// <summary>
    /// Adds a flat offset to the gun's minimum spread after the multiplier is applied.
    /// Negative values improve first-shot accuracy.
    /// </summary>
    [DataField("minAngleOffset")]
    public Angle MinAngleOffset = Angle.Zero;
}
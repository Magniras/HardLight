using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._HL.Body.Components;

/// <summary>
/// Alters an entity's blood solution after the bloodstream has been initialized.
/// </summary>
[RegisterComponent]
public sealed partial class BloodSolutionModifierComponent : Component
{
    /// <summary>
    /// Optionally changes the entity's bloodstream blood reagent before applying <see cref="Solution"/>.
    /// </summary>
    [DataField("bloodReagent", customTypeSerializer: typeof(PrototypeIdSerializer<ReagentPrototype>))]
    public string? BloodReagent;

    /// <summary>
    /// If true, clears the current blood solution before adding <see cref="Solution"/>.
    /// </summary>
    [DataField("clearExisting")]
    public bool ClearExisting = true;

    /// <summary>
    /// The reagents to add to the bloodstream.
    /// </summary>
    [DataField("solution")]
    public Solution Solution = new();
}
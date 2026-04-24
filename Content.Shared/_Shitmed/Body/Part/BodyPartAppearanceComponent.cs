using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Body.Part;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BodyPartAppearanceComponent : Component
{
    /// <summary>
    ///     HumanoidVisualLayer type for this body part.
    /// </summary>
    /// <remarks>
    ///     Serialized as "partType" to avoid collision with the engine's reserved
    ///     component-name "type" key in <c>EntitySerializer</c>, which previously
    ///     caused "Already contains key type" exceptions when saving grids
    ///     (e.g. ship saves) carrying humanoid body parts.
    /// </remarks>
    [DataField("partType"), AutoNetworkedField]
    public HumanoidVisualLayers Type { get; set; }

    /// <summary>
    ///     Relevant markings for this body part that will be applied on attachment.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<HumanoidVisualLayers, List<Marking>> Markings = new();

    /// <summary>
    ///     ID of this custom base layer. Must be a <see cref="HumanoidSpeciesSpriteLayer"/>.
    ///     I don't actually know if these serializer props are necessary. I just lifted this from MS14 lol.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<HumanoidSpeciesSpriteLayer>)), AutoNetworkedField]
    public string? ID { get; set; }

    /// <summary>
    ///     Color of this custom base layer. Null implies skin colour if the corresponding <see cref="HumanoidSpeciesSpriteLayer"/> is set to match skin.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? Color { get; set; }

    /// <summary>
    ///     Color of this custom base eye layer. Null implies eye colour if the corresponding <see cref="HumanoidSpeciesSpriteLayer"/> is set to match skin.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? EyeColor { get; set; }
}

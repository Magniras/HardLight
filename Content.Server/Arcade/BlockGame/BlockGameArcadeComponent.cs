using Robust.Shared.Player;
using Robust.Shared.Prototypes; // Coyote
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List; // Coyote

namespace Content.Server.Arcade.BlockGame;

[RegisterComponent]
public sealed partial class BlockGameArcadeComponent : Component
{
    /// <summary>
    /// The currently active session of NT-BG.
    /// </summary>
    public BlockGame? Game = null;

    /// <summary>
    /// The player currently playing the active session of NT-BG.
    /// </summary>
    public EntityUid? Player = null;

    /// <summary>
    /// The players currently viewing (but not playing) the active session of NT-BG.
    /// </summary>
    public readonly List<EntityUid> Spectators = new();

    // COYOTE START

    /// <summary>
    /// The prototypes that can be dispensed as a reward for winning the game.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("possibleRewards", customTypeSerializer: typeof(PrototypeIdListSerializer<EntityPrototype>))]
    public List<string> PossibleRewards = new();

    // COYOTE END
}

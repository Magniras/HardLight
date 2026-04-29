using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
///     Sent by an admin client to teleport a player's attached entity to a
///     safe tile on the player's own owned ship.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestTeleportPlayerToShipMessage : EntityEventArgs
{
    public string OwnerUserId { get; }

    public RequestTeleportPlayerToShipMessage(string ownerUserId)
    {
        OwnerUserId = ownerUserId;
    }
}

[Serializable, NetSerializable]
public sealed class TeleportPlayerToShipResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public bool Success { get; }
    public string? Error { get; }
    public string? ShipName { get; }
    public Vector2 DestinationPosition { get; }

    public TeleportPlayerToShipResponseMessage(
        string ownerUserId,
        bool success,
        string? error = null,
        string? shipName = null,
        Vector2 destinationPosition = default)
    {
        OwnerUserId = ownerUserId;
        Success = success;
        Error = error;
        ShipName = shipName;
        DestinationPosition = destinationPosition;
    }
}

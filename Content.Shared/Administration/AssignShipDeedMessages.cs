using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
///     Sent by an admin client to request the full list of ships
///     that can be assigned as a deed to a player's ID card.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestShipDeedListMessage : EntityEventArgs
{
    /// <summary>The ahelp target player whose channel the admin is in.</summary>
    public string TargetUserId { get; }

    public RequestShipDeedListMessage(string targetUserId)
    {
        TargetUserId = targetUserId;
    }
}

/// <summary>
///     Single entry in the ship list returned to the admin.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipDeedEntry
{
    public NetEntity ShipNetEntity { get; }
    public string ShipName { get; }
    public string? OwnerName { get; }
    public string? OwnerUserId { get; }

    public ShipDeedEntry(NetEntity shipNetEntity, string shipName, string? ownerName, string? ownerUserId)
    {
        ShipNetEntity = shipNetEntity;
        ShipName = shipName;
        OwnerName = ownerName;
        OwnerUserId = ownerUserId;
    }
}

/// <summary>
///     Sent from the server to the requesting admin containing all available ships.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipDeedListResponseMessage : EntityEventArgs
{
    public string TargetUserId { get; }
    public ShipDeedEntry[] Ships { get; }

    public ShipDeedListResponseMessage(string targetUserId, ShipDeedEntry[] ships)
    {
        TargetUserId = targetUserId;
        Ships = ships;
    }
}

/// <summary>
///     Sent by an admin to assign the deed for a specific ship to the
///     ID card held by or stored in the target player's PDA.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestAssignShipDeedMessage : EntityEventArgs
{
    public string TargetUserId { get; }
    public NetEntity ShipNetEntity { get; }

    public RequestAssignShipDeedMessage(string targetUserId, NetEntity shipNetEntity)
    {
        TargetUserId = targetUserId;
        ShipNetEntity = shipNetEntity;
    }
}

/// <summary>
///     Server response to an assign-deed request.
/// </summary>
[Serializable, NetSerializable]
public sealed class AssignShipDeedResponseMessage : EntityEventArgs
{
    public string TargetUserId { get; }
    public bool Success { get; }
    public string? Error { get; }
    public string? ShipName { get; }

    public AssignShipDeedResponseMessage(
        string targetUserId,
        bool success,
        string? error = null,
        string? shipName = null)
    {
        TargetUserId = targetUserId;
        Success = success;
        Error = error;
        ShipName = shipName;
    }
}

using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

/// <summary>
/// Admin-side request to spawn an item prototype next to the ahelping player.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestSpawnAhelpItemMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public string PrototypeId { get; }

    public RequestSpawnAhelpItemMessage(string ownerUserId, string prototypeId)
    {
        OwnerUserId = ownerUserId;
        PrototypeId = prototypeId;
    }
}

[Serializable, NetSerializable]
public sealed class SpawnAhelpItemResponseMessage : EntityEventArgs
{
    public string OwnerUserId { get; }
    public string PrototypeId { get; }
    public bool Success { get; }
    public string? Error { get; }
    public string? ItemName { get; }
    public Vector2 SpawnPosition { get; }

    public SpawnAhelpItemResponseMessage(
        string ownerUserId,
        string prototypeId,
        bool success,
        string? error = null,
        string? itemName = null,
        Vector2 spawnPosition = default)
    {
        OwnerUserId = ownerUserId;
        PrototypeId = prototypeId;
        Success = success;
        Error = error;
        ItemName = itemName;
        SpawnPosition = spawnPosition;
    }
}

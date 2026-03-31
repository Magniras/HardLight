using System.Numerics;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Content.Shared.Procedural;
using Content.Shared.Random.Helpers;
using Content.Shared.Whitelist;
using Robust.Shared.Collections;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Procedural;

public sealed partial class DungeonSystem
{
    private readonly Dictionary<string, CachedRoomTemplate> _roomTemplateCache = new();
    private readonly HashSet<ResPath> _cachedRoomTemplateAtlases = new();

    // Temporary caches.
    private readonly HashSet<EntityUid> _entitySet = new();
    private readonly List<DungeonRoomPrototype> _availableRooms = new();

    private sealed record CachedRoomTemplate(
        CachedRoomTile[] Tiles,
        CachedRoomEntity[] Entities,
        CachedRoomDecal[] Decals);

    private readonly record struct CachedRoomTile(Vector2i LocalIndices, Tile Tile);

    private readonly record struct CachedRoomEntity(
        string PrototypeId,
        Vector2 LocalPosition,
        Angle LocalRotation,
        bool Anchored);

    private readonly record struct CachedRoomDecal(
        string Id,
        Vector2 LocalCoordinates,
        Color? Color,
        Angle Angle,
        int ZIndex,
        bool Cleanable);

    /// <summary>
    /// Gets a random dungeon room matching the specified area, whitelist and size.
    /// </summary>
    public DungeonRoomPrototype? GetRoomPrototype(Random random, EntityWhitelist? whitelist = null, Vector2i? size = null)
    {
        return GetRoomPrototype(random, whitelist, minSize: size, maxSize: size);
    }

    /// <summary>
    /// Gets a random dungeon room matching the specified area and whitelist and size range
    /// </summary>
    public DungeonRoomPrototype? GetRoomPrototype(Random random,
        EntityWhitelist? whitelist = null,
        Vector2i? minSize = null,
        Vector2i? maxSize = null)
    {
        // Can never be true.
        if (whitelist is { Tags: null })
        {
            return null;
        }

        _availableRooms.Clear();

        foreach (var proto in _prototype.EnumeratePrototypes<DungeonRoomPrototype>())
        {
            if (minSize is not null && (proto.Size.X < minSize.Value.X || proto.Size.Y < minSize.Value.Y))
                continue;

            if (maxSize is not null && (proto.Size.X > maxSize.Value.X || proto.Size.Y > maxSize.Value.Y))
                continue;

            if (whitelist == null)
            {
                _availableRooms.Add(proto);
                continue;
            }

            foreach (var tag in whitelist.Tags)
            {
                if (!proto.Tags.Contains(tag))
                    continue;

                _availableRooms.Add(proto);
                break;
            }
        }

        if (_availableRooms.Count == 0)
            return null;

        var room = _availableRooms[random.Next(_availableRooms.Count)];

        return room;
    }

    public void SpawnRoom(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i origin,
        DungeonRoomPrototype room,
        Random random,
        HashSet<Vector2i>? reservedTiles,
        bool clearExisting = false,
        bool rotation = false)
    {
        var originTransform = Matrix3Helpers.CreateTranslation(origin.X, origin.Y);
        var roomRotation = Angle.Zero;

        if (rotation)
        {
            roomRotation = GetRoomRotation(room, random);
        }

        var roomTransform = Matrix3Helpers.CreateTransform((Vector2)room.Size / 2f, roomRotation);
        var finalTransform = Matrix3x2.Multiply(roomTransform, originTransform);

        SpawnRoom(gridUid, grid, finalTransform, room, reservedTiles, clearExisting);
    }

    public Angle GetRoomRotation(DungeonRoomPrototype room, Random random)
    {
        var roomRotation = Angle.Zero;

        if (room.Size.X == room.Size.Y)
        {
            // Give it a random rotation
            roomRotation = random.Next(4) * Math.PI / 2;
        }
        else if (random.Next(2) == 1)
        {
            roomRotation += Math.PI;
        }

        return roomRotation;
    }

    public void SpawnRoom(
        EntityUid gridUid,
        MapGridComponent grid,
        Matrix3x2 roomTransform,
        DungeonRoomPrototype room,
        HashSet<Vector2i>? reservedTiles = null,
        bool clearExisting = false)
    {
        var cachedRoom = GetOrCreateRoomTemplateData(room);
        var roomDimensions = room.Size;

        var finalRoomRotation = roomTransform.Rotation();

        // HardLight: CachedRoomTile.LocalIndices are room-local, so tile transform must use
        // room-local center. Using atlas offset here shifts floors/walls away from room entities.
        var roomCenter = (Vector2) roomDimensions / 2f * grid.TileSize; // HardLight
        var tileOffset = -roomCenter + grid.TileSizeHalfVector;
        _tiles.Clear();

        // Load tiles
        foreach (var cachedTile in cachedRoom.Tiles)
        {
            var tilePos = Vector2.Transform(cachedTile.LocalIndices + tileOffset, roomTransform);
            var rounded = tilePos.Floored();

            if (!clearExisting && reservedTiles?.Contains(rounded) == true)
                continue;

            _tiles.Add((rounded, cachedTile.Tile));

            if (!clearExisting)
                continue;

            var anchored = _maps.GetAnchoredEntities((gridUid, grid), rounded);
            foreach (var ent in anchored)
            {
                QueueDel(ent);
            }
        }

        _maps.SetTiles(gridUid, grid, _tiles);

        // Load entities
        foreach (var templateEnt in cachedRoom.Entities)
        {
            var childPos = Vector2.Transform(templateEnt.LocalPosition, roomTransform);

            if (!clearExisting && reservedTiles?.Contains(childPos.Floored()) == true)
                continue;

            var childRot = templateEnt.LocalRotation + finalRoomRotation;
            var ent = Spawn(templateEnt.PrototypeId, new EntityCoordinates(gridUid, childPos));

            var childXform = _xformQuery.GetComponent(ent);
            _transform.SetLocalRotation(ent, childRot, childXform);

            // If the templated entity was anchored then anchor us too.
            if (templateEnt.Anchored && !childXform.Anchored)
                _transform.AnchorEntity((ent, childXform), (gridUid, grid));
            else if (!templateEnt.Anchored && childXform.Anchored)
                _transform.Unanchor(ent, childXform);
        }

        // Load decals
        if (cachedRoom.Decals.Length > 0)
        {
            EnsureComp<DecalGridComponent>(gridUid);

            foreach (var decal in cachedRoom.Decals)
            {
                var position = Vector2.Transform(decal.LocalCoordinates, roomTransform);
                position -= grid.TileSizeHalfVector;

                if (!clearExisting && reservedTiles?.Contains(position.Floored()) == true)
                    continue;

                // Umm uhh I love decals so uhhhh idk what to do about this
                var angle = (decal.Angle + finalRoomRotation).Reduced();

                // Adjust because 32x32 so we can't rotate cleanly
                // Yeah idk about the uhh vectors here but it looked visually okay but they may still be off by 1.
                // Also EyeManager.PixelsPerMeter should really be in shared.
                if (angle.Equals(Math.PI))
                {
                    position += new Vector2(-1f / 32f, 1f / 32f);
                }
                else if (angle.Equals(-Math.PI / 2f))
                {
                    position += new Vector2(-1f / 32f, 0f);
                }
                else if (angle.Equals(Math.PI / 2f))
                {
                    position += new Vector2(0f, 1f / 32f);
                }
                else if (angle.Equals(Math.PI * 1.5f))
                {
                    // I hate this but decals are bottom-left rather than center position and doing the
                    // matrix ops is a PITA hence this workaround for now; I also don't want to add a stupid
                    // field for 1 specific op on decals
                    if (decal.Id != "DiagonalCheckerAOverlay" &&
                        decal.Id != "DiagonalCheckerBOverlay")
                    {
                        position += new Vector2(-1f / 32f, 0f);
                    }
                }

                var tilePos = position.Floored();

                // Fallback because uhhhhhhhh yeah, a corner tile might look valid on the original
                // but place 1 nanometre off grid and fail the add.
                if (!_maps.TryGetTileRef(gridUid, grid, tilePos, out var tileRef) || tileRef.Tile.IsEmpty)
                {
                    _maps.SetTile(gridUid, grid, tilePos, _tile.GetVariantTile((ContentTileDefinition)_tileDefManager[FallbackTileId], _random.GetRandom()));
                }

                var result = _decals.TryAddDecal(
                    decal.Id,
                    new EntityCoordinates(gridUid, position),
                    out _,
                    decal.Color,
                    angle,
                    decal.ZIndex,
                    decal.Cleanable);

                DebugTools.Assert(result);
            }
        }
    }

    private CachedRoomTemplate GetOrCreateRoomTemplateData(DungeonRoomPrototype room)
    {
        if (_roomTemplateCache.TryGetValue(room.ID, out var cachedRoom))
            return cachedRoom;

        BuildRoomTemplateCacheForAtlas(room.AtlasPath);

        if (_roomTemplateCache.TryGetValue(room.ID, out cachedRoom))
            return cachedRoom;

        throw new Exception($"Failed to build cached dungeon room template for {room.ID}.");
    }

    private void BuildRoomTemplateCacheForAtlas(ResPath atlasPath)
    {
        if (!_cachedRoomTemplateAtlases.Add(atlasPath))
            return;

        var opts = new MapLoadOptions
        {
            DeserializationOptions = DeserializationOptions.Default with { PauseMaps = true },
            ExpectedCategory = FileCategory.Map,
        };

        if (!_loader.TryLoadGeneric(atlasPath, out var result, opts) || !result.Grids.TryFirstOrNull(out var templateGridUid))
            throw new Exception($"Failed to load dungeon atlas template {atlasPath}.");

        try
        {
            var templateGrid = Comp<MapGridComponent>(templateGridUid.Value);

            foreach (var room in _prototype.EnumeratePrototypes<DungeonRoomPrototype>())
            {
                if (!room.AtlasPath.Equals(atlasPath))
                    continue;

                _roomTemplateCache[room.ID] = BuildRoomTemplateData(templateGridUid.Value, templateGrid, room);
            }
        }
        finally
        {
            _loader.Delete(result);
        }
    }

    private CachedRoomTemplate BuildRoomTemplateData(
        EntityUid templateGridUid,
        MapGridComponent templateGrid,
        DungeonRoomPrototype room)
    {
        var roomCenter = (room.Offset + room.Size / 2f) * templateGrid.TileSize;
        var tileBounds = new Box2(room.Offset, room.Offset + room.Size);

        var cachedTiles = new List<CachedRoomTile>(room.Size.X * room.Size.Y);
        for (var x = 0; x < room.Size.X; x++)
        {
            for (var y = 0; y < room.Size.Y; y++)
            {
                var indices = new Vector2i(x + room.Offset.X, y + room.Offset.Y);

                if (room.IgnoreTile is not null &&
                    _maps.TryGetTileDef(templateGrid, indices, out var tileDef) &&
                    room.IgnoreTile == tileDef.ID)
                {
                    continue;
                }

                cachedTiles.Add(new CachedRoomTile(new Vector2i(x, y), _maps.GetTileRef(templateGridUid, templateGrid, indices).Tile));
            }
        }

        var cachedEntities = new List<CachedRoomEntity>();
        foreach (var entity in _lookup.GetEntitiesIntersecting(templateGridUid, tileBounds, LookupFlags.Uncontained))
        {
            var prototypeId = _metaQuery.GetComponent(entity).EntityPrototype?.ID;
            if (prototypeId == null)
                continue;

            var xform = _xformQuery.GetComponent(entity);
            var tile = xform.LocalPosition.Floored(); // HardLight

            // HardLight: Intersections can include neighboring-room entities whose bounds cross edges.
            // Cache only entities whose origin tile is strictly inside this room rectangle.
            if (tile.X < room.Offset.X || tile.Y < room.Offset.Y ||
                tile.X >= room.Offset.X + room.Size.X || tile.Y >= room.Offset.Y + room.Size.Y)
            {
                continue;
            }

            cachedEntities.Add(new CachedRoomEntity(
                prototypeId,
                xform.LocalPosition - roomCenter,
                xform.LocalRotation,
                xform.Anchored));
        }

        var cachedDecals = new List<CachedRoomDecal>();
        if (TryComp<DecalGridComponent>(templateGridUid, out var loadedDecals))
        {
            foreach (var (_, decal) in _decals.GetDecalsIntersecting(templateGridUid, tileBounds, loadedDecals))
            {
                var tile = decal.Coordinates.Floored(); // HardLight

                // HardLight: Intersections can include neighboring-room decals whose bounds cross edges.
                if (tile.X < room.Offset.X || tile.Y < room.Offset.Y ||
                    tile.X >= room.Offset.X + room.Size.X || tile.Y >= room.Offset.Y + room.Size.Y)
                {
                    continue;
                }

                cachedDecals.Add(new CachedRoomDecal(
                    decal.Id,
                    decal.Coordinates + templateGrid.TileSizeHalfVector - roomCenter,
                    decal.Color,
                    decal.Angle,
                    decal.ZIndex,
                    decal.Cleanable));
            }
        }

        return new CachedRoomTemplate(
            cachedTiles.ToArray(),
            cachedEntities.ToArray(),
            cachedDecals.ToArray());
    }
}

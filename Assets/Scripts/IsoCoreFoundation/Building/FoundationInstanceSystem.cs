using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Runtime instance pockets for building interiors and portals. This keeps the
    /// canonical scene and grid intact while giving entrances a real teleport target.
    /// </summary>
    public sealed class FoundationInstanceSystem
    {
        IsoWorld _world;
        IsoFoundationPlayer _player;
        FoundationContent _content;
        FoundationInteractionOverlay _overlay;
        readonly System.Collections.Generic.List<GameObject> _instanceObjects = new();
        readonly System.Collections.Generic.List<FoundationInstanceDecoration> _decorations = new();
        readonly System.Collections.Generic.List<Vector2Int> _blockedDecorationCells = new();
        readonly List<Vector2Int> _activeRenderCells = new();

        bool _inside;
        string _instanceId;
        string _displayName;
        Vector2 _returnGround;
        Vector2Int _returnCell;
        Vector2Int _originCell;
        Vector2Int _activeSpawnCell;
        Vector2Int _activeDungeonExitCell;
        Vector2Int _renderMin;
        Vector2Int _renderMax;
        int _activeDungeonTier;
        FoundationDungeonRoomMarker[] _activeDungeonRoomMarkers = Array.Empty<FoundationDungeonRoomMarker>();

        const int DefaultPocketSize = 10;
        const int TavernPocketSize = 22;
        const int LibraryPocketSize = 24;
        const int BoundaryPadding = 1;

        public bool IsInsideInstance => _inside;
        public string ActiveInstanceId => _inside ? _instanceId : "";
        public string ActiveDisplayName => _inside ? _displayName : "";
        public Vector2Int OriginCell => _originCell;
        public Vector2Int ReturnCell => _returnCell;
        public Vector2Int ActiveSpawnCell => _activeSpawnCell;
        public bool IsInsideDungeon => _inside && !string.IsNullOrWhiteSpace(_instanceId) &&
            _instanceId.StartsWith("dungeon_", StringComparison.Ordinal);
        public int ActiveDungeonTier => IsInsideDungeon ? _activeDungeonTier : 0;
        public Vector2Int ActiveDungeonExitCell => IsInsideDungeon ? _activeDungeonExitCell : default;
        public Vector2Int ActiveRenderMin => _renderMin;
        public Vector2Int ActiveRenderMax => _renderMax;

        public event Action<string, string> Entered;
        public event Action<string, string> Exited;

        public void Init(IsoWorld world, IsoFoundationPlayer player, FoundationContent content,
            FoundationInteractionOverlay overlay)
        {
            _world = world;
            _player = player;
            _content = content;
            _overlay = overlay;
        }

        public bool Enter(PlaceableDefinition entrance, Vector2Int entranceCell)
        {
            if (entrance == null || string.IsNullOrWhiteSpace(entrance.destinationId) ||
                _world == null || _player == null || _content == null)
                return false;

            _returnGround = _player.Ground;
            _returnCell = entranceCell;
            _instanceId = entrance.destinationId.Trim();
            _displayName = string.IsNullOrWhiteSpace(entrance.destinationDisplayName)
                ? entrance.Display
                : entrance.destinationDisplayName.Trim();
            _originCell = OriginFor(_instanceId);
            _inside = true;

            ClearInstanceObjects();
            BuildPocket(_instanceId, _displayName, _originCell);
            _player.SetCell(_activeSpawnCell.x, _activeSpawnCell.y);
            _overlay?.Flash($"Entered {_displayName}");
            Entered?.Invoke(_instanceId, _displayName);
            return true;
        }

        public bool EnterDungeon(FoundationDungeonBuild dungeon, Vector2Int returnCell)
        {
            return EnterDungeon(dungeon, returnCell, _player != null ? _player.Ground : Vector2.zero);
        }

        public bool EnterDungeon(FoundationDungeonBuild dungeon, Vector2Int returnCell, Vector2 returnGround)
        {
            if (dungeon == null || _world == null || _player == null)
                return false;

            _returnGround = returnGround;
            _returnCell = returnCell;
            _instanceId = dungeon.instanceId;
            _displayName = dungeon.displayName;
            _originCell = dungeon.spawnCell;
            _activeSpawnCell = dungeon.spawnCell;
            _activeDungeonExitCell = dungeon.exitCell;
            _activeDungeonTier = Mathf.Max(1, dungeon.tier);
            _activeDungeonRoomMarkers = dungeon.roomMarkers ?? Array.Empty<FoundationDungeonRoomMarker>();
            _renderMin = dungeon.renderMin;
            _renderMax = dungeon.renderMax;
            _activeRenderCells.Clear();
            AddDungeonRenderCells(dungeon);
            _inside = true;

            ClearInstanceObjects();
            _world.RestoreModifiedCells(dungeon.cells);
            SpawnDungeonDecorations(dungeon);
            _player.SetCell(dungeon.spawnCell.x, dungeon.spawnCell.y);
            _overlay?.Flash($"Entered {_displayName} - Tier {dungeon.tier}");
            Entered?.Invoke(_instanceId, _displayName);
            return true;
        }

        public bool Exit()
        {
            if (!_inside || _player == null) return false;

            string id = _instanceId;
            string display = _displayName;
            _player.SetGround(_returnGround);
            _inside = false;
            _instanceId = "";
            _displayName = "";
            _originCell = default;
            _activeSpawnCell = default;
            _activeDungeonExitCell = default;
            _activeDungeonTier = 0;
            _activeDungeonRoomMarkers = Array.Empty<FoundationDungeonRoomMarker>();
            _renderMin = default;
            _renderMax = default;
            _activeRenderCells.Clear();
            ClearInstanceObjects();

            _overlay?.Flash($"Returned from {display}");
            Exited?.Invoke(id, display);
            return true;
        }

        public FoundationSavedInstance CaptureState()
        {
            return new FoundationSavedInstance
            {
                active = _inside,
                instanceId = _instanceId,
                displayName = _displayName,
                originX = _originCell.x,
                originY = _originCell.y,
                returnCellX = _returnCell.x,
                returnCellY = _returnCell.y,
                returnGroundX = _returnGround.x,
                returnGroundY = _returnGround.y,
            };
        }

        public void RestoreState(FoundationSavedInstance state)
        {
            _inside = false;
            _instanceId = "";
            _displayName = "";
            _originCell = default;
            _activeSpawnCell = default;
            _activeDungeonExitCell = default;
            _activeDungeonTier = 0;
            _activeDungeonRoomMarkers = Array.Empty<FoundationDungeonRoomMarker>();
            _renderMin = default;
            _renderMax = default;
            _activeRenderCells.Clear();

            if (!state.active || string.IsNullOrWhiteSpace(state.instanceId) || _world == null)
                return;

            _inside = true;
            _instanceId = state.instanceId.Trim();
            _displayName = string.IsNullOrWhiteSpace(state.displayName) ? _instanceId : state.displayName.Trim();
            _originCell = state.originX != 0 || state.originY != 0
                ? new Vector2Int(state.originX, state.originY)
                : OriginFor(_instanceId);
            _returnCell = new Vector2Int(state.returnCellX, state.returnCellY);
            _returnGround = new Vector2(state.returnGroundX, state.returnGroundY);
            BuildPocket(_instanceId, _displayName, _originCell);
        }

        public bool CopyActiveRenderCells(List<Vector2Int> cells)
        {
            if (!_inside || cells == null || _activeRenderCells.Count == 0)
                return false;

            cells.Clear();
            cells.AddRange(_activeRenderCells);
            return true;
        }

        public FoundationDungeonRoomMarker[] SnapshotActiveDungeonRoomMarkers()
        {
            if (!IsInsideDungeon || _activeDungeonRoomMarkers == null || _activeDungeonRoomMarkers.Length == 0)
                return Array.Empty<FoundationDungeonRoomMarker>();

            var result = new FoundationDungeonRoomMarker[_activeDungeonRoomMarkers.Length];
            Array.Copy(_activeDungeonRoomMarkers, result, result.Length);
            return result;
        }

        public bool TryGetDecorationUnderCursor(Camera cam, Vector2 screenPosition, out FoundationInstanceDecoration decoration)
        {
            decoration = null;
            if (!_inside || cam == null || _decorations.Count == 0)
                return false;

            var wp = cam.ScreenToWorldPoint(screenPosition);
            var point = new Vector2(wp.x, wp.y);
            int bestOrder = int.MinValue;
            foreach (var d in _decorations)
            {
                if (d == null || !d.Contains(point))
                    continue;

                int order = d.SortingOrder;
                if (decoration != null && order < bestOrder)
                    continue;

                decoration = d;
                bestOrder = order;
            }

            return decoration != null;
        }

        public bool IsExitDecoration(FoundationInstanceDecoration decoration) =>
            decoration != null && decoration.IsExitPortal;

        void BuildPocket(string instanceId, string displayName, Vector2Int origin)
        {
            _activeRenderCells.Clear();
            _activeSpawnCell = new Vector2Int(origin.x, origin.y - PocketHalf(instanceId) + 2);

            if (IsTavern(instanceId))
            {
                BuildInteriorLayout(FoundationInteriorLayout.TavernHearthSnug(), origin);
                return;
            }

            bool structuredRoom = HasStructuredRoomWalls(instanceId);
            bool library = IsLibrary(instanceId);
            var floor = library
                ? _content.Blocks.Get("stone_path") ?? _content.Blocks.Get("wood_floor")
                : _content.Blocks.Get("wood_floor") ?? _content.Blocks.Get("stone_path");
            string floorId = floor != null ? floor.id : "grass_1";
            int pocketSize = PocketSizeFor(instanceId);
            int pocketHalf = pocketSize / 2;
            _renderMin = new Vector2Int(origin.x - pocketHalf, origin.y - pocketHalf);
            _renderMax = new Vector2Int(_renderMin.x + pocketSize - 1, _renderMin.y + pocketSize - 1);

            int totalSize = pocketSize + BoundaryPadding * 2;
            var cells = new FoundationSavedCell[totalSize * totalSize];
            int n = 0;

            for (int y = _renderMin.y - BoundaryPadding; y <= _renderMax.y + BoundaryPadding; y++)
            for (int x = _renderMin.x - BoundaryPadding; x <= _renderMax.x + BoundaryPadding; x++)
            {
                bool boundary = x < _renderMin.x || x > _renderMax.x ||
                    y < _renderMin.y || y > _renderMax.y;
                bool roomWall = structuredRoom && !boundary &&
                    (x == _renderMin.x || x == _renderMax.x || y == _renderMax.y);
                bool doorway = structuredRoom && !boundary &&
                    x == origin.x && y == _renderMin.y;
                if (doorway)
                    roomWall = false;

                bool visibleFloor = !boundary && !roomWall;
                if (visibleFloor)
                    _activeRenderCells.Add(new Vector2Int(x, y));

                cells[n++] = new FoundationSavedCell
                {
                    x = x,
                    y = y,
                    height = 0,
                    biomeIndex = 0,
                    surfaceBlockId = boundary ? "stone_block" : floorId,
                    occupantId = roomWall ? "interior_wall" : null,
                    nodeId = null,
                    solidBlock = boundary,
                    water = false,
                    occupantBlocks = roomWall,
                    nodeBlocks = false,
                    underBlockId = boundary ? floorId : null,
                    underHeight = 0,
                };
            }

            _world.RestoreModifiedCells(cells);
            if (library)
            {
                SpawnLibraryWallDressing(origin);
                SpawnLibraryDecorations(origin);
            }
            SpawnExitPortal(origin, pocketHalf);
        }

        void BuildInteriorLayout(FoundationInteriorLayout layout, Vector2Int origin)
        {
            if (layout == null || _world == null || _content == null)
                return;

            if (!FoundationInteriorLayoutValidator.TryValidate(layout, out string validation))
                Debug.LogWarning($"[FoundationInstanceSystem] Interior layout '{layout.id}' validation failed: {validation}");

            var floor = _content.Blocks.Get(layout.floorBlockId) ??
                _content.Blocks.Get("wood_floor") ??
                _content.Blocks.Get("stone_path");
            string floorId = floor != null ? floor.id : "grass_1";

            var worldFloor = new HashSet<Vector2Int>();
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (var rel in layout.floorTiles)
            {
                var world = layout.ToWorld(origin, rel);
                worldFloor.Add(world);
                minX = Mathf.Min(minX, world.x);
                minY = Mathf.Min(minY, world.y);
                maxX = Mathf.Max(maxX, world.x);
                maxY = Mathf.Max(maxY, world.y);
            }

            if (worldFloor.Count == 0)
                return;

            int floorWidth = maxX - minX + 1;
            int floorHeight = maxY - minY + 1;
            int renderWidth = Mathf.Max(PocketSizeFor(_instanceId), floorWidth);
            int renderHeight = Mathf.Max(PocketSizeFor(_instanceId), floorHeight);
            int padX = renderWidth - floorWidth;
            int padY = renderHeight - floorHeight;
            _renderMin = new Vector2Int(minX - padX / 2, minY - padY / 2);
            _renderMax = new Vector2Int(maxX + (padX - padX / 2), maxY + (padY - padY / 2));
            _activeSpawnCell = layout.ToWorld(origin, layout.spawnCell);
            var exitCell = layout.ToWorld(origin, layout.exitCell);

            for (int y = _renderMin.y; y <= _renderMax.y; y++)
            for (int x = _renderMin.x; x <= _renderMax.x; x++)
                _activeRenderCells.Add(new Vector2Int(x, y));

            var cells = new List<FoundationSavedCell>(_activeRenderCells.Count);
            foreach (var c in _activeRenderCells)
            {
                bool isFloor = worldFloor.Contains(c);
                cells.Add(new FoundationSavedCell
                {
                    x = c.x,
                    y = c.y,
                    height = 0,
                    biomeIndex = 0,
                    surfaceBlockId = isFloor ? floorId : "stone_block",
                    occupantId = null,
                    nodeId = null,
                    solidBlock = !isFloor,
                    water = false,
                    occupantBlocks = false,
                    nodeBlocks = false,
                    underBlockId = isFloor ? null : floorId,
                    underHeight = 0,
                });
            }

            _world.RestoreModifiedCells(cells.ToArray());
            SpawnLayoutWallDressing(layout, origin);
            SpawnLayoutProps(layout, origin);
            SpawnInteriorProp(FoundationDungeonSpriteResolver.Portal(),
                exitCell.x, exitCell.y, 0.02f, "Exit", false, true, 1, 1);
        }

        void AddDungeonRenderCells(FoundationDungeonBuild dungeon)
        {
            if (dungeon == null)
                return;

            if (dungeon.renderCells != null && dungeon.renderCells.Length > 0)
            {
                _activeRenderCells.AddRange(dungeon.renderCells);
                return;
            }

            if (dungeon.cells == null)
                return;

            foreach (var cell in dungeon.cells)
            {
                if (cell.solidBlock || cell.water || cell.occupantBlocks || cell.nodeBlocks)
                    continue;

                _activeRenderCells.Add(new Vector2Int(cell.x, cell.y));
            }
        }

        void SpawnLayoutWallDressing(FoundationInteriorLayout layout, Vector2Int origin)
        {
            foreach (var rel in layout.floorTiles)
            {
                var world = layout.ToWorld(origin, rel);
                if (!layout.floorTiles.Contains(new Vector2Int(rel.x, rel.y + 1)))
                    SpawnWallStack(world.x, world.y, $"{layout.displayName} north wall", layout.wallStackLevels);
                if (!layout.floorTiles.Contains(new Vector2Int(rel.x - 1, rel.y)))
                    SpawnWallStack(world.x, world.y, $"{layout.displayName} west wall", layout.wallStackLevels);
                if (!layout.floorTiles.Contains(new Vector2Int(rel.x + 1, rel.y)) && rel.y > layout.spawnCell.y - 5)
                    SpawnWallStack(world.x, world.y, $"{layout.displayName} east wall", layout.wallStackLevels);
            }
        }

        void SpawnLayoutProps(FoundationInteriorLayout layout, Vector2Int origin)
        {
            foreach (var prop in layout.props)
            {
                if (prop == null) continue;
                var sprite = FoundationInteriorSpriteResolver.DecorV2(prop.spriteKey);
                var cell = layout.ToWorld(origin, prop.cell);
                int sortOffset = prop.sortingOffset;
                if (prop.anchor == FoundationInteriorAnchorKind.Rug)
                    sortOffset -= 8;
                else if (prop.anchor == FoundationInteriorAnchorKind.Wall)
                    sortOffset += 1;

                SpawnInteriorProp(sprite, cell.x, cell.y, prop.yOffset, prop.displayName,
                    prop.blocksMovement, false, prop.footprintW, prop.footprintH, sortOffset);
            }
        }

        static bool IsTavern(string instanceId) =>
            !string.IsNullOrWhiteSpace(instanceId) &&
            instanceId.IndexOf("tavern", StringComparison.OrdinalIgnoreCase) >= 0;

        static bool IsLibrary(string instanceId) =>
            !string.IsNullOrWhiteSpace(instanceId) &&
            instanceId.IndexOf("library", StringComparison.OrdinalIgnoreCase) >= 0;

        static bool HasStructuredRoomWalls(string instanceId) => IsTavern(instanceId) || IsLibrary(instanceId);

        static int PocketSizeFor(string instanceId) =>
            IsTavern(instanceId) ? TavernPocketSize :
            IsLibrary(instanceId) ? LibraryPocketSize :
            DefaultPocketSize;
        static int PocketHalf(string instanceId) => PocketSizeFor(instanceId) / 2;

        void SpawnWallStack(int wx, int wy, string label, int levels)
        {
            var sprite = FoundationInteriorSpriteResolver.WallBlock();
            if (sprite == null) return;

            int count = Mathf.Max(1, levels);
            for (int level = 0; level < count; level++)
            {
                float yOffset = 0.04f + level * 0.22f;
                SpawnInteriorProp(sprite, wx, wy, yOffset, label, false, false, 1, 1, level);
            }
        }

        void SpawnLibraryWallDressing(Vector2Int origin)
        {
            for (int x = _renderMin.x; x <= _renderMax.x; x++)
                SpawnWallStack(x, _renderMax.y, "Library north wall", 3);

            for (int y = _renderMin.y + 1; y <= _renderMax.y - 1; y++)
            {
                SpawnWallStack(_renderMin.x, y, "Library west wall", 3);
                SpawnWallStack(_renderMax.x, y, "Library east wall", 3);
            }
        }

        void SpawnLibraryDecorations(Vector2Int origin)
        {
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("floorCarpet_N"),
                origin.x, origin.y - 1, -0.10f, "Reading carpet", false, false, 1, 1);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("floorCarpetSmall_S"),
                origin.x, origin.y - 7, -0.10f, "Entrance carpet", false, false, 1, 1);

            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("bookcaseWideBooks_S"),
                origin.x - 7, origin.y + 10, -0.04f, "Bookcase", true, false, 3, 1);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("bookcaseWideBooksLadder_S"),
                origin.x, origin.y + 10, -0.04f, "Ladder bookcase", true, false, 3, 1);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("bookcaseWideBooks_S"),
                origin.x + 7, origin.y + 10, -0.04f, "Bookcase", true, false, 3, 1);

            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("bookcaseWideBooks_E"),
                origin.x - 10, origin.y + 4, -0.04f, "Bookcase", true, false, 1, 3);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("bookcaseBooks_E"),
                origin.x - 10, origin.y - 2, -0.04f, "Bookcase", true, false, 1, 2);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("bookcaseWideBooks_W"),
                origin.x + 10, origin.y + 4, -0.04f, "Bookcase", true, false, 1, 3);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("bookcaseBooks_S"),
                origin.x + 7, origin.y - 8, -0.04f, "Low bookcase", true, false, 2, 1);

            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("longTableDecoratedChairsBooks_N"),
                origin.x - 3, origin.y + 2, -0.05f, "Reading table", true, false, 4, 2);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("longTableDecoratedChairsBooks_S"),
                origin.x + 4, origin.y - 3, -0.05f, "Reading table", true, false, 4, 2);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("longTableLarge_N"),
                origin.x + 3, origin.y + 5, -0.05f, "Archive table", true, false, 4, 2);

            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("displayCaseBooks_S"),
                origin.x - 6, origin.y - 8, -0.05f, "Book display", true, false, 2, 1);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("displayCaseOpen_N"),
                origin.x, origin.y + 7, -0.05f, "Glass display", true, false, 2, 1);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("bookStand_S"),
                origin.x - 1, origin.y - 8, -0.05f, "Reading stand", true, false, 1, 1);
            SpawnInteriorProp(FoundationInteriorSpriteResolver.LibraryProp("candleStandDouble_S"),
                origin.x + 9, origin.y - 7, -0.02f, "Candle stand", false, false, 1, 1);
        }

        void SpawnExitPortal(Vector2Int origin, int pocketHalf)
        {
            var sprite = FoundationDungeonSpriteResolver.Portal();
            SpawnInteriorProp(sprite, origin.x, origin.y - pocketHalf + 1, 0.02f, "Exit", false, true, 1, 1);
        }

        FoundationInstanceDecoration SpawnInteriorProp(Sprite sprite, int wx, int wy, float yOffset, string label,
            bool blocksMovement = false, bool exitPortal = false, int footprintW = 1, int footprintH = 1,
            int sortingOffset = 0)
        {
            if (sprite == null || _world == null)
                return null;

            var go = new GameObject($"{label}_{wx}_{wy}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteAmbient.Material;
            sr.sprite = sprite;
            int h = _world.GetHeight(wx, wy);
            var pos = IsoGrid.CellToWorld(wx, wy, h);
            pos.y += yOffset;
            go.transform.position = pos;
            sr.sortingOrder = IsoGrid.SortingOrder(wx, wy, h, IsoGrid.LayerProp) + sortingOffset;
            var deco = go.AddComponent<FoundationInstanceDecoration>();
            deco.Init(label, wx, wy, sr, exitPortal);
            if (exitPortal)
                AttachPortalVisual(go, sr, _activeDungeonTier > 0 ? _activeDungeonTier : 1, 0.92f);
            bool isWall = !string.IsNullOrWhiteSpace(label) &&
                label.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0;
            FoundationDepthPolish.Attach(go,
                fadeWhenOccluding: blocksMovement && !isWall,
                castLongShadow: blocksMovement && !exitPortal && !isWall,
                contactScale: Mathf.Clamp(footprintW * 0.55f, 0.55f, 1.8f),
                contactAlpha: exitPortal ? 0.34f : 0.24f);
            _decorations.Add(deco);
            _instanceObjects.Add(go);
            if (blocksMovement)
                BlockFootprint(wx, wy, Mathf.Max(1, footprintW), Mathf.Max(1, footprintH));
            return deco;
        }

        void BlockFootprint(int centerX, int centerY, int width, int height)
        {
            int minX = centerX - width / 2;
            int minY = centerY - height / 2;
            for (int y = minY; y < minY + height; y++)
            for (int x = minX; x < minX + width; x++)
            {
                if (_world.TryPlaceOccupant(x, y, "interior_prop", true))
                    _blockedDecorationCells.Add(new Vector2Int(x, y));
            }
        }

        void SpawnDungeonDecorations(FoundationDungeonBuild dungeon)
        {
            if (dungeon.decorations == null) return;
            foreach (var d in dungeon.decorations)
            {
                bool isExit = string.Equals(d.spriteKey, FoundationDungeonDecoration.ExitPortalSpriteKey,
                    StringComparison.Ordinal);
                var sprite = isExit
                    ? FoundationDungeonSpriteResolver.Portal()
                    : FoundationDungeonSpriteResolver.Decoration(d.spriteKey);
                if (sprite == null) continue;

                var go = new GameObject($"DungeonDecor_{d.spriteKey}_{d.x}_{d.y}");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = SpriteAmbient.Material;
                sr.sprite = sprite;
                int h = _world.GetHeight(d.x, d.y);
                var pos = IsoGrid.CellToWorld(d.x, d.y, h);
                pos.y += d.yOffset;
                go.transform.position = pos;
                sr.sortingOrder = IsoGrid.SortingOrder(d.x, d.y, h, IsoGrid.LayerProp);
                var deco = go.AddComponent<FoundationInstanceDecoration>();
                deco.Init(isExit ? "Dungeon Exit" : "Dungeon Decoration", d.x, d.y, sr, false, isExit, false);
                if (isExit)
                    AttachPortalVisual(go, sr, _activeDungeonTier > 0 ? _activeDungeonTier : 1, 0.98f);
                FoundationDepthPolish.Attach(go,
                    fadeWhenOccluding: !isExit,
                    castLongShadow: !isExit,
                    contactScale: isExit ? 0.95f : 0.65f,
                    contactAlpha: isExit ? 0.34f : 0.22f);
                _decorations.Add(deco);
                _instanceObjects.Add(go);
            }
        }

        static void AttachPortalVisual(GameObject go, SpriteRenderer renderer, int tier, float visualScale)
        {
            if (go == null || renderer == null)
                return;

            var visual = go.GetComponent<FoundationPortalVisual>();
            if (visual == null)
                visual = go.AddComponent<FoundationPortalVisual>();
            visual.Init(renderer, tier, FoundationPortalVisual.ColorForTier(tier), visualScale);
        }

        void ClearInstanceObjects()
        {
            if (_world != null)
            {
                foreach (var c in _blockedDecorationCells)
                    _world.ClearOccupant(c.x, c.y);
            }
            _blockedDecorationCells.Clear();
            _decorations.Clear();
            foreach (var go in _instanceObjects)
            {
                if (!go) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
            }
            _instanceObjects.Clear();
        }

        static Vector2Int OriginFor(string instanceId)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < instanceId.Length; i++)
                    hash = hash * 31 + instanceId[i];

                int bucketX = Mathf.Abs(hash % 64);
                int bucketY = Mathf.Abs((hash / 64) % 64);
                return new Vector2Int(20000 + bucketX * 32, 20000 + bucketY * 32);
            }
        }
    }
}

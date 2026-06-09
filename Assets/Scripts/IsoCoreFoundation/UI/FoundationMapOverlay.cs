using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    public sealed class FoundationMapOverlay : MonoBehaviour
    {
        IsoWorld _world;
        IsoFoundationPlayer _player;
        FoundationInstanceSystem _instances;
        FoundationDungeonPortalSystem _portals;
        readonly HashSet<long> _explored = new();
        readonly List<Vector2Int> _activeInstanceCells = new();

        Rect _miniRect;
        Rect _largeRect;
        string _layoutDragId;
        int _layoutDragMode;
        Rect _layoutStartRect;
        Vector2 _layoutStartMouse;
        bool _layoutLoaded;

        float _scanTimer;
        bool _largeOpen;
        Texture2D _pixel;
        float _largeZoom = 1f;
        Vector2 _largePan;
        bool _panningLarge;
        Vector2 _panStartMouse;
        Vector2 _panStart;

        const int ExploreRadius = 11;
        const float ScanInterval = 0.20f;
        const string PrefPrefix = "map.layout.";

        public void Init(IsoWorld world, IsoFoundationPlayer player, FoundationInstanceSystem instances,
            FoundationDungeonPortalSystem portals = null)
        {
            _world = world;
            _player = player;
            _instances = instances;
            _portals = portals;
            _pixel = Texture2D.whiteTexture;
            Scan();
        }

        public FoundationSavedMapCell[] SnapshotExploredCells()
        {
            var cells = new FoundationSavedMapCell[_explored.Count];
            int i = 0;
            foreach (long key in _explored)
            {
                DecodeKey(key, out int x, out int y);
                cells[i++] = new FoundationSavedMapCell(x, y);
            }
            return cells;
        }

        public void RestoreExploredCells(FoundationSavedMapCell[] cells)
        {
            _explored.Clear();
            if (cells != null)
                for (int i = 0; i < cells.Length; i++)
                    _explored.Add(Key(cells[i].x, cells[i].y));
            Scan();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
            {
                var ui = FoundationUiCoordinator.Active;
                if (ui == null || _largeOpen || ui.CanToggleMap())
                {
                    _largeOpen = !_largeOpen;
                    ui?.SetModalOpen("map", _largeOpen);
                    ui?.ConsumeInputThisFrame();
                }
            }

            if ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                Input.GetKeyDown(KeyCode.R))
            {
                ResetMapLayout();
            }

            _scanTimer += Time.deltaTime;
            if (_scanTimer >= ScanInterval)
            {
                _scanTimer = 0f;
                Scan();
            }
        }

        void OnDestroy()
        {
            FoundationUiCoordinator.Active?.SetModalOpen("map", false);
        }

        void Scan()
        {
            if (_world == null || _player == null)
                return;

            var c = _player.CurrentCell;
            for (int y = c.y - ExploreRadius; y <= c.y + ExploreRadius; y++)
            for (int x = c.x - ExploreRadius; x <= c.x + ExploreRadius; x++)
                if ((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y) <= ExploreRadius * ExploreRadius)
                    _explored.Add(Key(x, y));
        }

        void OnGUI()
        {
            if (_world == null || _player == null || _pixel == null)
                return;

            var oldMatrix = GUI.matrix;
            float scale = FoundationUiCoordinator.UiScale;
            if (Mathf.Abs(scale - 1f) > 0.01f)
                GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            EnsureLayout(sw, sh);

            if (ShouldShowMiniMap(FoundationUiCoordinator.CurrentHudViewMode))
            {
                HandleLayout(ref _miniRect, "mini", new Vector2(132f, 132f), new Vector2(420f, 420f));
                DrawMiniMap(_miniRect);
            }

            if (_largeOpen)
            {
                HandleLayout(ref _largeRect, "large", new Vector2(520f, 360f), new Vector2(sw - 40f, sh - 40f));
                DrawLargeMap(_largeRect);
            }

            GUI.matrix = oldMatrix;
        }

        void EnsureLayout(float sw, float sh)
        {
            if (_layoutLoaded)
                return;

            _miniRect = new Rect(sw - 226f, 30f, 190f, 190f);
            _largeRect = new Rect(80f, 64f, sw - 160f, sh - 128f);
            _largeZoom = PlayerPrefs.GetFloat(PrefPrefix + "large.zoom", 1f);
            _largePan = new Vector2(
                PlayerPrefs.GetFloat(PrefPrefix + "large.panX", 0f),
                PlayerPrefs.GetFloat(PrefPrefix + "large.panY", 0f));
            LoadRect("mini", ref _miniRect);
            LoadRect("large", ref _largeRect);
            _layoutLoaded = true;
        }

        static bool ShouldShowMiniMap(FoundationHudViewMode mode)
        {
            return mode == FoundationHudViewMode.Adventure;
        }

        void LoadRect(string id, ref Rect rect)
        {
            string key = PrefPrefix + id + ".";
            if (!PlayerPrefs.HasKey(key + "x"))
                return;

            rect.x = PlayerPrefs.GetFloat(key + "x", rect.x);
            rect.y = PlayerPrefs.GetFloat(key + "y", rect.y);
            rect.width = PlayerPrefs.GetFloat(key + "w", rect.width);
            rect.height = PlayerPrefs.GetFloat(key + "h", rect.height);
        }

        void SaveRect(string id, Rect rect)
        {
            string key = PrefPrefix + id + ".";
            PlayerPrefs.SetFloat(key + "x", rect.x);
            PlayerPrefs.SetFloat(key + "y", rect.y);
            PlayerPrefs.SetFloat(key + "w", rect.width);
            PlayerPrefs.SetFloat(key + "h", rect.height);
            PlayerPrefs.Save();
        }

        void SaveLargeView()
        {
            PlayerPrefs.SetFloat(PrefPrefix + "large.zoom", _largeZoom);
            PlayerPrefs.SetFloat(PrefPrefix + "large.panX", _largePan.x);
            PlayerPrefs.SetFloat(PrefPrefix + "large.panY", _largePan.y);
            PlayerPrefs.Save();
        }

        void ResetMapLayout()
        {
            string[] ids = { "mini", "large" };
            foreach (var id in ids)
            {
                string key = PrefPrefix + id + ".";
                PlayerPrefs.DeleteKey(key + "x");
                PlayerPrefs.DeleteKey(key + "y");
                PlayerPrefs.DeleteKey(key + "w");
                PlayerPrefs.DeleteKey(key + "h");
            }
            PlayerPrefs.DeleteKey(PrefPrefix + "large.zoom");
            PlayerPrefs.DeleteKey(PrefPrefix + "large.panX");
            PlayerPrefs.DeleteKey(PrefPrefix + "large.panY");
            PlayerPrefs.Save();
            _layoutLoaded = false;
        }

        void HandleLayout(ref Rect rect, string id, Vector2 minSize, Vector2 maxSize)
        {
            var e = Event.current;
            if (e == null)
                return;

            bool alt = e.alt || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            Rect move = new Rect(rect.x, rect.y, rect.width, 28f);
            Rect resize = new Rect(rect.xMax - 26f, rect.yMax - 26f, 26f, 26f);

            if (alt && e.type == EventType.MouseDown && e.button == 0 &&
                (resize.Contains(e.mousePosition) || move.Contains(e.mousePosition)))
            {
                _layoutDragId = id;
                _layoutDragMode = resize.Contains(e.mousePosition) ? 2 : 1;
                _layoutStartRect = rect;
                _layoutStartMouse = e.mousePosition;
                e.Use();
            }

            if (_layoutDragId == id && e.type == EventType.MouseDrag)
            {
                Vector2 d = e.mousePosition - _layoutStartMouse;
                if (_layoutDragMode == 1)
                {
                    rect.x = _layoutStartRect.x + d.x;
                    rect.y = _layoutStartRect.y + d.y;
                }
                else if (_layoutDragMode == 2)
                {
                    rect.width = Mathf.Clamp(_layoutStartRect.width + d.x, minSize.x, maxSize.x);
                    rect.height = Mathf.Clamp(_layoutStartRect.height + d.y, minSize.y, maxSize.y);
                }
                e.Use();
            }

            if (_layoutDragId == id && e.type == EventType.MouseUp)
            {
                _layoutDragId = null;
                _layoutDragMode = 0;
                SaveRect(id, rect);
                e.Use();
            }

            if (alt)
            {
                DrawRect(move, new Color(1f, 1f, 1f, 0.035f));
                DrawRect(resize, new Color(1f, 0.86f, 0.35f, 0.25f));
            }
        }

        void DrawMiniMap(Rect r)
        {
            DrawPanel(r, new Color(0.03f, 0.035f, 0.045f, 0.88f));
            GUI.Label(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, 18f),
                IsDungeonMap ? $"Dungeon T{_instances.ActiveDungeonTier}" : "Map");
            var body = new Rect(r.x + 10f, r.y + 30f, r.width - 20f, r.height - 56f);
            if (IsDungeonMap)
                DrawDungeonLocalMapCells(body, 30);
            else
                DrawLocalMapCells(body, 26);
            GUI.Label(new Rect(r.x + 10f, r.y + r.height - 24f, r.width - 20f, 18f), "M map | Alt drag/resize");
        }

        void DrawLargeMap(Rect r)
        {
            DrawPanel(r, new Color(0.025f, 0.025f, 0.032f, 0.96f));
            GUI.Label(new Rect(r.x + 18f, r.y + 10f, r.width - 36f, 24f),
                IsDungeonMap
                    ? $"Dungeon Map - {_instances.ActiveDisplayName} T{_instances.ActiveDungeonTier}   M close | drag map to pan | wheel zoom | Alt drag/resize"
                    : "Explored Map   M close | drag map to pan | wheel zoom | Alt drag/resize HUD/map | Alt+Shift+R reset layout");

            Rect body = new Rect(r.x + 18f, r.y + 42f, r.width - 220f, r.height - 62f);
            Rect legend = new Rect(body.xMax + 16f, body.y, 184f, body.height);
            if (IsDungeonMap)
                DrawDungeonMapCells(body);
            else
                DrawExploredMapCells(body);
            DrawLegend(legend);
        }

        void DrawLocalMapCells(Rect r, int radius)
        {
            var p = _player.CurrentCell;
            int minX = p.x - radius, maxX = p.x + radius;
            int minY = p.y - radius, maxY = p.y + radius;
            float cell = Mathf.Min(r.width / (radius * 2 + 1), r.height / (radius * 2 + 1));
            float ox = r.x + (r.width - (radius * 2 + 1) * cell) * 0.5f;
            float oy = r.y + (r.height - (radius * 2 + 1) * cell) * 0.5f;

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                if (!IsExplored(x, y))
                    continue;
                var rect = CellRect(x, y, minX, maxY, cell, ox, oy);
                DrawRect(rect, CellColor(x, y));
                DrawCellMarkers(x, y, rect, cell, false);
            }

            DrawMarker(CellRect(p.x, p.y, minX, maxY, cell, ox, oy), new Color(1f, 0.88f, 0.25f, 1f), cell + 3f);
        }

        void DrawDungeonLocalMapCells(Rect r, int radius)
        {
            if (!CopyActiveInstanceCells())
                return;

            var p = _player.CurrentCell;
            int minX = p.x - radius, maxX = p.x + radius;
            int minY = p.y - radius, maxY = p.y + radius;
            float cell = Mathf.Min(r.width / (radius * 2 + 1), r.height / (radius * 2 + 1));
            float ox = r.x + (r.width - (radius * 2 + 1) * cell) * 0.5f;
            float oy = r.y + (r.height - (radius * 2 + 1) * cell) * 0.5f;

            foreach (var c in _activeInstanceCells)
            {
                if (c.x < minX || c.x > maxX || c.y < minY || c.y > maxY)
                    continue;

                var rect = CellRect(c.x, c.y, minX, maxY, cell, ox, oy);
                DrawRect(rect, CellColor(c.x, c.y));
                DrawCellMarkers(c.x, c.y, rect, cell, false);
            }

            DrawDungeonMarkers(minX, maxY, cell, ox, oy, r, false);
            DrawMarker(CellRect(p.x, p.y, minX, maxY, cell, ox, oy), new Color(1f, 0.88f, 0.25f, 1f), cell + 3f);
        }

        void DrawExploredMapCells(Rect r)
        {
            if (_explored.Count == 0)
                return;

            HandleLargeMapNavigation(r);
            GetExploredBounds(out int minX, out int minY, out int maxX, out int maxY);
            minX -= 8; minY -= 8; maxX += 8; maxY += 8;

            int width = Mathf.Max(1, maxX - minX + 1);
            int height = Mathf.Max(1, maxY - minY + 1);
            float fit = Mathf.Min(r.width / width, r.height / height);
            float cell = Mathf.Clamp(fit * _largeZoom, 2f, 18f);
            float mapW = width * cell;
            float mapH = height * cell;
            float ox = r.x + (r.width - mapW) * 0.5f + _largePan.x;
            float oy = r.y + (r.height - mapH) * 0.5f + _largePan.y;

            foreach (long key in _explored)
            {
                DecodeKey(key, out int x, out int y);
                var rect = CellRect(x, y, minX, maxY, cell, ox, oy);
                if (!Overlaps(rect, r))
                    continue;
                DrawRect(rect, CellColor(x, y));
                DrawCellMarkers(x, y, rect, cell, true);
            }

            DrawPortalMarkers(minX, maxY, cell, ox, oy, r);
            DrawMarker(CellRect(0, 0, minX, maxY, cell, ox, oy), new Color(1f, 1f, 1f, 1f), Mathf.Max(cell + 4f, 7f));
            var p = _player.CurrentCell;
            DrawMarker(CellRect(p.x, p.y, minX, maxY, cell, ox, oy), new Color(1f, 0.88f, 0.25f, 1f), Mathf.Max(cell + 6f, 9f));
        }

        void DrawDungeonMapCells(Rect r)
        {
            if (!CopyActiveInstanceCells())
                return;

            HandleLargeMapNavigation(r);
            GetActiveInstanceBounds(_activeInstanceCells, out int minX, out int minY, out int maxX, out int maxY);
            minX -= 5; minY -= 5; maxX += 5; maxY += 5;

            int width = Mathf.Max(1, maxX - minX + 1);
            int height = Mathf.Max(1, maxY - minY + 1);
            float fit = Mathf.Min(r.width / width, r.height / height);
            float cell = Mathf.Clamp(fit * _largeZoom, 2f, 18f);
            float mapW = width * cell;
            float mapH = height * cell;
            float ox = r.x + (r.width - mapW) * 0.5f + _largePan.x;
            float oy = r.y + (r.height - mapH) * 0.5f + _largePan.y;

            foreach (var c in _activeInstanceCells)
            {
                var rect = CellRect(c.x, c.y, minX, maxY, cell, ox, oy);
                if (!Overlaps(rect, r))
                    continue;

                DrawRect(rect, CellColor(c.x, c.y));
                DrawCellMarkers(c.x, c.y, rect, cell, true);
            }

            DrawDungeonMarkers(minX, maxY, cell, ox, oy, r, true);
            var p = _player.CurrentCell;
            DrawMarker(CellRect(p.x, p.y, minX, maxY, cell, ox, oy), new Color(1f, 0.88f, 0.25f, 1f), Mathf.Max(cell + 6f, 9f));
        }

        void HandleLargeMapNavigation(Rect r)
        {
            var e = Event.current;
            if (e == null || !r.Contains(e.mousePosition))
                return;

            bool alt = e.alt || Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (alt)
                return;

            if (e.type == EventType.ScrollWheel)
            {
                float before = _largeZoom;
                _largeZoom = Mathf.Clamp(_largeZoom * (e.delta.y > 0f ? 0.88f : 1.12f), 0.45f, 6f);
                if (!Mathf.Approximately(before, _largeZoom))
                    SaveLargeView();
                e.Use();
            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                _panningLarge = true;
                _panStartMouse = e.mousePosition;
                _panStart = _largePan;
                e.Use();
            }
            else if (_panningLarge && e.type == EventType.MouseDrag)
            {
                _largePan = _panStart + (e.mousePosition - _panStartMouse);
                e.Use();
            }
            else if (_panningLarge && e.type == EventType.MouseUp)
            {
                _panningLarge = false;
                SaveLargeView();
                e.Use();
            }
        }

        void DrawCellMarkers(int x, int y, Rect rect, float cell, bool large)
        {
            var c = _world.GetCell(x, y);
            if (c.HasNode)
                DrawMarker(rect, new Color(0.35f, 0.95f, 0.42f, 1f), Mathf.Max(cell, large ? 5f : 3f));
            if (c.HasOccupant)
                DrawMarker(rect, new Color(1f, 0.62f, 0.25f, 1f), Mathf.Max(cell, large ? 6f : 4f));
            if (_instances != null && _instances.IsInsideInstance && !_instances.IsInsideDungeon &&
                x == _instances.ReturnCell.x && y == _instances.ReturnCell.y)
                DrawMarker(rect, new Color(0.95f, 0.55f, 1f, 1f), Mathf.Max(cell + 3f, 7f));
        }

        void DrawDungeonMarkers(int minX, int maxY, float cell, float ox, float oy, Rect clip, bool large)
        {
            if (!IsDungeonMap)
                return;

            var markers = _instances.SnapshotActiveDungeonRoomMarkers();
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                var rect = CellRect(marker.x, marker.y, minX, maxY, cell, ox, oy);
                if (!Overlaps(rect, clip))
                    continue;

                float size = marker.kind == FoundationDungeonRoomKind.Arena
                    ? Mathf.Max(cell * 2.25f, large ? 11f : 6f)
                    : Mathf.Max(cell + 4f, large ? 8f : 5f);
                DrawMarker(rect, RoomColor(marker.kind), size);
                if (large && cell >= 5f)
                    GUI.Label(new Rect(rect.x + 4f, rect.y - 10f, 72f, 18f), RoomShortLabel(marker.kind));
            }

            var spawn = _instances.ActiveSpawnCell;
            var exit = _instances.ActiveDungeonExitCell;
            var spawnRect = CellRect(spawn.x, spawn.y, minX, maxY, cell, ox, oy);
            if (Overlaps(spawnRect, clip))
                DrawMarker(spawnRect, new Color(0.38f, 1f, 0.78f, 1f), Mathf.Max(cell + 8f, large ? 12f : 7f));
            var exitRect = CellRect(exit.x, exit.y, minX, maxY, cell, ox, oy);
            if (Overlaps(exitRect, clip))
                DrawMarker(exitRect, new Color(1f, 0.42f, 0.92f, 1f), Mathf.Max(cell + 8f, large ? 12f : 7f));
        }

        void DrawPortalMarkers(int minX, int maxY, float cell, float ox, float oy, Rect clip)
        {
            var portals = _portals != null ? _portals.SnapshotPortals() : null;
            if (portals == null)
                return;

            foreach (var portal in portals)
            {
                if (portal == null || !IsExplored(portal.Cell.x, portal.Cell.y))
                    continue;

                var rect = CellRect(portal.Cell.x, portal.Cell.y, minX, maxY, cell, ox, oy);
                if (!Overlaps(rect, clip))
                    continue;

                DrawMarker(rect, portal.Completed
                    ? new Color(0.45f, 0.45f, 0.48f, 1f)
                    : new Color(0.55f, 0.65f, 1f, 1f), Mathf.Max(cell + 5f, 8f));
            }
        }

        void DrawLegend(Rect r)
        {
            DrawPanel(r, new Color(0.035f, 0.04f, 0.052f, 0.92f));
            float y = r.y + 12f;
            GUI.Label(new Rect(r.x + 12f, y, r.width - 24f, 20f), "Legend"); y += 28f;
            LegendRow(r.x + 12f, ref y, new Color(1f, 0.88f, 0.25f, 1f), "Player");
            LegendRow(r.x + 12f, ref y, Color.white, "Spawn/Home");
            LegendRow(r.x + 12f, ref y, new Color(0.55f, 0.65f, 1f, 1f), "Portal");
            LegendRow(r.x + 12f, ref y, new Color(0.35f, 0.95f, 0.42f, 1f), "Resource");
            LegendRow(r.x + 12f, ref y, new Color(1f, 0.62f, 0.25f, 1f), "Building/Prop");
            if (IsDungeonMap)
            {
                LegendRow(r.x + 12f, ref y, new Color(0.38f, 1f, 0.78f, 1f), "Entrance");
                LegendRow(r.x + 12f, ref y, new Color(1f, 0.42f, 0.92f, 1f), "Exit");
                LegendRow(r.x + 12f, ref y, RoomColor(FoundationDungeonRoomKind.Arena), "Arena");
                LegendRow(r.x + 12f, ref y, RoomColor(FoundationDungeonRoomKind.Junction), "Junction");
            }
            LegendRow(r.x + 12f, ref y, new Color(0.18f, 0.38f, 0.72f, 1f), "Water");
            LegendRow(r.x + 12f, ref y, new Color(0.42f, 0.74f, 0.34f, 1f), "Biome");
            y += 16f;
            GUI.Label(new Rect(r.x + 12f, y, r.width - 24f, 78f),
                IsDungeonMap
                    ? $"Dungeon cells: {_activeInstanceCells.Count}\nRooms: {_instances.SnapshotActiveDungeonRoomMarkers().Length}\nZoom: {_largeZoom:0.00}x"
                    : $"Explored cells: {_explored.Count}\nZoom: {_largeZoom:0.00}x\nAlt+drag panels to author your layout.");
        }

        void LegendRow(float x, ref float y, Color color, string label)
        {
            DrawRect(new Rect(x, y + 4f, 14f, 14f), color);
            GUI.Label(new Rect(x + 22f, y, 140f, 22f), label);
            y += 24f;
        }

        void GetExploredBounds(out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = int.MaxValue; minY = int.MaxValue;
            maxX = int.MinValue; maxY = int.MinValue;

            foreach (long key in _explored)
            {
                DecodeKey(key, out int x, out int y);
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            var p = _player.CurrentCell;
            minX = Mathf.Min(minX, p.x, 0);
            minY = Mathf.Min(minY, p.y, 0);
            maxX = Mathf.Max(maxX, p.x, 0);
            maxY = Mathf.Max(maxY, p.y, 0);
        }

        Rect CellRect(int x, int y, int minX, int maxY, float cell, float ox, float oy)
        {
            return new Rect(ox + (x - minX) * cell, oy + (maxY - y) * cell, Mathf.Ceil(cell), Mathf.Ceil(cell));
        }

        bool IsExplored(int x, int y) =>
            _explored.Contains(Key(x, y)) || (_instances != null && _instances.IsInsideInstance);

        bool IsDungeonMap => _instances != null && _instances.IsInsideDungeon;

        bool CopyActiveInstanceCells()
        {
            _activeInstanceCells.Clear();
            return _instances != null && _instances.CopyActiveRenderCells(_activeInstanceCells) &&
                _activeInstanceCells.Count > 0;
        }

        Color CellColor(int x, int y)
        {
            var c = _world.GetCell(x, y);
            if (c.Water) return new Color(0.18f, 0.38f, 0.72f, 0.95f);
            if (c.SolidBlock) return new Color(0.43f, 0.43f, 0.47f, 0.95f);
            if (c.SurfaceBlockId == "wood_floor") return new Color(0.56f, 0.36f, 0.18f, 0.95f);
            if (!string.IsNullOrEmpty(c.SurfaceBlockId) &&
                c.SurfaceBlockId.StartsWith("dungeon_floor_", System.StringComparison.Ordinal))
                return new Color(0.30f, 0.34f, 0.40f, 0.95f);
            if (c.SurfaceBlockId == "stone_path") return new Color(0.55f, 0.55f, 0.58f, 0.95f);
            if (c.SurfaceBlockId == "soil") return new Color(0.35f, 0.20f, 0.10f, 0.95f);
            var biome = _world.GetBiome(x, y);
            return biome != null ? new Color(biome.debugTint.r, biome.debugTint.g, biome.debugTint.b, 0.95f)
                : new Color(0.30f, 0.55f, 0.24f, 0.95f);
        }

        void DrawPanel(Rect r, Color color)
        {
            DrawRect(r, color);
            DrawRect(new Rect(r.x, r.y, r.width, 2f), new Color(1f, 0.88f, 0.42f, 0.55f));
            DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), new Color(0f, 0f, 0f, 0.45f));
        }

        void DrawMarker(Rect cellRect, Color color, float size)
        {
            float s = Mathf.Max(3f, size);
            var r = new Rect(cellRect.center.x - s * 0.5f, cellRect.center.y - s * 0.5f, s, s);
            DrawRect(r, color);
            DrawRect(new Rect(r.x, r.y, r.width, 1f), Color.black);
            DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), Color.black);
        }

        void DrawRect(Rect r, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, _pixel);
            GUI.color = old;
        }

        static void GetActiveInstanceBounds(List<Vector2Int> cells, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = int.MaxValue; minY = int.MaxValue;
            maxX = int.MinValue; maxY = int.MinValue;

            foreach (var c in cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.x > maxX) maxX = c.x;
                if (c.y > maxY) maxY = c.y;
            }

            if (minX != int.MaxValue)
                return;

            minX = minY = maxX = maxY = 0;
        }

        static Color RoomColor(FoundationDungeonRoomKind kind)
        {
            switch (kind)
            {
                case FoundationDungeonRoomKind.Spawn: return new Color(0.38f, 1f, 0.78f, 1f);
                case FoundationDungeonRoomKind.Arena: return new Color(1f, 0.55f, 0.22f, 1f);
                case FoundationDungeonRoomKind.Junction: return new Color(0.75f, 0.62f, 1f, 1f);
                case FoundationDungeonRoomKind.Exit: return new Color(1f, 0.42f, 0.92f, 1f);
                default: return new Color(0.95f, 0.75f, 0.28f, 1f);
            }
        }

        static string RoomShortLabel(FoundationDungeonRoomKind kind)
        {
            switch (kind)
            {
                case FoundationDungeonRoomKind.Spawn: return "ENT";
                case FoundationDungeonRoomKind.Arena: return "ARENA";
                case FoundationDungeonRoomKind.Junction: return "JCT";
                case FoundationDungeonRoomKind.Exit: return "EXIT";
                default: return "ROOM";
            }
        }

        static bool Overlaps(Rect a, Rect b) =>
            a.xMax >= b.x && a.x <= b.xMax && a.yMax >= b.y && a.y <= b.yMax;

        static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;

        static void DecodeKey(long key, out int x, out int y)
        {
            x = (int)(key >> 32);
            y = (int)key;
        }
    }
}

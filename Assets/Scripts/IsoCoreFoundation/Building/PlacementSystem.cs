using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Mouse-targeted placement of blocks and placeables. Shows a ghost preview,
    /// validates against world occupancy, consumes the item, writes occupancy into
    /// the cell (so movement collision blocks against it), and spawns instances.
    /// </summary>
    public class PlacementSystem : MonoBehaviour
    {
        IsoWorld _world;
        FoundationContent _content;
        Inventory _inv;
        Hotbar _hotbar;
        StorageSystem _storage;
        Camera _cam;
        IsoFoundationPlayer _player;
        Transform _placeParent;
        SpriteRenderer _ghost;
        readonly List<SpriteRenderer> _footprintGhosts = new();
        Sprite _footprintGhostSprite;
        TextMesh _ghostLabel;
        MeshRenderer _ghostLabelRenderer;

        public event Action<ItemDefinition, int, int> Placed;
        public event Action<string, int, int> Removed;

        readonly List<PlaceableInstance> _placeables = new();
        readonly Dictionary<long, PlaceableInstance> _placeablesByCell = new();
        readonly Dictionary<string, string> _blockItemByBlockId = new();
        static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;

        public void Init(IsoWorld world, FoundationContent content, Inventory inv, Hotbar hotbar,
            Camera cam, IsoFoundationPlayer player, StorageSystem storage = null)
        {
            _world = world; _content = content; _inv = inv; _hotbar = hotbar; _cam = cam; _player = player;
            _storage = storage;

            _placeParent = new GameObject("Placeables").transform;
            _placeParent.SetParent(transform, false);

            var ghostGo = new GameObject("PlacementGhost");
            ghostGo.transform.SetParent(transform, false);
            _ghost = ghostGo.AddComponent<SpriteRenderer>();
            _ghost.enabled = false;

            var labelGo = new GameObject("PlacementGhostLabel");
            labelGo.transform.SetParent(transform, false);
            _ghostLabel = labelGo.AddComponent<TextMesh>();
            _ghostLabel.anchor = TextAnchor.MiddleCenter;
            _ghostLabel.alignment = TextAlignment.Center;
            _ghostLabel.fontSize = 32;
            _ghostLabel.characterSize = 0.035f;
            _ghostLabel.lineSpacing = 0.88f;
            _ghostLabel.text = "";
            _ghostLabelRenderer = labelGo.GetComponent<MeshRenderer>();
            labelGo.SetActive(false);

            _blockItemByBlockId.Clear();
            if (_content != null)
            {
                foreach (var it in _content.Items.All)
                {
                    if (it != null && it.PlacesBlock && !string.IsNullOrEmpty(it.placeBlockId) &&
                        !_blockItemByBlockId.ContainsKey(it.placeBlockId))
                        _blockItemByBlockId.Add(it.placeBlockId, it.id);
                }
            }
        }

        Vector2Int CursorCell()
        {
            var wp = _cam.ScreenToWorldPoint(Input.mousePosition);
            wp.z = 0f;
            // Refine for tile height: the rendered tile is lifted by height*HeightStep,
            // so a raw WorldToCell (height-0 plane) lands ~one cell off on raised terrain.
            var c = IsoGrid.WorldToCell(wp);
            int h = _world.GetHeight(c.x, c.y);
            if (h != 0) c = IsoGrid.WorldToCell(new Vector3(wp.x, wp.y - h * IsoGrid.HeightStep, 0f));
            return c;
        }

        ItemDefinition SelectedPlaceable()
        {
            var stack = _hotbar.SelectedStack;
            if (stack.IsEmpty) return null;
            var def = _content.Items.Get(stack.itemId);
            return (def != null && def.IsPlaceable) ? def : null;
        }

        void Update()
        {
            if (_world == null) return;
            var def = SelectedPlaceable();
            if (def == null || !_inv.Has(def.id, 1)) { HidePlacementPreview(); return; }

            var c = CursorCell();
            bool valid = CanPlace(def, c);
            int h = _world.GetHeight(c.x, c.y);

            if (def.PlacesPlaceable)
            {
                var placeable = _content.Placeables.Get(def.placeableId);
                if (placeable != null && placeable.HasMultiCellFootprint)
                {
                    UpdateFootprintGhost(placeable, c, valid);
                    return;
                }
            }

            HideFootprintGhosts();

            _ghost.enabled = true;
            _ghost.sprite = def.PlacesBlock
                ? PlaceholderArt.Diamond(BlockColor(def.placeBlockId))
                : PlaceholderArt.Box(PlaceableColor(def.placeableId), 0.8f, 1f);
            _ghost.transform.position = IsoGrid.CellToWorld(c.x, c.y, h);
            _ghost.sortingOrder = IsoGrid.SortingOrder(c.x, c.y, h, IsoGrid.LayerPreview);
            var col = valid ? new Color(0.4f, 1f, 0.4f, 0.6f) : new Color(1f, 0.35f, 0.35f, 0.6f);
            _ghost.color = col;
        }

        void HidePlacementPreview()
        {
            if (_ghost != null)
                _ghost.enabled = false;
            HideFootprintGhosts();
        }

        void HideFootprintGhosts()
        {
            for (int i = 0; i < _footprintGhosts.Count; i++)
                if (_footprintGhosts[i] != null)
                    _footprintGhosts[i].enabled = false;
            if (_ghostLabel != null)
                _ghostLabel.gameObject.SetActive(false);
        }

        void UpdateFootprintGhost(PlaceableDefinition placeable, Vector2Int anchor, bool valid)
        {
            if (_ghost != null)
                _ghost.enabled = false;

            var cells = FootprintCellsList(placeable, anchor.x, anchor.y);
            EnsureFootprintGhostCount(cells.Count);
            var col = valid ? new Color(0.30f, 0.95f, 0.45f, 0.48f) : new Color(1f, 0.25f, 0.25f, 0.50f);
            int labelOrder = int.MinValue;

            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                int h = _world.GetHeight(c.x, c.y);
                var sr = _footprintGhosts[i];
                sr.enabled = true;
                sr.sprite = _footprintGhostSprite;
                sr.color = col;
                sr.transform.position = IsoGrid.CellToWorld(c.x, c.y, h);
                sr.sortingOrder = IsoGrid.SortingOrder(c.x, c.y, h, IsoGrid.LayerPreview);
                labelOrder = Mathf.Max(labelOrder, sr.sortingOrder);
            }

            for (int i = cells.Count; i < _footprintGhosts.Count; i++)
                if (_footprintGhosts[i] != null)
                    _footprintGhosts[i].enabled = false;

            if (_ghostLabel != null)
            {
                int anchorHeight = _world.GetHeight(anchor.x, anchor.y);
                _ghostLabel.text = FootprintPreviewText(placeable);
                _ghostLabel.color = valid ? new Color(1f, 0.96f, 0.78f, 1f) : new Color(1f, 0.72f, 0.72f, 1f);
                _ghostLabel.transform.position = IsoGrid.CellToWorld(anchor.x, anchor.y, anchorHeight) +
                    new Vector3(0f, 1.05f, 0f);
                _ghostLabel.gameObject.SetActive(true);
                if (_ghostLabelRenderer != null)
                    _ghostLabelRenderer.sortingOrder = labelOrder + 4;
            }
        }

        void EnsureFootprintGhostCount(int count)
        {
            if (_footprintGhostSprite == null)
                _footprintGhostSprite = PlaceholderArt.Diamond(Color.white);

            while (_footprintGhosts.Count < count)
            {
                var go = new GameObject($"PlacementFootprint_{_footprintGhosts.Count}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.enabled = false;
                _footprintGhosts.Add(sr);
            }
        }

        string FootprintPreviewText(PlaceableDefinition placeable)
        {
            string label = placeable != null ? placeable.FootprintDisplay : "Placement";
            if (placeable == null)
                return label;

            string size = $"{placeable.FootprintWidth}x{placeable.FootprintHeight}";
            if (placeable.IsConstructionPlot)
                return $"{label}\n{size} build plot\n{ConstructionCostText(placeable)}";
            return $"{label}\n{size}";
        }

        string ConstructionCostText(PlaceableDefinition placeable)
        {
            if (placeable == null || placeable.constructionCost == null || placeable.constructionCost.Length == 0)
                return "ready";

            var parts = new List<string>();
            foreach (var cost in placeable.constructionCost)
            {
                if (cost.count <= 0 || string.IsNullOrWhiteSpace(cost.itemId))
                    continue;

                var item = _content != null ? _content.Items.Get(cost.itemId) : null;
                parts.Add($"{DisplayName(item, cost.itemId)} x{cost.count}");
            }

            return parts.Count == 0 ? "ready" : string.Join(", ", parts);
        }

        static string DisplayName(FoundationDefinition def, string fallback) =>
            def != null ? def.Display : fallback;

        Color BlockColor(string id)
        {
            var b = _content.Blocks.Get(id);
            return b != null ? b.color : Color.magenta;
        }
        Color PlaceableColor(string id)
        {
            var p = _content.Placeables.Get(id);
            return p != null ? p.color : Color.magenta;
        }

        bool CanPlace(ItemDefinition def, Vector2Int c)
        {
            var cell = _world.GetCell(c.x, c.y);
            bool onPlayer = _player != null && _player.CurrentCell == c;
            if (def.PlacesBlock)
            {
                if (!_content.Blocks.Has(def.placeBlockId)) return false;
                var b = _content.Blocks.Get(def.placeBlockId);
                if (onPlayer && b != null && b.IsSolid) return false; // never trap the player
                return !cell.HasOccupant && !cell.HasNode && !cell.Water && !cell.SolidBlock;
            }
            if (def.PlacesPlaceable)
            {
                var p = _content.Placeables.Get(def.placeableId);
                return CanPlacePlaceable(p, c);
            }
            return false;
        }

        bool CanPlacePlaceable(PlaceableDefinition placeable, Vector2Int anchor)
        {
            if (placeable == null)
                return false;

            foreach (var c in FootprintCells(placeable, anchor.x, anchor.y))
            {
                var cell = _world.GetCell(c.x, c.y);
                bool onPlayer = _player != null && _player.CurrentCell == c;
                if (onPlayer && placeable.blocksMovement)
                    return false;
                if (cell.Blocked || cell.HasOccupant || cell.HasNode)
                    return false;
            }

            return true;
        }

        bool TryPlaceFootprint(PlaceableDefinition placeable, int wx, int wy)
        {
            if (placeable == null || !CanPlacePlaceable(placeable, new Vector2Int(wx, wy)))
                return false;

            var placed = new List<Vector2Int>();
            foreach (var c in FootprintCells(placeable, wx, wy))
            {
                if (!_world.TryPlaceOccupant(c.x, c.y, placeable.id, placeable.blocksMovement))
                {
                    for (int i = 0; i < placed.Count; i++)
                        _world.ClearOccupant(placed[i].x, placed[i].y);
                    return false;
                }

                placed.Add(c);
            }

            return true;
        }

        bool TryEnsureFootprintOccupant(PlaceableDefinition placeable, int wx, int wy)
        {
            if (placeable == null)
                return false;

            var placed = new List<Vector2Int>();
            foreach (var c in FootprintCells(placeable, wx, wy))
            {
                var cell = _world.GetCell(c.x, c.y);
                if (cell.HasOccupant)
                {
                    if (cell.OccupantId != placeable.id)
                    {
                        ClearOccupants(placed);
                        return false;
                    }
                    continue;
                }

                if (cell.SolidBlock || cell.Water || cell.HasNode)
                {
                    ClearOccupants(placed);
                    return false;
                }

                if (!_world.TryPlaceOccupant(c.x, c.y, placeable.id, placeable.blocksMovement))
                {
                    ClearOccupants(placed);
                    return false;
                }

                placed.Add(c);
            }

            return true;
        }

        void ClearOccupants(List<Vector2Int> cells)
        {
            if (cells == null)
                return;

            for (int i = 0; i < cells.Count; i++)
                _world.ClearOccupant(cells[i].x, cells[i].y);
        }

        IEnumerable<Vector2Int> FootprintCells(PlaceableDefinition placeable, int wx, int wy)
        {
            int w = placeable != null ? placeable.FootprintWidth : 1;
            int h = placeable != null ? placeable.FootprintHeight : 1;
            int minX = wx - w / 2;
            int minY = wy - h / 2;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    yield return new Vector2Int(minX + x, minY + y);
        }

        List<Vector2Int> FootprintCellsList(PlaceableDefinition placeable, int wx, int wy) =>
            new List<Vector2Int>(FootprintCells(placeable, wx, wy));

        void RegisterPlaceableCells(PlaceableInstance inst)
        {
            if (inst == null || inst.Def == null)
                return;

            foreach (var c in FootprintCells(inst.Def, inst.Wx, inst.Wy))
                _placeablesByCell[Key(c.x, c.y)] = inst;
        }

        void UnregisterPlaceableCells(PlaceableInstance inst)
        {
            if (inst == null || inst.Def == null)
                return;

            foreach (var c in FootprintCells(inst.Def, inst.Wx, inst.Wy))
                _placeablesByCell.Remove(Key(c.x, c.y));
        }

        void ClearFootprintOccupants(PlaceableDefinition placeable, int wx, int wy)
        {
            if (placeable == null)
                return;

            foreach (var c in FootprintCells(placeable, wx, wy))
            {
                var cell = _world.GetCell(c.x, c.y);
                if (cell.HasOccupant && cell.OccupantId == placeable.id)
                    _world.ClearOccupant(c.x, c.y);
            }
        }

        public bool TryPlaceSelected()
        {
            var def = SelectedPlaceable();
            if (def == null || !_inv.Has(def.id, 1)) return false;
            var c = CursorCell();
            if (!CanPlace(def, c)) return false;

            if (def.PlacesBlock)
            {
                var b = _content.Blocks.Get(def.placeBlockId);
                if (_world.TryPlaceBlock(c.x, c.y, b))
                {
                    _inv.Remove(def.id, 1);
                    Placed?.Invoke(def, c.x, c.y);
                    return true;
                }
            }
            else if (def.PlacesPlaceable)
            {
                var p = _content.Placeables.Get(def.placeableId);
                if (p != null && TryPlaceFootprint(p, c.x, c.y))
                {
                    _inv.Remove(def.id, 1);
                    SpawnPlaceable(p, c.x, c.y);
                    Placed?.Invoke(def, c.x, c.y);
                    return true;
                }
            }
            return false;
        }

        public bool TryRemoveAtCursor() => TryRemoveAtCursor(out _);

        public bool TryRemoveAtCursor(out string blockedMessage)
        {
            var c = CursorCell();
            return TryRemoveAtCell(c.x, c.y, out blockedMessage);
        }

        string FindBlockItem(string blockId)
        {
            return !string.IsNullOrEmpty(blockId) &&
                _blockItemByBlockId.TryGetValue(blockId, out var itemId)
                    ? itemId
                    : null;
        }

        void SpawnPlaceable(PlaceableDefinition def, int wx, int wy)
        {
            var go = new GameObject($"Placeable_{def.id}_{wx}_{wy}");
            go.transform.SetParent(_placeParent, false);
            var inst = go.AddComponent<PlaceableInstance>();
            inst.Init(def, _world, wx, wy);
            _placeables.Add(inst);
            RegisterPlaceableCells(inst);
            _storage?.EnsureContainer(def, wx, wy);
        }

        public bool TryReplacePlaceableAtCell(int wx, int wy, PlaceableDefinition result)
        {
            if (result == null || _world == null)
                return false;

            var key = Key(wx, wy);
            if (!_placeablesByCell.TryGetValue(key, out var oldInst) || oldInst == null)
                return false;

            wx = oldInst.Wx;
            wy = oldInst.Wy;
            var oldDef = oldInst.Def;
            string oldId = oldDef != null ? oldDef.id : "";
            _storage?.RemoveContainer(wx, wy, false);
            UnregisterPlaceableCells(oldInst);
            ClearFootprintOccupants(oldDef, wx, wy);

            if (!TryPlaceFootprint(result, wx, wy))
            {
                if (oldDef != null && TryPlaceFootprint(oldDef, wx, wy))
                {
                    RegisterPlaceableCells(oldInst);
                    _storage?.EnsureContainer(oldDef, wx, wy);
                }
                return false;
            }

            _placeables.Remove(oldInst);
            if (oldInst)
            {
                if (Application.isPlaying) Destroy(oldInst.gameObject);
                else DestroyImmediate(oldInst.gameObject);
            }

            SpawnPlaceable(result, wx, wy);
            Placed?.Invoke(_content.Items.Get(result.requiredItemId), wx, wy);
            Removed?.Invoke(oldId, wx, wy);
            return true;
        }

        public FoundationSavedPlaceable[] SnapshotPlaceables()
        {
            var result = new List<FoundationSavedPlaceable>();
            foreach (var p in _placeables)
            {
                if (!p || p.Def == null) continue;
                result.Add(new FoundationSavedPlaceable
                {
                    placeableId = p.Def.id,
                    x = p.Wx,
                    y = p.Wy,
                });
            }
            return result.ToArray();
        }

        public bool HasContainerPlaceable(int wx, int wy, string placeableId)
        {
            var def = _content?.Placeables.Get(placeableId);
            if (def == null || def.interaction != InteractionKind.Container)
                return false;

            return _placeablesByCell.TryGetValue(Key(wx, wy), out var p) &&
                p &&
                p.Def != null &&
                p.Def.id == def.id &&
                p.Def.interaction == InteractionKind.Container;
        }

        public void RestorePlaceables(FoundationSavedPlaceable[] placeables)
        {
            ClearPlaceables();
            if (placeables == null || _world == null || _content == null) return;

            foreach (var saved in placeables)
            {
                var def = _content.Placeables.Get(saved.placeableId);
                if (def == null) continue;

                var cell = _world.GetCell(saved.x, saved.y);
                if (cell.HasOccupant && cell.OccupantId != def.id)
                    continue;
                if (!TryEnsureFootprintOccupant(def, saved.x, saved.y))
                    continue;

                SpawnPlaceable(def, saved.x, saved.y);
            }
        }

        void ClearPlaceables()
        {
            foreach (var p in _placeables)
            {
                if (!p) continue;
                _storage?.RemoveContainer(p.Wx, p.Wy, false);
                if (Application.isPlaying) Destroy(p.gameObject);
                else DestroyImmediate(p.gameObject);
            }
            _placeables.Clear();
            _placeablesByCell.Clear();
        }

        public PlaceableInstance NearestInteractable(Vector3 pos, float range)
        {
            PlaceableInstance best = null;
            float bd = range * range;
            foreach (var p in _placeables)
            {
                if (!p || p.Def.interaction == InteractionKind.None) continue;
                float d = ((Vector2)(p.transform.position - pos)).sqrMagnitude;
                if (d <= bd) { bd = d; best = p; }
            }
            return best;
        }

        public PlaceableInstance PlaceableAtCell(int wx, int wy)
        {
            return _placeablesByCell.TryGetValue(Key(wx, wy), out var p) && p ? p : null;
        }

        public bool TryGetPlaceableUnderCursor(Camera cam, Vector2 screenPosition, out PlaceableInstance placeable)
        {
            placeable = null;
            if (cam == null || _placeables.Count == 0)
                return false;

            var wp = cam.ScreenToWorldPoint(screenPosition);
            var point = new Vector2(wp.x, wp.y);
            int bestOrder = int.MinValue;
            foreach (var candidate in _placeables)
            {
                if (candidate == null || !candidate.ContainsWorldPoint(point))
                    continue;

                int order = candidate.SortingOrder;
                if (placeable != null && order < bestOrder)
                    continue;

                placeable = candidate;
                bestOrder = order;
            }

            return placeable != null;
        }

        public bool TryRemoveAtCell(int wx, int wy, out string blockedMessage)
        {
            blockedMessage = null;
            var cell = _world.GetCell(wx, wy);

            if (cell.HasOccupant)
            {
                string occupantId = cell.OccupantId;
                var key = Key(wx, wy);
                _placeablesByCell.TryGetValue(key, out var inst);
                var pdef = inst != null && inst.Def != null
                    ? inst.Def
                    : _content.Placeables.Get(cell.OccupantId);
                int anchorX = inst != null ? inst.Wx : wx;
                int anchorY = inst != null ? inst.Wy : wy;
                string refundItem = pdef != null ? pdef.requiredItemId : null;
                if (!string.IsNullOrEmpty(refundItem) && !_inv.CanFit(refundItem, 1))
                {
                    blockedMessage = "Inventory full!";
                    return false;
                }

                if (pdef != null && pdef.interaction == InteractionKind.Container &&
                    _storage != null && !_storage.RemoveContainer(anchorX, anchorY))
                {
                    blockedMessage = $"{pdef.Display} is not empty";
                    return false;
                }

                if (inst != null && pdef != null)
                {
                    ClearFootprintOccupants(pdef, anchorX, anchorY);
                    UnregisterPlaceableCells(inst);
                }
                else
                {
                    _world.ClearOccupant(wx, wy);
                    _placeablesByCell.Remove(key);
                }

                if (inst) { _placeables.Remove(inst); Destroy(inst.gameObject); }
                if (!string.IsNullOrEmpty(refundItem))
                    _inv.Add(refundItem, 1);
                Removed?.Invoke(pdef != null ? pdef.id : occupantId, anchorX, anchorY);
                return true;
            }

            if (cell.SolidBlock)
            {
                string blockId = cell.SurfaceBlockId;
                string item = FindBlockItem(blockId);
                if (item != null && !_inv.CanFit(item, 1))
                {
                    blockedMessage = "Inventory full!";
                    return false;
                }

                if (_world.RemoveSolidBlock(wx, wy))
                {
                    if (item != null) _inv.Add(item, 1);
                    Removed?.Invoke(blockId, wx, wy);
                    return true;
                }
            }

            return false;
        }

        public bool IsStationInRange(Vector3 pos, float range, StationType st)
        {
            float r2 = range * range;
            foreach (var p in _placeables)
            {
                if (!p) continue;
                bool matchesStation = p.Def.stationType == st ||
                    (st == StationType.CookingPot && p.Def.isCampsite);
                if (!matchesStation) continue;
                if (((Vector2)(p.transform.position - pos)).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        public bool TryFindBestCampsite(Vector2 ground, out PlaceableInstance campsite, out float distanceSq)
        {
            campsite = null;
            distanceSq = float.MaxValue;

            foreach (var p in _placeables)
            {
                if (!p || p.Def == null || !p.Def.isCampsite)
                    continue;

                float radius = Mathf.Max(0f, p.Def.campWardRadius);
                if (radius <= 0f)
                    continue;

                float d = ((Vector2)p.transform.position - ground).sqrMagnitude;
                if (d > radius * radius || d >= distanceSq)
                    continue;

                campsite = p;
                distanceSq = d;
            }

            return campsite != null;
        }

        /// <summary>
        /// Death respawn (audit rec #3): nearest placed campsite anywhere in the world.
        /// Unlike TryFindBestCampsite this has no ward-radius gate — it answers
        /// "where is the player's campfire?", not "is the player inside an aura?".
        /// </summary>
        public bool TryFindNearestCampsite(Vector2 ground, out PlaceableInstance campsite)
        {
            campsite = null;
            float best = float.MaxValue;

            foreach (var p in _placeables)
            {
                if (!p || p.Def == null || !p.Def.isCampsite)
                    continue;

                float d = ((Vector2)p.transform.position - ground).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    campsite = p;
                }
            }

            return campsite != null;
        }
    }
}

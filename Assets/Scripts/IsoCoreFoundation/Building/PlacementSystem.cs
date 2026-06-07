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

        public event Action<ItemDefinition, int, int> Placed;
        public event Action<string, int, int> Removed;

        readonly List<PlaceableInstance> _placeables = new();

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
            if (def == null || !_inv.Has(def.id, 1)) { _ghost.enabled = false; return; }

            var c = CursorCell();
            bool valid = CanPlace(def, c);
            int h = _world.GetHeight(c.x, c.y);

            _ghost.enabled = true;
            _ghost.sprite = def.PlacesBlock
                ? PlaceholderArt.Diamond(BlockColor(def.placeBlockId))
                : PlaceholderArt.Box(PlaceableColor(def.placeableId), 0.8f, 1f);
            _ghost.transform.position = IsoGrid.CellToWorld(c.x, c.y, h);
            _ghost.sortingOrder = IsoGrid.SortingOrder(c.x, c.y, h, IsoGrid.LayerPreview);
            var col = valid ? new Color(0.4f, 1f, 0.4f, 0.6f) : new Color(1f, 0.35f, 0.35f, 0.6f);
            _ghost.color = col;
        }

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
                if (onPlayer && p != null && p.blocksMovement) return false; // never trap the player
                return !cell.Blocked && !cell.HasOccupant && !cell.HasNode;
            }
            return false;
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
                if (p != null && _world.TryPlaceOccupant(c.x, c.y, p.id, p.blocksMovement))
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
            foreach (var it in _content.Items.All)
                if (it.PlacesBlock && it.placeBlockId == blockId) return it.id;
            return null;
        }

        void SpawnPlaceable(PlaceableDefinition def, int wx, int wy)
        {
            var go = new GameObject($"Placeable_{def.id}_{wx}_{wy}");
            go.transform.SetParent(_placeParent, false);
            var inst = go.AddComponent<PlaceableInstance>();
            inst.Init(def, _world, wx, wy);
            _placeables.Add(inst);
            _storage?.EnsureContainer(def, wx, wy);
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

            foreach (var p in _placeables)
            {
                if (!p || p.Def == null) continue;
                if (p.Wx == wx && p.Wy == wy && p.Def.id == def.id &&
                    p.Def.interaction == InteractionKind.Container)
                    return true;
            }
            return false;
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
                if (!cell.HasOccupant)
                {
                    if (!_world.TryPlaceOccupant(saved.x, saved.y, def.id, def.blocksMovement))
                        continue;
                }
                else if (cell.OccupantId != def.id)
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
            foreach (var p in _placeables)
                if (p && p.Wx == wx && p.Wy == wy)
                    return p;
            return null;
        }

        public bool TryRemoveAtCell(int wx, int wy, out string blockedMessage)
        {
            blockedMessage = null;
            var cell = _world.GetCell(wx, wy);

            if (cell.HasOccupant)
            {
                string occupantId = cell.OccupantId;
                var pdef = _content.Placeables.Get(cell.OccupantId);
                string refundItem = pdef != null ? pdef.requiredItemId : null;
                if (!string.IsNullOrEmpty(refundItem) && !_inv.CanFit(refundItem, 1))
                {
                    blockedMessage = "Inventory full!";
                    return false;
                }

                if (pdef != null && pdef.interaction == InteractionKind.Container &&
                    _storage != null && !_storage.RemoveContainer(wx, wy))
                {
                    blockedMessage = $"{pdef.Display} is not empty";
                    return false;
                }

                var inst = _placeables.Find(p => p && p.Wx == wx && p.Wy == wy);
                _world.ClearOccupant(wx, wy);
                if (inst) { _placeables.Remove(inst); Destroy(inst.gameObject); }
                if (!string.IsNullOrEmpty(refundItem))
                    _inv.Add(refundItem, 1);
                Removed?.Invoke(pdef != null ? pdef.id : occupantId, wx, wy);
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
                if (!p || p.Def.stationType != st) continue;
                if (((Vector2)(p.transform.position - pos)).sqrMagnitude <= r2) return true;
            }
            return false;
        }
    }
}

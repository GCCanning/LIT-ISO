using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Farming: hoe tills ground into soil; seeds plant crops on soil; mature crops
    /// are harvested with E. Crops are walkable, non-blocking, and self-grow.
    /// </summary>
    public class FarmingSystem : MonoBehaviour
    {
        IsoWorld _world;
        FoundationContent _content;
        Inventory _inv;
        Hotbar _hotbar;
        Camera _cam;
        Transform _parent;

        public event Action<int, int> SoilTilled;
        public event Action<ItemDefinition, CropDefinition, int, int> SeedPlanted;
        public event Action<CropDefinition> CropHarvested;

        readonly Dictionary<long, CropInstance> _crops = new();
        static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;

        public void Init(IsoWorld world, FoundationContent content, Inventory inv, Hotbar hotbar, Camera cam)
        {
            _world = world; _content = content; _inv = inv; _hotbar = hotbar; _cam = cam;
            _parent = new GameObject("Crops").transform;
            _parent.SetParent(transform, false);
        }

        Vector2Int CursorCell()
        {
            var wp = _cam.ScreenToWorldPoint(Input.mousePosition);
            wp.z = 0f;
            var c = IsoGrid.WorldToCell(wp);
            int h = _world.GetHeight(c.x, c.y);
            if (h != 0) c = IsoGrid.WorldToCell(new Vector3(wp.x, wp.y - h * IsoGrid.HeightStep, 0f));
            return c;
        }

        ItemDefinition Selected()
        {
            var s = _hotbar.SelectedStack;
            return s.IsEmpty ? null : _content.Items.Get(s.itemId);
        }

        /// <summary>LMB action when a hoe or seed is selected. Returns a HUD message, or null if not handled.</summary>
        public string TryUseSelected()
        {
            var def = Selected();
            if (def == null) return null;
            var c = CursorCell();

            if (def.category == ItemCategory.Tool && def.toolType == ToolType.Hoe)
            {
                if (!_world.TryTill(c.x, c.y)) return null;
                SoilTilled?.Invoke(c.x, c.y);
                return "Tilled soil";
            }

            if (def.IsSeed)
                return TryPlant(def, c) ? "Planted" : null;

            return null;
        }

        bool TryPlant(ItemDefinition seed, Vector2Int c)
        {
            if (_crops.ContainsKey(Key(c.x, c.y))) return false;
            var cell = _world.GetCell(c.x, c.y);
            if (cell.SurfaceBlockId != "soil") return false;       // must till first
            if (!_inv.Has(seed.id, 1)) return false;
            var crop = _content.Crops.Get(seed.plantCropId);
            if (crop == null) return false;

            _inv.Remove(seed.id, 1);
            var go = new GameObject($"Crop_{crop.id}_{c.x}_{c.y}");
            go.transform.SetParent(_parent, false);
            var ci = go.AddComponent<CropInstance>();
            ci.Init(crop, _world, c.x, c.y);
            _crops[Key(c.x, c.y)] = ci;
            SeedPlanted?.Invoke(seed, crop, c.x, c.y);
            return true;
        }

        /// <summary>Harvest the nearest mature crop within range. Returns true on success.</summary>
        public bool TryHarvestCrop(Vector3 pos, float range, out bool blockedFull)
        {
            blockedFull = false;
            CropInstance best = null; long bestKey = 0;
            float bd = range * range;
            foreach (var kv in _crops)
            {
                var ci = kv.Value;
                if (!ci || !ci.Mature) continue;
                float d = ((Vector2)(ci.transform.position - pos)).sqrMagnitude;
                if (d <= bd) { bd = d; best = ci; bestKey = kv.Key; }
            }
            if (best == null) return false;
            if (!best.Harvest(_inv, out blockedFull)) return false;
            CropHarvested?.Invoke(best.Def);
            _crops.Remove(bestKey);
            Destroy(best.gameObject);
            return true;
        }

        /// <summary>True if a mature crop is within range (for the interact prompt ordering).</summary>
        public bool HasMatureCropNear(Vector3 pos, float range)
        {
            float r2 = range * range;
            foreach (var kv in _crops)
            {
                var ci = kv.Value;
                if (ci && ci.Mature && ((Vector2)(ci.transform.position - pos)).sqrMagnitude <= r2) return true;
            }
            return false;
        }
    }
}

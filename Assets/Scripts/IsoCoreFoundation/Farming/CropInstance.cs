using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>A growing crop on a tilled soil cell. Advances stages over time.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CropInstance : MonoBehaviour
    {
        public CropDefinition Def { get; private set; }
        public int Wx { get; private set; }
        public int Wy { get; private set; }
        public int Stage => _stage;
        public float StageTimer => _timer;
        public bool Mature => _stage >= Def.stages - 1;

        IsoWorld _world;
        SpriteRenderer _sr;
        int _stage;
        float _timer;

        public void Init(CropDefinition def, IsoWorld world, int wx, int wy, int stage = 0, float stageTimer = 0f)
        {
            Def = def; _world = world; Wx = wx; Wy = wy;
            _sr = GetComponent<SpriteRenderer>();
            _stage = Mathf.Clamp(stage, 0, Mathf.Max(0, def.stages - 1));
            _timer = Mathf.Max(0f, stageTimer);
            Render();
        }

        public void Tick(float deltaTime)
        {
            if (_world == null || Mature || deltaTime <= 0f) return;
            _timer += deltaTime;
            while (_timer >= Def.secondsPerStage && !Mature)
            {
                _timer -= Def.secondsPerStage;
                _stage = Mathf.Min(_stage + 1, Def.stages - 1);
                Render();
            }
        }

        void Render()
        {
            int h = _world.GetHeight(Wx, Wy);
            float t = Def.stages <= 1 ? 1f : (float)_stage / (Def.stages - 1);
            float ht = Mathf.Lerp(0.25f, Def.matureHeightUnits, t);
            var col = Color.Lerp(Def.youngColor, Def.ripeColor, t);
            transform.position = IsoGrid.CellToWorld(Wx, Wy, h);
            _sr.sprite = PlaceholderArt.Box(col, 0.5f, ht);
            _sr.sortingOrder = IsoGrid.SortingOrder(Wx, Wy, h, IsoGrid.LayerProp);
        }

        /// <summary>Harvest if mature: grants produce. Returns true on success.</summary>
        public bool Harvest(Inventory inv, out bool blockedFull)
        {
            blockedFull = false;
            if (!Mature) return false;
            if (!DropsCanFit(inv))
            {
                blockedFull = true;
                return false;
            }
            HarvestSystem.RollDrops(Def.harvest, inv);
            return true;
        }

        bool DropsCanFit(Inventory inv)
        {
            if (Def.harvest == null || inv == null) return true;
            int count = 0;
            foreach (var d in Def.harvest)
                if (!string.IsNullOrEmpty(d.itemId) && d.chance >= 1f) count++;
            if (count == 0) return true;

            var guaranteed = new ItemStack[count];
            int i = 0;
            foreach (var d in Def.harvest)
            {
                if (string.IsNullOrEmpty(d.itemId) || d.chance < 1f) continue;
                guaranteed[i++] = new ItemStack(d.itemId, Mathf.Max(1, d.max));
            }
            return inv.CanFitAll(guaranteed);
        }
    }
}

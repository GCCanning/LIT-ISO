using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>A harvestable world instance (tree/rock/bush). Occupies its cell.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ResourceNode : MonoBehaviour
    {
        public ResourceNodeDefinition Def { get; private set; }
        public int Wx { get; private set; }
        public int Wy { get; private set; }

        IsoWorld _world;
        int _remainingHits;
        bool _shaking;

        // Down-screen nudge (world units) that seats a prop into the front-centre of its
        // tile instead of on the tile's widest line (the 4-tile meeting points).
        const float SeatOffsetY = -0.14f;

        public void Init(ResourceNodeDefinition def, IsoWorld world, int wx, int wy)
        {
            Def = def; _world = world; Wx = wx; Wy = wy;
            _remainingHits = Mathf.Max(1, def.hitsToHarvest);

            int height = world.GetHeight(wx, wy);
            // Seat the prop slightly forward (down-screen) of the exact cell centre. The
            // cell centre lies on the diamond's widest line, whose ends are the points
            // where four tiles meet — planting a base there reads as "on the intersection".
            // Nudging down into the front triangle plants it clearly inside the one tile.
            Vector3 pos = IsoGrid.CellToWorld(wx, wy, height);
            pos.y += SeatOffsetY;
            transform.position = pos;
            var sr = GetComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteAmbient.Material; // day/night world tint
            // Prefer real pixel-art from Resources/Decorations/<nodeId>.png (sized via its
            // PPU and pivoted at its base so it stands inside the cell). Fall back to the
            // procedural placeholder box when no art is present for this node id.
            var art = DecorationSpriteResolver.Resolve(def.id);
            if (art != null)
            {
                sr.sprite = art;
                sr.color = Color.white; // real art carries its own colour
            }
            else
            {
                sr.sprite = PlaceholderArt.Box(def.color, def.widthUnits, def.heightUnits);
            }
            sr.sortingOrder = IsoGrid.SortingOrder(wx, wy, height, IsoGrid.LayerProp);
        }

        /// <summary>True when this node needs a tool the player isn't holding.</summary>
        public bool RequiresMissingTool(ToolType tool) =>
            Def.toolMandatory && Def.requiredTool != ToolType.None && tool != Def.requiredTool;

        /// <summary>One harvest hit. Better tools (higher tier) deplete faster.
        /// Returns true when fully depleted (drops granted).</summary>
        public bool Harvest(Inventory inv, ToolType tool, int tier, out bool blockedFull, List<ItemStack> grantedDrops = null)
        {
            blockedFull = false;
            if (RequiresMissingTool(tool)) return false;
            int power = (Def.requiredTool != ToolType.None && tool == Def.requiredTool)
                ? Mathf.Max(1, tier) : 1; // right tool tier is faster; hand/other = 1

            // Only the depleting hit grants drops — block it (without losing yield) if the
            // guaranteed drops can't fit. Considers partial stacks via Inventory.CanFit.
            if (_remainingHits - power <= 0 && !DropsCanFit(inv))
            {
                blockedFull = true;
                return false;
            }

            _remainingHits -= power;

            // Per-hit juice: a directional chip burst, sound, and a quick shake.
            Vector3 fxOrigin = transform.position + Vector3.up * (Def.heightUnits * 0.5f);
            WorldFx.Debris(fxOrigin, DebrisColor(), 6);
            SfxManager.Play(HitSfxKey());

            if (_remainingHits > 0)
            {
                StartShake();
                return false;
            }

            // Depletion: grant drops, show pickup text, bigger burst + completion sound.
            var granted = grantedDrops ?? new List<ItemStack>();
            HarvestSystem.RollDrops(Def.drops, inv, granted);
            ShowPickups(granted, fxOrigin);
            WorldFx.Debris(fxOrigin, DebrisColor(), 12, 0.08f, 2.9f);
            SfxManager.Play("harvest");
            _world.ClearNode(Wx, Wy); // controller despawns this GO via OnCellChanged
            return true;
        }

        // ---- Juice helpers ----
        string HitSfxKey()
        {
            switch (Def.id)
            {
                case "rock":
                case "copper_vein": return "mine";
                case "bush":
                case "flower": return "harvest";
                default: return "chop"; // tree/pine/stump/log
            }
        }

        Color DebrisColor()
        {
            // Colour chips by the primary drop so wood looks brown, stone grey, etc.
            string drop = (Def.drops != null && Def.drops.Length > 0) ? Def.drops[0].itemId : null;
            switch (drop)
            {
                case "wood": return new Color(0.55f, 0.36f, 0.18f);
                case "stone": return new Color(0.58f, 0.58f, 0.60f);
                case "copper_ore": return new Color(0.80f, 0.45f, 0.22f);
                case "fiber": return new Color(0.45f, 0.62f, 0.28f);
                default: return Def.color;
            }
        }

        void ShowPickups(List<ItemStack> granted, Vector3 origin)
        {
            if (granted == null) return;
            float offset = 0f;
            foreach (var g in granted)
            {
                if (g.count <= 0 || string.IsNullOrEmpty(g.itemId)) continue;
                FloatingText.Spawn(origin + new Vector3(0f, offset, 0f),
                    $"+{g.count} {g.itemId}", new Color(1f, 0.96f, 0.7f));
                offset += 0.28f;
            }
            if (granted.Count > 0) SfxManager.Play("pickup", 0.8f);
        }

        void StartShake()
        {
            if (_shaking) return;
            StartCoroutine(ShakeRoutine());
        }

        IEnumerator ShakeRoutine()
        {
            _shaking = true;
            Vector3 baseP = transform.position;
            float t = 0f;
            const float dur = 0.18f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float amp = (1f - t / dur) * 0.06f;
                transform.position = baseP + new Vector3(Mathf.Sin(t * 70f) * amp, 0f, 0f);
                yield return null;
            }
            transform.position = baseP;
            _shaking = false;
        }

        bool DropsCanFit(Inventory inv)
        {
            if (Def.drops == null) return true;
            int count = 0;
            foreach (var d in Def.drops)
                if (!string.IsNullOrEmpty(d.itemId) && d.chance >= 1f) count++;
            if (count == 0) return true;

            var guaranteed = new ItemStack[count];
            int i = 0;
            foreach (var d in Def.drops)
            {
                if (string.IsNullOrEmpty(d.itemId) || d.chance < 1f) continue; // bonus drops may be skipped
                guaranteed[i++] = new ItemStack(d.itemId, Mathf.Max(1, d.max));
            }
            return inv.CanFitAll(guaranteed);
        }
    }
}

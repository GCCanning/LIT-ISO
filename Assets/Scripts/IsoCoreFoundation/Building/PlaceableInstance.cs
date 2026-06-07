using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>A placed object in the world. Crafting stations are placeables whose
    /// definition carries a non-None stationType.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlaceableInstance : MonoBehaviour, IInteractable
    {
        public PlaceableDefinition Def { get; private set; }
        public int Wx { get; private set; }
        public int Wy { get; private set; }

        public void Init(PlaceableDefinition def, IsoWorld world, int wx, int wy)
        {
            Def = def; Wx = wx; Wy = wy;
            int h = world.GetHeight(wx, wy);
            transform.position = IsoGrid.CellToWorld(wx, wy, h);
            var sr = GetComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteAmbient.Material; // day/night tint like the world
            var art = DecorationSpriteResolver.Resolve(def.id);
            sr.sprite = art != null ? art : PlaceholderArt.Box(def.color, def.widthUnits, def.heightUnits);
            sr.sortingOrder = IsoGrid.SortingOrder(wx, wy, h, IsoGrid.LayerProp);

            if (def.emitsLight)
            {
                var glow = new GameObject("Glow");
                glow.transform.SetParent(transform, false);
                glow.AddComponent<CampfireGlow>().Setup(def.lightColor, def.lightRadius, sr.sortingOrder);
            }
        }

        public string Prompt => Def.interaction switch
        {
            InteractionKind.CraftingStation => $"Right-click {Def.Display}",
            InteractionKind.Container => $"Right-click {Def.Display}",
            InteractionKind.Entrance => $"Right-click {Def.Display}",
            _ => Def.Display
        };

        public void Interact(GameObject interactor) { /* routed via PlayerInteraction/HUD */ }
    }
}

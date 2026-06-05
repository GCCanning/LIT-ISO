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
            sr.sprite = PlaceholderArt.Box(def.color, def.widthUnits, def.heightUnits);
            sr.sortingOrder = IsoGrid.SortingOrder(wx, wy, h, IsoGrid.LayerProp);
        }

        public string Prompt => Def.interaction switch
        {
            InteractionKind.CraftingStation => $"[E] Use {Def.Display}",
            InteractionKind.Container => $"[E] Open {Def.Display}",
            _ => Def.Display
        };

        public void Interact(GameObject interactor) { /* routed via PlayerInteraction/HUD */ }
    }
}

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
        SpriteRenderer _renderer;

        public int SortingOrder => _renderer != null ? _renderer.sortingOrder : 0;
        public Vector3 HighlightPosition => transform.position;
        public float HoverHighlightScale => Def != null
            ? Mathf.Clamp(Mathf.Max(Def.FootprintWidth, Def.FootprintHeight) * 0.95f, 1f, 3.5f)
            : 1f;
        public float HoverLift => Def != null
            ? Mathf.Clamp(0.06f + Def.heightUnits * 0.04f, 0.06f, 0.24f)
            : 0.08f;
        public Color HoverHighlightColor => Def != null && Def.interaction == InteractionKind.Entrance
            ? new Color(0.60f, 0.82f, 1f, 0.82f)
            : new Color(1f, 0.94f, 0.62f, 0.82f);

        public void Init(PlaceableDefinition def, IsoWorld world, int wx, int wy)
        {
            Def = def; Wx = wx; Wy = wy;
            int h = world.GetHeight(wx, wy);
            transform.position = IsoGrid.CellToWorld(wx, wy, h);
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sharedMaterial = SpriteAmbient.Material; // day/night tint like the world
            var art = FoundationPlaceableSpriteResolver.Resolve(def.id);
            _renderer.sprite = art != null ? art : PlaceholderArt.Box(def.color, def.widthUnits, def.heightUnits);
            transform.localScale = Vector3.one;
            if (art != null && ShouldScaleArtToDefinition(def, art))
                transform.localScale = Vector3.one * ArtScaleFor(def, art);
            _renderer.sortingOrder = IsoGrid.SortingOrder(wx, wy, h, IsoGrid.LayerProp);

            if (def.id == "campfire" || def.id == "fireplace")
                gameObject.AddComponent<FoundationCampfireAnimator>().Init(_renderer);

            FoundationDepthPolish.Attach(gameObject,
                fadeWhenOccluding: def.blocksMovement || def.interaction == InteractionKind.Entrance,
                castLongShadow: def.heightUnits >= 0.7f,
                contactScale: Mathf.Clamp(def.widthUnits, 0.65f, 1.5f),
                contactAlpha: def.blocksMovement ? 0.30f : 0.22f);

            if (def.emitsLight)
            {
                var glow = new GameObject("Glow");
                glow.transform.SetParent(transform, false);
                glow.AddComponent<CampfireGlow>().Setup(def.lightColor, def.lightRadius, _renderer.sortingOrder);
            }
        }

        public bool ContainsWorldPoint(Vector2 worldPoint)
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            return _renderer != null &&
                _renderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, _renderer.bounds.center.z));
        }

        public string Prompt => Def.interaction switch
        {
            InteractionKind.CraftingStation => $"Right-click {Def.Display}",
            InteractionKind.Container => $"Right-click {Def.Display}",
            InteractionKind.Entrance => $"Right-click {Def.Display}",
            InteractionKind.Construction => $"Right-click {Def.Display}",
            _ => Def.Display
        };

        public void Interact(GameObject interactor) { /* routed via PlayerInteraction/HUD */ }

        static bool ShouldScaleArtToDefinition(PlaceableDefinition def, Sprite art) =>
            def != null && art != null &&
            (def.HasMultiCellFootprint || def.id == "campfire" || def.id == "fireplace");

        static float ArtScaleFor(PlaceableDefinition def, Sprite art)
        {
            float spriteWidth = art.bounds.size.x;
            if (spriteWidth <= 0.001f)
                return 1f;

            float desiredWidth = Mathf.Max(0.1f, def.widthUnits);
            if (def.HasMultiCellFootprint)
                desiredWidth = Mathf.Max(desiredWidth, def.FootprintWidth * 0.95f);

            return Mathf.Clamp(desiredWidth / spriteWidth, 0.75f, 4f);
        }
    }
}

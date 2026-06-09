using UnityEngine;

namespace IsoCore.Foundation
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FoundationDungeonPortalInstance : MonoBehaviour
    {
        public string PortalId { get; private set; }
        public string DungeonId { get; private set; }
        public string DisplayName { get; private set; }
        public int Tier { get; private set; }
        public Vector2Int Cell { get; private set; }
        public bool RewardOpened { get; private set; }
        public bool Completed { get; private set; }
        public int SortingOrder => _renderer != null ? _renderer.sortingOrder : 0;
        public Vector3 HighlightPosition => transform.position;

        SpriteRenderer _renderer;
        FoundationPortalVisual _visual;
        Color _baseColor = Color.white;

        public void Init(string portalId, string dungeonId, string displayName, int tier,
            Vector2Int cell, IsoWorld world, Color color)
        {
            PortalId = portalId;
            DungeonId = dungeonId;
            DisplayName = displayName;
            Tier = Mathf.Max(1, tier);
            Cell = cell;
            _baseColor = color;

            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sharedMaterial = SpriteAmbient.Material;
            _renderer.sprite = FoundationDungeonSpriteResolver.Portal() ?? PlaceholderArt.Blob(color, 0.9f);

            int h = world != null ? world.GetHeight(cell.x, cell.y) : 0;
            var pos = IsoGrid.CellToWorld(cell.x, cell.y, h);
            pos.y -= 0.12f;
            transform.position = pos;
            _renderer.sortingOrder = IsoGrid.SortingOrder(cell.x, cell.y, h, IsoGrid.LayerProp);
            _visual = gameObject.GetComponent<FoundationPortalVisual>();
            if (_visual == null)
                _visual = gameObject.AddComponent<FoundationPortalVisual>();
            _visual.Init(_renderer, Tier, color, 1.18f);
            FoundationDepthPolish.Attach(gameObject, fadeWhenOccluding: true, castLongShadow: false,
                contactScale: 1.15f, contactAlpha: 0.34f);
        }

        public void SetHistoryState(bool rewardOpened, bool completed)
        {
            RewardOpened = rewardOpened;
            Completed = completed;

            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
                return;

            if (completed)
                ApplyColor(Color.Lerp(_baseColor, new Color(0.35f, 0.35f, 0.35f, 1f), 0.55f));
            else if (rewardOpened)
                ApplyColor(Color.Lerp(_baseColor, Color.white, 0.30f));
            else
                ApplyColor(_baseColor);
        }

        void ApplyColor(Color color)
        {
            if (_visual != null)
                _visual.ApplyColor(color);
            else if (_renderer != null)
                _renderer.color = color;
        }

        public bool ContainsWorldPoint(Vector2 worldPoint)
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            return _renderer != null &&
                _renderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, _renderer.bounds.center.z));
        }
    }
}

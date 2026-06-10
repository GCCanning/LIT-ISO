using UnityEngine;

namespace IsoCore.Foundation
{
    public sealed class FoundationInstanceDecoration : MonoBehaviour
    {
        SpriteRenderer _renderer;

        public string DisplayName { get; private set; }
        public int Wx { get; private set; }
        public int Wy { get; private set; }
        public bool IsExitPortal { get; private set; }
        public bool IsDungeonExit { get; private set; }
        public bool IsDungeonReward { get; private set; }
        public int FootprintWidth { get; private set; } = 1;
        public int FootprintHeight { get; private set; } = 1;
        public float HoverLift { get; private set; } = 0.08f;
        public float HoverHighlightScale => Mathf.Clamp(Mathf.Max(FootprintWidth, FootprintHeight) * 0.95f, 1f, 3.5f);
        public Color HoverHighlightColor => IsDungeonExit
            ? new Color(1f, 0.50f, 0.90f, 0.84f)
            : IsDungeonReward
                ? new Color(0.52f, 1f, 0.78f, 0.84f)
                : IsExitPortal
                    ? new Color(0.62f, 0.82f, 1f, 0.84f)
                    : new Color(1f, 0.94f, 0.62f, 0.80f);
        public int SortingOrder => _renderer != null ? _renderer.sortingOrder : 0;

        public void Init(string displayName, int wx, int wy, SpriteRenderer renderer, bool isExitPortal)
        {
            Init(displayName, wx, wy, renderer, isExitPortal, false, false, 1, 1, 0.08f);
        }

        public void Init(string displayName, int wx, int wy, SpriteRenderer renderer, bool isExitPortal,
            bool isDungeonExit, bool isDungeonReward, int footprintW = 1, int footprintH = 1, float hoverLift = 0.08f)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Decoration" : displayName;
            Wx = wx;
            Wy = wy;
            _renderer = renderer;
            IsExitPortal = isExitPortal;
            IsDungeonExit = isDungeonExit;
            IsDungeonReward = isDungeonReward;
            FootprintWidth = Mathf.Max(1, footprintW);
            FootprintHeight = Mathf.Max(1, footprintH);
            HoverLift = Mathf.Max(0.04f, hoverLift);
        }

        public bool Contains(Vector2 worldPoint)
        {
            return _renderer != null && _renderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, _renderer.bounds.center.z));
        }

        public Vector3 HighlightPosition => transform.position;
    }
}

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
        public int SortingOrder => _renderer != null ? _renderer.sortingOrder : 0;

        public void Init(string displayName, int wx, int wy, SpriteRenderer renderer, bool isExitPortal)
        {
            Init(displayName, wx, wy, renderer, isExitPortal, false, false);
        }

        public void Init(string displayName, int wx, int wy, SpriteRenderer renderer, bool isExitPortal,
            bool isDungeonExit, bool isDungeonReward)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Decoration" : displayName;
            Wx = wx;
            Wy = wy;
            _renderer = renderer;
            IsExitPortal = isExitPortal;
            IsDungeonExit = isDungeonExit;
            IsDungeonReward = isDungeonReward;
        }

        public bool Contains(Vector2 worldPoint)
        {
            return _renderer != null && _renderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, _renderer.bounds.center.z));
        }

        public Vector3 HighlightPosition => transform.position;
    }
}

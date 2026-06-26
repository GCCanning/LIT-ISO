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

        // --- World station / board interactivity (added 2026-06) ---------------
        // Most decorations are pure scenery: these stay default (None/0/false) so
        // existing props are unaffected. The decoration SPAWN site
        // (FoundationInstanceSystem.SpawnLayoutProps) detects station/board props by
        // their sprite key and calls MarkStation / MarkQuestBoard so PlayerInteraction
        // can route a click to the Crafting screen or the Quest Board.

        /// <summary>Crafting station this prop represents, or None for plain scenery.</summary>
        public StationType StationType { get; private set; } = StationType.None;

        /// <summary>Tier of the station (higher tier -> higher-tier recipes/outputs).
        /// Defaults to 0; the spawn site sets 1 today.
        /// TODO(tiers): data-drive this from the layout/placement data once
        /// FoundationInteriorPropPlacement carries a tier field.</summary>
        public int StationTier { get; private set; } = 0;

        /// <summary>True when this prop is a quest / notice board (opens Quest Board).</summary>
        public bool IsQuestBoard { get; private set; }

        /// <summary>True when this prop should route a click to the Crafting screen.</summary>
        public bool IsCraftingStation => StationType != StationType.None;
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

        /// <summary>Flag this decoration as an interactable crafting station of the
        /// given type/tier. Called from the decoration spawn site after Init.</summary>
        public void MarkStation(StationType stationType, int tier = 1)
        {
            StationType = stationType;
            StationTier = Mathf.Max(0, tier);
            IsQuestBoard = false;
        }

        /// <summary>Flag this decoration as a quest / notice board.</summary>
        public void MarkQuestBoard()
        {
            IsQuestBoard = true;
            StationType = StationType.None;
            StationTier = 0;
        }

        public bool Contains(Vector2 worldPoint)
        {
            return _renderer != null && _renderer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, _renderer.bounds.center.z));
        }

        public Vector3 HighlightPosition => transform.position;
    }
}

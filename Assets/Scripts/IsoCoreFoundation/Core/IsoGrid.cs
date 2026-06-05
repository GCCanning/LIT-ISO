using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Pure isometric coordinate + sorting math. Single source of truth for the
    /// 2:1 iso footprint, cell&lt;-&gt;world conversion, and draw-order sorting.
    /// No system reads sort order or positions from anywhere else.
    /// </summary>
    public static class IsoGrid
    {
        // 2:1 isometric footprint. One ground cell occupies 1.0 x 0.5 world units.
        public const float TileWidth = 1.0f;
        public const float TileHeight = 0.5f;
        public const float TileHalfW = TileWidth * 0.5f;   // 0.50
        public const float TileHalfH = TileHeight * 0.5f;  // 0.25

        // Each discrete height level lifts the visual Y by this much.
        public const float HeightStep = 0.25f;

        // Sorting: order increases with iso depth (cx+cy), then height, then entity layer.
        // DepthScale must exceed (MaxHeight*HeightScale + maxEntityLayer) so a deeper
        // cell never bleeds into a shallower one. 7*8 + 7 = 63 < 64. OK.
        public const int DepthScale = 64;
        public const int HeightScale = 8;

        // Entity layers within a single cell (ground at the bottom).
        public const int LayerGround = 0;
        public const int LayerFloor = 1;
        public const int LayerProp = 4;   // placeables, resource nodes
        public const int LayerActor = 5;  // player, mobs
        public const int LayerPreview = 7;

        /// <summary>Cell (cx,cy) at a height level -> world position (z=0).</summary>
        public static Vector3 CellToWorld(int cx, int cy, int height = 0)
        {
            float x = (cx - cy) * TileHalfW;
            float y = (cx + cy) * TileHalfH + height * HeightStep;
            return new Vector3(x, y, 0f);
        }

        /// <summary>Y of a cell's ground plane, excluding height lift.</summary>
        public static float GroundPlaneY(int cx, int cy) => (cx + cy) * TileHalfH;

        /// <summary>
        /// World position (interpreted on the height-0 plane) -> nearest cell.
        /// Used for mouse targeting and player ground tracking; height is resolved
        /// separately from the cell data.
        /// </summary>
        public static Vector2Int WorldToCell(Vector3 world)
        {
            float a = world.x / TileHalfW; // cx - cy
            float b = world.y / TileHalfH; // cx + cy (height-0 plane)
            int cx = Mathf.RoundToInt((a + b) * 0.5f);
            int cy = Mathf.RoundToInt((b - a) * 0.5f);
            return new Vector2Int(cx, cy);
        }

        public static int SortingOrder(int cx, int cy, int height, int entityLayer)
        {
            return (cx + cy) * DepthScale + height * HeightScale + entityLayer;
        }
    }
}

using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// This script demonstrates how to use Rule Tiles for isometric auto-tiling.
/// Rule Tiles automatically select sprite variants based on neighboring tiles.
///
/// From the Unity blog: "A Rule Tile acts as a normal tile with an additional list
/// of tiling parameters, and using these parameters the tile can automatically choose
/// which sprite should be painted based on its neighboring tiles."
///
/// To use Rule Tiles:
/// 1. In Assets, right-click > Create > 2D > Rule Tile (requires 2D Tilemap Extras package)
/// 2. For isometric, use "Isometric Rule Tile" from 2D Extras
/// 3. Configure rules to match:
///    - Top tile (full sprite with top+sides visible)
///    - Left-edge tile (no tile to left)
///    - Right-edge tile (no tile to right)
///    - Interior tile (surrounded)
///    - Water/Grass/Stone transitions
/// 4. Drag Rule Tile into Tile Palette and paint normally
/// 5. Rule Tile automatically picks correct variant based on neighbors
/// </summary>
public class IsometricAutoTiler : MonoBehaviour
{
    [Header("Rule Tiles for Auto-Tiling")]
    [Tooltip("Grass Rule Tile with variants for edges and corners")]
    public Tile grassRuleTile;

    [Tooltip("Water Rule Tile with shore variants")]
    public Tile waterRuleTile;

    [Tooltip("Stone Rule Tile with edge variants")]
    public Tile stoneRuleTile;

    [Header("Tilemap Reference")]
    public Tilemap tilemap;

    // The Rule Tiles handle all the neighbor checking automatically
    // Just paint with the Rule Tile and it selects the right sprite

    public void PaintWithRuleTile(Vector3Int position, Tile ruleTile)
    {
        if (tilemap == null || ruleTile == null)
            return;

        // Paint the Rule Tile at this position
        tilemap.SetTile(position, ruleTile);

        // Rule Tile will automatically:
        // 1. Check neighboring tile positions
        // 2. Compare against its configured rules
        // 3. Select the correct sprite variant
        // 4. Display the appropriate sprite for this context

        // Optionally refresh neighbors so they update their variants too
        RefreshTile(position);
    }

    private void RefreshTile(Vector3Int position)
    {
        if (tilemap == null) return;

        // Refresh the tile and its neighbors so Rule Tiles re-evaluate
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector3Int checkPos = position + new Vector3Int(x, y, 0);
                tilemap.RefreshTile(checkPos);
            }
        }
    }
}

/*
HOW RULE TILES WORK (from Unity blog):

Rule Tiles are configured with a list of "rules". Each rule has:
- A pattern (which neighbors must match certain conditions)
- Output sprite(s) to use when that pattern is detected

Common patterns for isometric tiles:
1. "All neighbors present" → Interior tile (no exposed sides)
2. "No neighbor on left" → Left-edge tile (shows left side)
3. "No neighbor on right" → Right-edge tile (shows right side)
4. "No neighbors on left or right" → Isolated tile (shows all sides)
5. "Water neighbor on left, grass on right" → Transition tile

The Rule Tile automatically evaluates these rules in order and
applies the first matching rule's sprite.

For random variety, you can set a rule to "Random" output and
specify multiple sprite variants, and it will randomly pick one.

This is far more powerful than manual tile selection because
all the context-aware logic is automated!
*/

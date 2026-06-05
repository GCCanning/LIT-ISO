# Item icons — drop them here

Item icons resolve at runtime via `ItemIconResolver.Resolve(itemId)`:

1. **Primary:** `content.Items.Get(itemId)?.Icon` — Codex's `ItemDefinition.icon` field on
   each item's ScriptableObject asset (set in the inspector).
2. **Fallback:** `Resources.Load<Sprite>("Items/" + itemId)` — drop a PNG named exactly
   `<itemId>.png` into THIS folder and it appears automatically in hotbar / inventory /
   crafting with no code change.

## Conventions
- Filename matches the `itemId` exactly: lowercase, snake_case (e.g. `wood.png`,
  `iron_ore.png`, `potion_health.png`).
- Import as **Sprite (2D and UI)**, **Filter Mode = Point**, **Compression = None**.
- 16×16, 32×32, or 64×64 sources all fine — the hotbar slot scales to fit.

## Resolution order recap
| Try | Source |
|---|---|
| 1 | `ItemDefinition.Icon` (set on the SO in Codex's Foundation content) |
| 2 | `Resources/Items/<itemId>.png` (this folder) |
| 3 | null — view shows the empty-slot placeholder |

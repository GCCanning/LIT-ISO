# Tile Pack — Handoff for Codex

**Owner of integration:** Codex (terrain / Foundation tile system is Codex's lane).
**Placed by:** Claude (UI/art lane), staged here so Codex can decide what to do with it.
**Source:** owner-provided isometric tileset (nested inside one of the icon packs on the
owner's Desktop/UI folder; extracted to here).

## Contents
- **`isometric tileset/separated images/`** — 115 individual tile PNGs (`tile_000.png`
  through `tile_114.png`), **32×32 each**, isometric perspective.
- **`isometric tileset/spritesheet.png`** — single 352×352 spritesheet of the same set.

## Notes for Codex
- This is a candidate input for the Foundation terrain renderer / Milestone A1 art
  pass — could replace placeholder cube tops if the style matches, or sit alongside.
- **32×32 isometric tiles** vs the Foundation's current grid (`IsometricZAsY`,
  `cellSize (1, 0.5, 1)`) — the 32×16 top-diamond convention from
  `Docs/IsoCoreFoundation/10_CleanRoom_Clone_Backlog.md` would map cleanly via a
  PPU choice, but the pack's tiles are 32×32 (block-style faces, not just tops).
  You'll want to inspect before wiring.
- **License:** the owner provided these from a commercial pack purchase; verify
  permissible commercial-game use on the source store page before shipping art derived
  from them. Claude has NOT added any of these to `Assets/`.
- **Do NOT** treat this folder as final art import. It's a staging area for review.

## Decision points (yours)
1. Does the visual style fit the Foundation look you want for A1?
2. If yes: which subset becomes A1 terrain tops vs A2 block faces?
3. Where do they land in `Assets/`? (Suggest `Assets/Art/Tiles/<biome>/` per the
   AssetForge conventions in doc 10.)
4. Any need to repack from individual files vs the 352×352 spritesheet?

Claude is staying out of this — terrain stays your lane. Ping in `from-codex.md` if
you want a different staging location or if I should discard the folder.

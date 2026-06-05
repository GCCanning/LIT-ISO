# Biome Decoration Drop Folder

Drop your decoration PNGs into THIS folder (`Assets/Art/BiomeDecorations/`) and
name each file **exactly** as listed in the "Filename" column below. The names are
lowercase, kebab-case, `.png`. Exact names matter — my import + biome-wiring code
finds each sprite by this precise path, e.g.
`Assets/Art/BiomeDecorations/tree-oak.png`.

After you've dropped them in, tell me and I'll run the import pass (Point filter,
uncompressed, full-rect mesh, bottom-center pivot for tall props) and wire each
into the correct biome's decoration list.

---

## Image specs (so they slot into the isometric world correctly)
- **Transparent background** (PNG with alpha)
- **Tall sprites are fine** — props like trees/walls/pillars should rise above
  their tile (the source set used ~130×230 px on a 128×64 tile). The base of the
  prop sits on the tile; the rest overhangs upward.
- **Pivot:** bottom-center (I set this on import — you don't need to).
- Any resolution is OK; consistent scale across a set looks best.

---

## 🌿 Nature props → Plains / Forest / Mountain biomes

| Filename | What it is | Target biome(s) |
|----------|-----------|-----------------|
| `tree-oak.png` | Broad green oak tree | Plains |
| `tree-pine.png` | Tall pine / conifer | Mountain, Plains edge |
| `bush.png` | Small leafy bush | Plains |
| `log.png` | Fallen log | Plains, Forest |
| `rock-large.png` | Large boulder | Mountain, Plains |
| `rock-small.png` | Small rock / stone | All land biomes |
| `flower-red.png` | Red flower cluster | Plains |
| `flower-blue.png` | Blue flower cluster | Plains |

## 🌉 Structures → crossings / special

| Filename | What it is | Target biome(s) |
|----------|-----------|-----------------|
| `bridge.png` | Wooden bridge segment | Rivers / transitions |

## 💀 Dungeon / Temple props → Temple biome & future dungeons

| Filename | What it is | Target biome(s) |
|----------|-----------|-----------------|
| `dungeon-wall-corner.png` | Stone wall corner piece | Temple / Dungeon |
| `dungeon-wall-ne-sw.png` | Stone wall (NE–SW run) | Temple / Dungeon |
| `dungeon-wall-nw-se.png` | Stone wall (NW–SE run) | Temple / Dungeon |
| `pillar-stone.png` | Stone pillar / column | Temple |
| `torch-stand.png` | Standing torch | Temple / Dungeon |
| `chest-closed.png` | Closed treasure chest | Dungeon (lootable later) |
| `barrel.png` | Wooden barrel | Temple / Dungeon |
| `crate-wood.png` | Wooden crate | Temple / Dungeon |
| `skull-pile.png` | Pile of skulls / bones | Desert, Dungeon |
| `cauldron.png` | Iron cauldron | Dungeon |
| `cage-iron.png` | Iron cage | Dungeon |
| `altar-dark.png` | Dark ritual altar | Temple (landmark) |
| `stairs-down.png` | Descending stairs | Dungeon entrance |

---

## Total: 22 sprites

**Minimum to make a big difference:** the 8 nature props alone will transform the
overworld biomes. The dungeon props matter once we build out Temple/dungeon content
(Sprint 2–3), so you can drop those in later if you want to stage it.

When ready, just say **"decorations dropped"** and I'll take it from there.

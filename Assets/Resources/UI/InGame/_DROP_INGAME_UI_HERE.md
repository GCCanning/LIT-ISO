# In-game UI images — drop them here

The uGUI in-game UI (`GameUIController`) auto-loads these by filename from
`Resources/UI/InGame/`. Every slot is **optional** with a procedural fallback, so add
art one at a time. Pixel art → import as **Sprite**, **Point** filter, **No compression**;
for any framed/9-sliced piece set the **Border** in the Sprite Editor.

## Bottom HUD bar (quick bar + vitals)
| Filename | Slot | Suggested size |
|---|---|---|
| `bar_bg.png` | bar background strip | wide, 9-slice |
| `slot.png` | hotbar slot frame (empty) | 64×64, 9-slice |
| `slot_selected.png` | selected slot highlight | 64×64, 9-slice |
| `bar_health_fill.png` | health bar fill | 256×24, 9-slice |
| `bar_hunger_fill.png` | hunger bar fill | 256×24, 9-slice |
| `bar_xp_fill.png` | XP/level bar fill | 256×16, 9-slice |
| `bar_track.png` | empty bar track (shared) | 256×24, 9-slice |

## Inventory screen (later)
| `panel.png` | window frame | 9-slice |
| `inv_slot.png` | inventory slot | 64×64, 9-slice |

## Crafting screen (later)
| `craft_panel.png` | window frame | 9-slice |
| `craft_button.png` | craft button | 9-slice |

## Status / System page (LitRPG, later)
| `system_panel.png` | the "System" window frame | 9-slice, sci-fi/arcane vibe |
| `system_header.png` | title banner (optional) | wide |
| stat icons: `icon_hp.png`, `icon_hunger.png`, `icon_level.png`, `icon_str.png`, … | 32×32 each |

## Item icons (separate — these belong to the game's item set)
Item icons (`wood`, `stone`, `carrot`, tools…) resolve from the item database, not this
folder. Codex's lane will wire those; coordinate naming when you generate them.

> Filenames are lowercase and exact. The loader looks for `Resources/UI/InGame/<name>`.

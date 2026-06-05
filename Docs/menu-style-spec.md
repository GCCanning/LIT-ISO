# Welcome Menu — Visual Style Spec

> **Status:** ready to execute *after* Milestone A1 sets the game's color palette.
> Owner: Claude (menu/art/integration lane). Target file:
> `Assets/Scripts/UI/WelcomeScreenManager.cs` (procedural UI; no prefab to author).
> This spec is **palette-agnostic** — it defines tokens and rules; A1 fills the colors.

## How to use
1. When A1 terrain art exists, sample its palette → fill the **Color tokens** table below.
2. Apply tokens to the serialized fields (see **Field mapping**) — mostly value swaps.
3. Implement the few structural items (background fit, title, button states, 8px grid).
4. Keep everything sprite-ready: every panel/button is an `Image`, so swapping a flat
   color for a 9-sliced sprite later is a one-line change.

## Design intent
Cozy isometric survival, read at a glance, calm not flashy. The campfire splash is the
hero; UI sits in a single grounded panel that never fights the art. Warm parchment text
on a cool dark panel (firelight vs dusk). Generous breathing room over density.

## Color tokens (fill from A1 palette; current values = sensible placeholders)
| Token | Role | Placeholder (current) |
|---|---|---|
| `surface/panel` | main panel fill | `0.07, 0.09, 0.13, 0.95` |
| `surface/panel-border` | panel edge (1–2px) | `0.30, 0.36, 0.42, 1` |
| `surface/input` | text-field fill | `0.05, 0.06, 0.09, 0.9` |
| `control/button` | button rest | `0.15, 0.18, 0.24, 0.9` |
| `control/button-hover` | button hover | `0.22, 0.26, 0.34, 1` |
| `control/button-press` | button active (new) | hover × 0.85 value |
| `control/button-disabled` | disabled (new) | button × 0.6, text α 0.4 |
| `text/primary` | buttons, titles (warm) | `0.95, 0.91, 0.74, 1` |
| `text/secondary` | labels, hints (muted) | `0.75, 0.75, 0.75, 1` |
| `text/input` | typed text | `0.90, 0.90, 0.90, 1` |
| `accent/primary` | focus ring, slider fill, selected row (new) | derive from A1 (e.g. ember/leaf) |
| `scrim` | dark gradient behind panel for legibility (new) | `0, 0, 0, 0.35` |

> Contrast rule: `text/primary` on `surface/panel` ≥ 4.5:1; `text/secondary` ≥ 3:1.

## Layout — 8px grid
- **Base unit `u = 8px`.** All spacing/padding/sizes are multiples of `u`.
- Spacing scale: `xs=4, sm=8, md=16, lg=24, xl=40`.
- Panel: width `560` (= 70u), padding `lg` (24) all sides, centered (anchor mid-center).
- Vertical rhythm inside panel: section gap `lg`, control gap `md`, label→control `sm`.
- Buttons: height `56` (7u), full panel-content width; corner radius via 9-slice later.
- Replace current `spacing = 12` with `md (16)`; `buttonHeight 50 → 56`;
  `panelWidth 500 → 560`; `panelHeight 600 → auto/640` so content drives height.

## Type scale (LitIsoFont)
| Style | Size | Use |
|---|---|---|
| Title | 48 | "LIT-ISO" wordmark on Main Menu |
| H2 | 28 | screen headers ("Create World", "Load World") |
| Button | 22 | menu buttons |
| Body | 18 | labels, list rows |
| Caption | 14 | hints (seed help, difficulty desc) |
Letter-spacing +4% on Title/H2; line-height 1.3 on body.

## Components
- **Panel:** single rounded card; `surface/panel` + 1px `surface/panel-border`; drop a
  `scrim` gradient image behind it (full-screen, bottom-darker) so text stays legible
  over any splash.
- **Button (4 states):** rest / hover / press / disabled per tokens. Add press feedback
  (`CreateMenuButton`/`CreateSmallButton` currently only do rest+hover — add
  `OnPointerDown` → press color, `OnPointerUp` → hover). Selected/primary action
  ("Play", "Create") gets an `accent/primary` left-edge bar or underline.
- **Text input:** `surface/input` fill, 1px border that switches to `accent/primary`
  on focus; placeholder in `text/secondary` italic.
- **Difficulty slider:** track `surface/input`, fill `accent/primary`, knob `text/primary`;
  caption line under it shows the selected tier name + one-line description.
- **Load-world list row:** Body name in `text/primary`, caption meta (seed · difficulty ·
  date) in `text/secondary`; whole row is a button; selected row tinted `accent/primary`
  at α 0.18; Delete as a small destructive (red-tinted) `CreateSmallButton` on the right.

## Background treatment
- Campfire splash = full-screen, **aspect-fill** (cover, not stretch): set the `Image`
  to preserve aspect and scale to cover the canvas (anchor stretch, `preserveAspect`
  with overscan), so it never distorts at any resolution.
- Layer order: splash → `scrim` gradient → panel → controls.

## Per-screen layout
- **Main Menu:** Title (top, centered) → New Game / Load World / Options / Quit stacked
  with `md` gaps. "New Game" is the primary (accent) button.
- **Create World:** H2 header → Name (label+input) → Seed (label+input+caption hint
  "blank = random") → Difficulty (label+slider+caption) → row: Back (secondary) +
  Create (primary), right-aligned.
- **Load World:** H2 header → scroll list of rows (empty state: centered Caption "No
  saved worlds yet") → Back.
- **Options:** H2 header → simple labeled rows (placeholder until settings exist) → Back.

## Field mapping (mechanical edits)
| Spec | `WelcomeScreenManager` field / method |
|---|---|
| panel size/pad | `panelWidth`, `panelHeight`, `spacing` |
| button height | `buttonHeight` |
| color tokens | `panelBg`, `panelBorder`, `buttonBg`, `buttonBgHover`, `inputBg`, `inputText`, `labelText`, `buttonText` (+ add `buttonPress`, `buttonDisabled`, `accent`, `scrim`) |
| button states | `CreateMenuButton`, `CreateSmallButton` (add press/disabled) |
| labels/captions | `CreateLabel` (add a caption variant) |
| screens | `BuildMainMenu`, `BuildCreateWorld`, `BuildLoadGame`, `BuildOptions` |
| background fit | `BuildCanvas` (background `Image` setup) |

## Execution checklist (post-A1)
- [ ] Fill color tokens from A1 palette; add 4 new color fields.
- [ ] Apply 8px grid constants (width 560, button 56, gaps md/lg).
- [ ] Background aspect-fill + scrim layer.
- [ ] Title wordmark + H2 headers (type scale).
- [ ] Button press/disabled states + primary-action accent.
- [ ] Input focus ring; slider fill/caption; load-row selected/delete styling.
- [ ] Verify contrast ratios; play-test at 16:9 and ultrawide.

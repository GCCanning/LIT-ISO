# LIT-ISO Prop & Decoration Catalog — for owner review
**Date:** 2026-06-11 · Beyond the basic biome props (trees/bushes/rocks already in the pipeline).
**Quality ladder (5 tiers):** Common → Uncommon → Rare → Epic → Mythical

## The tier visual language (one rule, applied everywhere)
Players must read rarity at a glance, before any tooltip:
| Tier | Visual rule |
|---|---|
| Common | plain material, muted palette |
| Uncommon | richer color, intact/healthy look |
| Rare | blue-cyan glow accents (gems/cracks/runes) |
| Epic | purple-gold, ornate detailing |
| Mythical | white-gold radiance + animated shimmer (frame pair) |

**Generation method — three cost tiers (owner-refined 2026-06-11):**
1. **FREE — local palette/hue shift** (Claude's scripts, zero generations): tier
   recolors where only color changes — vein color on the same rock, wood→iron→gold
   chest banding, herb species recolors. Same technique as the mask-locked tiles.
2. **CHEAP — `/objects/{id}/states`** (a few gens): when the tier adds *structure* —
   runes, ornament, locks, glow halos (Rare+ usually).
3. **FULL — `create-1-direction-object`** (~25 gens): once per base silhouette only.

Rule of thumb: Common/Uncommon/Rare = base + local recolors; Epic/Mythical = generated
states (ornament + glow needs real pixels, not hue).

## THE FOOTPRINT CONTRACT (owner rule, 2026-06-11 — applies to ALL props, incl. existing)
Every prop declares a footprint (1x1, 2x1, 2x2...). **The BASE of the sprite must sit
entirely inside that footprint** — no ground-level bleed into neighbor tiles. Overhang
above ground level is fine (tree canopy may shade neighbors; trunks may not).
- Generation: prompts specify "base fits within N tile(s)"; QA measures the bottom 25%
  of content pixels against footprint width and rejects violators automatically.
- Existing props get the same audit; violators are trimmed, rescaled, or get their
  footprint redeclared. (Audit script: Tools/PixelLab/audit_prop_footprints.py)

---

## A. Ore ladder, Common → Mythical (owner-requested scale, 2026-06-11) — PRIORITY 1
One ore per tier; vein color carries the tier read (free recolors on shared rock bases).

| Tier | Ore | Vein look | Found | Description |
|---|---|---|---|---|
| **Common** | Copper | dull orange streaks | plains/forest outcrops, h1+ | The trial's first metal. Soft, forgiving, everywhere — every smith's first scar. |
| **Common** | Iron | dark gray bands | hills h2+, dungeon T1-2 | The workhorse. Tools that last, armor that holds. The world runs on iron. |
| **Uncommon** | Silver | pale moonlit sheen, faint night glow | mountains h3+, riverside cliffs | Drinks moonlight. Wards and fine-work love it; so do the things that fear wards. |
| **Rare** | Gold | warm glints, blue-glow flecks | mountain h4+, dungeon T3-4 | Soft as a promise, precious as one too. Conducts mana better than it holds an edge. |
| **Epic** | Manacrystal | violet-blue crystal cluster, pulsing | deep dungeons T4-5, near portals | Raw, crystallized possibility. Hums against your teeth. Handle with intent. |
| **Mythical** | Starmetal | near-black meteor stone, white-gold radiant fractures, shimmer (anim pair) | summit peaks h6+, T6 dungeon vaults, fallen-star events | Not from this world — like you. The System has no entry for it. It remembers falling. |

Visual/gameplay rules: tier = vein color + glow per the global tier language; node SIZE also
steps up slightly per tier; Mythical is animated. Spawn rates fall ~5x per tier; depleted
states for all. Copper/Iron/Silver/Gold share one rock silhouette (recolors); Manacrystal
and Starmetal get their own generated bases.
Each also gets a **depleted state** (mined-out look) — 1 extra variant per node.
**Cost cut:** copper/iron/silver/gold = ONE rock-with-vein base + free local vein
recolors; only manacrystal + starmetal need their own generated bases (crystal/meteor
silhouettes). Depleted = local desaturate + crack overlay where possible.
*~3 generated bases + free recolors = same 36 sprites at a third of the spend*

## B. Stone & special gathering — PRIORITY 2
Marble block (white, building-tier), Obsidian shard node (volcanic/dungeon), Salt crust
(cooking), Clay deposit (riverbank apron prop), Flint scatter (early-game).
*~5 bases, tiers only where it matters (marble/obsidian): ~9 sprites*

## C. Food & forage (wild, tiered where magical) — PRIORITY 1
| Prop | Tiers |
|---|---|
| Berry bush (red/blue variants) | Common ×2 |
| Wild herb clusters (3 species: healing/mana/stamina) | Common ×3 |
| Mushroom ring | Common + Glowcap (Rare, glows at night) |
| Wild wheat / oat patch | Common |
| Honey hive (on-tree prop) | Uncommon |
| Pumpkin / melon wild patch | Common ×2 |
| Golden apple tree | Epic (small canopy tree with glints) |
| Moonfruit vine | Mythical (night-blooming, shimmer pair) |
*~12 sprites + 2 animated pairs*

## D. Fishing spots — PRIORITY 3
Ripple ring (Common), bubbling spot (Uncommon), glimmer shoal (Rare), abyssal glow
(Epic, deep water only). Animated 2-frame each. *~4 animated pairs*

## E. Dungeon props — PRIORITY 1 (pairs with the dungeon tile family)
| Prop | Grades |
|---|---|
| Chests | ONE wooden base generated (+open state); Iron/Gilded = free local hue+banding recolors; Arcane-sealed + Mythic relic = generated states (runes/glow) + opened each |
| Mimic chest | 1 (reuses Gilded silhouette — that's the trap) |
| Breakable urns/pots | 2 variants + broken state |
| Coin pile / treasure mound | small / large |
| Bone pile + skull | 2 |
| Brazier (lit/unlit) | animated flame pair |
| Floor rune decal | per dungeon tier palette (3) |
| Sarcophagus | closed/open (deep tiers) |
| Lever + pressure plate | interaction props, 2 states each |
*~28 sprites + 1 animated pair*

## F. World landmarks & POI decor — PRIORITY 2
| Prop | Notes |
|---|---|
| Affinity shrines ×7 | one per affinity (Ember/Tide/Root/Stone/Gale/Glimmer/Hearth), tier-Rare glow language |
| Waystone | fast-travel anchor candidate, dormant/awakened states |
| Abandoned campsite kit | cold firepit, torn tent, log seat (3) |
| Ruin kit v2 | broken pillar, arch fragment, rubble mound, mossy statue (4) — replaces the 2 rough decors |
| Signpost + fence pieces | straight/corner/gate (4) |
| Bridge planks | for river crossings (2) |
*~22 sprites*

## H. Interior props & floor overlays — PRIORITY 1 (owner addition 2026-06-11)
**New render category: FLOOR OVERLAY** — sits cleanly on top of a floor tile, fully
walkable, drawn above the tile but below all props/characters, no collision.
(Codex: one new layer in the renderer between surface and decoration — flagged in comms.)

| Group | Props |
|---|---|
| Floor overlays | carpet/rug (3 shapes: runner, square, round) × tier recolors; wood plank floor tiles (2 tones); stone tile floor; door mat |
| Library | bookshelf (full/half/corner), reading desk + chair, lectern, globe, candelabra, scroll rack, book piles (2), librarian counter |
| Tavern | bar counter (straight/corner), bar stools, round table + chairs, bench table, keg stack, hanging mugs rack, hearth (animated pair exists), notice board, stage corner |
| Guild hall | quest board (the big one), trophy wall mounts (3), armory rack, banner hangings (tier-colored — free recolors), war table, ledger desk, ranking plaque |
*~35 base sprites + free tier recolors; replaces the imported Kenney/free-pack interiors with originals*

## I. Building exteriors with RANK EVOLUTION — PRIORITY 1
Same footprint per building (door stays put — placement/save-safe), richer build per rank:
| Building | Rank ladder (visual) |
|---|---|
| Tavern | R1 current-style timber → R2 stone foundation + sign → R3 two-tone roof, lanterns, chimney smoke (anim pair) |
| Guild hall | R1 modest lodge → R2 banners + stone arch → R3 tower corner + braziers → R4 epic standard flying |
| Library | R1 cottage → R2 dome corner + skylight → R3 arcane observatory hints (Rare-glow windows) |
| Workshop/forge (future) | R1 → R3 same pattern |
**Method:** generate R1 base per building (square-ish multi-cell like current tavern,
~128-192px); higher ranks via generated states on the same base (structure changes are
real pixels). Footprint contract identical across ranks.
*~3 bases + ~8 rank states*

## J. Craftable campsites, Common → Mythical — PRIORITY 1
Placeable at night on long cross-biome runs; pairs with the existing camping/ward system.
| Tier | Kit look | Gameplay hook (Codex) |
|---|---|---|
| Common | bedroll + tiny firepit | basic rest, small ward |
| Uncommon | + canvas tent, log seat | faster rest, +ward radius |
| Rare | + blue-flame lantern, drying rack | mob ward strong, weather shelter |
| Epic | pavilion tent, brazier pair, banner | party-size, stat buff on rest |
| Mythical | radiant ward circle, floating ember lights (anim pair) | near-sanctuary, teleport anchor? |
Each = one composite placeable sprite (~2x2 cells) + lit/unlit states; lower tiers via
recolor where possible, Epic/Mythical generated.
*~5 composites + ~5 lit states + 1 anim pair*

## K. Farming kit (owner additions 2026-06-11) — PRIORITY 1
| Group | Content |
|---|---|
| Farming tileset (tiles_6 family) | tilled soil dry/watered, soil-with-mulch, raised bed, farm path, puddled corner — generated like the other tile families |
| Crop growth stages | **the recolor/stage trick:** per crop, generate the MATURE plant once; earlier stages derived locally (sprout = shared tiny sprout sprite, mid = scaled/pruned cut of mature). Crops: carrot, wheat, potato, pumpkin, sugar beet, + 2 magical (manabloom Rare, moonwheat Epic) |
| Scarecrow | 2 variants (straw common; "warded" version with faint glow = also functions vs night mobs?) |
| Field furniture | fence straight/corner/gate (shared with F), water trough, compost bin, seed crate |

## L. Market & travel (owner additions) — PRIORITY 2
| Prop | Notes |
|---|---|
| Market stalls | 3 stall types (produce awning, goods table, hanging-wares) + tier recolors for vendor quality |
| **Boat** | ocean crossing vessel: small sailboat, 2-3 cell footprint, idle-bob anim pair + sail furled/unfurled states. Gameplay (Codex): board at coast, travel over ocean water cells, disembark on beach. The ocean stops being a wall. |
| Dock pieces | short pier planks (2) so boats have somewhere to live |

## M. Cinematic screens (owner-specced) — PRIORITY 1, art via PixelLab scene route
1. **Class Selection backdrop**: pixel cinematic — a void; the player figure small in
   frame, looking up; an incomprehensible BEING (vast, geometric-eldritch, softly
   animated) gazes down over the floating LIT-ISO world (the menu-scene island
   miniaturized below). Animated via the region trick (being's glow + void motes).
   Class options then present as a **side-by-side carousel** (Word-style): scroll
   between class cards, each with a simple class preview sprite, rarity color, and
   starting skills list. (ClassAssignmentView gets a carousel rework — ledger item.)
2. **Transmigration intro** (first entry into a new world): "booting up" sequence —
   black, System lines typing with flicker/glitch, disoriented double-vision pulses,
   slow wake into the world. IMPLEMENTED procedurally (TransmigrationIntro.cs);
   art pass optional later.

## G. Already covered elsewhere (not in this catalog)
Biome trees/bushes/rocks (current props batch), tavern/library interiors (imported packs),
buildings (craft system), UI icons (separate icon route).

---

## Budget & order
| Phase | Content | Est. generations* |
|---|---|---|
| P1a | Ores (A) + Dungeon (E) + Campsites (J) | ~250–350 |
| P1b | Interiors (H) + Building ranks (I) | ~300–400 |
| P2 | Food (C) + Landmarks (F) + Stone (B) | ~250–350 |
| P3 | Fishing (D) + animated shimmer pairs | ~80 |
*hybrid method: generated bases only for unique silhouettes; tier ladders via free local
recolors; generated states reserved for Epic/Mythical ornament. Whole catalog now fits
inside one month's 2,000 with room for the character work.*

## Review questions for Gary
1. Tier count OK at 5 (Common/Uncommon/Rare/Epic/Mythical), or match the existing class ladder exactly?
2. Ore lineup: copper→iron→silver→gold→manacrystal→starmetal — add/remove any (tin? coal?)
3. Mimic chest: yes/no (needs a mob behavior from Codex eventually)?
4. Affinity shrines: all 7 now, or 2–3 pilots first?
5. Anything missing you already know you want (e.g., farm scarecrow, beehive box, market stall props)?

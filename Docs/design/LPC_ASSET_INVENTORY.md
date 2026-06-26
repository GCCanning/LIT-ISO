# LPC Wardrobe — Current Inventory & Expansion List

What our layered character creator (`Resources/Characters/Layers/layer_catalog.json`) ships
today, the full set the Universal LPC project offers, and what's worth importing for the
game's classes/factions. Frame format: **64×64, 8 directions (rows), per-animation columns.**

---

## 1. What we have now (47 items)

| Slot | Items | Notes |
|---|---|---|
| body | `lpc/body` | **21 skin tones** (light…black, lavender, blue, zombie_green, …) |
| head | `lpc/head` | 22 variants (matches skin) |
| eyes | `lpc/eyes` | 8 colours |
| hair | 12 styles | afro, bangs, bob, braid, buzzcut, curly_long, long, messy1, pixie, ponytail, spiked, swoop — **26 hair colours each** |
| shirt (torso) | 3 | clothes_shortsleeve, clothes_longsleeve, clothes_sleeveless — 24 colours each (incl. "leather") |
| pants (legs) | 5 | pants, shorts, skirts_plain, leggings, formal — 24 colours |
| shoes (feet) | 3 | shoes, boots, sandals — 24 colours |
| accessory | 5 | cape_solid, cape_tattered, hat_bandana, hat_cap_feather, facial_glasses |

**Animations we have (6):** `walk` (9 cols), `cast`/spellcast (7), `thrust` (8), `slash` (6),
`shoot` (13), `hurt` (6). **Body types:** male, female.

**Bottom line:** we have a solid *commoner* wardrobe (clothes, hair, capes, hats) but **no
armour, no weapons, no helmets/hoods, no robes** — which is why NPC class-theming is currently
limited to capes/bandanas/colour.

---

## 2. Full Universal LPC set (available to import)

The Universal LPC Spritesheet Character Generator (CC-BY-SA / GPL / OGA-BY licensed) defines
each category as a JSON in its `sheet_definitions/` folder. The major categories beyond what we
have:

**Body / anatomy**
- Body types: **male, female, teen, child, muscular, pregnant**, plus **skeleton, zombie, orc, troll, boarman, lizard, wolf** non-human bodies.
- Head shapes, **ears** (elf, big), **nose**, **eyebrows**, **wrinkles/aging**, **expressions** (overlays), **facial hair** (beards, mustaches, goatees), **eyes** (more colours).

**Headgear (big category — what we're missing most)**
- **Helmets:** plate, barbute, bascinet, great helm, horned, kettle, nasal, spangenhelm, **chainmail coif**, leather cap, **bucket helm**, visor variants.
- **Hoods:** cloth hood, **leather/chain hood**.
- Hats: wizard/pointed hat, straw, tricorne, top hat, crown, tiara, headband, bandana (have), cap (have).

**Torso / arms**
- **Armour:** **plate** (chest), **chainmail**, **leather armour**, scale, **brigandine**.
- **Robes** (mage!), **dresses**, tunics, vests, jackets, **aprons**, corsets, blouses, sleeves (separate).
- **Arms:** plate arms/pauldrons, **bracers**, gloves/gauntlets, **shoulders/spaulders**.

**Legs / feet**
- **Plate legs / greaves**, chain legs, leather legs; robe skirts; armoured boots, **plate boots**.

**Weapons (held + fully animated for slash/thrust/shoot/cast)**
- Melee: **sword** (arming, longsword, rapier, saber, katana), **dagger**, **axe** (hand/great), **mace**, **hammer/warhammer**, **spear**, **halberd**, **glaive**, **whip**, club, sickle, cane.
- Ranged: **bow** + **arrows/quiver**, **crossbow**, **sling**.
- Magic: **staff**, **wand**, spellbook, **magic effects** (the `cast` animation).
- **Shields:** round, heater, kite, spartan, etc.
- Tools: **watering can** (drives the `watering` anim), fishing rod, pickaxe, hoe.

**Accessories**
- **Capes** (have solid/tattered) + more, **quivers**, **backpacks**, belts, sashes, necklaces, earrings, **wings**, **tails**, **horns**, scarves.

**Animations — full set (we have 6 of these):**
- Core (we have): **spellcast, thrust, walk, slash, shoot, hurt**.
- Also standard: **watering** (tool use).
- **LPC Expanded** (commonly added): **idle, run, jump, sit, climb, emote, combat_idle, 1h backslash/halfslash, jump, and a death pose**.

---

## 3. Recommended additions for *this* game (priority order)

To make the four classes + bandits read instantly and support combat, import these first:

1. **Armour torsos:** plate, chainmail, leather → Tank/Knight silhouettes (vs Mage robe).
2. **Robe** (torso/legs) → Mage.
3. **Helmets + hoods:** a plate helm (Knight/Tank), wizard hat (Mage), **hood** (Rogue/Bandit).
4. **Weapons matched to our abilities:** sword (steady_strike/slash), dagger (Rogue), staff/wand (Mage cast — we already have `ember_spark`/`mana_bolt`), **shield** (Tank guard_step/stone_skin), bow (ranged).
5. **`run` + `idle` animations** → NPCs/player feel alive (idle breathing, running when chasing/fleeing).
6. **`combat_idle`** → bandits/adventurers visibly "ready up" when a fight starts (ties into the faction combat already added).

Each is a drop of the LPC art into `Resources/Characters/Layers/<...>` + one item entry per
category in `layer_catalog.json` (id, slot, variants, draws). Animations are added to the
catalog's `animations[]` (id, cols, fps) and the sheets must include those rows.

**Licensing:** LPC assets are CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0 — fine to use, but each
contributor must be credited (the generator ships a `CREDITS.csv`; we already load
`Characters/Layers/lpc_credits`). Keep crediting when importing new sheets.

Sources: Universal LPC Spritesheet Character Generator (LiberatedPixelCup/sanderfrenken) and
the LPC Expanded animation packs on OpenGameArt.

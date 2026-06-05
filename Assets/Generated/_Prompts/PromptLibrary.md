# LIT-ISO Prompt Library

This is our shared workspace for designing prompts. Edit this together with Claude.

**Style baseline** (apply to ALL prompts unless specifically overridden):
- Isometric 3/4 view (camera angle ~30° pitch, 45° yaw)
- Hand-painted cel-shaded look
- Soft top-left key light, gentle ambient fill
- Transparent background for sprites
- Sharp pixel-perfect silhouettes
- Consistent character proportions (head ≈ 1/6 of body height)
- Fantasy adventure RPG genre
- Color palette: warm muted tones (think early Studio Ghibli + Octopath Traveler)

---

## 🧑 Characters — Base Bodies

Generate FIRST. These become the canvas everything else is layered on.

### Male_Adventurer_Base
```
Isometric RPG character full body, T-pose, male adult adventurer, neutral
expression, athletic build, bald and faceless template (hair and face will be
added as separate layers), wearing tight neutral grey undergarments only,
centered on pure transparent background, 3/4 isometric view, hand-painted cel
shading, sharp clean silhouette, studio lighting from upper-left, pixel-perfect
edges, consistent fantasy art style.
```
Negative:
```
multiple characters, scene, background, shadow on ground, hair, beard, facial
features, clothes, armor, weapon, dramatic pose, blurry, watermark, text, logo,
low quality, photorealistic, anime, dark colors.
```

### Female_Adventurer_Base
```
Isometric RPG character full body, T-pose, female adult adventurer, neutral
expression, athletic build, bald and faceless template (hair and face will be
added as separate layers), wearing tight neutral grey undergarments only,
centered on pure transparent background, 3/4 isometric view, hand-painted cel
shading, sharp clean silhouette, studio lighting from upper-left, pixel-perfect
edges, consistent fantasy art style.
```
Negative: same as Male_Adventurer_Base

### Plain_Template_Base
```
Isometric RPG character full body, T-pose, androgynous adult template, neutral
expression, balanced build between male and female proportions, bald and
faceless, neutral grey full-body undergarments, centered on pure transparent
background, 3/4 isometric view, hand-painted cel shading, sharp silhouette,
designed as a base for character creation customization, consistent with male
and female base bodies.
```

---

## 💇 Character Layers — Hair

Each hair entry will be a separate image that overlays the base body.

### Hair_LongStraight
```
Long straight fantasy hair on transparent background, isometric 3/4 view,
hand-painted cel shading, isolated as a wig (no face visible, no body), soft
highlights, suitable for layered character sprite, shoulder-length cut, fits
typical adventurer head proportions.
```

### Hair_ShortMessy
```
Short messy fantasy hair, slightly tousled, isometric 3/4 view, on transparent
background, hand-painted cel shading, isolated wig (no face, no body), suitable
for layered character sprite, masculine cut.
```

### Hair_Braid
```
Long braided fantasy hair tied behind, isometric 3/4 view, transparent
background, hand-painted cel shading, isolated wig, side-swept braid, suitable
for layered female character sprite.
```

### Hair_Bald (no generation needed)
*Empty layer — base body shows through.*

---

## 😊 Character Layers — Face

Just facial features (eyes, nose, mouth, eyebrows). Goes on top of the bald base.

### Face_Neutral
```
Isometric character facial features only — eyes, eyebrows, nose, mouth —
isolated on transparent background, hand-painted cel shading, suitable for
layered sprite. Neutral expression, soft warm tones, fantasy RPG style.
```

### Face_Determined
```
Same as Face_Neutral but with focused determined expression, slightly furrowed
brow, set jaw.
```

### Face_Smiling
```
Same as Face_Neutral but with warm friendly smile, bright eyes.
```

---

## 👕 Character Layers — Clothes

Clothes designed to overlay the base body at the same proportions.

### Clothes_StarterTunic
```
Simple brown leather tunic with cloth pants, isometric T-pose configuration,
transparent background, hand-painted cel shading, no character visible (clothes
only), consistent with adventurer base body proportions, leather belt, fabric
detail visible.
```

### Clothes_Robe
```
Fantasy mage robe in muted purple, full-length, isometric T-pose, transparent
background, hand-painted cel shading, no character visible, hooded shoulders,
flowing fabric, magical embroidery hints.
```

### Clothes_PeasantOutfit
```
Simple linen peasant outfit, off-white shirt with brown vest, knee-length pants,
isometric T-pose, transparent background, hand-painted cel shading, no character
visible, well-worn but clean.
```

---

## 🛡️ Character Layers — Armor (later)

To be designed after base bodies + clothes are validated.

---

## 🎨 Color Palettes (applied via shader, not generation)

These will be applied programmatically at runtime via material tinting. List for reference:

- **Skin**: porcelain (#F8E0CB), wheat (#E5B895), tan (#C49161), bronze (#8C5E3C), ebony (#5B3424)
- **Hair**: blonde (#E9D49C), brown (#7A4F2A), black (#1F1A18), red (#A8492C), white (#E8E2D0)
- **Eyes**: blue (#4A82B7), green (#5C8F4F), brown (#6F4626), hazel (#A07442), grey (#7E8A8C)

---

## 🪨 Resource Props (later — once characters working)

Planned categories:
- Trees (Oak, Pine, Dead, Cherry Blossom)
- Rocks (Stone, Copper Ore, Iron Ore, Coal, Crystal)
- Plants (Wheat, Berry Bush, Mushroom, Flowers)
- Special (Sword in the Stone, Chest, Campfire)

---

## 🌍 Biome-Specific Decorations (later)

Each biome will have its own decoration prompts:
- Plains
- Desert
- Frozen Mountain
- Frozen Cave
- Temple

---

## 🧱 Tile Bases (later)

Seamless isometric tiles for terrain. Each biome needs:
- Flat ground (1 base + 5-8 variants)
- Raised cliff top (1 base + 5-8 variants)
- Cliff side wall (N/S/E/W)
- Slope/stairs

---

## 🎯 Current Priority Workflow

1. ✅ Build pipeline tooling (Configure → Generate → Review)
2. ⏳ Generate Male_Adventurer_Base (4 samples, pick best)
3. ⏳ Generate Female_Adventurer_Base
4. ⏳ Generate Plain_Template_Base
5. ⏳ Generate 3 Hair styles (LongStraight, ShortMessy, Braid)
6. ⏳ Generate 3 Faces (Neutral, Determined, Smiling)
7. ⏳ Generate 3 Clothes (StarterTunic, Robe, PeasantOutfit)
8. ⏳ Build runtime layer compositor (next phase)
9. ⏳ Build in-game character creation UI (final phase)

---

## 📝 Notes for Future Refinement

- Once we see Pixal3D output for a base body, we may need to adjust prompts to enforce specific proportions (e.g. "head occupies upper 1/6 of frame", "feet at lower 1/8")
- Color palette should be locked in early — consider generating one full character with each palette to verify shader-tinting will work
- If layered approach proves tricky in practice, consider pre-baked full-character variants as a fallback

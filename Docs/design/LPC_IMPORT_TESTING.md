# LPC Import (v4) — Unity Playtest Checklist

Status: the full import (610 items / 1,174 base sheets) + the coupled C# rework are done and
statically reconciled, but **all C# is untested-by-design** — this project only compiles inside
Unity, which is not available in the import environment. Run the steps below in the Unity Editor to
verify, and use the Revert section if anything is broken.

See `LPC_IMPORT_PIPELINE.md` for the design/spec. This file is the verification + rollback guide.

---

## 0. What changed (so you know what to watch)

- **Data:** `Assets/Resources/Characters/Layers/layer_catalog.json` is now **v4** (610 items). Art is
  `Assets/Resources/Characters/Layers/lpc/<item>/{male,female}.png.bytes` (one stitched base sheet per
  body type, 1,174 total). New palettes ship at `Assets/Resources/Characters/Palettes/<material>.json.bytes`.
- **C# (8 files in `Assets/Scripts/UI/CharacterCreator/`):** catalog schema v4, runtime palette
  recolor + sheet-slice compositor, 4-direction + East-mirror animator, cosmetic-vs-equipment split,
  and reconciled NPC/player/equipment references. Full list in the import report / handoff.
- **Old art removed from Resources** (moved to `Docs/_lpc_backup/old_layers/`) so Unity won't load
  stale orphans. Player base sheets under `Assets/Resources/Characters/Player/` were left untouched.

---

## 1. First open in Unity — let it import

1. Open the project in Unity. Let it recompile scripts and (re)import the ~1,174 new `.png.bytes`
   TextAssets. **Expected:** Console shows **0 compile errors**. If you see compile errors, they are
   almost certainly in one of the 8 CharacterCreator files — check that file against the spec, or revert.
2. Watch the Console during the first character bake for `[Compositor]` / `[CharacterLayerCatalog]`
   warnings. **Expected:** no "Missing base sheet", no "Missing palette", no "Unknown item".
   A one-off "Missing layer sheet" for a single odd item is non-fatal (that item just won't draw).

## 2. Character Creator — cosmetics only, correct colours

Open the flow that calls `CharacterCreatorUI.Show(...)` (the welcome / new-character menu).

3. **Cosmetic-only wardrobe:** the Hair / Shirt / Pants / Shoes pickers must list **only cosmetic
   items** — no plate cuirass, no helmet, no weapon should ever appear as a "shirt"/"pants"/etc.
   (The creator now pulls `CosmeticSlot(...)`.) There are many fantasy cosmetics (wings/tails are
   under "body", animal heads under "head" cosmetic) — that's expected; they are cosmetic.
4. **Body type:** toggling Body male/female must re-bake and visibly swap the silhouette **and** the
   head (head is a separate item now: `heads_human_male` vs `heads_human_female`).
5. **Skin slider:** dragging Skin must recolour body + head together (they share the body palette /
   skin tone). The swatch next to the slider should track the chosen tone.
6. **Eyes:** only the cyclops eyes item exists in v1 and it has **no recolorable ramp**, so the Eyes
   slider may not visibly change colour — this is a **source-data limitation, not a bug**. Eyes should
   still render (in their authored colour).
7. **Hair / Shirt / Pants / Shoes:** for each, the `<` `>` arrows must change the item and re-bake;
   the colour slider must recolour it and update the swatch. **Expected:** colours look right (e.g.
   a "red" shirt is actually red), with no stray mis-mapped pixels — this validates the
   base-ramp→target-ramp recolor LUT.
8. **Animation cycler** (`<` Walk/Spellcast/Thrust/Slash/Shoot/Hurt... `>`): cycle every animation for
   a dressed character. **Expected:** each one plays its own frame band; clothing/hair stay aligned to
   the body across all of them (this checks per-item `rowOffset/rows/cols` slicing). `hurt` is a single
   row — it should still display (using the South band).
9. **Rotate (`<` `>`):** cycles facings. **Expected:** the character faces N / W / S / E and the
   sprite is coherent in each (no garbled rows). East is a mirrored West — confirm it isn't blank.
10. **Randomize:** rolls a new cosmetic outfit and keeps any equipped gear. **Done** saves and live-
    applies to the in-world player.

## 3. In-world player — 4 directions + actions

11. Enter play / load into the world. **Expected:** the player renders as the created character and
    animates while walking. Moving in 8 input directions should snap to the nearest of the 4 cardinals
    (diagonals reuse the nearest cardinal; East mirrors West via `SpriteRenderer.flipX`).
12. **Idle:** standing still shows the idle pose (the dedicated `idle` animation if present, else walk
    frame 0) — not a frozen mid-stride frame.
13. **Actions:** press the attack key (default **E**) and cast spells (keys **1–4**). **Expected:**
    `slash`/`thrust`/`shoot`/`spellcast` play once then return to walk/idle; taking damage plays `hurt`.
    Weapon-aware mapping: bow/ranged→shoot, polearm/spear→thrust, magic/staff/wand→spellcast, else slash.

## 4. Equipment layering (player + NPC)

14. **Equip gear:** via the inventory/equip flow (or by calling `CharacterEquipmentVisuals.Equip("lpc/cape_solid", "red")`
    on the player), equip a cape / weapon / armour. **Expected:** it layers onto the character on the
    correct z-order (sortOrder) and animates with the body. One item per equip slot (equipping a second
    cape replaces the first).
15. **Big weapons:** equip a large weapon and play `slash`/`thrust`. **Expected:** the weapon stays with
    the hand. NOTE: the oversize 128px swing layers were **not imported** (see Known limitations) — very
    large weapons may clip slightly past the 64px frame edge mid-swing. Flag if it looks bad.
16. **NPCs:** spawn humanoid mobs that have an `AppearanceTheme` (bandit / rogue / knight / mage). The
    `LayeredNpcHook` should dress them with the themed wardrobe and the same animator. **Expected:**
    a bandit wears a sleeveless shirt + bandana + tattered cape; a rogue wears a feathered cap +
    tattered cape; a knight/mage wears a longsleeve + solid cape. They animate while moving.

## 5. Credits

17. The attribution text is at `Resources/Characters/Layers/lpc_credits.txt` (630 entries). Confirm
    whatever credits screen reads `CharacterLayerCatalog.CreditsText` still shows content.

---

## Known limitations / expected deviations (not bugs)

- **Eyes recolor:** only `lpc/eyes_cyclops` exists in v1 and its art uses non-palette colours, so its
  colour slider is effectively a no-op. Renders fine in the authored colour.
- **128px oversize swing layers** (`slash_128`/`backslash_128`/`halfslash_128`): **not imported.** The
  v4 slice math is 64px-frame based; the source ships these as 2×-frame `custom_animation` layers the
  foundation importer skips. Great weapons use the standard 64px swing — watch for minor clipping (step 15).
- **43 source items were skipped** (0 base sheets): child/pregnant/wheelchair bodies, non-human child
  heads, `face_*` emote overlays, and a few weapons that only ship `custom_animation` art. They are not
  in the catalog by design (male/female-only decision + "draws must exist on disk").
- **Renamed id:** the NPC rogue hat was `lpc/hat_cap_feather` (doesn't exist) → now
  `lpc/hat_cap_leather_feather` (same feathered-cap intent).
- **`baseVariant` was auto-corrected at import:** 205 items had the wrong authored-variant recorded;
  a detection pass rewrote each to the palette variant its stored pixels actually match (verified
  87–100% recolor coverage on samples). If a specific item recolors oddly, its baseVariant may need
  manual correction in `layer_catalog.json`.

---

## Revert (if it's broken)

Everything is preserved under `Docs/_lpc_backup/`. To roll back to the pre-import (v3) state:

1. **C# —** copy all 8 backed-up files back over the live ones:
   `Docs/_lpc_backup/cs/*.cs` → `Assets/Scripts/UI/CharacterCreator/`
   (CharacterLayerCatalog, CharacterCompositor, LayeredAppearance, LayeredCharacterAnimator,
   CharacterCreatorUI, LayeredNpcHook, LayeredCharacterActions, CharacterEquipmentVisuals).
2. **Catalog —** copy `Docs/_lpc_backup/old_layers/layer_catalog.json` back to
   `Assets/Resources/Characters/Layers/layer_catalog.json`.
3. **Old art —** move the slot folders back into Resources:
   `Docs/_lpc_backup/old_layers/{accessory,body,hair,pants,shirt,shoes}` →
   `Assets/Resources/Characters/Layers/` (and their `.meta` files). The old per-anim v3 `lpc` tree is at
   `Docs/_lpc_backup/old_layers/lpc_v3_partial/` if needed.
4. **Remove the v4 additions:** delete `Assets/Resources/Characters/Layers/lpc/` (the new base sheets)
   and `Assets/Resources/Characters/Palettes/` if you want a clean v3 state.
5. Let Unity reimport. **Do not delete `Docs/_lpc_backup/`.**

Partial revert (keep the import, undo only the C#) is also valid: do step 1 only — but note the v3 C#
will NOT read the v4 catalog correctly, so you'd also need step 2/3 to get a working game.

---

# Phase 2 — Coupled gameplay systems (equipment / loot / NPC gear+skills / ability anim)

**Status: untested-by-design.** All C# below was written and statically reconciled but never compiled
(no Unity here). It builds on the LPC import above. Every new `ItemDefinition`/`MobDefinition`/
`FoundationAbilityDefinition`/`PlaceableDefinition` field is optional with a safe default, and
`FoundationSaveData` bumped v10 -> v11 (older saves load; the new `equipment` block defaults null).
Foundation never references the wardrobe; all visual/animation calls live in Assembly-CSharp and reach
Foundation only through static events + `FoundationBootstrap` getters.

## What changed (files)

Foundation (IsoCore.Foundation asmdef):
- `Core/FoundationTypes.cs` — new `EquipSlot` enum.
- `Items/ItemDefinition.cs` — `equipSlot`, `FoundationStatBonus[] statBonuses`, `lpcCatalogId`,
  `lpcVariant`, `IsEquippable`.
- `Progression/FoundationPlayerStats.cs` — split core stats into base+equip; `STR/DEX/...` now report
  base+equip; new `ApplyEquipmentModifiers(...)`. Save stores BASE stats; equip re-applies on load.
- `Inventory/EquipmentLoadout.cs` (new) — slot->ItemStack model; `Equip/Unequip`; sums StatBonuses into
  stats; raises `EquipmentChanged(slot, stack, def)`; capture/restore (`EquipmentSaveData`).
- `Inventory/LootSystem.cs` (new) — named weighted loot tables; subscribes to `MobSpawner.MobDefeated`
  (rolls `mob.drops` into inventory) and `StorageSystem.ContainerOpened` (fills a loot-table chest once).
- `Building/PlaceableDefinition.cs` — new `lootTableId`.
- `Mobs/MobDefinition.cs` — `abilityIds`, `abilityCooldownSeconds`, `EquipTier[] equipTiers` (+ `EquipTier` struct).
- `Mobs/Mob.cs` — `AbilityUsed(string)` event; `SetContent(...)`; per-mob `TryCastAbility()` in Update
  (cooldown-only, damage scales by `_tierDmgMul`).
- `Mobs/MobSpawner.cs` — calls `mob.SetContent(_content)` before Init.
- `Progression/FoundationAbilityDefinition.cs` — `animationId` + `ResolvedAnimationId` (Spell->spellcast,
  Projectile->shoot, melee Skill->slash).
- `Progression/FoundationAbilitySystem.cs` — `OnAbilityUsed(abilityId, animId)` raised on success.
- `Core/FoundationBootstrap.cs` — creates `Equipment` + `Loot`; capture/restore equipment;
  marks restored containers filled.
- `Core/FoundationContent.cs` — 9 starter equipment items; 2 loot chests + 2 loot tables; gear in
  bandit drops; abilityIds + equipTiers on bandit/knight/mage/rogue/tank.

Assembly-CSharp (UI/CharacterCreator/, references Foundation):
- `LayeredNpcHook.cs` — `ApplyTierGear(mob, anim)` + driver now applies tier gear once Level is final
  and subscribes to `Mob.AbilityUsed` -> `PlayOneShot`.
- `LayeredAbilityAnimHook.cs` (new) — `LayeredAbilityAnim.Resolve(abilityId)` and
  `LayeredAbilityPlayerHook` (player `OnAbilityUsed` -> animator).
- `LayeredEquipmentPlayerHook.cs` (new) — player `EquipmentChanged` -> `CharacterEquipmentVisuals`.

## Starter equipment registered (id -> slot, bonuses, lpc id)
- `iron_longsword` Weapon STR+3 DEX+1 `lpc/weapon_sword_longsword`(iron)
- `oak_shortbow` Weapon DEX+4 `lpc/weapon_ranged_bow_recurve`
- `apprentice_staff` Weapon INT+4 `lpc/weapon_magic_gnarled`
- `plate_cuirass` Chest DEF+6 VIT+2 `lpc/torso_armour_plate`(iron)
- `leather_jerkin` Chest DEF+3 DEX+1 `lpc/torso_armour_leather`
- `iron_helmet` Head DEF+3 `lpc/hat_helmet_barbarian`(iron)
- `plate_greaves` Legs DEF+3 `lpc/legs_armour`(iron)
- `iron_kite_shield` Offhand DEF+4 VIT+1 `lpc/shield_kite`(iron)
- `travelers_cape` Back LUCK+2 `lpc/cape_solid`

## Loot registered
- Table `chest_common` (placeable `loot_chest`): apple, leather, + chance of leather_jerkin / oak_shortbow / travelers_cape.
- Table `chest_armoury` (placeable `loot_chest_armoury`): iron_ore, + chance of iron_helmet / plate_greaves / iron_kite_shield / iron_longsword / plate_cuirass.
- Mob drops: `bandit` gains a 10% leather_jerkin / 6% iron_longsword chance.

## Verify in Unity

Equipment (Phase 1):
1. `bootstrap.Equipment.Equip("plate_cuirass")` — Character/HUD DEF jumps +6, VIT +2, MaxHealth rises
   (VIT-derived); the plate layers visibly onto the player (LayeredEquipmentPlayerHook).
2. Unequip — bonuses revert, MaxHealth clamps down, the plate layer disappears. A dead (0 HP) player
   stays dead (RecalculateVitals never refills).
3. Equipping a second item into the same slot replaces the first (loadout returns the previous stack —
   caller is responsible for returning it to inventory; no inventory auto-return is wired yet, see TODO).
4. Save, reload — equipped gear restored (base stats in save, equip re-applied), layers re-baked.

Loot (Phase 2):
5. Place/spawn a `loot_chest` and open it — fills once from `chest_common` (re-opening does NOT re-roll).
   `loot_chest_armoury` fills from `chest_armoury`. A non-empty (player-stored or save-restored) chest is
   never overwritten.
6. Defeat a bandit (player kill = MarkDefeated) — leather plus occasional gear lands in the inventory.
   NPC-vs-NPC kills (TakeMobDamage path) do NOT grant player loot — by design.

NPC gear + skills (Phase 3):
7. Spawn humanoid NPCs far from the hearth so tierLevel >= 2/3 (~24/48 world-units out). Bandits gain a
   leather torso at L2 and a helm+sword at L3; knights gain plate then helm+shield; mages a staff; tanks
   plate+shield. Tier gear layers over the themed base.
8. Watch an aggressive caster (mage casts mana_bolt/ember_spark; knight/bandit steady_strike). It fires on
   its `abilityCooldownSeconds`, deals tier-scaled damage to its target, and plays the matching one-shot.

Ability animation (Phase 4):
9. Player: cast abilities through `FoundationAbilitySystem.TryUseAbility` (ability wheel). The player's
   LayeredCharacterAnimator plays spellcast (spells) / shoot (projectile-delivery skills) / slash (melee).
   This is independent of the legacy `SpellCaster.OnSpellCast` path (LayeredCharacterActions) — confirm
   they don't double-fire awkwardly if both are active.

## Known limitations / TODO (not bugs)
- **No equip UI / inventory<->loadout transfer wired.** `EquipmentLoadout.Equip/Unequip` exist and are
  save/restored, but no inventory slot drives them yet and the returned previous stack is not
  auto-returned to the inventory by any caller. Hook a UI/`PlayerInteraction` path to `bootstrap.Equipment`.
- **Item durability on equipped gear** is carried in the loadout stack but equipment items are registered
  with `maxDurability = 0` (no wear), so durability is inert for now.
- Some intended lpc ids from the brief don't exist in the v1 catalog; substituted real ids:
  `weapon_bow_recurve`->`weapon_ranged_bow_recurve`, `weapon_magic_staff`->`weapon_magic_gnarled`,
  `legs_armour_plate`->`legs_armour`.
- NPC ability casts apply damage Foundation-side immediately (no projectile travel/VFX); only the
  animation is bridged. Wire `FoundationAbilityDispatcher`-style VFX later if desired.

## Revert (Phase 2 only)
All Phase-2 fields are additive and optional; to back out, remove the new files
(`EquipmentLoadout.cs`, `LootSystem.cs`, `LayeredAbilityAnimHook.cs`, `LayeredEquipmentPlayerHook.cs`),
revert the edits in the files listed above, and drop `FoundationSaveData.CurrentVersion` back to 10
(the `equipment` field is ignored by v10 loads regardless).

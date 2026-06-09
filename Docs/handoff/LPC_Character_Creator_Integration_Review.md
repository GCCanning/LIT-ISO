# LPC Character Creator Integration Review

Purpose: determine how the Universal LPC Spritesheet Character Generator could
fit LIT-ISO's character creation flow, visual identity, save/runtime systems, and
licensing rules.

No runtime code was changed.

Mockup image:

- `Docs/handoff/lpc_character_creator_mockup.png`

Primary references:

- Generator: https://liberatedpixelcup.github.io/Universal-LPC-Spritesheet-Character-Generator/
- Repository: https://github.com/LiberatedPixelCup/Universal-LPC-Spritesheet-Character-Generator
- README/licensing: https://raw.githubusercontent.com/liberatedpixelcup/Universal-LPC-Spritesheet-Character-Generator/master/README.md
- Credits CSV: https://raw.githubusercontent.com/liberatedpixelcup/Universal-LPC-Spritesheet-Character-Generator/master/CREDITS.csv
- Provided visual reference: `C:/Users/garyc/Downloads/Gemini_Generated_Image_hzeizohzeizohzei.png`

## Bottom Line

The LPC generator is technically useful, but direct shipping use is risky for
LIT-ISO.

Recommended approach:

1. Use LPC as a prototype-only reference for the character creator shape,
   metadata, layering, animation taxonomy, and attribution workflow.
2. Build LIT-ISO's shipping character creator with original character sprites,
   original summoning-circle art, original UI, original music, and original VFX.
3. If LPC pixels are used in any public build, first make a deliberate legal/art
   policy decision and include per-asset credits in-game.

This preserves the project's clean-room rule: all shipped art/audio should be
authored fresh for LIT-ISO.

## What LPC Gives Us

Useful pieces:

- Large modular character layer catalog.
- Body starts such as male, female, teen, child, muscular, pregnant, skeleton,
  and zombie.
- Layer categories such as body, head, hair, torso, legs, feet, arms, headwear,
  tools, weapons, shields, capes, backpacks, beards, eyes, facial features, and
  accessories.
- Pre-named sprite paths and JSON definitions.
- Exported PNG spritesheets.
- Exported JSON state.
- Exported credits as TXT/CSV.
- A useful model for provenance and per-asset attribution.

Animation categories discovered from the generator/repo include:

- spellcast
- thrust
- walk
- slash
- shoot
- hurt
- watering
- idle
- jump
- run
- sit
- emote
- climb
- combat
- one-handed slash/backslash/halfslash variants

Important direction note:

- LPC is historically strongest as 4-direction/cardinal sprite work.
- Some related LPC expansions add more poses and animation types, but we should
  not assume every chosen layer has production-ready 8-direction support.
- LIT-ISO's current `PlayerAnimator` expects an 8-row directional sheet with 4
  frames per row, so the import contract must either:
  - accept true 8D sheets, or
  - adapt 4D sheets by mapping diagonals to nearest cardinal directions, or
  - use 4D only for prototype and replace with original 8D art before shipping.

## Licensing Findings

The generator code is GPL-3.0. The art is not one single simple license. The
repo README says artwork in `spritesheets/` uses a mix of:

- CC0
- CC-BY-SA
- CC-BY
- OGA-BY
- GPL

Practical implications:

- Generated sprites require attribution for non-CC0 assets.
- The repo recommends exporting JSON so the exact selected state can be recovered
  later.
- The repo's `CREDITS.csv` lists authors, licenses, source URLs, and notes for
  individual sprite files.
- CC-BY-SA and GPL assets may create derivative/share-alike obligations.
- The README warns about DRM/encryption ambiguity for some Creative Commons
  assets and suggests CC0/OGA-BY-only assets for safer storefront publishing.

Recommendation:

- Do not ship direct LPC pixels by default.
- If the team intentionally chooses to ship LPC assets, store:
  - exported PNG,
  - exported JSON,
  - exported credits CSV/TXT,
  - source repo URL and commit/date,
  - exact license policy for each selected layer.
- Add an in-game credits screen entry and ship a discoverable credits file.

This is an implementation review, not legal advice.

## Clean-Room Fit

LIT-ISO's current project rule says shipped pixels/audio must be authored fresh.
Using LPC output directly would conflict with that rule even if the licenses are
compatible.

Better fit:

- Use LPC as a reference for system design.
- Use the same kind of layer categories and manifests.
- Author our own body bases, hairstyles, outfits, palette swaps, animations, and
  credits/provenance records.
- Keep any LPC experiments quarantined under non-shipping prototype folders.

## Character Creation UX

The provided summoning circle image is a strong first-screen anchor.

Recommended screen:

- Fullscreen cosmic void background.
- Summoning circle in lower-middle of the screen.
- Plain unclassed character floating above the circle.
- A soft vertical beam behind the character so the sprite remains readable.
- Slow star particles in parallax.
- Small rune fragments rising around the character.
- Sparse void music:
  - low drone,
  - glassy bell tones,
  - distant choir pad,
  - reversed shimmer on category changes,
  - warm chord bloom on Begin.

UI layout:

- Top-left title:
  - `SYSTEM INITIALIZATION`
  - `Vessel Configuration`
  - `Origin Pending`
- Left vertical tabs:
  - Body
  - Face
  - Hair
  - Clothes
  - Colors
  - Name
  - Summary
- Right active option panel:
  - body type,
  - age class,
  - frame,
  - skin tone,
  - name/designation.
- Bottom center commands:
  - Randomize
  - Back
  - Begin
  - Save Preset later.

Default starts:

- Plain male adult.
- Plain female adult.
- Teen.
- Muscular.

These are starting body frames, not classes. The character remains unclassed
until the seven-day Trial ends.

Suggested System copy:

- `Vessel Type`
- `Age Class`
- `Body Frame`
- `Hair Pattern`
- `Garment Layer`
- `Palette`
- `Designation`
- `No class assigned`
- `Appearance matrix stabilized`
- `Origin Pending`

## Unity Integration

### Current Project Seams

Menu/UI lane:

- `Assets/Scripts/UI/WelcomeScreenManager.cs`
- Already has `Screen.CallingSelect`.
- New Game creates world metadata, then opens Calling selection, then calls
  `FoundationBootstrap.ConfigureLaunch(...)`.

Foundation/runtime lane:

- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs`
- Owns canonical launch state and runtime player creation.
- Already accepts world name, seed, difficulty, and calling id.
- Already persists calling id.

Player sprite lane:

- `Assets/Scripts/IsoCoreFoundation/Player/IsoFoundationPlayer.cs`
- Creates a placeholder sprite during player init.
- `Assets/Scripts/IsoCoreFoundation/Player/PlayerAnimator.cs`
- Currently hardcodes `Resources/Characters/Player/ReferenceKnight_Idle_512x1024`.
- Current runtime expects `rowCount = 8` and `framesPerRow = 4`.

Save/load lane:

- `Assets/Scripts/IsoCoreFoundation/Core/FoundationSaveData.cs`
- Current save version is already versioned.
- Calling is persisted; appearance is not yet persisted.

### Required Foundation Contract

Add a tiny character appearance contract before building the UI.

Recommended state:

```csharp
[Serializable]
public struct FoundationCharacterAppearanceSaveData
{
    public string appearanceId;
    public string bodyId;
    public string bodyType;
    public string skinPaletteId;
    public string hairId;
    public string hairPaletteId;
    public string outfitId;
    public string outfitPaletteId;
    public int directionCount;
    public int framesPerRow;
    public string spriteResource;
    public string provenanceId;
}
```

Recommended launch API:

```csharp
FoundationBootstrap.ConfigureLaunch(
    string worldName,
    string seed,
    int difficulty = 1,
    string callingId = null,
    FoundationCharacterAppearanceSaveData? appearance = null);
```

If nullable structs are inconvenient for Unity serialization, use a class or a
plain `appearanceId` first and resolve the rest from a catalog.

Recommended runtime API:

```csharp
PlayerAnimator.SetAppearance(FoundationCharacterAppearanceDefinition appearance);
```

Fallback rule:

- Invalid or missing appearance resolves to current default reference knight or
  another approved original placeholder.

### Character Asset Catalog

Do not let menu UI choose raw resource paths.

Use a catalog:

- `appearanceId`
- display name
- body type
- supported direction count
- supported animations
- resource path
- preview sprite/portrait
- source/provenance id
- license/provenance policy

For prototype LPC imports, store assets outside shipping Resources until approved.
For original production assets, promote into a stable path such as:

- `Assets/Resources/Characters/Player/Appearances/<appearanceId>/...`

Actual art promotion belongs to the menu/art/integration lane.

## Implementation Slices

### Slice 1: Foundation Appearance Contract

Owner: Codex/Foundation.

Tasks:

- Add appearance save data or `characterAppearanceId`.
- Add launch option.
- Save/load the field.
- Add `PlayerAnimator.SetAppearance(...)`.
- Keep current default if no appearance is provided.
- Add validator coverage:
  - launch applies appearance before `Ready`,
  - save captures appearance,
  - load restores appearance,
  - invalid id falls back safely.

### Slice 2: Character Creator Screen

Owner: Claude/UI.

Tasks:

- Add `Screen.CharacterCreate` to `WelcomeScreenManager`.
- Route New Game:
  - Create World
  - Character Create
  - Calling Select, or Calling Select then Character Create
  - Launch Foundation.
- Build the summoning-circle UI using the provided visual direction.
- Pass only an appearance id/data contract to Foundation.
- Do not mutate Foundation runtime directly from UI.

Recommended order:

- Character Create before Calling Select feels better narratively: configure
  vessel first, then select starting gifts/calling.

### Slice 3: Prototype Importer

Owner: Codex/Tools or separate branch.

Tasks:

- Build an offline importer that consumes:
  - exported LPC PNG,
  - exported LPC JSON,
  - exported credits CSV/TXT.
- Slice sprites into Unity multi-sprite assets.
- Generate a local manifest.
- Flag assets as prototype-only.
- Never promote directly into `Resources` without explicit approval.

### Slice 4: Original LIT-ISO Character Generator

Owner: art/integration with Foundation contracts.

Tasks:

- Author original base bodies:
  - male,
  - female,
  - teen,
  - muscular.
- Author original hair/outfit/accessory layers.
- Support palette swaps.
- Export 8D sheets matching current runtime.
- Use LPC-style metadata discipline, but no LPC pixels.

### Slice 5: Save/Load And Continue Fix

Owner: cross-lane.

Tasks:

- Menu Continue/Load should prefer `FoundationBootstrap.ConfigureLoad(savePath)`
  when a Foundation save exists.
- Foundation metadata should expose appearance id/name for save list display.
- Old `*.world.json` remains menu metadata, not runtime truth.

## 4D/8D Strategy

Short-term prototype:

- Accept 4D sheets.
- Map diagonal movement to nearest cardinal row.
- Or duplicate nearest cardinal rows to fill the existing 8-row runtime sheet.

Production:

- Use true 8D original character sheets.
- Keep row order compatible with current `PlayerAnimator`:
  - row 0 = South,
  - row 1 = South-East,
  - row 2 = East,
  - row 3 = North-East,
  - row 4 = North,
  - row 5 = North-West,
  - row 6 = West,
  - row 7 = South-West.
- Keep `framesPerRow = 4` for initial idle/walk compatibility unless the animator
  is upgraded to animation clips.

## Risks

- Direct LPC pixels conflict with clean-room art policy.
- Mixed licenses can complicate commercial/storefront release.
- UI-only selection will not work until Foundation has an appearance contract.
- Current runtime hardcodes a single sprite resource.
- Current Continue/Load path can bypass full Foundation save loading.
- Art promotion crosses ownership lanes.
- 4D character art will feel less natural in the current 8-direction isometric
  runtime.

## Recommendation

Build this as a LIT-ISO original character creator, inspired by LPC's structure:

- same useful idea of modular body/hair/outfit/palette layers,
- same discipline around JSON/provenance,
- same export/import thinking,
- but original art, original UI, original music, original VFX, and original
  summoning ritual presentation.

Use LPC only as a prototype/reference unless the owner explicitly changes the
clean-room policy and accepts the licensing requirements.

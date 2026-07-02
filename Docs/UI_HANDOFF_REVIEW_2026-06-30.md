# LIT-RPG UI Handoff Review

Date: 2026-06-30  
Source: `Tools/LitRpgUIStudio/handoffs/lit-rpg-ui-handoff-2026-06-30.json`

## Verdict

The handoff is suitable as the visual layout contract, but it is not ready to
apply directly to the runtime without an asset and node-creation pass.

The handoff defines:

- 1920x1080 design resolution;
- 10 screens;
- 45 positioned components;
- 27 unique `Resources` image IDs;
- normalized rectangles plus Unity-style anchors and pivots;
- intended runtime surface and node names;
- per-screen PixelLab prompt briefs.

The layouts should remain the source of truth while the UI shell art is
generated and reviewed.

## Existing Asset Status

Two of the 27 exact handoff IDs currently exist:

- `UI/Menu/button`
- `UI/Menu/panel`

Those files belong to the older dark/brass skin. Their presence does not count
as approval for the new warm and cozy direction. They are protected from
replacement by the promotion script unless `--replace` is explicitly supplied.

The other 25 exact handoff IDs are absent.

## Runtime Review

### Existing surfaces

These handoff targets map to current procedural uGUI surfaces:

- `WelcomeScreenManager`
- `LoadingScreen`
- `GameUIController`
- `CharacterPanelView`
- `InventoryView`
- `AbilityWheelView`
- `WorldMapView`

### Nodes that must be created or adapted

These JSON targets describe desired nodes, not guaranteed existing GameObjects:

- `CharacterCreator/Name`
- `CharacterCreator/Categories`
- `CharacterCreator/Preview`
- `CharacterCreator/Options`
- `CharacterCreator/Confirm`
- `WelcomeScreenManager/WorldPreview`
- `GameUIController/TopRight/Minimap`
- `GameUIController/TopRight/QuestTracker`
- the split `CharacterPanelView/Body/Settings/*` surfaces
- the split crafting storage surface

Implementation must create these nodes deliberately and retain current behavior
when an optional asset or node is missing.

## Decisions Required Before Runtime Implementation

1. **Hotbar count:** the handoff shows 10 visible slots and four rows. The
   current runtime contract previously used nine visible slots. The new
   implementation should adopt 10 only after inventory mapping, save data, and
   number-key behavior are updated together.
2. **Character creator:** the handoff combines appearance editing and class
   selection. The existing calling selection state is not yet a complete LPC
   appearance editor.
3. **World preview:** define whether this is a generated seed preview, a static
   illustration, or a biome summary map.
4. **HUD minimap:** decide whether it is the current `WorldMapView` rendered in
   compact form or a separate minimap renderer sharing map data.
5. **Ability wheel geometry:** `UI/Skills/wheel_segment` must be rotationally
   symmetric. Reject attractive candidates that cannot form a clean circle.
6. **Wordmark:** `UI/Menu/logo` must be constructed from an approved pixel font
   or runtime text. Do not rely on an image model to spell `LIT-RPG`.
7. **Background animation:** the approved campfire concept is not represented by
   a handoff resource ID. It should be promoted separately to
   `UI/Menu/background` and its final frame sequence after animation approval.

## Asset Coverage

### Handoff assets

- Menu: button, logo, panel.
- Loading: tip frame, flame.
- Character creator: category button, option slot, class slot.
- World creation: preview frame.
- HUD: vitals frame, day band, minimap frame, quest frame, ability slot,
  hotbar slot.
- System book: tab, item slot, equipment slot, detail frame.
- Skills: skill slot, wheel segment.
- Quest and map: quest log frame, map frame.
- Crafting: recipe slot.
- Settings: category, keycap, panel.

### Derived deterministic states

These must preserve the approved source geometry and therefore are derived
locally rather than independently regenerated:

- `UI/Menu/button_hover`
- `UI/Menu/button_pressed`
- `UI/Menu/button_disabled`
- `UI/System/tab_active`
- `UI/HUD/hotbar_slot_selected`

### Procedural meter assets

These are simple palette-controlled strips and should not spend PixelLab
credits:

- `UI/HUD/bar_track`
- `UI/HUD/health_fill`
- `UI/HUD/mana_fill`
- `UI/HUD/stamina_fill`
- `UI/HUD/xp_fill`

### Additional shell controls

The generator adds candidates for controls required by the complete screens but
not named in the exported JSON:

- close button;
- tooltip;
- context menu;
- dropdown;
- scroll track and handle;
- on/off toggles.

Class icons, system-tab icons, map pins, weather icons, equipment silhouettes,
item icons, and ability icons should be a separate iconography batch after the
shell style is approved.

## PixelLab Generation Contract

Script:

`Tools/PixelLab/generate_ui_handoff_assets.bat`

Default behavior is an offline dry run. It writes:

- `Tools/PixelLab/review/UIHandoff/2026-06-30/asset_manifest.json`
- `Tools/PixelLab/review/UIHandoff/2026-06-30/prompts.json`
- `Tools/PixelLab/review/UIHandoff/2026-06-30/approvals.txt`
- candidate PNGs under the sibling `candidates` directory after live generation;
- `jobs.json` for resumable PixelLab jobs;
- a contact sheet after generation.

Candidate naming is deterministic:

`UI__HUD__vitals_frame__c01.png`

The corresponding promotion target remains:

`Assets/Resources/UI/HUD/vitals_frame.png`

### Recommended staged generation

Generate one screen at a time instead of spending the full batch immediately:

```bat
generate_ui_handoff_assets.bat --generate --screen main-menu --candidates 3
generate_ui_handoff_assets.bat --generate --screen hud --candidates 2
generate_ui_handoff_assets.bat --generate --screen backpack,skills --candidates 2
```

Use `--only UI/HUD/vitals_frame` to regenerate one resource.

## Approval And Promotion

1. Review the generated contact sheet and individual candidate PNGs.
2. Uncomment one candidate per approved resource in `approvals.txt`.
3. Dry-run promotion:

```bat
promote_ui_handoff_assets.bat
```

4. Apply approved new resources:

```bat
promote_ui_handoff_assets.bat --apply
```

5. Replacing the existing menu skin requires an explicit command:

```bat
promote_ui_handoff_assets.bat --apply --replace
```

6. Open Unity once so it imports PNGs and creates or updates `.meta` files.
7. Commit every promoted PNG together with its `.meta`.

The generator never writes candidates directly into `Assets/Resources`.

## Candidate Acceptance Criteria

- exact requested dimensions;
- transparent pixels outside the component;
- crisp pixel edges without antialiasing haze;
- warm wood, forest canvas, cream, ember, and leaf palette;
- no text, letters, numbers, logos, or watermarks;
- plain stretchable centers for 9-sliced components;
- matching scale and border weight across the entire batch;
- readable at 1080p without oversized ornament;
- no black-and-gold MMO styling;
- no copied external game assets;
- rotational symmetry for ability-wheel parts;
- no baked icons where runtime content must remain replaceable.

## Implementation Gate

Do not begin the runtime overhaul until at least these shell families have an
approved keeper:

- menu button and panel;
- HUD vitals, hotbar slot, ability slot, quest frame, and minimap frame;
- system tab, item slot, equipment slot, and detail frame;
- character creator category, option, and class slots.

Once those are approved, the runtime can be implemented screen by screen while
remaining assets continue through review.

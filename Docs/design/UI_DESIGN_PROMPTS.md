# LIT-ISO — UI Design Prompts (all scenes / tabs)

Each section below is a ready-to-paste prompt for generating a mockup (in Claude, Figma AI, or any
image/UI tool). **Prepend the STYLE BLOCK to every prompt** so the look stays consistent. Replace
bracketed placeholders if your data differs.

---

## STYLE BLOCK (prepend to every prompt)

> Design a UI screen for **LIT-ISO**, a 2D **isometric pixel-art** survival/crafting RPG with
> litRPG/Royal-Road progression. Art direction: cozy dark fantasy. Dark slate panels (#15171C / #1D2027)
> with thin 1–2px borders, warm **gold** accents (#E8C468), parchment/wood/stone framing that feels
> hand-crafted (not flat/modern), soft torch-glow ambiance, crisp readable bitmap/serif-ish pixel font.
> Reference the warm hand-painted tavern interior art for texture. Resolution 1920×1080, anchored &
> scalable. Show **rest / hover / pressed / disabled** states for interactive elements, and keep the
> screen readable over a busy game scene. Pixel-perfect, no anti-aliased gradients where a pixel ramp
> would do.

---

# A. Front-end / menu scenes

## 1. Main Menu (Title)
> Design the title screen. Center/left a logo wordmark "LIT-ISO" over an animated campfire scene.
> Vertical buttons: **Continue** (only if a save exists), **New Game**, **Creation Instance**,
> **Load Game**, **Options**, **Quit**. Bottom-right version tag. Ambient embers/fireflies. Buttons
> hug their label width; whole menu sits on the left so the scene stays visible.

## 2. Create World
> Design the world-creation screen. Fields: **World Name** (text), **Seed** (text, "blank = random"),
> **Difficulty** slider (Easy / Normal / Hard with a live label). Primary **Play** + secondary **Back**.
> Parchment card on the dark scene. Validate-empty states.

## 3. Character Creator (appearance)
> Design the appearance creator (layered paper-doll wardrobe). Left: large rotating/animated character
> preview with direction + walk/idle toggle. Right: cosmetic-only slot pickers — **Body skin, Head,
> Eyes, Hair (style + colour), Facial hair, Shirt, Pants, Shoes** — each a swatch row with colour
> variants. Randomize, Reset, Confirm. Note: full RPG gear is loot, not here. Keep slots scrollable.

## 4. Class / Calling Selection (Day-7 "trial" offer)
> Design the class-offer screen shown at the end of the 7-day trial. Title "Choose Your Calling",
> subtitle "sets your starting gifts; you can still learn everything." Scrollable cards for the four
> starting classes — **Mage, Tank, Rogue, Knight** — each card: name, starting title, stat-bonus line
> (STR/DEX/INT/VIT/DEF/LUCK), short description. Selected card highlighted gold. **Begin** + **Back**.

## 5. Load Game
> Design the saved-worlds list. Scrollable rows: world name · seed · difficulty · "saved/new" tag, with
> **Play** and a two-step **Delete** ("Delete" → "Sure?") per row. Empty state "No saved worlds yet". Back.

## 6. Options / Settings
> Design the settings panel. Sliders for **Master / SFX / Music** volume with live values; sections for
> Display and Controls (key rebinds list). Back saves. Reuse on the title screen and as a pause sub-panel.

## 7. Loading / Transition screen
> Design the loading screen: full-bleed art, a gold progress bar or spinner, a rotating gameplay **tip**
> line, and the destination name ("Entering <World>…"). Used for scene loads and instance entry/exit.

---

# B. In-game HUD (overlay, always-on)

## 8. Vitals cluster
> Design the top-left vitals stack: **HP** (red), **MP/Mana** (blue), **Stamina** (green), and a thin
> **XP** bar with current/next, plus **Level**. Framed bars with numeric overlays. Compact, glanceable.

## 9. Clock / day band
> Design the day/time strip: current **Lv**, in-world **time + Day/Night** icon, and during the opening,
> a "TRIAL · Day X of 7 · Forecast <rank>" banner. Small, top area.

## 10. Hotbar
> Design the bottom item hotbar: 9 numbered slots with item icon, stack count, durability sliver,
> selected-slot highlight, and cooldown sweep. Empty-slot and disabled states.

## 11. Ability bar
> Design the bottom-left ability bar: keybound skill buttons (e.g. **Q Flash Step, E Mana Bolt,
> R Ember Spark, F Mending Light**) with icon, hotkey, radial cooldown, and mana/stamina-cost tint when
> unaffordable. Element-coloured frames (ember/tide/root/stone/gale/glimmer/hearth).

## 12. Quest tracker
> Design the top-right objective tracker: quest title, 1–3 checklist objectives with progress (e.g.
> "Gather wood 0/5"), and reward line ("Reward: XP ×40"). Collapsible. Parchment style.

## 13. Minimap
> Design the corner minimap: rounded framed map showing terrain shape, player arrow, settlements,
> objectives, and a region label. Toggle to expand to the full map.

## 14. Interaction prompt
> Design the contextual interaction pill that appears over a targeted object: a small framed label like
> "Enter — Tavern", "Open Chest", "Break Rock", with the bound key. Appears near the cursor/target.

## 15. Floating text & status effects
> Design floating combat/feedback text (damage, "-12", "+40 XP", "Fire Trap") and a small status-effect
> tray (buff/debuff icons with timers) under the vitals. Colour-coded, short-lived, non-intrusive.

---

# C. Menu tabs / panels (the "character" book)

> These six often live as **tabs in one windowed panel** — design a shared frame + tab rail
> (Inventory · Equipment · Character · Skills · Crafting · Map · Quests), then each tab body:

## 16. Inventory tab
> Design the inventory grid: slots with item icon, stack count, durability, rarity frame; sort/filter
> bar; drag-to-move and right-click context (use/equip/split/drop); weight or slot count; hover tooltip.

## 17. Equipment / paper-doll tab
> Design the equipment screen: central character paper-doll with slots **Head, Chest, Legs, Feet, Hands,
> Shoulders, Back, Waist, Weapon, Offhand, Accessory**; click a slot to equip from inventory; right
> panel shows the **derived stat totals** the gear grants and a compare-tooltip on hover.

## 18. Character / stats tab
> Design the stats sheet: core stats **STR, DEX, INT, VIT, DEF, LUCK** with base+gear breakdown;
> derived values (Max HP/MP/Stamina, move speed, cooldown, jump); **Adventurer Rank F→E→D→C→B→A→S**
> badge with progress; class + starting title.

## 19. Skills / abilities tab
> Design the abilities/skill screen: learnable skills and spells by **element** (ember/tide/root/stone/
> gale/glimmer/hearth), each with icon, description, resource cost, cooldown, and "assign to ability bar"
> action; show locked vs learned; a separate **Professions** track (gather/craft trades) as its own sub-tab.

## 20. Crafting tab
> Design the crafting panel: station context (Workbench / Furnace / Tannery), searchable recipe list,
> selected recipe with ingredient costs (have/need, dimmed if short), output preview, and a Craft button
> with quantity. "Can't craft" reasons surfaced.

## 21. Map tab (full map)
> Design the full-screen map: pannable/zoomable world with biomes, the spawn hearth, discovered
> settlements (campsite→hamlet→village→town→city icons), points of interest, player marker, and a
> legend. Place-able custom pins optional.

## 22. Quest log / Guild board tab
> Design the quest log: tabs for Active / Available / Completed; quest cards with title, giver, region,
> objectives, rewards, and a **Track** toggle (feeds HUD #12). A "Guild Board" variant lists rank-gated
> contracts with a guild-rank header.

---

# D. World / building UI

## 23. Pause menu
> Design the pause overlay: dimmed scene, centered panel with Resume, Options, Save, Quit to Menu, and a
> small "Day X · <world>" header.

## 24. Build / placement menu
> Design the build mode UI: a category rail (Camp, Crafting, Storage, Structures, Decor), a scrollable
> placeable palette with cost, a ghost-placement preview readout (valid/blocked), and rotate/confirm/
> cancel controls. Used for placing campfire/workbench/chests/plots etc.

## 25. Storage / chest window
> Design the container window: side-by-side **container grid** and **player inventory**, with
> take/deposit, "take all / deposit all", and stack-merge. Title shows the container type.

## 26. Shop / vendor (trade)
> Design the trade window: vendor stock list with prices + your gold, your sellable inventory, buy/sell
> with quantity, running total, and a confirm. Rarity frames; affordability dimming.

---

# E. Dialogue & services

## 27. Dialogue
> Design the NPC dialogue UI: a framed speech panel with **speaker name** (e.g. "Lady Prienne"),
> body text, optional portrait, and a list of player **response choices**; supports multi-page text and
> a continue indicator. Should read over the world scene (see the existing in-world speech bubble).

## 28. Services panel (tavern / guild / inn)
> Design a building-services menu opened on entering: tabs/buttons for the building's services — e.g.
> Tavern (Rest, Tavern Jobs), Guild Hall (Guild Board, Rank), Inn (Sleep/Save), Shop (Trade). Each leads
> to the relevant panel above. Themed to the interior art.

## 29. Level-up / rank-up flourish
> Design the level-up and **rank-up** (F→S) celebration: a brief centered banner/burst with the new
> level/rank, stat gains, and any unlocked skills/slots. Transient, satisfying, skippable.

## 30. Notifications / toasts
> Design the toast stack (bottom or top-right): item pickups ("+3 Wood"), unlocks, quest updates, autosave
> indicator. Stacked, auto-dismiss, icon + short text.

---

## Tips for using these
- Generate **2–3 variations** of the shared frame (#15–22 tab window) first; lock the frame, then fill tabs.
- Ask for the **component spec** (colours, paddings, font sizes, 9-slice borders) once a look is chosen so
  it ports cleanly to Unity (your UI is built procedurally in C#, e.g. WelcomeScreenManager + the HUD).
- Keep one **design-tokens** list (palette, type scale, border art) shared across every screen.

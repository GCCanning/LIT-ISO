# HUD-overlaps-MenuScene fix — 2026-06-14

## Root cause
`GameHudInitializer` (Assets/Scripts/UI/InGame/GameHudInitializer.cs) spawns the
gameplay HUD as several top-level `DontDestroyOnLoad` GameObjects
(`[uGUI HUD]`, `[uGUI Panels]`, `[uGUI QuestTracker]`, `[uGUI Notifications]`,
`[uGUI DayClock]`, `AbilityWheel`, `TrialBanner`). Because they're
`DontDestroyOnLoad`, they survive the `SceneManager.LoadScene("MenuScene")`
call triggered by "Save & Quit to Menu" in
`Assets/Scripts/IsoCoreFoundation/UI/PauseMenu.cs` (`QuitToMenu()`), and remain
visible drawn on top of the menu canvas.

## Fix
Added `GameHudInitializer.ShutdownHud()` — destroys all the persistent HUD root
GameObjects, disposes their adapters/bridges, and clears the static refs
(`_hud`, `_panels`, `_questView`, `_notifyView`, `_dayView`, `_abilityWheel`,
`_trialBanner`, `_boundBootstrap`, `_boundInteraction`).

`PauseMenu.QuitToMenu()` now calls `GameHudInitializer.ShutdownHud()` before
`SceneManager.LoadScene("MenuScene")`.

## Why gameplay re-entry still works
`OnFoundationReadyInner` checks `_hud == null` / `_boundBootstrap == bootstrap`
before reusing existing instances. After `ShutdownHud()` these statics are
null, so the next `FoundationBootstrap.Ready` (loading back into a gameplay
scene, e.g. world or dungeon) rebuilds the full HUD set from scratch exactly as
on first boot. No changes needed to dungeon <-> overworld transitions — those
don't go through `QuitToMenu()` and the HUD objects are left untouched.

## Files touched
- `Assets/Scripts/UI/InGame/GameHudInitializer.cs` — added `ShutdownHud()` and `DestroyGo()` helper.
- `Assets/Scripts/IsoCoreFoundation/UI/PauseMenu.cs` — `QuitToMenu()` calls `GameHudInitializer.ShutdownHud()`.

# PauseMenu.cs CS0103 'LitIso' build fix — 2026-06-14

## Error
```
Assets\Scripts\IsoCoreFoundation\UI\PauseMenu.cs(149,13): error CS0103: The name 'LitIso' does not exist in the current context
```
Build-blocking (Tundra build failed, IsoCore.Foundation.dll).

## Root cause
The #58 HUD-persistence fix added a direct call:
```csharp
LitIso.UI.InGame.GameHudInitializer.ShutdownHud();
```
into `PauseMenu.QuitToMenu()`. `PauseMenu.cs` compiles as part of the
`IsoCore.Foundation` asmdef assembly, which has no reference to (and is
compiled *before*) the loose `Assets/Scripts/UI/InGame/` scripts
(`LitIso.UI.InGame`, part of the default Assembly-CSharp). Cross-assembly
type reference in the wrong direction — `LitIso` namespace is unresolvable.

## Fix (event-based decoupling)
- `Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs`: added
  `public static event Action HudShutdownRequested;` and
  `public static void RequestHudShutdown() => HudShutdownRequested?.Invoke();`
- `Assets/Scripts/IsoCoreFoundation/UI/PauseMenu.cs`: `QuitToMenu()` now calls
  `FoundationBootstrap.RequestHudShutdown();` instead of the direct LitIso call.
- `Assets/Scripts/UI/InGame/GameHudInitializer.cs`: `Hook()` now also
  subscribes `FoundationBootstrap.HudShutdownRequested += ShutdownHud;`
  (alongside the existing `Ready` subscription), so the HUD tears itself
  down exactly as before, but via an event raised from the assembly that
  can legally see it.

No behavior change versus the #58 intent — HUD still torn down on
Quit-to-Menu — just decoupled so `IsoCore.Foundation` builds cleanly.

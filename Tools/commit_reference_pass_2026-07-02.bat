@echo off
setlocal enabledelayedexpansion
rem ============================================================================
rem  LIT-ISO - land the 2026-07-02 reference-integration pass (TWO commits).
rem  Run from Windows (git-lfs required). Never pushes. Never touches main.
rem
rem  Commit 1: in-flight owner work that tracked code already depends on
rem            (atmosphere overlay, impact feedback, shaders, shadows, etc.)
rem  Commit 2: reference-integration pass (night readability, ward ring,
rem            biome discovery journal, HUD phase band, save v13)
rem ============================================================================
cd /d C:\Projects\Unity-Projects\LIT-ISO

for /f "delims=" %%b in ('git branch --show-current') do set BRANCH=%%b
echo Current branch: %BRANCH%
if "%BRANCH%"=="main" (
  echo ERROR: refusing to commit on main. Switch to the feature branch first.
  pause & exit /b 1
)

git lfs version >nul 2>&1
if errorlevel 1 (
  echo ERROR: git-lfs not available. Binaries must go through LFS. Aborting.
  pause & exit /b 1
)

echo.
echo ==== Commit 1: land in-flight owner work =================================
git add ^
  Assets/Scripts/IsoCoreFoundation/World/FoundationAtmosphereOverlay.cs ^
  Assets/Scripts/IsoCoreFoundation/World/FoundationAtmosphereOverlay.cs.meta ^
  Assets/Scripts/IsoCoreFoundation/World/FoundationImpactFeedback.cs ^
  Assets/Scripts/IsoCoreFoundation/World/FoundationImpactFeedback.cs.meta ^
  Assets/Scripts/IsoCoreFoundation/Editor/AtmosphereTextureImporter.cs ^
  Assets/Scripts/IsoCoreFoundation/Editor/AtmosphereTextureImporter.cs.meta ^
  Assets/Shaders/AtmosphereStrip.shader ^
  Assets/Shaders/AtmosphereStrip.shader.meta ^
  Assets/Shaders/ProjectedSpriteShadow.shader ^
  Assets/Shaders/ProjectedSpriteShadow.shader.meta ^
  "Assets/Resources/VFX.meta" ^
  "Assets/Resources/VFX" ^
  Assets/Scripts/IsoCoreFoundation/Biomes/BiomeDefinition.cs ^
  Assets/Scripts/IsoCoreFoundation/Blocks/BlockDefinition.cs ^
  Assets/Scripts/IsoCoreFoundation/Core/DayNightSystem.cs ^
  Assets/Scripts/IsoCoreFoundation/Core/FoundationConfig.cs ^
  Assets/Scripts/IsoCoreFoundation/Editor/FoundationBiomeMapExporter.cs ^
  Assets/Scripts/IsoCoreFoundation/Editor/FoundationIntegratedSliceValidator.cs ^
  Assets/Scripts/IsoCoreFoundation/Harvesting/ResourceNode.cs ^
  Assets/Scripts/IsoCoreFoundation/Player/IsoFoundationPlayer.cs ^
  Assets/Scripts/IsoCoreFoundation/Player/PlayerInteraction.cs ^
  Assets/Scripts/IsoCoreFoundation/Progression/FoundationAbilityDispatcher.cs ^
  Assets/Scripts/IsoCoreFoundation/Progression/FoundationProjectile.cs ^
  Assets/Scripts/IsoCoreFoundation/World/BiomeSuiteLoader.cs ^
  Assets/Scripts/IsoCoreFoundation/World/DecorationShadow.cs ^
  Assets/Scripts/IsoCoreFoundation/World/FoundationWeatherVisuals.cs ^
  Assets/Scripts/IsoCoreFoundation/World/IsoTerrainSampler.cs ^
  Assets/Scripts/UI/CharacterCreator/LayeredCharacterAnimator.cs ^
  Docs/IsoCoreFoundation/Biome_Prop_Placement_Rules.md ^
  Docs/UI_HANDOFF_REVIEW_2026-06-30.md ^
  CREDITS_VFX.txt
if errorlevel 1 ( echo git add failed & pause & exit /b 1 )
git commit -m "wip: land in-flight atmosphere/shadow/feedback work (owner session work; required by tracked FoundationBootstrap)"
if errorlevel 1 ( echo Commit 1 failed (possibly nothing staged) & pause & exit /b 1 )

echo.
echo ==== Commit 2: reference-integration pass ================================
git add ^
  Assets/Scripts/IsoCoreFoundation/World/FoundationBiomeDiscovery.cs ^
  Assets/Scripts/IsoCoreFoundation/World/FoundationBiomeDiscovery.cs.meta ^
  Assets/Scripts/IsoCoreFoundation/Survival/CampfireWardRing.cs ^
  Assets/Scripts/IsoCoreFoundation/Survival/CampfireWardRing.cs.meta ^
  Assets/Scripts/IsoCoreFoundation/Survival/FoundationCampingSystem.cs ^
  Assets/Scripts/IsoCoreFoundation/Mobs/Mob.cs ^
  Assets/Scripts/IsoCoreFoundation/Core/FoundationSaveData.cs ^
  Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs ^
  Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs ^
  Assets/Scripts/UI/InGame/GameUIController.cs ^
  Docs/REFERENCE_INTEGRATION_ELACORIA_ROMESTEAD_2026-07-02.md ^
  Tools/commit_reference_pass_2026-07-02.bat
if errorlevel 1 ( echo git add failed & pause & exit /b 1 )
git commit -m "feat(survival/ui): night-readability + biome-discovery pass (Elacoria/Romestead reference integration, save v13)" -m "Dusk/nightfall warnings; visible campfire ward ring; night-hunter tint; biome discovery journal -> Trial evidence; HUD live biome + phase colours. Bootstrap/Content/GameUIController/Mob also carry in-flight owner hunks that could not be split file-internally. Details: Docs/REFERENCE_INTEGRATION_ELACORIA_ROMESTEAD_2026-07-02.md"
if errorlevel 1 ( echo Commit 2 failed & pause & exit /b 1 )

echo.
echo ==== Result ==============================================================
git log --oneline -4
echo.
echo Remaining un-staged work (expected: UI studio tooling, scratch, etc.):
git status --short | findstr /v "^$" | more
echo.
echo Done. Nothing was pushed. Review with: git show HEAD, git show HEAD~1
pause

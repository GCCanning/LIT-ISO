@echo off
setlocal
rem ============================================================================
rem  LIT-ISO - land the "watching System" trial pass + combat/jump/VFX fixes.
rem  Run from Windows AFTER commit_reference_pass_2026-07-02.bat. Never pushes.
rem ============================================================================
cd /d C:\Projects\Unity-Projects\LIT-ISO

for /f "delims=" %%b in ('git branch --show-current') do set BRANCH=%%b
if "%BRANCH%"=="main" ( echo ERROR: refusing to commit on main. & pause & exit /b 1 )

echo ==== Commit 1: directional melee + leap fixes + raw-animation VFX gate ====
git add ^
  Assets/Scripts/IsoCoreFoundation/Core/FoundationConfig.cs ^
  Assets/Scripts/IsoCoreFoundation/Mobs/Mob.cs ^
  Assets/Scripts/IsoCoreFoundation/Player/IsoFoundationPlayer.cs
git commit -m "fix(combat/movement): directional melee cone, smooth river/gap leaps, attack-VFX gate" -m "Melee hits what's in front via the live mob registry (mobs have no colliders; the old physics overlap never found them). Leaps land on cell centres and hold takeoff height mid-arc (no more stutter/caught-on-bank); jumpLeapTiles 2->3. New cfg.attackVfxEnabled=false hides slash arc + sprite flash so raw LPC/tool animations read."
if errorlevel 1 ( echo Commit 1 failed & pause & exit /b 1 )

echo ==== Commit 2: the System watches - trial scoring feels judged ============
git add ^
  Assets/Scripts/IsoCoreFoundation/Progression/FoundationProgression.cs ^
  Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs ^
  Assets/Scripts/IsoCoreFoundation/Core/FoundationContent.cs ^
  Assets/Scripts/IsoCoreFoundation/Survival/FoundationCampingSystem.cs ^
  Docs/REFERENCE_INTEGRATION_ELACORIA_ROMESTEAD_2026-07-02.md ^
  Tools/commit_watching_system_2026-07-02.bat
git commit -m "feat(trial): the System watches - judged proof, not grind (owner direction 2026-07-02)" -m "Hard diminishing returns per deed (full x5, half to 10, quarter to 20, then 0) on TRIAL SCORE only; novelty (new source per deed, e.g. each creature species) always counts fully; night/distance boldness multiplier; System speaks rarely (weighings, forecast changes, one dawn verdict per day); trial day now actually advances on the day/night wrap and completes into the existing class-offer pipeline; new night_endured evidence. Counts rebuild from the persisted evidence ledger - no save version change."
if errorlevel 1 ( echo Commit 2 failed & pause & exit /b 1 )

git log --oneline -3
echo Done. Nothing was pushed.
pause

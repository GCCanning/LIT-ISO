@echo off
setlocal
rem ============================================================================
rem  LIT-ISO - land: mob bodies + separation, leap feel, F9 unstuck, F10 recorder.
rem  Run AFTER commit_watching_system_2026-07-02.bat (if that one hasn't been run
rem  yet, run it first). Never pushes.
rem ============================================================================
cd /d C:\Projects\Unity-Projects\LIT-ISO

for /f "delims=" %%b in ('git branch --show-current') do set BRANCH=%%b
if "%BRANCH%"=="main" ( echo ERROR: refusing to commit on main. & pause & exit /b 1 )

git add ^
  Assets/Scripts/IsoCoreFoundation/Mobs/Mob.cs ^
  Assets/Scripts/IsoCoreFoundation/Player/IsoFoundationPlayer.cs ^
  Assets/Scripts/IsoCoreFoundation/Survival/FoundationDeathSystem.cs ^
  Assets/Scripts/IsoCoreFoundation/QoL/FoundationGameplayRecorder.cs ^
  Assets/Scripts/IsoCoreFoundation/QoL/FoundationGameplayRecorder.cs.meta ^
  Assets/Scripts/IsoCoreFoundation/Core/FoundationBootstrap.cs ^
  .gitignore
git commit -m "feat(feel/tools): mob bodies + separation, distance-scaled leaps, F9 unstuck, F10 gameplay recorder" -m "Mobs get trigger body colliders (targeting/projectiles/overlaps) + soft separation so they never stack in each other or the player (movement stays world-query). Leap duration scales with distance (constant speed feel). Hold F9 1.5s to be carried to the death-system wake point when trapped by worldgen. F10 records PNG frame bursts + session.json to Recordings/ for owner+AI review; folder gitignored."
if errorlevel 1 ( echo Nothing staged or commit failed - did you run the earlier scripts first? & pause & exit /b 1 )

git log --oneline -4
echo Done. Nothing was pushed.
pause

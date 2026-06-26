@echo off
setlocal
rem ── LIT-ISO one-click Windows build + desktop shortcut ──────────────────
set PROJECT=C:\Projects\Unity-Projects\LIT-ISO
set UNITY=C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe

if not exist "%UNITY%" (
  rem fall back: take the first editor the Hub has installed
  for /d %%v in ("C:\Program Files\Unity\Hub\Editor\*") do (
    if exist "%%v\Editor\Unity.exe" set UNITY=%%v\Editor\Unity.exe
  )
)
if not exist "%UNITY%" (
  echo Could not find Unity.exe under C:\Program Files\Unity\Hub\Editor\
  pause & exit /b 1
)

rem ── warn if Unity is still holding the project (batchmode will bail instantly,
rem    silently leaving any older build folder in place — looks like a stale
rem    "success" otherwise) ──────────────────────────────────────────────────
if exist "%PROJECT%\Temp\UnityLockfile" (
  echo NOTE: %PROJECT%\Temp\UnityLockfile exists. If the Unity Editor is open on
  echo this project, CLOSE IT NOW or this batchmode build will fail instantly
  echo with no useful error in build_log.txt.
  echo.
)

echo Building with: %UNITY%
echo This takes a few minutes; close the Unity Editor first if it is open.

rem NOTE (2026-06-14): -nographics was removed below. On this machine, batchmode
rem + -nographics together crashed Unity during native bootstrap (before any
rem script compilation), every time, at the same byte offset in the log. The
rem Editor opens and compiles fine in normal (GUI) mode, so the GPU/D3D init
rem path that -nographics tries to skip appears to be required for a clean
rem batchmode start here. If this still fails, try adding -force-d3d11 (the
rem D3D12 path is the default and is newer/less battle-tested in batchmode).

rem Each build gets its own timestamped folder under Build\ (GameBuilder.cs),
rem so record what's there BEFORE the build to detect the NEW folder afterward.
set "PREVLATEST="
for /f "delims=" %%d in ('dir "%PROJECT%\Build" /b /ad /o-d 2^>nul ^| findstr /b "LIT-ISO_"') do (
  if not defined PREVLATEST set "PREVLATEST=%%d"
)

"%UNITY%" -batchmode -quit -projectPath "%PROJECT%" -executeMethod BuildScript.BuildWindows -logFile "%PROJECT%\Build\build_log.txt"

rem Find the newest timestamped build folder now.
set "NEWLATEST="
for /f "delims=" %%d in ('dir "%PROJECT%\Build" /b /ad /o-d 2^>nul ^| findstr /b "LIT-ISO_"') do (
  if not defined NEWLATEST set "NEWLATEST=%%d"
)

if not defined NEWLATEST (
  echo BUILD FAILED — no Build\LIT-ISO_* folder was produced at all.
  echo See Build\build_log.txt ^(paste the tail to Claude^).
  pause & exit /b 1
)

if "%NEWLATEST%"=="%PREVLATEST%" (
  echo BUILD FAILED — Build\%NEWLATEST% already existed before this run, so
  echo Unity did not produce a new build this time ^(it likely exited
  echo immediately — check the UnityLockfile note above, and see
  echo Build\build_log.txt^).
  pause & exit /b 1
)

set "OUTDIR=%PROJECT%\Build\%NEWLATEST%\LIT-ISO"

if not exist "%OUTDIR%\LIT-ISO.exe" (
  echo BUILD FAILED — see Build\build_log.txt ^(paste the tail to Claude^).
  pause & exit /b 1
)

rem ── desktop shortcut (always points at the newest build) ───────────────────
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $lnk = $ws.CreateShortcut([Environment]::GetFolderPath('Desktop') + '\LIT-ISO.lnk'); $lnk.TargetPath = '%OUTDIR%\LIT-ISO.exe'; $lnk.WorkingDirectory = '%OUTDIR%'; $lnk.Save()"

echo.
echo Done! Built to: %OUTDIR%
echo A desktop shortcut now points at this build.
echo To pin to the taskbar: right-click the desktop shortcut ^> Pin to taskbar
echo   (Windows does not allow programs to pin for you.)

if exist "%OUTDIR%\build_info.txt" (
  echo.
  echo ── Build info ──────────────────────────────────────────────────────
  type "%OUTDIR%\build_info.txt"
  echo ─────────────────────────────────────────────────────────────────────
)

echo.
choice /C YN /M "Launch LIT-ISO now"
if errorlevel 2 goto :skiplaunch
start "" "%OUTDIR%\LIT-ISO.exe"
:skiplaunch

pause

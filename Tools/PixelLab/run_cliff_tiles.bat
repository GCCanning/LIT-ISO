@echo off
cd /d "%~dp0"
echo Starting cliff tile generation...
py -3 generate_cliff_tiles.py %*
if errorlevel 1 (
    echo.
    echo Script exited with an error. Check cliff_tiles_crash.log in this folder.
    pause
) else (
    pause
)

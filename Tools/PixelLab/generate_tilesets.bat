@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 generate_tilesets.py %*) || (python generate_tilesets.py %*)
pause

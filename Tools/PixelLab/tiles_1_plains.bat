@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_tilesets.py --family plains %*) else (python generate_tilesets.py --family plains %*)
pause

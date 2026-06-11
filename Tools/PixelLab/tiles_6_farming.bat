@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_tilesets.py --family farming %*) else (python generate_tilesets.py --family farming %*)
pause

@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_props.py --family forest %*) else (python generate_props.py --family forest %*)
pause

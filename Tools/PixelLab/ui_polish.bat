@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_ui_polish.py %*) else (python generate_ui_polish.py %*)
pause

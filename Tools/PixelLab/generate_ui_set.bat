@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 generate_ui_set.py %*) || (python generate_ui_set.py %*)
pause

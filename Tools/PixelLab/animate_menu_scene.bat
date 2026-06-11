@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 animate_menu_scene.py %*) || (python animate_menu_scene.py %*)
pause

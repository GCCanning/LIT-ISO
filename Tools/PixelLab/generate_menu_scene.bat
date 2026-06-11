@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 generate_menu_scene.py %*) || (python generate_menu_scene.py %*)
pause

@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 generate_class_scene.py %*) || (python generate_class_scene.py %*)
pause

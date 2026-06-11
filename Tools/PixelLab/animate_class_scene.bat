@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 animate_class_scene.py %*) else (python animate_class_scene.py %*)
pause

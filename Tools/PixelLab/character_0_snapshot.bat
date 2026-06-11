@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_character_set.py --phase snapshot %*) else (python generate_character_set.py --phase snapshot %*)
pause

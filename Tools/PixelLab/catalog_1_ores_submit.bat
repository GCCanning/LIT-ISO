@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_catalog_props.py --family ores %*) else (python generate_catalog_props.py --family ores %*)
pause

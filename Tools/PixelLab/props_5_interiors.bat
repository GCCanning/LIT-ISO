@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_catalog_props.py --family guild & py -3 generate_catalog_props.py --family library & py -3 generate_catalog_props.py --family tavern) else (python generate_catalog_props.py --family guild & python generate_catalog_props.py --family library & python generate_catalog_props.py --family tavern)
pause

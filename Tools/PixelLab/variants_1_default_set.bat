@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_palette_variants.py --default-set --presets lush,dry,autumn,frost --register --contact-sheet %*) else (python generate_palette_variants.py --default-set --presets lush,dry,autumn,frost --register --contact-sheet %*)
pause

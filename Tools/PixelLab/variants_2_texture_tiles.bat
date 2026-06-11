@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_texture_variants.py --default-set --count 2 --register --contact-sheet --overwrite %*) else (python generate_texture_variants.py --default-set --count 2 --register --contact-sheet --overwrite %*)
pause

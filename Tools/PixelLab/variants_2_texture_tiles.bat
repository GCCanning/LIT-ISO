@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_texture_variants.py --default-set --mode remix --count 4 --strength 1.2 --seed litiso --register --contact-sheet --overwrite %*) else (python generate_texture_variants.py --default-set --mode remix --count 4 --strength 1.2 --seed litiso --register --contact-sheet --overwrite %*)
pause

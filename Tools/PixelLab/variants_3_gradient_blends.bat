@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 generate_tile_blends.py --default-pairs --register --contact-sheet --overwrite %*) else (python generate_tile_blends.py --default-pairs --register --contact-sheet --overwrite %*)
pause

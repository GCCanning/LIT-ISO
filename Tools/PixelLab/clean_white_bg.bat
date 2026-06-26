@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (py -3 clean_white_bg.py "C:\Users\garyc\OneDrive\Desktop\PixelArt\Props" %*) else (python clean_white_bg.py "C:\Users\garyc\OneDrive\Desktop\PixelArt\Props" %*)
pause

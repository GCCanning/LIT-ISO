@echo off
cd /d "%~dp0"
where py >nul 2>nul
if %errorlevel%==0 (set PY=py -3) else (set PY=python)

echo === 1/4: queueing the lights family (campfire, torches, brazier, lanterns) ===
%PY% generate_catalog_props.py --family lights

echo.
echo === 2/4: downloading ALL catalog prop candidates (town/guild/library/tavern/camp/buildings/stations/ores/chest/lights/ambient) ===
%PY% generate_catalog_props.py --status

echo.
echo === 3/4: downloading plains/forest prop candidates ===
%PY% generate_props.py --status

echo.
echo === 4/4: cleaning white backgrounds (originals kept as .orig.png) ===
%PY% clean_white_bg.py "C:\Users\garyc\OneDrive\Desktop\PixelArt\Props"

echo.
echo All done - contact sheets are in each PixelArt\Props\^<family^>\^<prop^> folder.
echo If the lights family was still generating, run this bat again in ~5 minutes
echo to pull its candidates too.
pause

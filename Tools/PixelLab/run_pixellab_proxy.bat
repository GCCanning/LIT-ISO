@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 pixellab_proxy.py) || (python pixellab_proxy.py)
pause

@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 import_menu_frames.py %*) && goto :done
where python >nul 2>nul && (python import_menu_frames.py %*) && goto :done
if exist "%LocalAppData%\Programs\Python\Python311\python.exe" (
  "%LocalAppData%\Programs\Python\Python311\python.exe" import_menu_frames.py %*
  goto :done
)
echo Python was not found. Install Python or add py/python to PATH.
:done
pause

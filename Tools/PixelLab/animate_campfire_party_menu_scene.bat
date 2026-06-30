@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 animate_campfire_party_menu_scene.py %*) && goto :done
where python >nul 2>nul && (python animate_campfire_party_menu_scene.py %*) && goto :done
if exist "%LocalAppData%\Programs\Python\Python311\python.exe" (
  "%LocalAppData%\Programs\Python\Python311\python.exe" animate_campfire_party_menu_scene.py %*
  goto :done
)
echo Python was not found. Install Python or add py/python to PATH.
:done
pause

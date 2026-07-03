@echo off
setlocal
cd /d "%~dp0"

set "BUNDLED_PY=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
if exist "%BUNDLED_PY%" goto run_bundled
where py >nul 2>nul
if not errorlevel 1 goto run_py
python generate_ui_handoff_assets.py %*
goto finished

:run_bundled
"%BUNDLED_PY%" generate_ui_handoff_assets.py %*
goto finished

:run_py
py -3 generate_ui_handoff_assets.py %*

:finished
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if "%EXIT_CODE%"=="0" (
  echo UI handoff generator finished successfully.
) else (
  echo UI handoff generator failed with exit code %EXIT_CODE%.
)
pause

endlocal & exit /b %EXIT_CODE%

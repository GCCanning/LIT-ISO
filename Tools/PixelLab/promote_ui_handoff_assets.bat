@echo off
setlocal
cd /d "%~dp0"

set "BUNDLED_PY=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
if exist "%BUNDLED_PY%" goto run_bundled
where py >nul 2>nul
if not errorlevel 1 goto run_py
python promote_ui_handoff_assets.py %*
goto finished

:run_bundled
"%BUNDLED_PY%" promote_ui_handoff_assets.py %*
goto finished

:run_py
py -3 promote_ui_handoff_assets.py %*

:finished
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if "%EXIT_CODE%"=="0" (
  echo UI asset promotion check finished successfully.
) else (
  echo UI asset promotion check failed with exit code %EXIT_CODE%.
)
pause

endlocal & exit /b %EXIT_CODE%

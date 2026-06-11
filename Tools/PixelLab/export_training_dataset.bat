@echo off
cd /d "%~dp0"
where py >nul 2>nul && (py -3 export_training_dataset.py %*) || (python export_training_dataset.py %*)
pause

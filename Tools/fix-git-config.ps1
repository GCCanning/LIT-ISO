# Strips null bytes from corrupted .git/config
# Run from PowerShell:
#   cd C:\Projects\Unity-Projects\LIT-ISO
#   .\Tools\fix-git-config.ps1

$path = ".git\config"
$raw = [System.IO.File]::ReadAllBytes($path)
$clean = $raw | Where-Object { $_ -ne 0 }
[System.IO.File]::WriteAllBytes($path, $clean)
Write-Host "Fixed. Null bytes removed from .git/config" -ForegroundColor Green

# Verify
git config --list | Select-String "game-feel"
git log --oneline -3

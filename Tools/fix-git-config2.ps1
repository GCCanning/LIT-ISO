# Fix corrupted .git/config (null bytes) - uses absolute path
$path = "C:\Projects\Unity-Projects\LIT-ISO\.git\config"
$raw = [System.IO.File]::ReadAllBytes($path)
$clean = $raw | Where-Object { $_ -ne 0 }
[System.IO.File]::WriteAllBytes($path, $clean)
Write-Host "Fixed." -ForegroundColor Green
Set-Location "C:\Projects\Unity-Projects\LIT-ISO"
git log --oneline origin/main -5

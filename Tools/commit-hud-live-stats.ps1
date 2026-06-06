
# LIT-ISO: Commit FoundationHudAdapter live stats binding
# Run: cd C:\Projects\Unity-Projects\LIT-ISO && .\Tools\commit-hud-live-stats.ps1

Set-Location "C:\Projects\Unity-Projects\LIT-ISO"

function Remove-Lock {
    $lock = "C:\Projects\Unity-Projects\LIT-ISO\.git\index.lock"
    if (Test-Path $lock) { Remove-Item $lock -Force; Write-Host "Removed index.lock" -ForegroundColor Yellow }
}

Remove-Lock
git checkout main
Remove-Lock
git pull --rebase origin main
if ($LASTEXITCODE -ne 0) { Write-Host "Pull failed" -ForegroundColor Red; exit 1 }

Remove-Lock
git checkout -b claude/hud-live-stats

git add "Assets/Scripts/UI/InGame/FoundationHudAdapter.cs"

$msg = @"
Claude HUD: wire vitals to live PlayerHealth/Mana/XPSystem singletons

FoundationHudAdapter.cs: replace static placeholder values with live reads.

Health01 - reads PlayerHealth.Instance.CurrentHealth / maxHealth
Mana01   - reads PlayerMana.Instance.CurrentMana / MaxMana
Xp01     - reads XPSystem.Instance.XPInCurrentLevel / XPNeededForNextLevel
Level    - reads XPSystem.Instance.CurrentLevel
Hunger01 - stays 0f (survival scope not yet active, bar hidden by default)

Event subscriptions added in ctor / Dispose:
  PlayerHealth.OnHealthChanged -> Changed
  PlayerMana.OnManaChanged     -> Changed
  PlayerStats.OnStatsChanged   -> Changed (catches stat allocation re-deriving maxHP/MP)
  XPSystem.OnXPGained          -> Changed
  XPSystem.OnLevelUp           -> Changed

All subscriptions are null-guarded so the HUD still renders correctly in
scenes where the player GameObjects haven't spawned yet (e.g. main menu).
No changes to IGameHudModel interface or any Foundation assembly file.
"@
$msg | Out-File -FilePath ".git\COMMIT_EDITMSG_TMP" -Encoding utf8
git commit -F ".git\COMMIT_EDITMSG_TMP"
Remove-Item ".git\COMMIT_EDITMSG_TMP" -Force -ErrorAction SilentlyContinue

git push -u origin claude/hud-live-stats
Write-Host ""
Write-Host "PR: https://github.com/GCCanning/LIT-ISO/compare/main...claude/hud-live-stats" -ForegroundColor Green
Write-Host "Done." -ForegroundColor Green

# push.ps1 - one-liner to commit everything pending and open a PR
# Usage: .\Tools\push.ps1 <branch-name>
# Example: .\Tools\push.ps1 claude/hud-live-stats
#
# What it does:
#   1. Removes index.lock if stale
#   2. Syncs main with origin
#   3. Creates (or switches to) the branch
#   4. Stages ALL tracked-file changes + all untracked files NOT in .gitignore
#   5. Writes Claude's prepared commit message from .git/COMMIT_MSG if it exists,
#      otherwise prompts you to type one
#   6. Commits and pushes
#   7. Prints the PR URL

param(
    [Parameter(Mandatory)][string]$Branch,
    [string]$Message = ""
)

Set-Location "C:\Projects\Unity-Projects\LIT-ISO"

function Remove-Lock {
    $lock = "C:\Projects\Unity-Projects\LIT-ISO\.git\index.lock"
    if (Test-Path $lock) { Remove-Item $lock -Force; Write-Host "Removed index.lock" -ForegroundColor Yellow }
}

# 1. Clean lock
Remove-Lock

# 2. Sync main
git checkout main 2>$null
Remove-Lock
git stash push -m "push-script-drift" --include-untracked 2>$null
Remove-Lock
git pull --rebase origin main
if ($LASTEXITCODE -ne 0) { Write-Host "Pull failed - fix conflicts and retry" -ForegroundColor Red; exit 1 }
git stash pop 2>$null
Remove-Lock

# 3. Branch
$existing = git branch --list $Branch
if ($existing) {
    git checkout $Branch
} else {
    git checkout -b $Branch
}
Remove-Lock

# 4. Stage
git add -u          # all tracked changes
git add .           # all untracked (respects .gitignore)
Remove-Lock

# 5. Commit message
$msgFile = ".git\COMMIT_MSG"
if ($Message -ne "") {
    $Message | Out-File -FilePath ".git\COMMIT_EDITMSG_TMP" -Encoding utf8
    git commit -F ".git\COMMIT_EDITMSG_TMP"
    Remove-Item ".git\COMMIT_EDITMSG_TMP" -Force -ErrorAction SilentlyContinue
} elseif (Test-Path $msgFile) {
    git commit -F $msgFile
    Remove-Item $msgFile -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "No commit message. Either:" -ForegroundColor Yellow
    Write-Host "  - Pass -Message 'your message'" -ForegroundColor Yellow
    Write-Host "  - Or save message to .git\COMMIT_MSG then re-run" -ForegroundColor Yellow
    exit 1
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Nothing to commit or commit failed." -ForegroundColor Yellow
    exit 0
}

# 6. Push
git push -u origin $Branch
Remove-Lock

# 7. PR link
$repo = "GCCanning/LIT-ISO"
Write-Host ""
Write-Host "PR: https://github.com/$repo/compare/main...$Branch" -ForegroundColor Green

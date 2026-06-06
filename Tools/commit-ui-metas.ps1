# LIT-ISO: Commit orphaned Claude UI .meta files
# These are Unity-generated .meta files for existing UI scripts that were never committed.
# Run from PowerShell in the project root:
#   cd C:\Projects\Unity-Projects\LIT-ISO
#   .\Tools\commit-ui-metas.ps1

Set-Location "C:\Projects\Unity-Projects\LIT-ISO"

# Remove stale lock if present
$lock = Join-Path (Get-Location) ".git\index.lock"
if (Test-Path $lock) {
    Remove-Item $lock -Force
    Write-Host "Removed stale index.lock" -ForegroundColor Yellow
}

# Switch back to main first, then branch
git checkout main
git pull --rebase origin main

# Create the fix branch
git checkout -b claude/fix-ui-metas

# Stage the orphaned meta files (Claude lane - UI/InGame)
git add "Assets/Scripts/UI/InGame.meta"
git add "Assets/Scripts/UI/InGame/CharacterSheetView.cs.meta"
git add "Assets/Scripts/UI/InGame/CraftingView.cs.meta"
git add "Assets/Scripts/UI/InGame/FoundationHudAdapter.cs.meta"
git add "Assets/Scripts/UI/InGame/FoundationInventoryAdapter.cs.meta"
git add "Assets/Scripts/UI/InGame/GameHudInitializer.cs.meta"
git add "Assets/Scripts/UI/InGame/GamePanelsController.cs.meta"
git add "Assets/Scripts/UI/InGame/InventoryView.cs.meta"
git add "Assets/Scripts/UI/InGame/ItemIconResolver.cs.meta"
git add "Assets/Scripts/UI/InGame/UiBuilder.cs.meta"

# Also the Editor TileImportPostprocessor meta that was missed
git add "Assets/Scripts/Editor/TileImportPostprocessor.cs.meta"

# Also the TileSpriteResolver meta
git add "Assets/Scripts/IsoCoreFoundation/World/TileSpriteResolver.cs.meta"

git commit -m "Fix: commit missing .meta files for UI/InGame scripts

Unity generates a .meta for every .cs file but these were never staged.
Missing metas cause yellow '!' warnings in Unity's Project view and
break asset GUIDs for anyone who clones fresh.

- Assets/Scripts/UI/InGame.meta (folder meta)
- CharacterSheetView.cs.meta
- CraftingView.cs.meta
- FoundationHudAdapter.cs.meta
- FoundationInventoryAdapter.cs.meta
- GameHudInitializer.cs.meta
- GamePanelsController.cs.meta
- InventoryView.cs.meta
- ItemIconResolver.cs.meta
- UiBuilder.cs.meta
- Assets/Scripts/Editor/TileImportPostprocessor.cs.meta
- Assets/Scripts/IsoCoreFoundation/World/TileSpriteResolver.cs.meta"

# Push
git push -u origin claude/fix-ui-metas

Write-Host ""
Write-Host "PR URL:" -ForegroundColor Cyan
Write-Host "https://github.com/GCCanning/LIT-ISO/compare/main...claude/fix-ui-metas" -ForegroundColor Green
Write-Host ""
git log --oneline -3

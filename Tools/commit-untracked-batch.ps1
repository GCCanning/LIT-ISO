# LIT-ISO: Fix pull conflict + commit untracked Unity AI content in logical batches
# Run: cd C:\Projects\Unity-Projects\LIT-ISO && .\Tools\commit-untracked-batch.ps1

Set-Location "C:\Projects\Unity-Projects\LIT-ISO"

function Remove-Lock {
    $lock = "C:\Projects\Unity-Projects\LIT-ISO\.git\index.lock"
    if (Test-Path $lock) { Remove-Item $lock -Force; Write-Host "Removed index.lock" -ForegroundColor Yellow }
}

# -----------------------------------------------------------------------
# STEP 1: Fix pull conflict and sync main
# -----------------------------------------------------------------------
Write-Host "`n=== STEP 1: Sync main ===" -ForegroundColor Cyan
Remove-Lock
git checkout main
Remove-Lock

# Remove local copies that already landed in main via PR #10
Remove-Item "Assets\Scripts\Editor\AssetForgeGeneratedImportPostprocessor.cs" -Force -ErrorAction SilentlyContinue
Remove-Item "Assets\Scripts\Editor\AssetForgeGeneratedImportPostprocessor.cs.meta" -Force -ErrorAction SilentlyContinue

git stash push -m "drift-before-untracked-batch" --include-untracked 2>$null
Remove-Lock
git pull --rebase origin main
if ($LASTEXITCODE -ne 0) { Write-Host "Pull failed" -ForegroundColor Red; exit 1 }

git stash pop
Remove-Lock
Write-Host "main synced: $(git log --oneline -1)" -ForegroundColor Green

# -----------------------------------------------------------------------
# STEP 2: Update .gitignore
# -----------------------------------------------------------------------
$gi = Get-Content ".gitignore" -Raw
if ($gi -notmatch "__pycache__") {
    Add-Content ".gitignore" "`n# Python cache`n__pycache__/`n*.pyc`n*.pyo"
    Write-Host "Updated .gitignore" -ForegroundColor Green
}

# -----------------------------------------------------------------------
# STEP 3: claude/resources-audio-items
# -----------------------------------------------------------------------
Write-Host "`n=== STEP 3: claude/resources-audio-items ===" -ForegroundColor Cyan
Remove-Lock
git checkout -b claude/resources-audio-items

git add "Assets/Resources/Items/"
git add "Assets/Resources/Audio/"
git add "Assets/Resources/Decorations/"
git add "Assets/Resources/Materials/"
git add "Assets/Resources/Tiles.meta"
git add "Assets/Resources/UI/InGame.meta"
git add "Assets/Resources/UI/Menu.meta"
git add "Assets/Shaders/SpriteAmbient.shader"
git add "Assets/Shaders/SpriteAmbient.shader.meta"
git add ".gitignore"

$msg1 = @"
Claude resources: item icons, audio, decorations, shader, folder metas

- Assets/Resources/Items/ - 8 item icon PNGs (apple, copper_ore, fiber, hide,
  stone, wood, wood_axe, wood_pickaxe). Drop-in for ItemIconResolver.
- Assets/Resources/Audio/Music/ - day.wav + night.wav for WorldAudioController.
- Assets/Resources/Audio/SFX/ - SFX pool folder (SfxManager loads by key).
- Assets/Resources/Audio/Ambient/ - ambient audio folder.
- Assets/Resources/Decorations/ - decoration sprites for DecorationSpriteResolver.
- Assets/Resources/Materials/ - shared runtime materials folder.
- Assets/Shaders/SpriteAmbient.shader - single-pass sprite shader that reads
  the global _AmbientColor set by AmbientLightController (day/night tint).
- Folder metas: Tiles.meta, UI/InGame.meta, UI/Menu.meta.
- .gitignore: added __pycache__/ and *.pyc rules.
"@
$msg1 | Out-File -FilePath ".git\COMMIT_EDITMSG_TMP" -Encoding utf8
git commit -F ".git\COMMIT_EDITMSG_TMP"

git push -u origin claude/resources-audio-items
Write-Host "PR 1: https://github.com/GCCanning/LIT-ISO/compare/main...claude/resources-audio-items" -ForegroundColor Green

# -----------------------------------------------------------------------
# STEP 4: codex/tilemap-assetforge-docs
# -----------------------------------------------------------------------
Write-Host "`n=== STEP 4: codex/tilemap-assetforge-docs ===" -ForegroundColor Cyan
Remove-Lock
git checkout main
Remove-Lock
git checkout -b codex/tilemap-assetforge-docs

git add "Assets/Tilemaps/Isometric/Tiles/Plains/"
git add "Tools/AssetForge/"
git add "Tools/LoRA/eval_latest_synced_lora.ps1"
git add "Tools/LoRA/eval_litiso_tile_prop_v1_comfy.py"
git add "Tools/LoRA/pause_litiso_training.ps1"
git add "Tools/LoRA/start_resumable_litiso_training.ps1"
git add "Tools/LoRA/status_litiso_training.ps1"
git add "Tools/LoRA/sync_lora_to_comfyui.ps1"
git add "Tools/LoRA/train_litiso_lora_resumable.py"
git add "Docs/IsoCoreFoundation/11_AssetForge_Sprixen_Quality_Standard.md"
git add "Docs/IsoCoreFoundation/12_AssetForge_Local_Dashboard_Spec.md"
git add "Docs/IsoCoreFoundation/13_AssetForge_Self_Review.md"
git add "Docs/IsoCoreFoundation/14_AssetForge_Production_Readiness_Map.md"
git add "Docs/IsoCoreFoundation/SprixenClone_Backlog.md"
git add "Assets/Generated/Tiles/"
git add "Assets/Generated/Props/"
git add "Assets/Generated/_Datasets/"
git add "Assets/Generated/_Review/CodexBiomeStarter.meta"
git add "Assets/Generated/_Review/CodexBiomeStarter/"

$msg2 = @"
Codex: tilemap tiles, AssetForge pipeline, LoRA tools, generated assets, docs

Tilemap tile ScriptableObjects (Assets/Tilemaps/Isometric/Tiles/Plains/):
- Tile_Plains_Dirt, Grass1/2/3, Soil, StoneBlock, StonePath, Water

AssetForge pipeline (Tools/AssetForge/):
- serve_dashboard.ps1 - local HTTP server for review dashboard
- build_review_pack.ps1, approve_review_pack.ps1 - review/approval gate
- capture_dataset_from_review.ps1 - dataset capture from review decisions
- test_strict_asset_quality.ps1 - strict QA scanner (36/36 pass)
- Dashboard/ - HTML review dashboard UI

LoRA training scripts (Tools/LoRA/):
- train_litiso_lora_resumable.py + start/pause/status/sync/eval scripts
- litiso_tile_prop_v1 LoRA trained and synced to ComfyUI (experimental)

Generated approved assets (Assets/Generated/):
- Tiles/Plains/ - 10 approved plains tile variants
- Tiles/Forest/, Tiles/Shared/ - forest and shared tile variants
- Props/Plains/ - 5 approved plains props (bush, rock, trees, dry_bush)
- Props/Forest/, Props/Shared/ - forest and shared props
- _Datasets/ - training dataset captures
- _Review/CodexBiomeStarter/ - full review pack (36 assets, 36 pass strict QA)

New docs (Docs/IsoCoreFoundation/):
- 11_AssetForge_Sprixen_Quality_Standard.md
- 12_AssetForge_Local_Dashboard_Spec.md
- 13_AssetForge_Self_Review.md
- 14_AssetForge_Production_Readiness_Map.md
- SprixenClone_Backlog.md
"@
$msg2 | Out-File -FilePath ".git\COMMIT_EDITMSG_TMP" -Encoding utf8
git commit -F ".git\COMMIT_EDITMSG_TMP"

git push -u origin codex/tilemap-assetforge-docs
Write-Host "PR 2: https://github.com/GCCanning/LIT-ISO/compare/main...codex/tilemap-assetforge-docs" -ForegroundColor Green

# -----------------------------------------------------------------------
# STEP 5: claude/scripts-and-tools
# -----------------------------------------------------------------------
Write-Host "`n=== STEP 5: claude/scripts-and-tools ===" -ForegroundColor Cyan
Remove-Lock
git checkout main
Remove-Lock
git checkout -b claude/scripts-and-tools

git add "Tools/commit-game-feel-batch.ps1"
git add "Tools/commit-ui-metas.ps1"
git add "Tools/fix-git-config.ps1"
git add "Tools/fix-git-config2.ps1"
git add "Tools/commit-untracked-batch.ps1"
if (Test-Path "Assets/Plans") { git add "Assets/Plans/" }

$msg3 = @"
Tools: session helper scripts and project plans

PowerShell helper scripts (Tools/):
- commit-game-feel-batch.ps1 - staged the Codex game-feel batch
- commit-ui-metas.ps1 - staged orphaned UI .meta files
- fix-git-config.ps1 / fix-git-config2.ps1 - stripped null bytes from .git/config
- commit-untracked-batch.ps1 - organized untracked Unity AI content into commits

Assets/Plans/ - project planning documents.
"@
$msg3 | Out-File -FilePath ".git\COMMIT_EDITMSG_TMP" -Encoding utf8
git commit -F ".git\COMMIT_EDITMSG_TMP"

git push -u origin claude/scripts-and-tools
Write-Host "PR 3: https://github.com/GCCanning/LIT-ISO/compare/main...claude/scripts-and-tools" -ForegroundColor Green

# Cleanup temp file
Remove-Item ".git\COMMIT_EDITMSG_TMP" -Force -ErrorAction SilentlyContinue

# -----------------------------------------------------------------------
# Summary
# -----------------------------------------------------------------------
Write-Host "`n=== ALL DONE ===" -ForegroundColor Green
Write-Host "Merge these 3 PRs on GitHub (squash and merge, in order):"
Write-Host "  1. claude/resources-audio-items"
Write-Host "  2. codex/tilemap-assetforge-docs"
Write-Host "  3. claude/scripts-and-tools"
Write-Host ""
Write-Host "Still untracked (review separately - large files):"
Write-Host "  - Assets/AI Toolkit/   (Unity AI plugin)"
Write-Host "  - GeneratedAssets/ UUID folders  (AI-generated audio)"
Write-Host "  - Assets/Art/_Samples/"

# LIT-ISO: Commit Codex game-feel batch to codex/game-feel-batch branch
# Run from PowerShell in the project root:
#   cd C:\Projects\Unity-Projects\LIT-ISO
#   .\Tools\commit-game-feel-batch.ps1

Set-Location "C:\Projects\Unity-Projects\LIT-ISO"

# 1. Remove stale lock if present
$lock = ".git\index.lock"
if (Test-Path $lock) {
    Remove-Item $lock -Force
    Write-Host "Removed stale index.lock" -ForegroundColor Yellow
}

# 2. Switch to the branch (already created, just check it out)
git checkout codex/game-feel-batch
if ($LASTEXITCODE -ne 0) {
    git checkout -b codex/game-feel-batch
}

# 3. Stage all IsoCoreFoundation script changes (Codex lane)
git add Assets/Scripts/IsoCoreFoundation/
git add Assets/Scripts/Editor/AssetForgeGeneratedImportPostprocessor.cs
git add Assets/Scripts/Editor/AssetForgeGeneratedImportPostprocessor.cs.meta
git add Assets/Scripts/Editor/TileImportPostprocessor.cs.meta

# 4. Commit
git commit -m "Codex game-feel batch: animation, atmosphere, audio, FX, pause menu

New systems (all in Foundation lane):
- PlayerAnimator: 8-way directional sprite anim from ReferenceKnight sheet,
  walk/idle fps, subtle vertical bob. Auto-added by FoundationBootstrap.
- AmbientLightController: per-frame day/night world tint via shader global
  (dawn warm -> noon bright -> moonlit blue). Wired to DayNightSystem.
- AmbientParticles: pollen by day, fireflies by night, camera-following.
- CampfireGlow: flickering additive fake-light for placed campfires/lanterns,
  fades in with NightFactor so placed lights are genuinely useful in the dark.
- WorldFx: fire-and-forget particle bursts (debris, footstep dust, harvest pop).
- FloatingText: damage/loot numbers that drift up and fade.
- TargetHighlight: selection ring under hovered interactables.
- PropOcclusionFader: fades props that block the camera's view of the player.
- DecorationSpriteResolver: picks the right decoration sprite per biome/block.
- DecorationShadow: simple drop-shadow blob under props.
- SpriteAmbient: global ambient colour helper (one SetGlobalColor per frame).
- PauseMenu: Esc-toggled overlay with Resume/Quit-to-Menu + master/music/SFX
  volume sliders (PlayerPrefs), control hints. Self-contained uGUI.
- SfxManager: pooled one-shot SFX with pitch variance.
- WorldAudioController: day/night music bed crossfade.
- AssetForgeGeneratedImportPostprocessor: locks import settings on generated
  assets in Assets/Generated/_Review/<PackName>/ folders.

FoundationBootstrap wired:
- PlayerAnimator on player GO (after Init).
- AmbientLightController + AmbientParticles on bootstrap GO.
- AudioListener guard on camera, SfxManager.Ensure(), WorldAudioController.
- PauseMenu on bootstrap GO.
- PixelPerfectCamera setup (PPU 32, 640x360 ref, stretchFill) when
  config.pixelPerfect is true.

Core script updates:
- FoundationBootstrap, FoundationConfig, FoundationContent, IsoGrid,
  DayNightSystem, IsoWorldController, IsoWorldRenderer, IsoTerrainSampler,
  CraftingSystem, HarvestSystem, ResourceNode, Mob, IsoFoundationPlayer,
  PlayerInteraction, PlaceableDefinition, PlaceableInstance."

if ($LASTEXITCODE -ne 0) {
    Write-Host "Nothing new to commit (may already be staged). Checking..." -ForegroundColor Yellow
}

# 5. Push
git push -u origin codex/game-feel-batch
Write-Host ""
Write-Host "PR URL:" -ForegroundColor Cyan
Write-Host "https://github.com/GCCanning/LIT-ISO/compare/main...codex/game-feel-batch" -ForegroundColor Green

# --- Also clean up transit stashes (safe to drop - they're backup snapshots) ---
Write-Host ""
Write-Host "Dropping transit stashes..." -ForegroundColor Yellow
$stashes = git stash list | Select-String "transit-"
foreach ($s in $stashes) {
    $ref = ($s -split ":")[0]
    Write-Host "  Dropping $ref"
    git stash drop $ref 2>$null
}

# --- Delete merged local branches ---
Write-Host ""
Write-Host "Deleting merged local branches..." -ForegroundColor Yellow
$merged = @(
    "claude/foundation-hud-binding",
    "claude/hud-polish-and-tile-handoff",
    "claude/icon-integration",
    "claude/ingame-panels",
    "claude/ingame-ui",
    "claude/menu-save-hardening",
    "claude/tile-tessellation-hud-stacking",
    "codex/foundation-ui-contract",
    "codex/foundation-ui-contract-clean",
    "codex/integrated-slice-validation"
)
foreach ($b in $merged) {
    git branch -d $b 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Deleted $b" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Done. Open the PR at the URL above." -ForegroundColor Green

param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$BatchPath = (Join-Path $PSScriptRoot "training_batches\B1_cyan_knight_style_alignment.json"),
    [string]$Provider = "sprixen",
    [int]$MaxVariantsPerJob = 3,
    [switch]$ReplaceExisting
)

$ErrorActionPreference = "Stop"

function Get-SafeName {
    param([string]$Value, [string]$Default = "asset_job")
    if ([string]::IsNullOrWhiteSpace($Value)) { return $Default }
    $safe = ($Value.Trim() -replace "[^A-Za-z0-9_.-]", "_")
    $safe = $safe.Trim("._")
    if ([string]::IsNullOrWhiteSpace($safe)) { return $Default }
    return $safe
}

function Assert-UnderRoot {
    param([string]$Root, [string]$Path, [string]$Label)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd("\", "/")
    $pathFull = [IO.Path]::GetFullPath($Path).TrimEnd("\", "/")
    if ($pathFull -ne $rootFull -and -not $pathFull.StartsWith($rootFull + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label must stay inside ProjectRoot. Root: $rootFull Path: $pathFull"
    }
}

function Convert-ToRepoPath {
    param([string]$Path)
    $root = (Resolve-Path -LiteralPath $ProjectRoot).Path.TrimEnd("\", "/")
    $full = [IO.Path]::GetFullPath($Path)
    if ($full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($root.Length + 1).Replace("\", "/")
    }
    return $full.Replace("\", "/")
}

function New-Request {
    param([object]$Batch, [object]$Job)

    $jobName = Get-SafeName -Value ([string]$Job.job_name)
    $assetMode = [string]$Job.asset_mode
    $targetCount = [int]$Job.target_count
    $batchCount = [Math]::Max(1, [Math]::Min($MaxVariantsPerJob, $targetCount))
    $canvasSize = switch ($assetMode) {
        "tile" { 32 }
        "item" { 64 }
        default { 128 }
    }
    $ppu = $canvasSize
    $pivot = if ($assetMode -eq "tile" -or $assetMode -eq "item") { [ordered]@{ x = 0.5; y = 0.5 } } else { [ordered]@{ x = 0.5; y = 0.0 } }
    $anchor = if ($assetMode -eq "tile" -or $assetMode -eq "item") { "center" } else { "bottom_center" }
    $prompt = "$($Batch.global_positive_prefix), $($Job.prompt)"
    $negative = [string]$Batch.global_negative
    $savedUtc = (Get-Date).ToUniversalTime().ToString("o")

    $request = [ordered]@{
        schema = "lit_iso.asset_forge.generation_request.v1"
        generated_utc = $savedUtc
        saved_utc = $savedUtc
        pack_name = $Batch.batch_id
        job_name = $jobName
        asset_mode = $assetMode
        provider = $Provider
        status = "queued_from_training_batch"
        prompt = $prompt
        negative_prompt = $negative
        user_prompt = [string]$Job.prompt
        user_negative_prompt = ""
        reference_image = ""
        reference_image_url = ""
        seed = "random"
        directions = [string]$Job.directions
        canonical_direction_order = @("S", "SE", "E", "NE", "N", "NW", "W", "SW")
        animation = [ordered]@{ name = [string]$Job.animation; frame_count = 1; fps = 0; loop = $false; clips = @() }
        clips = @()
        batch_count = $batchCount
        asset_spec = [ordered]@{
            subtype = [string]$Job.subtype
            biome = [string]$Job.biome
            variant = "training_seed"
            footprint = if ($assetMode -eq "tile") { "diamond_1x1" } elseif ($assetMode -eq "item") { "icon" } else { "1x1" }
            background_policy = "transparent"
            shadow_policy = "none"
            palette_policy = "reference_locked"
            quality_gate = "strict"
            tile_overlay_policy = if ($assetMode -eq "tile") { "terrain_only_no_props_or_trees" } else { "not_applicable" }
            target_positive_count = $targetCount
        }
        canvas = [ordered]@{ width = $canvasSize; height = $canvasSize; cell_size = $canvasSize; ppu = $ppu; pivot = $pivot; anchor = $anchor }
        output_intent = [ordered]@{
            review_folder = "Assets/Generated/_Review/$jobName"
            unity_category = if ($assetMode -eq "tile") { "Tiles" } elseif ($assetMode -eq "prop") { "Props" } elseif ($assetMode -eq "item") { "Items" } elseif ($assetMode -eq "mob") { "Mobs" } elseif ($assetMode -eq "npc") { "NPCs" } else { "Characters" }
            transparent_background = $true
            pixel_perfect = $true
            point_filtering = $true
            no_mipmaps = $true
            bottom_center_anchor = -not ($assetMode -eq "tile" -or $assetMode -eq "item")
        }
        post_process = @("background_remove", "sprite_fusion_snap", "palette_cap", "nearest_neighbor_resize", "fixed_canvas_normalize", "anchor_lock", "qa_report")
        acceptance_checks = @("transparent_png", "pixel_perfect_edges", "manifest_ready_for_unity", "strict_qa_report")
        training_batch = [ordered]@{
            batch_id = [string]$Batch.batch_id
            style_profile_id = [string]$Batch.style_profile_id
            provider_priority = @($Batch.provider_priority)
            target_count = $targetCount
            queued_count = $batchCount
        }
        clean_room_note = "Original LIT-ISO training seed request. Generated outputs must pass review before approval/training."
    }
    return $request
}

$projectRootResolved = (Resolve-Path -LiteralPath $ProjectRoot).Path
$batchFull = (Resolve-Path -LiteralPath $BatchPath).Path
$batch = Get-Content -Raw -LiteralPath $batchFull | ConvertFrom-Json
$requestParent = Join-Path $ProjectRoot "Assets\Generated\_Review\_Requests"
Assert-UnderRoot -Root $projectRootResolved -Path $requestParent -Label "RequestParent"
New-Item -ItemType Directory -Force -Path $requestParent | Out-Null

$created = @()
foreach ($job in @($batch.jobs)) {
    $request = New-Request -Batch $batch -Job $job
    $jobName = [string]$request.job_name
    $requestRoot = Join-Path $requestParent $jobName
    Assert-UnderRoot -Root $projectRootResolved -Path $requestRoot -Label "RequestRoot"
    if ((Test-Path $requestRoot) -and -not $ReplaceExisting.IsPresent) {
        $created += [ordered]@{ job_name = $jobName; status = "skipped_exists"; request_root = Convert-ToRepoPath $requestRoot }
        continue
    }
    if (Test-Path $requestRoot) { Remove-Item -LiteralPath $requestRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Join-Path $requestRoot "Inputs"), (Join-Path $requestRoot "Outputs"), (Join-Path $requestRoot "Review") | Out-Null

    $requestPath = Join-Path $requestRoot "generation_request.json"
    $workerPath = Join-Path $requestRoot "worker_queue_item.json"
    $statusPath = Join-Path $requestRoot "request_status.json"
    $request | ConvertTo-Json -Depth 24 | Set-Content -LiteralPath $requestPath -Encoding UTF8
    [ordered]@{
        schema = "lit_iso.asset_forge.worker_queue_item.v1"
        status = "queued"
        saved_utc = (Get-Date).ToUniversalTime().ToString("o")
        job_name = $jobName
        asset_mode = [string]$request.asset_mode
        provider = $Provider
        request_path = Convert-ToRepoPath $requestPath
        request_root = Convert-ToRepoPath $requestRoot
        intended_review_pack_root = "Assets/Generated/_Review/$jobName"
        source_batch = Convert-ToRepoPath $batchFull
    } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $workerPath -Encoding UTF8
    [ordered]@{
        ok = $true
        status = "queued"
        saved_utc = (Get-Date).ToUniversalTime().ToString("o")
        job_name = $jobName
        asset_mode = [string]$request.asset_mode
        provider = $Provider
        source_batch = Convert-ToRepoPath $batchFull
        next_step = if ($Provider -eq "sprixen") { "Run process_generation_request_sprixen.ps1 -JobName $jobName -DryRun first." } else { "Run the matching Asset Forge provider worker." }
    } | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $statusPath -Encoding UTF8
    Set-Content -LiteralPath (Join-Path $requestRoot "README.md") -Encoding UTF8 -Value "# $jobName`n`nQueued from training batch $($batch.batch_id). Review and dry-run before spending provider credits."
    $created += [ordered]@{ job_name = $jobName; status = "queued"; request_path = Convert-ToRepoPath $requestPath; provider = $Provider; batch_count = [int]$request.batch_count; target_count = [int]$request.training_batch.target_count }
}

[ordered]@{
    ok = $true
    batch_id = [string]$batch.batch_id
    provider = $Provider
    max_variants_per_job = $MaxVariantsPerJob
    created_count = @($created | Where-Object { $_.status -eq "queued" }).Count
    skipped_count = @($created | Where-Object { $_.status -ne "queued" }).Count
    requests = $created
} | ConvertTo-Json -Depth 12

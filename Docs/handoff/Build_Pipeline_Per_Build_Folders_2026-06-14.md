# Build Pipeline: Per-Build Folders + Failure Detection — 2026-06-14

## What prompted this

Owner reported the built .exe still showed the **old menu background**
(`CampfireMenu.png`) after running `Tools/BuildGame.bat`, even though
`Assets/Resources/UI/Menu/background.png` (the new splash, loaded preferentially
by `WelcomeScreenManager.LoadSkin()`) was already in place and the loading code
was correct.

## Root cause

The build never actually ran. `Build/build_log.txt` showed Unity printing only
its license-handshake banner, the command-line args, and:

```
Successfully changed project path to: C:\Projects\Unity-Projects\LIT-ISO
C:/Projects/Unity-Projects/LIT-ISO
Exiting without the bug reporter. Application will terminate with return code 1
```

— i.e. Unity exited immediately, before any asset refresh/compile/build step,
so `BuildScript.BuildWindows` never ran and `build_info.txt` was never written.
`Build/LIT-ISO/LIT-ISO.exe` was a **stale exe from 2026-06-11**, ~22h older than
`background.png`. The old `.bat` only checked "does
`Build\LIT-ISO\LIT-ISO.exe` exist" — since the stale exe was still there from a
prior successful build, it reported success and the shortcut kept pointing at
the old build, which still had the old background baked in.

`Temp/UnityLockfile` exists in the project right now, which strongly suggests
the Unity Editor was open on this project when `BuildGame.bat` ran — a second
batchmode instance on a locked project exits almost instantly with no useful
log, matching exactly what was observed.

## Fixes (this session)

### 1. `Assets/Scripts/Editor/GameBuilder.cs`
- Every build now goes to its own timestamped folder:
  `Build/LIT-ISO_yyyy-MM-dd_HHmmss/LIT-ISO/LIT-ISO.exe` (owner request: "it
  should make new folders for each build"). `_buildStamp` is set once at the
  top of `RunBuildPipeline` and used by `BuildOutputFolder`/`BuildExePath`/
  `WriteBuildInfo` for the rest of that run.
- `FindLatestBuildFolder()` — new helper, finds the newest
  `Build/LIT-ISO_*` folder by creation time.
- `OpenBuildsFolder` now opens the latest timestamped build folder (falls back
  to `Build/` root if none exist yet).
- `CleanBuildsFolder` now deletes the entire `Build/` root (all timestamped
  builds), with an updated confirmation dialog.

### 2. `Tools/BuildGame.bat`
- Pre-flight check: if `Temp\UnityLockfile` exists, prints a loud warning to
  close the Unity Editor before continuing (doesn't block, since the lockfile
  can also be a harmless leftover, but flags the likely cause up front).
- Records the latest `Build\LIT-ISO_*` folder **before** invoking Unity, then
  again **after**. If no such folder exists at all, or the "latest" folder
  didn't change (i.e. Unity didn't produce a new one), the script now reports
  **BUILD FAILED** with a pointer to `build_log.txt` and the lockfile note —
  instead of silently reusing whatever old exe happened to exist.
- Desktop shortcut and the optional "Launch now" prompt now always point at
  the newest `Build\LIT-ISO_<timestamp>\LIT-ISO\LIT-ISO.exe`.

## Owner action needed

1. **Close the Unity Editor** (the open instance is almost certainly why the
   last run produced nothing).
2. Run `Tools/BuildGame.bat` again. It should now either:
   - Succeed, producing `Build\LIT-ISO_<timestamp>\LIT-ISO\LIT-ISO.exe` with
     the new menu background and a `build_info.txt`, or
   - Fail loudly with a clear reason (no stale-success false positive anymore).
3. Old folders `Build\LIT-ISO\` (stale, pre-timestamp) and `Builds\` (plural,
   dead from an earlier drift) can be deleted any time — neither is referenced
   by the new pipeline.

## Not done / open
- Could not actually run a Unity batchmode build in this sandbox (no
  Unity/dotnet available) — the fix above is based on static review + the
  `build_log.txt`/timestamp evidence above, not an end-to-end test.
- Did not add automatic pruning of old timestamped build folders — they'll
  accumulate under `Build\` until `Clean Builds Folder` (now deletes all of
  them) is run manually.

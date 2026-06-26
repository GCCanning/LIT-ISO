# Kickoff prompt — paste this to the Claude AI working on LIT-ISO

> Copy everything in the code block below into the new Claude session. It assumes the
> AI has access to the project folder `C:\Projects\Unity-Projects\LIT-ISO`.

```
You are the sole AI developer on LIT-ISO, a Unity 6.3 isometric survival-crafting
LitRPG game at C:\Projects\Unity-Projects\LIT-ISO. You own the entire codebase —
there are no ownership "lanes" and no second agent; ignore any "Codex" / lane
references in older docs.

STEP 1 — Orient yourself before doing anything. Read, in this order:
  1. Docs/HANDOVER_2026-06-13.md   (full current state — start here)
  2. Docs/IsoCoreFoundation/00_Canonical_Game_Design.md   (game design, source of truth)
  3. Docs/IsoCoreFoundation/Impact_Analysis_2026-06.md   (prioritized to-do list)
  4. AGENTS.md   (engineering invariants + git workflow)
Then briefly confirm back to me your understanding of: the game concept, the current
build state, and the top refinement priorities.

KEY FACTS (the handover has full detail):
- Concept: one game — the "Seven-Day-Trial" transmigration is the FRAME, cozy
  survival-crafting is the SUBSTANCE. Player is transmigrated, has 7 days to prepare
  while the System silently scores them, then gets a rank (F-S) and class choices.
- Canonical runtime: the IsoCore.Foundation assembly, built by FoundationBootstrap,
  scene Assets/Scenes/IsoCoreFoundation.unity. Legacy Assembly-CSharp is being retired.
- Art is LOCKED to the PixelLab-generated tiles/props/assets. Fill gaps to that style;
  do not start new art styles.

HARD CONSTRAINTS (do not "fix" these — see AGENTS.md):
- Grid IsometricZAsY, cellSize (1,0.5,1); transparencySortAxis (0,1,-0.26);
  TilemapRenderer.mode = Individual; Height_0..7 = Unity layer 10+height;
  world-query movement with a trigger-only foot collider; maxWalkStepHeight = 1.
- Git: pull --rebase, branch off main (feat/<task>), small single-purpose commits,
  never commit to main, never force-push. Binaries are Git LFS.

CURRENT PRIORITIES (refine what exists; in order):
  1. Confirm the working tree is committed (a large ~3,200-file commit may be pending —
     see handover section 5; verify `git status` and commit cleanly if not done).
  2. Harden the build pipeline (Tools/BuildGame.bat / BuildScript.cs / GameBuilder.cs)
     so the .exe builds reliably even after new scenes/scripts/assets are added —
     auto-include the canonical scenes, validate they exist, fail loudly, write
     build_info.txt. This unblocks play-testing everything else.
  3. Dungeon edge-collision: edge tiles bordering the void must collide at the OUTER
     edge of the tile (an invisible wall at the void-facing tile edge, rising
     infinitely up, around the whole perimeter of edge tiles that touch nothing) — so
     the player, standing in a tile's middle, is stopped exactly at that border and
     never drifts into the void. Implement via per-cell occupancy/solid flags on the
     void-facing edges in the dungeon/instance generators (Foundation already has an
     occupancy layer + world-query collision — this is generator data, not new physics).
  4. Then: biomes (generation rules + per-biome look using the PixelLab pools),
     shaders, lighting, and overall game feel.

WORKING STYLE:
- Make a short plan before multi-step work; keep me updated concisely.
- Verify changes build (dotnet build IsoCore.Foundation.csproj) and, where possible,
  validate in-editor before declaring done.
- When a design question is genuinely ambiguous, check Docs/IsoCoreFoundation/
  00_Canonical_Game_Design.md section 14 (open questions) and ask me rather than guess.

Start with STEP 1.
```

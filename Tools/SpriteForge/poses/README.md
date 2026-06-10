# Action Pose Library

The consistency backbone (SPEC §2). Every action is generated against these
canned OpenPose skeleton sequences, all anchored on one shared idle pose.

- `VERSION` — bump on any pose change; jobs record the version they used.
- `<action>/action.json` — see `_schema/action.schema.json` and the walk
  example. Fields: frames, fps, loop, anchor_frame, weapon_hand, engine
  default, per-direction overrides.
- `<action>/<direction>/frame_###.png` — OpenPose skeleton render, 512x512,
  transparent background. Directions: S, SE, E, NE, N, NW, W, SW.
- Derivation: start from `Tools/AssetForge/build_litiso_openpose_direction_library.py`
  and the LPC/OGA oracle builders (skeletons only — never oracle pixels).

P1 ships the first versioned subset:

- `idle` — 4 frames, 8 directions, frame 0 = shared anchor.
- `walk` — 6 frames, 8 directions, frame 0 = byte-identical to the matching
  `idle/<direction>/frame_000.png` anchor.

P1 gate artifacts:

- `idle/idle_pose_contact_sheet.png`
- `walk/walk_pose_contact_sheet.png`
- `pose_library_manifest.json`
- `p1_gate_report.json`

Future v1 actions (priority order): run(6f), attack_swing(6f), cast(6f),
hurt(3f), death(5f). All must start from the idle anchor frame.

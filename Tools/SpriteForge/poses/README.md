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

v1 actions (priority order): idle(4f), walk(6f), run(6f), attack_swing(6f),
cast(6f), hurt(3f), death(5f). All start from the idle anchor frame.

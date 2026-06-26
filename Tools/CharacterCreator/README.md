# LIT-ISO Character Creator Tooling

This folder is the review-side bridge for the in-game character creator.

Current slice:

- `Assets/StreamingAssets/character_creator/appearance_catalog.json` defines the creator tabs, runtime sheet contract, item slots, and initial appearance options.
- `build_character_creator_suite.py` builds a review contact sheet and manifest from the current `Resources/Characters/Player` sheets.

Run:

```powershell
python Tools\CharacterCreator\build_character_creator_suite.py
```

Output:

```text
Assets/Generated/_Review/character_creator_suite_v1/
```

The current appearances are marked `prototypeOnly`. They prove the runtime and UI contract; final shipping doll parts still need original production art approval.

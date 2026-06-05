# LPC Character System - LIT-ISO

Layered pixel art character system built on the Universal LPC (Liberated Pixel Cup) sprite library.
4-directional sprites (N/W/S/E) with 13 supported animations.
Diagonal movement snaps to the nearest cardinal direction (standard pro practice - same as Stardew Valley, Hyper Light Drifter).

## License

All LPC sprites are CC-BY-SA 3.0 / GPL 3.0 / OGA-BY 3.0.
**You must include attribution** to all original LPC artists if you ship a game using these assets.
See `Assets/LPC/Sprites/CREDITS.csv` (to be copied from the LPC repo) for the full author list.

## Folder Structure

```
Assets/LPC/
├── Scripts/
│   ├── LPCEnums.cs              -- Direction, Animation, Layer enums
│   ├── LPCAnimationData.cs      -- Sheet layout metadata (row offsets, FPS)
│   ├── LPCSpriteSheet.cs        -- ScriptableObject wrapping a PNG sheet
│   ├── LPCSpriteSlicer.cs       -- Runtime PNG -> Sprite slicing
│   ├── LPCCharacter.cs          -- MonoBehaviour, layered renderer
│   ├── LPCAnimator.cs           -- Animation playback + 8->4 dir snap
│   ├── LPCImporter.cs           -- Editor utility (LIT-ISO menu)
│   └── LPCDemoController.cs     -- WASD test controller
├── Sprites/                     -- Source PNG sheets (organized by slot)
│   ├── body/   hair/   torso/   legs/   feet/   etc.
├── Data/                        -- Generated LPCSpriteSheet assets
└── Prefabs/                     -- Reusable character prefabs
```

## Quick Start (Test the System)

1. **Open Unity** and let it import the new scripts.

2. **Import sprite sheets**: Menu `LIT-ISO -> LPC -> Import Sheets from Folder`
   This scans `Assets/LPC/Sprites/` and creates LPCSpriteSheet assets in `Assets/LPC/Data/`.
   It also configures each PNG with pixel-art import settings (Point filter, no compression).

3. **Create a character in a scene**:
   - Create an empty GameObject, name it "Knight"
   - Add component: `LPCCharacter`
   - Add component: `LPCAnimator`
   - In LPCCharacter inspector, drag sheet assets into the slots:
     - `Body Sheet` -> body_male_light
     - `Hair Sheet` -> hair_plain_black
     - `Torso Sheet` -> torso_chainmail_gray
     - `Legs Sheet` -> legs_plate_brass
     - `Feet Sheet` -> feet_boots_black
   - Add component: `LPCDemoController`

4. **Press Play**. Use WASD to walk around, Space to slash, Q to spellcast.

## How It Works

### Sheet Format

Each LPC PNG is 64x64 pixels per frame, arranged as:
- Each animation block = 4 rows (one per direction N/W/S/E)
- Animations are stacked vertically in this order:
  Spellcast, Thrust, Walk, Slash, Shoot, Hurt, Idle, Run, Jump, Sit, Emote, Climb, Combat
- Sheet width: 832 px (13 frames max per row)

### Layered Rendering

`LPCCharacter` creates one child `SpriteRenderer` per equipped slot.
Each renderer has its `sortingOrder` set to that slot's `LPCLayer` value, so:
- Body (10) renders below
- Torso (30) renders above body
- Head/helmet (20) renders above hair
- Weapon (70) renders on top
(See `LPCEnums.cs` for the full z-order.)

### Animation Sync

`LPCAnimator` ticks once per frame. On each frame change, it calls
`character.SetFrame(animation, direction, frame)`. The character then updates
EVERY child SpriteRenderer to the same animation+direction+frame in their
respective sheet. This keeps all layers locked in sync.

### Diagonal Movement -> 4D Sprites

`LPCAnimator.FaceMovement(Vector2)` converts an 8-direction movement vector
into one of the 4 cardinal sprite directions:
- Movement angle bucketed into 90deg ranges
- E.g. NE movement -> displays East-facing sprite

This is industry standard and looks natural in motion.

## Adding More Sprites

The LPC repo has 24,000+ PNG files. Only a starter set was copied here.
To add more:

1. Copy desired PNGs into the appropriate `Assets/LPC/Sprites/<slot>/` folder
2. Run `LIT-ISO -> LPC -> Import Sheets from Folder` again
3. Drag the new sheet asset into a character's equipment slot

## Equipment Swap at Runtime

```csharp
var character = GetComponent<LPCCharacter>();
var newArmor = Resources.Load<LPCSpriteSheet>("LPC/Data/torso_chainmail_gold");
character.SetEquipment(LPCLayer.Torso, newArmor);
```

## Source

Universal LPC Spritesheet Character Generator:
https://github.com/sanderfrenken/Universal-LPC-Spritesheet-Character-Generator

Browser-based composer (great for building reference characters):
https://sanderfrenken.github.io/Universal-LPC-Spritesheet-Character-Generator/

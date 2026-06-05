# Player Sprite Animation Specification

**Game:** LIT-ISO Isometric  
**Grid Layout:** IsometricZAsY  
**Directions:** 8-way (N, NE, E, SE, S, SW, W, NW)  
**Animation Types:** Idle, Walk

---

## 📐 SPRITE SHEET FORMAT

### **Idle Animation Sprite Sheet**
```
LAYOUT: 8 columns × 2 rows
SIZE: 256×64 pixels (8 frames of 32×32 each)
or: 512×128 pixels (8 frames of 64×64 each) — recommended for quality

Frame layout (left to right):
[N] [NE] [E] [SE] [S] [SW] [W] [NW]

Row 1: Frame A (first idle pose)
Row 2: Frame B (second idle pose - blink, breath, etc.)
```

### **Walk Animation Sprite Sheet**
```
LAYOUT: 8 columns × 2 rows
SIZE: 256×64 pixels (8 frames of 32×32 each)
or: 512×128 pixels (8 frames of 64×64 each) — recommended

Frame layout (left to right):
[N] [NE] [E] [SE] [S] [SW] [W] [NW]

Row 1: Walk frame A (leg forward)
Row 2: Walk frame B (leg back)
```

---

## 🎭 DIRECTION MAPPING

The 8 directions map to isometric blend tree positions:

```
        N (0, 1)
       ↗   ↑   ↖
     NW    |    NE
  (-0.707, 0.707)  (0.707, 0.707)
       ↖   |   ↗
W(-1,0)----+----E(1,0)
       ↙   |   ↘
  (-0.707,-0.707)  (0.707,-0.707)
     SW    |    SE
       ↙   ↓   ↘
      S (0, -1)
```

### **Visual Direction** (how character faces in-game):

| Direction | Blend Tree | Visual | Keys |
|-----------|-----------|--------|------|
| **N** | (0, 1) | Facing UP-LEFT (NW iso) | W |
| **NE** | (0.707, 0.707) | Facing UP-RIGHT (NE iso) | W+D |
| **E** | (1, 0) | Facing RIGHT (E iso) | D |
| **SE** | (0.707, -0.707) | Facing DOWN-RIGHT (SE iso) | D+S |
| **S** | (0, -1) | Facing DOWN (SE iso) | S |
| **SW** | (-0.707, -0.707) | Facing DOWN-LEFT (SW iso) | S+A |
| **W** | (-1, 0) | Facing LEFT (W iso) | A |
| **NW** | (-0.707, 0.707) | Facing UP-LEFT (NW iso) | A+W |

---

## 👤 CHARACTER REQUIREMENTS

### **Idle Animation**
- **Duration:** 1-2 seconds per loop
- **Frames:** 2 frames minimum (can be same frame twice for static idle)
- **Features:**
  - Neutral standing pose
  - Optional: slight breathing, weight shift, or blink
  - All 8 directions should show character facing that direction

### **Walk Animation**
- **Duration:** 0.5 seconds per loop (comfortable walking pace)
- **Frames:** 2 frames (leg positions)
- **Features:**
  - Frame A: one leg forward, one back
  - Frame B: opposite leg forward
  - Smooth 2-frame walk cycle (legs swinging)
  - All 8 directions

---

## 📐 PIXEL DIMENSIONS

### **Option 1: Small (32×32 per frame)**
```
32×32 pixels per character
Good for: Fast prototyping, low-poly aesthetic
File size: ~10-20KB per spritesheet

Idle: 256×64 px (8 frames wide, 2 frames tall)
Walk: 256×64 px (8 frames wide, 2 frames tall)
```

### **Option 2: Medium (64×64 per frame)** ⭐ RECOMMENDED
```
64×64 pixels per character
Good for: Balanced quality, visible detail
File size: ~40-80KB per spritesheet

Idle: 512×128 px (8 frames wide, 2 frames tall)
Walk: 512×128 px (8 frames wide, 2 frames tall)
```

### **Option 3: High (128×128 per frame)**
```
128×128 pixels per character
Good for: Maximum quality, high detail
File size: ~160-320KB per spritesheet

Idle: 1024×256 px (8 frames wide, 2 frames tall)
Walk: 1024×256 px (8 frames wide, 2 frames tall)
```

---

## 🎨 ISOMETRIC PERSPECTIVE GUIDE

For isometric games, characters should be drawn with a specific perspective:

### **Character Face/Front**
- Head at top-center
- Body center-aligned
- Legs/feet at bottom
- Shadow underneath (optional)

### **Direction-Specific Details**

```
N (facing NW iso):          E (facing E iso):
    Head                       Head
    /|\                       /|\
   / | \      ↔             / | \
  /  |  \                  /  |  \
   \ | /                    \ | /
    \|/                      \|/

Character appears to face "towards camera-up-left"
```

**Key Rule:** In isometric view:
- N/NE/E sprites show front-right shoulder
- S/SW/W sprites show front-left shoulder  
- Helps create depth and immersion

---

## 📦 INTEGRATION INTO GAME

### **How PlayerSetupWindow Uses These**

The `PlayerSetupWindow.cs` expects:

```csharp
// You'll select:
idleSpriteSheet  → Your 256×64 (or 512×128) idle sheet
walkSpriteSheet  → Your 256×64 (or 512×128) walk sheet

// Click "Setup Everything" which:
1. Slices sheet into 16 sprites (8 directions × 2 frames)
2. Creates 8 idle animation clips (one per direction)
3. Creates 8 walk animation clips (one per direction)
4. Builds Animator with 2D blend tree
5. Wires to IsoCharacterAnimator component
```

---

## 🎬 ANIMATION CLIP DETAILS

### **Generated Idle Clips**
- **File:** `Assets/Characters/Player/Animations/Player_Idle_[N/NE/E/SE/S/SW/W/NW].anim`
- **Duration:** ~1 second (2 frames at 2 FPS)
- **Looping:** Yes
- **Blend Tree Position:** Corresponding direction

### **Generated Walk Clips**
- **File:** `Assets/Characters/Player/Animations/Player_Walk_[N/NE/E/SE/S/SW/W/NW].anim`
- **Duration:** ~0.25 seconds (2 frames at 8 FPS)
- **Looping:** Yes
- **Blend Tree Position:** Corresponding direction

---

## ✅ CHECKLIST FOR CREATING SPRITES

- [ ] Create/source 8-directional idle animation (16 frames total: 8 directions × 2 frames)
- [ ] Create/source 8-directional walk animation (16 frames total: 8 directions × 2 frames)
- [ ] Arrange into 256×64 (or larger) sprite sheets
- [ ] Ensure all frames are 32×32 (or 64×64, etc.) consistently
- [ ] Character centered in each frame
- [ ] Transparent background (PNG)
- [ ] Point filter mode (crisp pixels, no smoothing)
- [ ] Place sheets in project:
  - `Assets/Resources/Characters/Player/AnimationSprites/idle.png`
  - `Assets/Resources/Characters/Player/AnimationSprites/walk.png`
- [ ] Run `Tools > Iso World > Player Setup Wizard`
- [ ] Select both sheets
- [ ] Click "Setup Everything"
- [ ] Press Play and test!

---

## 🚀 OPTIONS FOR GETTING SPRITES

### **1. Generate with Sprixen AI** (Fastest)
We can use the Sprixen platform to auto-generate:
- A player character sprite
- Idle animations for 8 directions
- Walk animations for 8 directions

Cost: ~5 credits per sprite generation

### **2. Find Existing Packs**
Look for:
- OpenGameArt.org - free 2D sprite packs
- Itch.io - indie game assets
- Kenney.nl - free game assets
- AssetStore - paid professional sprites

### **3. Commission Artist**
- Fiverr, Upwork - custom pixel art
- Game dev communities - indie artists
- Local artists - support local talent

### **4. Create Yourself**
- Aseprite - professional pixel art tool
- Piskel - free online sprite editor
- LibreSprite - free Aseprite fork

---

## 🔧 CURRENT GAME STATE

**What you have:**
- ✅ Isometric grid system (IsometricZAsY)
- ✅ 8-directional movement system
- ✅ Animator with 2D blend tree setup
- ✅ PlayerSetupWindow to auto-slice & generate animations

**What you need:**
- ❌ Idle sprite sheet (256×64 minimum)
- ❌ Walk sprite sheet (256×64 minimum)

**Time to implement:**
- If you have sprites: ~5 minutes (just run PlayerSetupWindow)
- If you need to create: 2-8 hours (depends on method)
- If you want to generate with AI: ~15 minutes

---

## 📋 EXAMPLE SPRITE LAYOUT

Here's what a 512×128 idle sheet looks like:

```
IDLE SPRITESHEET (512×128)
┌─────────────────────────────────────────────────────────────────┐
│ N     │ NE    │ E     │ SE    │ S     │ SW    │ W     │ NW    │
│ ↑     │ ↗     │ →     │ ↘     │ ↓     │ ↙     │ ←     │ ↖     │
│ pose1 │ pose1 │ pose1 │ pose1 │ pose1 │ pose1 │ pose1 │ pose1 │
├─────────────────────────────────────────────────────────────────┤
│ N     │ NE    │ E     │ SE    │ S     │ SW    │ W     │ NW    │
│ ↑     │ ↗     │ →     │ ↘     │ ↓     │ ↙     │ ←     │ ↖     │
│ pose2 │ pose2 │ pose2 │ pose2 │ pose2 │ pose2 │ pose2 │ pose2 │
│       │       │       │       │       │       │       │       │
└─────────────────────────────────────────────────────────────────┘

Each cell: 64×64 pixels
```

---

**Next Steps:**

1. **Decide on sprite source** (generate, commission, find existing)
2. **Prepare sprite sheets** in the correct format
3. **Place in project folder**
4. **Run PlayerSetupWindow tool**
5. **Test in-game!**

Would you like me to help generate sprites using Sprixen AI?

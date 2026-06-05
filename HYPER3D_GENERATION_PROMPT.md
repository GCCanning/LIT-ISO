# Hyper3D.ai Generation Prompt & Specifications

**Website:** https://hyper3d.ai/video  
**Purpose:** Generate 3D isometric character with idle animation (8 directions)

---

## 📝 MAIN PROMPT FOR HYPER3D.AI

Copy and paste this into the Hyper3D.ai generator:

---

### **IDLE ANIMATION PROMPT**

```
Generate a fantasy witch character in an isometric RPG style. 
The character should be standing idle in a neutral pose, looking slightly to the front-right. 
She is wearing a purple/dark robe with magical accents. 
Create a subtle idle animation showing gentle breathing or a slight weight shift.

The character should:
- Be centered in the frame
- Have a clear silhouette
- Show no movement other than subtle breathing/idle movement
- Be suitable for a top-down isometric perspective
- Have a cohesive fantasy RPG aesthetic
- Include proper shadow beneath the character

Idle animation: Gentle breathing with slight weight shift, no walking or action
Duration: 2-3 seconds looping
Style: 2D sprite-like appearance, pixel art quality
Perspective: Isometric view, character facing slightly toward camera-upper-left
```

---

## 🎬 TECHNICAL SPECIFICATIONS

### **Output Format**
- **Format:** MP4 or WebM video
- **Resolution:** 512×512 pixels minimum (1024×1024 recommended for quality)
- **Background:** Transparent (or solid color for reference)
- **Duration:** 2-3 seconds (looping)
- **FPS:** 24-30 FPS
- **Codec:** H.264 or VP9

### **Animation Specifications**
- **Type:** Idle/Standing animation
- **Movement:** Subtle (breathing, weight shift, blink)
- **Loop:** Should loop smoothly
- **Camera:** Isometric angle, 45 degrees from top
- **Character Position:** Centered, fills ~60% of frame

### **Character Specifications**
- **Style:** Fantasy RPG, pixel art-inspired
- **Clothing:** Purple/dark robe, magical theme
- **Pose:** Standing neutral, facing forward-right
- **Silhouette:** Clear and defined
- **Accessories:** Hat/staff optional but welcome

---

## 🔄 8-DIRECTION WORKFLOW (After Getting Base Model)

Since Hyper3D generates from one angle, here's how to create all 8 directions:

### **Direction 1: Generated (N - Facing Up-Left)**
- Use as-is from Hyper3D
- This is your North direction

### **Directions 2-8: Mirror/Rotate in Post**
For an isometric game, you only need 4 unique angles:
```
Generated view (N) ────→ Mirror for (W)
                   ↓
                Creates 4 unique angles:
                - N (0°) - Use direct output
                - NE/E (45°) - Slight rotation right
                - S (180°) - Full rotation/mirror
                - SW/W (225°) - Slight rotation left

Then in engine:
- N direction uses original
- NE uses slight right angle
- E uses slight right angle (or mirror)
- SE uses rotated view
- S uses 180° rotation
- SW uses rotated left
- W uses mirrored
- NW uses slight left rotation
```

---

## 📊 EXPECTED OUTPUT

What you'll get from Hyper3D:
- ✅ 3D model rendered as 2D video
- ✅ Idle animation (breathing, weight shift)
- ✅ Transparent background (if available)
- ✅ Isometric perspective
- ✅ ~2-3 second looping animation

---

## 🛠️ NEXT STEPS AFTER GENERATION

1. **Download the video** from Hyper3D.ai
2. **Extract frames** using FFmpeg:
   ```bash
   ffmpeg -i idle_animation.mp4 -vf fps=12 frame_%04d.png
   ```
3. **Arrange into sprite sheet** (see PLAYER_SPRITE_SPEC.md for layout)
4. **Create 8 copies** for each direction (N, NE, E, SE, S, SW, W, NW)
5. **Use PlayerSetupWindow** to auto-slice and generate animations
6. **Test in-game!**

---

## 💡 ALTERNATIVE PROMPTS (If you want variations)

### **For a different character style:**

#### Brave Knight:
```
Generate a brave knight character in idle animation for an isometric RPG.
The knight wears full armor with a sword, standing in a neutral ready pose.
Show subtle idle animation with gentle weight shift or breathing.
Isometric perspective, pixel art style, fantasy RPG aesthetic.
Duration: 2-3 seconds looping. Character facing forward-right.
```

#### Rogue Assassin:
```
Generate a stealthy rogue character in idle animation for an isometric RPG.
The rogue wears leather armor and carries daggers, in a relaxed stance.
Show subtle idle animation with weight shift or looking around.
Isometric perspective, pixel art style, fantasy RPG aesthetic.
Duration: 2-3 seconds looping. Character facing forward-right.
```

#### Wise Mage:
```
Generate a wise mage character in idle animation for an isometric RPG.
The mage wears robes with magical aura, holding a staff, in a composed stance.
Show subtle idle animation with magical sparkles or weight shift.
Isometric perspective, pixel art style, fantasy RPG aesthetic.
Duration: 2-3 seconds looping. Character facing forward-right.
```

---

## 📋 QUALITY CHECKLIST

When you get the output, verify:
- [ ] Character is centered
- [ ] Background is transparent (or removable)
- [ ] Animation loops smoothly (no jump at end)
- [ ] Idle movement is subtle (not excessive)
- [ ] Character is clearly visible (good contrast)
- [ ] Isometric angle is correct (45° top-down view)
- [ ] Resolution is at least 512×512
- [ ] Frame rate is 24+ FPS
- [ ] Duration is 2-3 seconds

---

## ⚙️ INTEGRATION WITH YOUR GAME

Once you have the sprite sheet from extracted frames:

1. **Place file:**
   ```
   Assets/Resources/Characters/Player/AnimationSprites/idle.png
   ```

2. **Run tool:**
   ```
   Tools > Iso World > Player Setup Wizard
   ```

3. **Select idle sheet** and configure:
   - PPU: 32 (or your preference)
   - Idle FPS: 5-6 (for smooth animation)
   - Click "Setup Everything"

4. **Play and test!**

---

## 🎯 QUICK START STEPS

1. Go to https://hyper3d.ai/video
2. Paste the **IDLE ANIMATION PROMPT** above
3. Click Generate
4. Wait for render (~2-5 minutes)
5. Download the video (MP4)
6. Extract frames using FFmpeg (see above)
7. Arrange into sprite sheet format
8. Use PlayerSetupWindow to integrate
9. Test in-game!

---

## 💾 FILE NAMING

When you save the output:
```
idle_animation_hyper3d.mp4          (original render)
idle_frames/                        (extracted frames)
  frame_0001.png
  frame_0002.png
  ... (24-30 frames)
  frame_00XX.png

idle_spritesheet_512x128.png        (final sprite sheet for game)
```

---

**Total Time:** ~20 minutes (generation + frame extraction + sheet assembly)  
**Cost:** Free (Hyper3D has free tier)

Ready to generate? Go to: **https://hyper3d.ai/video** and paste the prompt above! 🚀

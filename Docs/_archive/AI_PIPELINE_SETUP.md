# Scenario AI Generation Pipeline — Setup Guide

This pipeline lets us design prompts, generate tiles/assets/props/characters via the Scenario API, review them in Unity, and organize them into the project structure.

---

## 🏗️ Architecture Overview

```
   Prompt design (PromptLibrary.md)
              ↓
   Scenario Text-to-Image  ──→  2D concept image (PNG)
              ↓
   [optional] Pixal3D       ──→  3D model (.glb)
              ↓
   Review in Unity Editor
              ↓
   Approve → Final folder (Assets/Generated/[Type]/[Biome]/)
              ↓
   Import as Unity asset (sprite / tile / prefab)
```

---

## 🔧 First-Time Setup (5 min)

### 1. Get a Scenario API key

1. Go to https://app.scenario.com
2. Create an account if you don't have one
3. Settings → **API Keys** → "Create New Key"
4. Copy both the **Key** and **Secret** (you only see the secret once!)

### 2. Configure Unity

```
Tools > LIT-ISO > AI Generation > Configure Scenario API
```

- Paste your **API Key**
- Paste your **API Secret** (use "Show" toggle to verify)
- Leave Text-to-Image Model ID empty for now (auto-discover in step 3)
- Pixal3D Model ID: `model_pixal3d` (default is correct)
- Output Root: `Assets/Generated` (default)
- Click **Test Connection** — should show ✅
- Click **Save**

### 3. Discover Your Models

```
Tools > LIT-ISO > AI Generation > Generation Window
```

- Click **Refresh Models** button
- You'll see a list of all models in your Scenario account
- Find a good model for **isometric character art** (e.g. one trained on iso characters or fantasy RPGs)
- Click "Use as text-to-image" next to it — sets it as your default

---

## 🎨 Generating Your First Asset (3 min)

### Step 1: Open the prompt library
Open `Assets/Generated/_Prompts/PromptLibrary.md` and review the **Male_Adventurer_Base** prompt. Edit if needed.

### Step 2: Configure the generation

In the Generation Window:
- **Category**: `CharacterBase`
- **Variant Name**: `Male_Adventurer`
- **Biome / Sub-folder**: (leave empty for characters)
- Click **📋 Load Suggested Prompt for CharacterBase** (or paste from PromptLibrary.md)
- Adjust the prompt as desired
- **Width / Height**: 1024 × 1024
- **Number of Samples**: 4 (gives you variations to pick from)

### Step 3: Generate

- Click **🚀 Generate**
- Status updates show:
  - "Submitting prompt..."
  - "Polling inference..." (30-90 seconds)
  - "Generation complete — N images. Downloading..."
  - "All N images downloaded"

### Step 4: Review

- 4 thumbnails appear in the **Preview Results** section
- **Approve** moves them to: `Assets/Generated/Characters/Bases/Male_Adventurer/`
- **Discard** deletes them
- Files are also saved to `Assets/Generated/_Review/<timestamp>_<variant>/` so you can always retrieve

---

## 📂 Folder Structure

```
Assets/
├── Generated/
│   ├── _Prompts/                  ← Edit this with Claude
│   │   └── PromptLibrary.md
│   ├── _Review/                   ← Temp folder for new generations
│   │   └── 2026-05-20_14-30-12_Male_Adventurer/
│   │       ├── sample_00.png
│   │       ├── sample_01.png
│   │       └── ...
│   ├── Characters/
│   │   ├── Bases/
│   │   │   ├── Male_Adventurer/
│   │   │   ├── Female_Adventurer/
│   │   │   └── Plain_Template/
│   │   ├── Hair/
│   │   ├── Faces/
│   │   ├── Clothes/
│   │   └── Armor/
│   ├── Props/
│   │   ├── Plains/
│   │   ├── Desert/
│   │   └── ...
│   └── Tiles/
└── Editor/Scenario/
    ├── ScenarioConfig.cs          ← API key (EditorPrefs)
    ├── ScenarioApiClient.cs       ← HTTP client
    └── ScenarioGenerationWindow.cs ← Editor UI
```

---

## 🎯 Current Workflow Priorities

Per the latest discussion, we're starting with **Character Creation**:

1. ✅ Build pipeline tooling
2. ⏳ Generate 3 base bodies (Male, Female, Plain)
3. ⏳ Generate 3 hair styles
4. ⏳ Generate 3 face options
5. ⏳ Generate 3 clothing options
6. ⏳ Build the runtime layer compositor
7. ⏳ Build the in-game character creator UI

---

## 🛠 Menu Reference

| Menu | What It Does |
|------|--------------|
| `Tools/LIT-ISO/AI Generation/Configure Scenario API` | Set API key + model IDs |
| `Tools/LIT-ISO/AI Generation/Generation Window` | Main generation UI |

---

## 🐛 Troubleshooting

### "Connection Failed" on Test Connection
- Double-check key and secret (Show toggle to verify secret)
- Verify the key is active in https://app.scenario.com → API Keys

### "Inference failed: rate limit"
- You've exceeded your plan's request quota. Wait or upgrade plan.

### "Could not parse inference ID from response"
- Scenario API response shape might be different than expected
- Check the Console for the full raw response and adjust `ScenarioApiClient.cs` DTOs

### Generation completes but no images download
- Result URLs are temporary (~30 min lifespan)
- If polling took too long, regenerate
- Check that the `_Review` folder is writable

### Wrong model picked / weird results
- Use **Refresh Models** and try a different model
- Some models are trained on specific styles (anime, photo, etc.) — pick an iso/fantasy one

### "Model ID is empty"
- Run **Refresh Models** then click "Use as text-to-image" next to a model

---

## 🔮 Pixal3D Integration (Next Phase)

The pipeline is built to support Pixal3D image-to-3D conversion. The flow will be:

1. Generate 2D concept image (working now)
2. Upload that image to Scenario as an asset → get `assetId`
3. Submit to `model_pixal3d` with the assetId
4. Poll until 3D model is ready
5. Download `.glb` model + texture

To enable: tick **"Run Pixal3D after generation"** in the Generation Window. (Currently a stub — full implementation comes after we validate the text-to-image flow works.)

---

## 🤝 How We Iterate Together

1. **You** open the Generation Window, paste a prompt from PromptLibrary.md
2. **You** click Generate, see the result
3. If results are off, **we** discuss what to change (style, detail, framing)
4. **I** update the prompts in PromptLibrary.md based on feedback
5. **You** regenerate with new prompts
6. Repeat until happy
7. **You** click Approve to file it into the final folder
8. **We** build the runtime systems (layer compositor, character creator UI) once the art is solid

---

**Status:** Pipeline foundation ready ✅
**Next:** Configure API → Refresh Models → Generate first character base

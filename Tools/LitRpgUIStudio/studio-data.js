(function () {
  "use strict";

  const component = (id, type, label, x, y, w, h, options = {}) => ({
    id,
    type,
    label,
    x,
    y,
    w,
    h,
    z: options.z || 1,
    anchor: options.anchor || "top-left",
    variant: options.variant || "canvas",
    visible: options.visible !== false,
    locked: options.locked === true,
    runtime: options.runtime || "",
    resource: options.resource || "",
    props: options.props || {}
  });

  const itemIcons = ["◆", "✦", "●", "▲", "■", "✚", "◇", "✣", "◈", "⬟"];
  const skillIcons = ["✦", "✣", "◆", "◈", "▲", "●", "✚", "◇"];

  window.LIT_RPG_UI_STUDIO_DEFAULTS = {
    schema: "lit-rpg-ui-studio@1",
    designResolution: { width: 1920, height: 1080 },
    theme: {
      wood: "#704627",
      canvas: "#2f4b37",
      ember: "#e99b45",
      cream: "#f4e8c4",
      leaf: "#7fa45c",
      health: "#c94f48",
      mana: "#4d8fba",
      radius: 4
    },
    groups: [
      { id: "entry", label: "Entry Flow" },
      { id: "gameplay", label: "Gameplay" },
      { id: "system", label: "System Book" }
    ],
    screens: [
      {
        id: "main-menu",
        group: "entry",
        label: "Main Menu",
        symbol: "M",
        background: "campfire",
        scrim: 22,
        uiScale: 100,
        runtimeSurface: "LitIso.UI.WelcomeScreenManager / MainMenu",
        runtimeNotes: "Procedural menu surface. Keep the left side menu-safe and the campfire party visible.",
        pixelLabBrief: "full-screen warm nighttime forest campsite key art with four friendly adventurers around a campfire, clear left-side negative space, crisp original pixel art",
        components: [
          component("welcome/Brand", "brand", "LIT-RPG wordmark", 92, 96, 520, 110, {
            variant: "floating",
            runtime: "WelcomeScreenManager/Title",
            resource: "UI/Menu/logo"
          }),
          component("welcome/MainNav", "menu", "Main navigation", 100, 250, 390, 390, {
            runtime: "WelcomeScreenManager/MainMenu",
            resource: "UI/Menu/button",
            props: { items: ["Continue", "New Adventure", "Join Friends", "Settings", "Quit"] }
          }),
          component("welcome/Version", "text", "Version", 22, 1028, 210, 28, {
            anchor: "bottom-left",
            variant: "minimal",
            locked: true,
            runtime: "WelcomeScreenManager/Version",
            props: { text: "Early Development" }
          })
        ]
      },
      {
        id: "loading",
        group: "entry",
        label: "Loading",
        symbol: "L",
        background: "campfire",
        scrim: 18,
        uiScale: 100,
        runtimeSurface: "LitIso.UI.LoadingScreen",
        runtimeNotes: "Full-bleed scene art. Existing fade timing remains 0.35 / 0.25 / 0.5 seconds.",
        pixelLabBrief: "warm cinematic pixel-art forest campsite loading scene, four adventurers resting, subtle fireflies, large unobstructed environment",
        components: [
          component("loading/Destination", "heading", "Destination", 100, 86, 560, 94, {
            variant: "floating",
            runtime: "LoadingScreen/Title",
            props: { kicker: "TRAVELLING TO", text: "Pinewood Vale" }
          }),
          component("loading/Tip", "loading-tip", "Loading tip", 510, 790, 900, 112, {
            anchor: "bottom-center",
            runtime: "LoadingScreen/Tip",
            resource: "UI/Loading/tip_frame"
          }),
          component("loading/Progress", "flames", "Campfire progress", 760, 930, 400, 72, {
            anchor: "bottom-center",
            variant: "floating",
            runtime: "LoadingScreen/Progress",
            resource: "UI/Loading/flame"
          })
        ]
      },
      {
        id: "character-creator",
        group: "entry",
        label: "Character Creator",
        symbol: "C",
        background: "forest",
        scrim: 44,
        uiScale: 100,
        runtimeSurface: "WelcomeScreenManager / CallingSelect + character appearance flow",
        runtimeNotes: "Appearance selection is visual-first. Class tokens lead into the existing calling selection state.",
        pixelLabBrief: "transparent cozy pixel UI kit for character creation: thin wood tabs, green stitched canvas panels, appearance thumbnails, class object icons, no text",
        components: [
          component("creator/Name", "name-field", "Character name", 660, 42, 600, 68, {
            anchor: "top-center",
            runtime: "CharacterCreator/Name"
          }),
          component("creator/Categories", "category-tabs", "Appearance categories", 70, 160, 265, 570, {
            runtime: "CharacterCreator/Categories",
            resource: "UI/Creator/category_button"
          }),
          component("creator/Preview", "avatar", "Character preview", 390, 150, 650, 670, {
            variant: "floating",
            runtime: "CharacterCreator/Preview"
          }),
          component("creator/Options", "swatches", "Appearance choices", 1120, 150, 720, 670, {
            runtime: "CharacterCreator/Options",
            resource: "UI/Creator/option_slot"
          }),
          component("creator/Classes", "classes", "Class tokens", 450, 840, 1020, 170, {
            anchor: "bottom-center",
            runtime: "WelcomeScreenManager/CallingSelect",
            resource: "UI/Creator/class_slot"
          }),
          component("creator/Back", "action", "Back", 70, 940, 190, 72, {
            anchor: "bottom-left",
            runtime: "CharacterCreator/Back",
            props: { text: "Back", tone: "secondary" }
          }),
          component("creator/Start", "action", "Start Adventure", 1600, 930, 250, 82, {
            anchor: "bottom-right",
            runtime: "CharacterCreator/Confirm",
            props: { text: "Start Adventure", tone: "primary" }
          })
        ]
      },
      {
        id: "create-world",
        group: "entry",
        label: "Create World",
        symbol: "W",
        background: "map",
        scrim: 18,
        uiScale: 100,
        runtimeSurface: "LitIso.UI.WelcomeScreenManager / CreateWorld",
        runtimeNotes: "Matches the current CreateWorld state while making the generated world preview the visual focus.",
        pixelLabBrief: "original colorful isometric pixel world-map preview with forest, meadow, coast, mountains, village, camp and dungeon markers",
        components: [
          component("world/Form", "world-form", "World settings", 90, 120, 620, 750, {
            runtime: "WelcomeScreenManager/CreateWorldPanel",
            resource: "UI/Menu/panel"
          }),
          component("world/Preview", "world-map", "World preview", 790, 90, 1040, 830, {
            variant: "plain",
            runtime: "WelcomeScreenManager/WorldPreview",
            resource: "UI/World/preview_frame"
          }),
          component("world/Back", "action", "Back", 90, 940, 190, 72, {
            anchor: "bottom-left",
            runtime: "WelcomeScreenManager/Back",
            props: { text: "Back", tone: "secondary" }
          }),
          component("world/Create", "action", "Create World", 1530, 940, 300, 72, {
            anchor: "bottom-right",
            runtime: "WelcomeScreenManager/Create",
            props: { text: "Create World", tone: "primary" }
          })
        ]
      },
      {
        id: "hud",
        group: "gameplay",
        label: "Gameplay HUD",
        symbol: "H",
        background: "forest",
        scrim: 8,
        uiScale: 100,
        runtimeSurface: "LitIso.UI.InGame.GameUIController",
        runtimeNotes: "Canvas order 100 at 1920x1080. Hotbar pages inventory rows; ability loadout remains Q/E/R/F.",
        pixelLabBrief: "transparent cozy survival-game HUD kit: compact wood and green canvas frames, ten square hotbar slots, four skill slots, circular minimap frame, quest tracker",
        components: [
          component("hud/Vitals", "vitals", "Vitals", 28, 28, 380, 170, {
            runtime: "GameUIController/Vitals",
            resource: "UI/HUD/vitals_frame"
          }),
          component("hud/DayBand", "day", "Day, time and weather", 740, 24, 440, 56, {
            anchor: "top-center",
            runtime: "GameUIController/DayBand",
            resource: "UI/HUD/day_band"
          }),
          component("hud/Minimap", "minimap", "Minimap", 1650, 28, 230, 230, {
            anchor: "top-right",
            variant: "wood",
            runtime: "GameUIController/TopRight/Minimap",
            resource: "UI/HUD/minimap_frame"
          }),
          component("hud/QuestTracker", "quests", "Pinned quest", 1540, 275, 340, 210, {
            anchor: "top-right",
            runtime: "GameUIController/TopRight/QuestTracker",
            resource: "UI/HUD/quest_frame"
          }),
          component("hud/AbilityBar", "ability-bar", "Abilities Q E R F", 714, 855, 492, 92, {
            anchor: "bottom-center",
            variant: "floating",
            runtime: "GameUIController/AbilityBar",
            resource: "UI/HUD/ability_slot"
          }),
          component("hud/Hotbar", "hotbar", "Inventory hotbar", 510, 950, 900, 100, {
            anchor: "bottom-center",
            runtime: "GameUIController/Hotbar",
            resource: "UI/HUD/hotbar_slot",
            props: { slots: 10, activeRow: 2, rows: 4 }
          }),
          component("hud/InteractPrompt", "context", "Context prompt", 760, 640, 400, 68, {
            anchor: "center",
            variant: "plain",
            runtime: "GameUIController/InteractPrompt"
          })
        ]
      },
      {
        id: "backpack",
        group: "system",
        label: "Backpack & Gear",
        symbol: "B",
        background: "forest",
        scrim: 48,
        uiScale: 100,
        runtimeSurface: "LitIso.UI.InGame.CharacterPanelView / Inventory",
        runtimeNotes: "The system book owns this surface. InventoryView remains a secondary standalone view with its existing 6-column model.",
        pixelLabBrief: "transparent cozy inventory UI kit with stitched forest-green canvas, medium wood slots, paper-doll equipment frames, colorful pixel item placeholders, no baked text",
        components: [
          component("system/TabStrip", "tabs", "System tabs", 100, 52, 1720, 80, {
            anchor: "top-center",
            runtime: "CharacterPanelView/TabStrip",
            resource: "UI/System/tab"
          }),
          component("inventory/Backpack", "items", "Backpack grid", 110, 160, 760, 720, {
            runtime: "CharacterPanelView/Body/InventoryGrid",
            resource: "UI/System/item_slot",
            props: { columns: 8, count: 48 }
          }),
          component("inventory/PaperDoll", "paper-doll", "Character and gear", 900, 160, 560, 720, {
            runtime: "CharacterPanelView/Body/Equipment",
            resource: "UI/System/equipment_slot"
          }),
          component("inventory/Details", "item-detail", "Selected item", 1490, 160, 320, 720, {
            runtime: "CharacterPanelView/Body/ItemDetails",
            resource: "UI/System/detail_frame"
          }),
          component("inventory/HotbarRow", "hotbar", "Exposed hotbar row", 480, 920, 960, 96, {
            anchor: "bottom-center",
            runtime: "CharacterPanelView/InvOpsOverlay",
            resource: "UI/HUD/hotbar_slot",
            props: { slots: 10, activeRow: 2, rows: 4 }
          })
        ]
      },
      {
        id: "skills",
        group: "system",
        label: "Skills & Wheel",
        symbol: "S",
        background: "forest",
        scrim: 58,
        uiScale: 100,
        runtimeSurface: "CharacterPanelView / Skills + LitIso.UI.InGame.AbilityWheelView",
        runtimeNotes: "Ability wheel canvas order 210. Q/E/R/F are stable assignment anchors and hold-X opens the wheel.",
        pixelLabBrief: "transparent cozy ability UI kit with natural wood ring segments, elemental skill icons, green canvas skill grid and four Q E R F loadout slots",
        components: [
          component("system/TabStrip", "tabs", "System tabs", 100, 52, 1720, 80, {
            anchor: "top-center",
            runtime: "CharacterPanelView/TabStrip",
            resource: "UI/System/tab"
          }),
          component("skills/Grid", "skills", "Learned skills", 100, 165, 560, 740, {
            runtime: "CharacterPanelView/Body/Skills",
            resource: "UI/Skills/skill_slot",
            props: { icons: skillIcons }
          }),
          component("skills/Wheel", "wheel", "Ability wheel", 670, 150, 760, 760, {
            variant: "floating",
            runtime: "AbilityWheelView/Wheel",
            resource: "UI/Skills/wheel_segment"
          }),
          component("skills/Details", "skill-detail", "Skill details", 1460, 165, 360, 590, {
            runtime: "CharacterPanelView/Body/SkillDetails",
            resource: "UI/System/detail_frame"
          }),
          component("skills/Loadout", "ability-bar", "Q E R F loadout", 1415, 790, 450, 105, {
            anchor: "bottom-right",
            runtime: "AbilityWheelView/AnchorQ,AnchorE,AnchorR,AnchorF",
            resource: "UI/HUD/ability_slot"
          })
        ]
      },
      {
        id: "quests-map",
        group: "system",
        label: "Quests & Map",
        symbol: "Q",
        background: "map",
        scrim: 18,
        uiScale: 100,
        runtimeSurface: "CharacterPanelView / Journal + LitIso.UI.InGame.WorldMapView",
        runtimeNotes: "WorldMapView uses normalized pin positions, fog opacity, and a dedicated map canvas at order 120.",
        pixelLabBrief: "colorful explored pixel world map with forest paths, river, village, camp, dungeon, resource and quest markers; separate transparent quest panel frame",
        components: [
          component("system/TabStrip", "tabs", "System tabs", 100, 52, 1720, 80, {
            anchor: "top-center",
            runtime: "CharacterPanelView/TabStrip",
            resource: "UI/System/tab"
          }),
          component("quests/List", "quest-book", "Quest log", 100, 160, 560, 780, {
            runtime: "CharacterPanelView/Body/Journal",
            resource: "UI/Quest/log_frame"
          }),
          component("map/MapArea", "world-map", "World map", 690, 160, 1130, 780, {
            variant: "plain",
            runtime: "WorldMapView/MapArea",
            resource: "UI/Map/frame"
          })
        ]
      },
      {
        id: "crafting-storage",
        group: "system",
        label: "Crafting & Storage",
        symbol: "F",
        background: "forest",
        scrim: 54,
        uiScale: 100,
        runtimeSurface: "CharacterPanelView / Crafting",
        runtimeNotes: "Crafting stays inside the system book. Storage is a split-view extension for nearby containers.",
        pixelLabBrief: "transparent cozy crafting and storage UI kit: recipe tabs, item slots, ingredient rows, craft queue, wood buttons, green stitched canvas",
        components: [
          component("system/TabStrip", "tabs", "System tabs", 100, 52, 1720, 80, {
            anchor: "top-center",
            runtime: "CharacterPanelView/TabStrip",
            resource: "UI/System/tab"
          }),
          component("crafting/Recipes", "recipes", "Recipes", 100, 160, 650, 760, {
            runtime: "CharacterPanelView/Body/Crafting/Recipes",
            resource: "UI/Crafting/recipe_slot"
          }),
          component("crafting/Details", "recipe-detail", "Recipe details", 780, 160, 470, 760, {
            runtime: "CharacterPanelView/Body/Crafting/Details",
            resource: "UI/System/detail_frame"
          }),
          component("crafting/Storage", "items", "Nearby storage", 1280, 160, 540, 760, {
            runtime: "CharacterPanelView/Body/Crafting/Storage",
            resource: "UI/System/item_slot",
            props: { columns: 6, count: 30 }
          })
        ]
      },
      {
        id: "settings",
        group: "system",
        label: "Settings & Pause",
        symbol: "P",
        background: "campfire",
        scrim: 58,
        uiScale: 100,
        runtimeSurface: "CharacterPanelView / Settings + pause actions",
        runtimeNotes: "Shared preference keys include hud.scale and audio.master. Every overlay closes with Esc and an explicit close control.",
        pixelLabBrief: "transparent warm indie-game settings panel kit with compact wood category tabs, green canvas rows, pixel sliders, toggles and controller keycaps",
        components: [
          component("settings/Categories", "settings-tabs", "Settings categories", 240, 170, 310, 650, {
            runtime: "CharacterPanelView/Body/Settings/Categories",
            resource: "UI/Settings/category"
          }),
          component("settings/Options", "settings", "Settings controls", 580, 170, 720, 650, {
            runtime: "CharacterPanelView/Body/Settings/Options",
            resource: "UI/Settings/panel"
          }),
          component("settings/Keybinds", "keybinds", "Keybinds", 1330, 170, 350, 650, {
            runtime: "CharacterPanelView/Body/Settings/Keybinds",
            resource: "UI/Settings/keycap"
          }),
          component("settings/Actions", "pause-actions", "Pause actions", 480, 860, 960, 100, {
            anchor: "bottom-center",
            variant: "floating",
            runtime: "CharacterPanelView/Body/Settings/Actions"
          })
        ]
      }
    ],
    catalogs: {
      itemIcons,
      skillIcons,
      systemTabs: ["Backpack", "Gear", "Skills", "Quests", "Map", "Crafting", "Character", "Journal", "Settings"],
      bindings: {
        "hud.health.fill": "IGameHudModel.Health01",
        "hud.mana.fill": "IGameHudModel.Mana01",
        "hud.hotbar.items": "IGameHudModel.GetSlot",
        "inventory.items": "IInventoryViewModel.GetSlot",
        "ability.items": "IAbilityLoadoutViewModel.GetAbility",
        "map.pins": "WorldMapView.SetPins"
      }
    }
  };
}());

(function () {
  "use strict";

  const defaults = window.LIT_RPG_UI_STUDIO_DEFAULTS;
  const storageKey = "lit-rpg-ui-studio-v1";
  const maxHistory = 60;

  const dom = {
    nav: document.getElementById("screen-nav"),
    title: document.getElementById("screen-title"),
    group: document.getElementById("screen-group"),
    viewportShell: document.getElementById("viewport-shell"),
    viewport: document.getElementById("viewport"),
    background: document.getElementById("screen-background"),
    scrim: document.getElementById("screen-scrim"),
    layer: document.getElementById("component-layer"),
    safeArea: document.getElementById("safe-area"),
    layerList: document.getElementById("layer-list"),
    emptySelection: document.getElementById("empty-selection"),
    elementInspector: document.getElementById("element-inspector"),
    screenSettingsTitle: document.getElementById("screen-settings-title"),
    runtimeSurface: document.getElementById("runtime-surface"),
    runtimeNotes: document.getElementById("runtime-notes"),
    saveStatus: document.getElementById("save-status"),
    toast: document.getElementById("toast"),
    exportDialog: document.getElementById("export-dialog"),
    exportPreview: document.getElementById("export-preview"),
    importFile: document.getElementById("import-file")
  };

  let state = loadState();
  let activeScreenId = state.activeScreenId || defaults.screens[0].id;
  let selectedId = null;
  let mode = "edit";
  let zoomMode = "fit";
  let manualZoom = 0.55;
  let effectiveZoom = 0.55;
  let history = [];
  let historyIndex = -1;
  let toastTimer = null;
  let pendingHistorySnapshot = null;

  function deepClone(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function initialState() {
    return {
      schema: defaults.schema,
      activeScreenId: defaults.screens[0].id,
      previewResolution: { width: 1920, height: 1080 },
      showSafeArea: false,
      franukaSkin: true,
      theme: deepClone(defaults.theme),
      screens: deepClone(defaults.screens)
    };
  }

  function loadState() {
    const clean = initialState();
    try {
      const raw = localStorage.getItem(storageKey);
      if (!raw) return clean;
      return mergeState(clean, JSON.parse(raw));
    } catch (error) {
      console.warn("UI Studio local state could not be loaded.", error);
      return clean;
    }
  }

  function mergeState(clean, saved) {
    if (!saved || typeof saved !== "object") return clean;
    clean.activeScreenId = typeof saved.activeScreenId === "string" ? saved.activeScreenId : clean.activeScreenId;
    clean.previewResolution = saved.previewResolution || clean.previewResolution;
    clean.showSafeArea = Boolean(saved.showSafeArea);
    clean.franukaSkin = saved.franukaSkin !== false;
    clean.theme = Object.assign(clean.theme, saved.theme || {});

    const savedScreens = new Map((saved.screens || []).map(screen => [screen.id, screen]));
    clean.screens = clean.screens.map(screen => {
      const stored = savedScreens.get(screen.id);
      if (!stored) return screen;
      const storedComponents = new Map((stored.components || []).map(item => [item.id, item]));
      const mergedComponents = screen.components.map(item => Object.assign(item, storedComponents.get(item.id) || {}));
      for (const storedItem of stored.components || []) {
        if (!mergedComponents.some(item => item.id === storedItem.id)) mergedComponents.push(storedItem);
      }
      return Object.assign(screen, stored, { components: mergedComponents });
    });
    return clean;
  }

  function saveState() {
    state.activeScreenId = activeScreenId;
    dom.saveStatus.textContent = "Saving…";
    try {
      localStorage.setItem(storageKey, JSON.stringify(state));
      window.setTimeout(() => {
        dom.saveStatus.textContent = "Saved locally";
      }, 180);
    } catch (error) {
      dom.saveStatus.textContent = "Export to preserve changes";
      console.warn("UI Studio local state could not be saved.", error);
    }
  }

  function screenById(id = activeScreenId) {
    return state.screens.find(screen => screen.id === id);
  }

  function defaultScreenById(id = activeScreenId) {
    return defaults.screens.find(screen => screen.id === id);
  }

  function selectedComponent() {
    return screenById()?.components.find(item => item.id === selectedId) || null;
  }

  function groupById(id) {
    return defaults.groups.find(group => group.id === id);
  }

  function escapeHtml(value) {
    return String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  function pushHistory(snapshot = deepClone(state)) {
    if (historyIndex < history.length - 1) history = history.slice(0, historyIndex + 1);
    history.push(snapshot);
    if (history.length > maxHistory) history.shift();
    historyIndex = history.length - 1;
    updateHistoryButtons();
  }

  function commitHistory(beforeSnapshot) {
    const after = JSON.stringify(state);
    if (JSON.stringify(beforeSnapshot) === after) return;
    if (history.length === 0) pushHistory(beforeSnapshot);
    pushHistory(deepClone(state));
    saveState();
  }

  function restoreHistory(index) {
    if (index < 0 || index >= history.length) return;
    historyIndex = index;
    state = deepClone(history[historyIndex]);
    activeScreenId = state.activeScreenId || activeScreenId;
    selectedId = null;
    renderAll();
    saveState();
    updateHistoryButtons();
  }

  function updateHistoryButtons() {
    document.getElementById("undo-button").disabled = historyIndex <= 0;
    document.getElementById("redo-button").disabled = historyIndex < 0 || historyIndex >= history.length - 1;
  }

  function showToast(message) {
    window.clearTimeout(toastTimer);
    dom.toast.textContent = message;
    dom.toast.classList.add("visible");
    toastTimer = window.setTimeout(() => dom.toast.classList.remove("visible"), 1800);
  }

  function renderAll() {
    applyTheme();
    renderNavigation();
    renderScreen();
    renderInspector();
    requestAnimationFrame(updateViewportTransform);
  }

  function applyTheme() {
    const root = document.documentElement.style;
    root.setProperty("--wood", state.theme.wood);
    root.setProperty("--canvas", state.theme.canvas);
    root.setProperty("--ember", state.theme.ember);
    root.setProperty("--cream", state.theme.cream);
    root.setProperty("--leaf", state.theme.leaf);
    root.setProperty("--health", state.theme.health);
    root.setProperty("--mana", state.theme.mana);
    root.setProperty("--bevel", `${state.theme.radius}px`);

    dom.viewport.classList.toggle("skin-franuka", state.franukaSkin !== false);
    const franukaToggle = document.getElementById("franuka-skin-toggle");
    if (franukaToggle) franukaToggle.checked = state.franukaSkin !== false;

    document.getElementById("theme-wood").value = state.theme.wood;
    document.getElementById("theme-canvas").value = state.theme.canvas;
    document.getElementById("theme-ember").value = state.theme.ember;
    document.getElementById("theme-cream").value = state.theme.cream;
    document.getElementById("theme-leaf").value = state.theme.leaf;
    document.getElementById("theme-health").value = state.theme.health;
    document.getElementById("theme-mana").value = state.theme.mana;
    document.getElementById("theme-radius").value = state.theme.radius;
    document.getElementById("radius-output").textContent = `${state.theme.radius} px`;
  }

  function renderNavigation() {
    dom.nav.innerHTML = "";
    for (const group of defaults.groups) {
      const section = document.createElement("div");
      section.className = "screen-group";
      section.innerHTML = `<span class="screen-group-title">${escapeHtml(group.label)}</span>`;
      for (const screen of state.screens.filter(item => item.group === group.id)) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = `screen-nav-button${screen.id === activeScreenId ? " active" : ""}`;
        button.dataset.screenId = screen.id;
        button.innerHTML = `
          <span class="screen-nav-icon">${escapeHtml(screen.symbol)}</span>
          <span>${escapeHtml(screen.label)}</span>`;
        section.append(button);
      }
      dom.nav.append(section);
    }
  }

  function renderScreen() {
    const screen = screenById();
    if (!screen) return;
    const group = groupById(screen.group);
    dom.title.textContent = screen.label;
    dom.group.textContent = group?.label || "Screen";
    dom.screenSettingsTitle.textContent = screen.label;
    dom.runtimeSurface.textContent = screen.runtimeSurface;
    dom.runtimeNotes.textContent = screen.runtimeNotes;
    dom.background.dataset.background = screen.background;
    dom.scrim.style.background = `rgba(7, 11, 8, ${screen.scrim / 100})`;
    dom.layer.style.transform = `scale(${screen.uiScale / 100})`;
    dom.layer.innerHTML = "";
    dom.safeArea.classList.toggle("visible", state.showSafeArea);

    for (const component of [...screen.components].sort((a, b) => a.z - b.z)) {
      dom.layer.append(buildComponent(component));
    }
    wirePreviewInteractions();
    updateScreenFields();
  }

  function buildComponent(component) {
    const node = document.createElement("div");
    node.className = [
      "ui-component",
      component.id === selectedId ? "selected" : "",
      component.locked ? "locked" : "",
      component.visible ? "" : "hidden-element"
    ].filter(Boolean).join(" ");
    node.dataset.componentId = component.id;
    node.dataset.variant = component.variant;
    node.style.left = `${component.x}px`;
    node.style.top = `${component.y}px`;
    node.style.width = `${component.w}px`;
    node.style.height = `${component.h}px`;
    node.style.zIndex = component.z;
    node.innerHTML = `
      <div class="component-content">${renderComponentContent(component)}</div>
      <span class="resize-handle" aria-hidden="true"></span>`;
    applyComponentSkin(node, component);
    if (mode === "edit") wireDragAndResize(node, component);
    node.addEventListener("pointerdown", event => {
      if (mode !== "edit") return;
      event.stopPropagation();
      selectComponent(component.id);
    });
    return node;
  }

  function renderComponentContent(component) {
    const props = component.props || {};
    const icons = defaults.catalogs.itemIcons;
    const skills = defaults.catalogs.skillIcons;
    switch (component.type) {
      case "brand":
        return `<div class="game-brand">LIT-RPG</div>`;
      case "menu":
        return `<div class="menu-stack">${(props.items || []).map((item, index) =>
          `<button type="button" class="${index === 0 ? "active" : ""}">${escapeHtml(item)}</button>`).join("")}</div>`;
      case "text":
        return `<div class="component-pad component-kicker">${escapeHtml(props.text || component.label)}</div>`;
      case "heading":
        return `<div class="component-pad"><div class="component-kicker">${escapeHtml(props.kicker)}</div><div class="component-title">${escapeHtml(props.text)}</div></div>`;
      case "loading-tip":
        return `<div class="loading-tip"><strong>Campfire Wisdom</strong><span>Cooking together restores party spirit.</span></div>`;
      case "flames":
        return `<div class="flame-row">${Array.from({ length: 7 }, (_, index) =>
          `<i class="flame${index > 4 ? " dim" : ""}"></i>`).join("")}</div>`;
      case "name-field":
        return `<div class="world-form"><div class="form-row"><span>Name</span><input value="Campfire Wanderer" readonly></div></div>`;
      case "category-tabs":
        return `<div class="category-stack">${["Body", "Face", "Hair", "Outfit", "Colors"].map((item, index) =>
          `<button type="button" class="${index === 0 ? "active" : ""}">${escapeHtml(item)}</button>`).join("")}</div>`;
      case "avatar":
        return avatarMarkup();
      case "swatches":
        return `<div class="swatch-grid">${["#4c2e1d", "#7d4b2c", "#c87c49", "#e6ba81", "#809c6b", "#426f86", "#7f5f8e", "#d9d2bb", "#4e5b43", "#9b7044", "#273c32", "#b45e45", "#6d7ca7", "#d2a652", "#483229"].map((color, index) =>
          `<button type="button" class="swatch${index === 2 ? " active" : ""}" style="--swatch:${color}" aria-label="Appearance option ${index + 1}"></button>`).join("")}</div>`;
      case "classes":
        return `<div class="class-bar">${[
          ["G", "Guardian"], ["H", "Healer"], ["R", "Ranger"], ["D", "Rogue"]
        ].map((entry, index) => `<button type="button" class="class-token${index === 2 ? " active" : ""}"><b>${entry[0]}</b><span>${entry[1]}</span></button>`).join("")}</div>`;
      case "action":
        return `<button type="button" class="wood-button" style="width:100%;height:100%;text-align:center">${escapeHtml(props.text || component.label)}</button>`;
      case "world-form":
        return `<div class="world-form">
          <div class="component-title">Create a World</div>
          <div class="form-row"><span>World Name</span><input value="Pinewood Vale" readonly></div>
          <div class="form-row"><span>Seed</span><input value="7F6A-9C1E" readonly></div>
          <div class="form-row"><span>Challenge</span><b>Normal</b></div>
          <div class="form-row"><span>Play Style</span><b>Balanced</b></div>
          <div class="form-row"><span>Friends</span><b>Invite Only</b></div>
        </div>`;
      case "world-map":
        return worldMapMarkup();
      case "vitals":
        return `<div class="vitals"><div class="portrait">12</div><div class="bar-stack">
          <div class="meter" style="--meter-color:var(--health);--amount:82%"><span></span><small>420 / 512</small></div>
          <div class="meter" style="--meter-color:var(--mana);--amount:66%"><span></span><small>132 / 200</small></div>
          <div class="meter" style="--meter-color:var(--stamina);--amount:74%"><span></span><small>STAMINA</small></div>
          <div class="meter" style="--meter-color:#d8ae45;--amount:48%"><span></span><small>LEVEL 12</small></div>
        </div></div>`;
      case "day":
        return `<div class="day-strip"><span>Day 15</span><span>22:48</span><span>Clear 8°C</span></div>`;
      case "minimap":
        return `<div class="component-pad"><div class="minimap"></div></div>`;
      case "quests":
        return questListMarkup(false);
      case "ability-bar":
        return `<div class="ability-bar" style="--slot-count:4">${["Q", "E", "R", "F"].map((key, index) =>
          `<button type="button" class="ability-slot${index === 1 ? " active" : ""}">${skills[index]}<small>${key}</small></button>`).join("")}</div>`;
      case "hotbar":
        return hotbarMarkup(props.slots || 10, props.activeRow || 1, props.rows || 4);
      case "context":
        return `<div class="context-prompt"><span class="context-key">E</span><span>Gather herbs</span></div>`;
      case "tabs":
        return `<div class="tab-list">${defaults.catalogs.systemTabs.map(tab =>
          `<button type="button" class="tab-chip${tab === tabForScreen(activeScreenId) ? " active" : ""}">${escapeHtml(tab)}</button>`).join("")}</div>`;
      case "items":
        return itemGridMarkup(props.columns || 8, props.count || 40);
      case "paper-doll":
        return paperDollMarkup();
      case "item-detail":
        return `<div class="item-detail"><h3>Iron Sword</h3><div class="item-hero-icon">†</div>
          <div class="stat-row"><span>Damage</span><b>32</b></div>
          <div class="stat-row"><span>Speed</span><b>1.25</b></div>
          <div class="stat-row"><span>Durability</span><b>86 / 100</b></div>
          <button class="wood-button" type="button">Use</button><button class="wood-button" type="button">Equip</button><button class="wood-button" type="button">Drop</button></div>`;
      case "skills":
        return skillGridMarkup();
      case "wheel":
        return abilityWheelMarkup();
      case "skill-detail":
        return `<div class="item-detail"><h3>Shield Bash</h3><div class="item-hero-icon">◆</div>
          <div class="stat-row"><span>Stamina</span><b>15</b></div>
          <div class="stat-row"><span>Cooldown</span><b>7 sec</b></div>
          <div class="stat-row"><span>Range</span><b>Short</b></div>
          <p class="component-kicker">Bash forward with your shield, stunning nearby enemies.</p></div>`;
      case "quest-book":
        return questListMarkup(true);
      case "recipes":
        return `<div class="recipe-list">${Array.from({ length: 20 }, (_, index) =>
          `<button type="button" class="item-slot${index === 1 ? " active" : ""}">${icons[index % icons.length]}<small>${index % 4 === 0 ? "!" : ""}</small></button>`).join("")}</div>`;
      case "recipe-detail":
        return `<div class="recipe-detail"><h3>Iron Pickaxe</h3><div class="item-hero-icon">⌁</div>
          <div class="ingredient-row"><span>Iron Ingot</span><b>7 / 3</b></div>
          <div class="ingredient-row"><span>Wooden Haft</span><b>1 / 1</b></div>
          <div class="ingredient-row"><span>Leather Strip</span><b style="color:var(--warning)">0 / 2</b></div>
          <button class="wood-button" type="button" style="width:100%;margin-top:18px">Craft</button></div>`;
      case "settings-tabs":
        return `<div class="category-stack">${["Gameplay", "Controls", "Audio", "Video", "Interface", "Accessibility"].map((item, index) =>
          `<button type="button" class="${index === 0 ? "active" : ""}">${item}</button>`).join("")}</div>`;
      case "settings":
        return `<div class="settings-list">${["Master Volume", "Music Volume", "Ambience", "SFX Volume", "HUD Scale"].map((item, index) =>
          `<div class="setting-row"><span>${item}</span><input type="range" value="${90 - index * 8}" disabled></div>`).join("")}
          <div class="setting-row"><span>Screen Shake</span><i class="toggle"></i></div>
          <div class="setting-row"><span>Weather Intensity</span><b>Medium</b></div>
          <div class="setting-row"><span>Hold Wheel Slowdown</span><i class="toggle"></i></div></div>`;
      case "keybinds":
        return `<div class="settings-list">${[
          ["Move", "WASD"], ["Interact", "E"], ["Inventory", "TAB"], ["Skill Wheel", "X"], ["Dodge", "SHIFT"], ["Map", "M"]
        ].map(row => `<div class="setting-row"><span>${row[0]}</span><b>${row[1]}</b></div>`).join("")}</div>`;
      case "pause-actions":
        return `<div class="pause-actions"><button type="button" class="wood-button">Resume</button><button type="button" class="wood-button">Save</button><button type="button" class="wood-button">Quit to Menu</button></div>`;
      default:
        return `<div class="component-pad">${escapeHtml(component.label)}</div>`;
    }
  }

  function applyComponentSkin(node, component) {
    if (!component.skin) return;
    const content = node.querySelector(".component-content");
    content.classList.add("skinned");
    const url = `url("${encodeURI(component.skin)}")`;
    const slice = Math.max(0, Number(component.skinSlice) || 0);
    if (slice > 0) {
      content.style.borderStyle = "solid";
      content.style.borderColor = "transparent";
      content.style.borderWidth = `${slice * 3}px`;
      content.style.borderImage = `${url} ${slice} fill stretch`;
    } else {
      content.style.backgroundImage = url;
      content.style.backgroundSize = "100% 100%";
    }
  }

  function avatarMarkup() {
    return `<div class="avatar-stage"><div class="pixel-avatar" aria-label="Pixel adventurer preview">
      <span class="head"></span><span class="hair"></span><span class="body"></span><span class="belt"></span>
      <span class="leg left"></span><span class="leg right"></span></div></div>`;
  }

  function worldMapMarkup() {
    return `<div class="map-preview">
      <i class="map-marker" style="left:21%;top:27%"><span>♠</span></i>
      <i class="map-marker" style="left:48%;top:55%"><span>⌂</span></i>
      <i class="map-marker" style="left:73%;top:30%"><span>▲</span></i>
      <i class="map-marker" style="left:78%;top:70%"><span>◆</span></i>
      <i class="map-marker" style="left:34%;top:69%"><span>✦</span></i>
    </div>`;
  }

  function questListMarkup(longForm) {
    return `<div class="quest-list"><h3>${longForm ? "Quest Log" : "Pinned Quest"}</h3>
      <div class="quest-row done"><span>Follow the tracks</span><b>1 / 1</b></div>
      <div class="quest-row"><span>Find the caravan</span><b>0 / 1</b></div>
      <div class="quest-row"><span>Return to Pinewood</span><b>0 / 1</b></div>
      ${longForm ? `<div class="quest-row"><span>Gather supplies</span><b>12 / 25</b></div>
      <div class="quest-row"><span>Campfire cooking</span><b>0 / 1</b></div>
      <div class="quest-row"><span>Old forest shrine</span><b>0 / 3</b></div>` : ""}
    </div>`;
  }

  function hotbarMarkup(slotCount, activeRow, rows) {
    const icons = defaults.catalogs.itemIcons;
    return `<div class="hotbar" style="--slot-count:${slotCount}">${Array.from({ length: slotCount }, (_, index) =>
      `<button type="button" class="hotbar-slot${index === 4 ? " active" : ""}">${icons[index % icons.length]}<small>${(index + 1) % 10}</small></button>`).join("")}
      <span style="position:absolute;right:18px;top:-24px;font:12px Inter,sans-serif">ROW ${activeRow} / ${rows}</span></div>`;
  }

  function itemGridMarkup(columns, count) {
    const icons = defaults.catalogs.itemIcons;
    return `<div class="item-grid" style="grid-template-columns:repeat(${columns},minmax(0,1fr))">${Array.from({ length: count }, (_, index) =>
      `<button type="button" class="item-slot${index === 12 ? " active" : ""}">${icons[index % icons.length]}<small>${index % 3 === 0 ? index + 2 : ""}</small></button>`).join("")}</div>`;
  }

  function paperDollMarkup() {
    const gearColumn = `<div class="gear-column">${["H", "B", "G", "F"].map(item => `<span class="gear-slot">${item}</span>`).join("")}</div>`;
    return `<div class="paper-doll">${gearColumn}${avatarMarkup()}${gearColumn}</div>`;
  }

  function skillGridMarkup() {
    const icons = defaults.catalogs.skillIcons;
    return `<div class="skill-grid">${Array.from({ length: 30 }, (_, index) =>
      `<button type="button" class="skill-slot${index === 8 ? " active" : ""}">${icons[index % icons.length]}<small>${"●".repeat(index % 4)}</small></button>`).join("")}</div>`;
  }

  function abilityWheelMarkup() {
    const icons = defaults.catalogs.skillIcons;
    const outer = icons.map((icon, index) =>
      `<button type="button" class="wheel-slot${index === 1 ? " active" : ""}" style="--angle:${index * 45}deg">${icon}</button>`).join("");
    const inner = ["Q", "E", "R", "F"].map((key, index) =>
      `<button type="button" class="ability-slot${index === 1 ? " active" : ""}">${icons[index]}<small>${key}</small></button>`).join("");
    return `<div class="ability-wheel"><div class="wheel-ring">${outer}<div class="wheel-center">${inner}</div></div></div>`;
  }

  function tabForScreen(screenId) {
    return {
      backpack: "Backpack",
      skills: "Skills",
      "quests-map": "Quests",
      "crafting-storage": "Crafting",
      settings: "Settings"
    }[screenId] || "Backpack";
  }

  function wireDragAndResize(node, component) {
    node.addEventListener("pointerdown", event => {
      if (component.locked || event.target.classList.contains("resize-handle")) return;
      event.preventDefault();
      node.setPointerCapture(event.pointerId);
      pendingHistorySnapshot = deepClone(state);
      const start = { x: event.clientX, y: event.clientY, left: component.x, top: component.y };
      const move = moveEvent => {
        const scale = effectiveZoom * (screenById().uiScale / 100);
        component.x = Math.round(start.left + (moveEvent.clientX - start.x) / scale);
        component.y = Math.round(start.top + (moveEvent.clientY - start.y) / scale);
        clampComponent(component);
        node.style.left = `${component.x}px`;
        node.style.top = `${component.y}px`;
        syncPositionFields(component);
      };
      const up = () => {
        node.removeEventListener("pointermove", move);
        node.removeEventListener("pointerup", up);
        commitHistory(pendingHistorySnapshot);
        pendingHistorySnapshot = null;
      };
      node.addEventListener("pointermove", move);
      node.addEventListener("pointerup", up);
    });

    node.querySelector(".resize-handle").addEventListener("pointerdown", event => {
      if (component.locked) return;
      event.preventDefault();
      event.stopPropagation();
      node.setPointerCapture(event.pointerId);
      pendingHistorySnapshot = deepClone(state);
      const start = { x: event.clientX, y: event.clientY, w: component.w, h: component.h };
      const move = moveEvent => {
        const scale = effectiveZoom * (screenById().uiScale / 100);
        component.w = Math.max(40, Math.round(start.w + (moveEvent.clientX - start.x) / scale));
        component.h = Math.max(30, Math.round(start.h + (moveEvent.clientY - start.y) / scale));
        clampComponent(component);
        node.style.width = `${component.w}px`;
        node.style.height = `${component.h}px`;
        syncPositionFields(component);
      };
      const up = () => {
        node.removeEventListener("pointermove", move);
        node.removeEventListener("pointerup", up);
        commitHistory(pendingHistorySnapshot);
        pendingHistorySnapshot = null;
      };
      node.addEventListener("pointermove", move);
      node.addEventListener("pointerup", up);
    });
  }

  function clampComponent(component) {
    const design = defaults.designResolution;
    component.w = Math.min(component.w, design.width);
    component.h = Math.min(component.h, design.height);
    component.x = Math.max(0, Math.min(component.x, design.width - component.w));
    component.y = Math.max(0, Math.min(component.y, design.height - component.h));
  }

  function wirePreviewInteractions() {
    dom.layer.querySelectorAll("button").forEach(button => {
      button.addEventListener("click", event => {
        if (mode !== "preview") return;
        event.stopPropagation();
        const parent = button.parentElement;
        if (!parent) return;
        const selectable = parent.querySelectorAll(".active");
        selectable.forEach(item => item.classList.remove("active"));
        button.classList.add("active");
      });
    });
  }

  function selectComponent(id) {
    selectedId = id;
    renderScreen();
    renderInspector();
  }

  function renderInspector() {
    renderLayerList();
    const component = selectedComponent();
    dom.emptySelection.hidden = Boolean(component);
    dom.elementInspector.hidden = !component;
    if (!component) return;

    document.getElementById("element-type").textContent = component.type.replaceAll("-", " ");
    document.getElementById("element-name").textContent = component.label;
    document.getElementById("field-x").value = component.x;
    document.getElementById("field-y").value = component.y;
    document.getElementById("field-w").value = component.w;
    document.getElementById("field-h").value = component.h;
    document.getElementById("field-label").value = component.label;
    document.getElementById("field-anchor").value = component.anchor;
    document.getElementById("field-variant").value = component.variant;
    document.getElementById("field-runtime").value = component.runtime;
    document.getElementById("field-resource").value = component.resource;
    const skinThumb = document.getElementById("skin-thumb");
    skinThumb.style.backgroundImage = component.skin ? `url("${encodeURI(component.skin)}")` : "none";
    document.getElementById("skin-name").textContent = component.skin
      ? component.skin.split("/").pop()
      : "None — pick from the Assets tab";
    document.getElementById("field-skin-slice").value = Number(component.skinSlice) || 0;
    document.getElementById("toggle-lock").textContent = component.locked ? "◆" : "◇";
    document.getElementById("toggle-visible").textContent = component.visible ? "◉" : "○";
  }

  function renderLayerList() {
    const screen = screenById();
    dom.layerList.innerHTML = "";
    for (const component of [...screen.components].sort((a, b) => b.z - a.z)) {
      const button = document.createElement("button");
      button.type = "button";
      button.className = `layer-button${component.id === selectedId ? " active" : ""}`;
      button.dataset.componentId = component.id;
      button.innerHTML = `
        <span class="layer-symbol">${escapeHtml(component.type.charAt(0).toUpperCase())}</span>
        <span>${escapeHtml(component.label)}</span>
        <span class="layer-state">${component.locked ? "LOCK" : component.visible ? "" : "HIDE"}</span>`;
      dom.layerList.append(button);
    }
  }

  function syncPositionFields(component) {
    if (component.id !== selectedId) return;
    document.getElementById("field-x").value = component.x;
    document.getElementById("field-y").value = component.y;
    document.getElementById("field-w").value = component.w;
    document.getElementById("field-h").value = component.h;
  }

  function updateScreenFields() {
    const screen = screenById();
    document.getElementById("background-select").value = screen.background;
    document.getElementById("scrim-range").value = screen.scrim;
    document.getElementById("scrim-output").textContent = `${screen.scrim}%`;
    document.getElementById("ui-scale-range").value = screen.uiScale;
    document.getElementById("ui-scale-output").textContent = `${screen.uiScale}%`;
    document.getElementById("safe-area-toggle").checked = state.showSafeArea;
  }

  function updateViewportTransform() {
    const design = defaults.designResolution;
    const rect = dom.viewportShell.getBoundingClientRect();
    const fit = Math.min((rect.width - 48) / design.width, (rect.height - 48) / design.height);
    effectiveZoom = zoomMode === "fit" ? Math.max(0.1, fit) : manualZoom;
    const width = design.width * effectiveZoom;
    const height = design.height * effectiveZoom;
    const left = Math.max(24, (rect.width - width) / 2);
    const top = Math.max(24, (rect.height - height) / 2);
    dom.viewport.style.left = `${left}px`;
    dom.viewport.style.top = `${top}px`;
    dom.viewport.style.transform = `scale(${effectiveZoom})`;
    document.getElementById("zoom-label").textContent = zoomMode === "fit" ? "Fit" : `${Math.round(effectiveZoom * 100)}%`;
  }

  function updateSelectedField(property, value) {
    const component = selectedComponent();
    if (!component) return;
    const before = deepClone(state);
    component[property] = value;
    if (["x", "y", "w", "h"].includes(property)) clampComponent(component);
    commitHistory(before);
    renderScreen();
    renderInspector();
  }

  function updateScreenProperty(property, value) {
    const before = deepClone(state);
    screenById()[property] = value;
    commitHistory(before);
    renderScreen();
  }

  function wireStaticEvents() {
    dom.nav.addEventListener("click", event => {
      const button = event.target.closest("[data-screen-id]");
      if (!button) return;
      activeScreenId = button.dataset.screenId;
      state.activeScreenId = activeScreenId;
      selectedId = null;
      renderAll();
      saveState();
    });

    dom.layerList.addEventListener("click", event => {
      const button = event.target.closest("[data-component-id]");
      if (button) selectComponent(button.dataset.componentId);
    });

    dom.viewport.addEventListener("pointerdown", event => {
      if (event.target === dom.viewport || event.target === dom.layer || event.target === dom.background || event.target === dom.scrim) {
        selectedId = null;
        renderScreen();
        renderInspector();
      }
    });

    document.querySelectorAll("[data-inspector-tab]").forEach(button => {
      button.addEventListener("click", () => {
        document.querySelectorAll("[data-inspector-tab]").forEach(item => item.classList.toggle("active", item === button));
        document.querySelectorAll(".inspector-panel").forEach(panel => panel.classList.toggle("active", panel.dataset.panel === button.dataset.inspectorTab));
      });
    });

    document.getElementById("mode-edit").addEventListener("click", () => setMode("edit"));
    document.getElementById("mode-preview").addEventListener("click", () => setMode("preview"));
    document.getElementById("undo-button").addEventListener("click", () => restoreHistory(historyIndex - 1));
    document.getElementById("redo-button").addEventListener("click", () => restoreHistory(historyIndex + 1));
    document.getElementById("zoom-fit").addEventListener("click", () => {
      zoomMode = "fit";
      updateViewportTransform();
    });
    document.getElementById("zoom-in").addEventListener("click", () => adjustZoom(0.05));
    document.getElementById("zoom-out").addEventListener("click", () => adjustZoom(-0.05));
    window.addEventListener("resize", updateViewportTransform);

    document.getElementById("viewport-select").addEventListener("change", event => {
      const [width, height] = event.target.value.split("x").map(Number);
      state.previewResolution = { width, height };
      saveState();
      showToast(`Preview target set to ${width} x ${height}`);
    });

    bindNumberField("field-x", "x");
    bindNumberField("field-y", "y");
    bindNumberField("field-w", "w", 40);
    bindNumberField("field-h", "h", 30);
    bindTextField("field-label", "label");
    bindTextField("field-runtime", "runtime");
    bindTextField("field-resource", "resource");
    bindNumberField("field-skin-slice", "skinSlice", 0);
    document.getElementById("clear-skin").addEventListener("click", () => {
      const component = selectedComponent();
      if (!component || !component.skin) return;
      const before = deepClone(state);
      delete component.skin;
      delete component.skinSlice;
      commitHistory(before);
      renderScreen();
      renderInspector();
      renderAssetGrid();
    });
    document.getElementById("franuka-skin-toggle").addEventListener("change", event => {
      state.franukaSkin = event.target.checked;
      applyTheme();
      saveState();
    });
    document.getElementById("field-anchor").addEventListener("change", event => updateSelectedField("anchor", event.target.value));
    document.getElementById("field-variant").addEventListener("change", event => updateSelectedField("variant", event.target.value));

    document.getElementById("toggle-lock").addEventListener("click", () => {
      const component = selectedComponent();
      if (component) updateSelectedField("locked", !component.locked);
    });
    document.getElementById("toggle-visible").addEventListener("click", () => {
      const component = selectedComponent();
      if (component) updateSelectedField("visible", !component.visible);
    });
    document.getElementById("duplicate-element").addEventListener("click", duplicateSelected);
    document.getElementById("reset-screen").addEventListener("click", resetCurrentScreen);

    document.getElementById("background-select").addEventListener("change", event => updateScreenProperty("background", event.target.value));
    document.getElementById("scrim-range").addEventListener("input", event => {
      const value = Number(event.target.value);
      screenById().scrim = value;
      document.getElementById("scrim-output").textContent = `${value}%`;
      dom.scrim.style.background = `rgba(7, 11, 8, ${value / 100})`;
      saveState();
    });
    document.getElementById("scrim-range").addEventListener("change", () => pushHistory(deepClone(state)));
    document.getElementById("ui-scale-range").addEventListener("input", event => {
      const value = Number(event.target.value);
      screenById().uiScale = value;
      document.getElementById("ui-scale-output").textContent = `${value}%`;
      dom.layer.style.transform = `scale(${value / 100})`;
      saveState();
    });
    document.getElementById("ui-scale-range").addEventListener("change", () => pushHistory(deepClone(state)));
    document.getElementById("safe-area-toggle").addEventListener("change", event => {
      state.showSafeArea = event.target.checked;
      dom.safeArea.classList.toggle("visible", state.showSafeArea);
      saveState();
    });

    ["wood", "canvas", "ember", "cream", "leaf", "health", "mana"].forEach(key => {
      document.getElementById(`theme-${key}`).addEventListener("input", event => {
        state.theme[key] = event.target.value;
        applyTheme();
        saveState();
      });
    });
    document.getElementById("theme-radius").addEventListener("input", event => {
      state.theme.radius = Number(event.target.value);
      applyTheme();
      saveState();
    });
    document.getElementById("reset-theme").addEventListener("click", () => {
      const before = deepClone(state);
      state.theme = deepClone(defaults.theme);
      commitHistory(before);
      renderAll();
    });

    document.getElementById("copy-screen-json").addEventListener("click", () => copyText(JSON.stringify(exportScreen(screenById()), null, 2), "Screen JSON copied"));
    document.getElementById("copy-pixellab-prompt").addEventListener("click", () => copyText(buildPixelLabPrompt(screenById()), "PixelLab prompt copied"));
    document.getElementById("download-project").addEventListener("click", () => downloadJson(buildExport(true, true, true)));
    document.getElementById("import-project").addEventListener("click", () => dom.importFile.click());
    dom.importFile.addEventListener("change", importJsonFile);

    document.getElementById("export-button").addEventListener("click", openExportDialog);
    document.getElementById("export-all").addEventListener("change", refreshExportPreview);
    document.getElementById("export-prompts").addEventListener("change", refreshExportPreview);
    document.getElementById("export-runtime").addEventListener("change", refreshExportPreview);
    document.getElementById("copy-export").addEventListener("click", () => copyText(dom.exportPreview.textContent, "Export JSON copied"));
    document.getElementById("confirm-download").addEventListener("click", () => {
      downloadJson(JSON.parse(dom.exportPreview.textContent));
      dom.exportDialog.close();
    });

    document.addEventListener("keydown", event => {
      const tag = document.activeElement?.tagName;
      if (tag === "INPUT" || tag === "SELECT" || tag === "TEXTAREA") return;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "z") {
        event.preventDefault();
        restoreHistory(event.shiftKey ? historyIndex + 1 : historyIndex - 1);
      }
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "y") {
        event.preventDefault();
        restoreHistory(historyIndex + 1);
      }
      if (event.key === "Escape" && mode === "preview") setMode("edit");
    });
  }

  function bindNumberField(id, property, min = null) {
    document.getElementById(id).addEventListener("change", event => {
      let value = Number(event.target.value);
      if (!Number.isFinite(value)) return;
      if (min !== null) value = Math.max(min, value);
      updateSelectedField(property, Math.round(value));
    });
  }

  function bindTextField(id, property) {
    document.getElementById(id).addEventListener("change", event => updateSelectedField(property, event.target.value.trim()));
  }

  function setMode(nextMode) {
    mode = nextMode;
    document.getElementById("mode-edit").classList.toggle("active", mode === "edit");
    document.getElementById("mode-preview").classList.toggle("active", mode === "preview");
    dom.viewport.classList.toggle("preview-mode", mode === "preview");
    if (mode === "preview") selectedId = null;
    renderScreen();
    renderInspector();
  }

  function adjustZoom(delta) {
    zoomMode = "manual";
    manualZoom = Math.max(0.2, Math.min(1, effectiveZoom + delta));
    updateViewportTransform();
  }

  function duplicateSelected() {
    const component = selectedComponent();
    if (!component) return;
    const before = deepClone(state);
    const copy = deepClone(component);
    const suffix = Date.now().toString(36);
    copy.id = `${component.id}-copy-${suffix}`;
    copy.label = `${component.label} Copy`;
    copy.x += 24;
    copy.y += 24;
    copy.z = Math.max(...screenById().components.map(item => item.z)) + 1;
    clampComponent(copy);
    screenById().components.push(copy);
    selectedId = copy.id;
    commitHistory(before);
    renderScreen();
    renderInspector();
  }

  function resetCurrentScreen() {
    const original = defaultScreenById();
    if (!original || !window.confirm(`Reset ${screenById().label} to its default layout?`)) return;
    const before = deepClone(state);
    const index = state.screens.findIndex(screen => screen.id === activeScreenId);
    state.screens[index] = deepClone(original);
    selectedId = null;
    commitHistory(before);
    renderAll();
    showToast("Screen reset");
  }

  function anchorTuple(anchor) {
    const map = {
      "top-left": [[0, 1], [0, 1], [0, 1]],
      "top-center": [[0.5, 1], [0.5, 1], [0.5, 1]],
      "top-right": [[1, 1], [1, 1], [1, 1]],
      "middle-left": [[0, 0.5], [0, 0.5], [0, 0.5]],
      center: [[0.5, 0.5], [0.5, 0.5], [0.5, 0.5]],
      "middle-right": [[1, 0.5], [1, 0.5], [1, 0.5]],
      "bottom-left": [[0, 0], [0, 0], [0, 0]],
      "bottom-center": [[0.5, 0], [0.5, 0], [0.5, 0]],
      "bottom-right": [[1, 0], [1, 0], [1, 0]]
    };
    return map[anchor] || map["top-left"];
  }

  function exportComponent(component) {
    const design = defaults.designResolution;
    const [anchorMin, anchorMax, pivot] = anchorTuple(component.anchor);
    const pivotPixel = {
      x: component.x + component.w * pivot[0],
      y: design.height - component.y - component.h * (1 - pivot[1])
    };
    const anchorPixel = {
      x: anchorMin[0] * design.width,
      y: anchorMin[1] * design.height
    };
    return {
      id: component.id,
      type: component.type,
      label: component.label,
      rect: {
        x: component.x,
        y: component.y,
        width: component.w,
        height: component.h,
        normalized: {
          x: round(component.x / design.width),
          y: round(component.y / design.height),
          width: round(component.w / design.width),
          height: round(component.h / design.height)
        },
        anchorMin,
        anchorMax,
        pivot,
        anchoredPosition: [
          Math.round(pivotPixel.x - anchorPixel.x),
          Math.round(pivotPixel.y - anchorPixel.y)
        ],
        sizeDelta: [component.w, component.h]
      },
      style: {
        variant: component.variant,
        visible: component.visible,
        locked: component.locked,
        z: component.z
      },
      runtimeNode: component.runtime,
      resourcesKey: component.resource || null,
      franukaSkin: component.skin
        ? { file: component.skin, slicePx: Number(component.skinSlice) || 0 }
        : null,
      props: component.props || {}
    };
  }

  function exportScreen(screen) {
    return {
      id: screen.id,
      label: screen.label,
      group: screen.group,
      background: screen.background,
      scrimPercent: screen.scrim,
      uiScalePercent: screen.uiScale,
      runtimeSurface: screen.runtimeSurface,
      components: screen.components.map(exportComponent)
    };
  }

  function buildExport(includeAll, includePrompts, includeRuntime) {
    const screens = includeAll ? state.screens : [screenById()];
    const payload = {
      $schema: "litiso-ui-export/v1",
      studioSchema: defaults.schema,
      generatedAt: new Date().toISOString(),
      designResolution: [defaults.designResolution.width, defaults.designResolution.height],
      previewResolution: [state.previewResolution.width, state.previewResolution.height],
      theme: deepClone(state.theme),
      assetPack: window.FRANUKA_ASSETS ? deepClone(window.FRANUKA_ASSETS.credit) : null,
      screens: screens.map(exportScreen),
      bindings: deepClone(defaults.catalogs.bindings)
    };
    if (includePrompts) {
      payload.pixelLab = screens.map(screen => ({
        screenId: screen.id,
        prompt: buildPixelLabPrompt(screen),
        resources: screen.components.map(item => item.resource).filter(Boolean)
      }));
    }
    if (includeRuntime) {
      payload.runtime = screens.map(screen => ({
        screenId: screen.id,
        target: screen.runtimeSurface,
        notes: screen.runtimeNotes
      }));
    }
    return payload;
  }

  function buildPixelLabPrompt(screen) {
    const resources = screen.components.map(item => item.resource).filter(Boolean);
    return [
      `Create original production-ready pixel-art UI assets for the LIT-RPG ${screen.label} screen.`,
      `Direction: warm cozy indie survival-crafting game, medium brown wood, stitched forest-green canvas, cream markings, ember-orange focus, crisp pixel edges.`,
      `Scene or kit brief: ${screen.pixelLabBrief}.`,
      resources.length ? `Required transparent asset IDs: ${resources.join(", ")}.` : "",
      "No text baked into UI sprites. Keep corners square or lightly beveled. Each component must be isolated, centered, and suitable for Unity 9-slicing where applicable.",
      "Avoid copied game assets, MMO ornament, gothic black-and-gold framing, purple gradients, smooth vector rendering, excessive filigree, watermarks."
    ].filter(Boolean).join(" ");
  }

  function round(value) {
    return Math.round(value * 100000) / 100000;
  }

  function openExportDialog() {
    refreshExportPreview();
    dom.exportDialog.showModal();
  }

  function refreshExportPreview() {
    const payload = buildExport(
      document.getElementById("export-all").checked,
      document.getElementById("export-prompts").checked,
      document.getElementById("export-runtime").checked
    );
    dom.exportPreview.textContent = JSON.stringify(payload, null, 2);
  }

  async function copyText(text, successMessage) {
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(text);
      } else {
        const area = document.createElement("textarea");
        area.value = text;
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.append(area);
        area.select();
        document.execCommand("copy");
        area.remove();
      }
      showToast(successMessage);
    } catch (error) {
      console.warn("Clipboard copy failed.", error);
      showToast("Clipboard unavailable; use Download JSON");
    }
  }

  function downloadJson(payload) {
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `lit-rpg-ui-handoff-${new Date().toISOString().slice(0, 10)}.json`;
    document.body.append(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    showToast("Unity handoff downloaded");
  }

  function importJsonFile(event) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const payload = JSON.parse(String(reader.result));
        validateImport(payload);
        const before = deepClone(state);
        if (payload.$schema === "litiso-ui-export/v1") {
          const imported = initialState();
          imported.theme = Object.assign(imported.theme, payload.theme || {});
          for (const screenPayload of payload.screens || []) {
            const target = imported.screens.find(screen => screen.id === screenPayload.id);
            if (!target) continue;
            target.background = screenPayload.background || target.background;
            target.scrim = Number(screenPayload.scrimPercent ?? target.scrim);
            target.uiScale = Number(screenPayload.uiScalePercent ?? target.uiScale);
            const componentMap = new Map(target.components.map(item => [item.id, item]));
            for (const node of screenPayload.components || []) {
              const component = componentMap.get(node.id);
              if (!component) continue;
              component.x = Number(node.rect?.x ?? component.x);
              component.y = Number(node.rect?.y ?? component.y);
              component.w = Number(node.rect?.width ?? component.w);
              component.h = Number(node.rect?.height ?? component.h);
              component.anchor = anchorFromTuple(node.rect?.pivot) || component.anchor;
              component.variant = node.style?.variant || component.variant;
              component.visible = node.style?.visible !== false;
              component.locked = node.style?.locked === true;
              component.runtime = node.runtimeNode || component.runtime;
              component.resource = node.resourcesKey || component.resource;
              if (node.franukaSkin && node.franukaSkin.file) {
                component.skin = node.franukaSkin.file;
                component.skinSlice = Number(node.franukaSkin.slicePx) || 0;
              }
            }
          }
          state = imported;
        } else if ((payload.studioSchema === defaults.schema || payload.schema === defaults.schema) && Array.isArray(payload.screens)) {
          state = mergeState(initialState(), payload);
        }
        activeScreenId = state.activeScreenId || state.screens[0].id;
        selectedId = null;
        commitHistory(before);
        renderAll();
        showToast("Handoff imported");
      } catch (error) {
        console.error(error);
        showToast(`Import failed: ${error.message}`);
      }
    };
    reader.readAsText(file);
  }

  function validateImport(payload) {
    if (!payload || typeof payload !== "object") throw new Error("JSON root must be an object");
    if (payload.$schema !== "litiso-ui-export/v1" && payload.studioSchema !== defaults.schema && payload.schema !== defaults.schema) {
      throw new Error("Unsupported UI Studio schema");
    }
    if (!Array.isArray(payload.screens)) throw new Error("Missing screens array");
    const ids = new Set();
    for (const screen of payload.screens) {
      if (!screen.id || ids.has(screen.id)) throw new Error("Screen IDs must be unique");
      ids.add(screen.id);
    }
  }

  function anchorFromTuple(pivot) {
    if (!Array.isArray(pivot) || pivot.length !== 2) return null;
    const key = `${pivot[0]},${pivot[1]}`;
    return {
      "0,1": "top-left",
      "0.5,1": "top-center",
      "1,1": "top-right",
      "0,0.5": "middle-left",
      "0.5,0.5": "center",
      "1,0.5": "middle-right",
      "0,0": "bottom-left",
      "0.5,0": "bottom-center",
      "1,0": "bottom-right"
    }[key] || null;
  }

  const assetDefaultSlice = {
    "background-boxes": 16,
    "buttons": 6,
    "title-banners": 8,
    "sliders-bars": 6,
    "item-slots": 5
  };
  const assetFilter = { search: "", category: "", scale: "1x" };

  function setupAssetBrowser() {
    const manifest = window.FRANUKA_ASSETS;
    if (!manifest) return;
    const categorySelect = document.getElementById("asset-category");
    for (const category of manifest.categories) {
      const option = document.createElement("option");
      option.value = category.id;
      option.textContent = `${category.label} (${category.files.length})`;
      categorySelect.append(option);
    }
    document.getElementById("asset-search").addEventListener("input", event => {
      assetFilter.search = event.target.value.trim().toLowerCase();
      renderAssetGrid();
    });
    categorySelect.addEventListener("change", event => {
      assetFilter.category = event.target.value;
      renderAssetGrid();
    });
    document.getElementById("asset-scale").addEventListener("change", event => {
      assetFilter.scale = event.target.value;
      renderAssetGrid();
    });
    document.getElementById("asset-grid").addEventListener("click", event => {
      const tile = event.target.closest("[data-asset-path]");
      if (tile) assignSkin(tile.dataset.assetPath, tile.dataset.assetCategory);
    });
    renderAssetGrid();
  }

  function renderAssetGrid() {
    const manifest = window.FRANUKA_ASSETS;
    const grid = document.getElementById("asset-grid");
    if (!manifest || !grid) return;
    const limit = 240;
    const rows = [];
    for (const category of manifest.categories) {
      if (assetFilter.category && category.id !== assetFilter.category) continue;
      for (const file of category.files) {
        if (assetFilter.search && !file.toLowerCase().includes(assetFilter.search)) continue;
        rows.push({ category, file });
      }
    }
    const selectedSkin = selectedComponent()?.skin || "";
    grid.innerHTML = rows.slice(0, limit).map(({ category, file }) => {
      const path = `${manifest.base}${assetFilter.scale}/${category.id}/${file}`;
      return `<button type="button" class="asset-tile${selectedSkin === path ? " assigned" : ""}"
        data-asset-path="${path}" data-asset-category="${category.id}" title="${file}">
        <img src="${encodeURI(path)}" alt="" loading="lazy"><span>${file.replace(/\.png$/, "")}</span></button>`;
    }).join("");
    document.getElementById("asset-count").textContent = rows.length > limit
      ? `Showing ${limit} of ${rows.length} assets — refine the search`
      : `${rows.length} assets`;
  }

  function assignSkin(path, categoryId) {
    const component = selectedComponent();
    if (!component) {
      showToast("Select a component first (Element tab)");
      return;
    }
    const before = deepClone(state);
    component.skin = path;
    if (component.skinSlice === undefined) component.skinSlice = assetDefaultSlice[categoryId] ?? 0;
    commitHistory(before);
    renderScreen();
    renderInspector();
    renderAssetGrid();
    showToast(`Skin set: ${path.split("/").pop()}`);
  }

  document.getElementById("viewport-select").value = `${state.previewResolution.width}x${state.previewResolution.height}`;
  wireStaticEvents();
  setupAssetBrowser();
  pushHistory(deepClone(state));
  renderAll();
}());

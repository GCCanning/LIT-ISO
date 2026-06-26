"use strict";

const LAYERS = [
  "00undr", "01body", "02sock", "03fot1", "04lwr1",
  "05shrt", "06lwr2", "07fot2", "08lwr3", "09hand",
  "10outr", "11neck", "12face", "13hair", "14head"
];

const ANIMATIONS = [
  { name: "idle", length: 1.00, frameTime: 1, frames: [0], flip: 0 },
  { name: "walk_down", length: 0.81, frameTime: 0.135, frames: [48, 49, 50, 48, 49, 50], flip: [0, 0, 0, 1, 1, 1] },
  { name: "walk_up", length: 0.81, frameTime: 0.135, frames: [52, 53, 54, 52, 53, 54], flip: [0, 0, 0, 1, 1, 1] },
  { name: "walk_right", length: 0.81, frameTime: 0.135, frames: [64, 65, 66, 67, 68, 69], flip: 0 },
  { name: "run_down", length: 0.50, frameTime: [0, 0.080, 0.135, 0.250, 0.330, 0.385], frames: [48, 49, 51, 48, 49, 51], flip: [0, 0, 0, 1, 1, 1] },
  { name: "run_up", length: 0.50, frameTime: [0, 0.080, 0.135, 0.250, 0.330, 0.385], frames: [52, 53, 55, 52, 53, 55], flip: [0, 0, 0, 1, 1, 1] },
  { name: "run_right", length: 0.50, frameTime: [0, 0.080, 0.135, 0.250, 0.330, 0.385], frames: [64, 65, 70, 67, 68, 71], flip: 0 },
  { name: "death", length: 1.00, frameTime: [0, 0.2, 0.4, 0.5, 0.7], frames: [178, 179, 180, 179, 180], flip: 0 },
  { name: "forehand_strike", length: 0.64, frameTime: [0, 0.18, 0.26, 0.34], frames: [131, 132, 133, 133], flip: 0 }
];

const state = {
  assets: new Map(),
  selected: Object.fromEntries(LAYERS.map(layer => [layer, null])),
  styles: Object.fromEntries(LAYERS.map(layer => [layer, 0])),
  frameIndex: 0,
  playing: true,
  animationName: "idle",
  zoom: 5,
  startedAt: performance.now(),
  cellW: 16,
  cellH: 16
};

const stage = document.getElementById("stage");
const ctx = stage.getContext("2d");
ctx.imageSmoothingEnabled = false;

const el = {
  folderInput: document.getElementById("folderInput"),
  folderStatus: document.getElementById("folderStatus"),
  layers: document.getElementById("layers"),
  animationSelect: document.getElementById("animationSelect"),
  frameSlider: document.getElementById("frameSlider"),
  zoomSlider: document.getElementById("zoomSlider"),
  playToggle: document.getElementById("playToggle"),
  randomLayers: document.getElementById("randomLayers"),
  randomStyles: document.getElementById("randomStyles"),
  exportPng: document.getElementById("exportPng"),
  exportStrip: document.getElementById("exportStrip"),
  exportPreset: document.getElementById("exportPreset"),
  presetInput: document.getElementById("presetInput"),
  exportLitIso: document.getElementById("exportLitIso")
};

function layerLabel(layer) {
  const labels = {
    "00undr": "Undergarment", "01body": "Body", "02sock": "Socks", "03fot1": "Footwear A",
    "04lwr1": "Lower A", "05shrt": "Shirt", "06lwr2": "Lower B", "07fot2": "Footwear B",
    "08lwr3": "Lower C", "09hand": "Hands", "10outr": "Outerwear", "11neck": "Neck",
    "12face": "Face", "13hair": "Hair", "14head": "Headwear"
  };
  return `${layer} - ${labels[layer] || layer}`;
}

function currentAnimation() {
  return ANIMATIONS.find(animation => animation.name === state.animationName) || ANIMATIONS[0];
}

function keyTime(animation, index) {
  return Array.isArray(animation.frameTime) ? animation.frameTime[index] : index * animation.frameTime;
}

function frameFlip(animation, index) {
  return Array.isArray(animation.flip) ? animation.flip[index] : animation.flip;
}

function selectedFrameFromClock(now) {
  const animation = currentAnimation();
  const elapsed = ((now - state.startedAt) / 1000) % Math.max(animation.length, 0.001);
  let index = 0;
  for (let i = 0; i < animation.frames.length; i += 1) {
    if (elapsed >= keyTime(animation, i)) index = i;
  }
  return index;
}

function makeAsset(layer, file) {
  const prefix = `fbas_${layer}_`;
  let name = file.name.endsWith(".png") ? file.name.slice(0, -4) : file.name;
  if (name.startsWith(prefix)) name = name.slice(prefix.length);
  const img = new Image();
  img.decoding = "async";
  img.src = URL.createObjectURL(file);
  img.onload = () => {
    state.cellW = Math.max(1, Math.floor(img.naturalWidth / 16));
    state.cellH = Math.max(1, Math.floor(img.naturalHeight / 16));
    draw();
  };
  return { layer, name, fileName: file.name, path: file.webkitRelativePath || file.name, img };
}

function parseFolder(files) {
  state.assets.clear();
  for (const layer of LAYERS) state.assets.set(layer, []);

  for (const file of files) {
    if (!file.name.toLowerCase().endsWith(".png")) continue;
    const parts = (file.webkitRelativePath || file.name).split(/[\\/]/);
    const layer = parts.find(part => LAYERS.includes(part));
    if (!layer || !file.name.startsWith(`fbas_${layer}_`)) continue;
    state.assets.get(layer).push(makeAsset(layer, file));
  }

  for (const layer of LAYERS) {
    const rows = state.assets.get(layer);
    rows.sort((a, b) => a.name.localeCompare(b.name));
    state.selected[layer] = layer === "01body" && rows[0] ? rows[0].name : null;
    state.styles[layer] = 0;
  }

  const found = [...state.assets.values()].reduce((sum, rows) => sum + rows.length, 0);
  el.folderStatus.textContent = found
    ? `Loaded ${found} layer PNGs across ${[...state.assets.values()].filter(rows => rows.length).length} folders.`
    : "No matching fbas_* layer PNGs found.";
  renderLayerControls();
  draw();
}

function renderAnimationOptions() {
  el.animationSelect.innerHTML = "";
  for (const animation of ANIMATIONS) {
    const option = document.createElement("option");
    option.value = animation.name;
    option.textContent = animation.name;
    el.animationSelect.appendChild(option);
  }
  el.animationSelect.value = state.animationName;
  updateFrameSlider();
}

function renderLayerControls() {
  el.layers.innerHTML = "";
  for (const layer of LAYERS) {
    const assets = state.assets.get(layer) || [];
    const wrap = document.createElement("section");
    wrap.className = "layer";

    const head = document.createElement("div");
    head.className = "layerHead";
    const title = document.createElement("div");
    title.className = "layerName";
    title.textContent = layerLabel(layer);
    const count = document.createElement("div");
    count.className = "muted";
    count.textContent = `${assets.length}`;
    head.append(title, count);

    const select = document.createElement("select");
    if (layer !== "01body") {
      const none = document.createElement("option");
      none.value = "";
      none.textContent = "None";
      select.appendChild(none);
    }
    for (const asset of assets) {
      const option = document.createElement("option");
      option.value = asset.name;
      option.textContent = asset.name;
      select.appendChild(option);
    }
    select.value = state.selected[layer] || "";
    select.disabled = assets.length === 0;
    select.addEventListener("change", () => {
      state.selected[layer] = select.value || null;
      if (layer === "14head" && select.value.endsWith("_e")) state.selected["13hair"] = null;
      renderLayerControls();
      draw();
    });

    const styleLabel = document.createElement("label");
    styleLabel.textContent = `Style tint ${state.styles[layer]}`;
    const style = document.createElement("input");
    style.type = "range";
    style.min = "0";
    style.max = layer === "13hair" || layer === "01body" ? "57" : "47";
    style.value = state.styles[layer];
    style.addEventListener("input", () => {
      state.styles[layer] = Number(style.value);
      styleLabel.textContent = `Style tint ${state.styles[layer]}`;
      draw();
    });
    styleLabel.appendChild(style);

    wrap.append(head, select, styleLabel);
    el.layers.appendChild(wrap);
  }
}

function updateFrameSlider() {
  const animation = currentAnimation();
  el.frameSlider.max = String(Math.max(0, animation.frames.length - 1));
  if (state.frameIndex >= animation.frames.length) state.frameIndex = 0;
  el.frameSlider.value = String(state.frameIndex);
}

function assetFor(layer) {
  const selected = state.selected[layer];
  if (!selected) return null;
  return (state.assets.get(layer) || []).find(asset => asset.name === selected) || null;
}

function layerFilter(layer) {
  const style = state.styles[layer] || 0;
  if (style <= 0) return "none";
  const hue = (style * 19 + LAYERS.indexOf(layer) * 11) % 360;
  const sat = layer === "01body" ? 0.92 : 1.12;
  const brightness = 0.92 + ((style % 7) * 0.025);
  return `hue-rotate(${hue}deg) saturate(${sat}) brightness(${brightness})`;
}

function drawComposite(targetCtx, frameNumber, flip, scale, pad) {
  const w = state.cellW;
  const h = state.cellH;
  targetCtx.save();
  targetCtx.imageSmoothingEnabled = false;
  targetCtx.translate(pad, pad);
  targetCtx.scale(scale, scale);
  if (flip) {
    targetCtx.translate(w, 0);
    targetCtx.scale(-1, 1);
  }
  const sx = (frameNumber % 16) * w;
  const sy = Math.floor(frameNumber / 16) * h;
  for (const layer of LAYERS) {
    const asset = assetFor(layer);
    if (!asset || !asset.img.complete) continue;
    targetCtx.filter = layerFilter(layer);
    targetCtx.drawImage(asset.img, sx, sy, w, h, 0, 0, w, h);
  }
  targetCtx.restore();
  targetCtx.filter = "none";
}

function draw() {
  const animation = currentAnimation();
  const index = Math.min(state.frameIndex, animation.frames.length - 1);
  const frame = animation.frames[index] || 0;
  const flip = frameFlip(animation, index);
  const scale = state.zoom;
  const pad = Math.ceil(16 * scale);
  stage.width = state.cellW * scale + pad * 2;
  stage.height = state.cellH * scale + pad * 2;
  ctx.clearRect(0, 0, stage.width, stage.height);
  ctx.fillStyle = "#0c0e12";
  ctx.fillRect(0, 0, stage.width, stage.height);
  ctx.strokeStyle = "#2f3641";
  ctx.strokeRect(pad - 0.5, pad - 0.5, state.cellW * scale + 1, state.cellH * scale + 1);
  drawComposite(ctx, frame, flip, scale, pad);
}

function tick(now) {
  if (state.playing) {
    const next = selectedFrameFromClock(now);
    if (next !== state.frameIndex) {
      state.frameIndex = next;
      el.frameSlider.value = String(next);
      draw();
    }
  }
  requestAnimationFrame(tick);
}

function downloadCanvas(canvas, name) {
  const a = document.createElement("a");
  a.href = canvas.toDataURL("image/png");
  a.download = name;
  a.click();
}

function downloadText(text, name, type) {
  const blob = new Blob([text], { type });
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

function exportStrip() {
  const animation = currentAnimation();
  const scale = Number(state.zoom);
  const pad = 2 * scale;
  const out = document.createElement("canvas");
  out.width = animation.frames.length * (state.cellW * scale + pad * 2);
  out.height = state.cellH * scale + pad * 2;
  const outCtx = out.getContext("2d");
  outCtx.imageSmoothingEnabled = false;
  outCtx.clearRect(0, 0, out.width, out.height);
  animation.frames.forEach((frame, index) => {
    outCtx.save();
    outCtx.translate(index * (state.cellW * scale + pad * 2), 0);
    drawComposite(outCtx, frame, frameFlip(animation, index), scale, pad);
    outCtx.restore();
  });
  downloadCanvas(out, `farmer_${animation.name}_strip.png`);
}

function renderApprox8D() {
  const mapping = [
    ["S", "walk_down", false],
    ["SE", "walk_down", false],
    ["E", "walk_right", false],
    ["NE", "walk_up", false],
    ["N", "walk_up", false],
    ["NW", "walk_up", true],
    ["W", "walk_right", true],
    ["SW", "walk_down", true]
  ];
  const framesPerRow = 4;
  const out = document.createElement("canvas");
  out.width = state.cellW * framesPerRow;
  out.height = state.cellH * mapping.length;
  const outCtx = out.getContext("2d");
  outCtx.imageSmoothingEnabled = false;
  outCtx.clearRect(0, 0, out.width, out.height);
  const oldAnimation = state.animationName;
  const oldFrame = state.frameIndex;
  mapping.forEach((row, rowIndex) => {
    const animation = ANIMATIONS.find(item => item.name === row[1]);
    for (let col = 0; col < framesPerRow; col += 1) {
      const srcIndex = Math.floor(col * animation.frames.length / framesPerRow);
      const frame = animation.frames[srcIndex];
      const flip = Boolean(frameFlip(animation, srcIndex)) !== row[2];
      outCtx.save();
      outCtx.translate(col * state.cellW, rowIndex * state.cellH);
      drawComposite(outCtx, frame, flip, 1, 0);
      outCtx.restore();
    }
  });
  state.animationName = oldAnimation;
  state.frameIndex = oldFrame;
  return { canvas: out, mapping: mapping.map(row => ({ direction: row[0], sourceAnimation: row[1], mirrored: row[2] })) };
}

function exportLitIsoApprox() {
  const rendered = renderApprox8D();
  downloadCanvas(rendered.canvas, "farmer_litiso_8d_walk_approx.png");
  downloadText(JSON.stringify({
    note: "Approximation only. Source has down/up/right movement; diagonals reuse nearest directions.",
    cellWidth: state.cellW,
    cellHeight: state.cellH,
    rows: rendered.mapping,
    selected: state.selected,
    styles: state.styles
  }, null, 2), "farmer_litiso_8d_walk_approx_manifest.json", "application/json");
}

function exportPreset() {
  downloadText(JSON.stringify({
    tool: "LIT-ISO Farmer Character Builder",
    selected: state.selected,
    styles: state.styles,
    animationName: state.animationName
  }, null, 2), "farmer_character_preset.json", "application/json");
}

function loadPreset(file) {
  const reader = new FileReader();
  reader.onload = () => {
    const preset = JSON.parse(String(reader.result));
    for (const layer of LAYERS) {
      if (Object.prototype.hasOwnProperty.call(preset.selected || {}, layer)) {
        state.selected[layer] = preset.selected[layer];
      }
      if (Object.prototype.hasOwnProperty.call(preset.styles || {}, layer)) {
        state.styles[layer] = Number(preset.styles[layer]) || 0;
      }
    }
    if (preset.animationName) state.animationName = preset.animationName;
    renderLayerControls();
    renderAnimationOptions();
    draw();
  };
  reader.readAsText(file);
}

function randomChoice(items) {
  return items[Math.floor(Math.random() * items.length)];
}

el.folderInput.addEventListener("change", event => parseFolder([...event.target.files]));
el.animationSelect.addEventListener("change", () => {
  state.animationName = el.animationSelect.value;
  state.frameIndex = 0;
  state.startedAt = performance.now();
  updateFrameSlider();
  draw();
});
el.frameSlider.addEventListener("input", () => {
  state.playing = false;
  el.playToggle.textContent = "Play";
  el.playToggle.classList.remove("on");
  state.frameIndex = Number(el.frameSlider.value);
  draw();
});
el.zoomSlider.addEventListener("input", () => {
  state.zoom = Number(el.zoomSlider.value);
  draw();
});
el.playToggle.addEventListener("click", () => {
  state.playing = !state.playing;
  state.startedAt = performance.now();
  el.playToggle.textContent = state.playing ? "Pause" : "Play";
  el.playToggle.classList.toggle("on", state.playing);
});
el.randomLayers.addEventListener("click", () => {
  for (const layer of LAYERS) {
    const assets = state.assets.get(layer) || [];
    if (!assets.length) continue;
    if (layer !== "01body" && Math.random() < 0.25) state.selected[layer] = null;
    else state.selected[layer] = randomChoice(assets).name;
  }
  if ((state.selected["14head"] || "").endsWith("_e")) state.selected["13hair"] = null;
  renderLayerControls();
  draw();
});
el.randomStyles.addEventListener("click", () => {
  for (const layer of LAYERS) {
    const max = layer === "13hair" || layer === "01body" ? 57 : 47;
    state.styles[layer] = Math.floor(Math.random() * max);
  }
  renderLayerControls();
  draw();
});
el.exportPng.addEventListener("click", () => downloadCanvas(stage, `farmer_${state.animationName}_frame_${state.frameIndex}.png`));
el.exportStrip.addEventListener("click", exportStrip);
el.exportPreset.addEventListener("click", exportPreset);
el.exportLitIso.addEventListener("click", exportLitIsoApprox);
el.presetInput.addEventListener("change", event => {
  const file = event.target.files[0];
  if (file) loadPreset(file);
});

renderAnimationOptions();
renderLayerControls();
draw();
requestAnimationFrame(tick);

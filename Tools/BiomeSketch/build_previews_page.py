#!/usr/bin/env python3
"""Build a self-contained Tools/BiomeSketch/previews.html gallery.

Combines:
  - Tools/WorldGenPreview close-up renders (real tile/prop art, isometric
    composites) -- previews/closeups.json
  - Tools/WorldGenPreview rule-preview renders (macro/blend/town demos)
    -- previews/previews.js

All images are embedded as base64 data URIs so the page works as a single
file (no relative-path issues). Close-ups are downscaled for the embed to
keep the HTML a reasonable size; full-resolution PNGs remain on disk in
previews/.

Run: python3 Tools/BiomeSketch/build_previews_page.py
"""
import base64
import io
import json
import os

try:
    from PIL import Image
except ImportError:
    Image = None

HERE = os.path.dirname(os.path.abspath(__file__))
PREVIEWS_DIR = os.path.join(HERE, "previews")
CLOSEUPS_JSON = os.path.join(PREVIEWS_DIR, "closeups.json")
RULE_JS = os.path.join(PREVIEWS_DIR, "previews.js")
OUT_HTML = os.path.join(HERE, "previews.html")

CLOSEUP_MAX_W = 1400  # downscale embed width for close-ups (full-res stays on disk)


def load_json_or_jsish(path, prefix, suffix):
    with open(path, "r", encoding="utf-8") as f:
        text = f.read()
    start = text.find("{")
    end = text.rfind("}")
    text = text[start:end + 1]
    return json.loads(text)


def embed(rel_path, max_w=None):
    abs_path = os.path.join(HERE, rel_path)
    if max_w and Image is not None:
        im = Image.open(abs_path)
        if im.width > max_w:
            ratio = max_w / im.width
            im = im.resize((max_w, max(1, int(im.height * ratio))), Image.NEAREST)
        buf = io.BytesIO()
        im.save(buf, format="PNG")
        data = buf.getvalue()
    else:
        with open(abs_path, "rb") as f:
            data = f.read()
    return "data:image/png;base64," + base64.b64encode(data).decode("ascii")


def main():
    with open(CLOSEUPS_JSON, "r", encoding="utf-8") as f:
        closeups = json.load(f)

    rule_data = load_json_or_jsish(RULE_JS, "const RULE_PREVIEWS = ", "")
    rule_items = rule_data["items"]

    closeup_cards = []
    for item in closeups:
        closeup_cards.append({
            "src": embed(item["file"], max_w=CLOSEUP_MAX_W),
            "title": item["title"],
            "note": item["note"],
        })

    rule_cards = []
    for item in rule_items:
        rule_cards.append({
            "src": embed(item["file"]),
            "title": item["title"],
            "note": item["note"],
        })

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>LIT-ISO — Worldgen Preview Gallery</title>
<style>
  :root {{ --bg:#14171c; --panel:#1c2128; --line:#323a44; --text:#d8dde3;
           --dim:#8a93a0; --accent:#e8a33d; }}
  * {{ box-sizing:border-box; }}
  body {{ margin:0; background:var(--bg); color:var(--text);
          font:13px/1.5 "Segoe UI", system-ui, sans-serif; }}
  header {{ padding:14px 18px; background:var(--panel); border-bottom:1px solid var(--line); }}
  header h1 {{ margin:0 0 6px; font-size:17px; color:var(--accent); }}
  header p {{ margin:2px 0; color:var(--dim); font-size:12.5px; }}
  header a {{ color:var(--accent); }}
  h2.section {{ margin:22px 18px 8px; font-size:14px; color:var(--accent);
                 border-bottom:1px solid var(--line); padding-bottom:6px; }}
  #grid {{ display:grid; grid-template-columns:repeat(auto-fill, minmax(320px, 1fr));
           gap:14px; padding:14px 18px; }}
  .item {{ background:var(--panel); border:1px solid var(--line); border-radius:8px;
           overflow:hidden; cursor:zoom-in; }}
  .item img {{ width:100%; display:block; image-rendering:pixelated; background:#0a0c10; }}
  .item .cap {{ padding:8px 10px; }}
  .item .cap h3 {{ margin:0 0 4px; font-size:12.5px; color:var(--text); }}
  .item .cap p {{ margin:0; font-size:11.5px; color:var(--dim); line-height:1.4; }}
  #zoom {{ position:fixed; inset:0; background:rgba(8,9,11,.92); display:none;
           align-items:center; justify-content:center; cursor:zoom-out; z-index:50; }}
  #zoom.show {{ display:flex; }}
  #zoom img {{ max-width:96vw; max-height:92vh; image-rendering:pixelated; }}
</style>
</head>
<body>
<header>
  <h1>LIT-ISO — Worldgen Preview Gallery</h1>
  <p>Close-ups use real tile/prop art composited isometrically from
     Assets/Resources/Tiles + Decorations. Rule previews are a 256-cell
     prototype render of <code>Docs/WORLDGEN_RULES_PROPOSAL.md</code> (not the
     runtime sampler). Companion editor: <a href="rules.html">rules.html</a>
     (drag-drop biome/tile/prop triage).</p>
  <p>Click any image to zoom. Regenerate with
     <code>python3 Tools/WorldGenPreview/render_closeups.py &amp;&amp;
     python3 Tools/WorldGenPreview/preview_proposed_rules.py &amp;&amp;
     python3 Tools/BiomeSketch/build_previews_page.py</code></p>
</header>

<h2 class="section">Close-ups — real tile/prop art</h2>
<div id="grid-closeups" class="grid"></div>

<h2 class="section">Rule previews — macro layout, blends, towns</h2>
<div id="grid-rules" class="grid"></div>

<div id="zoom"><img id="zoomImg" src="" alt=""></div>

<script>
const CLOSEUPS = {json.dumps(closeup_cards)};
const RULES = {json.dumps(rule_cards)};

function buildGrid(targetId, items) {{
  const el = document.getElementById(targetId);
  el.className = "grid";
  el.style.display = "grid";
  el.style.gridTemplateColumns = "repeat(auto-fill, minmax(320px, 1fr))";
  el.style.gap = "14px";
  el.style.padding = "0 18px 14px";
  let html = "";
  for (const it of items) {{
    html += `<div class="item" data-src="${{it.src}}">
      <img loading="lazy" src="${{it.src}}" alt="">
      <div class="cap"><h3>${{it.title}}</h3><p>${{it.note}}</p></div>
    </div>`;
  }}
  el.innerHTML = html;
}}
buildGrid("grid-closeups", CLOSEUPS);
buildGrid("grid-rules", RULES);

const zoom = document.getElementById("zoom");
const zoomImg = document.getElementById("zoomImg");
document.body.addEventListener("click", e => {{
  const item = e.target.closest(".item");
  if (item) {{
    zoomImg.src = item.dataset.src;
    zoom.classList.add("show");
    return;
  }}
  if (e.target.closest("#zoom")) zoom.classList.remove("show");
}});
</script>
</body>
</html>
"""
    with open(OUT_HTML, "w", encoding="utf-8") as f:
        f.write(html)
    print("wrote", OUT_HTML, f"({os.path.getsize(OUT_HTML)/1024:.0f} KB)")


if __name__ == "__main__":
    main()

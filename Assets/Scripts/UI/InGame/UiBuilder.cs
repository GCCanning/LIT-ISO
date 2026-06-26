using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Shared uGUI construction helpers used by every HUD view and panel screen.
    ///
    /// Component grammar (from the approved LIT-ISO front-end spec):
    ///   Panels  — 3px ink outer border + 1px steel inset + 1px gold hairline inset,
    ///             gold diamond stud in each corner, Panel (#15171C) fill. Square corners only.
    ///   Buttons — Raised fill, 2px ink border, 1px bevel, 6px hard bottom shadow that
    ///             collapses to 1px on :active (LitIsoButtonPress handles the press).
    ///   Slots   — Panel2 fill, rarity-colour 1px border, durability fill strip at bottom.
    /// </summary>
    internal static class UiBuilder
    {
        // Palette aliases sourced from LitIsoTheme so every view matches the design system.
        internal static readonly Color TextCol  = LitIsoTheme.Parchment;
        internal static readonly Color MutedCol = LitIsoTheme.WarmTan;
        internal static readonly Color PanelBg  = LitIsoTheme.Panel;
        internal static readonly Color Border   = LitIsoTheme.Stone;
        internal static readonly Color Scrim    = new Color(0f, 0f, 0f, 0.62f);
        internal static readonly Color SlotBg   = LitIsoTheme.Panel2;
        internal static readonly Color Select   = LitIsoTheme.Gold;

        // ─── resource loading ────────────────────────────────────────────────────

        /// <summary>Sprite from Resources/UI/InGame/&lt;name&gt; — null if absent.</summary>
        internal static Sprite Spr(string name) =>
            Resources.Load<Sprite>("UI/InGame/" + name);

        // ─── canvas / scaler tracking ─────────────────────────────────────────────

        static readonly System.Collections.Generic.List<CanvasScaler> s_scalers
            = new System.Collections.Generic.List<CanvasScaler>();

        internal static Canvas NewCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode  = RenderMode.ScreenSpaceOverlay;
            c.pixelPerfect = true;
            c.sortingOrder = sortingOrder;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.matchWidthOrHeight = 0.5f;
            s_scalers.Add(s);
            ApplyScaleTo(s, CurrentUiScale());
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        internal static float CurrentUiScale() =>
            Mathf.Clamp(PlayerPrefs.GetFloat("ui.scale", 1f), 0.75f, 1.75f);

        static void ApplyScaleTo(CanvasScaler s, float scale) =>
            s.referenceResolution = new Vector2(1920f / scale, 1080f / scale);

        internal static void ApplyUiScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.75f, 1.75f);
            s_scalers.RemoveAll(s => s == null);
            foreach (var s in s_scalers) ApplyScaleTo(s, scale);
        }

        // ─── primitive builders ───────────────────────────────────────────────────

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        internal static Image NewImage(Transform parent, string name, Sprite sprite, Color fallback)
        {
            var go  = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color  = sprite != null ? Color.white : fallback;
            return img;
        }

        /// <summary>Stretches the rect to fill its parent with optional uniform padding.</summary>
        internal static RectTransform Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
            return rt;
        }

        // ─── text ─────────────────────────────────────────────────────────────────

        internal static Text NewText(Transform parent, string name, string value,
            int size, TextAnchor anchor, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t  = go.AddComponent<Text>();
            t.text      = value;
            t.alignment = anchor;
            t.color     = color ?? TextCol;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Truncate;
            if (size >= 19) LitIsoTheme.ApplyDisplay(t, size);
            else            LitIsoTheme.ApplyBody(t, size);
            ApplyTextReadability(t);
            return t;
        }

        internal static void FitText(Text t)
        {
            if (t == null) return;
            t.horizontalOverflow  = HorizontalWrapMode.Wrap;
            t.verticalOverflow    = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize   = t.fontSize;
            t.resizeTextMinSize   = Mathf.Min(9, t.fontSize);
        }

        internal static void ApplyTextReadability(Text text)
        {
            if (text == null || text.GetComponent<Shadow>() != null) return;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }

        // ─── PIXEL PANEL (primary window grammar) ────────────────────────────────

        /// <summary>
        /// Build a full pixel-art window panel on <paramref name="parent"/>.
        /// Produces the canonical grammar from the LIT-ISO front-end spec:
        ///   • Panel fill (#15171C)
        ///   • 3px ink (#0a0b0e) hard outer border   [BorderInk]
        ///   • 1px steel (#2c313c) inset line         [BorderSteel]
        ///   • 1px gold (#E8C468 @ 40 %) hairline     [BorderGold]
        ///   • Gold diamond stud in each corner
        ///
        /// Returns the content RectTransform — children should be parented here so
        /// they sit inside the border grammar automatically.
        /// </summary>
        internal static RectTransform BuildPixelPanel(Transform parent, string name,
            bool addStuds = true)
        {
            // Root — ink border (3px Outline component)
            var root = NewImage(parent, name, null, LitIsoTheme.Base);
            root.raycastTarget = false;
            var rootRt = root.rectTransform;
            AddPixelOutline(root.gameObject, LitIsoTheme.Base, LitIsoTheme.BorderInk);

            // Fill layer — Panel colour
            var fill = NewImage(rootRt, "Fill", null, LitIsoTheme.Panel);
            fill.raycastTarget = false;
            Stretch(fill.rectTransform, LitIsoTheme.BorderInk);

            // Steel inset line
            var steel = NewImage(fill.rectTransform, "InsetSteel", null, LitIsoTheme.Stone);
            steel.raycastTarget = false;
            Stretch(steel.rectTransform);
            var steelFill = NewImage(steel.rectTransform, "SteelFill", null, LitIsoTheme.Panel);
            steelFill.raycastTarget = false;
            Stretch(steelFill.rectTransform, LitIsoTheme.BorderSteel);

            // Gold hairline inset
            var gold = NewImage(steelFill.rectTransform, "InsetGold", null,
                new Color(LitIsoTheme.Gold.r, LitIsoTheme.Gold.g, LitIsoTheme.Gold.b, 0.40f));
            gold.raycastTarget = false;
            Stretch(gold.rectTransform);
            var goldFill = NewImage(gold.rectTransform, "GoldFill", null, LitIsoTheme.Panel);
            goldFill.raycastTarget = false;
            Stretch(goldFill.rectTransform, LitIsoTheme.BorderGold);

            // Content area — parents sit inside the gold hairline
            var content = NewRect("Content", goldFill.rectTransform);
            Stretch(content, 2f); // small inner padding

            if (addStuds) AddCornerStuds(rootRt);

            return content;
        }

        /// <summary>
        /// Skinnable panel: uses a 9-sliced sprite skin if present, otherwise a flat
        /// <paramref name="fallback"/> fill wrapped in the shared Stone design-system
        /// frame. Returns the panel Image so callers can size its rectTransform and
        /// parent children onto it directly (the usage every in-game view relies on).
        ///
        /// The richer pixel-art window grammar lives in <see cref="BuildPixelPanel"/>;
        /// migrate views to it individually rather than rerouting NewPanel, since that
        /// returns a nested content rect with a different parenting contract.
        /// </summary>
        internal static Image NewPanel(Transform parent, string name,
            string skinName, Color fallback)
        {
            var img = NewImage(parent, name, Spr(skinName), fallback);
            if (img.sprite != null) img.type = Image.Type.Sliced;
            else LitIsoTheme.StyleFrame(img, LitIsoTheme.FrameStyle.Stone);
            return img;
        }

        // ─── CORNER STUDS ─────────────────────────────────────────────────────────

        /// <summary>
        /// Adds four gold diamond studs at the corners of <paramref name="parent"/>.
        /// Each stud is a small square Image rotated 45°, matching the mockup.
        /// </summary>
        internal static void AddCornerStuds(RectTransform parent)
        {
            float s = LitIsoTheme.StudSize;
            // (anchorMin, anchorMax, pivot, offset from corner)
            var corners = new (Vector2 anch, Vector2 piv, Vector2 pos)[]
            {
                (new Vector2(0,1), new Vector2(0,1), new Vector2( s * 0.5f, -s * 0.5f)), // top-left
                (new Vector2(1,1), new Vector2(1,1), new Vector2(-s * 0.5f, -s * 0.5f)), // top-right
                (new Vector2(0,0), new Vector2(0,0), new Vector2( s * 0.5f,  s * 0.5f)), // bottom-left
                (new Vector2(1,0), new Vector2(1,0), new Vector2(-s * 0.5f,  s * 0.5f)), // bottom-right
            };
            string[] names = { "StudTL", "StudTR", "StudBL", "StudBR" };
            for (int i = 0; i < 4; i++)
            {
                var go  = new GameObject(names[i], typeof(RectTransform));
                go.transform.SetParent(parent, false);
                var img = go.AddComponent<Image>();
                img.color = LitIsoTheme.Gold;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin        = corners[i].anch;
                rt.anchorMax        = corners[i].anch;
                rt.pivot            = corners[i].piv;
                rt.sizeDelta        = new Vector2(s, s);
                rt.anchoredPosition = corners[i].pos;
                rt.localRotation    = Quaternion.Euler(0f, 0f, 45f);
                // Ink outline on the stud
                AddPixelOutline(go, LitIsoTheme.Base, 1.5f);
            }
        }

        // ─── PIXEL BUTTON ─────────────────────────────────────────────────────────

        /// <summary>
        /// Build a pixel-art button: Raised fill, 2px ink border, 1px bevel,
        /// 6px hard bottom shadow (collapses to 1px on press via LitIsoButtonPress).
        /// </summary>
        internal static Button BuildPixelButton(Transform parent, string name,
            string label, int labelSize = 18,
            LitIsoTheme.ButtonStyle style = LitIsoTheme.ButtonStyle.Stone)
        {
            var skinName = style == LitIsoTheme.ButtonStyle.Gold ? "btn_gold" : "btn_stone";
            return NewButton(parent, name, skinName, label, labelSize, style);
        }

        /// <summary>Skinnable button — sprite-swap when skin exists, pixel grammar otherwise.</summary>
        internal static Button NewButton(Transform parent, string name,
            string skinName, string label, int size = 18)
            => NewButton(parent, name, skinName, label, size, LitIsoTheme.ButtonStyle.Stone);

        internal static Button NewButton(Transform parent, string name,
            string skinName, string label, int size,
            LitIsoTheme.ButtonStyle style)
        {
            var img = NewImage(parent, name, Spr(skinName), SlotBg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var hoverSpr = Spr(skinName + "_hover");
            if (img.sprite != null)
            {
                img.type = Image.Type.Sliced;
                if (hoverSpr != null)
                {
                    btn.transition   = Selectable.Transition.SpriteSwap;
                    btn.spriteState  = new SpriteState
                    {
                        highlightedSprite = hoverSpr,
                        pressedSprite     = hoverSpr,
                        selectedSprite    = hoverSpr
                    };
                }
            }
            else
            {
                LitIsoTheme.StyleButton(btn, img, style);
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = NewText(img.transform, "Label", label, size, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform, 6f);
                FitText(t);
            }
            return btn;
        }

        // ─── ITEM SLOT ────────────────────────────────────────────────────────────

        /// <summary>
        /// Build a single inventory/hotbar slot:
        ///   • Panel2 dark fill
        ///   • 1px rarity-coloured border (default = Common grey)
        ///   • Icon Image child (centred, 80 % size)
        ///   • Count Text child (bottom-right, Pixelify Sans 14)
        ///   • Durability strip child (1px at bottom, coloured by durability)
        ///
        /// Returns the slot root Image so callers can resize and wire onClick.
        /// </summary>
        internal static (Image slot, Image icon, Text count, Image durability)
            BuildRaritySlot(Transform parent, string name, float size = 52f)
        {
            // Slot background
            var slot = NewImage(parent, name, null, LitIsoTheme.Panel2);
            var slotRt = slot.rectTransform;
            slotRt.sizeDelta = new Vector2(size, size);
            AddPixelOutline(slot.gameObject, LitIsoTheme.RarityCommon, 1f);

            // Icon
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slotRt, false);
            var icon = iconGo.AddComponent<Image>();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget  = false;
            var iconRt = icon.rectTransform;
            float pad = size * 0.1f;
            iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(pad, pad + 3f); // leave room for durability strip
            iconRt.offsetMax = new Vector2(-pad, -pad);

            // Count label (bottom-right)
            var countGo = new GameObject("Count", typeof(RectTransform));
            countGo.transform.SetParent(slotRt, false);
            var count = countGo.AddComponent<Text>();
            count.text      = "";
            count.alignment = TextAnchor.LowerRight;
            count.color     = LitIsoTheme.Parchment;
            LitIsoTheme.ApplyBody(count, 13);
            count.raycastTarget = false;
            var countRt = count.rectTransform;
            countRt.anchorMin = Vector2.zero; countRt.anchorMax = Vector2.one;
            countRt.offsetMin = new Vector2(2f, 4f); countRt.offsetMax = new Vector2(-2f, -2f);
            ApplyTextReadability(count);

            // Durability strip (2px at bottom)
            var durGo = new GameObject("Durability", typeof(RectTransform));
            durGo.transform.SetParent(slotRt, false);
            var dur = durGo.AddComponent<Image>();
            dur.color = LitIsoTheme.DurabilityGood;
            dur.raycastTarget = false;
            var durRt = dur.rectTransform;
            durRt.anchorMin = new Vector2(0f, 0f); durRt.anchorMax = new Vector2(1f, 0f);
            durRt.pivot     = new Vector2(0f, 0f);
            durRt.offsetMin = new Vector2(1f, 1f); durRt.offsetMax = new Vector2(-1f, 3f);

            return (slot, icon, count, dur);
        }

        /// <summary>Update a slot's rarity border colour.</summary>
        internal static void SetSlotRarity(Image slot, string rarity)
        {
            var outline = slot.GetComponent<Outline>();
            if (outline != null) outline.effectColor = LitIsoTheme.RarityColor(rarity);
        }

        /// <summary>Update a slot's durability strip width and colour.</summary>
        internal static void SetSlotDurability(Image durBar, float t01)
        {
            if (durBar == null) return;
            durBar.color = LitIsoTheme.DurabilityColor(t01);
            var rt = durBar.rectTransform;
            float fullWidth = rt.parent.GetComponent<RectTransform>().rect.width - 2f;
            rt.offsetMax = new Vector2(-1f + fullWidth * t01, rt.offsetMax.y);
        }

        // ─── MODAL SCRIM ──────────────────────────────────────────────────────────

        /// <summary>Full-screen semi-transparent overlay (rgba 4,5,7,.6) behind modal panels.</summary>
        internal static Image NewScrim(Transform parent)
        {
            var img = NewImage(parent, "Scrim", null, LitIsoTheme.ModalScrim);
            Stretch(img.rectTransform);
            img.raycastTarget = true; // blocks clicks through to world
            return img;
        }

        // ─── SECTION DIVIDER ─────────────────────────────────────────────────────

        /// <summary>Horizontal 1px steel divider line, full width of parent.</summary>
        internal static Image NewDivider(Transform parent, string name = "Divider")
        {
            var img = NewImage(parent, name, null, LitIsoTheme.Stone);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
            rt.offsetMax = new Vector2(0f, rt.offsetMax.y);
            return img;
        }

        // ─── SECTION HEADER ──────────────────────────────────────────────────────

        /// <summary>
        /// Compact section header: gold diamond bullet + uppercase label in WarmTan.
        /// Matches the "CRAFTING STATIONS" / "STORAGE &amp; WORLD" headers in the mockup.
        /// </summary>
        internal static RectTransform NewSectionHeader(Transform parent, string label, float yPos)
        {
            var row = NewRect("Header_" + label, parent);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f);
            row.pivot     = new Vector2(0f, 1f);
            row.sizeDelta = new Vector2(0f, 22f);
            row.anchoredPosition = new Vector2(0f, -yPos);

            // Gold bullet
            var bullet = new GameObject("Bullet", typeof(RectTransform));
            bullet.transform.SetParent(row, false);
            var bImg = bullet.AddComponent<Image>();
            bImg.color = LitIsoTheme.Gold;
            bImg.raycastTarget = false;
            var bRt = bImg.rectTransform;
            bRt.anchorMin = new Vector2(0f, 0.5f); bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.pivot = new Vector2(0.5f, 0.5f);
            bRt.sizeDelta = new Vector2(7f, 7f);
            bRt.anchoredPosition = new Vector2(8f, 0f);
            bRt.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Label
            var t = NewText(row, "Label", label.ToUpper(), 11, TextAnchor.MiddleLeft,
                LitIsoTheme.WarmTan);
            var tRt = t.rectTransform;
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(22f, 0f); tRt.offsetMax = Vector2.zero;

            return row;
        }

        // ─── OUTLINE HELPER ──────────────────────────────────────────────────────

        /// <summary>
        /// Applies a hard pixel Outline component (no blur). Uses effectDistance
        /// so Unity draws 1px on each axis = effectively a solid border on all 4 sides.
        /// </summary>
        internal static void AddPixelOutline(GameObject go, Color color, float thickness)
        {
            var o = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
            o.effectColor    = color;
            o.effectDistance = new Vector2(thickness, -thickness);
            o.useGraphicAlpha = false;
        }
    }
}

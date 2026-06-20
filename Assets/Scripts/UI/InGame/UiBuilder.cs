using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Shared uGUI construction helpers used by the HUD + panel views.
    /// Centralises canvas / image / text / skin-loading boilerplate so every
    /// view has a consistent look and the procedural-fallback story.
    /// </summary>
    internal static class UiBuilder
    {
        // Palette now sourced from the shared LIT-ISO design system
        // (LitIsoTheme) so the in-game panels and the main menu match exactly.
        internal static readonly Color TextCol  = LitIsoTheme.Parchment;                 // #d8d4c8 parchment
        internal static readonly Color MutedCol = LitIsoTheme.WarmTan;                    // #8a8578 warm tan
        internal static readonly Color PanelBg  = LitIsoTheme.Panel;                      // #15171C stone panel
        internal static readonly Color Border   = LitIsoTheme.Stone;                      // #2c313c inset bevel
        internal static readonly Color Scrim    = new Color(0f, 0f, 0f, 0.62f);           // dim scene behind a panel
        internal static readonly Color SlotBg   = LitIsoTheme.Panel2;                     // #1D2027 raised slot
        internal static readonly Color Select   = LitIsoTheme.Gold;                       // #E8C468 gold accent

        /// <summary>Sprite from Resources/UI/InGame/&lt;name&gt; (null if not present).</summary>
        internal static Sprite Spr(string name) => Resources.Load<Sprite>("UI/InGame/" + name);

        static readonly System.Collections.Generic.List<CanvasScaler> s_scalers
            = new System.Collections.Generic.List<CanvasScaler>();

        internal static Canvas NewCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.pixelPerfect = true;
            c.sortingOrder = sortingOrder;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
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

        /// <summary>Live-applies the shared ui.scale pref to every canvas this
        /// builder created (Settings tab calls this — fixes the stored-but-
        /// never-applied UI scale bug found in the 2026-06-11 audit).</summary>
        internal static void ApplyUiScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.75f, 1.75f);
            s_scalers.RemoveAll(s => s == null);
            foreach (var s in s_scalers) ApplyScaleTo(s, scale);
        }

        internal static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        internal static Image NewImage(Transform parent, string name, Sprite sprite, Color fallback)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = sprite != null ? Color.white : fallback;
            return img;
        }

        /// <summary>Skinnable panel: uses sprite (sliced) if present, else flat fill + outline.</summary>
        internal static Image NewPanel(Transform parent, string name, string skinName, Color fallback)
        {
            var img = NewImage(parent, name, Spr(skinName), fallback);
            if (img.sprite != null) { img.type = Image.Type.Sliced; }
            else
            {
                // No sprite skin: fall back to the shared design-system Stone
                // frame (2px border + inset bevel) instead of a bare outline.
                LitIsoTheme.StyleFrame(img, LitIsoTheme.FrameStyle.Stone);
            }
            return img;
        }

        internal static Text NewText(Transform parent, string name, string value, int size, TextAnchor anchor, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = value;
            t.alignment = anchor;
            t.color = color ?? TextCol;
            // Owner feedback: UI text must never spill outside its rect.
            // Wrap + Truncate keeps strays inside; variable-length labels
            // should additionally call FitText so they shrink instead of clip.
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            // Theme fonts: headings (>=19) use the display face, smaller text the
            // readable body face. LitIsoFont.Apply still owns sizing + scaling.
            if (size >= 19) LitIsoTheme.ApplyDisplay(t, size);
            else            LitIsoTheme.ApplyBody(t, size);
            ApplyTextReadability(t);
            return t;
        }

        /// <summary>
        /// Best-fit for variable-length labels (same pattern as the welcome
        /// screen's CreateText): long strings shrink into their rect instead
        /// of spilling or clipping. LitIsoFont.Apply turns best-fit off, so
        /// call this AFTER NewText/Apply. Do not use on text whose rect is
        /// driven by its own preferred size (ContentSizeFitter feedback).
        /// </summary>
        internal static void FitText(Text t)
        {
            if (t == null) return;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = t.fontSize;
            t.resizeTextMinSize = Mathf.Min(11, t.fontSize);
        }

        internal static void ApplyTextReadability(Text text)
        {
            if (text == null || text.GetComponent<Shadow>() != null)
                return;

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
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

        /// <summary>Skinnable button — uses sprite-swap on hover if skin is provided.</summary>
        internal static Button NewButton(Transform parent, string name, string skinName, string label, int size = 18)
            => NewButton(parent, name, skinName, label, size, LitIsoTheme.ButtonStyle.Stone);

        /// <summary>Themed button. When no sprite skin exists it uses the shared
        /// design-system button (gold or stone) with the 5px hard bottom shadow
        /// + press offset; with a skin it keeps the sprite-swap behaviour.</summary>
        internal static Button NewButton(Transform parent, string name, string skinName, string label, int size,
            LitIsoTheme.ButtonStyle style)
        {
            // Build the image WITHOUT NewPanel's auto-frame so the themed button
            // styling owns the look in the procedural case.
            var img = NewImage(parent, name, Spr(skinName), SlotBg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var hoverSpr = Spr(skinName + "_hover");
            if (img.sprite != null)
            {
                img.type = Image.Type.Sliced;
                if (hoverSpr != null)
                {
                    btn.transition = Selectable.Transition.SpriteSwap;
                    btn.spriteState = new SpriteState { highlightedSprite = hoverSpr, pressedSprite = hoverSpr, selectedSprite = hoverSpr };
                }
            }
            else
            {
                // No skin: apply the shared design-system button states.
                LitIsoTheme.StyleButton(btn, img, style);
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = NewText(img.transform, "Label", label, size, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform, 6f);
                // Button labels are often translated/dynamic (tab names, "Craft
                // All (99)", etc.) — shrink to fit rather than spilling past
                // the button's edges.
                FitText(t);
            }
            return btn;
        }

        internal static Image NewScrim(Transform parent)
        {
            var img = NewImage(parent, "Scrim", null, Scrim);
            Stretch(img.rectTransform);
            return img;
        }
    }
}

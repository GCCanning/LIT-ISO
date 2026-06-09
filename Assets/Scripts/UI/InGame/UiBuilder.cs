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
        internal static readonly Color TextCol  = new Color(1.00f, 0.94f, 0.76f, 1f);
        internal static readonly Color MutedCol = new Color(0.82f, 0.88f, 0.90f, 1f);
        internal static readonly Color PanelBg  = new Color(0.040f, 0.050f, 0.070f, 0.985f);
        internal static readonly Color Border   = new Color(0.70f, 0.56f, 0.28f, 1f);
        internal static readonly Color Scrim    = new Color(0f, 0f, 0f, 0.62f);
        internal static readonly Color SlotBg   = new Color(0.075f, 0.085f, 0.110f, 0.96f);
        internal static readonly Color Select   = new Color(1.00f, 0.78f, 0.28f, 1f);

        /// <summary>Sprite from Resources/UI/InGame/&lt;name&gt; (null if not present).</summary>
        internal static Sprite Spr(string name) => Resources.Load<Sprite>("UI/InGame/" + name);

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
            s.referenceResolution = new Vector2(1920, 1080);
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
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
                var outline = img.gameObject.AddComponent<Outline>();
                outline.effectColor = Border;
                outline.effectDistance = new Vector2(2.5f, -2.5f);

                var shadow = img.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.60f);
                shadow.effectDistance = new Vector2(3f, -3f);
                shadow.useGraphicAlpha = true;
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
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            LitIsoFont.Apply(t, size);
            ApplyTextReadability(t);
            return t;
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
        {
            var img = NewPanel(parent, name, skinName, SlotBg);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var hoverSpr = Spr(skinName + "_hover");
            if (img.sprite != null && hoverSpr != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                btn.spriteState = new SpriteState { highlightedSprite = hoverSpr, pressedSprite = hoverSpr, selectedSprite = hoverSpr };
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = NewText(img.transform, "Label", label, size, TextAnchor.MiddleCenter);
                Stretch(t.rectTransform, 6f);
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

// PHASE_COMPLETE Phase7
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Data pin for a named location on the world map (town, dungeon, point of interest).
    /// </summary>
    public struct MapPin
    {
        public string label;        // display name ("Ironveil Village")
        public Vector2 mapNorm;     // 0..1 normalised position on the map rect
        public bool discovered;     // false → pin hidden until the player visits
    }

    /// <summary>
    /// uGUI world map overlay. Opens over the full screen with M / from the HUD.
    ///
    /// Visual grammar:
    ///   - Full-screen stone panel backdrop (LitIsoTheme.Panel + modal scrim).
    ///   - 3px ink border + 1px steel inset + 1px gold hairline + gold corner studs on
    ///     the inner map frame (mirrors the crafting-panel / character-book grammar).
    ///   - Parchment-tinted map area (colour: LitIsoTheme.ParchFill tinted mid).
    ///   - Fog-of-war: dark vignette rect over cells that haven't been explored.
    ///   - Location pins: gold diamond stud + Pixelify Sans label.
    ///   - Player marker: gold diamond that pulses in alpha via code animation.
    ///   - Close button: top-right, LitIsoTheme stone style.
    ///
    /// The view is Foundation-free; wire live data via <see cref="SetExploredRegion"/>,
    /// <see cref="SetPlayerPosition"/>, and <see cref="SetPins"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldMapView : MonoBehaviour
    {
        // ---- design constants ----
        static readonly Color MapBg        = new Color(0.78f, 0.71f, 0.56f, 1f); // parchment mid
        static readonly Color FogColor     = new Color(0.04f, 0.05f, 0.07f, 0.82f);
        static readonly Color GridLine     = new Color(LitIsoTheme.Stone.r, LitIsoTheme.Stone.g, LitIsoTheme.Stone.b, 0.22f);
        static readonly Color PinColor     = LitIsoTheme.GoldLit;
        static readonly Color PlayerColor  = LitIsoTheme.Gold;
        static readonly Color CompassColor = new Color(LitIsoTheme.WarmTan.r, LitIsoTheme.WarmTan.g, LitIsoTheme.WarmTan.b, 0.75f);

        // ---- live state ----
        Canvas _canvas;
        GameObject _root;
        RectTransform _mapRect;
        RectTransform _playerMarker;
        Image _playerMarkerImg;
        RectTransform _fogOverlay;
        Transform _pinContainer;
        Text _regionLabel;

        readonly List<(GameObject go, MapPin pin)> _pins = new List<(GameObject, MapPin)>();
        float _pulseTimer;
        bool _open;

        public bool IsOpen => _open;
        public event Action Closed;

        // ---------------------------------------------------------------- public API

        /// <summary>Open the world map.</summary>
        public void Show()
        {
            _open = true;
            if (_root != null) _root.SetActive(true);
        }

        /// <summary>Close the world map.</summary>
        public void Hide()
        {
            _open = false;
            if (_root != null) _root.SetActive(false);
            Closed?.Invoke();
        }

        public void Toggle() { if (_open) Hide(); else Show(); }

        /// <summary>
        /// Set the player's normalised position on the map (0,0 = bottom-left, 1,1 = top-right).
        /// </summary>
        public void SetPlayerPosition(Vector2 normPos)
        {
            if (_playerMarker == null || _mapRect == null) return;
            _playerMarker.anchoredPosition = new Vector2(
                normPos.x * _mapRect.rect.width,
                normPos.y * _mapRect.rect.height);
        }

        /// <summary>
        /// Update which world region the player is currently in (shown in the map header).
        /// </summary>
        public void SetRegionLabel(string regionName)
        {
            if (_regionLabel != null)
                _regionLabel.text = string.IsNullOrEmpty(regionName) ? "World Map" : regionName;
        }

        /// <summary>
        /// Replace all map pins. Call whenever the set of discoverable locations changes.
        /// </summary>
        public void SetPins(MapPin[] pins)
        {
            // Clear old pin GameObjects
            foreach (var (go, _) in _pins)
                if (go != null) Destroy(go);
            _pins.Clear();

            if (pins == null || _pinContainer == null) return;
            foreach (var p in pins)
            {
                if (!p.discovered) continue;
                var go = BuildPin(_pinContainer, p);
                _pins.Add((go, p));
            }
        }

        /// <summary>
        /// Drive the fog-of-war opacity from 0 (full map revealed) to 1 (fully fogged).
        /// Typically pass 1 minus (explored fraction). The fog is a simple dark overlay
        /// tinted panel; full cell-accurate masking requires a render texture extension.
        /// </summary>
        public void SetFogOpacity(float opacity01)
        {
            if (_fogOverlay == null) return;
            var img = _fogOverlay.GetComponent<Image>();
            if (img != null)
            {
                var c = FogColor;
                c.a = Mathf.Clamp01(opacity01) * FogColor.a;
                img.color = c;
            }
        }

        // ---------------------------------------------------------------- lifecycle

        void Awake()
        {
            Build();
            Hide();
        }

        void Update()
        {
            if (!_open) return;
            // Player marker pulse: oscillate alpha between 0.5 and 1 at ~1 Hz.
            _pulseTimer += Time.unscaledDeltaTime * Mathf.PI * 1.2f;
            if (_playerMarkerImg != null)
            {
                var c = _playerMarkerImg.color;
                c.a = Mathf.Lerp(0.5f, 1f, (Mathf.Sin(_pulseTimer) + 1f) * 0.5f);
                _playerMarkerImg.color = c;
            }
        }

        // ---------------------------------------------------------------- build

        void Build()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);

            var canvasGo = new GameObject("WorldMapCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight  = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(_canvas.transform, false);
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

            // ---- Full-screen stone-panel backdrop ----
            var backdropImg = _root.AddComponent<Image>();
            backdropImg.color = new Color(LitIsoTheme.Panel.r, LitIsoTheme.Panel.g, LitIsoTheme.Panel.b, 0.97f);
            backdropImg.raycastTarget = true;

            // ---- Map frame (centred, 80% wide, pixel-art border grammar) ----
            var frameGo = new GameObject("MapFrame", typeof(RectTransform));
            frameGo.transform.SetParent(_root.transform, false);
            var frameImg = frameGo.AddComponent<Image>();
            frameImg.color = LitIsoTheme.Panel;
            var frameRt = frameImg.rectTransform;
            frameRt.anchorMin = new Vector2(0.04f, 0.07f);
            frameRt.anchorMax = new Vector2(0.96f, 0.88f);
            frameRt.offsetMin = frameRt.offsetMax = Vector2.zero;

            // 3px ink border
            var inkOl = frameGo.AddComponent<Outline>();
            inkOl.effectColor    = LitIsoTheme.Base;
            inkOl.effectDistance = new Vector2(3f, -3f);
            inkOl.useGraphicAlpha = false;

            // 1px steel inset
            AddInsetLayer(frameRt, LitIsoTheme.Stone, 0.6f, 3f, 1f);

            // 1px gold hairline
            AddInsetLayer(frameRt, LitIsoTheme.GoldDeep, 0.45f, 5f, 1f);

            // Gold corner studs
            AddCornerStud(frameRt, new Vector2(0f, 1f), new Vector2(8f, -8f));
            AddCornerStud(frameRt, new Vector2(1f, 1f), new Vector2(-8f, -8f));
            AddCornerStud(frameRt, new Vector2(0f, 0f), new Vector2(8f, 8f));
            AddCornerStud(frameRt, new Vector2(1f, 0f), new Vector2(-8f, 8f));

            // ---- Parchment map area inside the frame ----
            var mapGo = new GameObject("MapArea", typeof(RectTransform));
            mapGo.transform.SetParent(frameRt, false);
            var mapImg = mapGo.AddComponent<Image>();
            mapImg.color = MapBg;
            _mapRect = mapImg.rectTransform;
            _mapRect.anchorMin = Vector2.zero; _mapRect.anchorMax = Vector2.one;
            _mapRect.offsetMin = new Vector2(7f, 7f); _mapRect.offsetMax = new Vector2(-7f, -7f);

            // ---- Isometric grid overlay (subtle lines on the parchment) ----
            BuildGridLines(_mapRect);

            // ---- Fog-of-war overlay ----
            var fogGo = new GameObject("Fog", typeof(RectTransform));
            fogGo.transform.SetParent(_mapRect, false);
            var fogImg = fogGo.AddComponent<Image>();
            fogImg.color = FogColor;
            fogImg.raycastTarget = false;
            _fogOverlay = fogImg.rectTransform;
            _fogOverlay.anchorMin = Vector2.zero; _fogOverlay.anchorMax = Vector2.one;
            _fogOverlay.offsetMin = _fogOverlay.offsetMax = Vector2.zero;

            // ---- Pin container (above fog, below player marker) ----
            var pinRoot = new GameObject("Pins", typeof(RectTransform));
            pinRoot.transform.SetParent(_mapRect, false);
            _pinContainer = pinRoot.transform;
            var pinRt = pinRoot.GetComponent<RectTransform>();
            pinRt.anchorMin = Vector2.zero; pinRt.anchorMax = Vector2.one;
            pinRt.offsetMin = pinRt.offsetMax = Vector2.zero;

            // ---- Player marker (pulsing gold diamond) ----
            var markerGo = new GameObject("PlayerMarker", typeof(RectTransform));
            markerGo.transform.SetParent(_mapRect, false);
            _playerMarkerImg = markerGo.AddComponent<Image>();
            _playerMarkerImg.color = PlayerColor;
            _playerMarkerImg.raycastTarget = false;
            var markerOl = markerGo.AddComponent<Outline>();
            markerOl.effectColor    = LitIsoTheme.Base;
            markerOl.effectDistance = new Vector2(2f, -2f);
            markerOl.useGraphicAlpha = false;
            _playerMarker = _playerMarkerImg.rectTransform;
            _playerMarker.anchorMin = _playerMarker.anchorMax = new Vector2(0.5f, 0.5f);
            _playerMarker.pivot     = new Vector2(0.5f, 0.5f);
            _playerMarker.sizeDelta = new Vector2(14f, 14f);
            _playerMarker.localEulerAngles = new Vector3(0f, 0f, 45f);

            // ---- Compass rose (bottom-left of map) ----
            BuildCompassRose(_mapRect);

            // ---- Header bar (above map frame, below close btn) ----
            BuildHeader(_root.transform, frameRt);

            // ---- Close button (top-right) ----
            BuildCloseButton(_root.transform);
        }

        void BuildGridLines(RectTransform mapRect)
        {
            // Draw a lightweight isometric grid as a set of thin colour rects.
            // Horizontal lines every ~8% of height; diagonal lines mimic iso perspective.
            int hLines = 12, vLines = 16;
            for (int i = 1; i < hLines; i++)
            {
                float t = i / (float)hLines;
                var go = new GameObject("HGrid" + i, typeof(RectTransform));
                go.transform.SetParent(mapRect, false);
                var img = go.AddComponent<Image>();
                img.color = GridLine;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0f, t); rt.anchorMax = new Vector2(1f, t);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(0f, 1f);
            }
            for (int i = 1; i < vLines; i++)
            {
                float t = i / (float)vLines;
                var go = new GameObject("VGrid" + i, typeof(RectTransform));
                go.transform.SetParent(mapRect, false);
                var img = go.AddComponent<Image>();
                img.color = GridLine;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(t, 0f); rt.anchorMax = new Vector2(t, 1f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(1f, 0f);
            }
        }

        void BuildCompassRose(RectTransform mapRect)
        {
            // A simple "N" label in a small stone chip at bottom-left of the map.
            var compassGo = new GameObject("Compass", typeof(RectTransform));
            compassGo.transform.SetParent(mapRect, false);
            var compassImg = compassGo.AddComponent<Image>();
            compassImg.color = new Color(LitIsoTheme.Panel.r, LitIsoTheme.Panel.g, LitIsoTheme.Panel.b, 0.82f);
            var ol = compassGo.AddComponent<Outline>();
            ol.effectColor = LitIsoTheme.GoldDeep; ol.effectDistance = new Vector2(1f, -1f); ol.useGraphicAlpha = false;
            var cr = compassImg.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 0f); cr.pivot = new Vector2(0f, 0f);
            cr.anchoredPosition = new Vector2(10f, 10f); cr.sizeDelta = new Vector2(36f, 36f);

            var nLabel = NewText(cr, "N", "N", 14, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(nLabel, 14, CompassColor);
            nLabel.raycastTarget = false;
            var nlr = nLabel.rectTransform;
            nlr.anchorMin = Vector2.zero; nlr.anchorMax = Vector2.one;
            nlr.offsetMin = nlr.offsetMax = Vector2.zero;
        }

        void BuildHeader(Transform root, RectTransform frameRt)
        {
            var headerGo = new GameObject("MapHeader", typeof(RectTransform));
            headerGo.transform.SetParent(root, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            // Sits above the frame's top edge
            headerRt.anchorMin = new Vector2(0.04f, 0.88f);
            headerRt.anchorMax = new Vector2(0.7f,  0.95f);
            headerRt.offsetMin = headerRt.offsetMax = Vector2.zero;

            // Title wordmark
            var title = NewText(headerRt, "Title", "WORLD MAP", 22, TextAnchor.MiddleLeft);
            LitIsoTheme.ApplyDisplay(title, 22, LitIsoTheme.Gold);
            title.raycastTarget = false;
            var trt = title.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f); trt.offsetMax = Vector2.zero;

            // Region sub-label (right of title)
            _regionLabel = NewText(headerRt, "Region", "Emberfall Woods", 14, TextAnchor.MiddleLeft);
            LitIsoTheme.ApplyBody(_regionLabel, 14, LitIsoTheme.WarmTan);
            _regionLabel.raycastTarget = false;
            var rlr = _regionLabel.rectTransform;
            rlr.anchorMin = Vector2.zero; rlr.anchorMax = Vector2.one;
            rlr.offsetMin = new Vector2(260f, 0f); rlr.offsetMax = Vector2.zero;
        }

        void BuildCloseButton(Transform root)
        {
            var go = new GameObject("CloseBtn", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var bg  = go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            LitIsoTheme.StyleButton(btn, bg, LitIsoTheme.ButtonStyle.Stone);
            btn.onClick.AddListener(Hide);
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
            rt.sizeDelta = new Vector2(100f, 42f);

            var lbl = NewText(rt, "Label", "CLOSE", 13, TextAnchor.MiddleCenter);
            LitIsoTheme.ApplyDisplay(lbl, 13, LitIsoTheme.Parchment);
            lbl.raycastTarget = false;
            var lr = lbl.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(4f, 2f); lr.offsetMax = new Vector2(-4f, -2f);
        }

        // Build a gold diamond pin + label at a normalised position on the map.
        GameObject BuildPin(Transform parent, MapPin p)
        {
            if (_mapRect == null) return null;
            var pinGo = new GameObject("Pin_" + p.label, typeof(RectTransform));
            pinGo.transform.SetParent(parent, false);
            var pinRt = pinGo.GetComponent<RectTransform>();
            pinRt.anchorMin = pinRt.anchorMax = new Vector2(0f, 0f);
            pinRt.pivot     = new Vector2(0.5f, 0f);
            pinRt.anchoredPosition = new Vector2(
                p.mapNorm.x * _mapRect.rect.width,
                p.mapNorm.y * _mapRect.rect.height);
            pinRt.sizeDelta = new Vector2(80f, 40f);

            // Gold diamond stud
            var diamond = new GameObject("Diamond", typeof(RectTransform));
            diamond.transform.SetParent(pinRt, false);
            var dImg = diamond.AddComponent<Image>();
            dImg.color = PinColor;
            dImg.raycastTarget = false;
            var dOl = diamond.AddComponent<Outline>();
            dOl.effectColor = LitIsoTheme.GoldShadow; dOl.effectDistance = new Vector2(1f, -1f); dOl.useGraphicAlpha = false;
            var dr = dImg.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 1f); dr.pivot = new Vector2(0.5f, 0.5f);
            dr.anchoredPosition = Vector2.zero; dr.sizeDelta = new Vector2(10f, 10f);
            dr.localEulerAngles = new Vector3(0f, 0f, 45f);

            // Label beneath the stud
            var lbl = NewText(pinRt, "Label", p.label, 12, TextAnchor.UpperCenter);
            LitIsoTheme.ApplyBody(lbl, 12, LitIsoTheme.Parchment);
            lbl.raycastTarget = false;
            var lr = lbl.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(0f, 4f); lr.offsetMax = Vector2.zero;

            return pinGo;
        }

        // ---------------------------------------------------------------- grammar helpers

        static void AddInsetLayer(RectTransform parent, Color color, float alpha, float offset, float thickness)
        {
            var go = new GameObject("Inset", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, alpha);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(offset, offset);
            rt.offsetMax = new Vector2(-offset, -offset);
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(rt, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = LitIsoTheme.Panel;
            fillImg.raycastTarget = false;
            var fr = fillImg.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(thickness, thickness);
            fr.offsetMax = new Vector2(-thickness, -thickness);
            go.transform.SetAsFirstSibling();
        }

        static void AddCornerStud(RectTransform parent, Vector2 anchor, Vector2 offset)
        {
            const float half = 5f;
            var go = new GameObject("Stud", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = LitIsoTheme.GoldLit;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(half * 2f, half * 2f);
            rt.localEulerAngles = new Vector3(0f, 0f, 45f);
        }

        static Text NewText(Transform parent, string name, string value, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text      = value;
            t.alignment = anchor;
            t.color     = LitIsoTheme.Parchment;
            LitIsoFont.Apply(t, size);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
            return t;
        }
    }
}

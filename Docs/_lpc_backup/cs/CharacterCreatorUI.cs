using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Stardew-style character creator (v3, LPC wardrobe). Entirely programmatic
    /// uGUI (legacy Text via LitIsoFont). Skinned with the shared Menu UI art
    /// (Resources/UI/Menu: panel / button / button_hover / button_pressed /
    /// input_frame / slider_track / slider_handle), 9-sliced, so the creator
    /// matches the welcome/main-menu look. If any sprite is missing it falls
    /// back to flat panels + outlines, so it always renders.
    ///
    /// Left: live animated preview (rotate / walk toggle / animation cycler).
    /// Right: body type, then a colour SLIDER + live swatch for skin and eyes,
    /// and for each clothing slot (hair, shirt, pants, shoes) an item picker
    /// (&lt; &gt; arrows) plus a colour slider + swatch. Randomize + Done at the
    /// bottom.
    ///
    /// Scope: basic customization only. Weapons, armour and other gear come from
    /// in-game drops via <see cref="CharacterEquipmentVisuals"/> and are
    /// intentionally NOT editable here.
    ///
    /// Usage: CharacterCreatorUI.Show(onConfirmed); from any menu flow.
    /// </summary>
    public class CharacterCreatorUI : MonoBehaviour
    {
        public static event Action<LayeredAppearance> OnConfirmed;

        // ----- procedural fallback palette (used only if Menu art is absent) ---
        static readonly Color PanelBg = new(0.07f, 0.09f, 0.13f, 0.96f);
        static readonly Color SectionBg = new(0.11f, 0.13f, 0.18f, 0.9f);
        static readonly Color InputBg = new(0.05f, 0.06f, 0.09f, 0.92f);
        static readonly Color Border = new(0.30f, 0.36f, 0.42f, 1f);
        static readonly Color TextCol = new(0.95f, 0.91f, 0.74f, 1f);
        static readonly Color HandleCol = new(0.98f, 0.85f, 0.45f, 1f);

        // ----- shared Menu UI art (9-sliced) -----
        Sprite _spPanel, _spButton, _spButtonHover, _spButtonPressed, _spInput,
               _spSliderTrack, _spSliderHandle;

        LayeredAppearance _appearance;
        CharacterLayerCatalog _cat;

        // preview state
        Image _previewImage;
        CharacterCompositor.BakeResult _baked;
        int _previewRow; // 0 = S, facing camera
        bool _previewWalking = true;
        int _previewWalkFrame;
        float _previewTimer;

        // animation preview cycler (walk, cast, thrust, slash, shoot, hurt)
        string[] _animIds;
        int _previewAnimIndex;
        int _previewActionFrame;
        float _previewActionTimer;
        Text _animLabel;

        readonly List<Action> _refreshers = new(); // refresh labels/sliders/swatches
        bool _refreshing; // guards slider onValueChanged against refresh-time clamps

        public static CharacterCreatorUI Show(Action<LayeredAppearance> onConfirmed = null)
        {
            var go = new GameObject("CharacterCreatorUI");
            var ui = go.AddComponent<CharacterCreatorUI>();
            if (onConfirmed != null)
            {
                Action<LayeredAppearance> handler = null;
                handler = a => { onConfirmed(a); OnConfirmed -= handler; };
                OnConfirmed += handler;
            }
            return ui;
        }

        void Awake()
        {
            LoadSkin();
            _cat = CharacterLayerCatalog.Instance;
            _appearance = LayeredAppearance.LoadOrDefault();
            _animIds = _cat.animations != null && _cat.animations.Length > 0
                ? _cat.AnimationIds.ToArray()
                : new[] { "walk" };
            _previewAnimIndex = Mathf.Max(0, Array.IndexOf(_animIds, _cat.defaultAnimation));
            BuildUI();
            Rebake();
        }

        void LoadSkin()
        {
            _spPanel = Resources.Load<Sprite>("UI/Menu/panel");
            _spButton = Resources.Load<Sprite>("UI/Menu/button");
            _spButtonHover = Resources.Load<Sprite>("UI/Menu/button_hover");
            _spButtonPressed = Resources.Load<Sprite>("UI/Menu/button_pressed");
            _spInput = Resources.Load<Sprite>("UI/Menu/input_frame");
            _spSliderTrack = Resources.Load<Sprite>("UI/Menu/slider_track");
            _spSliderHandle = Resources.Load<Sprite>("UI/Menu/slider_handle");
        }

        string CurrentAnimId => _animIds[_previewAnimIndex];

        void Update()
        {
            if (_baked == null || _previewImage == null) return;

            if (CurrentAnimId == "walk")
            {
                int walkFrameCount = Mathf.Max(1, _baked.framesPerRow - _baked.walkStartFrame);
                if (_previewWalking)
                {
                    float spf = 1f / Mathf.Max(0.01f, _baked.fps);
                    _previewTimer += Time.unscaledDeltaTime;
                    while (_previewTimer >= spf)
                    {
                        _previewTimer -= spf;
                        _previewWalkFrame = (_previewWalkFrame + 1) % walkFrameCount;
                    }
                    _previewImage.sprite = _baked.sprites[_previewRow * _baked.framesPerRow + _baked.walkStartFrame + _previewWalkFrame];
                }
                else
                {
                    _previewImage.sprite = _baked.sprites[_previewRow * _baked.framesPerRow + _baked.idleFrame];
                }
            }
            else
            {
                float spf = 1f / Mathf.Max(0.01f, _baked.fps);
                _previewActionTimer += Time.unscaledDeltaTime;
                while (_previewActionTimer >= spf)
                {
                    _previewActionTimer -= spf;
                    _previewActionFrame = (_previewActionFrame + 1) % _baked.framesPerRow;
                }
                _previewImage.sprite = _baked.sprites[_previewRow * _baked.framesPerRow + _previewActionFrame];
            }
        }

        void Rebake()
        {
            if (_baked?.texture != null) Destroy(_baked.texture);
            _baked = CharacterCompositor.Bake(_appearance, CurrentAnimId);
            _previewWalkFrame = 0;
            _previewActionFrame = 0;
            _previewActionTimer = 0f;
            if (_previewImage != null && _baked.sprites.Length > 0)
                _previewImage.sprite = _baked.sprites[_previewRow * _baked.framesPerRow + (CurrentAnimId == "walk" ? _baked.idleFrame : 0)];
            if (_animLabel != null) _animLabel.text = Capitalize(CurrentAnimId);
            _refreshing = true;
            foreach (var r in _refreshers) r();
            _refreshing = false;
        }

        // ------------------------------------------------------------- UI build
        void BuildUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

            // dim/scrim background
            var dim = MakeRect("Dim", canvasGo.transform);
            Stretch(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0, 0, 0, 0.55f);

            // main panel (skinned)
            var panel = MakeRect("Panel", canvasGo.transform);
            panel.sizeDelta = new Vector2(840, 600);
            SkinPanel(panel, PanelBg);

            Label(panel, "Create Your Character", new Vector2(0, 272), 26, bold: true);

            BuildPreviewColumn(panel);
            BuildOptionsColumn(panel);

            // bottom buttons
            Btn(panel, "Randomize", new Vector2(-220, -270), new Vector2(170, 44), () =>
            {
                var keep = _appearance.accessories;
                _appearance = LayeredAppearance.Random(_cat);
                _appearance.accessories = keep;
                Rebake();
            });
            Btn(panel, "Done", new Vector2(220, -270), new Vector2(170, 44), Confirm);
        }

        void BuildPreviewColumn(RectTransform panel)
        {
            var box = MakeRect("Preview", panel);
            box.anchoredPosition = new Vector2(-280, 6);
            box.sizeDelta = new Vector2(240, 400);
            SkinPanel(box, SectionBg);

            var img = MakeRect("Char", box);
            img.sizeDelta = new Vector2(256, 256);
            img.anchoredPosition = new Vector2(0, 40);
            _previewImage = img.gameObject.AddComponent<Image>();
            _previewImage.preserveAspect = true;
            _previewImage.raycastTarget = false;

            // rotate + walk toggle
            Btn(box, "<", new Vector2(-78, -140), new Vector2(38, 34),
                () => _previewRow = (_previewRow + 1) % _baked.rowCount);
            Btn(box, ">", new Vector2(-34, -140), new Vector2(38, 34),
                () => _previewRow = (_previewRow + _baked.rowCount - 1) % _baked.rowCount);
            Btn(box, "Walk", new Vector2(55, -140), new Vector2(86, 34),
                () => _previewWalking = !_previewWalking);

            // animation cycler — sanity-check every outfit across all 6 animations
            Btn(box, "<", new Vector2(-78, -182), new Vector2(38, 34), () =>
            {
                _previewAnimIndex = (_previewAnimIndex + _animIds.Length - 1) % _animIds.Length;
                Rebake();
            });
            _animLabel = Label(box, Capitalize(CurrentAnimId), new Vector2(20, -182), 15);
            _animLabel.rectTransform.sizeDelta = new Vector2(80, 30);
            Btn(box, ">", new Vector2(82, -182), new Vector2(38, 34), () =>
            {
                _previewAnimIndex = (_previewAnimIndex + 1) % _animIds.Length;
                Rebake();
            });
        }

        void BuildOptionsColumn(RectTransform panel)
        {
            var col = MakeRect("Options", panel);
            col.anchoredPosition = new Vector2(120, -6);
            col.sizeDelta = new Vector2(560, 540);

            float y = 244;

            // Body type — arrows only (no colour).
            ArrowOnlyRow(col, ref y, "Body",
                () => _appearance.bodyType,
                dir => Cycle(_cat.bodyTypes.ToList(), _appearance.bodyType, dir, v => _appearance.bodyType = v),
                Capitalize);

            // Skin tone — colour slider (drives body + head + matchBodyColor items).
            var bodyDef = _cat.Find("lpc/body");
            ColorOnlyRow(col, ref y, "Skin", () => bodyDef,
                () => _appearance.skinVariant, v => _appearance.skinVariant = v);

            // Eye colour — colour slider.
            var eyesDef = _cat.Find("lpc/eyes");
            ColorOnlyRow(col, ref y, "Eyes", () => eyesDef,
                () => _appearance.eyesVariant, v => _appearance.eyesVariant = v);

            y -= 8;
            ItemSlot(col, ref y, "Hair", "hair", allowNone: true,
                () => _appearance.hairId, v => _appearance.hairId = v,
                () => _appearance.hairVariant, v => _appearance.hairVariant = v);
            ItemSlot(col, ref y, "Shirt", "shirt", allowNone: false,
                () => _appearance.shirtId, v => _appearance.shirtId = v,
                () => _appearance.shirtVariant, v => _appearance.shirtVariant = v);
            ItemSlot(col, ref y, "Pants", "pants", allowNone: false,
                () => _appearance.pantsId, v => _appearance.pantsId = v,
                () => _appearance.pantsVariant, v => _appearance.pantsVariant = v);
            ItemSlot(col, ref y, "Shoes", "shoes", allowNone: false,
                () => _appearance.shoesId, v => _appearance.shoesId = v,
                () => _appearance.shoesVariant, v => _appearance.shoesVariant = v);
        }

        // One slot = an item-picker line (arrows) + a colour slider line.
        void ItemSlot(RectTransform parent, ref float y, string label, string slot, bool allowNone,
            Func<string> getId, Action<string> setId, Func<string> getVariant, Action<string> setVariant)
        {
            var options = _cat.Slot(slot);

            // line A: item picker
            Label(parent, label, new Vector2(-250, y), 15, alignLeft: true);
            var nameLabel = Label(parent, "", new Vector2(-20, y), 15);
            nameLabel.rectTransform.sizeDelta = new Vector2(150, 28);
            Btn(parent, "<", new Vector2(-128, y), new Vector2(30, 28), () =>
            {
                var list = options.Select(o => o.id).ToList();
                if (allowNone) list.Insert(0, "");
                int i = list.IndexOf(getId() ?? "");
                if (i < 0) i = 0;
                i = ((i - 1) % list.Count + list.Count) % list.Count;
                ApplyItem(list[i], getVariant, setId, setVariant);
            });
            Btn(parent, ">", new Vector2(118, y), new Vector2(30, 28), () =>
            {
                var list = options.Select(o => o.id).ToList();
                if (allowNone) list.Insert(0, "");
                int i = list.IndexOf(getId() ?? "");
                if (i < 0) i = 0;
                i = ((i + 1) % list.Count + list.Count) % list.Count;
                ApplyItem(list[i], getVariant, setId, setVariant);
            });
            _refreshers.Add(() => nameLabel.text = ItemDisplayName(getId(), allowNone));

            // line B: colour slider for the selected item
            y -= 34;
            ColorLine(parent, y, () => _cat.Find(getId()), getVariant, setVariant);
            y -= 40;
        }

        void ApplyItem(string newId, Func<string> getVariant, Action<string> setId, Action<string> setVariant)
        {
            setId(newId);
            if (!string.IsNullOrEmpty(newId))
            {
                var def = _cat.Find(newId);
                setVariant(def.SafeVariant(getVariant()));
            }
            Rebake();
        }

        // label + colour slider + swatch on a single line (skin / eyes).
        void ColorOnlyRow(RectTransform parent, ref float y, string label,
            Func<ItemDef> getDef, Func<string> getVariant, Action<string> setVariant)
        {
            Label(parent, label, new Vector2(-250, y), 15, alignLeft: true);
            ColorLine(parent, y, getDef, getVariant, setVariant);
            y -= 40;
        }

        // swatch + slider + variant-name, wired to the current item's variants.
        void ColorLine(RectTransform parent, float y,
            Func<ItemDef> getDef, Func<string> getVariant, Action<string> setVariant)
        {
            // swatch
            var swRt = MakeRect("Swatch", parent);
            swRt.anchoredPosition = new Vector2(-150, y);
            swRt.sizeDelta = new Vector2(26, 26);
            var swatch = swRt.gameObject.AddComponent<Image>();
            swatch.color = Color.gray;
            swatch.raycastTarget = false;
            AddOutline(swRt);

            // slider
            var slider = MakeSlider(parent, new Vector2(40, y), new Vector2(210, 18));

            // variant label
            var valLabel = Label(parent, "", new Vector2(196, y), 13);
            valLabel.rectTransform.sizeDelta = new Vector2(86, 26);
            valLabel.alignment = TextAnchor.MiddleLeft;

            slider.onValueChanged.AddListener(v =>
            {
                if (_refreshing) return; // ignore clamps fired while we set min/max
                var def = getDef();
                if (def == null || def.variants.Length == 0) return;
                int idx = Mathf.Clamp(Mathf.RoundToInt(v), 0, def.variants.Length - 1);
                if (def.variants[idx] == getVariant()) return; // no-op, avoids rebake storms
                setVariant(def.variants[idx]);
                Rebake();
            });

            _refreshers.Add(() =>
            {
                var def = getDef();
                if (def == null || def.variants.Length == 0)
                {
                    slider.interactable = false;
                    slider.SetValueWithoutNotify(0);
                    swatch.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                    valLabel.text = "—";
                    return;
                }
                slider.interactable = true;
                int idx = Array.IndexOf(def.variants, getVariant());
                if (idx < 0) idx = 0;
                slider.minValue = 0;
                slider.maxValue = def.variants.Length - 1;
                slider.SetValueWithoutNotify(idx);
                swatch.color = CharacterCompositor.SwatchColor(def, def.variants[idx], _appearance.bodyType);
                valLabel.text = Capitalize(def.variants[idx]);
            });
        }

        string ItemDisplayName(string id, bool allowNone)
        {
            if (string.IsNullOrEmpty(id)) return allowNone ? "None" : "—";
            return _cat.Find(id)?.displayName ?? id;
        }

        void Cycle(List<string> options, string current, int dir, Action<string> set)
        {
            if (options.Count == 0) return;
            int i = options.IndexOf(current);
            if (i < 0) i = 0;
            i = ((i + dir) % options.Count + options.Count) % options.Count;
            set(options[i]);
        }

        static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).Replace('_', ' ');

        // "label  < value >" row (no colour). Rebakes after every change.
        void ArrowOnlyRow(RectTransform parent, ref float y, string label,
            Func<string> get, Action<int> cycle, Func<string, string> format)
        {
            Label(parent, label, new Vector2(-250, y), 15, alignLeft: true);
            var valueLabel = Label(parent, "", new Vector2(-20, y), 15);
            valueLabel.rectTransform.sizeDelta = new Vector2(150, 28);
            Btn(parent, "<", new Vector2(-128, y), new Vector2(30, 28), () => { cycle(-1); Rebake(); });
            Btn(parent, ">", new Vector2(118, y), new Vector2(30, 28), () => { cycle(1); Rebake(); });
            _refreshers.Add(() => valueLabel.text = format(get() ?? ""));
            y -= 40;
        }

        void Confirm()
        {
            _appearance.Save();
            OnConfirmed?.Invoke(_appearance.Clone());
            // live-apply to the in-game player so the created character is what's rendered
            var anim = FindFirstObjectByType<LayeredCharacterAnimator>();
            if (anim != null) anim.Apply(_appearance.Clone());
            Destroy(gameObject);
        }

        // ------------------------------------------------------- skinned helpers
        static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void AddOutline(RectTransform rt)
        {
            var o = rt.gameObject.GetComponent<Outline>() ?? rt.gameObject.AddComponent<Outline>();
            o.effectColor = Border;
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // 9-sliced panel, or flat fill + outline if no art.
        Image SkinPanel(RectTransform rt, Color fallback)
        {
            var img = rt.gameObject.AddComponent<Image>();
            if (_spPanel != null)
            {
                img.sprite = _spPanel;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = fallback;
                AddOutline(rt);
            }
            return img;
        }

        // 9-sliced button with sprite-swap hover/press, or flat fallback.
        void Btn(RectTransform parent, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            var rt = MakeRect($"Btn_{label}", parent);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            if (_spButton != null)
            {
                img.sprite = _spButton;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
                btn.transition = Selectable.Transition.SpriteSwap;
                btn.spriteState = new SpriteState
                {
                    highlightedSprite = _spButtonHover != null ? _spButtonHover : _spButton,
                    pressedSprite = _spButtonPressed != null ? _spButtonPressed : (_spButtonHover != null ? _spButtonHover : _spButton),
                    selectedSprite = _spButtonHover != null ? _spButtonHover : _spButton,
                };
            }
            else
            {
                img.color = SectionBg;
                AddOutline(rt);
                var cb = btn.colors;
                cb.normalColor = SectionBg;
                cb.highlightedColor = new Color(0.22f, 0.26f, 0.34f, 1f);
                cb.pressedColor = new Color(0.10f, 0.12f, 0.18f, 1f);
                btn.colors = cb;
            }
            btn.onClick.AddListener(() => onClick());

            var t = Label(rt, label, Vector2.zero, 16);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
        }

        // Slider skinned with slider_track (bg) + slider_handle (handle), or flat.
        // Mirrors WelcomeScreenManager's proven handle-as-direct-child setup.
        Slider MakeSlider(RectTransform parent, Vector2 pos, Vector2 size)
        {
            var rt = MakeRect("Slider", parent);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var bg = rt.gameObject.AddComponent<Image>();
            if (_spSliderTrack != null)
            {
                bg.sprite = _spSliderTrack;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.color = InputBg;
                AddOutline(rt);
            }

            var slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = null;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = true;

            var handle = MakeRect("Handle", rt);
            handle.sizeDelta = new Vector2(18, size.y + 6);
            var hImg = handle.gameObject.AddComponent<Image>();
            if (_spSliderHandle != null)
            {
                hImg.sprite = _spSliderHandle;
                hImg.type = Image.Type.Simple;
                hImg.color = Color.white;
            }
            else
            {
                hImg.color = HandleCol;
            }
            slider.handleRect = handle;
            slider.targetGraphic = hImg;
            return slider;
        }

        Text Label(RectTransform parent, string text, Vector2 pos, int size,
            bool bold = false, bool alignLeft = false)
        {
            var rt = MakeRect("Label", parent);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280, 30);
            var t = rt.gameObject.AddComponent<Text>();
            LitIsoFont.Apply(t, size, bold ? FontStyle.Bold : FontStyle.Normal);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = t.fontSize;
            t.resizeTextMinSize = 10;
            t.text = text;
            t.color = TextCol;
            t.alignment = alignLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return t;
        }

        void OnDestroy()
        {
            if (_baked?.texture != null) Destroy(_baked.texture);
        }
    }
}

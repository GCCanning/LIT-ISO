using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Player-authored layout helper for procedural uGUI panels.
    /// Hold Alt and drag the top strip to move, or the lower-right handle to resize.
    /// </summary>
    public sealed class PlayerResizableUi : MonoBehaviour
    {
        RectTransform _target;
        string _id;
        Vector2 _defaultPosition;
        Vector2 _defaultSize;
        Vector2 _minSize;
        Vector2 _maxSize;

        const string Prefix = "ui.layout.";
        const float HandleSize = 28f;

        public static PlayerResizableUi Attach(RectTransform target, string id, Vector2 minSize, Vector2 maxSize)
        {
            if (target == null || string.IsNullOrWhiteSpace(id))
                return null;

            var layout = target.GetComponent<PlayerResizableUi>();
            if (layout == null)
                layout = target.gameObject.AddComponent<PlayerResizableUi>();

            layout.Configure(target, id, minSize, maxSize);
            return layout;
        }

        public void Configure(RectTransform target, string id, Vector2 minSize, Vector2 maxSize)
        {
            _target = target;
            _id = id.Trim();
            _defaultPosition = target.anchoredPosition;
            _defaultSize = target.sizeDelta;
            _minSize = minSize;
            _maxSize = maxSize;
            Load();
            EnsureHandles();
        }

        void Update()
        {
            if (_target == null || string.IsNullOrEmpty(_id))
                return;

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                    Input.GetKeyDown(KeyCode.R))
                {
                    ResetLayout();
                }
            }
        }

        void Load()
        {
            if (!PlayerPrefs.HasKey(Key("x")))
                return;

            _target.anchoredPosition = new Vector2(
                PlayerPrefs.GetFloat(Key("x"), _defaultPosition.x),
                PlayerPrefs.GetFloat(Key("y"), _defaultPosition.y));
            _target.sizeDelta = new Vector2(
                Mathf.Clamp(PlayerPrefs.GetFloat(Key("w"), _defaultSize.x), _minSize.x, _maxSize.x),
                Mathf.Clamp(PlayerPrefs.GetFloat(Key("h"), _defaultSize.y), _minSize.y, _maxSize.y));
        }

        void Save()
        {
            if (_target == null || string.IsNullOrEmpty(_id))
                return;

            PlayerPrefs.SetFloat(Key("x"), _target.anchoredPosition.x);
            PlayerPrefs.SetFloat(Key("y"), _target.anchoredPosition.y);
            PlayerPrefs.SetFloat(Key("w"), _target.sizeDelta.x);
            PlayerPrefs.SetFloat(Key("h"), _target.sizeDelta.y);
            PlayerPrefs.Save();
        }

        void ResetLayout()
        {
            _target.anchoredPosition = _defaultPosition;
            _target.sizeDelta = _defaultSize;
            PlayerPrefs.DeleteKey(Key("x"));
            PlayerPrefs.DeleteKey(Key("y"));
            PlayerPrefs.DeleteKey(Key("w"));
            PlayerPrefs.DeleteKey(Key("h"));
            PlayerPrefs.Save();
        }

        string Key(string suffix) => Prefix + _id + "." + suffix;

        void EnsureHandles()
        {
            EnsureHandle("LayoutMoveHandle", false);
            EnsureHandle("LayoutResizeHandle", true);
        }

        void EnsureHandle(string name, bool resize)
        {
            var existing = _target.Find(name);
            var go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_target, false);

            var image = go.GetComponent<Image>();
            if (image == null)
                image = go.AddComponent<Image>();
            image.color = resize
                ? new Color(1f, 0.86f, 0.35f, 0.16f)
                : new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            var rt = go.GetComponent<RectTransform>();
            if (resize)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(HandleSize, HandleSize);
            }
            else
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0f, HandleSize);
            }

            var handle = go.GetComponent<PlayerResizableUiHandle>();
            if (handle == null)
                handle = go.AddComponent<PlayerResizableUiHandle>();
            handle.Init(this, resize);
        }

        sealed class PlayerResizableUiHandle : MonoBehaviour, ICanvasRaycastFilter, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            PlayerResizableUi _owner;
            bool _resize;
            Vector2 _startPointer;
            Vector2 _startPosition;
            Vector2 _startSize;

            public void Init(PlayerResizableUi owner, bool resize)
            {
                _owner = owner;
                _resize = resize;
            }

            public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera) => AltHeld();

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (_owner?._target == null)
                    return;

                _startPointer = eventData.position;
                _startPosition = _owner._target.anchoredPosition;
                _startSize = _owner._target.sizeDelta;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (_owner?._target == null || !AltHeld())
                    return;

                float scale = 1f;
                var canvas = _owner._target.GetComponentInParent<Canvas>();
                if (canvas != null && canvas.scaleFactor > 0.001f)
                    scale = canvas.scaleFactor;

                Vector2 delta = (eventData.position - _startPointer) / scale;
                if (_resize)
                {
                    Vector2 next = _startSize + new Vector2(delta.x, -delta.y);
                    next.x = Mathf.Clamp(next.x, _owner._minSize.x, _owner._maxSize.x);
                    next.y = Mathf.Clamp(next.y, _owner._minSize.y, _owner._maxSize.y);
                    _owner._target.sizeDelta = next;
                }
                else
                {
                    _owner._target.anchoredPosition = _startPosition + delta;
                }
            }

            public void OnEndDrag(PointerEventData eventData) => _owner?.Save();

            static bool AltHeld() => Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }
}

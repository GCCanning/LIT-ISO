using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Companion to <see cref="LitIsoTheme"/> button styling. Reproduces the design
/// system's tactile press: while the pointer is held the button rect shifts
/// DOWN 4px and its 5px hard bottom shadow flattens to 1px (so the button reads
/// as "pressed into" the surface). Releasing restores the rest pose.
///
/// Driven by uGUI pointer events (no per-frame polling). Stores and restores the
/// button's authored anchoredPosition, so it is safe on rects positioned by the
/// menu OR by layout groups (it only animates the local offset, then snaps back).
/// </summary>
[DisallowMultipleComponent]
public sealed class LitIsoButtonPress : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private Button _btn;
    private RectTransform _rect;
    private RectTransform _shadow;
    private Vector2 _restPos;
    private Vector2 _shadowRestPos;
    private bool _captured;
    private bool _down;

    public void Configure(Button btn, RectTransform rect, RectTransform shadow)
    {
        _btn = btn;
        _rect = rect;
        _shadow = shadow;
        _captured = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (_btn != null && !_btn.interactable) return;
        // Re-read the live rest pose at press time so buttons positioned by a
        // layout group (which may have moved them since Configure) still spring
        // back to the correct spot on release.
        _restPos = _rect != null ? _rect.anchoredPosition : Vector2.zero;
        if (_shadow != null) _shadowRestPos = _shadow.anchoredPosition;
        _captured = true;
        _down = true;
        Apply(true);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (!_down) return;
        _down = false;
        Apply(false);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!_down) return;
        _down = false;
        Apply(false);
    }

    private void Apply(bool pressed)
    {
        if (_rect == null) return;
        _rect.anchoredPosition = pressed ? _restPos + new Vector2(0f, -4f) : _restPos;
        if (_shadow != null)
            _shadow.anchoredPosition = pressed
                ? _shadowRestPos + new Vector2(0f, 4f)  // shadow rises so it reads as ~1px
                : _shadowRestPos;
    }

    private void OnDisable()
    {
        if (_down) { _down = false; Apply(false); }
    }
}

using UnityEngine;

/// <summary>
/// Smooth camera zoom controller with keyboard, mouse-wheel, and UI button support.
///
/// Keyboard shortcuts:
///   - Ctrl + '=' or Ctrl + '+' : zoom in
///   - Ctrl + '-'              : zoom out
///   - Hold the keys for smooth continuous zoom
///   - Mouse wheel (optional)  : zoom in/out
///
/// Public API for UI buttons:
///   - <see cref="ZoomIn"/>      — Step zoom in
///   - <see cref="ZoomOut"/>     — Step zoom out
///   - <see cref="SetZoom"/>     — Set a specific zoom level
///   - <see cref="GetZoomPercent"/> — Get current zoom as 0-1 (for UI sliders)
///
/// Attach to the Main Camera GameObject.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ZoomController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Zoom Range")]
    [Tooltip("Minimum orthographicSize (closest zoom-in).")]
    public float minZoom = 3f;

    [Tooltip("Maximum orthographicSize (farthest zoom-out).")]
    public float maxZoom = 14f;

    [Tooltip("Starting orthographicSize. Will be clamped to [minZoom, maxZoom].")]
    public float defaultZoom = 6f;

    [Header("Input Speed")]
    [Tooltip("Continuous zoom speed (units of orthographicSize per second) when holding Ctrl +/-.")]
    public float holdZoomSpeed = 6f;

    [Tooltip("Step amount applied on initial tap of Ctrl +/- (instant feedback).")]
    public float tapZoomStep = 1f;

    [Tooltip("Mouse wheel zoom speed (negative to invert).")]
    public float scrollZoomSpeed = 2.5f;

    [Header("Smoothing")]
    [Tooltip("How quickly current zoom catches up to target. Higher = snappier.")]
    [Range(2f, 20f)]
    public float smoothing = 10f;

    [Header("Input")]
    [Tooltip("Require Ctrl to be held for +/- to zoom (true = standard, false = always zoom on +/-).")]
    public bool requireCtrl = true;

    [Tooltip("Enable mouse-wheel zoom.")]
    public bool enableScrollWheel = true;

    [Header("Debug — Read-only")]
    [SerializeField] private float targetZoom;
    [SerializeField] private float currentZoom;

    // -------------------------------------------------------------------------
    // Runtime
    // -------------------------------------------------------------------------

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        targetZoom = Mathf.Clamp(defaultZoom, minZoom, maxZoom);
        currentZoom = targetZoom;
        if (cam.orthographic)
        {
            cam.orthographicSize = currentZoom;
        }
    }

    private void Update()
    {
        HandleKeyboardInput();
        HandleScrollInput();
        ApplyZoom();
    }

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    private void HandleKeyboardInput()
    {
        bool ctrlHeld = !requireCtrl || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrlHeld) return;

        // Detect plus and minus keys. Plus is usually Shift+'=' on most keyboards,
        // so we accept both '=' and the keypad/regular '+'.
        bool zoomInHeld = Input.GetKey(KeyCode.Equals)
                       || Input.GetKey(KeyCode.Plus)
                       || Input.GetKey(KeyCode.KeypadPlus);

        bool zoomOutHeld = Input.GetKey(KeyCode.Minus)
                        || Input.GetKey(KeyCode.KeypadMinus);

        // Continuous zoom while held (smooth)
        if (zoomInHeld)
        {
            targetZoom -= holdZoomSpeed * Time.deltaTime;
        }
        if (zoomOutHeld)
        {
            targetZoom += holdZoomSpeed * Time.deltaTime;
        }

        // Extra immediate kick on the frame the key was first pressed
        // (so a tap gives a noticeable change before holding kicks in).
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            targetZoom -= tapZoomStep;
        }
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            targetZoom += tapZoomStep;
        }

        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
    }

    private void HandleScrollInput()
    {
        if (!enableScrollWheel) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            targetZoom -= scroll * scrollZoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }
    }

    private void ApplyZoom()
    {
        // Frame-rate independent exponential smoothing.
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        currentZoom = Mathf.Lerp(currentZoom, targetZoom, t);

        if (cam != null && cam.orthographic)
        {
            cam.orthographicSize = currentZoom;
        }
    }

    // -------------------------------------------------------------------------
    // Public API (for UI buttons or other scripts)
    // -------------------------------------------------------------------------

    /// <summary>Apply a stepped zoom-in (called by UI button).</summary>
    public void ZoomIn()
    {
        targetZoom = Mathf.Clamp(targetZoom - tapZoomStep * 2f, minZoom, maxZoom);
    }

    /// <summary>Apply a stepped zoom-out (called by UI button).</summary>
    public void ZoomOut()
    {
        targetZoom = Mathf.Clamp(targetZoom + tapZoomStep * 2f, minZoom, maxZoom);
    }

    /// <summary>Set a specific zoom level (1.0 = minZoom, 0.0 = maxZoom).</summary>
    public void SetZoom(float orthographicSize)
    {
        targetZoom = Mathf.Clamp(orthographicSize, minZoom, maxZoom);
    }

    /// <summary>Reset to default zoom level.</summary>
    public void ResetZoom()
    {
        targetZoom = Mathf.Clamp(defaultZoom, minZoom, maxZoom);
    }

    /// <summary>
    /// Returns current zoom as 0-1 (0 = zoomed-out max, 1 = zoomed-in max).
    /// Useful for UI sliders showing current zoom level.
    /// </summary>
    public float GetZoomPercent()
    {
        float t = Mathf.InverseLerp(maxZoom, minZoom, currentZoom);
        return Mathf.Clamp01(t);
    }
}

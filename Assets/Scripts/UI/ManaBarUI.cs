using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the player's mana bar.
/// Mirrors HealthBarUI in structure — subscribes to PlayerMana.OnManaChanged.
///
/// Setup:
///   1. Add this component to a panel in your HUD Canvas alongside HealthBarUI.
///   2. Assign fillImage (Filled, Horizontal) and optional manaText.
/// </summary>
public class ManaBarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Fill image. Set Image.type = Filled, FillMethod = Horizontal.")]
    public Image fillImage;

    [Tooltip("Optional text showing 'current / max'.")]
    public Text manaText;

    [Header("Colours")]
    public Color fullManaColor = new Color(0.25f, 0.55f, 1.00f);
    public Color lowManaColor  = new Color(0.50f, 0.25f, 0.80f);

    [Header("Animation")]
    public float fillSmoothSpeed    = 7f;
    [Range(0f, 0.5f)]
    public float lowManaThreshold   = 0.25f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private float targetFill  = 1f;
    private float displayFill = 1f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()  => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Start()
    {
        Unsubscribe();
        Subscribe();

        if (PlayerMana.Instance != null)
            HandleManaChanged(PlayerMana.Instance.CurrentMana, PlayerMana.Instance.MaxMana);
    }

    private void Update()
    {
        if (fillImage == null) return;

        displayFill       = Mathf.Lerp(displayFill, targetFill, Time.deltaTime * fillSmoothSpeed);
        fillImage.fillAmount = displayFill;

        float t = Mathf.Clamp01(displayFill / Mathf.Max(0.001f, lowManaThreshold + 0.2f));
        fillImage.color = Color.Lerp(lowManaColor, fullManaColor, t);
    }

    // -------------------------------------------------------------------------
    // Event handling
    // -------------------------------------------------------------------------

    private void Subscribe()
    {
        if (PlayerMana.Instance != null)
            PlayerMana.Instance.OnManaChanged += HandleManaChanged;
    }

    private void Unsubscribe()
    {
        if (PlayerMana.Instance != null)
            PlayerMana.Instance.OnManaChanged -= HandleManaChanged;
    }

    private void HandleManaChanged(int current, int max)
    {
        targetFill = max > 0 ? (float)current / max : 0f;

        if (manaText != null)
            manaText.text = $"{current} / {max}";
    }
}

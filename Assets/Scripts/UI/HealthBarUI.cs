using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a health bar and portrait at the bottom-left of the screen.
///
/// Subscribes to PlayerHealth.OnHealthChanged.
/// The fill smoothly lerps to the target value each Update (configurable speed).
/// The bar colour transitions from <see cref="fullHealthColor"/> (top) toward
/// <see cref="lowHealthColor"/> (bottom) as health drops.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("References (auto-wired by GameplayLayerSetup)")]
    [Tooltip("The small portrait sprite to the left of the bar. Optional.")]
    public Image portraitImage;

    [Tooltip("The coloured fill rect. Must have Image.type = Filled, FillMethod = Horizontal.")]
    public Image fillImage;

    [Tooltip("Optional text showing 'current/max'. Legacy Text component.")]
    public Text healthText;

    [Header("Colours")]
    public Color fullHealthColor = new Color(0.78f, 0.14f, 0.14f, 1f);
    public Color lowHealthColor  = new Color(0.90f, 0.58f, 0.10f, 1f);

    [Header("Animation")]
    [Tooltip("Higher = snappier bar drain.")]
    public float fillSmoothSpeed = 7f;

    [Tooltip("Fraction at or below which the bar adopts lowHealthColor.")]
    [Range(0f, 0.5f)]
    public float lowHealthThreshold = 0.30f;

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

        if (PlayerHealth.Instance != null)
            HandleHealthChanged(PlayerHealth.Instance.CurrentHealth, PlayerHealth.Instance.maxHealth);
    }

    private void Update()
    {
        if (fillImage == null) return;

        displayFill = Mathf.Lerp(displayFill, targetFill, Time.deltaTime * fillSmoothSpeed);
        fillImage.fillAmount = displayFill;

        // Lerp colour: full health → fullHealthColor, low health → lowHealthColor
        float t = Mathf.Clamp01(displayFill / Mathf.Max(0.001f, lowHealthThreshold + 0.2f));
        fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, t);
    }

    // -------------------------------------------------------------------------
    // Event handling
    // -------------------------------------------------------------------------

    private void Subscribe()
    {
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.OnHealthChanged += HandleHealthChanged;
    }

    private void Unsubscribe()
    {
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int current, int max)
    {
        targetFill = max > 0 ? (float)current / max : 0f;

        if (healthText != null)
            healthText.text = $"{current} / {max}";
    }
}

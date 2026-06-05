using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the player's XP progress bar and current level.
///
/// Subscribes to XPSystem events — no polling required.
///
/// Setup:
///   1. Add this component to a panel in your HUD Canvas.
///   2. Assign fillImage (Filled type, Horizontal fill), levelText, and xpText.
///
/// The bar smoothly lerps to the target fill to give a satisfying XP drain feel.
/// </summary>
public class XPBarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Fill image for the XP bar. Set Image.type = Filled, FillMethod = Horizontal.")]
    public Image fillImage;

    [Tooltip("Text showing current level number.")]
    public Text levelText;

    [Tooltip("Optional text showing 'XP / XP_to_next' or percentage.")]
    public Text xpText;

    [Header("Colours")]
    public Color xpBarColor    = new Color(0.4f, 0.9f, 1.0f);   // Cyan
    public Color maxLevelColor = new Color(1.0f, 0.84f, 0.0f);  // Gold when max level

    [Header("Animation")]
    [Tooltip("How fast the bar fill lerps to the target value.")]
    public float fillSmoothSpeed = 5f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private float targetFill  = 0f;
    private float displayFill = 0f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()
    {
        XPSystem.OnXPGained += HandleXPGained;
        XPSystem.OnLevelUp  += HandleLevelUp;
    }

    private void OnDisable()
    {
        XPSystem.OnXPGained -= HandleXPGained;
        XPSystem.OnLevelUp  -= HandleLevelUp;
    }

    private void Start()
    {
        OnEnable();
        Refresh();
    }

    private void Update()
    {
        if (fillImage == null) return;

        displayFill = Mathf.Lerp(displayFill, targetFill, Time.deltaTime * fillSmoothSpeed);
        fillImage.fillAmount = displayFill;
    }

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    private void HandleXPGained(int gained, int total) => Refresh();
    private void HandleLevelUp(int newLevel)           => Refresh();

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void Refresh()
    {
        if (XPSystem.Instance == null) return;

        int level      = XPSystem.Instance.CurrentLevel;
        int xpIn       = XPSystem.Instance.XPInCurrentLevel;
        int xpNeeded   = XPSystem.Instance.XPNeededForNextLevel;

        bool isMaxLevel = xpNeeded == int.MaxValue;

        targetFill = isMaxLevel ? 1f : (xpNeeded > 0 ? (float)xpIn / xpNeeded : 0f);

        if (fillImage != null)
            fillImage.color = isMaxLevel ? maxLevelColor : xpBarColor;

        if (levelText != null)
            levelText.text = $"Lv.{level}";

        if (xpText != null)
            xpText.text = isMaxLevel ? "MAX" : $"{xpIn} / {xpNeeded}";
    }
}

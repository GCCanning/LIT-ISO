using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Full stats panel UI.
/// Wire up in the inspector — no hard-coded layout dependencies.
///
/// Toggle visibility with TogglePanel() or bind to a key.
/// Opens/closes with Tab or a configured button.
/// </summary>
public class StatSheetUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Panel")]
    public GameObject panelRoot;

    [Header("Character Info")]
    public TMP_Text nameText;
    public TMP_Text classText;
    public TMP_Text levelText;
    public TMP_Text titleText;
    public Image    classIcon;

    [Header("Core Stats")]
    public TMP_Text strText;
    public TMP_Text agiText;
    public TMP_Text vitText;
    public TMP_Text intText;
    public TMP_Text wisText;
    public TMP_Text endText;
    public TMP_Text statPointsText;

    [Header("Derived Stats")]
    public TMP_Text maxHPText;
    public TMP_Text maxManaText;
    public TMP_Text moveSpeedText;
    public TMP_Text meleeDmgText;
    public TMP_Text spellDmgText;
    public TMP_Text critChanceText;
    public TMP_Text manaRegenText;
    public TMP_Text dashCountText;
    public TMP_Text cooldownRedText;

    [Header("Allocate Buttons (optional)")]
    public Button allocateSTR, allocateAGI, allocateVIT, allocateINT, allocateWIS, allocateEND;

    [Header("Toggle Key")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Currency")]
    public TMP_Text currencyText;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Wire allocation buttons
        if (allocateSTR) allocateSTR.onClick.AddListener(() => AllocateStat(PlayerStats.StatType.STR));
        if (allocateAGI) allocateAGI.onClick.AddListener(() => AllocateStat(PlayerStats.StatType.AGI));
        if (allocateVIT) allocateVIT.onClick.AddListener(() => AllocateStat(PlayerStats.StatType.VIT));
        if (allocateINT) allocateINT.onClick.AddListener(() => AllocateStat(PlayerStats.StatType.INT));
        if (allocateWIS) allocateWIS.onClick.AddListener(() => AllocateStat(PlayerStats.StatType.WIS));
        if (allocateEND) allocateEND.onClick.AddListener(() => AllocateStat(PlayerStats.StatType.END));
    }

    private void OnEnable()
    {
        PlayerStats.OnStatsChanged += Refresh;
        XPSystem.OnLevelUp         += _ => Refresh();
        ClassSystem.OnClassAssigned += _ => Refresh();
    }

    private void OnDisable()
    {
        PlayerStats.OnStatsChanged  -= Refresh;
        XPSystem.OnLevelUp          -= _ => Refresh();
        ClassSystem.OnClassAssigned -= _ => Refresh();
    }

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        Refresh();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey)) TogglePanel();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void TogglePanel()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(!panelRoot.activeSelf);
        if (panelRoot.activeSelf) Refresh();
    }

    public void Refresh()
    {
        RefreshCharacterInfo();
        RefreshCoreStats();
        RefreshDerivedStats();
        RefreshCurrency();
        RefreshAllocateButtons();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void RefreshCharacterInfo()
    {
        if (levelText != null && XPSystem.Instance != null)
            levelText.text = $"Level {XPSystem.Instance.CurrentLevel}";

        if (classText != null)
        {
            var cls = ClassSystem.Instance?.AssignedClass;
            classText.text = cls != null
                ? $"{cls.className} ({cls.rarity})"
                : "Unassigned";
            if (classText != null && cls != null)
                classText.color = cls.RarityColor();
        }

        if (classIcon != null)
        {
            var cls = ClassSystem.Instance?.AssignedClass;
            if (cls?.icon != null) classIcon.sprite = cls.icon;
        }

        if (titleText != null)
        {
            var t = TitleSystem.Instance?.EquippedTitle;
            titleText.text = t != null ? $"[{t.titleName}]" : "";
        }
    }

    private void RefreshCoreStats()
    {
        var s = PlayerStats.Instance;
        if (s == null) return;

        SetText(strText,       $"STR  {s.STR}");
        SetText(agiText,       $"AGI  {s.AGI}");
        SetText(vitText,       $"VIT  {s.VIT}");
        SetText(intText,       $"INT  {s.INT}");
        SetText(wisText,       $"WIS  {s.WIS}");
        SetText(endText,       $"END  {s.END}");
        SetText(statPointsText,$"Points Available: {s.StatPointsAvailable}");
    }

    private void RefreshDerivedStats()
    {
        var s = PlayerStats.Instance;
        if (s == null) return;

        SetText(maxHPText,      $"Max HP      {s.MaxHP:F0}");
        SetText(maxManaText,    $"Max Mana    {s.MaxMana:F0}");
        SetText(moveSpeedText,  $"Move Speed  {s.MoveSpeed:F2}");
        SetText(meleeDmgText,   $"Melee Dmg   {s.MeleeDmg:F1}");
        SetText(spellDmgText,   $"Spell Dmg   {s.SpellDmg:F1}");
        SetText(critChanceText, $"Crit Chance {s.CritChance * 100f:F1}%");
        SetText(manaRegenText,  $"Mana Regen  {s.ManaRegen:F2}/s");
        SetText(dashCountText,  $"Dashes      {s.DashCount}");
        SetText(cooldownRedText,$"CDR         {s.CooldownReduction * 100f:F0}%");
    }

    private void RefreshCurrency()
    {
        if (currencyText == null || CurrencySystem.Instance == null) return;
        currencyText.text = CurrencySystem.Instance.FormatWallet();
    }

    private void RefreshAllocateButtons()
    {
        bool hasPoints = PlayerStats.Instance != null && PlayerStats.Instance.StatPointsAvailable > 0;
        if (allocateSTR) allocateSTR.interactable = hasPoints;
        if (allocateAGI) allocateAGI.interactable = hasPoints;
        if (allocateVIT) allocateVIT.interactable = hasPoints;
        if (allocateINT) allocateINT.interactable = hasPoints;
        if (allocateWIS) allocateWIS.interactable = hasPoints;
        if (allocateEND) allocateEND.interactable = hasPoints;
    }

    private void AllocateStat(PlayerStats.StatType stat)
    {
        PlayerStats.Instance?.AllocateStat(stat);
        // Refresh is driven by OnStatsChanged event
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }
}

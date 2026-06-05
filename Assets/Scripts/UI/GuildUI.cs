using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Guild panel UI — found guild, view members, post quests, manage ranks.
///
/// Call Open() from a Guild Hall NPC/sign interaction.
/// </summary>
public class GuildUI : MonoBehaviour
{
    public static GuildUI Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Panel")]
    public GameObject panelRoot;

    [Header("No Guild View")]
    public GameObject noGuildView;
    public TMP_InputField guildNameInput;
    public Button         foundGuildButton;

    [Header("Guild View")]
    public GameObject guildView;
    public TMP_Text   guildNameText;
    public TMP_Text   guildTierText;
    public TMP_Text   guildPointsText;
    public TMP_Text   memberCountText;

    [Header("Member List")]
    public Transform   memberListParent;
    public GameObject  memberRowPrefab;   // Row: name, rank, GP contributed, promote/kick buttons

    [Header("Quest Board")]
    public Transform  questListParent;
    public GameObject questRowPrefab;    // Row: quest title, start button

    private readonly List<GameObject> _memberRows = new();
    private readonly List<GameObject> _questRows  = new();

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (foundGuildButton != null)
            foundGuildButton.onClick.AddListener(OnFoundGuildClicked);

        GuildManager.OnGuildFounded   += _ => Refresh();
        GuildManager.OnMemberJoined   += (_, _) => Refresh();
        GuildManager.OnMemberLeft     += (_, _) => Refresh();
        GuildManager.OnGuildTierUp    += (_, _) => Refresh();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void Refresh()
    {
        var guild = GuildManager.Instance?.FindMemberGuild("local_player");

        bool inGuild = guild != null;
        if (noGuildView != null) noGuildView.SetActive(!inGuild);
        if (guildView   != null) guildView.SetActive(inGuild);

        if (!inGuild) return;

        // Header
        var cfg = GuildManager.Instance?.config;
        if (guildNameText   != null) guildNameText.text   = guild.guildName;
        if (guildTierText   != null) guildTierText.text   = cfg != null ? cfg.tierNames[Mathf.Clamp(guild.tier, 0, 4)] : guild.tier.ToString();
        if (guildPointsText != null) guildPointsText.text = $"{guild.guildPoints} GP";
        if (memberCountText != null)
        {
            int cap = cfg != null ? cfg.memberCaps[Mathf.Clamp(guild.tier, 0, 4)] : 20;
            memberCountText.text = $"{guild.MemberCount} / {cap} members";
        }

        // Members
        foreach (var r in _memberRows) Destroy(r);
        _memberRows.Clear();

        if (memberListParent != null && memberRowPrefab != null)
        {
            foreach (var member in guild.members)
            {
                var row = Instantiate(memberRowPrefab, memberListParent);
                _memberRows.Add(row);

                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length > 0) texts[0].text = member.displayName;
                if (texts.Length > 1) texts[1].text = member.RankName;
                if (texts.Length > 2) texts[2].text = $"{member.contributedGP} GP";
            }
        }

        // Quest board
        foreach (var r in _questRows) Destroy(r);
        _questRows.Clear();

        if (questListParent != null && questRowPrefab != null)
        {
            foreach (var qid in guild.activeQuestIds)
            {
                var row = Instantiate(questRowPrefab, questListParent);
                _questRows.Add(row);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                if (texts.Length > 0) texts[0].text = qid;

                var btn = row.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    var capturedId = qid;
                    btn.onClick.AddListener(() => QuestManager.Instance?.StartQuest(capturedId));
                }
            }
        }
    }

    private void OnFoundGuildClicked()
    {
        string name = guildNameInput != null ? guildNameInput.text.Trim() : "New Guild";
        if (string.IsNullOrEmpty(name)) return;

        var inv = FindFirstObjectByType<PlayerInventory>();
        GuildManager.Instance?.FoundGuild(name, "default", "local_player", "Player", inv);
        Refresh();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD overlay showing active quest titles and objective progress.
/// Subscribes to QuestManager events — no polling.
///
/// Set up: place a vertical LayoutGroup panel in the corner of your canvas,
/// assign questEntryPrefab (a row with titleText + objectiveText), and assign
/// the parent transform to entryParent.
/// </summary>
public class QuestTrackerUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Layout")]
    public Transform entryParent;
    public GameObject questEntryPrefab;   // Must have TMP_Text children tagged "Title" & "Objective"
    [Tooltip("Max number of quests shown simultaneously.")]
    [Min(1)] public int maxVisible = 5;

    [Header("Colours")]
    public Color completedObjectiveColor = new Color(0.5f, 1f, 0.5f);
    public Color activeObjectiveColor    = Color.white;
    public Color questTitleColor         = new Color(1f, 0.85f, 0.3f);

    // -------------------------------------------------------------------------
    // Runtime
    // -------------------------------------------------------------------------

    private readonly Dictionary<string, QuestEntryView> views = new();

    private class QuestEntryView
    {
        public GameObject    root;
        public TMP_Text      titleText;
        public TMP_Text      objectiveText;
        public QuestDefinition def;
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()
    {
        QuestManager.OnQuestStarted      += HandleQuestStarted;
        QuestManager.OnObjectiveProgress += HandleObjectiveProgress;
        QuestManager.OnQuestCompleted    += HandleQuestCompleted;
    }

    private void OnDisable()
    {
        QuestManager.OnQuestStarted      -= HandleQuestStarted;
        QuestManager.OnObjectiveProgress -= HandleObjectiveProgress;
        QuestManager.OnQuestCompleted    -= HandleQuestCompleted;
    }

    // -------------------------------------------------------------------------
    // Event handlers
    // -------------------------------------------------------------------------

    private void HandleQuestStarted(QuestDefinition def)
    {
        if (views.Count >= maxVisible) return;
        if (questEntryPrefab == null || entryParent == null) return;
        if (views.ContainsKey(def.questId)) return;

        var go    = Instantiate(questEntryPrefab, entryParent);
        var texts = go.GetComponentsInChildren<TMP_Text>();

        var entry = new QuestEntryView { root = go, def = def };

        // Heuristic: first TMP_Text = title, second = objectives
        if (texts.Length > 0) { entry.titleText = texts[0]; entry.titleText.color = questTitleColor; }
        if (texts.Length > 1)   entry.objectiveText = texts[1];

        if (entry.titleText != null)     entry.titleText.text = def.title;
        if (entry.objectiveText != null) entry.objectiveText.text = BuildObjectiveText(def);

        views[def.questId] = entry;
    }

    private void HandleObjectiveProgress(QuestDefinition def, int objIndex, int current, int required)
    {
        if (!views.TryGetValue(def.questId, out var entry)) return;
        if (entry.objectiveText != null)
            entry.objectiveText.text = BuildObjectiveText(def, objIndex, current, required);
    }

    private void HandleQuestCompleted(QuestDefinition def)
    {
        if (!views.TryGetValue(def.questId, out var entry)) return;

        // Flash green then remove
        if (entry.titleText != null)     entry.titleText.color     = completedObjectiveColor;
        if (entry.objectiveText != null) entry.objectiveText.color = completedObjectiveColor;

        StartCoroutine(RemoveAfterDelay(def.questId, entry.root, 2f));
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private string BuildObjectiveText(QuestDefinition def, int highlightIdx = -1, int current = 0, int required = 0)
    {
        if (def.objectives == null || def.objectives.Length == 0) return "";

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < def.objectives.Length; i++)
        {
            var obj = def.objectives[i];
            string label = string.IsNullOrEmpty(obj.displayLabel)
                ? $"{obj.type} {obj.targetId}"
                : obj.displayLabel;

            if (i == highlightIdx)
                sb.AppendLine($"• {label} {current}/{required}");
            else
                sb.AppendLine($"• {label}");
        }
        return sb.ToString().TrimEnd();
    }

    private System.Collections.IEnumerator RemoveAfterDelay(string questId, GameObject go, float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        if (views.ContainsKey(questId)) views.Remove(questId);
        if (go != null) Destroy(go);
    }
}

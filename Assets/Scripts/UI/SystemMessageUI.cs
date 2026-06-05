using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays System notifications as animated banner messages on screen.
/// Subscribes to SystemNotifier.OnMessage and queues banners for display.
///
/// Setup:
///   1. Add this component to a Canvas panel (ideally an overlay Canvas at the top of screen).
///   2. Assign the messagePanel, messageText, and backgroundImage references.
///   3. The panel will auto-manage visibility — no manual enable/disable needed.
///
/// Each message:
///   - Fades in over fadeInDuration
///   - Holds for holdDuration
///   - Fades out over fadeOutDuration
///   - Up to maxVisible messages stack vertically
///
/// To change colours per message type: edit the TypeColor array in the Inspector.
/// </summary>
public class SystemMessageUI : MonoBehaviour
{
    [Header("Prefab / Panel References")]
    [Tooltip("Prefab for a single message row. Must have a Text child named 'MessageText'.")]
    public GameObject messagePrefab;

    [Tooltip("Parent RectTransform where message rows are instantiated.")]
    public RectTransform messageContainer;

    [Header("Animation")]
    [Tooltip("Seconds to fade the message in.")]
    public float fadeInDuration  = 0.3f;

    [Tooltip("Seconds the message stays fully visible.")]
    public float holdDuration    = 3.5f;

    [Tooltip("Seconds to fade the message out.")]
    public float fadeOutDuration = 0.6f;

    [Tooltip("Maximum messages visible at once before older ones are removed.")]
    public int maxVisible = 5;

    [Header("Type Colours")]
    public Color colorInfo        = Color.white;
    public Color colorLevelUp     = new Color(0.4f, 0.9f, 1.0f);   // Cyan
    public Color colorClassAssign = new Color(1.0f, 0.84f, 0.0f);  // Gold
    public Color colorDungeonClear= new Color(0.2f, 0.9f, 0.3f);   // Green
    public Color colorWorldEvent  = new Color(1.0f, 0.35f, 0.15f); // Orange-red
    public Color colorWarning     = new Color(1.0f, 0.9f, 0.1f);   // Yellow
    public Color colorAchievement = new Color(0.7f, 0.4f, 1.0f);   // Purple

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Queue<(string text, SystemNotifier.MessageType type)> pending = new();
    private List<GameObject> activeRows = new();
    private bool isShowing = false;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()  => SystemNotifier.OnMessage += EnqueueMessage;
    private void OnDisable() => SystemNotifier.OnMessage -= EnqueueMessage;

    private void Start()
    {
        // Hook up in case SystemNotifier was already awake
        SystemNotifier.OnMessage -= EnqueueMessage;
        SystemNotifier.OnMessage += EnqueueMessage;
    }

    private void Update()
    {
        if (!isShowing && pending.Count > 0)
            StartCoroutine(ShowNext());
    }

    // -------------------------------------------------------------------------
    // Message handling
    // -------------------------------------------------------------------------

    private void EnqueueMessage(string text, SystemNotifier.MessageType type)
    {
        pending.Enqueue((text, type));
    }

    private IEnumerator ShowNext()
    {
        isShowing = true;

        while (pending.Count > 0)
        {
            var (text, type) = pending.Dequeue();

            // Enforce max visible
            while (activeRows.Count >= maxVisible)
            {
                Destroy(activeRows[0]);
                activeRows.RemoveAt(0);
            }

            // Spawn row
            if (messagePrefab == null || messageContainer == null)
            {
                // Fallback: just log to console if UI isn't wired up
                Debug.Log($"[SYSTEM UI] {text}");
                yield return new WaitForSeconds(holdDuration);
                continue;
            }

            GameObject row = Instantiate(messagePrefab, messageContainer);
            activeRows.Add(row);

            Text label = row.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text  = $"[SYSTEM] {text}";
                label.color = GetTypeColor(type);
            }

            CanvasGroup cg = row.GetComponent<CanvasGroup>();
            if (cg == null) cg = row.AddComponent<CanvasGroup>();

            // Fade in
            yield return FadeCanvasGroup(cg, 0f, 1f, fadeInDuration);

            // Hold
            yield return new WaitForSeconds(holdDuration);

            // Fade out
            yield return FadeCanvasGroup(cg, 1f, 0f, fadeOutDuration);

            activeRows.Remove(row);
            Destroy(row);

            // Small gap between messages
            yield return new WaitForSeconds(0.15f);
        }

        isShowing = false;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
    }

    private Color GetTypeColor(SystemNotifier.MessageType type)
    {
        return type switch
        {
            SystemNotifier.MessageType.LevelUp      => colorLevelUp,
            SystemNotifier.MessageType.ClassAssign  => colorClassAssign,
            SystemNotifier.MessageType.DungeonClear => colorDungeonClear,
            SystemNotifier.MessageType.WorldEvent   => colorWorldEvent,
            SystemNotifier.MessageType.Warning      => colorWarning,
            SystemNotifier.MessageType.Achievement  => colorAchievement,
            _                                       => colorInfo,
        };
    }
}

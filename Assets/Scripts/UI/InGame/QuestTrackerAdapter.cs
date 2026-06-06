using System;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Adapts <see cref="FoundationProgression"/> to <see cref="IQuestTrackerViewModel"/>.
    ///
    /// Scans <c>Progression.Quests</c> for the first incomplete quest and exposes
    /// its first incomplete objective plus the first reward entry as a preview.
    /// Subscribes to <c>Progression.Changed</c> so the HUD refreshes automatically.
    ///
    /// Lives in Assembly-CSharp. Foundation assembly never references us.
    /// </summary>
    public sealed class QuestTrackerAdapter : IQuestTrackerViewModel, IDisposable
    {
        readonly FoundationProgression _progression;

        public event Action Changed;

        public QuestTrackerAdapter(FoundationProgression progression)
        {
            _progression = progression;
            if (_progression != null) _progression.Changed += OnChanged;
        }

        public void Dispose()
        {
            if (_progression != null) _progression.Changed -= OnChanged;
        }

        void OnChanged() => Changed?.Invoke();

        public QuestTrackerData? PinnedQuest
        {
            get
            {
                if (_progression?.Quests == null) return null;

                // Pin the first incomplete quest alphabetically by insertion order.
                FoundationQuestState activeState = default;
                bool found = false;
                foreach (var kv in _progression.Quests)
                {
                    if (!kv.Value.Completed) { activeState = kv.Value; found = true; break; }
                }
                if (!found) return null;

                var def = activeState.Definition;

                // First incomplete objective.
                string objText    = "";
                int    objCurrent = 0, objRequired = 1;
                if (def?.objectives != null)
                {
                    foreach (var obj in def.objectives)
                    {
                        activeState.ObjectiveProgress.TryGetValue(obj.id, out int prog);
                        if (prog < obj.required)
                        {
                            objText      = obj.text;
                            objCurrent   = prog;
                            objRequired  = Math.Max(1, obj.required);
                            break;
                        }
                    }
                }

                // First reward preview — show type and amount.
                string rewardText = "";
                if (def?.rewards != null && def.rewards.Length > 0)
                {
                    var r = def.rewards[0];
                    rewardText = r.amount > 1
                        ? $"{r.type} ×{r.amount}"
                        : r.type.ToString();
                }

                return new QuestTrackerData
                {
                    questId           = def?.id ?? "",
                    title             = def?.Display ?? def?.id ?? "Unknown Quest",
                    questType         = def != null ? def.type.ToString() : "",
                    objectiveText     = objText,
                    objectiveCurrent  = objCurrent,
                    objectiveRequired = objRequired,
                    rewardText        = rewardText,
                };
            }
        }
    }
}

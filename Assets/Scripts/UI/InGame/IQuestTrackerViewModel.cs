using System;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Read model for the compact quest tracker HUD overlay.
    /// Implemented by <see cref="QuestTrackerAdapter"/> which reads from
    /// <c>FoundationProgression.Quests</c>.
    /// </summary>
    public interface IQuestTrackerViewModel
    {
        /// <summary>Fires when quest state changes (new quest started, objective progress, completion).</summary>
        event Action Changed;

        /// <summary>The quest currently pinned to the HUD, or null if none are active.</summary>
        QuestTrackerData? PinnedQuest { get; }
    }

    public struct QuestTrackerData
    {
        public string questId;
        public string title;         // display name of the quest
        public string questType;     // e.g. "Hearth", "Field", "Craft"

        // First incomplete objective
        public string objectiveText;
        public int    objectiveCurrent;
        public int    objectiveRequired;

        // First reward in rewards[] — shown as a preview line
        public string rewardText;
    }
}

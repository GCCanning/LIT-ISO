using System;
using System.Collections.Generic;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Runtime state for the LitRPG layer: selected Calling, skill XP, quest progress,
    /// and Foundation-owned stats. Kept plain C# so save/load can serialize it later.
    /// </summary>
    public sealed class FoundationProgression
    {
        readonly FoundationContent _content;
        readonly Dictionary<string, int> _skillXp = new();
        readonly Dictionary<string, FoundationQuestState> _quests = new();

        public event Action Changed;
        public event Action<FoundationQuestDefinition> QuestStarted;
        public event Action<FoundationQuestDefinition> QuestCompleted;

        public FoundationPlayerStats Stats { get; } = new();
        public FoundationCallingDefinition CurrentCalling { get; private set; }

        public IReadOnlyDictionary<string, int> SkillXp => _skillXp;
        public IReadOnlyDictionary<string, FoundationQuestState> Quests => _quests;

        public FoundationProgression(FoundationContent content)
        {
            _content = content;
            SelectCalling("greenhand");
            StartQuest("first_flame_first_field");
        }

        public bool SelectCalling(string callingId)
        {
            var calling = _content?.Callings.Get(callingId);
            if (calling == null) return false;

            CurrentCalling = calling;
            Stats.ApplyCalling(calling);

            if (calling.starterSkillIds != null)
                foreach (var skillId in calling.starterSkillIds)
                    if (!string.IsNullOrWhiteSpace(skillId) && !_skillXp.ContainsKey(skillId))
                        _skillXp[skillId] = 0;

            Changed?.Invoke();
            return true;
        }

        public void AddActivityXp(FoundationProgressionActivity activity, int amount)
        {
            if (amount <= 0 || _content == null) return;

            foreach (var skill in _content.Skills.All)
            {
                if (skill.activity != activity) continue;
                AddSkillXp(skill.id, amount);
            }

            Stats.AddExperience(Math.Max(1, amount / 2));
            Changed?.Invoke();
        }

        public void AddSkillXp(string skillId, int amount)
        {
            if (string.IsNullOrWhiteSpace(skillId) || amount <= 0) return;
            if (!_skillXp.ContainsKey(skillId)) _skillXp[skillId] = 0;
            _skillXp[skillId] += amount;
        }

        public bool StartQuest(string questId)
        {
            var quest = _content?.Quests.Get(questId);
            if (quest == null || _quests.ContainsKey(questId)) return false;

            _quests[questId] = new FoundationQuestState(quest);
            QuestStarted?.Invoke(quest);
            Changed?.Invoke();
            return true;
        }

        public bool IsQuestActive(string questId) =>
            !string.IsNullOrWhiteSpace(questId) && _quests.ContainsKey(questId);

        public bool IsQuestCompleted(string questId) =>
            !string.IsNullOrWhiteSpace(questId)
            && _quests.TryGetValue(questId, out var state)
            && state.Completed;

        public int GetObjectiveProgress(string questId, string objectiveId)
        {
            if (string.IsNullOrWhiteSpace(objectiveId)) return 0;
            if (!_quests.TryGetValue(questId, out var state)) return 0;
            return state.ObjectiveProgress.TryGetValue(objectiveId, out int value) ? value : 0;
        }

        public bool AdvanceQuestObjective(string questId, string objectiveId, int amount = 1)
        {
            if (!_quests.TryGetValue(questId, out var state)) return false;
            bool changed = state.Advance(objectiveId, amount);
            if (changed)
            {
                if (state.Completed)
                {
                    GrantRewards(state.Definition);
                    QuestCompleted?.Invoke(state.Definition);
                }
                Changed?.Invoke();
            }
            return changed;
        }

        void GrantRewards(FoundationQuestDefinition quest)
        {
            if (quest?.rewards == null) return;
            foreach (var reward in quest.rewards)
            {
                if (reward.type == FoundationRewardType.Xp)
                    Stats.AddExperience(Math.Max(1, reward.amount));
                else if (reward.type == FoundationRewardType.CallingToken)
                    StartQuest(reward.id);
            }
        }
    }

    public sealed class FoundationQuestState
    {
        readonly Dictionary<string, int> _objectiveProgress = new();

        public FoundationQuestDefinition Definition { get; }
        public bool Completed { get; private set; }
        public IReadOnlyDictionary<string, int> ObjectiveProgress => _objectiveProgress;

        public FoundationQuestState(FoundationQuestDefinition definition)
        {
            Definition = definition;
            if (definition?.objectives == null) return;
            foreach (var objective in definition.objectives)
                if (!string.IsNullOrWhiteSpace(objective.id))
                    _objectiveProgress[objective.id] = 0;
        }

        public bool Advance(string objectiveId, int amount)
        {
            if (Completed || string.IsNullOrWhiteSpace(objectiveId) || amount <= 0) return false;
            if (!_objectiveProgress.ContainsKey(objectiveId)) return false;

            int required = RequiredFor(objectiveId);
            int next = Math.Min(required, _objectiveProgress[objectiveId] + amount);
            if (next == _objectiveProgress[objectiveId]) return false;

            _objectiveProgress[objectiveId] = next;
            Completed = AllObjectivesComplete();
            return true;
        }

        int RequiredFor(string objectiveId)
        {
            if (Definition?.objectives == null) return 1;
            foreach (var objective in Definition.objectives)
                if (objective.id == objectiveId)
                    return Math.Max(1, objective.required);
            return 1;
        }

        bool AllObjectivesComplete()
        {
            if (Definition?.objectives == null || Definition.objectives.Length == 0) return true;
            foreach (var objective in Definition.objectives)
            {
                int required = Math.Max(1, objective.required);
                _objectiveProgress.TryGetValue(objective.id, out int current);
                if (current < required) return false;
            }
            return true;
        }
    }
}

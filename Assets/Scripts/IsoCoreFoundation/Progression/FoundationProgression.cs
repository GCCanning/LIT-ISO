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
        readonly List<FoundationRewardUnlock> _unlockedRewards = new();
        readonly List<string> _recentUnlocks = new();
        readonly List<string> _activeBuffs = new();
        readonly List<string> _regionShifts = new();
        int _callingXp;
        string _selectedBranchId;
        const int XpPerProgressionLevel = 100;

        public event Action Changed;
        public event Action<FoundationQuestDefinition> QuestStarted;
        public event Action<FoundationQuestDefinition> QuestCompleted;
        public event Action<FoundationRewardUnlock> RewardUnlocked;

        public FoundationPlayerStats Stats { get; } = new();
        public FoundationCallingDefinition CurrentCalling { get; private set; }

        public IReadOnlyDictionary<string, int> SkillXp => _skillXp;
        public IReadOnlyDictionary<string, FoundationQuestState> Quests => _quests;
        public IReadOnlyList<FoundationRewardUnlock> UnlockedRewards => _unlockedRewards;
        public IReadOnlyList<string> RecentUnlocks => _recentUnlocks;
        public IReadOnlyList<string> ActiveBuffs => _activeBuffs;
        public IReadOnlyList<string> RegionShifts => _regionShifts;
        public string CurrentCallingId => CurrentCalling != null ? CurrentCalling.id : "";
        public int CallingXp => _callingXp;
        public int CallingLevel => LevelForXp(_callingXp);
        public float CallingProgress01 => ProgressForXp(_callingXp);
        public string SelectedBranchId => _selectedBranchId;
        public FoundationCallingTier CurrentCallingTier => TierForLevel(CallingLevel);

        public FoundationProgressionReadState CaptureReadState()
        {
            return new FoundationProgressionReadState
            {
                calling = CaptureCallingReadState(),
                skills = CaptureSkillReadStates(),
                quests = CaptureQuestReadStates(),
                unlockedRewards = CaptureUnlockedRewards(),
                recentUnlocks = CaptureRecentUnlocks(),
                activeBuffs = CaptureActiveBuffs(),
                regionShifts = CaptureRegionShifts(),
            };
        }

        public FoundationCallingReadState CaptureCallingReadState()
        {
            var calling = CurrentCalling;
            return new FoundationCallingReadState
            {
                hasCalling = calling != null,
                id = CurrentCallingId,
                displayName = calling != null ? calling.Display : "",
                className = Stats != null ? Stats.Class : "",
                title = Stats != null ? Stats.Title : "",
                description = calling != null ? calling.description : "",
                startingTier = calling != null ? calling.startingTier : FoundationCallingTier.Novice,
                tier = CurrentCallingTier,
                xp = _callingXp,
                level = CallingLevel,
                xpIntoLevel = XpIntoLevel(_callingXp),
                xpToNextLevel = XpPerProgressionLevel,
                progress01 = CallingProgress01,
                selectedBranchId = _selectedBranchId ?? "",
                branchIds = CopyStrings(calling != null ? calling.branchIds : null),
                starterSkillIds = CopyStrings(calling != null ? calling.starterSkillIds : null),
                statBonuses = CopyStatBonuses(calling != null ? calling.statBonuses : null),
                capstone = calling != null ? calling.capstone : "",
            };
        }

        public FoundationSkillReadState[] CaptureSkillReadStates()
        {
            var skills = new List<FoundationSkillReadState>();

            if (_content?.Skills != null)
            {
                foreach (var skill in _content.Skills.All)
                {
                    if (skill == null || string.IsNullOrWhiteSpace(skill.id)) continue;
                    skills.Add(CreateSkillReadState(skill, skill.id, GetSkillXp(skill.id), _skillXp.ContainsKey(skill.id)));
                }
            }

            foreach (var kv in _skillXp)
            {
                if (_content?.Skills != null && _content.Skills.Has(kv.Key)) continue;
                skills.Add(CreateSkillReadState(null, kv.Key, kv.Value, true));
            }

            return skills.ToArray();
        }

        public FoundationRewardUnlock[] CaptureUnlockedRewards() => _unlockedRewards.ToArray();

        public FoundationQuestReadState[] CaptureQuestReadStates()
        {
            var quests = new List<FoundationQuestReadState>();

            if (_content?.Quests != null)
            {
                foreach (var quest in _content.Quests.All)
                {
                    if (quest == null || string.IsNullOrWhiteSpace(quest.id)) continue;
                    if (_quests.TryGetValue(quest.id, out var state))
                        quests.Add(CreateQuestReadState(state));
                }
            }

            foreach (var kv in _quests)
            {
                if (_content?.Quests != null && _content.Quests.Has(kv.Key)) continue;
                quests.Add(CreateQuestReadState(kv.Value));
            }

            return quests.ToArray();
        }

        public FoundationQuestReadState CaptureQuestReadState(string questId)
        {
            return _quests.TryGetValue(questId ?? string.Empty, out var state)
                ? CreateQuestReadState(state)
                : default;
        }

        public string[] CaptureRecentUnlocks() => _recentUnlocks.ToArray();

        public string[] CaptureActiveBuffs() => _activeBuffs.ToArray();

        public string[] CaptureRegionShifts() => _regionShifts.ToArray();

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
            _selectedBranchId = null;
            Stats.ApplyCalling(calling);

            if (calling.starterSkillIds != null)
                foreach (var skillId in calling.starterSkillIds)
                    if (!string.IsNullOrWhiteSpace(skillId) && !_skillXp.ContainsKey(skillId))
                        _skillXp[skillId] = 0;

            Changed?.Invoke();
            return true;
        }

        public bool SelectCallingBranch(string branchId)
        {
            if (CurrentCalling == null || string.IsNullOrWhiteSpace(branchId)) return false;
            if (CurrentCalling.branchIds == null) return false;
            foreach (var id in CurrentCalling.branchIds)
            {
                if (!string.Equals(id, branchId, StringComparison.Ordinal)) continue;
                _selectedBranchId = branchId;
                RememberUnlock($"Calling branch: {branchId}");
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        public void AddActivityXp(FoundationProgressionActivity activity, int amount)
        {
            if (amount <= 0 || _content == null) return;

            _callingXp += amount;

            foreach (var skill in _content.Skills.All)
            {
                if (skill.activity != activity) continue;
                AddSkillXpInternal(skill.id, amount);
            }

            Stats.AddExperience(Math.Max(1, amount / 2));
            Changed?.Invoke();
        }

        public void AddSkillXp(string skillId, int amount)
        {
            if (AddSkillXpInternal(skillId, amount))
                Changed?.Invoke();
        }

        public int GetSkillXp(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId)) return 0;
            return _skillXp.TryGetValue(skillId, out int value) ? value : 0;
        }

        public int GetSkillLevel(string skillId) => LevelForXp(GetSkillXp(skillId));

        public float GetSkillProgress01(string skillId) => ProgressForXp(GetSkillXp(skillId));

        public bool HasUnlockedReward(FoundationRewardType type, string id)
        {
            foreach (var reward in _unlockedRewards)
                if (reward.type == type && string.Equals(reward.id, id, StringComparison.Ordinal))
                    return true;
            return false;
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
                {
                    Stats.AddExperience(Math.Max(1, reward.amount));
                    continue;
                }

                UnlockReward(reward);
            }
        }

        void UnlockReward(FoundationQuestReward reward)
        {
            if (string.IsNullOrWhiteSpace(reward.id)) return;
            if (!HasUnlockedReward(reward.type, reward.id))
            {
                var unlock = new FoundationRewardUnlock(reward.type, reward.id, reward.amount);
                _unlockedRewards.Add(unlock);
                RewardUnlocked?.Invoke(unlock);
            }

            RememberUnlock($"{reward.type}: {reward.id}");

            if (reward.type == FoundationRewardType.RegionShift && !_regionShifts.Contains(reward.id))
                _regionShifts.Add(reward.id);

            // Backward-compatible hook: a CallingToken reward may point at a follow-up
            // quest id in early data. Keep that bridge until a dedicated token UI lands.
            if (reward.type == FoundationRewardType.CallingToken)
                StartQuest(reward.id);
        }

        void RememberUnlock(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _recentUnlocks.Remove(text);
            _recentUnlocks.Insert(0, text);
            const int maxRecent = 12;
            while (_recentUnlocks.Count > maxRecent)
                _recentUnlocks.RemoveAt(_recentUnlocks.Count - 1);
        }

        public FoundationProgressionSaveData CaptureState()
        {
            var skills = new List<FoundationKeyValueInt>();
            foreach (var kv in _skillXp)
                skills.Add(new FoundationKeyValueInt(kv.Key, kv.Value));

            var quests = new List<FoundationQuestSaveData>();
            foreach (var kv in _quests)
                quests.Add(kv.Value.CaptureState());

            return new FoundationProgressionSaveData
            {
                currentCallingId = CurrentCallingId,
                callingXp = _callingXp,
                selectedBranchId = _selectedBranchId,
                stats = Stats.CaptureState(),
                skillXp = skills.ToArray(),
                quests = quests.ToArray(),
                unlockedRewards = _unlockedRewards.ToArray(),
                recentUnlocks = _recentUnlocks.ToArray(),
                activeBuffs = _activeBuffs.ToArray(),
                regionShifts = _regionShifts.ToArray(),
            };
        }

        public void RestoreState(FoundationProgressionSaveData state)
        {
            if (state == null) return;

            _skillXp.Clear();
            _quests.Clear();
            _unlockedRewards.Clear();
            _recentUnlocks.Clear();
            _activeBuffs.Clear();
            _regionShifts.Clear();

            if (!SelectCalling(string.IsNullOrWhiteSpace(state.currentCallingId) ? "greenhand" : state.currentCallingId))
                SelectCalling("greenhand");

            _callingXp = Math.Max(0, state.callingXp);
            if (!string.IsNullOrWhiteSpace(state.selectedBranchId))
                RestoreSelectedBranch(state.selectedBranchId);

            if (state.skillXp != null)
                foreach (var skill in state.skillXp)
                    if (!string.IsNullOrWhiteSpace(skill.id))
                        _skillXp[skill.id] = Math.Max(0, skill.value);

            if (state.quests != null)
            {
                foreach (var savedQuest in state.quests)
                {
                    var def = _content?.Quests.Get(savedQuest.questId);
                    if (def == null) continue;
                    var questState = new FoundationQuestState(def);
                    questState.RestoreState(savedQuest);
                    _quests[def.id] = questState;
                }
            }

            if (state.unlockedRewards != null)
                _unlockedRewards.AddRange(state.unlockedRewards);
            if (state.recentUnlocks != null)
                _recentUnlocks.AddRange(state.recentUnlocks);
            if (state.activeBuffs != null)
                _activeBuffs.AddRange(state.activeBuffs);
            if (state.regionShifts != null)
                _regionShifts.AddRange(state.regionShifts);

            Stats.RestoreState(state.stats);
            Changed?.Invoke();
        }

        bool AddSkillXpInternal(string skillId, int amount)
        {
            if (string.IsNullOrWhiteSpace(skillId) || amount <= 0) return false;
            if (!_skillXp.ContainsKey(skillId)) _skillXp[skillId] = 0;
            _skillXp[skillId] += amount;
            return true;
        }

        void RestoreSelectedBranch(string branchId)
        {
            _selectedBranchId = null;
            if (CurrentCalling == null || string.IsNullOrWhiteSpace(branchId)) return;
            if (CurrentCalling.branchIds == null) return;

            foreach (var id in CurrentCalling.branchIds)
            {
                if (!string.Equals(id, branchId, StringComparison.Ordinal)) continue;
                _selectedBranchId = branchId;
                return;
            }
        }

        FoundationSkillReadState CreateSkillReadState(FoundationSkillDefinition skill, string skillId, int xp, bool isTracked)
        {
            int safeXp = Math.Max(0, xp);
            return new FoundationSkillReadState
            {
                id = skill != null ? skill.id : skillId ?? "",
                displayName = skill != null ? skill.Display : skillId ?? "",
                description = skill != null ? skill.description : "",
                activity = skill != null ? skill.activity : default,
                primaryNodeKind = skill != null ? skill.primaryNodeKind : default,
                xp = safeXp,
                level = LevelForXp(safeXp),
                xpIntoLevel = XpIntoLevel(safeXp),
                xpToNextLevel = XpPerProgressionLevel,
                progress01 = ProgressForXp(safeXp),
                isTracked = isTracked,
                unlocks = CopyStrings(skill != null ? skill.unlocks : null),
            };
        }

        FoundationQuestReadState CreateQuestReadState(FoundationQuestState state)
        {
            var quest = state?.Definition;
            var sourceObjectives = quest?.objectives ?? Array.Empty<FoundationQuestObjective>();
            var objectives = new FoundationQuestObjectiveReadState[sourceObjectives.Length];
            int totalCurrent = 0;
            int totalRequired = 0;
            string firstIncompleteId = "";
            string firstIncompleteText = "";

            for (int i = 0; i < sourceObjectives.Length; i++)
            {
                var objective = sourceObjectives[i];
                int required = Math.Max(1, objective.required);
                int current = 0;
                state?.ObjectiveProgress.TryGetValue(objective.id, out current);
                current = Math.Max(0, Math.Min(required, current));

                bool completed = current >= required;
                if (!completed && string.IsNullOrEmpty(firstIncompleteId))
                {
                    firstIncompleteId = objective.id ?? "";
                    firstIncompleteText = objective.text ?? "";
                }

                objectives[i] = new FoundationQuestObjectiveReadState
                {
                    id = objective.id ?? "",
                    text = objective.text ?? "",
                    current = current,
                    required = required,
                    progress01 = required > 0 ? current / (float)required : 1f,
                    completed = completed,
                };

                totalCurrent += current;
                totalRequired += required;
            }

            return new FoundationQuestReadState
            {
                active = state != null,
                completed = state != null && state.Completed,
                id = quest != null ? quest.id : "",
                displayName = quest != null ? quest.Display : "",
                type = quest != null ? quest.type : default,
                act = quest != null ? quest.act : "",
                description = quest != null ? quest.description : "",
                objectives = objectives,
                rewards = CopyRewards(quest != null ? quest.rewards : null),
                firstIncompleteObjectiveId = firstIncompleteId,
                firstIncompleteObjectiveText = firstIncompleteText,
                progress01 = totalRequired > 0 ? Math.Min(1f, totalCurrent / (float)totalRequired) : 1f,
            };
        }

        static string[] CopyStrings(string[] values)
        {
            if (values == null || values.Length == 0) return Array.Empty<string>();
            var copy = new string[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        static FoundationStatBonus[] CopyStatBonuses(FoundationStatBonus[] values)
        {
            if (values == null || values.Length == 0) return Array.Empty<FoundationStatBonus>();
            var copy = new FoundationStatBonus[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        static FoundationQuestReward[] CopyRewards(FoundationQuestReward[] values)
        {
            if (values == null || values.Length == 0) return Array.Empty<FoundationQuestReward>();
            var copy = new FoundationQuestReward[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        static int LevelForXp(int xp) => Math.Max(1, Math.Max(0, xp) / XpPerProgressionLevel + 1);

        static int XpIntoLevel(int xp) => Math.Max(0, xp) % XpPerProgressionLevel;

        static float ProgressForXp(int xp)
        {
            int positive = Math.Max(0, xp);
            return XpIntoLevel(positive) / (float)XpPerProgressionLevel;
        }

        static FoundationCallingTier TierForLevel(int level)
        {
            if (level >= 51) return FoundationCallingTier.Mythwarm;
            if (level >= 31) return FoundationCallingTier.Luminary;
            if (level >= 16) return FoundationCallingTier.Artisan;
            if (level >= 6) return FoundationCallingTier.Adept;
            return FoundationCallingTier.Novice;
        }
    }

    [Serializable]
    public class FoundationProgressionReadState
    {
        public FoundationCallingReadState calling;
        public FoundationSkillReadState[] skills;
        public FoundationQuestReadState[] quests;
        public FoundationRewardUnlock[] unlockedRewards;
        public string[] recentUnlocks;
        public string[] activeBuffs;
        public string[] regionShifts;
    }

    [Serializable]
    public struct FoundationCallingReadState
    {
        public bool hasCalling;
        public string id;
        public string displayName;
        public string className;
        public string title;
        public string description;
        public FoundationCallingTier startingTier;
        public FoundationCallingTier tier;
        public int xp;
        public int level;
        public int xpIntoLevel;
        public int xpToNextLevel;
        public float progress01;
        public string selectedBranchId;
        public string[] branchIds;
        public string[] starterSkillIds;
        public FoundationStatBonus[] statBonuses;
        public string capstone;
    }

    [Serializable]
    public struct FoundationSkillReadState
    {
        public string id;
        public string displayName;
        public string description;
        public FoundationProgressionActivity activity;
        public FoundationSkillNodeKind primaryNodeKind;
        public int xp;
        public int level;
        public int xpIntoLevel;
        public int xpToNextLevel;
        public float progress01;
        public bool isTracked;
        public string[] unlocks;
    }

    [Serializable]
    public struct FoundationQuestReadState
    {
        public bool active;
        public bool completed;
        public string id;
        public string displayName;
        public FoundationQuestType type;
        public string act;
        public string description;
        public FoundationQuestObjectiveReadState[] objectives;
        public FoundationQuestReward[] rewards;
        public string firstIncompleteObjectiveId;
        public string firstIncompleteObjectiveText;
        public float progress01;
    }

    [Serializable]
    public struct FoundationQuestObjectiveReadState
    {
        public string id;
        public string text;
        public int current;
        public int required;
        public float progress01;
        public bool completed;
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

        public FoundationQuestSaveData CaptureState()
        {
            var objectives = new List<FoundationKeyValueInt>();
            foreach (var kv in _objectiveProgress)
                objectives.Add(new FoundationKeyValueInt(kv.Key, kv.Value));

            return new FoundationQuestSaveData
            {
                questId = Definition != null ? Definition.id : "",
                completed = Completed,
                objectives = objectives.ToArray(),
            };
        }

        public void RestoreState(FoundationQuestSaveData state)
        {
            if (state?.objectives != null)
            {
                foreach (var objective in state.objectives)
                {
                    if (string.IsNullOrWhiteSpace(objective.id) || !_objectiveProgress.ContainsKey(objective.id)) continue;
                    _objectiveProgress[objective.id] = Math.Min(RequiredFor(objective.id), Math.Max(0, objective.value));
                }
            }
            Completed = state != null && state.completed && AllObjectivesComplete();
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

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
        readonly Dictionary<TrialEvidenceCategory, int> _trialScores = new();
        readonly Dictionary<string, int> _xpChannels = new();
        readonly Dictionary<string, int> _titleProgress = new();
        readonly Dictionary<string, int> _affinityScores = new();
        readonly List<string> _acquiredTitles = new();
        readonly List<FoundationTrialEvidenceEntry> _evidenceLog = new();
        FoundationTrialLifecycleSaveData _trialLifecycle = CreateDefaultTrialLifecycle();
        int _nextEvidenceSequence = 1;
        int _callingXp;
        string _selectedBranchId;
        const int SevenDayTrialDuration = 7;
        const int XpPerProgressionLevel = 100;

        public event Action Changed;
        public event Action<FoundationQuestDefinition> QuestStarted;
        public event Action<FoundationQuestDefinition> QuestCompleted;
        public event Action<FoundationRewardUnlock> RewardUnlocked;
        public event Action<EvidenceEventDefinition, int> TrialEvidenceAdded;
        public event Action<FoundationGrade> GradeForecastChanged;
        public event Action<string, int> XpChannelChanged;
        public event Action<TitleDefinition> TitleAcquired;
        public event Action<string, int> TitleProgressChanged;
        public event Action<AffinityDefinition> AffinityAwakened;
        public event Action<string, int> AffinityChanged;

        public FoundationPlayerStats Stats { get; } = new();
        public SystemMessageFeed SystemFeed { get; } = new();
        public FoundationCallingDefinition CurrentCalling { get; private set; }

        public IReadOnlyDictionary<string, int> SkillXp => _skillXp;
        public IReadOnlyDictionary<string, FoundationQuestState> Quests => _quests;
        public IReadOnlyList<FoundationRewardUnlock> UnlockedRewards => _unlockedRewards;
        public IReadOnlyList<string> RecentUnlocks => _recentUnlocks;
        public IReadOnlyList<string> ActiveBuffs => _activeBuffs;
        public IReadOnlyList<string> RegionShifts => _regionShifts;
        public IReadOnlyDictionary<TrialEvidenceCategory, int> TrialScores => _trialScores;
        public IReadOnlyDictionary<string, int> XPChannels => _xpChannels;
        public IReadOnlyDictionary<string, int> TitleProgress => _titleProgress;
        public IReadOnlyDictionary<string, int> AffinityScores => _affinityScores;
        public IReadOnlyList<string> AcquiredTitles => _acquiredTitles;
        public IReadOnlyList<FoundationTrialEvidenceEntry> EvidenceLog => _evidenceLog;
        public int TrialDay => _trialLifecycle != null ? Math.Max(1, _trialLifecycle.trialDay) : 1;
        public int TrialDurationDays => _trialLifecycle != null ? Math.Max(1, _trialLifecycle.trialDurationDays) : SevenDayTrialDuration;
        public bool TrialCompleted => _trialLifecycle != null && _trialLifecycle.completed;
        public string SelectedClassId => _trialLifecycle != null ? _trialLifecycle.selectedClassId ?? "" : "";
        public string SelectedProfessionId => _trialLifecycle != null ? _trialLifecycle.selectedProfessionId ?? "" : "";
        public string CurrentCallingId => CurrentCalling != null ? CurrentCalling.id : "";
        public int CallingXp => _callingXp;
        public int CallingLevel => LevelForXp(_callingXp);
        public float CallingProgress01 => ProgressForXp(_callingXp);
        public string SelectedBranchId => _selectedBranchId;
        public FoundationCallingTier CurrentCallingTier => TierForLevel(CallingLevel);
        public FoundationGrade GradeForecast => GradeForTotal(TotalTrialScore);
        public int TotalTrialScore => SumTrialScores();

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
                trial = CaptureTrialReadState(),
                xpChannels = CaptureXpChannelReadStates(),
                titleProgress = CaptureTitleReadStates(),
                affinities = CaptureAffinityReadStates(),
                systemMessages = SystemFeed.CaptureState(),
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

        public void AddActivityXp(FoundationProgressionActivity activity, int amount, params string[] skillIds)
        {
            if (amount <= 0 || _content == null) return;

            _callingXp += amount;

            bool specificSkillAwarded = false;
            if (skillIds != null)
            {
                foreach (var skillId in skillIds)
                {
                    if (string.IsNullOrWhiteSpace(skillId)) continue;
                    var skill = _content.Skills.Get(skillId);
                    if (skill == null || skill.activity != activity) continue;
                    specificSkillAwarded |= AddSkillXpInternal(skill.id, amount);
                }
            }

            if (!specificSkillAwarded)
            {
                foreach (var skill in _content.Skills.All)
                {
                    if (skill.activity != activity) continue;
                    AddSkillXpInternal(skill.id, amount);
                }
            }

            Stats.AddExperience(Math.Max(1, amount / 2));
            Changed?.Invoke();
        }

        public void AddSkillXp(string skillId, int amount)
        {
            if (AddSkillXpInternal(skillId, amount))
                Changed?.Invoke();
        }

        public bool RecordEvidence(string evidenceId, int amount = 1, string sourceId = "")
        {
            if (string.IsNullOrWhiteSpace(evidenceId) || amount <= 0) return false;
            var evidence = _content?.EvidenceEvents.Get(evidenceId);
            if (evidence == null) return false;

            var beforeGrade = GradeForecast;
            var scoreBefore = CopyTrialScores();
            var xpBefore = CopyIntDictionary(_xpChannels);
            var titleBefore = CopyIntDictionary(_titleProgress);
            var affinityBefore = CopyIntDictionary(_affinityScores);
            ApplyEvidenceWeights(evidence.evidenceWeights, amount);
            ApplyXpGrants(evidence.xpGrants, amount);
            ApplyTitleProgress(evidence.titleProgress, amount);
            ApplyAffinityProgress(evidence.affinityProgress, amount);

            if (!string.IsNullOrWhiteSpace(evidence.message))
                SystemFeed.Queue(evidence.messageChannel, evidence.message, string.IsNullOrWhiteSpace(sourceId) ? evidence.id : sourceId, 1);

            _evidenceLog.Add(new FoundationTrialEvidenceEntry
            {
                sequence = _nextEvidenceSequence++,
                eventId = evidence.id,
                amount = amount,
                sourceId = string.IsNullOrWhiteSpace(sourceId) ? evidence.id : sourceId,
                scoreDeltas = CaptureTrialScoreDeltas(scoreBefore),
                xpDeltas = CaptureDeltas(xpBefore, _xpChannels),
                titleDeltas = CaptureDeltas(titleBefore, _titleProgress),
                affinityDeltas = CaptureDeltas(affinityBefore, _affinityScores),
                totalScoreAfter = TotalTrialScore,
                gradeAfter = GradeForecast,
            });

            TrialEvidenceAdded?.Invoke(evidence, amount);
            if (GradeForecast != beforeGrade)
                GradeForecastChanged?.Invoke(GradeForecast);
            Changed?.Invoke();
            return true;
        }

        public bool SetTrialDay(int day)
        {
            EnsureTrialLifecycle();
            int next = Math.Max(1, Math.Min(TrialDurationDays, day));
            if (_trialLifecycle.trialDay == next) return false;

            _trialLifecycle.trialDay = next;
            if (_trialLifecycle.trialDay >= TrialDurationDays)
                CompleteTrial();
            Changed?.Invoke();
            return true;
        }

        public bool AdvanceTrialDay(int days = 1)
        {
            if (days <= 0 || TrialCompleted) return false;
            return SetTrialDay(TrialDay + days);
        }

        public bool CompleteTrial(string selectedClassId = "", string selectedProfessionId = "")
        {
            EnsureTrialLifecycle();
            if (_trialLifecycle.completed &&
                (string.IsNullOrWhiteSpace(selectedClassId) || string.Equals(_trialLifecycle.selectedClassId, selectedClassId, StringComparison.Ordinal)) &&
                (string.IsNullOrWhiteSpace(selectedProfessionId) || string.Equals(_trialLifecycle.selectedProfessionId, selectedProfessionId, StringComparison.Ordinal)))
                return false;

            _trialLifecycle.trialDay = Math.Max(_trialLifecycle.trialDay, TrialDurationDays);
            _trialLifecycle.trialDurationDays = TrialDurationDays;
            _trialLifecycle.completed = true;
            _trialLifecycle.gradeSnapshot = GradeForecast;
            _trialLifecycle.classOffers = BuildClassOffers(selectedClassId);
            _trialLifecycle.professionOffers = BuildProfessionOffers(selectedProfessionId);
            _trialLifecycle.selectedClassId = SelectOfferId(_trialLifecycle.classOffers, selectedClassId);
            _trialLifecycle.selectedProfessionId = SelectOfferId(_trialLifecycle.professionOffers, selectedProfessionId);
            MarkSelectedOffers(_trialLifecycle.classOffers, _trialLifecycle.selectedClassId);
            MarkSelectedOffers(_trialLifecycle.professionOffers, _trialLifecycle.selectedProfessionId);

            Changed?.Invoke();
            return true;
        }

        public int GetTrialScore(TrialEvidenceCategory category) =>
            _trialScores.TryGetValue(category, out int value) ? value : 0;

        public int GetXpChannelValue(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return 0;
            return _xpChannels.TryGetValue(id, out int value) ? value : 0;
        }

        public int GetTitleProgress(string titleId)
        {
            if (string.IsNullOrWhiteSpace(titleId)) return 0;
            return _titleProgress.TryGetValue(titleId, out int value) ? value : 0;
        }

        public bool HasTitle(string titleId) =>
            !string.IsNullOrWhiteSpace(titleId) && _acquiredTitles.Contains(titleId);

        public int GetAffinityScore(string affinityId)
        {
            if (string.IsNullOrWhiteSpace(affinityId)) return 0;
            return _affinityScores.TryGetValue(affinityId, out int value) ? value : 0;
        }

        public FoundationAffinityRank GetAffinityRank(string affinityId)
        {
            return AffinityRankForScore(GetAffinityScore(affinityId));
        }

        public float GetAffinityEffectMultiplier(string affinityId)
        {
            if (string.IsNullOrWhiteSpace(affinityId))
                return 1f;
            return AffinityEffectMultiplier(GetAffinityRank(affinityId));
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

        public bool ApplyDungeonResult(DungeonResultDefinition result, int multiplier = 1)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.id))
                return false;

            multiplier = Math.Max(1, multiplier);
            ApplyXpGrants(result.xpRewards, multiplier);
            ApplyTitleProgress(result.titleProgress, multiplier);
            ApplyAffinityProgress(result.affinityProgress, multiplier);

            if (result.rewards != null)
            {
                foreach (var reward in result.rewards)
                {
                    if (reward.type == FoundationRewardType.Xp)
                    {
                        Stats.AddExperience(Math.Max(1, reward.amount * multiplier));
                        continue;
                    }

                    UnlockReward(new FoundationQuestReward(reward.type, reward.id, reward.amount * multiplier));
                }
            }

            RememberUnlock($"Dungeon result: {result.Display}");
            SystemFeed.Queue(SystemMessageChannel.DungeonAlert,
                string.IsNullOrWhiteSpace(result.summary) ? $"Dungeon cleared: {result.Display}." : result.summary,
                result.id, 3);
            Changed?.Invoke();
            return true;
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
                trialScores = CaptureTrialScores(),
                trialLifecycle = CaptureTrialLifecycle(),
                evidenceLog = _evidenceLog.ToArray(),
                xpChannels = CaptureKeyValueArray(_xpChannels),
                titleProgress = CaptureKeyValueArray(_titleProgress),
                affinityScores = CaptureKeyValueArray(_affinityScores),
                acquiredTitles = _acquiredTitles.ToArray(),
                systemMessages = SystemFeed.CaptureState(),
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
            _trialScores.Clear();
            _xpChannels.Clear();
            _titleProgress.Clear();
            _affinityScores.Clear();
            _acquiredTitles.Clear();
            _evidenceLog.Clear();
            _trialLifecycle = CreateDefaultTrialLifecycle();
            _nextEvidenceSequence = 1;
            SystemFeed.RestoreState(null);

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
            RestoreTrialScores(state.trialScores);
            RestoreTrialLifecycle(state.trialLifecycle);
            RestoreEvidenceLog(state.evidenceLog);
            RestoreKeyValueArray(state.xpChannels, _xpChannels);
            RestoreKeyValueArray(state.titleProgress, _titleProgress);
            RestoreKeyValueArray(state.affinityScores, _affinityScores);
            if (state.acquiredTitles != null)
                _acquiredTitles.AddRange(state.acquiredTitles);
            SystemFeed.RestoreState(state.systemMessages);

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

        void ApplyEvidenceWeights(FoundationEvidenceWeight[] weights, int multiplier)
        {
            if (weights == null) return;
            for (int i = 0; i < weights.Length; i++)
            {
                int amount = weights[i].amount * multiplier;
                if (amount <= 0) continue;
                if (!_trialScores.ContainsKey(weights[i].category)) _trialScores[weights[i].category] = 0;
                _trialScores[weights[i].category] += amount;
            }
        }

        void ApplyXpGrants(FoundationXpGrant[] grants, int multiplier)
        {
            if (grants == null) return;
            for (int i = 0; i < grants.Length; i++)
            {
                string id = string.IsNullOrWhiteSpace(grants[i].id)
                    ? grants[i].channel.ToString().ToLowerInvariant()
                    : grants[i].id.Trim();
                int amount = grants[i].amount * multiplier;
                if (string.IsNullOrWhiteSpace(id) || amount <= 0) continue;
                if (!_xpChannels.ContainsKey(id)) _xpChannels[id] = 0;
                _xpChannels[id] += amount;
                XpChannelChanged?.Invoke(id, _xpChannels[id]);
            }
        }

        void ApplyTitleProgress(FoundationTitleProgressGrant[] grants, int multiplier)
        {
            if (grants == null) return;
            for (int i = 0; i < grants.Length; i++)
            {
                string titleId = grants[i].titleId;
                int amount = grants[i].amount * multiplier;
                if (string.IsNullOrWhiteSpace(titleId) || amount <= 0) continue;
                var title = _content?.Titles.Get(titleId);
                if (title == null) continue;

                if (!_titleProgress.ContainsKey(titleId)) _titleProgress[titleId] = 0;
                _titleProgress[titleId] = Math.Min(Math.Max(1, title.threshold), _titleProgress[titleId] + amount);
                TitleProgressChanged?.Invoke(titleId, _titleProgress[titleId]);

                if (_titleProgress[titleId] < Math.Max(1, title.threshold) || _acquiredTitles.Contains(titleId)) continue;
                _acquiredTitles.Add(titleId);
                RememberUnlock($"Title: {title.Display}");
                SystemFeed.Queue(SystemMessageChannel.TitleAcquired,
                    string.IsNullOrWhiteSpace(title.unlockMessage) ? $"Title acquired: {title.Display}." : title.unlockMessage,
                    title.id, 2);
                TitleAcquired?.Invoke(title);
            }
        }

        void ApplyAffinityProgress(FoundationAffinityGrant[] grants, int multiplier)
        {
            if (grants == null) return;
            for (int i = 0; i < grants.Length; i++)
            {
                string affinityId = grants[i].affinityId;
                int amount = grants[i].amount * multiplier;
                if (string.IsNullOrWhiteSpace(affinityId) || amount <= 0) continue;
                var affinity = _content?.Affinities.Get(affinityId);
                if (affinity == null) continue;

                int previous = GetAffinityScore(affinityId);
                if (!_affinityScores.ContainsKey(affinityId)) _affinityScores[affinityId] = 0;
                _affinityScores[affinityId] += amount;
                AffinityChanged?.Invoke(affinityId, _affinityScores[affinityId]);

                int threshold = Math.Max(1, affinity.awakenThreshold);
                if (previous < threshold && _affinityScores[affinityId] >= threshold)
                {
                    RememberUnlock($"Affinity: {affinity.Display}");
                    SystemFeed.Queue(SystemMessageChannel.AffinityResonance,
                        $"Affinity awakened: {affinity.Display}.", affinity.id, 2);
                    AffinityAwakened?.Invoke(affinity);
                }
            }
        }

        FoundationTrialLifecycleSaveData CaptureTrialLifecycle()
        {
            EnsureTrialLifecycle();
            return new FoundationTrialLifecycleSaveData
            {
                trialDay = TrialDay,
                trialDurationDays = TrialDurationDays,
                completed = _trialLifecycle.completed,
                gradeSnapshot = _trialLifecycle.completed ? _trialLifecycle.gradeSnapshot : GradeForecast,
                classOffers = CopyOffers(_trialLifecycle.classOffers),
                professionOffers = CopyOffers(_trialLifecycle.professionOffers),
                selectedClassId = _trialLifecycle.selectedClassId ?? "",
                selectedProfessionId = _trialLifecycle.selectedProfessionId ?? "",
            };
        }

        void RestoreTrialLifecycle(FoundationTrialLifecycleSaveData state)
        {
            _trialLifecycle = state ?? CreateDefaultTrialLifecycle();
            _trialLifecycle.trialDurationDays = Math.Max(1, _trialLifecycle.trialDurationDays);
            _trialLifecycle.trialDay = Math.Max(1, Math.Min(_trialLifecycle.trialDurationDays, _trialLifecycle.trialDay));
            _trialLifecycle.classOffers = CopyOffers(_trialLifecycle.classOffers);
            _trialLifecycle.professionOffers = CopyOffers(_trialLifecycle.professionOffers);
            _trialLifecycle.selectedClassId = _trialLifecycle.selectedClassId ?? "";
            _trialLifecycle.selectedProfessionId = _trialLifecycle.selectedProfessionId ?? "";
            if (!_trialLifecycle.completed)
                _trialLifecycle.gradeSnapshot = GradeForecast;
        }

        void RestoreEvidenceLog(FoundationTrialEvidenceEntry[] entries)
        {
            if (entries == null) return;
            int maxSequence = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].sequence <= 0) continue;
                entries[i].scoreDeltas = entries[i].scoreDeltas ?? Array.Empty<FoundationKeyValueInt>();
                entries[i].xpDeltas = entries[i].xpDeltas ?? Array.Empty<FoundationKeyValueInt>();
                entries[i].titleDeltas = entries[i].titleDeltas ?? Array.Empty<FoundationKeyValueInt>();
                entries[i].affinityDeltas = entries[i].affinityDeltas ?? Array.Empty<FoundationKeyValueInt>();
                _evidenceLog.Add(entries[i]);
                maxSequence = Math.Max(maxSequence, entries[i].sequence);
            }
            _nextEvidenceSequence = Math.Max(1, maxSequence + 1);
        }

        static FoundationTrialLifecycleSaveData CreateDefaultTrialLifecycle()
        {
            return new FoundationTrialLifecycleSaveData
            {
                trialDay = 1,
                trialDurationDays = SevenDayTrialDuration,
                completed = false,
                gradeSnapshot = FoundationGrade.F,
                classOffers = Array.Empty<FoundationTrialOffer>(),
                professionOffers = Array.Empty<FoundationTrialOffer>(),
                selectedClassId = "",
                selectedProfessionId = "",
            };
        }

        void EnsureTrialLifecycle()
        {
            if (_trialLifecycle == null)
                _trialLifecycle = CreateDefaultTrialLifecycle();
            if (_trialLifecycle.trialDurationDays <= 0)
                _trialLifecycle.trialDurationDays = SevenDayTrialDuration;
            if (_trialLifecycle.trialDay <= 0)
                _trialLifecycle.trialDay = 1;
        }

        FoundationTrialOffer[] BuildClassOffers(string selectedClassId)
        {
            var offers = new List<FoundationTrialOffer>();
            if (_content?.Classes != null)
            {
                foreach (var cls in _content.Classes.All)
                {
                    if (cls == null || string.IsNullOrWhiteSpace(cls.id)) continue;
                    int score = ScoreEvidenceWeights(cls.weights);
                    if (cls.preferredAffinityIds != null)
                        for (int i = 0; i < cls.preferredAffinityIds.Length; i++)
                            score += GetAffinityScore(cls.preferredAffinityIds[i]);
                    offers.Add(new FoundationTrialOffer(cls.id, cls.Display, score,
                        string.Equals(cls.id, selectedClassId, StringComparison.Ordinal)));
                }
            }
            return TopOffers(offers, 3);
        }

        FoundationTrialOffer[] BuildProfessionOffers(string selectedProfessionId)
        {
            var offers = new List<FoundationTrialOffer>();
            if (_content?.Professions != null)
            {
                foreach (var profession in _content.Professions.All)
                {
                    if (profession == null || string.IsNullOrWhiteSpace(profession.id)) continue;
                    int score = 0;
                    if (profession.progressionSkillIds != null)
                        for (int i = 0; i < profession.progressionSkillIds.Length; i++)
                            score += GetSkillXp(profession.progressionSkillIds[i]);
                    score += ActivityScore(profession.primaryActivity);
                    offers.Add(new FoundationTrialOffer(profession.id, profession.Display, score,
                        string.Equals(profession.id, selectedProfessionId, StringComparison.Ordinal)));
                }
            }
            return TopOffers(offers, 3);
        }

        int ScoreEvidenceWeights(FoundationEvidenceWeight[] weights)
        {
            if (weights == null) return 0;
            int score = 0;
            for (int i = 0; i < weights.Length; i++)
                score += GetTrialScore(weights[i].category) * Math.Max(1, weights[i].amount);
            return score;
        }

        int ActivityScore(FoundationProgressionActivity activity)
        {
            int score = 0;
            if (_content?.Skills == null) return score;
            foreach (var skill in _content.Skills.All)
                if (skill != null && skill.activity == activity)
                    score += GetSkillXp(skill.id);
            return score;
        }

        static FoundationTrialOffer[] TopOffers(List<FoundationTrialOffer> offers, int max)
        {
            if (offers == null || offers.Count == 0) return Array.Empty<FoundationTrialOffer>();
            offers.Sort((a, b) =>
            {
                int score = b.score.CompareTo(a.score);
                return score != 0 ? score : string.Compare(a.id, b.id, StringComparison.Ordinal);
            });
            int count = Math.Min(Math.Max(1, max), offers.Count);
            var result = new FoundationTrialOffer[count];
            for (int i = 0; i < count; i++)
                result[i] = offers[i];
            return result;
        }

        static string SelectOfferId(FoundationTrialOffer[] offers, string requested)
        {
            if (offers == null || offers.Length == 0) return "";
            if (!string.IsNullOrWhiteSpace(requested))
                for (int i = 0; i < offers.Length; i++)
                    if (string.Equals(offers[i].id, requested, StringComparison.Ordinal))
                        return requested;
            return offers[0].id ?? "";
        }

        static void MarkSelectedOffers(FoundationTrialOffer[] offers, string selectedId)
        {
            if (offers == null) return;
            for (int i = 0; i < offers.Length; i++)
                offers[i].selected = !string.IsNullOrWhiteSpace(selectedId) &&
                    string.Equals(offers[i].id, selectedId, StringComparison.Ordinal);
        }

        static FoundationTrialOffer[] CopyOffers(FoundationTrialOffer[] offers)
        {
            if (offers == null || offers.Length == 0) return Array.Empty<FoundationTrialOffer>();
            var copy = new FoundationTrialOffer[offers.Length];
            Array.Copy(offers, copy, offers.Length);
            return copy;
        }

        FoundationKeyValueInt[] CaptureTrialScores()
        {
            var values = new List<FoundationKeyValueInt>();
            foreach (var kv in _trialScores)
                values.Add(new FoundationKeyValueInt(kv.Key.ToString(), kv.Value));
            return values.ToArray();
        }

        static FoundationKeyValueInt[] CaptureKeyValueArray(Dictionary<string, int> source)
        {
            var values = new List<FoundationKeyValueInt>();
            foreach (var kv in source)
                values.Add(new FoundationKeyValueInt(kv.Key, kv.Value));
            return values.ToArray();
        }

        Dictionary<TrialEvidenceCategory, int> CopyTrialScores()
        {
            var copy = new Dictionary<TrialEvidenceCategory, int>();
            foreach (var kv in _trialScores)
                copy[kv.Key] = kv.Value;
            return copy;
        }

        static Dictionary<string, int> CopyIntDictionary(Dictionary<string, int> source)
        {
            var copy = new Dictionary<string, int>();
            foreach (var kv in source)
                copy[kv.Key] = kv.Value;
            return copy;
        }

        FoundationKeyValueInt[] CaptureTrialScoreDeltas(Dictionary<TrialEvidenceCategory, int> before)
        {
            var deltas = new List<FoundationKeyValueInt>();
            foreach (var kv in _trialScores)
            {
                before.TryGetValue(kv.Key, out int previous);
                int delta = kv.Value - previous;
                if (delta > 0)
                    deltas.Add(new FoundationKeyValueInt(kv.Key.ToString(), delta));
            }
            return deltas.ToArray();
        }

        static FoundationKeyValueInt[] CaptureDeltas(Dictionary<string, int> before, Dictionary<string, int> after)
        {
            var deltas = new List<FoundationKeyValueInt>();
            foreach (var kv in after)
            {
                before.TryGetValue(kv.Key, out int previous);
                int delta = kv.Value - previous;
                if (delta > 0)
                    deltas.Add(new FoundationKeyValueInt(kv.Key, delta));
            }
            return deltas.ToArray();
        }

        void RestoreTrialScores(FoundationKeyValueInt[] values)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
                if (Enum.TryParse(values[i].id, out TrialEvidenceCategory category))
                    _trialScores[category] = Math.Max(0, values[i].value);
        }

        static void RestoreKeyValueArray(FoundationKeyValueInt[] values, Dictionary<string, int> target)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length; i++)
                if (!string.IsNullOrWhiteSpace(values[i].id))
                    target[values[i].id] = Math.Max(0, values[i].value);
        }

        FoundationTrialReadState CaptureTrialReadState()
        {
            var lifecycle = CaptureTrialLifecycle();
            return new FoundationTrialReadState
            {
                trialDay = lifecycle.trialDay,
                trialDurationDays = lifecycle.trialDurationDays,
                completed = lifecycle.completed,
                totalScore = TotalTrialScore,
                gradeForecast = GradeForecast,
                gradeSnapshot = lifecycle.gradeSnapshot,
                categories = CaptureTrialScores(),
                evidenceLog = _evidenceLog.ToArray(),
                classOffers = lifecycle.classOffers,
                professionOffers = lifecycle.professionOffers,
                selectedClassId = lifecycle.selectedClassId,
                selectedProfessionId = lifecycle.selectedProfessionId,
            };
        }

        FoundationXpChannelReadState[] CaptureXpChannelReadStates()
        {
            var states = new List<FoundationXpChannelReadState>();
            foreach (var channel in _xpChannels)
            {
                var def = _content?.XPChannels.Get(channel.Key);
                int perLevel = Math.Max(1, def != null ? def.xpPerLevel : XpPerProgressionLevel);
                int xp = Math.Max(0, channel.Value);
                states.Add(new FoundationXpChannelReadState
                {
                    id = channel.Key,
                    displayName = def != null ? def.Display : channel.Key,
                    channel = def != null ? def.channel : FoundationXpChannel.SkillMastery,
                    xp = xp,
                    level = xp / perLevel + 1,
                    progress01 = (xp % perLevel) / (float)perLevel,
                });
            }
            return states.ToArray();
        }

        FoundationTitleReadState[] CaptureTitleReadStates()
        {
            var states = new List<FoundationTitleReadState>();
            if (_content?.Titles == null) return states.ToArray();
            foreach (var title in _content.Titles.All)
            {
                int threshold = Math.Max(1, title.threshold);
                int progress = GetTitleProgress(title.id);
                states.Add(new FoundationTitleReadState
                {
                    id = title.id,
                    displayName = title.Display,
                    progress = progress,
                    threshold = threshold,
                    progress01 = Math.Min(1f, progress / (float)threshold),
                    acquired = _acquiredTitles.Contains(title.id),
                    effectPolicy = title.effectPolicy ?? "",
                });
            }
            return states.ToArray();
        }

        FoundationAffinityReadState[] CaptureAffinityReadStates()
        {
            var states = new List<FoundationAffinityReadState>();
            if (_content?.Affinities == null) return states.ToArray();
            foreach (var affinity in _content.Affinities.All)
            {
                int threshold = Math.Max(1, affinity.awakenThreshold);
                int score = GetAffinityScore(affinity.id);
                states.Add(new FoundationAffinityReadState
                {
                    id = affinity.id,
                    displayName = affinity.Display,
                    score = score,
                    awakenThreshold = threshold,
                    progress01 = Math.Min(1f, score / (float)threshold),
                    awakened = score >= threshold,
                    rank = AffinityRankForScore(score),
                    effectMultiplier = AffinityEffectMultiplier(AffinityRankForScore(score)),
                    family = affinity.family ?? "",
                });
            }
            return states.ToArray();
        }

        int SumTrialScores()
        {
            int total = 0;
            foreach (var kv in _trialScores)
                total += Math.Max(0, kv.Value);
            return total;
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

        static FoundationAffinityRank AffinityRankForScore(int score)
        {
            if (score >= 160) return FoundationAffinityRank.Perfect;
            if (score >= 110) return FoundationAffinityRank.Epic;
            if (score >= 70) return FoundationAffinityRank.Rare;
            if (score >= 40) return FoundationAffinityRank.Uncommon;
            if (score >= 20) return FoundationAffinityRank.Common;
            if (score >= 10) return FoundationAffinityRank.Basic;
            return FoundationAffinityRank.Dormant;
        }

        static float AffinityEffectMultiplier(FoundationAffinityRank rank)
        {
            switch (rank)
            {
                case FoundationAffinityRank.Basic: return 1.05f;
                case FoundationAffinityRank.Common: return 1.10f;
                case FoundationAffinityRank.Uncommon: return 1.20f;
                case FoundationAffinityRank.Rare: return 1.35f;
                case FoundationAffinityRank.Epic: return 1.50f;
                case FoundationAffinityRank.Perfect: return 1.75f;
                default: return 1f;
            }
        }

        static FoundationGrade GradeForTotal(int total)
        {
            if (total >= 240) return FoundationGrade.S;
            if (total >= 180) return FoundationGrade.A;
            if (total >= 130) return FoundationGrade.B;
            if (total >= 90) return FoundationGrade.C;
            if (total >= 55) return FoundationGrade.D;
            if (total >= 25) return FoundationGrade.E;
            return FoundationGrade.F;
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
        public FoundationTrialReadState trial;
        public FoundationXpChannelReadState[] xpChannels;
        public FoundationTitleReadState[] titleProgress;
        public FoundationAffinityReadState[] affinities;
        public FoundationSystemMessageEntry[] systemMessages;
    }

    [Serializable]
    public struct FoundationTrialReadState
    {
        public int trialDay;
        public int trialDurationDays;
        public bool completed;
        public int totalScore;
        public FoundationGrade gradeForecast;
        public FoundationGrade gradeSnapshot;
        public FoundationKeyValueInt[] categories;
        public FoundationTrialEvidenceEntry[] evidenceLog;
        public FoundationTrialOffer[] classOffers;
        public FoundationTrialOffer[] professionOffers;
        public string selectedClassId;
        public string selectedProfessionId;
    }

    [Serializable]
    public struct FoundationXpChannelReadState
    {
        public string id;
        public string displayName;
        public FoundationXpChannel channel;
        public int xp;
        public int level;
        public float progress01;
    }

    [Serializable]
    public struct FoundationTitleReadState
    {
        public string id;
        public string displayName;
        public int progress;
        public int threshold;
        public float progress01;
        public bool acquired;
        public string effectPolicy;
    }

    [Serializable]
    public struct FoundationAffinityReadState
    {
        public string id;
        public string displayName;
        public int score;
        public int awakenThreshold;
        public float progress01;
        public bool awakened;
        public FoundationAffinityRank rank;
        public float effectMultiplier;
        public string family;
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

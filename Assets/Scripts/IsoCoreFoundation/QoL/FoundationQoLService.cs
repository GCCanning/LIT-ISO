using System;
using System.Collections.Generic;

namespace IsoCore.Foundation
{
    public sealed class FoundationQoLService
    {
        readonly Dictionary<FoundationSystemFeedChannel, bool> _feedChannels = new();
        readonly List<FoundationPinnedGoalSaveData> _pinnedGoals = new();
        readonly Dictionary<int, FoundationInventorySlotQoLSaveData> _inventorySlots = new();
        readonly List<FoundationLoadoutSaveData> _loadouts = new();

        FoundationAccessibilitySaveData _accessibility = CreateDefaultAccessibility();
        FoundationContent _content;
        FoundationProgression _progression;
        Inventory _inventory;

        public event Action Changed;

        public void Init(FoundationContent content, FoundationProgression progression, Inventory inventory)
        {
            _content = content;
            _progression = progression;
            _inventory = inventory;
            EnsureFeedDefaults();
        }

        public FoundationQoLSaveData CaptureState()
        {
            return new FoundationQoLSaveData
            {
                feedSettings = CaptureFeedSettings(),
                pinnedGoals = _pinnedGoals.ToArray(),
                inventorySlots = CaptureInventorySlotSettings(),
                loadouts = CopyLoadouts(_loadouts),
                accessibility = CopyAccessibility(_accessibility),
            };
        }

        public void RestoreState(FoundationQoLSaveData state)
        {
            _feedChannels.Clear();
            _pinnedGoals.Clear();
            _inventorySlots.Clear();
            _loadouts.Clear();
            _accessibility = CreateDefaultAccessibility();

            if (state != null)
            {
                RestoreFeedSettings(state.feedSettings);
                RestorePinnedGoals(state.pinnedGoals);
                RestoreInventorySlotSettings(state.inventorySlots);
                RestoreLoadouts(state.loadouts);
                _accessibility = NormalizeAccessibility(state.accessibility);
            }

            EnsureFeedDefaults();
            Changed?.Invoke();
        }

        public FoundationQoLReadState CaptureReadState()
        {
            return new FoundationQoLReadState
            {
                feedSettings = CaptureFeedSettingsReadState(),
                pinnedGoals = CapturePinnedGoalReadStates(),
                inventory = CaptureInventoryReadState(),
                loadouts = CaptureLoadoutReadStates(),
                accessibility = CaptureAccessibilityReadState(),
                visibleMessages = CaptureVisibleMessages(),
            };
        }

        public bool SetFeedChannelVisible(FoundationSystemFeedChannel channel, bool visible)
        {
            EnsureFeedDefaults();
            if (_feedChannels.TryGetValue(channel, out bool current) && current == visible)
                return false;

            _feedChannels[channel] = visible;
            Changed?.Invoke();
            return true;
        }

        public bool IsFeedChannelVisible(FoundationSystemFeedChannel channel)
        {
            EnsureFeedDefaults();
            return !_feedChannels.TryGetValue(channel, out bool visible) || visible;
        }

        public bool PinGoal(FoundationPinnedGoalType type, string targetId, int slot = 0, int playerId = 0, bool shared = false)
        {
            if (type == FoundationPinnedGoalType.None || string.IsNullOrWhiteSpace(targetId))
                return false;

            var goal = new FoundationPinnedGoalSaveData
            {
                type = type,
                targetId = targetId.Trim(),
                playerId = Math.Max(0, playerId),
                shared = shared,
            };

            int index = Math.Max(0, Math.Min(slot, 4));
            while (_pinnedGoals.Count <= index)
                _pinnedGoals.Add(default);

            var current = _pinnedGoals[index];
            if (current.type == goal.type &&
                string.Equals(current.targetId, goal.targetId, StringComparison.Ordinal) &&
                current.playerId == goal.playerId &&
                current.shared == goal.shared)
                return false;

            _pinnedGoals[index] = goal;
            TrimEmptyPinnedTail();
            Changed?.Invoke();
            return true;
        }

        public bool ClearPinnedGoal(int slot = 0)
        {
            if (slot < 0 || slot >= _pinnedGoals.Count)
                return false;

            _pinnedGoals[slot] = default;
            TrimEmptyPinnedTail();
            Changed?.Invoke();
            return true;
        }

        public bool SetInventorySlotFlags(int slot, bool favorite, bool locked)
        {
            if (slot < 0 || (_inventory != null && slot >= _inventory.SlotCount))
                return false;

            if (!favorite && !locked)
            {
                bool removed = _inventorySlots.Remove(slot);
                if (removed) Changed?.Invoke();
                return removed;
            }

            if (_inventorySlots.TryGetValue(slot, out var current) &&
                current.favorite == favorite &&
                current.locked == locked)
                return false;

            _inventorySlots[slot] = new FoundationInventorySlotQoLSaveData
            {
                slot = slot,
                favorite = favorite,
                locked = locked,
            };
            Changed?.Invoke();
            return true;
        }

        public bool SetAccessibility(float hudScale, float feedDuration, float feedDensity, bool reducedMotion, bool highContrast)
        {
            var next = NormalizeAccessibility(new FoundationAccessibilitySaveData
            {
                hudScale = hudScale,
                systemFeedDuration = feedDuration,
                systemFeedDensity = feedDensity,
                reducedMotion = reducedMotion,
                highContrast = highContrast,
            });

            if (Math.Abs(_accessibility.hudScale - next.hudScale) < 0.001f &&
                Math.Abs(_accessibility.systemFeedDuration - next.systemFeedDuration) < 0.001f &&
                Math.Abs(_accessibility.systemFeedDensity - next.systemFeedDensity) < 0.001f &&
                _accessibility.reducedMotion == next.reducedMotion &&
                _accessibility.highContrast == next.highContrast)
                return false;

            _accessibility = next;
            Changed?.Invoke();
            return true;
        }

        FoundationSystemFeedSettingsSaveData CaptureFeedSettings()
        {
            EnsureFeedDefaults();
            return new FoundationSystemFeedSettingsSaveData
            {
                channels = CaptureChannelSettings(),
                maxVisibleMessages = 12,
                collapseRoutineMessages = true,
            };
        }

        FoundationSystemFeedSettingsReadState CaptureFeedSettingsReadState()
        {
            var save = CaptureFeedSettings();
            return new FoundationSystemFeedSettingsReadState
            {
                channels = save.channels,
                maxVisibleMessages = save.maxVisibleMessages,
                collapseRoutineMessages = save.collapseRoutineMessages,
            };
        }

        FoundationSystemFeedChannelSetting[] CaptureChannelSettings()
        {
            var values = (FoundationSystemFeedChannel[])Enum.GetValues(typeof(FoundationSystemFeedChannel));
            var settings = new FoundationSystemFeedChannelSetting[values.Length];
            for (int i = 0; i < values.Length; i++)
                settings[i] = new FoundationSystemFeedChannelSetting(values[i], IsFeedChannelVisible(values[i]));
            return settings;
        }

        FoundationPinnedGoalReadState[] CapturePinnedGoalReadStates()
        {
            var states = new List<FoundationPinnedGoalReadState>();
            for (int i = 0; i < _pinnedGoals.Count; i++)
            {
                var goal = _pinnedGoals[i];
                if (goal.type == FoundationPinnedGoalType.None || string.IsNullOrWhiteSpace(goal.targetId))
                    continue;
                states.Add(ResolvePinnedGoal(goal));
            }
            return states.ToArray();
        }

        FoundationPinnedGoalReadState ResolvePinnedGoal(FoundationPinnedGoalSaveData goal)
        {
            var state = new FoundationPinnedGoalReadState
            {
                type = goal.type,
                targetId = goal.targetId ?? "",
                displayName = goal.targetId ?? "",
                detail = "",
                progress01 = 0f,
                completed = false,
                available = false,
                shared = goal.shared,
                playerId = goal.playerId,
            };

            switch (goal.type)
            {
                case FoundationPinnedGoalType.Quest:
                    return ResolveQuestGoal(goal, state);
                case FoundationPinnedGoalType.Recipe:
                    return ResolveRecipeGoal(goal, state);
                case FoundationPinnedGoalType.Title:
                    return ResolveTitleGoal(goal, state);
                case FoundationPinnedGoalType.Affinity:
                    return ResolveAffinityGoal(goal, state);
                case FoundationPinnedGoalType.TrialTendency:
                    return ResolveTrialTendencyGoal(goal, state);
                default:
                    state.detail = "No resolver yet";
                    return state;
            }
        }

        FoundationPinnedGoalReadState ResolveQuestGoal(FoundationPinnedGoalSaveData goal, FoundationPinnedGoalReadState state)
        {
            var quest = _content?.Quests.Get(goal.targetId);
            if (quest == null)
            {
                state.detail = "Quest unavailable";
                return state;
            }

            var read = _progression != null ? _progression.CaptureQuestReadState(goal.targetId) : default;
            state.displayName = quest.Display;
            state.available = read.active || read.completed;
            state.completed = read.completed;
            state.progress01 = read.completed ? 1f : ActiveObjectiveProgress(read);
            state.detail = read.completed ? "Complete" :
                !string.IsNullOrWhiteSpace(read.firstIncompleteObjectiveText) ? read.firstIncompleteObjectiveText : quest.description;
            return state;
        }

        static float ActiveObjectiveProgress(FoundationQuestReadState read)
        {
            if (read.objectives == null || read.objectives.Length == 0)
                return read.progress01;

            for (int i = 0; i < read.objectives.Length; i++)
            {
                var objective = read.objectives[i];
                if (objective.completed) continue;
                int required = Math.Max(1, objective.required);
                return Math.Min(1f, Math.Max(0, objective.current) / (float)required);
            }

            return read.progress01;
        }

        FoundationPinnedGoalReadState ResolveRecipeGoal(FoundationPinnedGoalSaveData goal, FoundationPinnedGoalReadState state)
        {
            var recipe = _content?.Recipes.Get(goal.targetId);
            if (recipe == null)
            {
                state.detail = "Recipe unavailable";
                return state;
            }

            int have = 0;
            int need = 0;
            string missing = "";
            if (recipe.inputs != null)
            {
                for (int i = 0; i < recipe.inputs.Length; i++)
                {
                    var ingredient = recipe.inputs[i];
                    int required = Math.Max(1, ingredient.count);
                    int current = _inventory != null ? Math.Min(required, _inventory.Count(ingredient.itemId)) : 0;
                    have += current;
                    need += required;
                    if (current < required && string.IsNullOrEmpty(missing))
                        missing = $"{ingredient.itemId} {current}/{required}";
                }
            }

            state.displayName = recipe.Display;
            state.available = true;
            state.progress01 = need > 0 ? Math.Min(1f, have / (float)need) : 1f;
            state.completed = need == 0 || have >= need;
            state.detail = state.completed ? "Ready to craft" : missing;
            return state;
        }

        FoundationPinnedGoalReadState ResolveTitleGoal(FoundationPinnedGoalSaveData goal, FoundationPinnedGoalReadState state)
        {
            var read = _progression?.CaptureReadState();
            if (read?.titleProgress == null)
            {
                state.detail = "Title unavailable";
                return state;
            }

            for (int i = 0; i < read.titleProgress.Length; i++)
            {
                if (!string.Equals(read.titleProgress[i].id, goal.targetId, StringComparison.Ordinal)) continue;
                var title = read.titleProgress[i];
                state.displayName = title.displayName;
                state.available = true;
                state.progress01 = title.progress01;
                state.completed = title.acquired;
                state.detail = title.acquired ? "Acquired" : $"{title.progress}/{title.threshold}";
                return state;
            }

            state.detail = "Title unavailable";
            return state;
        }

        FoundationPinnedGoalReadState ResolveAffinityGoal(FoundationPinnedGoalSaveData goal, FoundationPinnedGoalReadState state)
        {
            var read = _progression?.CaptureReadState();
            if (read?.affinities == null)
            {
                state.detail = "Affinity unavailable";
                return state;
            }

            for (int i = 0; i < read.affinities.Length; i++)
            {
                if (!string.Equals(read.affinities[i].id, goal.targetId, StringComparison.Ordinal)) continue;
                var affinity = read.affinities[i];
                state.displayName = affinity.displayName;
                state.available = true;
                state.progress01 = affinity.progress01;
                state.completed = affinity.awakened;
                state.detail = affinity.awakened ? "Awakened" : $"{affinity.score}/{affinity.awakenThreshold}";
                return state;
            }

            state.detail = "Affinity unavailable";
            return state;
        }

        FoundationPinnedGoalReadState ResolveTrialTendencyGoal(FoundationPinnedGoalSaveData goal, FoundationPinnedGoalReadState state)
        {
            if (!Enum.TryParse(goal.targetId, true, out TrialEvidenceCategory category))
            {
                state.detail = "Trial tendency unavailable";
                return state;
            }

            int score = _progression != null ? _progression.GetTrialScore(category) : 0;
            state.displayName = category.ToString();
            state.available = true;
            state.progress01 = Math.Min(1f, score / 25f);
            state.completed = score >= 25;
            state.detail = $"{score}/25 marks";
            return state;
        }

        FoundationInventoryQoLReadState CaptureInventoryReadState()
        {
            int count = _inventory != null ? _inventory.SlotCount : 0;
            var slots = new FoundationInventorySlotQoLReadState[count];
            for (int i = 0; i < count; i++)
            {
                var stack = _inventory.GetSlot(i);
                _inventorySlots.TryGetValue(i, out var flags);
                slots[i] = new FoundationInventorySlotQoLReadState
                {
                    slot = i,
                    itemId = stack.itemId ?? "",
                    count = Math.Max(0, stack.count),
                    favorite = flags.favorite,
                    locked = flags.locked,
                };
            }
            return new FoundationInventoryQoLReadState { slots = slots };
        }

        FoundationLoadoutReadState[] CaptureLoadoutReadStates()
        {
            var states = new FoundationLoadoutReadState[_loadouts.Count];
            for (int i = 0; i < _loadouts.Count; i++)
            {
                var loadout = _loadouts[i];
                states[i] = new FoundationLoadoutReadState
                {
                    id = loadout.id ?? "",
                    templateType = loadout.templateType,
                    displayName = loadout.displayName ?? "",
                    items = CopyStacks(loadout.items),
                };
            }
            return states;
        }

        FoundationAccessibilityReadState CaptureAccessibilityReadState()
        {
            _accessibility = NormalizeAccessibility(_accessibility);
            return new FoundationAccessibilityReadState
            {
                hudScale = _accessibility.hudScale,
                systemFeedDuration = _accessibility.systemFeedDuration,
                systemFeedDensity = _accessibility.systemFeedDensity,
                reducedMotion = _accessibility.reducedMotion,
                highContrast = _accessibility.highContrast,
            };
        }

        FoundationSystemFeedMessageReadState[] CaptureVisibleMessages()
        {
            var messages = _progression?.SystemFeed.Messages;
            if (messages == null || messages.Count == 0)
                return Array.Empty<FoundationSystemFeedMessageReadState>();

            var result = new List<FoundationSystemFeedMessageReadState>();
            int max = 12;
            for (int i = 0; i < messages.Count && result.Count < max; i++)
            {
                var channel = MapChannel(messages[i].channel);
                bool visible = IsFeedChannelVisible(channel);
                if (!visible) continue;
                result.Add(new FoundationSystemFeedMessageReadState
                {
                    sequence = messages[i].sequence,
                    channel = channel,
                    text = messages[i].text ?? "",
                    sourceId = messages[i].sourceId ?? "",
                    priority = messages[i].priority,
                    visible = true,
                });
            }
            return result.ToArray();
        }

        void RestoreFeedSettings(FoundationSystemFeedSettingsSaveData settings)
        {
            if (settings?.channels == null) return;
            for (int i = 0; i < settings.channels.Length; i++)
                _feedChannels[settings.channels[i].channel] = settings.channels[i].visible;
        }

        void RestorePinnedGoals(FoundationPinnedGoalSaveData[] goals)
        {
            if (goals == null) return;
            for (int i = 0; i < goals.Length && i < 5; i++)
            {
                var goal = goals[i];
                if (goal.type == FoundationPinnedGoalType.None || string.IsNullOrWhiteSpace(goal.targetId))
                {
                    _pinnedGoals.Add(default);
                    continue;
                }
                goal.targetId = goal.targetId.Trim();
                goal.playerId = Math.Max(0, goal.playerId);
                _pinnedGoals.Add(goal);
            }
            TrimEmptyPinnedTail();
        }

        void RestoreInventorySlotSettings(FoundationInventorySlotQoLSaveData[] slots)
        {
            if (slots == null) return;
            int max = _inventory != null ? _inventory.SlotCount : int.MaxValue;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.slot < 0 || slot.slot >= max) continue;
                if (!slot.favorite && !slot.locked) continue;
                _inventorySlots[slot.slot] = slot;
            }
        }

        void RestoreLoadouts(FoundationLoadoutSaveData[] loadouts)
        {
            if (loadouts == null) return;
            for (int i = 0; i < loadouts.Length; i++)
            {
                var loadout = NormalizeLoadout(loadouts[i]);
                if (!string.IsNullOrWhiteSpace(loadout.id))
                    _loadouts.Add(loadout);
            }
        }

        FoundationInventorySlotQoLSaveData[] CaptureInventorySlotSettings()
        {
            var values = new List<FoundationInventorySlotQoLSaveData>();
            foreach (var kv in _inventorySlots)
                if (kv.Value.favorite || kv.Value.locked)
                    values.Add(kv.Value);
            values.Sort((a, b) => a.slot.CompareTo(b.slot));
            return values.ToArray();
        }

        void EnsureFeedDefaults()
        {
            var values = (FoundationSystemFeedChannel[])Enum.GetValues(typeof(FoundationSystemFeedChannel));
            for (int i = 0; i < values.Length; i++)
                if (!_feedChannels.ContainsKey(values[i]))
                    _feedChannels[values[i]] = true;
        }

        void TrimEmptyPinnedTail()
        {
            for (int i = _pinnedGoals.Count - 1; i >= 0; i--)
            {
                if (_pinnedGoals[i].type != FoundationPinnedGoalType.None ||
                    !string.IsNullOrWhiteSpace(_pinnedGoals[i].targetId))
                    break;
                _pinnedGoals.RemoveAt(i);
            }
        }

        static FoundationSystemFeedChannel MapChannel(SystemMessageChannel channel)
        {
            switch (channel)
            {
                case SystemMessageChannel.Warning: return FoundationSystemFeedChannel.Warning;
                case SystemMessageChannel.TrialEvidence: return FoundationSystemFeedChannel.TrialEvidence;
                case SystemMessageChannel.LevelUp: return FoundationSystemFeedChannel.LevelUp;
                case SystemMessageChannel.SkillUnlock: return FoundationSystemFeedChannel.SkillUnlock;
                case SystemMessageChannel.TitleAcquired: return FoundationSystemFeedChannel.Title;
                case SystemMessageChannel.AffinityResonance: return FoundationSystemFeedChannel.Affinity;
                case SystemMessageChannel.QuestUpdate: return FoundationSystemFeedChannel.Quest;
                case SystemMessageChannel.DungeonAlert: return FoundationSystemFeedChannel.Dungeon;
                case SystemMessageChannel.PartyEvent: return FoundationSystemFeedChannel.Party;
                case SystemMessageChannel.WorldEvent: return FoundationSystemFeedChannel.WorldEvent;
                default: return FoundationSystemFeedChannel.Notice;
            }
        }

        static FoundationAccessibilitySaveData CreateDefaultAccessibility() => new FoundationAccessibilitySaveData();

        static FoundationAccessibilitySaveData NormalizeAccessibility(FoundationAccessibilitySaveData value)
        {
            value ??= CreateDefaultAccessibility();
            value.hudScale = Clamp(value.hudScale <= 0f ? 1f : value.hudScale, 0.75f, 1.75f);
            value.systemFeedDuration = Clamp(value.systemFeedDuration <= 0f ? 4f : value.systemFeedDuration, 1f, 12f);
            value.systemFeedDensity = Clamp(value.systemFeedDensity <= 0f ? 1f : value.systemFeedDensity, 0.5f, 2f);
            return value;
        }

        static FoundationLoadoutSaveData NormalizeLoadout(FoundationLoadoutSaveData loadout)
        {
            loadout ??= new FoundationLoadoutSaveData();
            loadout.id = loadout.id?.Trim() ?? "";
            loadout.displayName = string.IsNullOrWhiteSpace(loadout.displayName) ? loadout.id : loadout.displayName.Trim();
            loadout.items = CopyStacks(loadout.items);
            return loadout;
        }

        static FoundationLoadoutSaveData[] CopyLoadouts(List<FoundationLoadoutSaveData> loadouts)
        {
            if (loadouts == null || loadouts.Count == 0) return Array.Empty<FoundationLoadoutSaveData>();
            var copy = new FoundationLoadoutSaveData[loadouts.Count];
            for (int i = 0; i < loadouts.Count; i++)
                copy[i] = NormalizeLoadout(loadouts[i]);
            return copy;
        }

        static FoundationAccessibilitySaveData CopyAccessibility(FoundationAccessibilitySaveData value)
        {
            value = NormalizeAccessibility(value);
            return new FoundationAccessibilitySaveData
            {
                hudScale = value.hudScale,
                systemFeedDuration = value.systemFeedDuration,
                systemFeedDensity = value.systemFeedDensity,
                reducedMotion = value.reducedMotion,
                highContrast = value.highContrast,
            };
        }

        static ItemStack[] CopyStacks(ItemStack[] stacks)
        {
            if (stacks == null || stacks.Length == 0) return Array.Empty<ItemStack>();
            var clean = new List<ItemStack>();
            for (int i = 0; i < stacks.Length; i++)
                if (!stacks[i].IsEmpty)
                    clean.Add(new ItemStack(stacks[i].itemId, Math.Max(1, stacks[i].count), stacks[i].durability));
            return clean.ToArray();
        }

        static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}

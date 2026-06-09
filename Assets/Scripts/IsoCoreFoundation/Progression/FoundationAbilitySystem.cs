using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    public sealed class FoundationAbilitySystem
    {
        readonly Dictionary<string, float> _nextReadyTime = new();
        FoundationContent _content;
        FoundationProgression _progression;
        FoundationPlayerStats _stats;
        Func<float> _timeProvider;

        public void Init(FoundationContent content, FoundationProgression progression, FoundationPlayerStats stats,
            Func<float> timeProvider = null)
        {
            _content = content;
            _progression = progression;
            _stats = stats;
            _timeProvider = timeProvider ?? (() => Time.time);
        }

        public bool TryUseAbility(string abilityId, string sourceId, out FoundationAbilityUseResult result)
        {
            result = default;
            var ability = _content?.Abilities.Get(abilityId);
            if (ability == null)
                return Fail(abilityId, "Unknown ability.", out result);
            if (_progression == null || _stats == null)
                return Fail(ability.id, "Ability runtime is not initialized.", out result);

            float now = Now();
            if (_nextReadyTime.TryGetValue(ability.id, out float readyAt) && now < readyAt)
                return Fail(ability.id, $"Cooldown {readyAt - now:0.0}s.", out result);

            int cost = Math.Max(0, ability.resourceCost);
            bool spent = ability.resource == FoundationAbilityResource.Mana
                ? _stats.TrySpendMana(cost)
                : _stats.TrySpendStamina(cost);
            if (!spent)
                return Fail(ability.id, ability.resource == FoundationAbilityResource.Mana ? "Not enough mana." : "Not enough stamina.", out result);

            float affinityMultiplier = _progression.GetAffinityEffectMultiplier(ability.affinityId);
            int affinityScore = _progression.GetAffinityScore(ability.affinityId);
            var affinityRank = _progression.GetAffinityRank(ability.affinityId);
            float scaledPower = Math.Max(0f, ability.basePower) * affinityMultiplier;

            if (ability.activityXp > 0)
                _progression.AddActivityXp(ability.activity, ability.activityXp, ability.skillIds);
            if (!string.IsNullOrWhiteSpace(ability.evidenceId))
                _progression.RecordEvidence(ability.evidenceId, 1,
                    string.IsNullOrWhiteSpace(sourceId) ? ability.id : sourceId);
            if (!string.IsNullOrWhiteSpace(ability.systemMessage))
                _progression.SystemFeed.Queue(
                    ability.UsesAffinity ? SystemMessageChannel.AffinityResonance : SystemMessageChannel.Notice,
                    ability.systemMessage,
                    ability.id,
                    ability.IsSpell ? 2 : 1);

            if (ability.cooldownSeconds > 0f)
                _nextReadyTime[ability.id] = now + ability.cooldownSeconds;

            result = CreateResult(ability, true, "", cost, scaledPower, affinityScore, affinityRank,
                affinityMultiplier, now);
            return true;
        }

        public FoundationAbilityReadState[] CaptureReadState()
        {
            if (_content?.Abilities == null)
                return Array.Empty<FoundationAbilityReadState>();

            var states = new List<FoundationAbilityReadState>();
            float now = Now();
            foreach (var ability in _content.Abilities.All)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.id))
                    continue;

                string unavailable = UnavailableReason(ability, now);
                int affinityScore = _progression != null ? _progression.GetAffinityScore(ability.affinityId) : 0;
                var rank = _progression != null ? _progression.GetAffinityRank(ability.affinityId) : FoundationAffinityRank.Dormant;
                float multiplier = _progression != null ? _progression.GetAffinityEffectMultiplier(ability.affinityId) : 1f;
                states.Add(new FoundationAbilityReadState
                {
                    id = ability.id,
                    displayName = ability.Display,
                    description = ability.description ?? "",
                    kind = ability.kind,
                    resource = ability.resource,
                    element = ability.element,
                    activity = ability.activity,
                    resourceCost = Math.Max(0, ability.resourceCost),
                    cooldownSeconds = Math.Max(0f, ability.cooldownSeconds),
                    cooldownRemaining = CooldownRemaining(ability.id, now),
                    canUse = string.IsNullOrWhiteSpace(unavailable),
                    unavailableReason = unavailable,
                    basePower = Math.Max(0f, ability.basePower),
                    scaledPower = Math.Max(0f, ability.basePower) * multiplier,
                    range = Math.Max(0f, ability.range),
                    skillIds = CopyStrings(ability.skillIds),
                    evidenceId = ability.evidenceId ?? "",
                    affinityId = ability.affinityId ?? "",
                    affinityScore = affinityScore,
                    affinityRank = rank,
                    affinityMultiplier = multiplier,
                });
            }

            return states.ToArray();
        }

        string UnavailableReason(FoundationAbilityDefinition ability, float now)
        {
            if (ability == null)
                return "Unknown ability.";
            if (_stats == null)
                return "Stats unavailable.";

            float remaining = CooldownRemaining(ability.id, now);
            if (remaining > 0f)
                return $"Cooldown {remaining:0.0}s.";

            int cost = Math.Max(0, ability.resourceCost);
            if (ability.resource == FoundationAbilityResource.Mana && _stats.Mana < cost)
                return "Not enough mana.";
            if (ability.resource == FoundationAbilityResource.Stamina && _stats.Stamina < cost)
                return "Not enough stamina.";

            return "";
        }

        float CooldownRemaining(string abilityId, float now)
        {
            return _nextReadyTime.TryGetValue(abilityId ?? "", out float readyAt)
                ? Math.Max(0f, readyAt - now)
                : 0f;
        }

        static bool Fail(string abilityId, string reason, out FoundationAbilityUseResult result)
        {
            result = new FoundationAbilityUseResult
            {
                abilityId = abilityId ?? "",
                success = false,
                failureReason = reason ?? "Unavailable.",
                resourceSpent = 0,
                scaledPower = 0f,
                affinityMultiplier = 1f,
                affinityRank = FoundationAffinityRank.Dormant,
            };
            return false;
        }

        FoundationAbilityUseResult CreateResult(FoundationAbilityDefinition ability, bool success, string failureReason,
            int cost, float scaledPower, int affinityScore, FoundationAffinityRank affinityRank, float affinityMultiplier,
            float now)
        {
            return new FoundationAbilityUseResult
            {
                abilityId = ability.id,
                displayName = ability.Display,
                success = success,
                failureReason = failureReason ?? "",
                kind = ability.kind,
                resource = ability.resource,
                element = ability.element,
                resourceSpent = cost,
                scaledPower = scaledPower,
                cooldownRemaining = CooldownRemaining(ability.id, now),
                affinityId = ability.affinityId ?? "",
                affinityScore = affinityScore,
                affinityRank = affinityRank,
                affinityMultiplier = affinityMultiplier,
            };
        }

        float Now() => _timeProvider != null ? _timeProvider() : 0f;

        static string[] CopyStrings(string[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<string>();
            var copy = new string[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }
    }

    [Serializable]
    public struct FoundationAbilityUseResult
    {
        public string abilityId;
        public string displayName;
        public bool success;
        public string failureReason;
        public FoundationAbilityKind kind;
        public FoundationAbilityResource resource;
        public FoundationAbilityElement element;
        public int resourceSpent;
        public float scaledPower;
        public float cooldownRemaining;
        public string affinityId;
        public int affinityScore;
        public FoundationAffinityRank affinityRank;
        public float affinityMultiplier;
    }

    [Serializable]
    public struct FoundationAbilityReadState
    {
        public string id;
        public string displayName;
        public string description;
        public FoundationAbilityKind kind;
        public FoundationAbilityResource resource;
        public FoundationAbilityElement element;
        public FoundationProgressionActivity activity;
        public int resourceCost;
        public float cooldownSeconds;
        public float cooldownRemaining;
        public bool canUse;
        public string unavailableReason;
        public float basePower;
        public float scaledPower;
        public float range;
        public string[] skillIds;
        public string evidenceId;
        public string affinityId;
        public int affinityScore;
        public FoundationAffinityRank affinityRank;
        public float affinityMultiplier;
    }
}

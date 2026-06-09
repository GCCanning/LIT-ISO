using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Lightweight campsite mechanics: fire auras suppress low-tier mob spawns,
    /// night reduces recovery away from camp, and campfires/fireplaces offer rest.
    /// </summary>
    public sealed class FoundationCampingSystem : MonoBehaviour
    {
        IsoFoundationPlayer _player;
        PlacementSystem _placement;
        DayNightSystem _dayNight;
        FoundationProgression _progression;
        FoundationInteractionOverlay _overlay;

        PlaceableInstance _activeCamp;
        float _recheckTimer;
        float _recoveryTimer;
        float _messageTimer;
        bool _lastAtCamp;
        bool _lastNightFatigue;

        const float RecheckInterval = 0.25f;
        const float RecoveryTickSeconds = 1f;
        const float BaseHealthRecoveryPerSecond = 0.45f;
        const float BaseManaRecoveryPerSecond = 0.55f;

        public bool AtCampsite => _activeCamp != null;
        public int ActiveCampTier => AtCampsite ? Mathf.Max(0, _activeCamp.Def.campTier) : 0;
        public float ActiveCampRadius => AtCampsite ? Mathf.Max(0f, _activeCamp.Def.campWardRadius) : 0f;
        public bool NightFatigueActive => IsNight && !AtCampsite;
        public float RecoveryMultiplier
        {
            get
            {
                if (AtCampsite)
                    return Mathf.Max(1f, _activeCamp.Def.campRecoveryMultiplier);
                return IsNight ? 0.20f : 0.75f;
            }
        }

        bool IsNight => _dayNight != null && _dayNight.NightFactor >= 0.45f;

        public void Init(IsoFoundationPlayer player, PlacementSystem placement, DayNightSystem dayNight,
            FoundationProgression progression, FoundationInteractionOverlay overlay)
        {
            _player = player;
            _placement = placement;
            _dayNight = dayNight;
            _progression = progression;
            _overlay = overlay;
        }

        void Update()
        {
            if (_player == null || _placement == null)
                return;

            _recheckTimer -= Time.deltaTime;
            if (_recheckTimer <= 0f)
            {
                _recheckTimer = RecheckInterval;
                RefreshActiveCamp();
            }

            _recoveryTimer -= Time.deltaTime;
            if (_recoveryTimer <= 0f)
            {
                _recoveryTimer = RecoveryTickSeconds;
                ApplyRecoveryTick();
            }

            if (_messageTimer > 0f)
                _messageTimer -= Time.deltaTime;
        }

        void RefreshActiveCamp()
        {
            _activeCamp = null;
            if (_placement.TryFindBestCampsite(_player.Ground, out var camp, out _))
                _activeCamp = camp;

            bool atCamp = AtCampsite;
            bool fatigue = NightFatigueActive;

            if (atCamp != _lastAtCamp)
            {
                if (atCamp)
                    QueueNotice($"Campsite aura active: Tier {ActiveCampTier} ward, {ActiveCampRadius:0.#}m radius.");
                else
                    QueueNotice("You left the campsite aura.");
                _lastAtCamp = atCamp;
            }

            if (fatigue != _lastNightFatigue)
            {
                if (fatigue)
                    QueueWarning("Night fatigue: recovery is heavily reduced away from a campfire.");
                else if (IsNight && atCamp)
                    QueueNotice("Firelight steadies you. Night recovery restored at camp.");
                _lastNightFatigue = fatigue;
            }
        }

        void ApplyRecoveryTick()
        {
            var stats = _progression?.Stats;
            if (stats == null)
                return;

            float multiplier = RecoveryMultiplier;
            stats.Heal(BaseHealthRecoveryPerSecond * multiplier * RecoveryTickSeconds);
            stats.RestoreMana(BaseManaRecoveryPerSecond * multiplier * RecoveryTickSeconds);
        }

        public bool CanRestAt(PlaceableInstance camp)
        {
            if (camp == null || camp.Def == null || !camp.Def.isCampsite || _player == null)
                return false;

            float radius = Mathf.Max(1.2f, camp.Def.campWardRadius);
            return ((Vector2)(camp.transform.position - _player.transform.position)).sqrMagnitude <= radius * radius;
        }

        public bool RestAt(PlaceableInstance camp)
        {
            if (!CanRestAt(camp))
                return false;

            _dayNight?.SetTime(0.26f);
            var stats = _progression?.Stats;
            if (stats != null)
            {
                stats.Heal(stats.MaxHealth);
                stats.RestoreMana(stats.MaxMana);
            }

            _progression?.AddActivityXp(FoundationProgressionActivity.Explore, 6, "exploration", "cooking");
            _progression?.RecordEvidence("rest_at_camp", 1, camp.Def.id);
            QueueNotice("You rest by the fire. Dawn finds you steadier.");
            _overlay?.Flash("Rested until dawn", 3f);
            return true;
        }

        public string DescribeCamp(PlaceableInstance camp)
        {
            if (camp == null || camp.Def == null || !camp.Def.isCampsite)
                return "No campsite aura.";

            return $"{camp.Def.Display}: wards Tier {camp.Def.campTier} mobs within {camp.Def.campWardRadius:0.#}m. " +
                   $"Recovery x{camp.Def.campRecoveryMultiplier:0.#} while inside.";
        }

        public bool ShouldSuppressMobSpawn(MobDefinition mob, Vector2 spawnGround)
        {
            return RollMobSpawnWard(mob, spawnGround, out _);
        }

        public bool RollMobSpawnWard(MobDefinition mob, Vector2 spawnGround, out bool breached)
        {
            breached = false;
            if (mob == null || !AtCampsite || _player == null)
                return false;

            float radius = ActiveCampRadius;
            if (radius <= 0f || (spawnGround - _player.Ground).sqrMagnitude > radius * radius)
                return false;

            int threatTier = Mathf.Max(0, mob.threatTier);
            int campTier = ActiveCampTier;
            if (threatTier <= campTier)
                return true;

            int tierGap = threatTier - campTier;
            float breachChance = Mathf.Clamp01(mob.campWardIgnoreChance + Mathf.Max(0, tierGap - 1) * 0.12f);
            breached = Random.value < breachChance;
            if (breached)
                QueueWarning($"{mob.Display} presses through the fire ward.");
            return !breached;
        }

        void QueueNotice(string text)
        {
            Queue(SystemMessageChannel.Notice, text, 1);
        }

        void QueueWarning(string text)
        {
            Queue(SystemMessageChannel.Warning, text, 2);
        }

        void Queue(SystemMessageChannel channel, string text, int priority)
        {
            if (_messageTimer > 0f)
                return;

            _messageTimer = 4f;
            _progression?.SystemFeed.Queue(channel, text, "camping", priority);
            _overlay?.Tutorial(text, 5f);
        }
    }
}

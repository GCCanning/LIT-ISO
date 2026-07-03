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
        FoundationConfig _cfg;

        PlaceableInstance _activeCamp;
        CampfireWardRing _wardRing;
        float _recheckTimer;
        float _recoveryTimer;
        float _messageTimer;
        bool _lastAtCamp;
        bool _lastNightFatigue;
        bool _wasDusk;
        bool _wasNight;

        const float RecheckInterval = 0.25f;
        const float RecoveryTickSeconds = 1f;
        const float BaseHealthRecoveryPerSecond = 0.45f;
        const float BaseManaRecoveryPerSecond = 0.55f;

        public bool AtCampsite => _activeCamp != null;
        public int ActiveCampTier => AtCampsite ? Mathf.Max(0, _activeCamp.Def.campTier) : 0;
        /// <summary>
        /// Effective campfire safe radius: the placed prop's own campWardRadius, falling back to
        /// FoundationConfig.campfireSafeRadius when the prop doesn't define one (0).
        /// </summary>
        public float ActiveCampRadius
        {
            get
            {
                if (!AtCampsite) return 0f;
                float propRadius = Mathf.Max(0f, _activeCamp.Def.campWardRadius);
                if (propRadius > 0f) return propRadius;
                return _cfg != null ? Mathf.Max(0f, _cfg.campfireSafeRadius) : 0f;
            }
        }
        public bool NightFatigueActive => IsNight && !AtCampsite;

        /// <summary>Public day/night danger signal for other systems (e.g. MobSpawner).</summary>
        public bool IsNight => _dayNight != null && _dayNight.IsNight;
        public float RecoveryMultiplier
        {
            get
            {
                if (AtCampsite)
                    return Mathf.Max(1f, _activeCamp.Def.campRecoveryMultiplier);
                return IsNight ? 0.20f : 0.75f;
            }
        }

        public void Init(IsoFoundationPlayer player, PlacementSystem placement, DayNightSystem dayNight,
            FoundationProgression progression, FoundationInteractionOverlay overlay, FoundationConfig cfg = null)
        {
            _player = player;
            _placement = placement;
            _dayNight = dayNight;
            _progression = progression;
            _overlay = overlay;
            _cfg = cfg;
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

            UpdatePhaseWarnings();
            UpdateWardRing();
        }

        /// <summary>
        /// Day/night rhythm readability (reference-integration pass, 2026-07-02): the
        /// approach of night is announced BEFORE it becomes dangerous, so daytime is
        /// visibly "preparation time" and night pressure never feels arbitrary.
        /// These transition messages bypass the anti-spam window — they fire once per
        /// cycle and are the survival loop's most important cues.
        /// </summary>
        void UpdatePhaseWarnings()
        {
            if (_dayNight == null)
                return;

            bool dusk = _dayNight.time >= 0.68f && _dayNight.time < 0.80f;
            if (dusk && !_wasDusk)
            {
                _progression?.SystemFeed.Queue(SystemMessageChannel.Warning,
                    "Dusk falls. Night hunters wake soon — reach a campfire ward or keep your weapon close.",
                    "camping", 2);
                _overlay?.Tutorial("Dusk falls. Night hunters wake soon — reach a campfire ward.", 5f);
            }
            _wasDusk = dusk;

            bool night = _dayNight.IsNight;
            if (night && !_wasNight)
            {
                _progression?.SystemFeed.Queue(SystemMessageChannel.Warning,
                    AtCampsite
                        ? "Night has fallen. The fire ward holds — stay inside its light."
                        : "Night has fallen. The hunt begins. Firelight is safety.",
                    "camping", 2);
                _enduredUnwarded = false;
            }
            // Spending real time at night outside any ward is a deed the System marks
            // once at dawn ("night_endured" evidence, owner direction 2026-07-02).
            if (night && !AtCampsite)
                _unwardedNightSeconds += Time.deltaTime;
            if (night)
                _enduredUnwarded |= _unwardedNightSeconds >= 20f;
            if (!night && _wasNight)
            {
                if (_enduredUnwarded)
                    _progression?.RecordEvidence("night_endured", 1, "night");
                _enduredUnwarded = false;
                _unwardedNightSeconds = 0f;
            }
            _wasNight = night;
        }

        bool _enduredUnwarded;
        float _unwardedNightSeconds;

        /// <summary>
        /// Keeps the pooled ward-radius ring on the active camp, fading in from dusk so
        /// the safe ground is something the player can see rather than remember.
        /// </summary>
        void UpdateWardRing()
        {
            if (!AtCampsite || _dayNight == null)
            {
                _wardRing?.Hide();
                return;
            }

            float visibility = Mathf.InverseLerp(0.30f, 0.55f, _dayNight.NightFactor);
            if (visibility <= 0f)
            {
                _wardRing?.Hide();
                return;
            }

            if (_wardRing == null)
                _wardRing = CampfireWardRing.Create(transform);
            _wardRing.Show(_activeCamp.transform.position, ActiveCampRadius, visibility);
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
            float wardStrength = _cfg != null ? Mathf.Clamp01(_cfg.campfireWardStrength) : 1f;
            float breachChance = Mathf.Clamp01(mob.campWardIgnoreChance + Mathf.Max(0, tierGap - 1) * 0.12f
                + (1f - wardStrength));
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

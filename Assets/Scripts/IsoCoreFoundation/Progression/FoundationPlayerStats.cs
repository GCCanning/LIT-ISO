using System;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Foundation-owned LitRPG character stats for HUD/System UI binding.
    /// </summary>
    public sealed class FoundationPlayerStats
    {
        public event Action Changed;

        public int Level { get; private set; } = 1;
        public int Experience { get; private set; }
        public int ExperienceToNextLevel { get; private set; } = 100;

        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Mana { get; private set; }
        public float MaxMana { get; private set; }
        public float Stamina { get; private set; }
        public float MaxStamina { get; private set; }

        // Base core stats (from calling/level/debug). Equipment bonuses are layered on top
        // via _equip* offsets; the public STR/DEX/... getters report base+equip so all
        // existing consumers (vitals, multipliers, HUD) see the equipped totals.
        int _baseStr, _baseDex, _baseInt, _baseVit, _baseDef, _baseLuck;
        int _equipStr, _equipDex, _equipInt, _equipVit, _equipDef, _equipLuck;

        public int STR => _baseStr + _equipStr;
        public int DEX => _baseDex + _equipDex;
        public int INT => _baseInt + _equipInt;
        public int VIT => _baseVit + _equipVit;
        public int DEF => Math.Max(0, _baseDef + _equipDef);
        public int LUCK => Math.Max(0, _baseLuck + _equipLuck);

        public string Class { get; private set; } = "Wanderer";
        public string Title { get; private set; } = "Newcomer";

        // Fix #23: (review doc: RecalculateVitals revived dead players)
        // True once vitals have been seeded (new character or loaded save). After that,
        // RecalculateVitals only clamps downward — it never refills a zero (dead) vital.
        bool _vitalsInitialized;

        // ---- 2026-06-13 owner request: DEX-driven movement/casting progression ----
        // Baseline DEX is 8 (the starting value set by SetCoreStats/ApplyCalling), so
        // both multipliers evaluate to exactly 1.0x for a fresh character — existing
        // balance is unchanged until points are put into DEX (or removed from it by
        // future content). Clamped so neither extreme can break traversal or combat
        // pacing.

        /// <summary>Movement-speed scalar. Each DEX point above/below the baseline of 8
        /// shifts speed +/-2.5%, clamped to [0.7x, 1.6x].</summary>
        public float MoveSpeedMultiplier => Clamp(1f + (DEX - 8) * 0.025f, 0.7f, 1.6f);

        /// <summary>Ability cooldown/cast-time scalar. Each DEX point above/below the
        /// baseline of 8 shifts cooldowns -/+2%, clamped to [0.5x, 1.3x]. Higher DEX
        /// casts faster; lower DEX casts slower. 0.5x floor keeps cooldowns from
        /// hitting zero and trivializing resource costs.</summary>
        public float CooldownMultiplier => Clamp(1f - (DEX - 8) * 0.02f, 0.5f, 1.3f);

        // ---- 2026-06-13 owner request (A7): STR-driven jump mechanics ----
        // Baseline STR is 8 (same starting value as DEX from SetCoreStats/ApplyCalling),
        // so both jump properties evaluate to their neutral defaults for a fresh
        // character — existing traversal balance is unchanged until points are put
        // into STR (or removed from it by future content).

        /// <summary>Extra cfg.jumpClimbSteps allowance while airborne, from STR above the
        /// baseline of 8. Every 4 STR points above baseline grants +1 climbable step,
        /// clamped to [0, 3] so a jump can never trivially scale an entire cliff face.</summary>
        public int JumpClimbBonus => (int)Clamp((STR - 8) / 4f, 0f, 3f);

        /// <summary>Visual jump-hop height scalar. Each STR point above/below the baseline
        /// of 8 shifts the hop arc +/-3%, clamped to [0.85x, 1.45x]. Purely cosmetic — does
        /// not affect Walkable()'s climb logic, only Refresh()'s lift arc.</summary>
        public float JumpHeightMultiplier => Clamp(1f + (STR - 8) * 0.03f, 0.85f, 1.45f);

        public float Health01 => Ratio(Health, MaxHealth);
        public float Mana01 => Ratio(Mana, MaxMana);
        public float Stamina01 => Ratio(Stamina, MaxStamina);
        public float Xp01 => Ratio(Experience, ExperienceToNextLevel);

        public FoundationPlayerStats()
        {
            SetCoreStats(8, 8, 8, 10, 5, 5);
            Health = MaxHealth;
            Mana = MaxMana;
            Stamina = MaxStamina;
        }

        public void ApplyCalling(FoundationCallingDefinition calling)
        {
            if (calling == null) return;

            Class = calling.Display;
            Title = string.IsNullOrWhiteSpace(calling.startingTitle) ? "Newcomer" : calling.startingTitle;

            SetCoreStats(8, 8, 8, 10, 5, 5);
            if (calling.statBonuses != null)
                foreach (var bonus in calling.statBonuses)
                    AddStat(bonus.stat, bonus.amount);

            RecalculateVitals();
            Health = MaxHealth;
            Mana = MaxMana;
            Stamina = MaxStamina;
            Changed?.Invoke();
        }

        public void SetIdentity(string className, string title)
        {
            Class = string.IsNullOrWhiteSpace(className) ? "Wanderer" : className.Trim();
            Title = string.IsNullOrWhiteSpace(title) ? "Newcomer" : title.Trim();
            Changed?.Invoke();
        }

        public void SetCoreStats(int str, int dex, int intelligence, int vit, int def, int luck)
        {
            _baseStr = Math.Max(1, str);
            _baseDex = Math.Max(1, dex);
            _baseInt = Math.Max(1, intelligence);
            _baseVit = Math.Max(1, vit);
            _baseDef = Math.Max(0, def);
            _baseLuck = Math.Max(0, luck);
            RecalculateVitals();
            Changed?.Invoke();
        }

        public void SetVitals(float health, float maxHealth, float mana, float maxMana)
        {
            MaxHealth = Math.Max(1f, maxHealth);
            MaxMana = Math.Max(1f, maxMana);
            Health = Clamp(health, 0f, MaxHealth);
            Mana = Clamp(mana, 0f, MaxMana);
            Changed?.Invoke();
        }

        public void SetStamina(float stamina, float maxStamina)
        {
            MaxStamina = Math.Max(1f, maxStamina);
            Stamina = Clamp(stamina, 0f, MaxStamina);
            Changed?.Invoke();
        }

        public void Damage(float amount)
        {
            if (amount <= 0f) return;
            Health = Math.Max(0f, Health - amount);
            Changed?.Invoke();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            Health = Math.Min(MaxHealth, Health + amount);
            Changed?.Invoke();
        }

        public bool TrySpendMana(float amount)
        {
            if (amount <= 0f) return true;
            if (Mana < amount) return false;
            Mana -= amount;
            Changed?.Invoke();
            return true;
        }

        public bool TrySpendStamina(float amount)
        {
            if (amount <= 0f) return true;
            if (Stamina < amount) return false;
            Stamina -= amount;
            Changed?.Invoke();
            return true;
        }

        public void RestoreMana(float amount)
        {
            if (amount <= 0f) return;
            Mana = Math.Min(MaxMana, Mana + amount);
            Changed?.Invoke();
        }

        public void RestoreStamina(float amount)
        {
            if (amount <= 0f) return;
            Stamina = Math.Min(MaxStamina, Stamina + amount);
            Changed?.Invoke();
        }

        public void AddExperience(int amount)
        {
            if (amount <= 0) return;

            int previousLevel = Level;
            Experience += amount;
            while (Experience >= ExperienceToNextLevel)
            {
                Experience -= ExperienceToNextLevel;
                Level++;
                ExperienceToNextLevel = Math.Max(ExperienceToNextLevel + 25, (int)Math.Round(ExperienceToNextLevel * 1.2f));
                AddStat(FoundationStatType.VIT, 1);
                AddStat(FoundationStatType.LUCK, Level % 3 == 0 ? 1 : 0);
            }

            RecalculateVitals();
            Changed?.Invoke();

            // Real level-up flourish: only when the character actually gained one or more
            // levels from this XP award. Vitals/stats are already settled above, so the
            // celebration screen reflects the post-level-up state. Show() is a static no-op
            // until the LevelUpView singleton has installed itself, so this is safe at any
            // time during play.
            if (Level > previousLevel)
                FoundationUiBridge.RequestLevelUp(previousLevel, Level);
        }

        public FoundationPlayerStatsSaveData CaptureState()
        {
            return new FoundationPlayerStatsSaveData
            {
                level = Level,
                experience = Experience,
                experienceToNextLevel = ExperienceToNextLevel,
                health = Health,
                maxHealth = MaxHealth,
                mana = Mana,
                maxMana = MaxMana,
                stamina = Stamina,
                maxStamina = MaxStamina,
                str = _baseStr,
                dex = _baseDex,
                intelligence = _baseInt,
                vit = _baseVit,
                def = _baseDef,
                luck = _baseLuck,
                className = Class,
                title = Title,
            };
        }

        public void RestoreState(FoundationPlayerStatsSaveData state)
        {
            if (state == null) return;

            Level = Math.Max(1, state.level);
            Experience = Math.Max(0, state.experience);
            ExperienceToNextLevel = Math.Max(1, state.experienceToNextLevel);
            Class = string.IsNullOrWhiteSpace(state.className) ? "Wanderer" : state.className.Trim();
            Title = string.IsNullOrWhiteSpace(state.title) ? "Newcomer" : state.title.Trim();

            _baseStr = Math.Max(1, state.str);
            _baseDex = Math.Max(1, state.dex);
            _baseInt = Math.Max(1, state.intelligence);
            _baseVit = Math.Max(1, state.vit);
            _baseDef = Math.Max(0, state.def);
            _baseLuck = Math.Max(0, state.luck);
            // Equipment offsets are re-applied by EquipmentLoadout.RestoreState after this.
            _equipStr = _equipDex = _equipInt = _equipVit = _equipDef = _equipLuck = 0;

            MaxHealth = Math.Max(1f, state.maxHealth);
            MaxMana = Math.Max(1f, state.maxMana);
            MaxStamina = state.maxStamina > 0f
                ? Math.Max(1f, state.maxStamina)
                : Math.Max(1f, CalculatedMaxStamina());
            Health = Clamp(state.health, 0f, MaxHealth);
            Mana = Clamp(state.mana, 0f, MaxMana);
            Stamina = state.maxStamina > 0f ? Clamp(state.stamina, 0f, MaxStamina) : MaxStamina;
            // Fix #23: loaded vitals are authoritative: a saved dead character must stay
            // dead, so later recalculations may only clamp, never refill.
            _vitalsInitialized = true;
            Changed?.Invoke();
        }

        /// <summary>
        /// Admin/debug tab support: adjust a single core stat by +/-amount and
        /// recalculate dependent vitals, raising <see cref="Changed"/> so bound
        /// UI (Character tab, HUD) updates immediately.
        /// </summary>
        public void DebugAdjustStat(FoundationStatType stat, int amount)
        {
            if (amount == 0) return;
            AddStat(stat, amount);
            RecalculateVitals();
            Changed?.Invoke();
        }

        /// <summary>Admin/debug tab support: directly set the character level (min 1).
        /// Does not grant the stat bonuses normal level-ups award via AddExperience.</summary>
        public void DebugSetLevel(int level)
        {
            Level = Math.Max(1, level);
            Changed?.Invoke();
        }

        void AddStat(FoundationStatType stat, int amount)
        {
            if (amount == 0) return;
            switch (stat)
            {
                case FoundationStatType.STR: _baseStr = Math.Max(1, _baseStr + amount); break;
                case FoundationStatType.DEX: _baseDex = Math.Max(1, _baseDex + amount); break;
                case FoundationStatType.INT: _baseInt = Math.Max(1, _baseInt + amount); break;
                case FoundationStatType.VIT: _baseVit = Math.Max(1, _baseVit + amount); break;
                case FoundationStatType.DEF: _baseDef = Math.Max(0, _baseDef + amount); break;
                case FoundationStatType.LUCK: _baseLuck = Math.Max(0, _baseLuck + amount); break;
            }
        }

        /// <summary>
        /// Phase 1 equipment: replaces the current equipped stat offsets with the supplied
        /// totals (sum of every equipped item's StatBonuses) and recalculates dependent
        /// vitals. The EquipmentLoadout calls this whenever gear is equipped/unequipped.
        /// </summary>
        public void ApplyEquipmentModifiers(int str, int dex, int intelligence, int vit, int def, int luck)
        {
            _equipStr = str;
            _equipDex = dex;
            _equipInt = intelligence;
            _equipVit = vit;
            _equipDef = def;
            _equipLuck = luck;
            RecalculateVitals();
            Changed?.Invoke();
        }

        void RecalculateVitals()
        {
            MaxHealth = 60f + VIT * 10f;
            MaxMana = 30f + INT * 5f;
            MaxStamina = CalculatedMaxStamina();

            if (!_vitalsInitialized)
            {
                // Fix #23: first recalculation (new character): seed vitals at full.
                Health = MaxHealth;
                Mana = MaxMana;
                Stamina = MaxStamina;
                _vitalsInitialized = true;
                return;
            }

            // Fix #23: generic stat recalculation must never revive: only clamp down to the new
            // maximums. A dead (0 HP) character stays dead through level-ups/class changes.
            // (ApplyCalling intentionally refills vitals to max itself after this call.)
            Health = Math.Min(Health, MaxHealth);
            Mana = Math.Min(Mana, MaxMana);
            Stamina = Math.Min(Stamina, MaxStamina);
        }

        float CalculatedMaxStamina()
        {
            return 35f + DEX * 4f + VIT * 2f;
        }

        static float Ratio(float value, float max)
        {
            if (max <= 0f) return 0f;
            return Clamp(value / max, 0f, 1f);
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}

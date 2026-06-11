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

        public int STR { get; private set; }
        public int DEX { get; private set; }
        public int INT { get; private set; }
        public int VIT { get; private set; }
        public int DEF { get; private set; }
        public int LUCK { get; private set; }

        public string Class { get; private set; } = "Wanderer";
        public string Title { get; private set; } = "Newcomer";

        // Fix #23: (review doc: RecalculateVitals revived dead players)
        // True once vitals have been seeded (new character or loaded save). After that,
        // RecalculateVitals only clamps downward — it never refills a zero (dead) vital.
        bool _vitalsInitialized;

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
            STR = Math.Max(1, str);
            DEX = Math.Max(1, dex);
            INT = Math.Max(1, intelligence);
            VIT = Math.Max(1, vit);
            DEF = Math.Max(0, def);
            LUCK = Math.Max(0, luck);
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
                str = STR,
                dex = DEX,
                intelligence = INT,
                vit = VIT,
                def = DEF,
                luck = LUCK,
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

            STR = Math.Max(1, state.str);
            DEX = Math.Max(1, state.dex);
            INT = Math.Max(1, state.intelligence);
            VIT = Math.Max(1, state.vit);
            DEF = Math.Max(0, state.def);
            LUCK = Math.Max(0, state.luck);

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

        void AddStat(FoundationStatType stat, int amount)
        {
            if (amount == 0) return;
            switch (stat)
            {
                case FoundationStatType.STR: STR = Math.Max(1, STR + amount); break;
                case FoundationStatType.DEX: DEX = Math.Max(1, DEX + amount); break;
                case FoundationStatType.INT: INT = Math.Max(1, INT + amount); break;
                case FoundationStatType.VIT: VIT = Math.Max(1, VIT + amount); break;
                case FoundationStatType.DEF: DEF = Math.Max(0, DEF + amount); break;
                case FoundationStatType.LUCK: LUCK = Math.Max(0, LUCK + amount); break;
            }
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

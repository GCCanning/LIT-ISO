using UnityEngine;
using IsoCore.Foundation;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Dresses humanoid NPC mobs (bandits, adventuring parties) with the SAME layered character
    /// creator the player uses. Listens for <see cref="Mob.Spawned"/>; for any mob whose
    /// <see cref="Mob.AppearanceTheme"/> is set, it attaches a <see cref="LayeredCharacterAnimator"/>,
    /// applies a class-themed random wardrobe, tells the mob to yield its own sprite, and feeds the
    /// mob's facing/movement into the animator each frame.
    ///
    /// Lives in Assembly-CSharp (which references the IsoCore.Foundation asmdef), mirroring how
    /// LayeredCharacterPlayerHook wires the player — the foundation can't reference the wardrobe,
    /// so the wardrobe reaches in via the static Mob.Spawned event.
    /// </summary>
    public static class LayeredNpcHook
    {
        static bool _subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            if (_subscribed) return;
            _subscribed = true;
            Mob.Spawned += OnMobSpawned;
        }

        static void OnMobSpawned(Mob mob)
        {
            if (mob == null) return;
            string theme = mob.AppearanceTheme;
            if (string.IsNullOrEmpty(theme)) return;

            var go = mob.gameObject;
            if (go.GetComponent<LayeredCharacterAnimator>() != null) return;

            var anim = go.AddComponent<LayeredCharacterAnimator>();
            anim.autoLoadSavedAppearance = false;     // don't inherit the player's saved look
            anim.useWorldAmbientTint = true;          // tint with day/night like the world
            anim.Apply(ThemedRandom(theme));
            mob.UseExternalAppearance();              // the mob stops drawing its own blob

            go.AddComponent<LayeredNpcDriver>().Init(mob, anim);
        }

        /// <summary>
        /// Phase 3: layers the mob's tier-appropriate visual gear (MobDefinition.equipTiers)
        /// over its themed base, using the mob's Level. Cumulative — every tier whose minLevel
        /// the mob meets contributes its ids. Called by the driver once Level is known.
        /// </summary>
        public static void ApplyTierGear(Mob mob, LayeredCharacterAnimator anim)
        {
            if (mob == null || anim == null) return;
            var def = mob.Def;
            if (def == null || def.equipTiers == null || def.equipTiers.Length == 0) return;

            var cat = CharacterLayerCatalog.Instance;
            var appearance = anim.Appearance ?? LayeredAppearance.LoadOrDefault();
            bool changed = false;
            foreach (var tier in def.equipTiers)
            {
                if (mob.Level < tier.minLevel || tier.lpcCatalogIds == null) continue;
                foreach (var id in tier.lpcCatalogIds)
                {
                    var it = FindItem(cat, id);
                    if (it == null || appearance.HasEquipped(id)) continue;
                    appearance.Equip(id, RandomVariant(it), it.equipSlot);
                    changed = true;
                }
            }
            if (changed) anim.Apply(appearance);
        }

        // A fully random wardrobe, then class-readable overrides using the real LPC catalog ids.
        static LayeredAppearance ThemedRandom(string theme)
        {
            var cat = CharacterLayerCatalog.Instance;
            var a = LayeredAppearance.Random(cat);
            switch (theme)
            {
                case "knight":
                case "tank":
                    SetSlot(a, cat, "shirt", "lpc/torso_clothes_longsleeve");
                    SetSlot(a, cat, "pants", "lpc/legs_pants");
                    AddAcc(a, cat, "lpc/cape_solid");
                    break;
                case "mage":
                    SetSlot(a, cat, "shirt", "lpc/torso_clothes_longsleeve");
                    SetSlot(a, cat, "pants", "lpc/legs_skirts_plain");   // robe-ish
                    AddAcc(a, cat, "lpc/cape_solid");
                    break;
                case "rogue":
                    SetSlot(a, cat, "shirt", "lpc/torso_clothes_shortsleeve");
                    SetSlot(a, cat, "pants", "lpc/legs_pants");
                    AddAcc(a, cat, "lpc/hat_cap_leather_feather");   // was lpc/hat_cap_feather (renamed by importer)
                    AddAcc(a, cat, "lpc/cape_tattered");
                    break;
                case "bandit":
                    SetSlot(a, cat, "shirt", "lpc/torso_clothes_sleeveless");
                    SetSlot(a, cat, "pants", "lpc/legs_pants");
                    AddAcc(a, cat, "lpc/hat_bandana");
                    AddAcc(a, cat, "lpc/cape_tattered");
                    break;
            }
            return a;
        }

        static ItemDef FindItem(CharacterLayerCatalog cat, string id)
        {
            if (cat == null || cat.items == null) return null;
            foreach (var it in cat.items)
                if (it != null && it.id == id) return it;
            return null;
        }

        static string RandomVariant(ItemDef it) =>
            it != null && it.variants != null && it.variants.Length > 0
                ? it.variants[Random.Range(0, it.variants.Length)] : "";

        static void SetSlot(LayeredAppearance a, CharacterLayerCatalog cat, string slot, string id)
        {
            var it = FindItem(cat, id);
            if (it == null) return;
            string v = RandomVariant(it);
            if (slot == "shirt") { a.shirtId = id; if (v != "") a.shirtVariant = v; }
            else if (slot == "pants") { a.pantsId = id; if (v != "") a.pantsVariant = v; }
        }

        static void AddAcc(LayeredAppearance a, CharacterLayerCatalog cat, string id)
        {
            var it = FindItem(cat, id);
            if (it == null || a.HasEquipped(id)) return;
            a.Equip(id, RandomVariant(it), it.equipSlot);
        }
    }

    /// <summary>
    /// Per-NPC driver: feeds the mob's facing/movement into its layered animator each frame,
    /// applies tier-appropriate gear once the mob's Level is known (Phase 3), and plays the
    /// matching one-shot when the mob casts an ability (Phase 3/4, via <see cref="Mob.AbilityUsed"/>).
    /// </summary>
    public class LayeredNpcDriver : MonoBehaviour
    {
        Mob _mob;
        LayeredCharacterAnimator _anim;
        bool _tierGearApplied;
        bool _subscribed;

        public void Init(Mob mob, LayeredCharacterAnimator anim)
        {
            _mob = mob;
            _anim = anim;
            if (_mob != null) { _mob.AbilityUsed += OnAbilityUsed; _subscribed = true; }
        }

        void OnDisable()
        {
            if (_subscribed && _mob != null) { _mob.AbilityUsed -= OnAbilityUsed; _subscribed = false; }
        }

        void LateUpdate()
        {
            if (_mob == null) { Destroy(this); return; }
            // Tier gear is applied once: at first LateUpdate the spawner has already run
            // ApplyTier (Init -> Spawned -> SetCombatContext -> ApplyTier), so Level is final.
            if (!_tierGearApplied)
            {
                _tierGearApplied = true;
                LayeredNpcHook.ApplyTierGear(_mob, _anim);
            }
            if (_anim != null) _anim.SetMotion(_mob.FaceDir, _mob.IsMoving);
        }

        void OnAbilityUsed(string abilityId)
        {
            if (_anim == null) return;
            _anim.PlayOneShot(LayeredAbilityAnim.Resolve(abilityId));
        }
    }
}

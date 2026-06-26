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
                    AddAcc(a, cat, "lpc/hat_cap_feather");
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
            if (it == null || a.HasAccessory(id)) return;
            a.accessories.Add(new AccessoryEntry { itemId = id, variant = RandomVariant(it) });
        }
    }

    /// <summary>Per-NPC driver: feeds the mob's facing/movement into its layered animator each frame.</summary>
    public class LayeredNpcDriver : MonoBehaviour
    {
        Mob _mob;
        LayeredCharacterAnimator _anim;

        public void Init(Mob mob, LayeredCharacterAnimator anim) { _mob = mob; _anim = anim; }

        void LateUpdate()
        {
            if (_mob == null) { Destroy(this); return; }
            if (_anim != null) _anim.SetMotion(_mob.FaceDir, _mob.IsMoving);
        }
    }
}

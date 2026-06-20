using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// The player's chosen look (v3, LPC v4 wardrobe): body type + skin tone +
    /// cosmetic item/variant per creator slot (head/eyes/hair/shirt/pants/shoes)
    /// + an equipped-gear channel (weapons/armour/capes, equippable by player AND
    /// NPCs). The creator only edits cosmetic slots; equipment is layered on at
    /// runtime from the inventory/loot system. Persisted as JSON in PlayerPrefs.
    /// </summary>
    [Serializable]
    public class GearEntry
    {
        public string itemId;     // e.g. "lpc/cape_solid"
        public string variant;    // e.g. "red"
        public string equipSlot;  // weapon|offhand|head|chest|legs|feet|hands|back|waist|accessory
    }

    [Serializable]
    public class LayeredAppearance
    {
        public string bodyType = "male";
        public string skinVariant = "light";   // applied to body + head + matchBodyColor items
        public string eyesId = "lpc/eyes_cyclops";
        public string eyesVariant = "cyclops";   // authored color; the lone eyes item in v1 has no recolorable ramp
        public string hairId = "lpc/hair_plain";
        public string hairVariant = "ash";
        public string shirtId = "lpc/torso_clothes_shortsleeve";
        public string shirtVariant = "brown";
        public string pantsId = "lpc/legs_pants";
        public string pantsVariant = "brown";
        public string shoesId = "lpc/feet_shoes_basic";
        public string shoesVariant = "brown";

        // Equipped gear (player + NPC). Kept distinct from cosmetics so the
        // creator never rolls a cuirass as the "shirt".
        public List<GearEntry> equipped = new();

        // Legacy field (v2). Migrated into 'equipped' on load.
        public List<AccessoryEntry> accessories = new();

        public const string PrefsKey = "litiso.layered_appearance.v3";

        public string ToJson() => JsonUtility.ToJson(this);

        public LayeredAppearance Clone() => JsonUtility.FromJson<LayeredAppearance>(ToJson());

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, ToJson());
            PlayerPrefs.Save();
        }

        public static bool HasSaved => PlayerPrefs.HasKey(PrefsKey);

        public static LayeredAppearance LoadOrDefault()
        {
            var json = PlayerPrefs.GetString(PrefsKey, null);
            if (string.IsNullOrEmpty(json)) return Sanitize(new LayeredAppearance());
            try { return Sanitize(JsonUtility.FromJson<LayeredAppearance>(json) ?? new LayeredAppearance()); }
            catch { return Sanitize(new LayeredAppearance()); }
        }

        /// <summary>The human head item for this body type (head is separate from body in LPC v4).</summary>
        public string HeadId => bodyType == "female" ? "lpc/heads_human_female" : "lpc/heads_human_male";

        /// <summary>Clamp every reference to something that exists in the catalog.</summary>
        public static LayeredAppearance Sanitize(LayeredAppearance a)
        {
            var cat = CharacterLayerCatalog.Instance;
            if (cat.items.Length == 0) return a;
            if (Array.IndexOf(cat.bodyTypes, a.bodyType) < 0) a.bodyType = cat.bodyTypes[0];

            // migrate legacy accessories -> equipped
            if (a.accessories != null && a.accessories.Count > 0)
            {
                foreach (var acc in a.accessories)
                {
                    if (string.IsNullOrEmpty(acc.itemId)) continue;
                    if (a.equipped.Any(e => e.itemId == acc.itemId)) continue;
                    var d = cat.Find(acc.itemId);
                    if (d == null) continue;
                    a.equipped.Add(new GearEntry { itemId = acc.itemId, variant = d.SafeVariant(acc.variant), equipSlot = d.equipSlot });
                }
                a.accessories.Clear();
            }

            var body = cat.Find("lpc/body");
            if (body != null) a.skinVariant = body.SafeVariant(a.skinVariant);

            FixSlot(cat, "eyes", ref a.eyesId, ref a.eyesVariant, allowNone: true);
            FixSlot(cat, "hair", ref a.hairId, ref a.hairVariant, allowNone: true);
            FixSlot(cat, "shirt", ref a.shirtId, ref a.shirtVariant, allowNone: false);
            FixSlot(cat, "pants", ref a.pantsId, ref a.pantsVariant, allowNone: false);
            FixSlot(cat, "shoes", ref a.shoesId, ref a.shoesVariant, allowNone: false);

            a.equipped.RemoveAll(x => string.IsNullOrEmpty(x.itemId) || cat.Find(x.itemId) == null);
            foreach (var x in a.equipped)
            {
                var d = cat.Find(x.itemId);
                x.variant = d.SafeVariant(x.variant);
                x.equipSlot = d.equipSlot;
            }
            return a;
        }

        // Cosmetic-only slot fix: never resolves to an equipment item.
        static void FixSlot(CharacterLayerCatalog cat, string slot, ref string id, ref string variant, bool allowNone)
        {
            if (allowNone && string.IsNullOrEmpty(id)) return;
            var item = cat.Find(id);
            if (item != null && !item.IsCosmetic) item = null;     // ignore equipment match
            item ??= cat.FirstCosmetic(slot);
            if (item == null) { if (!allowNone) id = ""; return; }
            id = item.id;
            variant = item.SafeVariant(variant);
        }

        public static LayeredAppearance Random(CharacterLayerCatalog cat)
        {
            string PickVariant(ItemDef i) =>
                (i.variants == null || i.variants.Length == 0) ? "" : i.variants[UnityEngine.Random.Range(0, i.variants.Length)];
            ItemDef PickCosmetic(string slot)
            {
                var opts = cat.CosmeticSlot(slot);
                return opts.Count == 0 ? null : opts[UnityEngine.Random.Range(0, opts.Count)];
            }
            var a = new LayeredAppearance
            {
                bodyType = cat.bodyTypes[UnityEngine.Random.Range(0, cat.bodyTypes.Length)],
            };
            var body = cat.Find("lpc/body");
            if (body != null && body.variants.Length > 0)
                a.skinVariant = body.variants[UnityEngine.Random.Range(0, Mathf.Min(7, body.variants.Length))];

            var eyes = cat.Find("lpc/eyes_cyclops") ?? cat.FirstCosmetic("eyes");
            if (eyes != null) { a.eyesId = eyes.id; a.eyesVariant = eyes.baseVariant ?? eyes.SafeVariant("cyclops"); }
            var hair = PickCosmetic("hair"); if (hair != null) { a.hairId = hair.id; a.hairVariant = PickVariant(hair); }
            var shirt = PickCosmetic("shirt"); if (shirt != null) { a.shirtId = shirt.id; a.shirtVariant = PickVariant(shirt); }
            var pants = PickCosmetic("pants"); if (pants != null) { a.pantsId = pants.id; a.pantsVariant = PickVariant(pants); }
            var shoes = PickCosmetic("shoes"); if (shoes != null) { a.shoesId = shoes.id; a.shoesVariant = PickVariant(shoes); }
            return Sanitize(a);
        }

        /// <summary>All (itemId, variant) pairs to composite, cosmetics then equipment.</summary>
        public List<(string itemId, string variant)> Selection()
        {
            var sel = new List<(string, string)>
            {
                ("lpc/body", skinVariant),
                (HeadId, skinVariant),
            };
            if (!string.IsNullOrEmpty(eyesId)) sel.Add((eyesId, eyesVariant));
            if (!string.IsNullOrEmpty(hairId)) sel.Add((hairId, hairVariant));
            if (!string.IsNullOrEmpty(shirtId)) sel.Add((shirtId, shirtVariant));
            if (!string.IsNullOrEmpty(pantsId)) sel.Add((pantsId, pantsVariant));
            if (!string.IsNullOrEmpty(shoesId)) sel.Add((shoesId, shoesVariant));
            sel.AddRange(equipped.Where(x => !string.IsNullOrEmpty(x.itemId))
                                 .Select(x => (x.itemId, x.variant)));
            return sel;
        }

        // ----- equipped gear API (player + NPC) -----------------------------

        public bool HasEquipped(string itemId) => equipped.Any(x => x.itemId == itemId);

        public void Equip(string itemId, string variant, string equipSlot)
        {
            // one item per equipSlot (e.g. one weapon, one cape)
            if (!string.IsNullOrEmpty(equipSlot))
                equipped.RemoveAll(x => x.equipSlot == equipSlot);
            var existing = equipped.FirstOrDefault(x => x.itemId == itemId);
            if (existing != null) { existing.variant = variant; existing.equipSlot = equipSlot; }
            else equipped.Add(new GearEntry { itemId = itemId, variant = variant, equipSlot = equipSlot });
        }

        public void Unequip(string itemId) => equipped.RemoveAll(x => x.itemId == itemId);

        public void UnequipSlot(string equipSlot) => equipped.RemoveAll(x => x.equipSlot == equipSlot);
    }

    // Retained for save-migration only (legacy v2 accessories list).
    [Serializable]
    public class AccessoryEntry
    {
        public string itemId;
        public string variant;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// The player's chosen look (v2, LPC wardrobe): body type + skin tone +
    /// one item/variant per clothing slot + equipped accessory drops.
    /// Persisted as JSON in PlayerPrefs (see binder/Codex handoff for moving it
    /// into the Foundation save pipeline later).
    /// </summary>
    [Serializable]
    public class AccessoryEntry
    {
        public string itemId;   // e.g. "lpc/cape_solid"
        public string variant;  // e.g. "red"
    }

    [Serializable]
    public class LayeredAppearance
    {
        public string bodyType = "male";
        public string skinVariant = "light";   // applied to body, head + matchBodyColor items
        public string eyesVariant = "blue";
        public string hairId = "lpc/hair_bangs";
        public string hairVariant = "dark_brown";
        public string shirtId = "lpc/torso_clothes_shortsleeve";
        public string shirtVariant = "blue";
        public string pantsId = "lpc/legs_pants";
        public string pantsVariant = "charcoal";
        public string shoesId = "lpc/feet_shoes";
        public string shoesVariant = "brown";
        public List<AccessoryEntry> accessories = new();

        public const string PrefsKey = "litiso.layered_appearance.v2";

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

        /// <summary>Clamp every reference to something that exists in the catalog.</summary>
        public static LayeredAppearance Sanitize(LayeredAppearance a)
        {
            var cat = CharacterLayerCatalog.Instance;
            if (cat.items.Length == 0) return a;
            if (Array.IndexOf(cat.bodyTypes, a.bodyType) < 0) a.bodyType = cat.bodyTypes[0];

            var body = cat.Find("lpc/body");
            if (body != null) a.skinVariant = body.SafeVariant(a.skinVariant);
            var eyes = cat.Find("lpc/eyes");
            if (eyes != null) a.eyesVariant = eyes.SafeVariant(a.eyesVariant);

            FixSlot(cat, "hair", ref a.hairId, ref a.hairVariant, allowNone: true);
            FixSlot(cat, "shirt", ref a.shirtId, ref a.shirtVariant, allowNone: false);
            FixSlot(cat, "pants", ref a.pantsId, ref a.pantsVariant, allowNone: false);
            FixSlot(cat, "shoes", ref a.shoesId, ref a.shoesVariant, allowNone: false);
            a.accessories.RemoveAll(x => cat.Find(x.itemId) == null);
            foreach (var x in a.accessories)
                x.variant = cat.Find(x.itemId).SafeVariant(x.variant);
            return a;
        }

        static void FixSlot(CharacterLayerCatalog cat, string slot, ref string id, ref string variant, bool allowNone)
        {
            if (allowNone && string.IsNullOrEmpty(id)) return;
            var item = cat.Find(id) ?? cat.First(slot);
            if (item == null) { id = ""; return; }
            id = item.id;
            variant = item.SafeVariant(variant);
        }

        public static LayeredAppearance Random(CharacterLayerCatalog cat)
        {
            string PickVariant(ItemDef i) => i.variants[UnityEngine.Random.Range(0, i.variants.Length)];
            ItemDef PickItem(string slot)
            {
                var opts = cat.Slot(slot);
                return opts.Count == 0 ? null : opts[UnityEngine.Random.Range(0, opts.Count)];
            }
            var a = new LayeredAppearance
            {
                bodyType = cat.bodyTypes[UnityEngine.Random.Range(0, cat.bodyTypes.Length)],
            };
            var body = cat.Find("lpc/body");
            // bias to the human-ish first 7 skin variants when available
            if (body != null)
                a.skinVariant = body.variants[UnityEngine.Random.Range(0, Mathf.Min(7, body.variants.Length))];
            var eyes = cat.Find("lpc/eyes");
            if (eyes != null) a.eyesVariant = PickVariant(eyes);
            var hair = PickItem("hair"); if (hair != null) { a.hairId = hair.id; a.hairVariant = PickVariant(hair); }
            var shirt = PickItem("shirt"); if (shirt != null) { a.shirtId = shirt.id; a.shirtVariant = PickVariant(shirt); }
            var pants = PickItem("pants"); if (pants != null) { a.pantsId = pants.id; a.pantsVariant = PickVariant(pants); }
            var shoes = PickItem("shoes"); if (shoes != null) { a.shoesId = shoes.id; a.shoesVariant = PickVariant(shoes); }
            return a;
        }

        /// <summary>All (itemId, variant) pairs to composite.</summary>
        public List<(string itemId, string variant)> Selection()
        {
            var sel = new List<(string, string)>
            {
                ("lpc/body", skinVariant),
                ("lpc/head", skinVariant),
                ("lpc/eyes", eyesVariant),
            };
            if (!string.IsNullOrEmpty(hairId)) sel.Add((hairId, hairVariant));
            if (!string.IsNullOrEmpty(shirtId)) sel.Add((shirtId, shirtVariant));
            if (!string.IsNullOrEmpty(pantsId)) sel.Add((pantsId, pantsVariant));
            if (!string.IsNullOrEmpty(shoesId)) sel.Add((shoesId, shoesVariant));
            sel.AddRange(accessories.Where(x => !string.IsNullOrEmpty(x.itemId))
                                    .Select(x => (x.itemId, x.variant)));
            return sel;
        }

        public bool HasAccessory(string itemId) => accessories.Any(x => x.itemId == itemId);

        public void EquipAccessory(string itemId, string variant)
        {
            var existing = accessories.FirstOrDefault(x => x.itemId == itemId);
            if (existing != null) existing.variant = variant;
            else accessories.Add(new AccessoryEntry { itemId = itemId, variant = variant });
        }

        public void UnequipAccessory(string itemId) =>
            accessories.RemoveAll(x => x.itemId == itemId);
    }
}

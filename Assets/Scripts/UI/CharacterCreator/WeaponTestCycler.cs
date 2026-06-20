using System.Collections.Generic;
using System.Linq;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    // Debug harness for reviewing weapon attack animations. Self-hosts on a persistent
    // GameObject via FoundationBootstrap.Ready; no scene wiring needed. Cycles every
    // weapon-slot catalog item onto the player's LPC wardrobe. It uses the same
    // CharacterEquipmentVisuals path as real equipping but is independent of the
    // gameplay inventory/loadout. Keys: ] next, [ prev, \ replay attack, P play all.
    public class WeaponTestCycler : MonoBehaviour
    {
        static bool _spawned;

        FoundationBootstrap _bootstrap;
        IsoFoundationPlayer _player;
        LayeredCharacterAnimator _anim;
        CharacterEquipmentVisuals _visuals;
        List<ItemDef> _weapons;
        int _index = -1;
        string _equippedId;
        bool _hintShown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => FoundationBootstrap.Ready += OnReady;

        static void OnReady(FoundationBootstrap bootstrap)
        {
            if (_spawned) return;
            _spawned = true;
            var go = new GameObject("WeaponTestCycler");
            DontDestroyOnLoad(go);
            go.AddComponent<WeaponTestCycler>()._bootstrap = bootstrap;
        }

        bool Resolve()
        {
            if (_visuals != null && _anim != null) return true;
            _player = _bootstrap != null ? _bootstrap.Player : null;
            if (_player == null) return false;
            var go = _player.gameObject;
            _anim = go.GetComponent<LayeredCharacterAnimator>();
            if (_anim == null) return false;
            _visuals = go.GetComponent<CharacterEquipmentVisuals>()
                       ?? go.AddComponent<CharacterEquipmentVisuals>();
            return true;
        }

        void EnsureList()
        {
            if (_weapons != null) return;
            var cat = CharacterLayerCatalog.Instance;
            _weapons = cat != null && cat.items != null
                ? cat.items.Where(i => i != null && i.equipSlot == "weapon").OrderBy(i => i.id).ToList()
                : new List<ItemDef>();
        }

        void Update()
        {
            if (!Resolve()) return;
            EnsureList();
            if (_weapons.Count == 0) return;

            if (!_hintShown)
            {
                _hintShown = true;
                Toast($"Weapon test: ] / [ cycle  ({_weapons.Count} weapons), \\ replay, P play all");
            }

            if (Input.GetKeyDown(KeyCode.RightBracket)) Cycle(+1);
            else if (Input.GetKeyDown(KeyCode.LeftBracket)) Cycle(-1);
            else if (Input.GetKeyDown(KeyCode.Backslash)) PlayPrimary();
            else if (Input.GetKeyDown(KeyCode.P)) PlayAll();
        }

        void Cycle(int dir)
        {
            if (!string.IsNullOrEmpty(_equippedId)) _visuals.Unequip(_equippedId);
            _index = ((_index + dir) % _weapons.Count + _weapons.Count) % _weapons.Count;
            var w = _weapons[_index];
            _visuals.Equip(w.id);
            _equippedId = w.id;
            Toast($"{_index + 1}/{_weapons.Count}  {(string.IsNullOrEmpty(w.displayName) ? w.id : w.displayName)}");
            PlayPrimary();
        }

        string PrimaryAnim()
        {
            var w = _equippedId ?? "";
            if (w.Contains("bow") || w.Contains("ranged")) return "shoot";
            if (w.Contains("polearm") || w.Contains("spear") || w.Contains("trident") ||
                w.Contains("halberd") || w.Contains("whip") || w.Contains("rod") || w.Contains("magic"))
                return "thrust";
            return "slash";
        }

        void PlayPrimary()
        {
            if (_anim != null) _anim.PlayOneShot(PrimaryAnim());
        }

        void PlayAll() => PlaySeq(new[] { "slash", "thrust", "shoot", "spellcast" }, 0);

        void PlaySeq(string[] seq, int i)
        {
            if (_anim == null || i >= seq.Length) return;
            _anim.PlayOneShot(seq[i], () => PlaySeq(seq, i + 1));
        }

        void Toast(string msg)
        {
            Debug.Log($"[WeaponTest] {msg}");
            if (_player != null)
                FloatingText.Spawn(_player.transform.position + Vector3.up * 0.8f, msg, new Color(1f, 0.95f, 0.6f));
        }
    }
}

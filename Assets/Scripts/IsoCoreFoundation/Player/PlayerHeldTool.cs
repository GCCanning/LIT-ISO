using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Small child sprite that mirrors the selected hotbar tool and swings on use.</summary>
    public sealed class PlayerHeldTool : MonoBehaviour
    {
        IsoFoundationPlayer _player;
        Inventory _inventory;
        Hotbar _hotbar;
        FoundationContent _content;
        SpriteRenderer _sr;
        string _shownItemId;
        float _swingTimer;

        static readonly Dictionary<string, Sprite> _spriteCache = new();
        const float SwingDuration = 0.18f;

        public void Init(IsoFoundationPlayer player, Inventory inventory, Hotbar hotbar, FoundationContent content)
        {
            _player = player;
            _inventory = inventory;
            _hotbar = hotbar;
            _content = content;

            var go = new GameObject("HeldToolVisual");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteAmbient.Material;
            _sr.enabled = false;
            Refresh();
        }

        public void Swing()
        {
            if (_sr == null || !_sr.enabled)
                return;
            _swingTimer = SwingDuration;
        }

        void Update()
        {
            Refresh();
            Animate();
        }

        void Refresh()
        {
            if (_sr == null || _hotbar == null || _content == null)
                return;

            var stack = _hotbar.SelectedStack;
            var def = stack.IsEmpty ? null : _content.Items.Get(stack.itemId);
            if (def == null || def.category != ItemCategory.Tool)
            {
                _sr.enabled = false;
                _shownItemId = "";
                return;
            }

            if (_shownItemId != def.id)
            {
                _shownItemId = def.id;
                _sr.sprite = ResolveToolSprite(def);
                _sr.color = _sr.sprite != null ? Color.white : def.color;
            }

            _sr.enabled = _sr.sprite != null;
            if (_player != null)
            {
                var c = _player.CurrentCell;
                int h = _player.Height;
                _sr.sortingOrder = IsoGrid.SortingOrder(c.x, c.y, h, IsoGrid.LayerProp) + 3;
            }
        }

        void Animate()
        {
            if (_sr == null || !_sr.enabled)
                return;

            if (_swingTimer > 0f)
                _swingTimer = Mathf.Max(0f, _swingTimer - Time.deltaTime);

            float t = _swingTimer > 0f ? 1f - _swingTimer / SwingDuration : 0f;
            float eased = t <= 0f ? 0f : Mathf.Sin(t * Mathf.PI);
            var tr = _sr.transform;
            tr.localPosition = new Vector3(0.22f + eased * 0.08f, 0.22f - eased * 0.04f, 0f);
            tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-34f, 44f, t));
            tr.localScale = Vector3.one * (0.72f + eased * 0.06f);
        }

        Sprite ResolveToolSprite(ItemDefinition def)
        {
            if (def == null)
                return null;

            if (def.Icon != null)
                return def.Icon;

            if (_spriteCache.TryGetValue(def.id, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>("Items/" + def.id);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>("Items/" + def.id);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.15f), 16f);
            }

            if (sprite == null)
                sprite = PlaceholderArt.Box(def.color, 0.22f, 0.46f);

            _spriteCache[def.id] = sprite;
            return sprite;
        }
    }
}

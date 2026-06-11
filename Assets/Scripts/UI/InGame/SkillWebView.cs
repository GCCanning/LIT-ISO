using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    // ------------------------------------------------------------------------
    // Skill Web view-model contract (owner-approved progression rework,
    // 2026-06-10). A FoundationSkillWebAdapter binds this to the Foundation
    // runtime once Codex lands FoundationSkillWeb; until then the placeholder
    // below keeps the tab alive with demo data.
    // ------------------------------------------------------------------------

    public enum SkillWebNodeState { Locked, Allocatable, Owned, Gated }

    public struct SkillWebNodeData
    {
        public string id;
        public string name;
        public string effect;
        public int spoke;        // 0..6, -1 = center
        public int ring;         // 0 = innermost, -1 = center
        public bool keystone;
        public SkillWebNodeState state;
    }

    public struct SkillWebEdgeData { public string a, b; }

    public interface ISkillWebViewModel
    {
        int Points { get; }
        string StatusLine { get; }   // e.g. "Trial in progress — points banked" / "Iron Warden, Adept"
        int NodeCount { get; }
        SkillWebNodeData GetNode(int index);
        int EdgeCount { get; }
        SkillWebEdgeData GetEdge(int index);
        bool Allocate(string nodeId);
        event Action Changed;
    }

    /// <summary>
    /// Demo web: center + 7 spokes x 4 rings with cross-links at ring 2.
    /// Spokes follow the concept doc (Wilds/Hearth/Maker/Deep/Arcane/Folk/Blade).
    /// </summary>
    public sealed class PlaceholderSkillWebViewModel : ISkillWebViewModel
    {
        public static readonly string[] Spokes =
            { "Wilds", "Hearth", "Maker", "Deep", "Arcane", "Folk", "Blade" };

        static readonly string[][] NodeNames =
        {
            new[]{ "Forager's Eye", "Light Step", "Beastwise", "Warden of Trails" },
            new[]{ "Hearth Spice", "Green Thumb", "Warding Circle", "Keeper's Flame" },
            new[]{ "Steady Hands", "Timberwright", "Masterwork", "Architect" },
            new[]{ "Pick Sense", "Stonebreaker", "Deep Pockets", "Heart of the Mountain" },
            new[]{ "Mana Flow", "Ember Focus", "Twin Cast", "Archon's Web" },
            new[]{ "Bargainer", "Lorekeeper", "Guild Favor", "Voice of the Vale" },
            new[]{ "Steady Strike+", "Guard Step+", "Riposte", "Lanternblade" },
        };

        readonly List<SkillWebNodeData> _nodes = new List<SkillWebNodeData>();
        readonly List<SkillWebEdgeData> _edges = new List<SkillWebEdgeData>();
        readonly HashSet<string> _owned = new HashSet<string> { "core" };
        int _points = 6;

        public PlaceholderSkillWebViewModel()
        {
            _nodes.Add(new SkillWebNodeData
            {
                id = "core", name = "Unclassed Core", spoke = -1, ring = -1,
                effect = "Your trial-forged self. Class assignment lights a home spoke.",
            });
            for (int s = 0; s < Spokes.Length; s++)
            {
                for (int r = 0; r < 4; r++)
                {
                    string id = Spokes[s] + r;
                    _nodes.Add(new SkillWebNodeData
                    {
                        id = id, name = NodeNames[s][r], spoke = s, ring = r,
                        keystone = r == 3,
                        effect = (r == 3 ? "Keystone of the " : "A step along the ")
                                 + Spokes[s] + " path. (Demo data — runtime web pending.)",
                    });
                    _edges.Add(new SkillWebEdgeData { a = r == 0 ? "core" : Spokes[s] + (r - 1), b = id });
                }
            }
            for (int s = 0; s < Spokes.Length; s++)
                _edges.Add(new SkillWebEdgeData { a = Spokes[s] + "2", b = Spokes[(s + 1) % Spokes.Length] + "2" });
        }

        public int Points => _points;
        public string StatusLine => "Demo web — Foundation runtime pending. Points: banked trial levels.";
        public int NodeCount => _nodes.Count;
        public int EdgeCount => _edges.Count;
        public SkillWebEdgeData GetEdge(int index) => _edges[index];
        public event Action Changed;

        public SkillWebNodeData GetNode(int index)
        {
            var n = _nodes[index];
            n.state = _owned.Contains(n.id) ? SkillWebNodeState.Owned
                : IsAdjacentToOwned(n.id) && _points > 0 ? SkillWebNodeState.Allocatable
                : SkillWebNodeState.Locked;
            return n;
        }

        bool IsAdjacentToOwned(string id)
        {
            for (int i = 0; i < _edges.Count; i++)
            {
                var e = _edges[i];
                if ((e.a == id && _owned.Contains(e.b)) || (e.b == id && _owned.Contains(e.a)))
                    return true;
            }
            return false;
        }

        public bool Allocate(string nodeId)
        {
            if (_points <= 0 || _owned.Contains(nodeId) || !IsAdjacentToOwned(nodeId))
                return false;
            _owned.Add(nodeId);
            _points--;
            Changed?.Invoke();
            return true;
        }
    }

    /// <summary>
    /// Draws the radial skill web into a panel body. Stateless except for the
    /// selected node id (kept across refreshes). Click selects; the info strip's
    /// Allocate button commits, so misclicks never spend points.
    /// </summary>
    public static class SkillWebDrawer
    {
        static string s_selectedId;
        static Sprite s_circle;

        static readonly float[] RingRadius = { 86f, 148f, 208f, 268f };

        public static void Draw(RectTransform body, ISkillWebViewModel vm, Action refresh)
        {
            if (vm == null)
            {
                var none = UiBuilder.NewText(body, "WebEmpty", "Skill web unavailable", 18,
                    TextAnchor.MiddleCenter, UiBuilder.MutedCol);
                UiBuilder.Stretch(none.rectTransform);
                return;
            }

            var area = UiBuilder.NewRect("WebArea", body);
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 1f);
            area.offsetMin = new Vector2(0f, 86f);
            area.offsetMax = new Vector2(0f, -36f);

            var header = UiBuilder.NewText(body, "WebHeader",
                $"Skill points: {vm.Points}    |    {vm.StatusLine}", 15,
                TextAnchor.UpperCenter, UiBuilder.MutedCol);
            var headerRt = header.rectTransform;
            headerRt.anchorMin = new Vector2(0f, 1f); headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -6f);
            headerRt.sizeDelta = new Vector2(0f, 26f);

            // node positions (centered in the area, scaled to fit)
            var positions = new Dictionary<string, Vector2>();
            int count = vm.NodeCount;
            var nodes = new SkillWebNodeData[count];
            for (int i = 0; i < count; i++)
            {
                nodes[i] = vm.GetNode(i);
                positions[nodes[i].id] = PositionFor(nodes[i]);
            }

            // edges first (under nodes)
            for (int i = 0; i < vm.EdgeCount; i++)
            {
                var e = vm.GetEdge(i);
                if (!positions.TryGetValue(e.a, out var pa) || !positions.TryGetValue(e.b, out var pb))
                    continue;
                bool lit = StateOf(nodes, e.a) == SkillWebNodeState.Owned
                        && StateOf(nodes, e.b) == SkillWebNodeState.Owned;
                var line = UiBuilder.NewImage(area, "Edge", null,
                    lit ? new Color(0.22f, 0.72f, 0.55f, 0.95f) : new Color(0.45f, 0.46f, 0.50f, 0.30f));
                line.raycastTarget = false;
                var lrt = line.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                Vector2 mid = (pa + pb) * 0.5f, d = pb - pa;
                lrt.anchoredPosition = mid;
                lrt.sizeDelta = new Vector2(d.magnitude, lit ? 3f : 1.5f);
                lrt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }

            for (int i = 0; i < count; i++)
                BuildNode(area, nodes[i], positions[nodes[i].id], vm, refresh);

            BuildInfoStrip(body, nodes, vm, refresh);
        }

        static SkillWebNodeState StateOf(SkillWebNodeData[] nodes, string id)
        {
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].id == id) return nodes[i].state;
            return SkillWebNodeState.Locked;
        }

        static Vector2 PositionFor(SkillWebNodeData n)
        {
            if (n.spoke < 0) return Vector2.zero;
            float ang = Mathf.PI / 2f - n.spoke * (2f * Mathf.PI / 7f);
            float r = RingRadius[Mathf.Clamp(n.ring, 0, RingRadius.Length - 1)];
            return new Vector2(r * Mathf.Cos(ang), r * Mathf.Sin(ang));
        }

        static void BuildNode(RectTransform area, SkillWebNodeData n, Vector2 pos,
            ISkillWebViewModel vm, Action refresh)
        {
            float size = n.spoke < 0 ? 46f : n.keystone ? 38f : n.ring == 2 ? 30f : 22f;
            Color col = n.state switch
            {
                SkillWebNodeState.Owned       => new Color(0.18f, 0.72f, 0.55f, 1f),
                SkillWebNodeState.Allocatable => new Color(0.95f, 0.72f, 0.25f, 1f),
                SkillWebNodeState.Gated       => new Color(0.55f, 0.40f, 0.80f, 0.55f),
                _                             => new Color(0.30f, 0.31f, 0.36f, 0.45f),
            };

            var img = UiBuilder.NewImage(area, "Node_" + n.id, CircleSprite(), col);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);

            bool selected = n.id == s_selectedId;
            if (selected || n.state == SkillWebNodeState.Allocatable)
            {
                var halo = img.gameObject.AddComponent<Outline>();
                halo.effectColor = selected ? Color.white : new Color(0.95f, 0.72f, 0.25f, 0.8f);
                halo.effectDistance = new Vector2(2f, -2f);
            }

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            string id = n.id;
            btn.onClick.AddListener(() => { s_selectedId = id; refresh?.Invoke(); });

            if (n.keystone || n.spoke < 0)
            {
                var label = UiBuilder.NewText(area, "NodeLabel_" + n.id, n.name, 12,
                    TextAnchor.MiddleCenter, UiBuilder.MutedCol);
                label.raycastTarget = false;
                var lrt = label.rectTransform;
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.anchoredPosition = pos + new Vector2(0f, size * 0.5f + 12f);
                lrt.sizeDelta = new Vector2(170f, 18f);
            }
        }

        static void BuildInfoStrip(RectTransform body, SkillWebNodeData[] nodes,
            ISkillWebViewModel vm, Action refresh)
        {
            var strip = UiBuilder.NewPanel(body, "WebInfo", "system_row", UiBuilder.SlotBg);
            var srt = strip.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(0.5f, 0f);
            srt.anchoredPosition = new Vector2(0f, 8f);
            srt.sizeDelta = new Vector2(-24f, 70f);

            SkillWebNodeData sel = default;
            bool found = false;
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].id == s_selectedId) { sel = nodes[i]; found = true; break; }
            if (!found) s_selectedId = null;   // clear stale selection from a previous world

            string text = !found
                ? "Select a node. Gold nodes are within reach; allocation is confirmed here."
                : $"{sel.name}{(sel.keystone ? "  [Keystone]" : "")}\n{sel.effect}";
            var info = UiBuilder.NewText(strip.transform, "InfoText", text, 14,
                TextAnchor.MiddleLeft, UiBuilder.TextCol);
            var irt = info.rectTransform;
            irt.anchorMin = new Vector2(0f, 0f); irt.anchorMax = new Vector2(1f, 1f);
            irt.offsetMin = new Vector2(14f, 6f); irt.offsetMax = new Vector2(-150f, -6f);

            if (found && sel.state == SkillWebNodeState.Allocatable)
            {
                var btnBg = UiBuilder.NewPanel(strip.transform, "AllocBtn", "button",
                    new Color(0.95f, 0.72f, 0.25f, 0.92f));
                var brt = btnBg.rectTransform;
                brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.anchoredPosition = new Vector2(-12f, 0f);
                brt.sizeDelta = new Vector2(124f, 40f);
                var bl = UiBuilder.NewText(btnBg.transform, "AllocLabel", "Allocate", 15,
                    TextAnchor.MiddleCenter, new Color(0.12f, 0.10f, 0.04f, 1f));
                UiBuilder.Stretch(bl.rectTransform);
                bl.raycastTarget = false;
                var b = btnBg.gameObject.AddComponent<Button>();
                b.targetGraphic = btnBg;
                string id = sel.id;
                b.onClick.AddListener(() => { vm.Allocate(id); });
            }
        }

        static Sprite CircleSprite()
        {
            if (s_circle != null) return s_circle;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            float c = (S - 1) * 0.5f, rOut = c, rIn = rOut - 1.5f;
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(rOut - d + 0.5f);
                px[y * S + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            s_circle = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 64f);
            s_circle.hideFlags = HideFlags.HideAndDontSave;
            return s_circle;
        }
    }
}

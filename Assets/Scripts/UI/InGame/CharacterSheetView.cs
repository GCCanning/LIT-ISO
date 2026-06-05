using System;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    public struct CharacterStats { public int str, dex, intel, vit, def, luck; }

    /// <summary>
    /// Model the LitRPG character/system page renders from. Foundation-free.
    /// HP/MP/XP/Level + STR/DEX/INT/VIT/DEF/LUCK + Class + Title. Codex's eventual
    /// stats source binds via an adapter that implements this.
    /// </summary>
    public interface ICharacterSheetViewModel
    {
        string CharacterName { get; }
        string ClassName { get; }
        string TitleName { get; }
        Sprite Portrait { get; }
        int Level { get; }
        float Health01 { get; }
        float Mana01   { get; }
        float Xp01     { get; }
        CharacterStats Stats { get; }
        event Action Changed;
    }

    public sealed class PlaceholderCharacterSheetViewModel : ICharacterSheetViewModel
    {
        public string CharacterName => "Adventurer";
        public string ClassName => "Unassigned";
        public string TitleName => "Newcomer";
        public Sprite Portrait => Resources.Load<Sprite>("UI/InGame/system_portrait");
        public int Level => 1;
        public float Health01 => 0.85f;
        public float Mana01   => 0.65f;
        public float Xp01     => 0.30f;
        public CharacterStats Stats => new CharacterStats { str=5, dex=5, intel=5, vit=5, def=5, luck=5 };
        public event Action Changed;
        public void Raise() => Changed?.Invoke();
    }

    /// <summary>
    /// Character / "System" page. Single-page layout: portrait+identity top-left,
    /// vitals (HP/MP/XP+Level) below them, 6-stat grid (STR/DEX/INT/VIT/DEF/LUCK)
    /// right side. Skinnable: system_panel, system_portrait, system_row, system_divider,
    /// plus stat icon_<stat>. Esc/X closes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSheetView : MonoBehaviour
    {
        ICharacterSheetViewModel _model;
        Canvas _canvas;
        GameObject _root;
        Text _nameT, _classT, _titleT, _levelT;
        Image _hpFill, _mpFill, _xpFill;
        Text[] _statVals;
        static readonly string[] StatIds = { "str", "dex", "int", "vit", "def", "luck" };
        static readonly string[] StatLabels = { "STR", "DEX", "INT", "VIT", "DEF", "LUCK" };

        public bool IsOpen => _root != null && _root.activeSelf;
        public event Action Closed;

        public void Init(ICharacterSheetViewModel model)
        {
            Unsubscribe(); _model = model; Build(); Subscribe(); Refresh(); Hide();
        }
        void OnDestroy() => Unsubscribe();
        void Subscribe()   { if (_model != null) _model.Changed += Refresh; }
        void Unsubscribe() { if (_model != null) _model.Changed -= Refresh; }

        public void Show() { if (_root != null) { _root.SetActive(true); Refresh(); } }
        public void Hide() { if (_root != null) _root.SetActive(false); Closed?.Invoke(); }
        public void Toggle() { if (IsOpen) Hide(); else Show(); }

        void Build()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = UiBuilder.NewCanvas(transform, "CharacterCanvas", 200);
            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(_canvas.transform, false);
            UiBuilder.Stretch(_root.GetComponent<RectTransform>());

            var scrim = UiBuilder.NewScrim(_root.transform);
            var sb = scrim.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None;
            sb.onClick.AddListener(Hide);

            var panel = UiBuilder.NewPanel(_root.transform, "SysPanel", "system_panel", UiBuilder.PanelBg);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(960f, 640f);

            var title = UiBuilder.NewText(panel.transform, "Title", "Status", 26, TextAnchor.UpperCenter);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -16f);
            tr.sizeDelta = new Vector2(0f, 36f);

            var close = UiBuilder.NewButton(panel.transform, "Close", "btn_close", "X", 18);
            close.onClick.AddListener(Hide);
            var ccr = close.GetComponent<RectTransform>();
            ccr.anchorMin = ccr.anchorMax = new Vector2(1f, 1f);
            ccr.pivot = new Vector2(1f, 1f);
            ccr.anchoredPosition = new Vector2(-12f, -12f);
            ccr.sizeDelta = new Vector2(40f, 40f);

            // Left column: portrait + identity + vitals.
            BuildLeftColumn(panel.transform);
            // Right column: 6 stats grid (2 cols × 3 rows).
            BuildStatsGrid(panel.transform);
        }

        void BuildLeftColumn(Transform parent)
        {
            float x = -480f + 32f;  // panel half-width minus padding
            float yTop = 320f - 60f - 32f;

            // Portrait frame.
            var port = UiBuilder.NewPanel(parent, "Portrait", "system_portrait", UiBuilder.SlotBg);
            var pr = port.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0f, 1f);
            pr.anchoredPosition = new Vector2(x, yTop);
            pr.sizeDelta = new Vector2(160f, 160f);

            // Identity text block (right of portrait).
            _nameT  = UiBuilder.NewText(parent, "Name",  "—", 22, TextAnchor.UpperLeft);
            _classT = UiBuilder.NewText(parent, "Class", "—", 16, TextAnchor.UpperLeft, UiBuilder.MutedCol);
            _titleT = UiBuilder.NewText(parent, "Title", "—", 16, TextAnchor.UpperLeft, UiBuilder.MutedCol);
            _levelT = UiBuilder.NewText(parent, "Level", "Lv 1", 16, TextAnchor.UpperLeft);
            PlaceLine(_nameT,  x + 176f, yTop -   4f, 280f, 30f);
            PlaceLine(_classT, x + 176f, yTop -  38f, 280f, 22f);
            PlaceLine(_titleT, x + 176f, yTop -  62f, 280f, 22f);
            PlaceLine(_levelT, x + 176f, yTop -  90f, 280f, 22f);

            // Vitals bars (HP/MP/XP).
            float vy = yTop - 192f;
            _hpFill = MakeBar(parent, "HP", x, vy,        new Color(0.80f,0.25f,0.25f,1f), "bar_health_fill");
            _mpFill = MakeBar(parent, "MP", x, vy - 32f,  new Color(0.30f,0.55f,0.90f,1f), "bar_mana_fill");
            _xpFill = MakeBar(parent, "XP", x, vy - 64f,  new Color(0.85f,0.70f,0.30f,1f), "bar_xp_fill");
        }

        void BuildStatsGrid(Transform parent)
        {
            float x0 = 16f;     // right of panel center
            float y0 = 220f;
            const float ROW = 60f, COL = 200f;
            _statVals = new Text[6];
            for (int i = 0; i < 6; i++)
            {
                int r = i / 2, c = i % 2;
                float x = x0 + c * COL;
                float y = y0 - r * ROW;

                var row = UiBuilder.NewPanel(parent, "Stat_" + StatIds[i], "system_row", UiBuilder.SlotBg);
                var rr = row.rectTransform;
                rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
                rr.pivot = new Vector2(0f, 0.5f);
                rr.anchoredPosition = new Vector2(x, y);
                rr.sizeDelta = new Vector2(180f, 48f);

                // Icon.
                var icon = UiBuilder.NewImage(row.transform, "Icon", UiBuilder.Spr("icon_" + StatIds[i]), Color.white);
                icon.preserveAspect = true; icon.raycastTarget = false;
                var ir = icon.rectTransform;
                ir.anchorMin = new Vector2(0f, 0.5f); ir.anchorMax = new Vector2(0f, 0.5f);
                ir.pivot = new Vector2(0f, 0.5f);
                ir.anchoredPosition = new Vector2(8f, 0f); ir.sizeDelta = new Vector2(32f, 32f);
                if (icon.sprite == null) icon.enabled = false;

                // Label.
                var lbl = UiBuilder.NewText(row.transform, "Lbl", StatLabels[i], 16, TextAnchor.MiddleLeft, UiBuilder.MutedCol);
                var lr = lbl.rectTransform;
                lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0.55f, 1f);
                lr.offsetMin = new Vector2(48f, 0f); lr.offsetMax = new Vector2(0f, 0f);

                // Value.
                var val = UiBuilder.NewText(row.transform, "Val", "—", 20, TextAnchor.MiddleRight);
                var vr = val.rectTransform;
                vr.anchorMin = new Vector2(0.55f, 0f); vr.anchorMax = new Vector2(1f, 1f);
                vr.offsetMin = new Vector2(0f, 0f); vr.offsetMax = new Vector2(-12f, 0f);
                _statVals[i] = val;
            }
        }

        static void PlaceLine(Text t, float x, float y, float w, float h)
        {
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        Image MakeBar(Transform parent, string name, float x, float y, Color col, string fillSkin)
        {
            var track = UiBuilder.NewPanel(parent, name + "Track", "bar_track", UiBuilder.SlotBg);
            var tr = track.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0f, 0.5f);
            tr.anchoredPosition = new Vector2(x, y);
            tr.sizeDelta = new Vector2(280f, 24f);

            var fillSpr = UiBuilder.Spr(fillSkin);
            var fill = UiBuilder.NewImage(track.transform, name + "Fill", fillSpr, col);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.preserveAspect = fillSpr != null;
            UiBuilder.Stretch(fill.rectTransform, 3f);

            var label = UiBuilder.NewText(track.transform, "Lbl", name, 12, TextAnchor.MiddleLeft);
            var lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMin = new Vector2(8f, 0f); lr.offsetMax = new Vector2(-8f, 0f);
            return fill;
        }

        void Refresh()
        {
            if (_model == null) return;
            if (_nameT != null)  _nameT.text  = _model.CharacterName ?? "—";
            if (_classT != null) _classT.text = "Class: " + (_model.ClassName ?? "—");
            if (_titleT != null) _titleT.text = "Title: " + (_model.TitleName ?? "—");
            if (_levelT != null) _levelT.text = "Lv " + _model.Level;
            if (_hpFill != null) _hpFill.fillAmount = Mathf.Clamp01(_model.Health01);
            if (_mpFill != null) _mpFill.fillAmount = Mathf.Clamp01(_model.Mana01);
            if (_xpFill != null) _xpFill.fillAmount = Mathf.Clamp01(_model.Xp01);
            var s = _model.Stats;
            int[] vals = { s.str, s.dex, s.intel, s.vit, s.def, s.luck };
            if (_statVals != null)
                for (int i = 0; i < _statVals.Length; i++)
                    if (_statVals[i] != null) _statVals[i].text = vals[i].ToString();
        }
    }
}

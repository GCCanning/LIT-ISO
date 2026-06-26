using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Loads the offline-measured sprite trim table (Tools/ScaleAudit -> generated into
    /// Resources/sprite_content_metrics.json) so sprite scaling can size by VISIBLE art,
    /// not raw canvas height. Source PNGs carry 0%-45%+ transparent padding; without this,
    /// identical heightUnits render at different on-screen sizes. Regenerate the table with
    /// Tools/ScaleAudit/audit_sprite_scale.py after adding/changing prop art.
    /// </summary>
    public static class FoundationSpriteMetrics
    {
        [System.Serializable]
        private class Table { public string[] names; public float[] contentFracH; public float[] bottomPadFrac; }

        // value.x = contentFracH (fraction of canvas that is opaque art),
        // value.y = bottomPadFrac (empty fraction below the art; ground-anchor hint).
        private static Dictionary<string, Vector2> _map;

        private static void Ensure()
        {
            if (_map != null) return;
            _map = new Dictionary<string, Vector2>();
            var ta = Resources.Load<TextAsset>("sprite_content_metrics");
            if (ta == null)
            {
                Debug.LogWarning("[SpriteMetrics] Resources/sprite_content_metrics.json missing; scaling uses full canvas.");
                return;
            }
            var t = JsonUtility.FromJson<Table>(ta.text);
            if (t == null || t.names == null) return;
            int n = t.names.Length;
            for (int i = 0; i < n; i++)
            {
                float f = (t.contentFracH != null && i < t.contentFracH.Length) ? t.contentFracH[i] : 1f;
                float b = (t.bottomPadFrac != null && i < t.bottomPadFrac.Length) ? t.bottomPadFrac[i] : 0f;
                if (f <= 0.01f) f = 1f;
                _map[t.names[i]] = new Vector2(f, b);
            }
        }

        /// <summary>Fraction of the sprite canvas that is opaque art (1 = no padding). 1 if unknown.</summary>
        public static float ContentFracH(string spriteName)
        {
            Ensure();
            if (string.IsNullOrEmpty(spriteName)) return 1f;
            return _map.TryGetValue(spriteName, out var v) ? v.x : 1f;
        }

        /// <summary>Empty fraction below the art (ground-anchor hint). 0 if unknown.</summary>
        public static float BottomPadFrac(string spriteName)
        {
            Ensure();
            if (string.IsNullOrEmpty(spriteName)) return 0f;
            return _map.TryGetValue(spriteName, out var v) ? v.y : 0f;
        }
    }
}

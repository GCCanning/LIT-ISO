using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Casts a soft silhouette shadow for a decoration onto the ground, projected away
    /// from the active light (sun by day, moon by night). The shadow is a darkened copy of
    /// the prop's own sprite, anchored at the prop's base, that swings around the base and
    /// stretches as the light gets lower — short at noon, long at dawn/dusk, faint by
    /// moonlight, fading out entirely at the horizon hand-off. Reads DayNightSystem only.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class DecorationShadow : MonoBehaviour
    {
        [Tooltip("Shadow length at the lowest light (multiplier of the sprite height).")]
        public float maxLength = 1.15f;
        [Tooltip("Shadow length at peak light (sun directly overhead).")]
        public float minLength = 0.35f;
        [Tooltip("Horizontal squash applied to the lying-down shadow.")]
        public float widthScale = 0.7f;
        [Tooltip("Darkest shadow opacity (at strong light).")]
        public float maxAlpha = 0.42f;

        SpriteRenderer _src;
        SpriteRenderer _shadow;
        static Material s_shadowMaterial;
        static DayNightSystem s_day;

        static DayNightSystem Day()
        {
            if (s_day != null) return s_day;
            s_day = Object.FindFirstObjectByType<DayNightSystem>();
            return s_day;
        }

        void Start()
        {
            _src = GetComponent<SpriteRenderer>();
            if (_src.sprite == null) { enabled = false; return; }

            var go = new GameObject("Shadow");
            go.transform.SetParent(transform, false); // same base anchor (shared pivot)
            _shadow = go.AddComponent<SpriteRenderer>();
            _shadow.sprite = _src.sprite;              // identical silhouette, base-pivoted
            if (s_shadowMaterial == null)
            {
                var shader = Shader.Find("IsoCore/ProjectedSpriteShadow");
                if (shader != null)
                    s_shadowMaterial = new Material(shader) { name = "ProjectedSpriteShadowShared" };
            }
            if (s_shadowMaterial != null)
                _shadow.sharedMaterial = s_shadowMaterial;
            // Default sorting layer sits entirely in front of the "Ground" layer, so any
            // order here is above every ground tile; one below the prop keeps it beneath
            // the thing that casts it.
            _shadow.sortingLayerID = _src.sortingLayerID;
            _shadow.sortingOrder = _src.sortingOrder - 1;
        }

        void LateUpdate()
        {
            if (_shadow == null) return;
            var d = Day();

            _shadow.sprite = _src.sprite;
            _shadow.flipX = _src.flipX;
            _shadow.flipY = _src.flipY;
            _shadow.sortingLayerID = _src.sortingLayerID;
            _shadow.sortingOrder = _src.sortingOrder - 1;

            Vector2 fall = d != null ? d.ShadowGroundDirection : new Vector2(0.707f, -0.707f);
            float intensity = d != null ? d.LightIntensity : 0.8f;
            float elevation = d != null ? d.CelestialElevation01 : 0.8f;

            // World rotation prevents parent WindSway from rotating the shadow away
            // from the celestial light direction.
            float ang = Mathf.Atan2(-fall.x, fall.y) * Mathf.Rad2Deg;
            _shadow.transform.rotation = Quaternion.Euler(0f, 0f, ang);

            float len = Mathf.Lerp(maxLength, minLength, elevation);
            _shadow.transform.localScale = new Vector3(widthScale, len, 1f);

            Color c = d != null ? d.ShadowColor : new Color(0.06f, 0.06f, 0.09f);
            c.a = maxAlpha * Mathf.Clamp01(intensity * 1.25f); // fades out as the body sets
            _shadow.color = c;
            _shadow.enabled = c.a > 0.02f;
        }
    }
}

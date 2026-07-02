using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Ground ring that makes the campfire's ward radius visible at dusk and night, so
    /// "safe" is something the player can SEE (preparation communicates safety). Owned
    /// by FoundationCampingSystem — one pooled instance, repositioned as the active
    /// camp changes; no per-frame allocations.
    ///
    /// The ring is drawn in world space around the camp prop, matching the planar
    /// distance check FoundationCampingSystem/MobSpawner actually use for the ward, so
    /// the visual is mechanically honest.
    /// </summary>
    public sealed class CampfireWardRing : MonoBehaviour
    {
        const int Segments = 48;

        static readonly Color EmberWarm = new Color(1.00f, 0.62f, 0.24f);
        static readonly Color EmberDeep = new Color(0.95f, 0.38f, 0.12f);

        LineRenderer _line;
        Vector3[] _points;
        float _radius = -1f;
        Vector3 _center;
        float _visibility;   // eased 0..1 so the ring fades in/out with dusk/night
        float _targetVisibility;

        public static CampfireWardRing Create(Transform parent)
        {
            var go = new GameObject("CampfireWardRing");
            go.transform.SetParent(parent, false);
            var ring = go.AddComponent<CampfireWardRing>();
            ring.Build();
            go.SetActive(false);
            return ring;
        }

        void Build()
        {
            _points = new Vector3[Segments];
            _line = gameObject.AddComponent<LineRenderer>();
            _line.loop = true;
            _line.positionCount = Segments;
            _line.useWorldSpace = true;
            _line.startWidth = 0.045f;
            _line.endWidth = 0.045f;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) _line.material = new Material(shader);
            _line.sortingOrder = 8400; // matches the TargetHighlight overlay band
            _line.receiveShadows = false;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Show the ring for a camp. visibility01: 0 = day (hidden) → 1 = deep night.</summary>
        public void Show(Vector3 center, float radius, float visibility01)
        {
            _targetVisibility = Mathf.Clamp01(visibility01);
            if (_targetVisibility <= 0.01f) { Hide(); return; }

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            if (radius != _radius || (center - _center).sqrMagnitude > 0.0004f)
            {
                _radius = radius;
                _center = center;
                for (int i = 0; i < Segments; i++)
                {
                    float a = (i / (float)Segments) * Mathf.PI * 2f;
                    _points[i] = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                }
                _line.SetPositions(_points);
            }
        }

        public void Hide()
        {
            _targetVisibility = 0f;
            if (gameObject.activeSelf && _visibility <= 0.02f)
                gameObject.SetActive(false);
        }

        void Update()
        {
            _visibility = Mathf.MoveTowards(_visibility, _targetVisibility, Time.deltaTime * 1.5f);
            if (_visibility <= 0.02f && _targetVisibility <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            // Gentle ember pulse so the ward reads as alive firelight, not a debug gizmo.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.1f);
            var c = Color.Lerp(EmberDeep, EmberWarm, pulse);
            c.a = (0.28f + 0.22f * pulse) * _visibility;
            _line.startColor = c;
            _line.endColor = c;
        }
    }
}

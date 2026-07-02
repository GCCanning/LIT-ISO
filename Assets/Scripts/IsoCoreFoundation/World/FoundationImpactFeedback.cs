using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Small deterministic camera impulse for confirmed player impacts.</summary>
    public static class FoundationImpactFeedback
    {
        const float Duration = 0.11f;
        const float MaxMagnitude = 0.055f;

        static float _remaining;
        static float _magnitude;
        static float _phase;

        public static void Pulse(float strength)
        {
            _remaining = Mathf.Max(_remaining, Duration);
            _magnitude = Mathf.Max(_magnitude, MaxMagnitude * Mathf.Clamp(strength, 0.25f, 1.5f));
        }

        public static Vector2 Tick(float unscaledDeltaTime)
        {
            if (_remaining <= 0f)
            {
                _magnitude = 0f;
                return Vector2.zero;
            }

            _remaining = Mathf.Max(0f, _remaining - Mathf.Max(0f, unscaledDeltaTime));
            _phase += Mathf.Max(0f, unscaledDeltaTime) * 95f;
            float envelope = _remaining / Duration;
            return new Vector2(
                Mathf.Sin(_phase) * _magnitude * envelope,
                Mathf.Cos(_phase * 1.37f) * _magnitude * 0.55f * envelope);
        }
    }
}

using UnityEngine;

/// <summary>
/// Smooth camera follower with optional features:
///   - Lookahead: camera leads the player's movement direction for a more
///     dynamic feel
///   - SmoothDamp: critically-damped follow for natural acceleration/deceleration
///   - Camera shake: short, decaying noise offset (use Shake() to trigger)
///   - Pixel snapping: aligns camera to pixel grid for crisp pixel art
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Follow Behavior")]
    [Tooltip("Higher = faster catch-up. Used as smooth speed for lerp follow.")]
    public float smoothSpeed = 8f;

    [Tooltip("Use SmoothDamp instead of Lerp for more natural deceleration.")]
    public bool useSmoothDamp = true;

    [Tooltip("SmoothDamp time. Smaller = snappier; larger = floatier.")]
    [Range(0.05f, 1f)]
    public float smoothDampTime = 0.15f;

    [Header("Lookahead")]
    [Tooltip("Camera leads the target by this amount in the movement direction.")]
    [Range(0f, 3f)]
    public float lookaheadDistance = 0.6f;

    [Tooltip("How quickly the lookahead catches up to actual target velocity.")]
    [Range(1f, 15f)]
    public float lookaheadResponseSpeed = 4f;

    [Tooltip("Extra damping applied when the player reverses direction.")]
    [Range(0.05f, 0.6f)]
    public float lookaheadReverseSmoothTime = 0.28f;

    [Header("Camera Shake")]
    [Tooltip("Active when triggered via Shake(). Decays toward zero.")]
    public float shakeAmplitude = 0f;
    public float shakeFrequency = 22f;

    [Header("Pixel Snap")]
    [Tooltip("Snap camera position to pixel grid for crisp pixel art rendering.")]
    public bool snapToPixelGrid = false;
    [Tooltip("Pixels per unit. Set to match your sprites' import setting (typically 16, 32, 64).")]
    public int pixelsPerUnit = 32;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    private Vector3 currentVelocity;
    private Vector3 currentLookahead;
    private Vector3 lookaheadVelocity;
    private Vector3 lastTargetPosition;
    private float shakeTimer;
    private float currentShakeAmplitude;

    private void Start()
    {
        if (target != null)
        {
            lastTargetPosition = target.position;
            transform.position = target.position + offset;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Estimate target velocity for lookahead
        Vector3 targetVelocity = (target.position - lastTargetPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastTargetPosition = target.position;

        Vector3 desiredLookahead = Vector3.zero;
        if (lookaheadDistance > 0f && targetVelocity.sqrMagnitude > 0.01f)
        {
            desiredLookahead = targetVelocity.normalized * lookaheadDistance;
            desiredLookahead.z = 0f;
        }
        bool reversingLookahead = currentLookahead.sqrMagnitude > 0.01f
            && desiredLookahead.sqrMagnitude > 0.01f
            && Vector3.Dot(currentLookahead, desiredLookahead) < -0.05f;
        float lookaheadSmoothTime = reversingLookahead
            ? lookaheadReverseSmoothTime
            : 1f / Mathf.Max(1f, lookaheadResponseSpeed);
        currentLookahead = Vector3.SmoothDamp(
            currentLookahead,
            desiredLookahead,
            ref lookaheadVelocity,
            lookaheadSmoothTime);

        Vector3 desiredPosition = target.position + offset + currentLookahead;

        // Smooth follow
        Vector3 nextPos;
        if (useSmoothDamp)
        {
            nextPos = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref currentVelocity,
                smoothDampTime
            );
        }
        else
        {
            nextPos = Vector3.Lerp(
                transform.position,
                desiredPosition,
                smoothSpeed * Time.deltaTime
            );
        }

        // Camera shake (decays over time)
        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float decay = Mathf.Clamp01(shakeTimer);
            float noiseX = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * 2f;
            nextPos += new Vector3(noiseX, noiseY, 0f) * currentShakeAmplitude * decay;
            if (shakeTimer <= 0f) currentShakeAmplitude = 0f;
        }

        // Pixel snap (only if enabled)
        if (snapToPixelGrid && pixelsPerUnit > 0)
        {
            float unit = 1f / pixelsPerUnit;
            nextPos.x = Mathf.Round(nextPos.x / unit) * unit;
            nextPos.y = Mathf.Round(nextPos.y / unit) * unit;
        }

        transform.position = nextPos;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Trigger a camera shake. Useful for landings, impacts, explosions.
    /// </summary>
    public void Shake(float amplitude = 0.15f, float duration = 0.25f)
    {
        // Stack shakes (take strongest active one)
        if (amplitude > currentShakeAmplitude)
        {
            currentShakeAmplitude = amplitude;
        }
        shakeTimer = Mathf.Max(shakeTimer, duration);
    }

    /// <summary>Snap camera instantly to the target (skip smoothing).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        currentVelocity = Vector3.zero;
        lookaheadVelocity = Vector3.zero;
        currentLookahead = Vector3.zero;
    }
}

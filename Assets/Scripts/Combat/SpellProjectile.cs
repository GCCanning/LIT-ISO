using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Moves a spell projectile toward a target point and deals damage on collision.
/// Spawned by SpellCaster when a Projectile-delivery SpellDefinition is cast.
///
/// The projectile:
///   - Travels in a straight line at definition.projectileSpeed
///   - Despawns when it travels definition.range world units
///   - On hitting an enemy: deals damage, optionally applies a status effect,
///     spawns hitVFX, then destroys itself
///   - On hitting a wall/obstacle collider: destroys itself with hitVFX
///
/// Attach to a prefab. The prefab should also have:
///   - A SpriteRenderer (the projectile visual)
///   - A CircleCollider2D set to isTrigger = true
///   - (Optional) A child TrailRenderer or ParticleSystem for the travel trail
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class SpellProjectile : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Set by SpellCaster after instantiation
    // -------------------------------------------------------------------------

    [HideInInspector] public SpellDefinition definition;
    [HideInInspector] public float            bonusSpellDmg;
    [HideInInspector] public Vector2          direction;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private Rigidbody2D  rb;
    private float        distanceTravelled;
    private AudioSource  audioSrc;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints  = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;

        audioSrc = GetComponent<AudioSource>();
    }

    private void FixedUpdate()
    {
        if (definition == null) { Destroy(gameObject); return; }

        float step = definition.projectileSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + direction * step);
        distanceTravelled += step;

        if (distanceTravelled >= definition.range)
            DestroyProjectile(transform.position, false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (definition == null) return;

        // Check if we hit an enemy
        var enemy = other.GetComponentInParent<SlimeEnemyController>();
        if (enemy != null)
        {
            DealDamage(enemy);
            DestroyProjectile(transform.position, true);
            return;
        }

        // Ignore player colliders and other projectiles
        if (other.GetComponentInParent<IsoPlayerController>() != null) return;
        if (other.GetComponent<SpellProjectile>() != null) return;

        // Hit a wall/obstacle
        DestroyProjectile(transform.position, true);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void DealDamage(SlimeEnemyController enemy)
    {
        bool isCrit = definition.IsCrit();
        float dmg   = definition.RollDamage(bonusSpellDmg);

        enemy.TakeDamage(Mathf.RoundToInt(dmg));

        // Floating damage text
        Color textCol = isCrit
            ? new Color(1f, 0.84f, 0f)   // gold for crit
            : new Color(0.9f, 0.3f, 0.3f); // red for normal

        string label = isCrit ? $"<CRIT> {Mathf.RoundToInt(dmg)}" : Mathf.RoundToInt(dmg).ToString();
        WorldFloatingText.Spawn(transform.position, label, textCol,
                                fontSize: isCrit ? 32 : 26);

        // Status effect
        if (definition.appliesEffect != null
            && UnityEngine.Random.value < definition.statusChance)
        {
            var handler = enemy.GetComponent<StatusEffectHandler>();
            if (handler != null)
                handler.Apply(definition.appliesEffect);
        }

        // Tutorial recording
        TutorialSequence.Instance?.NotifySpellUsed();
    }

    private void DestroyProjectile(Vector3 hitPos, bool spawnHitFX)
    {
        if (spawnHitFX && definition.hitVFX != null)
        {
            var fx = Instantiate(definition.hitVFX, hitPos, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + 0.2f);
        }

        if (spawnHitFX && definition.hitSound != null)
        {
            AudioSource.PlayClipAtPoint(definition.hitSound, hitPos, definition.hitVolume);
        }

        Destroy(gameObject);
    }
}

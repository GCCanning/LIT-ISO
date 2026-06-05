using UnityEngine;

/// <summary>
/// Handles AoE spell delivery — an instant area-of-effect explosion at a world position.
/// Spawned by SpellCaster when an AoE-delivery SpellDefinition is cast.
///
/// On spawn it immediately:
///   1. Plays castVFX / hitVFX at the centre point
///   2. Finds all enemies within definition.aoeRadius via OverlapCircle
///   3. Deals damage + optionally applies a status effect to each
///   4. Destroys itself after the VFX finishes
///
/// Attach to a minimal prefab (empty GameObject + this component).
/// </summary>
public class SpellAoE : MonoBehaviour
{
    // Set by SpellCaster immediately after instantiation
    [HideInInspector] public SpellDefinition definition;
    [HideInInspector] public float            bonusSpellDmg;

    private void Start()
    {
        if (definition == null) { Destroy(gameObject); return; }

        Explode();

        // Self-destruct after VFX
        float lifetime = 0.5f;
        if (definition.hitVFX != null) lifetime = Mathf.Max(lifetime, definition.hitVFX.main.duration + 0.2f);
        Destroy(gameObject, lifetime);
    }

    private void Explode()
    {
        // VFX
        if (definition.hitVFX != null)
        {
            var fx = Instantiate(definition.hitVFX, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + 0.2f);
        }

        if (definition.hitSound != null)
            AudioSource.PlayClipAtPoint(definition.hitSound, transform.position, definition.hitVolume);

        // Hit all enemies in radius
        float radius = definition.aoeRadius;
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);

        int hitCount = 0;
        foreach (var col in hits)
        {
            var enemy = col.GetComponentInParent<SlimeEnemyController>();
            if (enemy == null) continue;

            DealDamage(enemy);
            hitCount++;
        }

        // Floating AoE indicator
        if (hitCount > 0)
        {
            WorldFloatingText.Spawn(transform.position + Vector3.up * 0.5f,
                                    $"AoE  ×{hitCount}",
                                    new Color(1f, 0.5f, 0.1f));
        }
    }

    private void DealDamage(SlimeEnemyController enemy)
    {
        bool  isCrit = definition.IsCrit();
        float dmg    = definition.RollDamage(bonusSpellDmg);

        enemy.TakeDamage(Mathf.RoundToInt(dmg));

        Color textCol = isCrit ? new Color(1f, 0.84f, 0f) : new Color(1f, 0.55f, 0.1f);
        string label  = isCrit ? $"<CRIT> {Mathf.RoundToInt(dmg)}" : Mathf.RoundToInt(dmg).ToString();
        WorldFloatingText.Spawn(enemy.transform.position + Vector3.up * 0.3f,
                                label, textCol, fontSize: isCrit ? 32 : 26);

        // Status effect
        if (definition.appliesEffect != null
            && UnityEngine.Random.value < definition.statusChance)
        {
            var handler = enemy.GetComponent<StatusEffectHandler>();
            handler?.Apply(definition.appliesEffect);
        }

        TutorialSequence.Instance?.NotifySpellUsed();
    }
}

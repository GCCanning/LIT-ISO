using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Manages all active status effects on an entity (player or enemy).
/// Add as a component on any entity that can receive status effects.
///
/// Features:
///   - Apply / remove effects by ID
///   - Automatic duration tracking and expiry
///   - Per-tick damage and healing
///   - Aggregated speed/damage/defence multipliers (for movement and combat code to query)
///   - VFX on apply and expire
///
/// Usage from SpellProjectile / SpellAoE:
///   entity.GetComponent<StatusEffectHandler>()?.Apply(spellDef.appliesEffect);
///
/// Usage from movement code (e.g. IsoPlayerController, SlimeEnemyController):
///   float finalSpeed = baseSpeed * statusHandler.GetSpeedMultiplier();
///
/// To support a new effect: just create a StatusEffectDefinition asset.
/// No code changes required here.
/// </summary>
public class StatusEffectHandler : MonoBehaviour
{
    public static event Action<GameObject, StatusEffectDefinition> OnEffectApplied;
    public static event Action<GameObject, string>                 OnEffectRemoved;

    // -------------------------------------------------------------------------
    // Active effect tracking
    // -------------------------------------------------------------------------

    private class ActiveEffect
    {
        public StatusEffectDefinition definition;
        public float remainingDuration;
        public Coroutine tickRoutine;
        public ParticleSystem activeVFX;
    }

    private readonly Dictionary<string, ActiveEffect> activeEffects = new();

    // -------------------------------------------------------------------------
    // Public API — Apply / Remove / Query
    // -------------------------------------------------------------------------

    /// <summary>Apply a status effect to this entity.</summary>
    public void Apply(StatusEffectDefinition def)
    {
        if (def == null) return;

        if (activeEffects.TryGetValue(def.effectId, out var existing))
        {
            if (def.refreshOnReapply)
                existing.remainingDuration = def.duration;   // Refresh duration
            return;                                           // Don't stack
        }

        var entry = new ActiveEffect
        {
            definition       = def,
            remainingDuration = def.duration,
        };

        // VFX
        if (def.onApplyVFX != null)
        {
            entry.activeVFX = Instantiate(def.onApplyVFX, transform.position, Quaternion.identity, transform);
            var vfxMain = entry.activeVFX.main;
            vfxMain.startColor = def.particleTint;
            entry.activeVFX.Play();
        }

        // Tick damage/heal coroutine
        if (def.tickInterval > 0f && (def.tickDamage > 0f || def.tickHeal > 0f))
            entry.tickRoutine = StartCoroutine(TickRoutine(def));

        activeEffects[def.effectId] = entry;
        StartCoroutine(DurationRoutine(def));

        OnEffectApplied?.Invoke(gameObject, def);
        Debug.Log($"[StatusEffect] {def.displayName} applied to {gameObject.name}");
    }

    /// <summary>Manually remove an effect before it expires.</summary>
    public void Remove(string effectId)
    {
        if (!activeEffects.TryGetValue(effectId, out var entry)) return;

        ExpireEffect(effectId, entry);
    }

    /// <summary>Returns true if the entity currently has the given effect.</summary>
    public bool HasEffect(string effectId) => activeEffects.ContainsKey(effectId);

    /// <summary>Product of all active speed multipliers.</summary>
    public float GetSpeedMultiplier()
    {
        float result = 1f;
        foreach (var e in activeEffects.Values)
            result *= e.definition.speedMultiplier;
        return result;
    }

    /// <summary>Product of all active damage multipliers.</summary>
    public float GetDamageMultiplier()
    {
        float result = 1f;
        foreach (var e in activeEffects.Values)
            result *= e.definition.damageMuliplier;
        return result;
    }

    /// <summary>Returns true if movement is blocked by any active effect.</summary>
    public bool IsMovementPrevented()
    {
        foreach (var e in activeEffects.Values)
            if (e.definition.preventsMovement) return true;
        return false;
    }

    /// <summary>Returns true if casting is blocked by any active effect.</summary>
    public bool IsCastingPrevented()
    {
        foreach (var e in activeEffects.Values)
            if (e.definition.preventsCasting) return true;
        return false;
    }

    /// <summary>Returns a snapshot of all active effect IDs (for UI display).</summary>
    public IEnumerable<string> GetActiveEffectIds() => activeEffects.Keys;

    // -------------------------------------------------------------------------
    // Coroutines
    // -------------------------------------------------------------------------

    private IEnumerator DurationRoutine(StatusEffectDefinition def)
    {
        yield return new WaitForSeconds(def.duration);

        if (activeEffects.TryGetValue(def.effectId, out var entry))
            ExpireEffect(def.effectId, entry);
    }

    private IEnumerator TickRoutine(StatusEffectDefinition def)
    {
        while (activeEffects.ContainsKey(def.effectId))
        {
            yield return new WaitForSeconds(def.tickInterval);

            if (!activeEffects.ContainsKey(def.effectId)) break;

            // Tick damage
            if (def.tickDamage > 0f)
            {
                var health = GetComponent<PlayerHealth>();
                if (health != null)
                    health.TakeDamage(Mathf.RoundToInt(def.tickDamage));

                var enemy = GetComponent<SlimeEnemyController>();
                if (enemy != null)
                    enemy.TakeDamage(Mathf.RoundToInt(def.tickDamage));

                WorldFloatingText.Spawn(transform.position + Vector3.up * 0.2f,
                    $"-{Mathf.RoundToInt(def.tickDamage)}",
                    new Color(0.8f, 0.3f, 0.1f), fontSize: 20);
            }

            // Tick heal
            if (def.tickHeal > 0f)
            {
                var health = GetComponent<PlayerHealth>();
                if (health != null)
                    health.Heal(Mathf.RoundToInt(def.tickHeal));

                WorldFloatingText.Spawn(transform.position + Vector3.up * 0.2f,
                    $"+{Mathf.RoundToInt(def.tickHeal)} HP",
                    new Color(0.3f, 0.9f, 0.4f), fontSize: 20);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void ExpireEffect(string effectId, ActiveEffect entry)
    {
        if (entry.tickRoutine != null)
            StopCoroutine(entry.tickRoutine);

        if (entry.activeVFX != null)
        {
            entry.activeVFX.Stop();
            Destroy(entry.activeVFX.gameObject, 1f);
        }

        if (entry.definition.onExpireVFX != null)
        {
            var fx = Instantiate(entry.definition.onExpireVFX, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + 0.2f);
        }

        activeEffects.Remove(effectId);
        OnEffectRemoved?.Invoke(gameObject, effectId);

        Debug.Log($"[StatusEffect] {effectId} expired on {gameObject.name}");
    }

    private void OnDestroy()
    {
        // Clean up VFX children
        foreach (var entry in activeEffects.Values)
        {
            if (entry.activeVFX != null)
                Destroy(entry.activeVFX.gameObject);
        }
    }
}

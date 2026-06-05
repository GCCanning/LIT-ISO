using System;
using System.Collections;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Handles spell input, mana checks, cooldown tracking, and spell delivery.
/// Add as a component on the Player GameObject alongside PlayerMana and PlayerStats.
///
/// Spell slots 0-3 map to keyboard keys 1-4.
/// Each slot holds a SpellDefinition asset. Slots are populated by ClassSystem on
/// class assignment and by the player equipping spells from their inventory.
///
/// To add a new delivery type:
///   1. Add a value to SpellDefinition.DeliveryType
///   2. Add a case in DeliverSpell()
///   No changes to SpellCaster's input or cooldown logic.
/// </summary>
public class SpellCaster : MonoBehaviour
{
    public static SpellCaster Instance { get; private set; }

    public static event Action<int, float> OnCooldownStarted;  // (slotIndex, cooldownDuration)
    public static event Action<int>        OnSpellCast;        // (slotIndex)

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Spell Slots (0 = key 1, 3 = key 4)")]
    [Tooltip("Drag SpellDefinition assets here. Populated automatically by ClassSystem.")]
    public SpellDefinition[] equippedSpells = new SpellDefinition[4];

    [Header("Aim")]
    [Tooltip("If true, spells are aimed at the mouse cursor (requires Screen Space). " +
             "If false, aimed in the player's facing direction.")]
    public bool aimAtMouse = true;

    [Tooltip("Camera used to convert screen position to world position.")]
    public Camera aimCamera;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private float[] cooldownRemaining  = new float[4];
    private float[] cooldownTotal      = new float[4];
    private bool    isCasting          = false;

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (aimCamera == null) aimCamera = Camera.main;

        // Populate spells from ClassSystem if available
        ClassSystem.OnClassAssigned += OnClassAssigned;
    }

    private void OnClassAssigned(ClassDefinition classDef)
    {
        if (classDef?.startingSkills == null) return;
        for (int i = 0; i < equippedSpells.Length && i < classDef.startingSkills.Length; i++)
            equippedSpells[i] = classDef.startingSkills[i];
    }

    // -------------------------------------------------------------------------
    // Input — called every frame
    // -------------------------------------------------------------------------

    private void Update()
    {
        // Tick cooldowns
        for (int i = 0; i < cooldownRemaining.Length; i++)
        {
            if (cooldownRemaining[i] > 0f)
                cooldownRemaining[i] -= Time.deltaTime;
        }

        if (isCasting) return;

        // Key 1-4 → spell slots 0-3
        if (Input.GetKeyDown(KeyCode.Alpha1)) TryCast(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) TryCast(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) TryCast(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) TryCast(3);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempt to cast the spell in the given slot.
    /// Returns false if: slot is empty, on cooldown, or not enough mana.
    /// </summary>
    public bool TryCast(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= equippedSpells.Length) return false;

        SpellDefinition spell = equippedSpells[slotIndex];
        if (spell == null) return false;

        // Cooldown check
        if (cooldownRemaining[slotIndex] > 0f)
        {
            WorldFloatingText.Spawn(transform.position,
                $"({cooldownRemaining[slotIndex]:F1}s)", Color.yellow, fontSize: 20);
            return false;
        }

        // Mana check
        if (PlayerMana.Instance != null && !PlayerMana.Instance.TrySpend(spell.manaCost))
        {
            WorldFloatingText.Spawn(transform.position, "No Mana!", new Color(0.5f, 0.3f, 1f), fontSize: 22);
            // Flash mana bar red — subscribers can listen to OnSpellCast with -1 as sentinel
            return false;
        }

        // Cast
        StartCoroutine(CastRoutine(slotIndex, spell));
        return true;
    }

    /// <summary>Equip a spell into a slot at runtime (inventory/shop UI).</summary>
    public void EquipSpell(int slotIndex, SpellDefinition spell)
    {
        if (slotIndex < 0 || slotIndex >= equippedSpells.Length) return;
        equippedSpells[slotIndex] = spell;
    }

    /// <summary>Returns the spell equipped in a given slot (null if empty).</summary>
    public SpellDefinition GetEquippedSpell(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= equippedSpells.Length) return null;
        return equippedSpells[slotIndex];
    }

    /// <summary>Returns the full cooldown duration for the given slot.</summary>
    public float GetCooldownTotal(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= cooldownTotal.Length) return 0f;
        return cooldownTotal[slotIndex];
    }

    /// <summary>Returns remaining cooldown [0, total] for the given slot.</summary>
    public float GetCooldownRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= cooldownRemaining.Length) return 0f;
        return Mathf.Max(0f, cooldownRemaining[slotIndex]);
    }

    /// <summary>Returns cooldown progress [0=ready, 1=just cast] for the given slot.</summary>
    public float GetCooldownFraction(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= cooldownTotal.Length) return 0f;
        float total = cooldownTotal[slotIndex];
        if (total <= 0f) return 0f;
        return Mathf.Clamp01(cooldownRemaining[slotIndex] / total);
    }

    // -------------------------------------------------------------------------
    // Cast coroutine
    // -------------------------------------------------------------------------

    private IEnumerator CastRoutine(int slotIndex, SpellDefinition spell)
    {
        isCasting = true;

        // Cast-time delay (lock movement/input)
        if (spell.castTimeSeconds > 0f)
            yield return new WaitForSeconds(spell.castTimeSeconds);

        // Cast VFX at caster
        if (spell.castVFX != null)
        {
            var fx = Instantiate(spell.castVFX, transform.position, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + 0.2f);
        }

        if (spell.castSound != null)
            AudioSource.PlayClipAtPoint(spell.castSound, transform.position, spell.castVolume);

        // Deliver the spell
        DeliverSpell(spell);

        // Set cooldown
        float cd = spell.cooldownSeconds;
        if (PlayerStats.Instance != null)
            cd *= (1f - PlayerStats.Instance.CooldownReduction);

        cooldownRemaining[slotIndex] = cd;
        cooldownTotal[slotIndex]     = cd;
        OnCooldownStarted?.Invoke(slotIndex, cd);
        OnSpellCast?.Invoke(slotIndex);

        // Log to action tracker
        ActionTracker.Instance?.LogAction("local_player", "SpellCast", spell.spellId, Mathf.RoundToInt(spell.baseDamage));

        isCasting = false;
    }

    // -------------------------------------------------------------------------
    // Delivery routing — add new DeliveryType cases here
    // -------------------------------------------------------------------------

    private void DeliverSpell(SpellDefinition spell)
    {
        Vector3 aimTarget = GetAimTarget(spell.range);
        Vector2 dir       = ((Vector2)aimTarget - (Vector2)transform.position).normalized;
        float   bonusDmg  = PlayerStats.Instance != null ? PlayerStats.Instance.SpellDmg - 5f : 0f;

        switch (spell.delivery)
        {
            case SpellDefinition.DeliveryType.Projectile:
                FireProjectile(spell, dir, bonusDmg);
                break;

            case SpellDefinition.DeliveryType.AoE:
                FireAoE(spell, aimTarget, bonusDmg);
                break;

            case SpellDefinition.DeliveryType.SelfBuff:
                ApplySelfBuff(spell);
                break;

            case SpellDefinition.DeliveryType.Beam:
                FireBeam(spell, dir, bonusDmg);
                break;

            case SpellDefinition.DeliveryType.Chain:
                FireChain(spell, bonusDmg);
                break;

            default:
                Debug.LogWarning($"[SpellCaster] Unhandled delivery type: {spell.delivery}");
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Delivery implementations
    // -------------------------------------------------------------------------

    private void FireProjectile(SpellDefinition spell, Vector2 dir, float bonusDmg)
    {
        if (spell.projectilePrefab == null)
        {
            Debug.LogWarning($"[SpellCaster] '{spell.displayName}' has no projectilePrefab assigned.");
            return;
        }

        GameObject go  = Instantiate(spell.projectilePrefab, transform.position, Quaternion.identity);
        var proj       = go.GetComponent<SpellProjectile>();
        if (proj == null) proj = go.AddComponent<SpellProjectile>();

        proj.definition    = spell;
        proj.bonusSpellDmg = bonusDmg;
        proj.direction     = dir;
    }

    private void FireAoE(SpellDefinition spell, Vector3 target, float bonusDmg)
    {
        // Clamp to range
        Vector3 dir    = (target - transform.position);
        if (dir.magnitude > spell.range)
            target = transform.position + dir.normalized * spell.range;

        GameObject go = new GameObject("SpellAoE");
        go.transform.position = target;

        var aoe       = go.AddComponent<SpellAoE>();
        aoe.definition    = spell;
        aoe.bonusSpellDmg = bonusDmg;
    }

    private void ApplySelfBuff(SpellDefinition spell)
    {
        if (spell.appliesEffect == null) return;

        var handler = GetComponent<StatusEffectHandler>();
        if (handler == null) handler = gameObject.AddComponent<StatusEffectHandler>();
        handler.Apply(spell.appliesEffect);
    }

    private void FireBeam(SpellDefinition spell, Vector2 dir, float bonusDmg)
    {
        // Raycast in direction, hit the first enemy in range
        var hits = Physics2D.RaycastAll(transform.position, dir, spell.range);
        float bonus = bonusDmg;

        foreach (var hit in hits)
        {
            var enemy = hit.collider.GetComponentInParent<SlimeEnemyController>();
            if (enemy == null) continue;

            float dmg = spell.RollDamage(bonus);
            enemy.TakeDamage(Mathf.RoundToInt(dmg));
            WorldFloatingText.Spawn(hit.point, Mathf.RoundToInt(dmg).ToString(),
                                    new Color(0.6f, 0.8f, 1f));

            if (spell.appliesEffect != null && UnityEngine.Random.value < spell.statusChance)
                hit.collider.GetComponentInParent<StatusEffectHandler>()?.Apply(spell.appliesEffect);
        }

        if (spell.hitVFX != null)
        {
            Vector3 endPos = transform.position + (Vector3)(dir * spell.range);
            var fx = Instantiate(spell.hitVFX, endPos, Quaternion.identity);
            fx.Play();
            Destroy(fx.gameObject, fx.main.duration + 0.2f);
        }
    }

    private void FireChain(SpellDefinition spell, float bonusDmg)
    {
        // Find all enemies in range, hit up to chainCount
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, spell.range);
        int bounces = 0;
        Vector3 lastPos = transform.position;

        foreach (var col in cols)
        {
            if (bounces >= spell.chainCount) break;
            var enemy = col.GetComponentInParent<SlimeEnemyController>();
            if (enemy == null) continue;

            float dmg = spell.RollDamage(bonusDmg) * Mathf.Pow(0.75f, bounces); // 25% falloff per bounce
            enemy.TakeDamage(Mathf.RoundToInt(dmg));
            WorldFloatingText.Spawn(enemy.transform.position, $"⚡{Mathf.RoundToInt(dmg)}",
                                    new Color(0.9f, 0.9f, 0.2f));

            if (spell.appliesEffect != null && UnityEngine.Random.value < spell.statusChance)
                col.GetComponentInParent<StatusEffectHandler>()?.Apply(spell.appliesEffect);

            lastPos = enemy.transform.position;
            bounces++;
        }
    }

    // -------------------------------------------------------------------------
    // Aim helpers
    // -------------------------------------------------------------------------

    private Vector3 GetAimTarget(float range)
    {
        if (aimAtMouse && aimCamera != null)
        {
            Vector3 mouseWorld = aimCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = transform.position.z;

            // Clamp to range
            Vector3 dir = mouseWorld - transform.position;
            if (dir.magnitude > range)
                mouseWorld = transform.position + dir.normalized * range;

            return mouseWorld;
        }

        // Fallback: aim in facing direction (right)
        return transform.position + Vector3.right * range;
    }
}

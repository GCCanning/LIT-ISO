using UnityEngine;
using EthraClone.TrialWeek;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class SlimeEnemyController : MonoBehaviour
{
    public EnemyDefinition definition;
    public Transform target;
    public IsoWorldChunkManager world;
    public int maxStepHeight = 0;
    public float footSampleRadius = 0.02f;
    public float spriteGroundLift = 0.04f;
    public int sortingOrderOffset = 0;

    private enum AiState
    {
        Idle,
        Wander,
        Chase,
        Attack,
        Hurt,
        Dead
    }

    private Rigidbody2D rb;
    private CircleCollider2D bodyCollider;
    private SpriteRenderer spriteRenderer;
    private Sprite[] idleSprites;
    private Sprite[] moveSprites;
    private Sprite[] attackSprites;
    private Sprite[] hurtSprites;
    private Sprite[] dieSprites;
    private AiState state = AiState.Idle;
    private Vector2 spawnPosition;
    private Vector2 wanderTarget;
    private float stateTimer;
    private float frameTimer;
    private float attackTimer;
    private int currentFrame;
    private int currentHealth;
    private Camera visibilityCamera;
    private float nextVisibilityCheckTime;
    private bool isVisibleToCamera = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        bodyCollider = GetComponent<CircleCollider2D>();
        bodyCollider.isTrigger = true;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            GameObject spriteObject = new GameObject("SpriteRenderer");
            spriteObject.transform.SetParent(transform);
            spriteObject.transform.localPosition = Vector3.zero;
            spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
        }
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
    }

    private void Start()
    {
        ApplyDefinition();
        spawnPosition = rb.position;

        if (target == null)
        {
            IsoPlayerController player = FindFirstObjectByType<IsoPlayerController>();
            if (player != null) target = player.transform;
        }

        if (world == null)
        {
            world = FindFirstObjectByType<IsoWorldChunkManager>();
        }
    }

    private void Update()
    {
        if (definition == null)
        {
            return;
        }

        UpdateVisibility();

        if (GameSettingsMenu.IsOpen && state != AiState.Dead)
        {
            Animate(idleSprites, definition.idleFrameDuration);
            return;
        }

        if (!isVisibleToCamera && state != AiState.Dead)
        {
            attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
            return;
        }

        attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);
        UpdateState();
        UpdateSpriteHeight();
        UpdateSorting();
    }

    private void FixedUpdate()
    {
        if (definition == null || GameSettingsMenu.IsOpen || state == AiState.Dead || state == AiState.Attack || state == AiState.Hurt)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (!isVisibleToCamera)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desired = Vector2.zero;
        if (state == AiState.Chase && target != null)
        {
            desired = ((Vector2)target.position - rb.position).normalized;
        }
        else if (state == AiState.Wander)
        {
            Vector2 toWander = wanderTarget - rb.position;
            if (toWander.sqrMagnitude > 0.04f)
            {
                desired = toWander.normalized * 0.65f;
            }
        }

        Vector2 delta = desired * definition.moveSpeed * Time.fixedDeltaTime;
        if (delta.sqrMagnitude > 0.000001f)
        {
            MoveWithTerrain(delta);
        }
        rb.linearVelocity = Vector2.zero;
    }

    public void TakeDamage(int amount)
    {
        if (definition == null || state == AiState.Dead || amount <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth <= 0)
        {
            EnterState(AiState.Dead);
            bodyCollider.enabled = false;

            // Award XP to the player
            if (XPSystem.Instance != null && definition != null)
                XPSystem.Instance.AwardXP(definition.xpReward, transform.position);

            // Log kill to ActionTracker
            if (ActionTracker.Instance != null && definition != null)
                ActionTracker.Instance.LogAction("local_player", "EnemyKilled", definition.enemyId, definition.xpReward);

            // Notify ClassSystem for tutorial tracking
            if (ClassSystem.Instance != null)
                ClassSystem.Instance.RecordKill();

            Destroy(gameObject, Mathf.Max(0.5f, definition.dieFrames.Length * definition.dieFrameDuration + 0.2f));
            return;
        }

        EnterState(AiState.Hurt);
    }

    private void ApplyDefinition()
    {
        if (definition == null)
        {
            return;
        }

        currentHealth = definition.maxHealth;
        bodyCollider.radius = definition.colliderRadius;
        transform.localScale = Vector3.one * definition.visualScale;
        spriteRenderer.color = definition.tint;
        spriteRenderer.transform.localPosition = new Vector3(0f, spriteGroundLift, 0f);

        idleSprites = BuildSprites(definition.idleFrames);
        moveSprites = BuildSprites(definition.moveFrames);
        attackSprites = BuildSprites(definition.attackFrames);
        hurtSprites = BuildSprites(definition.hurtFrames);
        dieSprites = BuildSprites(definition.dieFrames);

        if (idleSprites.Length > 0)
        {
            spriteRenderer.sprite = idleSprites[0];
        }
    }

    private Sprite[] BuildSprites(Texture2D[] textures)
    {
        if (textures == null || textures.Length == 0)
        {
            return new Sprite[0];
        }

        Sprite[] sprites = new Sprite[textures.Length];
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            if (texture == null)
            {
                continue;
            }

            texture.filterMode = FilterMode.Point;
            Rect rect = new Rect(0, 0, texture.width, texture.height);
            sprites[i] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.12f), definition.pixelsPerUnit);
        }
        return sprites;
    }

    private void UpdateState()
    {
        if (state == AiState.Dead)
        {
            AnimateOnce(dieSprites, definition.dieFrameDuration);
            return;
        }

        if (state == AiState.Hurt)
        {
            if (AnimateOnce(hurtSprites, definition.hurtFrameDuration))
            {
                EnterState(AiState.Idle);
            }
            return;
        }

        if (state == AiState.Attack)
        {
            if (AnimateOnce(attackSprites, definition.attackFrameDuration))
            {
                EnterState(AiState.Chase);
            }
            return;
        }

        float distanceToTarget = target != null
            ? Vector2.Distance(transform.position, target.position)
            : float.PositiveInfinity;

        if (distanceToTarget <= definition.attackRange && attackTimer <= 0f)
        {
            attackTimer = definition.attackCooldown;
            PlayerHealth health = target != null ? target.GetComponent<PlayerHealth>() : null;
            if (health != null)
            {
                health.TakeDamage(definition.contactDamage);
            }
            EnterState(AiState.Attack);
            return;
        }

        if (distanceToTarget <= definition.detectionRadius &&
            Vector2.Distance(spawnPosition, transform.position) <= definition.leashRadius)
        {
            state = AiState.Chase;
            Animate(moveSprites, definition.moveFrameDuration);
            FaceTarget(target.position);
            return;
        }

        if (state == AiState.Idle)
        {
            stateTimer -= Time.deltaTime;
            Animate(idleSprites, definition.idleFrameDuration);
            if (stateTimer <= 0f)
            {
                PickWanderTarget();
                state = AiState.Wander;
            }
            return;
        }

        if (state == AiState.Wander)
        {
            Animate(moveSprites, definition.moveFrameDuration);
            FaceTarget(wanderTarget);
            if (Vector2.Distance(rb.position, wanderTarget) <= 0.2f)
            {
                EnterState(AiState.Idle);
            }
        }
    }

    private void EnterState(AiState nextState)
    {
        state = nextState;
        frameTimer = 0f;
        currentFrame = 0;
        if (state == AiState.Idle)
        {
            stateTimer = Random.Range(definition.wanderPauseMin, definition.wanderPauseMax);
        }
    }

    private void PickWanderTarget()
    {
        Vector2 random = Random.insideUnitCircle * definition.wanderRadius;
        wanderTarget = spawnPosition + random;
    }

    private void MoveWithTerrain(Vector2 delta)
    {
        Vector2 targetPosition = rb.position + delta;
        if (CanStandAt(targetPosition))
        {
            rb.MovePosition(targetPosition);
            return;
        }

        EnterState(AiState.Idle);
    }

    private bool CanStandAt(Vector2 rootWorldPosition)
    {
        if (world == null)
        {
            return true;
        }

        Vector3 current = transform.position;
        // transform.position.z stores the current integer height for slimes
        int currentHeight = Mathf.RoundToInt(current.z);
        Vector3 targetPosition = new Vector3(rootWorldPosition.x, rootWorldPosition.y, current.z);
        return world.CanMoveFootprint(current, targetPosition, currentHeight, maxStepHeight, footSampleRadius, bodyCollider);
    }

    private void FaceTarget(Vector2 lookAt)
    {
        float dx = lookAt.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.03f)
        {
            spriteRenderer.flipX = dx < 0f;
        }
    }

    private void UpdateSpriteHeight()
    {
        if (world == null)
        {
            return;
        }

        int currentHeight = Mathf.RoundToInt(transform.position.z);
        // Use current height to avoid snapping to walls in front
        Vector3Int cell = world.WorldToGroundCell(transform.position, currentHeight);
        Vector3 cellCenter = world.GetCellCenterWorld(cell);
        Vector3 pos = transform.position;
        pos.z = cellCenter.z;
        transform.position = pos;
    }

    private void UpdateSorting()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f) + sortingOrderOffset;
        }
    }

    private void UpdateVisibility()
    {
        if (Time.unscaledTime < nextVisibilityCheckTime)
            return;

        if (visibilityCamera == null)
            visibilityCamera = Camera.main;

        if (visibilityCamera == null)
        {
            isVisibleToCamera = true;
            nextVisibilityCheckTime = Time.unscaledTime + 0.25f;
            return;
        }

        Vector3 viewport = visibilityCamera.WorldToViewportPoint(transform.position);
        isVisibleToCamera =
            viewport.z >= -1f &&
            viewport.x >= -0.2f && viewport.x <= 1.2f &&
            viewport.y >= -0.2f && viewport.y <= 1.2f;
        nextVisibilityCheckTime = Time.unscaledTime + 0.15f;
    }

    private void Animate(Sprite[] frames, float duration)
    {
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        frameTimer += Time.deltaTime;
        if (frameTimer >= duration)
        {
            frameTimer = 0f;
            currentFrame = (currentFrame + 1) % frames.Length;
            spriteRenderer.sprite = frames[currentFrame];
        }
    }

    private bool AnimateOnce(Sprite[] frames, float duration)
    {
        if (frames == null || frames.Length == 0)
        {
            return true;
        }

        spriteRenderer.sprite = frames[Mathf.Clamp(currentFrame, 0, frames.Length - 1)];
        frameTimer += Time.deltaTime;
        if (frameTimer >= duration)
        {
            frameTimer = 0f;
            currentFrame++;
        }

        if (currentFrame >= frames.Length)
        {
            currentFrame = frames.Length - 1;
            return true;
        }

        return false;
    }
}

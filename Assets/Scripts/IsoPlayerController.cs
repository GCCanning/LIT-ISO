using System;
using UnityEngine;

namespace EthraClone.TrialWeek
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class IsoPlayerController : MonoBehaviour
    {
        public event Action<Vector3> OnMoved;

        [Header("Movement")]
public float movementSpeed = 5f;
    public float acceleration = 34f;
    public float deceleration = 48f;
    public float wallStopDeceleration = 90f;
    public bool allowWallSlide = false;
    public bool useCameraRelativeInput = true;
    public int maxWalkStepHeight = 0;
    public float footSampleRadius = 0.15f;
    public Vector2 footSampleOffset = Vector2.zero;
    public int maxJumpHeight = 1;
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpEdgeForgivenessDistance = 0.36f;
    public int jumpEdgeSearchSteps = 4;
    public float jumpDuration = 0.40f;
    public float jumpArcHeight = 0.90f;
    public float landingLockoutDuration = 0.03f;
    public float jumpMomentumDistance = 1.20f;
    public float jumpMinimumDistance = 0.80f;
    public float jumpMomentumSpeedScale = 0.18f;
    public float spriteHeightOffsetPerLevel = 0.25f;

    [Header("World Input")]
    public Grid grid;
    public IsoWorldChunkManager world;
    public IsoRuntimeRecorder recorder;
    public Camera inputCamera;
    public Transform selectionMarker;
    public Vector3Int selectedCell;
    public string LastBlockedReason { get; private set; } = "None";
    public int CurrentGroundHeight => currentGroundHeight;

    private int currentGroundHeight = 0;

    [Header("Visuals")]
    public float visualScale = 1f;
    public float spriteGroundLift = 0.06f;
    public GameObject landingParticlePrefab;
    public Color fallbackSpriteColor = new Color(0.25f, 0.35f, 0.42f, 1f);
public Color fallbackAccentColor = new Color(0.92f, 0.78f, 0.38f, 1f);

    [Header("Animation")]
    public Texture2D walkSpriteSheet;
    public int walkSheetColumns = 4;
    public int walkSheetRows = 8;
    public float walkSpritePixelsPerUnit = 128f;
    public float frameDuration = 0.1f;
    public bool animateWalkFrames = false;
    public Texture2D idleSpriteSheet;
    public int idleSheetColumns = 4;
    public int idleSheetRows = 8;
    public float idleFrameDuration = 0.18f;
    public bool animateIdleFrames = true;
    public bool useWalkBob = true;
    public float walkBobHeight = 0.035f;
    public float walkBobFrequency = 8f;

    [Header("Audio")]
    public AudioClip idleAudioClip;
    [Range(0f, 1f)] public float idleAudioVolume = 0.22f;

    private const string DefaultWalkSheetPath = "Characters/Player/HollowedLight_512x1024";
    private const string DefaultIdleSheetPath = "Characters/Player/ReferenceKnight_Idle_512x1024";
    private const string DefaultIdleAudioPath = "Audio/Player/Player_Idle_IceHum";

    private Rigidbody2D rb;
    private CircleCollider2D physicalCollider;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveInput = Vector2.zero;
    private Vector2 motorVelocity = Vector2.zero;
    private Vector2 lastDirection = Vector2.down;
    private float animationTimer = 0f;
    private int currentFrameIndex = 0;
    private Sprite[] currentAnimationSet;
    private Direction8 currentDirection = Direction8.S;
    private Direction8 previousAnimationDirection = Direction8.S;
    private bool isMoving = false;
    private bool isJumping = false;
    private Vector3 jumpStartPosition;
    private Vector3 jumpTargetPosition;
    private Vector2 jumpHorizontalVelocity;
    private Vector3Int jumpTargetCell;
    private int jumpStartHeight;
    private int jumpTargetHeight;
    private float jumpTimer;
    private float landingLockoutTimer;
    private Vector3 spriteBaseLocalPosition;
    private Sprite[] walkSouthSprites;
    private Sprite[] walkSouthEastSprites;
    private Sprite[] walkEastSprites;
    private Sprite[] walkNorthEastSprites;
    private Sprite[] walkNorthSprites;
    private Sprite[] walkNorthWestSprites;
    private Sprite[] walkWestSprites;
    private Sprite[] walkSouthWestSprites;
    private Sprite[] idleSouthSprites;
    private Sprite[] idleSouthEastSprites;
    private Sprite[] idleEastSprites;
    private Sprite[] idleNorthEastSprites;
    private Sprite[] idleNorthSprites;
    private Sprite[] idleNorthWestSprites;
    private Sprite[] idleWestSprites;
    private Sprite[] idleSouthWestSprites;
    private AudioSource idleAudioSource;

    // Direction mapping for 8 directions
    private enum Direction8
    {
        N, NE, E, SE, S, SW, W, NW
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Ensure physics matrix is configured for height layers (10-17)
        for (int i = 0; i < 8; i++)
        {
            int layerA = 10 + i;
            for (int j = 0; j < 32; j++)
            {
                // Each height layer should collide with:
                // 1. Itself (The tilemap for this height)
                // 2. Default (Props, trees, etc.)
                // 3. Player (Enemies, NPCs, other players)
                bool shouldCollide = (j == layerA) || (j == 0) || (j == 18);
                Physics2D.IgnoreLayerCollision(layerA, j, !shouldCollide);
            }
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        if (recorder == null)
        {
            recorder = FindFirstObjectByType<IsoRuntimeRecorder>();
        }

        // Defensive fallback: if the world wasn't wired by setup, find it. Without a
        // world reference ALL terrain collision silently disables (the player walks
        // through everything), because movement is resolved against the world contract.
        if (world == null)
        {
            world = FindFirstObjectByType<IsoWorldChunkManager>();
            if (world == null)
            {
                Debug.LogWarning("IsoPlayerController: no IsoWorldChunkManager found — terrain collision is disabled until one exists.");
            }
        }

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            GameObject spriteObj = new GameObject("SpriteRenderer");
            spriteObj.transform.SetParent(transform);
            spriteObj.transform.localPosition = Vector3.zero;
            spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
        }
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
        spriteRenderer.transform.localScale = Vector3.one * visualScale;
        spriteBaseLocalPosition = spriteRenderer.transform.localPosition + new Vector3(0f, spriteGroundLift, 0f);
        spriteRenderer.transform.localPosition = spriteBaseLocalPosition;

        // Start on the physics plane
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        // The footprint collider participates in overlap queries and actor interactions,
        // but terrain movement is resolved explicitly by the world contract so hidden
        // cliff blockers do not physically shove the player around.
        physicalCollider = GetComponent<CircleCollider2D>();
        if (physicalCollider == null)
        {
            physicalCollider = gameObject.AddComponent<CircleCollider2D>();
        }
        physicalCollider.isTrigger = true;
        physicalCollider.radius = 0.2f; // Matches footprint radius

        BuildSpriteSheetAnimations();
        SetupIdleAudio();

        currentAnimationSet = GetIdleAnimationForDirection(Direction8.S);
        if (currentAnimationSet != null && currentAnimationSet.Length > 0)
        {
            spriteRenderer.sprite = currentAnimationSet[0];
        }
        else if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = CreateFallbackPlayerSprite();
        }
    }

    private void Start()
    {
        if (world != null)
        {
            // Initialize selection marker visual
            if (selectionMarker != null)
            {
                var sr = selectionMarker.GetComponent<SpriteRenderer>();
                if (sr == null) sr = selectionMarker.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = world.GetSelectionSprite();
                sr.sortingOrder = 30; // Above tile borders (20) and elevation (0)
                
                var lr = selectionMarker.GetComponent<LineRenderer>();
                if (lr != null) lr.enabled = false;
            }

            // At start, find the highest ground at the spawn point
            currentGroundHeight = GetMaxHeightFootprint(transform.position, world != null ? world.MaxSupportedHeight : IsoWorldChunkManager.HeightLayerCount - 1);
            UpdateLayerBasedOnHeight(currentGroundHeight);
            SetSpriteVisualOffset(currentGroundHeight, 0f);
        }
    }

    private void Update()
    {
        if (GameSettingsMenu.IsOpen)
        {
            moveInput = Vector2.zero;
            isMoving = false;
            UpdateAnimation();
            UpdateIdleAudio();
            return;
        }

        UpdateHoverSelection();
        HandleInput();
        HandleMouseInput();
        HandleJumpInput();
UpdateAnimation();
        UpdateIdleAudio();
    }

    private void FixedUpdate()
    {
        if (GameSettingsMenu.IsOpen)
        {
            motorVelocity = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isJumping)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (landingLockoutTimer > 0f)
            landingLockoutTimer -= Time.fixedDeltaTime;

        Vector2 desiredVelocity = moveInput * movementSpeed;
        float rate = desiredVelocity.sqrMagnitude > motorVelocity.sqrMagnitude ? acceleration : deceleration;
        motorVelocity = Vector2.MoveTowards(motorVelocity, desiredVelocity, rate * Time.fixedDeltaTime);

        Vector2 requestedDelta = motorVelocity * Time.fixedDeltaTime;
        Vector2 appliedDelta = ResolveMotorDelta(requestedDelta, out bool blockedByHeight);
        if (blockedByHeight)
        {
            motorVelocity = Vector2.MoveTowards(motorVelocity, Vector2.zero, wallStopDeceleration * Time.fixedDeltaTime);
        }

        Vector2 nextPosition = rb.position + appliedDelta;
        Vector2 oldPosition = rb.position;
        rb.MovePosition(nextPosition);
        rb.linearVelocity = Vector2.zero;

        if ((nextPosition - oldPosition).sqrMagnitude > 0.000001f)
        {
            OnMoved?.Invoke(transform.position);
        }
    }

    private Vector2 ResolveMotorDelta(Vector2 requestedDelta, out bool blockedByHeight)
    {
        blockedByHeight = false;
        if (requestedDelta.sqrMagnitude <= 0.000001f)
        {
            return Vector2.zero;
        }

        Vector3 currentPosition = transform.position;
        Vector3 fullTarget = currentPosition + (Vector3)requestedDelta;
        if (CanStandAt(fullTarget))
        {
            LastBlockedReason = "None";
            return requestedDelta;
        }

        IsoWorldChunkManager.FootprintMoveEvaluation evaluation = default;
        if (world != null)
        {
            evaluation = world.EvaluateFootprintMove(
                GetFootWorldPosition(transform.position),
                GetFootWorldPosition(fullTarget),
                currentGroundHeight,
                maxWalkStepHeight,
                footSampleRadius,
                physicalCollider);
        }

        blockedByHeight = evaluation.IsBlocked || IsHeightBlocked(fullTarget);
        if (blockedByHeight && world != null)
        {
            LastBlockedReason = evaluation.IsBlocked
                ? evaluation.Reason
                : $"Move blocked: {evaluation.FromCell.x},{evaluation.FromCell.y},h{evaluation.FromHeight} -> {evaluation.ToCell.x},{evaluation.ToCell.y},h{evaluation.TargetHeight}";
        }

        if (allowWallSlide)
        {
            Vector2 xDelta = new Vector2(requestedDelta.x, 0f);
            Vector2 yDelta = new Vector2(0f, requestedDelta.y);
            bool canX = Mathf.Abs(xDelta.x) > 0.0001f && CanStandAt(currentPosition + (Vector3)xDelta);
            bool canY = Mathf.Abs(yDelta.y) > 0.0001f && CanStandAt(currentPosition + (Vector3)yDelta);

            if (canX && !canY) return xDelta;
            if (canY && !canX) return yDelta;
        }

        Vector2 lastValid = Vector2.zero;
        Vector2 low = Vector2.zero;
        Vector2 high = requestedDelta;
        for (int i = 0; i < 6; i++)
        {
            Vector2 mid = Vector2.Lerp(low, high, 0.5f);
            if (CanStandAt(currentPosition + (Vector3)mid))
            {
                lastValid = mid;
                low = mid;
            }
            else
            {
                high = mid;
                blockedByHeight = true;
            }
        }

        return lastValid;
    }

    private bool CanStandAt(Vector3 rootWorldPosition)
    {
        if (world == null)
        {
            return true;
        }

        return world.CanMoveFootprint(
            GetFootWorldPosition(transform.position),
            GetFootWorldPosition(rootWorldPosition),
            currentGroundHeight,
            maxWalkStepHeight,
            footSampleRadius,
            physicalCollider);
    }

    private bool IsHeightBlocked(Vector3 rootWorldPosition)
    {
        if (world == null)
        {
            return false;
        }

        IsoWorldChunkManager.FootprintMoveEvaluation evaluation = world.EvaluateFootprintMove(
            GetFootWorldPosition(transform.position),
            GetFootWorldPosition(rootWorldPosition),
            currentGroundHeight,
            maxWalkStepHeight,
            footSampleRadius,
            physicalCollider);
        return evaluation.IsBlocked;
    }

    private void LateUpdate()
    {
        if (isJumping)
        {
            UpdateJump();
            return;
        }

        UpdateWorldElevation();
    }

    private void HandleInput()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        moveInput = QuantizeToEightDirections(GetCameraRelativeMoveInput(new Vector2(horizontalInput, verticalInput)));

        if (moveInput.sqrMagnitude > 0.01f)
        {
            isMoving = true;
            lastDirection = moveInput;
            currentDirection = GetDirection8(lastDirection);
        }
        else
        {
            isMoving = false;
            animationTimer = 0f;
            currentFrameIndex = 0;
        }
    }

    private void UpdateHoverSelection()
    {
        if (grid == null || inputCamera == null || world == null) return;

        Vector3 mouseWorld = inputCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;
        
        // Use the height-aware WorldToGroundCell to find the tile we are hovering over
        selectedCell = world.WorldToGroundCell(mouseWorld, world.MaxSupportedHeight);
        MoveSelectionMarker(selectedCell);
    }

    private void HandleMouseInput()
    {
        bool leftDown  = Input.GetMouseButtonDown(0);
        bool rightDown = Input.GetMouseButtonDown(1);
        if (!leftDown && !rightDown) return;

        if (leftDown)
        {
            if (recorder != null) recorder.RecordTileSelection(selectedCell, "tile_selected");
            Debug.Log($"Selected tile {selectedCell}");
        }
        else if (rightDown)
        {
            if (recorder != null) recorder.RecordTileSelection(selectedCell, "interact_requested");
            Debug.Log($"Interact requested at {selectedCell}");
        }
    }

    private void HandleJumpInput()
    {
        if (!Input.GetKeyDown(jumpKey) || isJumping || grid == null || world == null)
        {
            return;
        }

        Vector2 currentVelocity = motorVelocity;
        Vector2 jumpDirection = currentVelocity.sqrMagnitude > 0.04f
            ? currentVelocity.normalized
            : (moveInput.sqrMagnitude > 0.01f ? moveInput : lastDirection);
        float jumpDistance = Mathf.Max(
            jumpMinimumDistance,
            jumpMomentumDistance + currentVelocity.magnitude * jumpMomentumSpeedScale);

        Vector3 rootPos = transform.position;
        rootPos.z = 0f;
        // Use current height as search base
        Vector3Int fromCell = world.WorldToGroundCell(GetFootWorldPosition(rootPos), currentGroundHeight);
        if (!TryResolveJumpTarget(fromCell, jumpDirection, jumpDistance, out Vector3Int toCell, out int fromHeight, out int toHeight))
        {
            return;
        }

        jumpStartPosition = transform.position;
        jumpStartPosition.z = 0f;

        // Jump target for the ROOT is the cell center in the 0-plane
        jumpTargetPosition = grid.GetCellCenterWorld(new Vector3Int(toCell.x, toCell.y, 0)) - (Vector3)footSampleOffset;
        jumpTargetPosition.z = 0f;

        jumpTargetCell = toCell;
        jumpStartHeight = fromHeight;
        jumpTargetHeight = toHeight;
        jumpTimer = 0f;
        isJumping = true;
        landingLockoutTimer = 0f;
        jumpHorizontalVelocity = (new Vector2(jumpTargetPosition.x, jumpTargetPosition.y) - new Vector2(jumpStartPosition.x, jumpStartPosition.y)) / Mathf.Max(0.01f, jumpDuration);
        motorVelocity = jumpDirection * Mathf.Max(currentVelocity.magnitude, movementSpeed * 0.65f);
        rb.linearVelocity = Vector2.zero;

        // Disable physical collision during jump arc to allow passing over cliffs
        if (physicalCollider != null)
        {
            physicalCollider.isTrigger = true;
        }

        if (recorder != null)
        {
            recorder.RecordJumpStarted(fromCell, toCell, fromHeight, toHeight);
        }
    }

    private void UpdateJump()
    {
        jumpTimer += Time.deltaTime;
        float t = Mathf.Clamp01(jumpTimer / Mathf.Max(0.01f, jumpDuration));
        
        // Root lerps in the 0-plane
        Vector3 position = Vector3.Lerp(jumpStartPosition, jumpTargetPosition, t);
        position.z = 0f;
        
        float visualHeight = Mathf.Lerp(jumpStartHeight, jumpTargetHeight, t);
        float visualArc = 4f * jumpArcHeight * t * (1f - t);
        
        rb.position = new Vector2(position.x, position.y);
        transform.position = position;
        
        SetSpriteVisualOffset(visualHeight, visualArc);

        if (t >= 1f)
        {
            rb.position = new Vector2(jumpTargetPosition.x, jumpTargetPosition.y);
            transform.position = jumpTargetPosition;
            SetSpriteVisualOffset(jumpTargetHeight, 0f);
            isJumping = false;

            // Re-enable physical collision on landing
            if (physicalCollider != null)
            {
                physicalCollider.isTrigger = false;
            }

            // Immediately update layer based on the new landing height
            currentGroundHeight = jumpTargetHeight;
            UpdateLayerBasedOnHeight(currentGroundHeight);

            // Spawn landing particles if available
            if (landingParticlePrefab != null)
            {
                Instantiate(landingParticlePrefab, transform.position, Quaternion.identity);
            }
            else
            {
                // Fallback: simple procedural dust burst using existing GraphicsEnhancer logic
                CreateLandingDustBurst();
            }

            landingLockoutTimer = landingLockoutDuration;
motorVelocity = jumpHorizontalVelocity;
            rb.linearVelocity = Vector2.zero;

            // Camera shake feedback on landing — proportional to jump height
            CameraFollow camFollow = inputCamera != null
                ? inputCamera.GetComponent<CameraFollow>()
                : null;
            if (camFollow != null)
            {
                int heightDelta = Mathf.Abs(jumpTargetHeight - jumpStartHeight);
                float shakeAmount = 0.04f + heightDelta * 0.03f;
                camFollow.Shake(shakeAmount, 0.18f);
            }

            if (recorder != null)
            {
                recorder.RecordJumpLanded(jumpTargetCell);
            }
        }
    }

    private Vector3Int DirectionToCellStep(Vector2 direction)
{
        if (Mathf.Abs(direction.x) > 0.35f && Mathf.Abs(direction.y) > 0.35f)
        {
            return new Vector3Int(direction.x >= 0 ? 1 : -1, direction.y >= 0 ? 1 : -1, 0);
        }

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x >= 0 ? Vector3Int.right : Vector3Int.left;
        }

        return direction.y >= 0 ? Vector3Int.up : Vector3Int.down;
    }

    private bool TryResolveJumpTarget(
        Vector3Int fromCell,
        Vector2 jumpDirection,
        float jumpDistance,
        out Vector3Int toCell,
        out int fromHeight,
        out int toHeight)
    {
        fromHeight = world.GetHeightAtCell(fromCell);
        fromCell.z = fromHeight;

        Vector3 footPosition = GetFootWorldPosition(transform.position);
        Vector3 desiredLandingFootPosition = footPosition + (Vector3)(jumpDirection * jumpDistance);
        
        // Search for tiles up to max jump height from our current position
        toCell = world.WorldToGroundCell(desiredLandingFootPosition, fromHeight + maxJumpHeight);
        if (toCell.x == fromCell.x && toCell.y == fromCell.y)
        {
            toCell = fromCell + DirectionToCellStep(jumpDirection);
        }

        toHeight = world.GetHeightAtCell(toCell);
        toCell.z = toHeight;
        if (Mathf.Abs(toHeight - fromHeight) <= maxJumpHeight)
        {
            return true;
        }

        int steps = Mathf.Max(1, jumpEdgeSearchSteps);
        float stepDistance = Mathf.Max(0f, jumpEdgeForgivenessDistance) / steps;
        for (int i = 1; i <= steps; i++)
        {
            Vector3 forgivingFoot = footPosition + (Vector3)(jumpDirection * (stepDistance * i));
            Vector3Int forgivingFromCell = world.WorldToGroundCell(forgivingFoot, currentGroundHeight);
            int forgivingFromHeight = world.GetHeightAtCell(forgivingFromCell);
            Vector3 forgivingLanding = forgivingFoot + (Vector3)(jumpDirection * jumpDistance);
            Vector3Int forgivingToCell = world.WorldToGroundCell(forgivingLanding, fromHeight + maxJumpHeight);
            if (forgivingToCell.x == forgivingFromCell.x && forgivingToCell.y == forgivingFromCell.y)
            {
                forgivingToCell = forgivingFromCell + DirectionToCellStep(jumpDirection);
            }

            int forgivingToHeight = world.GetHeightAtCell(forgivingToCell);
            if (Mathf.Abs(forgivingToHeight - forgivingFromHeight) <= maxJumpHeight)
            {
                toCell = forgivingToCell;
                toCell.z = forgivingToHeight;
                toHeight = forgivingToHeight;
                return true;
            }
        }

        if (recorder != null)
        {
            recorder.RecordJumpBlocked(fromCell, toCell, fromHeight, toHeight);
        }
        LastBlockedReason = $"Jump blocked: {fromCell.x},{fromCell.y},h{fromHeight} -> {toCell.x},{toCell.y},h{toHeight}";

        return false;
    }

    private void MoveSelectionMarker(Vector3Int cell)
    {
        if (selectionMarker == null || grid == null)
        {
            return;
        }

        Vector3 markerPosition = world != null ? world.GetCellCenterWorld(cell) : grid.GetCellCenterWorld(cell);
        selectionMarker.position = markerPosition;
        selectionMarker.gameObject.SetActive(true);
    }

    private void UpdateWorldElevation()
    {
        if (grid == null || world == null)
        {
            return;
        }

        Vector3 position = transform.position;
        // Keep root in the physics plane
        position.z = 0f;
        
        // Search from current height down
        int footprintHeight = GetMaxHeightFootprint(position, currentGroundHeight);
        
        // Prevent auto-stepping up: only change height if it's a fall or if we are at the same level.
        // Stepping up requires a jump.
        if (footprintHeight < currentGroundHeight)
        {
            currentGroundHeight = footprintHeight;
        }
else if (footprintHeight == currentGroundHeight)
        {
            // Stay at current height
        }
        // If footprintHeight > currentGroundHeight, we ignore it (blocked by physics walls)

        transform.position = new Vector3(position.x, position.y, 0f);
        SetSpriteVisualOffset(currentGroundHeight, 0f);
        UpdateLayerBasedOnHeight(currentGroundHeight);
        UpdateSpriteSorting();
    }

    private int GetMaxHeightFootprint(Vector3 rootPosition, int searchHeight)
    {
        if (world == null) return 0;

        return world.GetMaxFootprintHeight(GetFootWorldPosition(rootPosition), searchHeight, Mathf.Max(footSampleRadius, 0.4f));
    }

    private void CreateLandingDustBurst()
    {
        GameObject dustGO = new GameObject("LandingDust");
        dustGO.transform.position = transform.position;
        
        ParticleSystem ps = dustGO.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = 0.4f;
        main.startSpeed = 0.8f;
        main.startSize = 0.15f;
        main.startColor = new Color(1f, 1f, 1f, 0.4f);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0, 15) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(90, 0, 0); // Flat on ground

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.3f); // Float up slightly

        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, 
                     new GradientAlphaKey[] { new GradientAlphaKey(0.5f, 0), new GradientAlphaKey(0, 1) });
        colorOL.color = grad;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        renderer.sortingOrder = spriteRenderer.sortingOrder - 1;

        ps.Play();
    }

    private void UpdateLayerBasedOnHeight(int height)
{
        // Height layers are indices 10-17
        int targetLayer = 10 + Mathf.Clamp(height, 0, world != null ? world.MaxSupportedHeight : IsoWorldChunkManager.HeightLayerCount - 1);
        if (gameObject.layer != targetLayer)
        {
            gameObject.layer = targetLayer;
        }
    }

    private void UpdateSpriteSorting()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Vector3 footPosition = GetFootWorldPosition(transform.position);
        spriteRenderer.sortingOrder = Mathf.RoundToInt(-footPosition.y * 100f);
    }

    private Vector3 GetFootWorldPosition(Vector3 rootWorldPosition)
    {
        Vector3 footPosition = rootWorldPosition + (Vector3)footSampleOffset;
        // Do NOT zero Z — the isometric mapping needs the current height
        // to correctly back-project the world position to the tile grid.
        return footPosition;
    }

    private Vector2 GetCameraRelativeMoveInput(Vector2 rawInput)
    {
        if (rawInput.sqrMagnitude <= 0.01f)
        {
            return Vector2.zero;
        }

        if (!useCameraRelativeInput || inputCamera == null)
        {
            return rawInput;
        }

        Vector3 cameraRight = inputCamera.transform.right;
        Vector3 cameraUp = inputCamera.transform.up;
        Vector2 worldRight = new Vector2(cameraRight.x, cameraRight.y).normalized;
        Vector2 worldUp = new Vector2(cameraUp.x, cameraUp.y).normalized;

        if (worldRight.sqrMagnitude <= 0.01f || worldUp.sqrMagnitude <= 0.01f)
        {
            return rawInput;
        }

        Vector2 worldInput = worldRight * rawInput.x + worldUp * rawInput.y;
        return worldInput.sqrMagnitude > 1f ? worldInput.normalized : worldInput;
    }

    private void SetSpriteVisualOffset(float height, float jumpArc)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Vector3 offset = spriteBaseLocalPosition;
        offset.y += height * spriteHeightOffsetPerLevel + jumpArc;
        if (useWalkBob && isMoving && !isJumping)
        {
            offset.y += Mathf.Abs(Mathf.Sin(Time.time * walkBobFrequency)) * walkBobHeight;
        }

        spriteRenderer.transform.localPosition = offset;
    }

    private void UpdateAnimation()
    {
        Direction8 direction = currentDirection;
        if (direction != previousAnimationDirection)
        {
            previousAnimationDirection = direction;
            animationTimer = 0f;
            currentFrameIndex = 0;
        }

        if (isMoving)
        {
            currentAnimationSet = GetAnimationForDirection(direction);
            spriteRenderer.flipX = false;

            if (currentAnimationSet == null || currentAnimationSet.Length == 0)
                return;

            if (animateWalkFrames)
            {
                animationTimer += Time.deltaTime;
                if (animationTimer >= frameDuration)
                {
                    animationTimer = 0f;
                    currentFrameIndex = (currentFrameIndex + 1) % currentAnimationSet.Length;
                }
            }
            else
            {
                currentFrameIndex = 0;
            }

            spriteRenderer.sprite = currentAnimationSet[currentFrameIndex];
        }
        else
        {
            currentAnimationSet = GetIdleAnimationForDirection(direction);
            spriteRenderer.flipX = false;

            if (currentAnimationSet == null || currentAnimationSet.Length == 0)
                return;

            if (animateIdleFrames && currentAnimationSet.Length > 1)
            {
                animationTimer += Time.deltaTime;
                if (animationTimer >= idleFrameDuration)
                {
                    animationTimer = 0f;
                    currentFrameIndex = (currentFrameIndex + 1) % currentAnimationSet.Length;
                }
            }
            else
            {
                currentFrameIndex = 0;
            }

            spriteRenderer.sprite = currentAnimationSet[currentFrameIndex];
        }
    }

    private Vector2 QuantizeToEightDirections(Vector2 input)
    {
        if (input.sqrMagnitude <= 0.01f)
        {
            return Vector2.zero;
        }

        Direction8 direction = GetDirection8(input);
        switch (direction)
        {
            case Direction8.N:
                return Vector2.up;
            case Direction8.NE:
                return new Vector2(1f, 1f).normalized;
            case Direction8.E:
                return Vector2.right;
            case Direction8.SE:
                return new Vector2(1f, -1f).normalized;
            case Direction8.S:
                return Vector2.down;
            case Direction8.SW:
                return new Vector2(-1f, -1f).normalized;
            case Direction8.W:
                return Vector2.left;
            case Direction8.NW:
                return new Vector2(-1f, 1f).normalized;
            default:
                return input.normalized;
        }
    }

    private Direction8 GetDirection8(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        angle = (angle + 360) % 360;

        // Map angle to 8 directions
        // N=90, NE=45, E=0, SE=315, S=270, SW=225, W=180, NW=135
        if (angle >= 67.5f && angle < 112.5f) return Direction8.N;
        if (angle >= 22.5f && angle < 67.5f) return Direction8.NE;
        if (angle >= 337.5f || angle < 22.5f) return Direction8.E;
        if (angle >= 292.5f && angle < 337.5f) return Direction8.SE;
        if (angle >= 247.5f && angle < 292.5f) return Direction8.S;
        if (angle >= 202.5f && angle < 247.5f) return Direction8.SW;
        if (angle >= 157.5f && angle < 202.5f) return Direction8.W;
        return Direction8.NW; // 112.5 to 157.5
    }

    private Sprite[] GetAnimationForDirection(Direction8 direction)
    {
        switch (direction)
        {
            case Direction8.N:
                return walkNorthSprites;
            case Direction8.NE:
                return walkNorthEastSprites;
            case Direction8.E:
                return walkEastSprites;
            case Direction8.SE:
                return walkSouthEastSprites;
            case Direction8.S:
                return walkSouthSprites;
            case Direction8.SW:
                return walkSouthWestSprites;
            case Direction8.W:
                return walkWestSprites;
            case Direction8.NW:
                return walkNorthWestSprites;
            default:
                return GetIdleAnimationForDirection(direction);
        }
    }

    private Sprite[] GetIdleAnimationForDirection(Direction8 direction)
    {
        switch (direction)
        {
            case Direction8.N:
                return idleNorthSprites ?? walkNorthSprites;
            case Direction8.NE:
                return idleNorthEastSprites ?? walkNorthEastSprites;
            case Direction8.E:
                return idleEastSprites ?? walkEastSprites;
            case Direction8.SE:
                return idleSouthEastSprites ?? walkSouthEastSprites;
            case Direction8.S:
                return idleSouthSprites ?? walkSouthSprites;
            case Direction8.SW:
                return idleSouthWestSprites ?? walkSouthWestSprites;
            case Direction8.W:
                return idleWestSprites ?? walkWestSprites;
            case Direction8.NW:
                return idleNorthWestSprites ?? walkNorthWestSprites;
            default:
                return idleSouthSprites ?? walkSouthSprites;
        }
    }

    private void BuildSpriteSheetAnimations()
    {
        walkSpriteSheet ??= Resources.Load<Texture2D>(DefaultWalkSheetPath);
        if (walkSpriteSheet == null || walkSheetColumns <= 0 || walkSheetRows <= 0)
        {
            BuildIdleSpriteSheetAnimations();
            return;
        }

        int frameWidth = walkSpriteSheet.width / walkSheetColumns;
        int frameHeight = walkSpriteSheet.height / walkSheetRows;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            BuildIdleSpriteSheetAnimations();
            return;
        }

        if (animateWalkFrames)
        {
            walkSouthSprites = SliceDirectionalRow(0, frameWidth, frameHeight);
            walkSouthEastSprites = SliceDirectionalRow(1, frameWidth, frameHeight);
            walkEastSprites = SliceDirectionalRow(2, frameWidth, frameHeight);
            walkNorthEastSprites = SliceDirectionalRow(3, frameWidth, frameHeight);
            walkNorthSprites = SliceDirectionalRow(4, frameWidth, frameHeight);
            walkNorthWestSprites = SliceDirectionalRow(5, frameWidth, frameHeight);
            walkWestSprites = SliceDirectionalRow(6, frameWidth, frameHeight);
            walkSouthWestSprites = SliceDirectionalRow(7, frameWidth, frameHeight);
            BuildIdleSpriteSheetAnimations();
            return;
        }

        // The current source sheet is not a true row-per-direction walk cycle.
        // Use hand-picked representative cells so 8-way facing is stable until
        // a clean walk sheet is available.
        walkSouthSprites = SliceSingleFrame(0, 0, frameWidth, frameHeight);
        walkSouthEastSprites = SliceSingleFrame(1, 2, frameWidth, frameHeight);
        walkEastSprites = SliceSingleFrame(2, 1, frameWidth, frameHeight);
        walkNorthEastSprites = SliceSingleFrame(3, 2, frameWidth, frameHeight);
        walkNorthSprites = SliceSingleFrame(4, 2, frameWidth, frameHeight);
        walkNorthWestSprites = SliceSingleFrame(5, 1, frameWidth, frameHeight);
        walkWestSprites = SliceSingleFrame(6, 1, frameWidth, frameHeight);
        walkSouthWestSprites = SliceSingleFrame(7, 0, frameWidth, frameHeight);

        BuildIdleSpriteSheetAnimations();
    }

    private void BuildIdleSpriteSheetAnimations()
    {
        idleSpriteSheet ??= Resources.Load<Texture2D>(DefaultIdleSheetPath);
        if (idleSpriteSheet == null || idleSheetColumns <= 0 || idleSheetRows <= 0)
        {
            return;
        }

        int frameWidth = idleSpriteSheet.width / idleSheetColumns;
        int frameHeight = idleSpriteSheet.height / idleSheetRows;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return;
        }

        idleSouthSprites = SliceDirectionalRow(idleSpriteSheet, idleSheetColumns, 0, frameWidth, frameHeight);
        idleSouthEastSprites = SliceDirectionalRow(idleSpriteSheet, idleSheetColumns, 1, frameWidth, frameHeight);
        idleEastSprites = SliceDirectionalRow(idleSpriteSheet, idleSheetColumns, 2, frameWidth, frameHeight);
        idleNorthEastSprites = SliceDirectionalRow(idleSpriteSheet, idleSheetColumns, 3, frameWidth, frameHeight);
        idleNorthSprites = SliceDirectionalRow(idleSpriteSheet, idleSheetColumns, 4, frameWidth, frameHeight);
        idleNorthWestSprites = SliceDirectionalRow(idleSpriteSheet, idleSheetColumns, 5, frameWidth, frameHeight);
        idleWestSprites = SliceDirectionalRow(idleSpriteSheet, idleSheetColumns, 6, frameWidth, frameHeight);
        idleSouthWestSprites = SliceDirectionalRow(idleSpriteSheet, idleSheetColumns, 7, frameWidth, frameHeight);
    }

    private Sprite[] SliceDirectionalRow(int rowFromTop, int frameWidth, int frameHeight)
    {
        return SliceDirectionalRow(walkSpriteSheet, walkSheetColumns, rowFromTop, frameWidth, frameHeight);
    }

    private Sprite[] SliceDirectionalRow(Texture2D sheet, int columns, int rowFromTop, int frameWidth, int frameHeight)
    {
        Sprite[] frames = new Sprite[columns];
        int y = sheet.height - ((rowFromTop + 1) * frameHeight);
        for (int x = 0; x < columns; x++)
        {
            Rect rect = new Rect(x * frameWidth, y, frameWidth, frameHeight);
            frames[x] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.05f), walkSpritePixelsPerUnit);
        }

        return frames;
    }

    private void SetupIdleAudio()
    {
        idleAudioClip ??= Resources.Load<AudioClip>(DefaultIdleAudioPath);
        if (idleAudioClip == null)
        {
            return;
        }

        idleAudioSource = GetComponent<AudioSource>();
        if (idleAudioSource == null)
        {
            idleAudioSource = gameObject.AddComponent<AudioSource>();
        }

        idleAudioSource.clip = idleAudioClip;
        idleAudioSource.loop = true;
        idleAudioSource.playOnAwake = false;
        idleAudioSource.spatialBlend = 0f;
        idleAudioSource.volume = idleAudioVolume;
    }

    private void UpdateIdleAudio()
    {
        if (idleAudioSource == null || idleAudioClip == null)
        {
            return;
        }

        bool shouldPlay = !isMoving && !isJumping;
        idleAudioSource.volume = idleAudioVolume;
        if (shouldPlay && !idleAudioSource.isPlaying)
        {
            idleAudioSource.Play();
        }
        else if (!shouldPlay && idleAudioSource.isPlaying)
        {
            idleAudioSource.Stop();
        }
    }

    private Sprite[] SliceSingleFrame(int rowFromTop, int column, int frameWidth, int frameHeight)
    {
        int y = walkSpriteSheet.height - ((rowFromTop + 1) * frameHeight);
        Rect rect = new Rect(column * frameWidth, y, frameWidth, frameHeight);
        return new[]
        {
            Sprite.Create(walkSpriteSheet, rect, new Vector2(0.5f, 0.05f), walkSpritePixelsPerUnit)
        };
    }

    private Sprite CreateFallbackPlayerSprite()
    {
        const int width = 32;
        const int height = 48;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(0f, 0f, 0f, 0f);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, clear);
            }
        }

        DrawEllipse(texture, 16, 12, 8, 5, new Color(0f, 0f, 0f, 0.35f));
        DrawEllipse(texture, 16, 25, 7, 12, fallbackSpriteColor);
        DrawEllipse(texture, 16, 38, 6, 6, fallbackAccentColor);
        DrawRect(texture, 13, 10, 6, 10, fallbackSpriteColor * 0.85f);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.18f), 32f);
    }

    private static void DrawEllipse(Texture2D texture, int centerX, int centerY, int radiusX, int radiusY, Color color)
    {
        for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
        {
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
                {
                    continue;
                }

                float dx = (x - centerX) / (float)radiusX;
                float dy = (y - centerY) / (float)radiusY;
                if (dx * dx + dy * dy <= 1f)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }

    private static void DrawRect(Texture2D texture, int startX, int startY, int rectWidth, int rectHeight, Color color)
    {
        for (int y = startY; y < startY + rectHeight; y++)
        {
            for (int x = startX; x < startX + rectWidth; x++)
            {
                if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                {
                    texture.SetPixel(x, y, color);
                }
            }
        }
    }
}
}

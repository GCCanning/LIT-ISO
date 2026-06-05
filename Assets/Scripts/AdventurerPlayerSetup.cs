using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AdventurerPlayerSetup : MonoBehaviour
{
    public float movementSpeed = 5f;
    public Sprite[] idleSprites;
    public Sprite[] runSprites;
    public Sprite[] attackSprites;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Vector2 lastDirection = Vector2.down;
    private int currentAnimationIndex = 0;
    private float animationTimer = 0f;
    private float frameDuration = 0.1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer == null)
        {
            GameObject spriteObj = new GameObject("SpriteRenderer");
            spriteObj.transform.SetParent(transform);
            spriteObj.transform.localPosition = Vector3.zero;
            spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
        }
        spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

        if (idleSprites != null && idleSprites.Length > 0)
        {
            spriteRenderer.sprite = idleSprites[0];
        }
    }

    private void Update()
    {
        HandleInput();
        UpdateAnimation();
    }

    private void HandleInput()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        Vector2 inputVector = new Vector2(horizontalInput, verticalInput).normalized;

        if (inputVector.magnitude > 0.1f)
        {
            lastDirection = inputVector;
            rb.linearVelocity = inputVector * movementSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void UpdateAnimation()
    {
        bool isMoving = rb.linearVelocity.magnitude > 0.1f;

        animationTimer += Time.deltaTime;

        Sprite[] currentAnimSet = isMoving ? runSprites : idleSprites;

        if (currentAnimSet == null || currentAnimSet.Length == 0)
            return;

        if (animationTimer >= frameDuration)
        {
            animationTimer = 0f;
            currentAnimationIndex = (currentAnimationIndex + 1) % currentAnimSet.Length;
        }

        spriteRenderer.sprite = currentAnimSet[GetDirectionIndex(lastDirection, currentAnimSet.Length)];
    }

    private int GetDirectionIndex(Vector2 direction, int frameCount)
    {
        int framesPerDirection = frameCount / 8;
        if (framesPerDirection <= 0)
        {
            return currentAnimationIndex % frameCount;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        angle = (angle + 360) % 360;

        int directionIndex = Mathf.RoundToInt(angle / 45f) % 8;
        int frameIndex = framesPerDirection * directionIndex + currentAnimationIndex % framesPerDirection;

        return Mathf.Min(frameIndex, frameCount - 1);
    }
}

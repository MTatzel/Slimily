using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlatformerSlimeController : MonoBehaviour
{
    [SerializeField] private PlatformerPhysicsData data;
    [SerializeField] private Transform visualTransform; // Child mit dem SpriteRenderer

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[4];
    private ContactFilter2D contactFilter;

    // --- State Machine & Zustände ---
    public PlatformerStateMachine StateMachine { get; private set; }
    public PlatformerGroundedState GroundedState { get; private set; }
    public PlatformerAirborneState AirborneState { get; private set; }

    // --- Zentralisierter Input ---
    public Vector2 MoveInput { get; private set; }
    public bool JumpTriggered { get; private set; }
    public bool JumpHeld { get; private set; }

    // --- Kollisions- & Orientierungsstatus ---
    public Vector2 Velocity { get; set; }
    public bool IsGrounded { get; set; }
    public bool HitCeiling { get; private set; }
    public bool HitWall { get; private set; }
    public Vector2 TargetSurfaceNormal { get; set; } = Vector2.up;
    public Vector2 CurrentSurfaceNormal { get; private set; } = Vector2.up;

    // Cooldown, um nach einem Absprung nicht sofort wieder eingesaugt zu werden
    private float detachCooldownTimer;

    [Header("Juice & Buffers")]
    [SerializeField] private float jumpBufferDuration = 0.12f;
    private float jumpBufferTimer;

    public bool HasBufferedJump => jumpBufferTimer > 0f;

    public void ConsumeJumpBuffer()
    {
        jumpBufferTimer = 0f;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        contactFilter = new ContactFilter2D
        {
            layerMask = data.groundLayer,
            useLayerMask = true,
            useTriggers = false
        };

        StateMachine = new PlatformerStateMachine();
        GroundedState = new PlatformerGroundedState(this, StateMachine, data);
        AirborneState = new PlatformerAirborneState(this, StateMachine, data);
    }

    private void Start()
    {
        StateMachine.Initialize(AirborneState);
    }

    private void Update()
    {
        ReadInput();
        StateMachine.Update();
        UpdateSurfaceRotation();

        if (detachCooldownTimer > 0f)
        {
            detachCooldownTimer -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        StateMachine.FixedUpdate();
        MoveAndResolveCollisions(Velocity * Time.fixedDeltaTime);
    }

    public void TriggerDetach(float duration = 0.12f)
    {
        detachCooldownTimer = duration;
        IsGrounded = false;
        TargetSurfaceNormal = Vector2.up;
    }

    public bool CanAttach() => detachCooldownTimer <= 0f;

    private void ReadInput()
{
    Vector2 input = Vector2.zero;
    var keyboard = Keyboard.current;

    if (keyboard != null)
    {
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;

        // Wenn Taste in diesem Frame gedrückt wurde: Puffer aufladen
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            jumpBufferTimer = jumpBufferDuration;
        }

        JumpHeld = keyboard.spaceKey.isPressed;
    }

    // Puffer herunterzählen
    if (jumpBufferTimer > 0f)
    {
        jumpBufferTimer -= Time.deltaTime;
    }

    MoveInput = input;
}

    private void UpdateSurfaceRotation()
    {
        transform.rotation = Quaternion.identity;

        if (visualTransform != null)
        {
            float targetAngle = Mathf.Atan2(TargetSurfaceNormal.y, TargetSurfaceNormal.x) * Mathf.Rad2Deg - 90f;
            visualTransform.rotation = Quaternion.Euler(0f, 0f, targetAngle);

            float halfColHeight = boxCollider.size.y * 0.5f;
            Vector2 visualOffset = -TargetSurfaceNormal * (halfColHeight - 0.26f);
            visualTransform.localPosition = (Vector2)boxCollider.offset + visualOffset;
        }

        CurrentSurfaceNormal = TargetSurfaceNormal;
    }

    private void MoveAndResolveCollisions(Vector2 deltaMovement)
    {
        IsGrounded = false;
        HitCeiling = false;
        HitWall = false;

        Vector2 boxSize = boxCollider.size - (Vector2.one * (data.skinWidth * 2f));
        Vector2 position = rb.position;
        Vector2 castOrigin = position + boxCollider.offset;

        Vector2 surfaceNormal = CurrentSurfaceNormal;
        Vector2 surfaceTangent = new Vector2(surfaceNormal.y, -surfaceNormal.x);

        bool isGroundedMode = StateMachine.CurrentState == GroundedState;

        if (isGroundedMode)
        {
            // 1. BEWEGUNG ENTLANG DER FLÄCHE
            if (Mathf.Abs(deltaMovement.x) > 0.0001f)
            {
                float moveDist = Mathf.Abs(deltaMovement.x) + data.skinWidth;
                Vector2 moveDir = surfaceTangent * Mathf.Sign(deltaMovement.x);

                int hits = Physics2D.BoxCast(
                    castOrigin,
                    boxSize,
                    0f,
                    moveDir,
                    contactFilter,
                    hitBuffer,
                    moveDist
                );

                if (hits > 0)
                {
                    RaycastHit2D hit = hitBuffer[0];
                    float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);
                    position += moveDir * actualDist;
                    HitWall = true;

                    TargetSurfaceNormal = hit.normal;
                    CurrentSurfaceNormal = hit.normal;
                    Velocity = Vector2.zero;
                }
                else
                {
                    position += surfaceTangent * deltaMovement.x;
                }
            }

            // 2. HAFTUNG ZUR FLÄCHE
            castOrigin = position + boxCollider.offset;
            float snapDist = data.skinWidth * 3f;
            Vector2 surfaceSnapDir = -surfaceNormal;

            int groundHits = Physics2D.BoxCast(
                castOrigin,
                boxSize,
                0f,
                surfaceSnapDir,
                contactFilter,
                hitBuffer,
                snapDist
            );

            if (groundHits > 0)
            {
                RaycastHit2D hit = hitBuffer[0];
                float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);
                position += surfaceSnapDir * actualDist;
                IsGrounded = true;
                TargetSurfaceNormal = hit.normal;
            }
            else
            {
                IsGrounded = false;
            }
        }
        else
        {
            // --- FLUG-MODUS ---

            // Y-Achse
            if (Mathf.Abs(deltaMovement.y) > 0.0001f)
            {
                float yDist = Mathf.Abs(deltaMovement.y) + data.skinWidth;
                float yDir = Mathf.Sign(deltaMovement.y);

                int yHits = Physics2D.BoxCast(
                    castOrigin,
                    boxSize,
                    0f,
                    Vector2.up * yDir,
                    contactFilter,
                    hitBuffer,
                    yDist
                );

                if (yHits > 0)
                {
                    RaycastHit2D hit = hitBuffer[0];
                    float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);
                    position.y += yDir * actualDist;

                    // Anheften nur erlauben, wenn der Detach-Cooldown abgelaufen ist
                    if (CanAttach())
                    {
                        TargetSurfaceNormal = hit.normal;
                        CurrentSurfaceNormal = hit.normal;
                        Velocity = Vector2.zero;
                        IsGrounded = true;
                        StateMachine.ChangeState(GroundedState);
                    }
                }
                else
                {
                    position.y += deltaMovement.y;
                }
            }

            // X-Achse
            castOrigin = position + boxCollider.offset;
            if (Mathf.Abs(deltaMovement.x) > 0.0001f)
            {
                float xDist = Mathf.Abs(deltaMovement.x) + data.skinWidth;
                float xDir = Mathf.Sign(deltaMovement.x);

                int xHits = Physics2D.BoxCast(
                    castOrigin,
                    boxSize,
                    0f,
                    Vector2.right * xDir,
                    contactFilter,
                    hitBuffer,
                    xDist
                );

                if (xHits > 0)
                {
                    RaycastHit2D hit = hitBuffer[0];
                    float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);
                    position.x += xDir * actualDist;

                    if (CanAttach())
                    {
                        HitWall = true;
                        TargetSurfaceNormal = hit.normal;
                        CurrentSurfaceNormal = hit.normal;
                        Velocity = Vector2.zero;
                        IsGrounded = true;
                        StateMachine.ChangeState(GroundedState);
                    }
                    else
                    {
                        Velocity = new Vector2(0f, Velocity.y);
                    }
                }
                else
                {
                    position.x += deltaMovement.x;
                }
            }
        }

        rb.MovePosition(position);
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = IsGrounded ? Color.green : Color.cyan;
        Vector2 center = (Vector2)transform.position + col.offset;
        Gizmos.DrawWireCube(center, col.size);
    }
}
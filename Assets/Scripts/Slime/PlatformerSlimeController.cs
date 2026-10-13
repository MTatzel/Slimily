using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlatformerSlimeController : MonoBehaviour
{
    [SerializeField] private PlatformerPhysicsData data;
    [SerializeField] private Transform visualTransform; // Child mit dem SpriteRenderer

    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[8];
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
        CurrentSurfaceNormal = Vector2.up;
    }

    public bool CanAttach() => detachCooldownTimer <= 0f;

    public float GetColliderExtent(Vector2 direction)
    {
        Vector2 halfSize = boxCollider.size * 0.5f;
        return Mathf.Abs(direction.x) * halfSize.x + Mathf.Abs(direction.y) * halfSize.y;
    }

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

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                jumpBufferTimer = jumpBufferDuration;
            }

            JumpHeld = keyboard.spaceKey.isPressed;
        }

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.04f)
            {
                input = stick;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                jumpBufferTimer = jumpBufferDuration;
            }

            JumpHeld |= gamepad.buttonSouth.isPressed;
        }

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
            Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);

            float rotSpeed = (data != null && data.alignRotationSpeed > 0f) ? data.alignRotationSpeed : 720f;
            visualTransform.rotation = Quaternion.RotateTowards(visualTransform.rotation, targetRot, rotSpeed * Time.deltaTime);

            float halfColHeight = boxCollider.size.y * 0.5f;
            Vector2 targetOffset = (Vector2)boxCollider.offset - TargetSurfaceNormal * (halfColHeight - 0.26f);
            visualTransform.localPosition = Vector2.MoveTowards(visualTransform.localPosition, targetOffset, 15f * Time.deltaTime);
        }
    }

    private void MoveAndResolveCollisions(Vector2 deltaMovement)
    {
        IsGrounded = false;
        HitCeiling = false;
        HitWall = false;

        Vector2 boxSize = boxCollider.size - (Vector2.one * (data.skinWidth * 2f));
        Vector2 position = rb.position;

        bool isGroundedMode = StateMachine.CurrentState == GroundedState;

        if (isGroundedMode)
        {
            float moveAmount = deltaMovement.x;
            float remainingMove = Mathf.Abs(moveAmount);
            float moveSign = Mathf.Sign(moveAmount);

            int maxIterations = 3;
            while (remainingMove > 0.0001f && maxIterations > 0)
            {
                maxIterations--;

                Vector2 currentNormal = CurrentSurfaceNormal;
                Vector2 currentTangent = new Vector2(currentNormal.y, -currentNormal.x);
                Vector2 moveDir = currentTangent * moveSign;

                Vector2 currentCenter = position + boxCollider.offset;
                float extentNormal = GetColliderExtent(-currentNormal);
                float extentTangent = GetColliderExtent(moveDir);

                float stepDist = remainingMove;
                Vector2 castOrigin = currentCenter;
                float castDist = stepDist + data.skinWidth;

                // 1. INNENECKEN-CHECK (Frontale Wand im Weg?)
                int hits = Physics2D.BoxCast(
                    castOrigin,
                    boxSize,
                    0f,
                    moveDir,
                    contactFilter,
                    hitBuffer,
                    castDist
                );

                int blockingHitIndex = -1;
                for (int i = 0; i < hits; i++)
                {
                    if (Vector2.Dot(hitBuffer[i].normal, moveDir) < -0.3f)
                    {
                        blockingHitIndex = i;
                        break;
                    }
                }

                if (blockingHitIndex >= 0)
                {
                    RaycastHit2D hit = hitBuffer[blockingHitIndex];
                    float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);
                    position += moveDir * actualDist;
                    remainingMove = Mathf.Max(0f, remainingMove - actualDist);

                    bool isInnerCorner = Mathf.Abs(Vector2.Dot(currentNormal, hit.normal)) < 0.35f;

                    if (isInnerCorner)
                    {
                        TargetSurfaceNormal = hit.normal;
                        CurrentSurfaceNormal = hit.normal;
                        HitWall = true;
                    }
                    else
                    {
                        HitWall = true;
                        Velocity = Vector2.zero;
                        remainingMove = 0f;
                    }
                    continue;
                }

                // 2. AUSSENECKEN-CHECK (Bricht der Boden vor uns ab?)
                Vector2 leadingEdge = currentCenter + moveDir * (extentTangent + stepDist);
                float floorCheckDist = extentNormal + Mathf.Max(0.2f, data.skinWidth * 8f);

                RaycastHit2D leadingGroundHit = Physics2D.Raycast(leadingEdge, -currentNormal, floorCheckDist, data.groundLayer);

                if (leadingGroundHit.collider != null && Vector2.Dot(leadingGroundHit.normal, currentNormal) > 0.6f)
                {
                    // Durchgehender Boden: Normalen Schritt ausführen
                    position += moveDir * stepDist;
                    remainingMove = 0f;
                }
                else
                {
                    // Außenkante voraus: Wand unterhalb der Kante abtasten
                    float probeDepth = Mathf.Min(0.2f, extentNormal * 0.5f);
                    Vector2 probeOrigin = leadingEdge - currentNormal * (extentNormal + probeDepth);
                    float probeDist = extentTangent + stepDist + 0.6f;

                    RaycastHit2D wallHit = Physics2D.Raycast(probeOrigin, -moveDir, probeDist, data.groundLayer);

                    if (wallHit.collider != null && Vector2.Dot(wallHit.normal, moveDir) > 0.4f)
                    {
                        Vector2 newNormal = wallHit.normal;

                        // Exakten Scheitelpunkt C aus Boden- und Wandebene berechnen
                        RaycastHit2D safeFloorHit = Physics2D.Raycast(currentCenter - moveDir * (extentTangent * 0.5f), -currentNormal, floorCheckDist, data.groundLayer);

                        float cornerAlongNormal;
                        if (safeFloorHit.collider != null)
                        {
                            cornerAlongNormal = Vector2.Dot(safeFloorHit.point, currentNormal);
                        }
                        else
                        {
                            cornerAlongNormal = Vector2.Dot(currentCenter - currentNormal * extentNormal, currentNormal);
                        }

                        float cornerAlongWall = Vector2.Dot(wallHit.point, newNormal);
                        Vector2 cornerPoint = currentNormal * cornerAlongNormal + newNormal * cornerAlongWall;

                        float distToCorner = Vector2.Dot(cornerPoint - currentCenter, moveDir);

                        if (distToCorner > stepDist)
                        {
                            // Kante noch nicht erreicht: Normal vorwärts bis vor die Kante laufen
                            position += moveDir * stepDist;
                            remainingMove = 0f;
                        }
                        else
                        {
                            // Kante in diesem Frame erreicht: Um die Ecke wickeln
                            float distOnOldSurface = Mathf.Max(0f, distToCorner);
                            float pastCornerDist = stepDist - distOnOldSurface;

                            Vector2 newTangent = new Vector2(newNormal.y, -newNormal.x);
                            Vector2 newMoveDir = newTangent * moveSign;

                            float newExtentNormal = GetColliderExtent(-newNormal);
                            float newExtentTangent = GetColliderExtent(newMoveDir);

                            Vector2 targetCenter = cornerPoint 
                                + newNormal * newExtentNormal 
                                + newMoveDir * (newExtentTangent + pastCornerDist);

                            position = targetCenter - boxCollider.offset;

                            TargetSurfaceNormal = newNormal;
                            CurrentSurfaceNormal = newNormal;
                            IsGrounded = true;
                            remainingMove = 0f; // Restbewegung an neuer Wand mitgenommen
                        }
                    }
                    else
                    {
                        // Fallback: Keine Seitenwand gefunden
                        position += moveDir * stepDist;
                        remainingMove = 0f;
                    }
                }
            }

            // 3. HAFTUNG ZUR FLÄCHE (Surface Snap)
            Vector2 finalNormal = CurrentSurfaceNormal;
            Vector2 snapDir = -finalNormal;
            Vector2 snapOrigin = position + boxCollider.offset;
            float snapDist = Mathf.Max(0.18f, data.skinWidth * 8f);

            int groundHits = Physics2D.BoxCast(
                snapOrigin,
                boxSize,
                0f,
                snapDir,
                contactFilter,
                hitBuffer,
                snapDist
            );

            if (groundHits > 0)
            {
                RaycastHit2D bestGroundHit = hitBuffer[0];
                for (int i = 0; i < groundHits; i++)
                {
                    if (Vector2.Dot(hitBuffer[i].normal, finalNormal) > 0.5f)
                    {
                        bestGroundHit = hitBuffer[i];
                        break;
                    }
                }

                float actualSnapDist = Mathf.Max(0f, bestGroundHit.distance - data.skinWidth);
                position += snapDir * actualSnapDist;

                IsGrounded = true;
                TargetSurfaceNormal = bestGroundHit.normal;
                CurrentSurfaceNormal = bestGroundHit.normal;
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
            Vector2 castOriginY = position + boxCollider.offset;
            if (Mathf.Abs(deltaMovement.y) > 0.0001f)
            {
                float yDist = Mathf.Abs(deltaMovement.y) + data.skinWidth;
                float yDir = Mathf.Sign(deltaMovement.y);

                int yHits = Physics2D.BoxCast(
                    castOriginY,
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
            Vector2 castOriginX = position + boxCollider.offset;
            if (Mathf.Abs(deltaMovement.x) > 0.0001f)
            {
                float xDist = Mathf.Abs(deltaMovement.x) + data.skinWidth;
                float xDir = Mathf.Sign(deltaMovement.x);

                int xHits = Physics2D.BoxCast(
                    castOriginX,
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
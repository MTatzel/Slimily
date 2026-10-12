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

            // Bis zu 3 Iterationen, um bei schnellen Bewegungen auch mehrere Ecken im selben Frame zu packen
            int maxIterations = 3;
            while (remainingMove > 0.0001f && maxIterations > 0)
            {
                maxIterations--;

                Vector2 currentNormal = CurrentSurfaceNormal;
                Vector2 currentTangent = new Vector2(currentNormal.y, -currentNormal.x);
                Vector2 moveDir = currentTangent * moveSign;

                Vector2 castOrigin = position + boxCollider.offset;
                float castDist = remainingMove + data.skinWidth;

                // 1. INNENECKEN-CHECK (Frontal-Hindernis im Weg?)
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
                    // Innenecke gefunden
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

                // 2. AUSSENECKEN-CHECK (Bricht die Fläche unter uns weg?)
                float stepDist = remainingMove;
                Vector2 tentativePos = position + moveDir * stepDist;
                Vector2 tentativeCenter = tentativePos + boxCollider.offset;

                float extentNormal = GetColliderExtent(-currentNormal);
                float extentTangent = GetColliderExtent(moveDir);

                // Prüfen, ob der Mittelpunkt an der neuen Position noch Boden hat
                float floorCheckDist = extentNormal + data.skinWidth * 4f;
                RaycastHit2D groundProbe = Physics2D.Raycast(tentativeCenter, -currentNormal, floorCheckDist, data.groundLayer);

                if (groundProbe.collider != null && Vector2.Dot(groundProbe.normal, currentNormal) > 0.6f)
                {
                    // Normaler Vorwärtsschritt ohne Kantenabriss
                    position = tentativePos;
                    remainingMove = 0f;
                }
                else
                {
                    // Außenkante erkannt: Die Wand der neuen Fläche liegt in Richtung moveDir
                    Vector2 wallProbeOrigin = tentativeCenter - currentNormal * (extentNormal * 0.7f);
                    float probeBackDist = stepDist + extentTangent + data.skinWidth * 4f;

                    RaycastHit2D wallHit = Physics2D.Raycast(wallProbeOrigin, -moveDir, probeBackDist, data.groundLayer);

                    if (wallHit.collider != null && Vector2.Dot(wallHit.normal, moveDir) > 0.6f)
                    {
                        // Exakten Kantenpunkt C ermitteln
                        Vector2 currentCenter = position + boxCollider.offset;
                        RaycastHit2D currentFloorHit = Physics2D.Raycast(currentCenter, -currentNormal, floorCheckDist, data.groundLayer);

                        Vector2 cornerPoint;
                        if (currentFloorHit.collider != null)
                        {
                            float cornerAlongNormal = Vector2.Dot(currentFloorHit.point, currentNormal);
                            float cornerAlongMove = Vector2.Dot(wallHit.point, moveDir);
                            cornerPoint = currentNormal * cornerAlongNormal + moveDir * cornerAlongMove;
                        }
                        else
                        {
                            cornerPoint = wallHit.point;
                        }

                        // Distanz bis zur Kante berechnen
                        float distToCorner = Mathf.Max(0f, Vector2.Dot(cornerPoint - currentCenter, moveDir));
                        float pastCornerDist = Mathf.Max(0f, stepDist - distToCorner);

                        // Umheften auf die neue Wand
                        Vector2 newNormal = wallHit.normal;
                        Vector2 newTangent = new Vector2(newNormal.y, -newNormal.x);
                        Vector2 newMoveDir = newTangent * moveSign;

                        float newExtentNormal = GetColliderExtent(-newNormal);
                        float newExtentTangent = GetColliderExtent(newMoveDir);

                        // Slime exakt an der neuen Wand positionieren und Restweg nach unten mitnehmen
                        Vector2 targetCenter = cornerPoint 
                            + newNormal * newExtentNormal 
                            + newMoveDir * (newExtentTangent + pastCornerDist);

                        position = targetCenter - boxCollider.offset;

                        TargetSurfaceNormal = newNormal;
                        CurrentSurfaceNormal = newNormal;
                        HitWall = false;
                        remainingMove = 0f; // Restdistanz wurde im Wrap bereits mitgenommen
                    }
                    else
                    {
                        // Fallback: Keine Wand gefunden
                        position = tentativePos;
                        remainingMove = 0f;
                    }
                }
            }

            // 3. HAFTUNG ZUR FLÄCHE (Surface Snap)
            Vector2 finalNormal = CurrentSurfaceNormal;
            Vector2 snapDir = -finalNormal;
            Vector2 snapOrigin = position + boxCollider.offset;
            float snapDist = data.skinWidth * 4f;

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
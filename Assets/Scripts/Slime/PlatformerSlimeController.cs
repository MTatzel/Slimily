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
    private readonly Collider2D[] colliderBuffer = new Collider2D[8];
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
    public float CurrentTangentSpeed { get; set; }
    public bool IsGrounded { get; set; }
    public bool HitCeiling { get; private set; }
    public bool HitWall { get; private set; }
    public Vector2 TargetSurfaceNormal { get; set; } = Vector2.up;
    public Vector2 CurrentSurfaceNormal { get; set; } = Vector2.up;

    /// <summary>
    /// Tangentenvektor entlang der Oberfläche im Uhrzeigersinn (Clockwise).
    /// Input.x > 0 wandert entlang dieser Tangente vorwärts.
    /// </summary>
    public Vector2 SurfaceTangent => new Vector2(CurrentSurfaceNormal.y, -CurrentSurfaceNormal.x);

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
        MoveAndResolveCollisions();
    }

    public void TriggerDetach(float duration = -1f)
    {
        float dur = duration > 0f ? duration : (data != null ? data.detachCooldown : 0.15f);
        detachCooldownTimer = dur;
        IsGrounded = false;
        TargetSurfaceNormal = Vector2.up;
        CurrentTangentSpeed = 0f;
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

        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = gamepad.leftStick.ReadValue();
            if (Mathf.Abs(stick.x) > Mathf.Abs(input.x)) input.x = stick.x;
            if (Mathf.Abs(stick.y) > Mathf.Abs(input.y)) input.y = stick.y;

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                jumpBufferTimer = jumpBufferDuration;
            }

            if (gamepad.buttonSouth.isPressed)
            {
                JumpHeld = true;
            }
        }

        // Puffer herunterzählen
        if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        MoveInput = input;
    }

    /// <summary>
    /// Rotiert den Slime dynamisch so, dass transform.up exakt mit der Oberflächennormale übereinstimmt.
    /// </summary>
    private void UpdateSurfaceRotation()
    {
        Vector2 targetNormal = TargetSurfaceNormal.sqrMagnitude > 0.001f ? TargetSurfaceNormal.normalized : Vector2.up;

        // 2D-Winkel: Normal (0, 1) entspricht 0 Grad Drehung
        float targetAngle = Mathf.Atan2(targetNormal.y, targetNormal.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);

        if (data != null && data.alignRotationSpeed > 0f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, data.alignRotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = targetRotation;
        }

        rb.rotation = transform.eulerAngles.z;

        if (visualTransform != null)
        {
            visualTransform.localRotation = Quaternion.identity;
            float halfColHeight = boxCollider.size.y * 0.5f;
            Vector3 visualOffset = -Vector3.up * (halfColHeight - 0.26f);
            visualTransform.localPosition = (Vector3)boxCollider.offset + visualOffset;
        }
    }

    private void MoveAndResolveCollisions()
    {
        if (StateMachine.CurrentState == GroundedState)
        {
            MoveGrounded(CurrentTangentSpeed * Time.fixedDeltaTime);
        }
        else
        {
            MoveAirborne(Velocity * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Echtes 360°-Oberflächen-Movement:
    /// - Bewegung projiziert entlang der Tangente
    /// - Nahtloser Übergang bei Innenkanten (Forward Probes)
    /// - Enges Umwickeln von Außenkanten (Outer Corner Probes + Vertex Snapping)
    /// </summary>
    private void MoveGrounded(float deltaMove)
    {
        IsGrounded = false;
        HitCeiling = false;
        HitWall = false;

        Vector2 colSize = boxCollider.size;
        float colHalfWidth = colSize.x * 0.5f;
        float colHalfHeight = colSize.y * 0.5f;
        Vector2 boxCastSize = colSize - (Vector2.one * (data.skinWidth * 2f));
        Vector2 position = rb.position;

        // 1. Stand-Zustand (keine Vorwärtsbewegung)
        if (Mathf.Abs(deltaMove) < 0.0001f)
        {
            Vector2 normal = CurrentSurfaceNormal;
            Vector2 castOrigin = position + (Vector2)(transform.rotation * boxCollider.offset);
            float snapDist = colHalfHeight + data.surfaceSnapDistance;

            int downHits = Physics2D.BoxCast(castOrigin, boxCastSize, transform.eulerAngles.z, -normal, contactFilter, hitBuffer, snapDist);
            if (downHits > 0)
            {
                RaycastHit2D hit = hitBuffer[0];
                position = hit.point + hit.normal * (colHalfHeight + data.skinWidth);
                CurrentSurfaceNormal = hit.normal;
                TargetSurfaceNormal = hit.normal;
                IsGrounded = true;
            }
            else if (TrySnapToNearbySurface(ref position, colHalfHeight, out Vector2 foundNormal))
            {
                CurrentSurfaceNormal = foundNormal;
                TargetSurfaceNormal = foundNormal;
                IsGrounded = true;
            }
            else
            {
                IsGrounded = false;
                StateMachine.ChangeState(AirborneState);
                return;
            }

            rb.MovePosition(position);
            return;
        }

        // 2. Aktive Bewegung entlang der Oberfläche
        float remainingDistance = Mathf.Abs(deltaMove);
        float moveSign = Mathf.Sign(deltaMove);
        int maxIterations = 3;

        for (int iter = 0; iter < maxIterations && remainingDistance > 0.0001f; iter++)
        {
            Vector2 normal = CurrentSurfaceNormal;
            Vector2 tangent = new Vector2(normal.y, -normal.x);
            Vector2 moveDir = tangent * moveSign;

            Vector2 castOrigin = position + (Vector2)(transform.rotation * boxCollider.offset);
            float stepDist = remainingDistance + data.skinWidth;

            // Vorwärtsprobe (Innenkanten)
            int fwdHits = Physics2D.BoxCast(castOrigin, boxCastSize, transform.eulerAngles.z, moveDir, contactFilter, hitBuffer, stepDist);
            if (fwdHits > 0 && Vector2.Dot(hitBuffer[0].normal, moveDir) < -0.1f)
            {
                RaycastHit2D wallHit = hitBuffer[0];
                float travel = Mathf.Max(0f, wallHit.distance - data.skinWidth);
                position += moveDir * travel;
                remainingDistance -= travel;

                Vector2 newNormal = wallHit.normal;
                CurrentSurfaceNormal = newNormal;
                TargetSurfaceNormal = newNormal;
                HitWall = true;
                position = wallHit.point + newNormal * (colHalfHeight + data.skinWidth);
                continue;
            }

            // Entlang Tangente vorwärts bewegen
            position += moveDir * remainingDistance;
            remainingDistance = 0f;

            // Flächenhaftung & Außenkanten
            castOrigin = position + (Vector2)(transform.rotation * boxCollider.offset);
            float snapDist = colHalfHeight + data.surfaceSnapDistance;

            int downHits = Physics2D.BoxCast(castOrigin, boxCastSize, transform.eulerAngles.z, -normal, contactFilter, hitBuffer, snapDist);
            if (downHits > 0)
            {
                RaycastHit2D groundHit = hitBuffer[0];
                position = groundHit.point + groundHit.normal * (colHalfHeight + data.skinWidth);
                CurrentSurfaceNormal = groundHit.normal;
                TargetSurfaceNormal = groundHit.normal;
                IsGrounded = true;
            }
            else if (!TryResolveOuterCorner(ref position, moveDir, normal, colHalfWidth, colHalfHeight))
            {
                IsGrounded = false;
                StateMachine.ChangeState(AirborneState);
                break;
            }
        }

        rb.MovePosition(position);
    }
    private bool TryResolveOuterCorner(ref Vector2 position, Vector2 moveDir, Vector2 normal, float colHalfWidth, float colHalfHeight)
    {
        // Probe 1: Kanten-Rückwärtscast zur Seitenfläche (für 90°-Außenkanten)
        float probeFwd = colHalfWidth + data.skinWidth;
        float probeDown = colHalfHeight + data.cornerCheckDistance;
        Vector2 cornerOrigin = position + moveDir * probeFwd - normal * probeDown;
        Vector2 cornerDir = -moveDir;
        float cornerLen = probeFwd + data.cornerCheckDistance;

        int cornerHits = Physics2D.Raycast(cornerOrigin, cornerDir, contactFilter, hitBuffer, cornerLen);
        if (cornerHits > 0)
        {
            RaycastHit2D cHit = hitBuffer[0];
            Vector2 newNormal = cHit.normal;
            Vector2 cornerVertex = cHit.collider.ClosestPoint(position);

            Vector2 radial = position - cornerVertex;
            if (radial.sqrMagnitude < 0.0001f)
            {
                radial = (normal + moveDir).normalized;
            }
            Vector2 wrapDir = radial.normalized;

            // Eng an die Kante anschmiegen
            position = cornerVertex + wrapDir * (colHalfHeight + data.skinWidth);

            if (Vector2.Dot(wrapDir, newNormal) > 0.6f)
            {
                CurrentSurfaceNormal = newNormal;
                TargetSurfaceNormal = newNormal;
            }
            else
            {
                CurrentSurfaceNormal = wrapDir;
                TargetSurfaceNormal = newNormal;
            }

            IsGrounded = true;
            return true;
        }

        // Probe 2: Diagonaler Cast für unregelmäßige oder stumpfe Außenwinkel
        Vector2 diagDir = (-normal + moveDir).normalized;
        int diagHits = Physics2D.Raycast(position, diagDir, contactFilter, hitBuffer, colHalfHeight + data.cornerCheckDistance * 1.5f);
        if (diagHits > 0)
        {
            RaycastHit2D dHit = hitBuffer[0];
            Vector2 cornerVertex = dHit.collider.ClosestPoint(position);
            Vector2 wrapDir = (position - cornerVertex).normalized;
            if (wrapDir.sqrMagnitude < 0.0001f) wrapDir = dHit.normal;

            position = cornerVertex + wrapDir * (colHalfHeight + data.skinWidth);
            CurrentSurfaceNormal = dHit.normal;
            TargetSurfaceNormal = dHit.normal;
            IsGrounded = true;
            return true;
        }

        // Probe 3: Proximity Fallback im Radius
        if (TrySnapToNearbySurface(ref position, colHalfHeight, out Vector2 foundNormal))
        {
            CurrentSurfaceNormal = foundNormal;
            TargetSurfaceNormal = foundNormal;
            IsGrounded = true;
            return true;
        }

        return false;
    }

    private bool TrySnapToNearbySurface(ref Vector2 position, float colHalfHeight, out Vector2 foundNormal)
    {
        foundNormal = Vector2.up;
        int overlapCount = Physics2D.OverlapCircle(position, data.surfaceRadius, contactFilter, colliderBuffer);
        if (overlapCount > 0)
        {
            Collider2D col = colliderBuffer[0];
            Vector2 closest = col.ClosestPoint(position);
            Vector2 diff = position - closest;
            float dist = diff.magnitude;

            if (dist > 0.0001f && dist <= data.surfaceRadius)
            {
                foundNormal = diff / dist;
                position = closest + foundNormal * (colHalfHeight + data.skinWidth);
                return true;
            }
        }
        return false;
    }

    private void MoveAirborne(Vector2 deltaMovement)
    {
        IsGrounded = false;
        HitCeiling = false;
        HitWall = false;

        Vector2 boxSize = boxCollider.size - (Vector2.one * (data.skinWidth * 2f));
        Vector2 position = rb.position;
        Vector2 castOrigin = position + (Vector2)(transform.rotation * boxCollider.offset);

        // Y-Achse
        if (Mathf.Abs(deltaMovement.y) > 0.0001f)
        {
            float yDist = Mathf.Abs(deltaMovement.y) + data.skinWidth;
            float yDir = Mathf.Sign(deltaMovement.y);

            int yHits = Physics2D.BoxCast(
                castOrigin,
                boxSize,
                transform.eulerAngles.z,
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

                if (yDir > 0f) HitCeiling = true;

                if (CanAttach())
                {
                    TargetSurfaceNormal = hit.normal;
                    CurrentSurfaceNormal = hit.normal;
                    Velocity = Vector2.zero;
                    CurrentTangentSpeed = 0f;
                    IsGrounded = true;
                    rb.MovePosition(position);
                    StateMachine.ChangeState(GroundedState);
                    return;
                }
                else
                {
                    Velocity = new Vector2(Velocity.x, 0f);
                }
            }
            else
            {
                position.y += deltaMovement.y;
            }
        }

        // X-Achse
        castOrigin = position + (Vector2)(transform.rotation * boxCollider.offset);
        if (Mathf.Abs(deltaMovement.x) > 0.0001f)
        {
            float xDist = Mathf.Abs(deltaMovement.x) + data.skinWidth;
            float xDir = Mathf.Sign(deltaMovement.x);

            int xHits = Physics2D.BoxCast(
                castOrigin,
                boxSize,
                transform.eulerAngles.z,
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

                HitWall = true;

                if (CanAttach())
                {
                    TargetSurfaceNormal = hit.normal;
                    CurrentSurfaceNormal = hit.normal;
                    Velocity = Vector2.zero;
                    CurrentTangentSpeed = 0f;
                    IsGrounded = true;
                    rb.MovePosition(position);
                    StateMachine.ChangeState(GroundedState);
                    return;
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

        rb.MovePosition(position);
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = IsGrounded ? Color.green : Color.cyan;
        Vector2 center = (Vector2)transform.position + (Vector2)(transform.rotation * col.offset);
        Gizmos.DrawWireCube(center, col.size);

        if (IsGrounded)
        {
            // Normale (Grün)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(center, CurrentSurfaceNormal * 0.8f);

            // Tangente im Uhrzeigersinn (Gelb)
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(center, SurfaceTangent * 0.8f);
        }

        if (data != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.2f);
            Gizmos.DrawWireSphere(center, data.surfaceRadius);
        }
    }
}
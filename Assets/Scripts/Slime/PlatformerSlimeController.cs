using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

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
    // --- Meditations- & Interaktionsstatus ---
    public bool IsViewingMap { get; set; }

    private float detachCooldownTimer;
    private Vector2 lastDetachedNormal = Vector2.zero;

    [Header("Juice & Buffers")]
    [SerializeField] private float jumpBufferDuration = 0.12f;
    private float jumpBufferTimer;

    [Header("Controller Settings")]
    [SerializeField] private float stickDeadzone = 0.2f;

    public bool HasBufferedJump => jumpBufferTimer > 0f;

    // --- Meditations- & Interaktionsstatus ---
    public bool IsMeditating { get; set; }

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
        // Ohne deprecated Parameter -> Warnung weg
        Tilemap[] allTilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        foreach (var tm in allTilemaps)
        {
            tm.RefreshAllTiles();
        }

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

    public void TriggerDetach(float duration = 0.15f)
    {
        TriggerDetach(CurrentSurfaceNormal, duration);
    }

    public void TriggerDetach(Vector2 fromSurfaceNormal, float duration = 0.15f)
    {
        detachCooldownTimer = duration;
        lastDetachedNormal = SnapToCardinal(fromSurfaceNormal);

        IsGrounded = false;
        TargetSurfaceNormal = Vector2.up;
        CurrentSurfaceNormal = Vector2.up;

        rb.position += lastDetachedNormal * (data.skinWidth * 3f + 0.03f);
    }

    public bool CanAttach() => detachCooldownTimer <= 0f;

    public bool CanAttachTo(Vector2 candidateNormal)
    {
        if (detachCooldownTimer <= 0f) return true;

        Vector2 snappedCandidate = SnapToCardinal(candidateNormal);
        return Vector2.Dot(snappedCandidate, lastDetachedNormal) < 0.5f;
    }

    public float GetColliderExtent(Vector2 direction)
    {
        Vector2 halfSize = boxCollider.size * 0.5f;
        return Mathf.Abs(direction.x) * halfSize.x + Mathf.Abs(direction.y) * halfSize.y;
    }

    private Vector2 SnapToCardinal(Vector2 rawNormal)
    {
        if (Mathf.Abs(rawNormal.x) > Mathf.Abs(rawNormal.y))
        {
            return new Vector2(Mathf.Sign(rawNormal.x), 0f);
        }
        return new Vector2(0f, Mathf.Sign(rawNormal.y));
    }

    private bool FindFrontalWallHit(
        Vector2 currentCenter,
        Vector2 currentNormal,
        Vector2 moveDir,
        float extentNormal,
        float extentTangent,
        float castDist,
        out float distanceToWall,
        out Vector2 wallNormal)
    {
        distanceToWall = float.MaxValue;
        wallNormal = Vector2.zero;
        bool found = false;

        float rayLength = extentTangent + castDist;
        Vector2 rayOriginBase = currentCenter - currentNormal * (extentNormal - data.skinWidth * 2f);
        Vector2 rayOriginMid = currentCenter;

        RaycastHit2D hitBase = Physics2D.Raycast(rayOriginBase, moveDir, rayLength, data.groundLayer);
        if (hitBase.collider != null && hitBase.distance > 0.0005f && Vector2.Dot(hitBase.normal, moveDir) < -0.35f)
        {
            float d = Mathf.Max(0f, hitBase.distance - extentTangent);
            if (d < distanceToWall)
            {
                distanceToWall = d;
                wallNormal = SnapToCardinal(hitBase.normal);
                found = true;
            }
        }

        RaycastHit2D hitMid = Physics2D.Raycast(rayOriginMid, moveDir, rayLength, data.groundLayer);
        if (hitMid.collider != null && hitMid.distance > 0.0005f && Vector2.Dot(hitMid.normal, moveDir) < -0.35f)
        {
            float d = Mathf.Max(0f, hitMid.distance - extentTangent);
            if (d < distanceToWall)
            {
                distanceToWall = d;
                wallNormal = SnapToCardinal(hitMid.normal);
                found = true;
            }
        }

        float normalClearance = 0.05f;
        Vector2 liftedOrigin = currentCenter + currentNormal * (normalClearance * 0.5f);
        Vector2 shrunkBoxSize = boxCollider.size - (Vector2.one * (data.skinWidth * 2f))
            - new Vector2(Mathf.Abs(currentNormal.x), Mathf.Abs(currentNormal.y)) * normalClearance;

        int hits = Physics2D.BoxCast(
            liftedOrigin,
            shrunkBoxSize,
            0f,
            moveDir,
            contactFilter,
            hitBuffer,
            castDist
        );

        for (int i = 0; i < hits; i++)
        {
            if (hitBuffer[i].distance > 0.0005f && Vector2.Dot(hitBuffer[i].normal, moveDir) < -0.35f)
            {
                if (hitBuffer[i].distance < distanceToWall)
                {
                    distanceToWall = hitBuffer[i].distance;
                    wallNormal = SnapToCardinal(hitBuffer[i].normal);
                    found = true;
                }
            }
        }

        return found;
    }

    private void ReadInput()
    {
        // Wenn meditiert wird oder die Karte offen ist: Keine Bewegungs-/Sprungeingaben
        if (IsMeditating || IsViewingMap)
        {
            MoveInput = Vector2.zero;
            jumpBufferTimer = 0f;
            JumpHeld = false;
            return;
        }

        Vector2 input = Vector2.zero;
        bool jumpPressedThisFrame = false;
        bool jumpIsHeld = false;

        // --- 1. TASTATUR ---
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;

            if (keyboard.spaceKey.wasPressedThisFrame) jumpPressedThisFrame = true;
            if (keyboard.spaceKey.isPressed) jumpIsHeld = true;
        }

        // --- 2. GAMEPAD / PS5 DUALSENSE ---
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            Vector2 stick = Vector2.zero;
            Vector2 rawStick = gamepad.leftStick.ReadValue();

            if (rawStick.magnitude >= stickDeadzone)
            {
                Vector2 stickDir = rawStick.normalized;
                float stickMag = Mathf.Clamp01((rawStick.magnitude - stickDeadzone) / (1f - stickDeadzone));

                // Achsen-Snapping gegen Schräglauf-Rauschen
                if (Mathf.Abs(stickDir.x) > 0.7f)
                {
                    stick = new Vector2(Mathf.Sign(stickDir.x) * stickMag, 0f);
                }
                else if (Mathf.Abs(stickDir.y) > 0.7f)
                {
                    stick = new Vector2(0f, Mathf.Sign(stickDir.y) * stickMag);
                }
                else
                {
                    stick = stickDir * stickMag;
                }
            }

            Vector2 dpad = gamepad.dpad.ReadValue();

            // Gamepad-Eingaben zur Tastatur addieren statt überschreiben
            if (dpad.sqrMagnitude > 0.1f)
            {
                input += dpad;
            }
            else if (stick.sqrMagnitude > 0.01f)
            {
                input += stick;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame) jumpPressedThisFrame = true;
            if (gamepad.buttonSouth.isPressed) jumpIsHeld = true;
        }

        // Maximale Eingabe begrenzen
        input.x = Mathf.Clamp(input.x, -1f, 1f);
        input.y = Mathf.Clamp(input.y, -1f, 1f);

        // Sprung-Puffer aktualisieren
        if (jumpPressedThisFrame)
        {
            jumpBufferTimer = jumpBufferDuration;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        JumpHeld = jumpIsHeld;
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

                Vector2 currentNormal = SnapToCardinal(CurrentSurfaceNormal);
                Vector2 currentTangent = new Vector2(currentNormal.y, -currentNormal.x);
                Vector2 moveDir = currentTangent * moveSign;

                Vector2 currentCenter = position + boxCollider.offset;
                float extentNormal = GetColliderExtent(-currentNormal);
                float extentTangent = GetColliderExtent(moveDir);

                // 1. INNENECKEN- & WAND-CHECK
                float lookAheadDist = remainingMove + extentTangent + data.skinWidth * 2f;

                if (FindFrontalWallHit(
                    currentCenter,
                    currentNormal,
                    moveDir,
                    extentNormal,
                    extentTangent,
                    lookAheadDist,
                    out float hitDist,
                    out Vector2 hitWallNormal))
                {
                    float actualDistToWall = Mathf.Max(0f, hitDist - data.skinWidth);

                    if (actualDistToWall <= remainingMove + 0.005f)
                    {
                        position += moveDir * actualDistToWall;
                        remainingMove = Mathf.Max(0f, remainingMove - actualDistToWall);

                        bool isInnerCorner = Mathf.Abs(Vector2.Dot(currentNormal, hitWallNormal)) < 0.35f;

                        if (isInnerCorner)
                        {
                            TargetSurfaceNormal = hitWallNormal;
                            CurrentSurfaceNormal = hitWallNormal;
                            HitWall = true;
                        }
                        else
                        {
                            HitWall = true;
                            Velocity = Vector2.zero;
                            remainingMove = 0f;
                        }
                    }
                    else
                    {
                        position += moveDir * remainingMove;
                        remainingMove = 0f;
                    }

                    continue;
                }

                // 2. AUSSENECKEN-CHECK
                float stepDist = remainingMove;
                Vector2 tentativePos = position + moveDir * stepDist;
                Vector2 tentativeCenter = tentativePos + boxCollider.offset;

                Vector2 leadingEdge = tentativeCenter + moveDir * extentTangent;
                float floorCheckDist = extentNormal + Mathf.Max(0.25f, data.skinWidth * 8f);

                RaycastHit2D floorCheck = Physics2D.Raycast(leadingEdge, -currentNormal, floorCheckDist, data.groundLayer);

                if (floorCheck.collider != null && Vector2.Dot(SnapToCardinal(floorCheck.normal), currentNormal) > 0.6f)
                {
                    position = tentativePos;
                    remainingMove = 0f;
                }
                else
                {
                    float probeDepth = Mathf.Min(0.2f, extentNormal * 0.5f);
                    Vector2 probeOrigin = leadingEdge - currentNormal * (extentNormal + probeDepth);
                    float probeDist = extentTangent + stepDist + 0.6f;

                    RaycastHit2D wallHit = Physics2D.Raycast(probeOrigin, -moveDir, probeDist, data.groundLayer);

                    if (wallHit.collider != null && Vector2.Dot(SnapToCardinal(wallHit.normal), moveDir) > 0.5f)
                    {
                        Vector2 newNormal = SnapToCardinal(wallHit.normal);

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
                            position += moveDir * stepDist;
                            remainingMove = 0f;
                        }
                        else
                        {
                            float distOnOldSurface = Mathf.Max(0f, distToCorner);
                            float pastCornerDist = stepDist - distOnOldSurface;

                            Vector2 newTangent = new Vector2(newNormal.y, -newNormal.x);
                            Vector2 newMoveDir = newTangent * moveSign;

                            float newExtentNormal = GetColliderExtent(-newNormal);
                            float newExtentTangent = GetColliderExtent(newMoveDir);

                            Vector2 targetCenter = cornerPoint 
                                + newNormal * (newExtentNormal + data.skinWidth) 
                                + newMoveDir * (newExtentTangent + pastCornerDist);

                            position = targetCenter - boxCollider.offset;

                            TargetSurfaceNormal = newNormal;
                            CurrentSurfaceNormal = newNormal;
                            IsGrounded = true;
                            remainingMove = 0f;
                        }
                    }
                    else
                    {
                        position += moveDir * stepDist;
                        remainingMove = 0f;
                    }
                }
            }

            // 3. HAFTUNG ZUR FLÄCHE (Surface Snap)
            Vector2 finalNormal = SnapToCardinal(CurrentSurfaceNormal);
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
                int bestIndex = -1;
                for (int i = 0; i < groundHits; i++)
                {
                    if (hitBuffer[i].distance > 0.0001f && Vector2.Dot(SnapToCardinal(hitBuffer[i].normal), finalNormal) > 0.7f)
                    {
                        bestIndex = i;
                        break;
                    }
                }

                if (bestIndex >= 0)
                {
                    RaycastHit2D bestGroundHit = hitBuffer[bestIndex];
                    float actualSnapDist = Mathf.Max(0f, bestGroundHit.distance - data.skinWidth);
                    position += snapDir * actualSnapDist;

                    IsGrounded = true;
                    TargetSurfaceNormal = finalNormal;
                    CurrentSurfaceNormal = finalNormal;
                }
                else
                {
                    IsGrounded = true;
                    TargetSurfaceNormal = finalNormal;
                    CurrentSurfaceNormal = finalNormal;
                }
            }
            else
            {
                RaycastHit2D centerGroundHit = Physics2D.Raycast(snapOrigin, snapDir, GetColliderExtent(snapDir) + snapDist, data.groundLayer);
                if (centerGroundHit.collider != null && Vector2.Dot(SnapToCardinal(centerGroundHit.normal), finalNormal) > 0.7f)
                {
                    float dist = Mathf.Max(0f, centerGroundHit.distance - GetColliderExtent(snapDir) - data.skinWidth);
                    position += snapDir * dist;
                    IsGrounded = true;
                    TargetSurfaceNormal = finalNormal;
                    CurrentSurfaceNormal = finalNormal;
                }
                else
                {
                    IsGrounded = false;
                }
            }
        }
        else
        {
            // --- FLUG-MODUS (Airborne) ---

            // Y-Achse
            Vector2 castOriginY = position + boxCollider.offset;
            if (Mathf.Abs(deltaMovement.y) > 0.0001f)
            {
                float yDist = Mathf.Abs(deltaMovement.y) + data.skinWidth;
                float yDir = Mathf.Sign(deltaMovement.y);
                Vector2 moveDirY = Vector2.up * yDir;

                int yHits = Physics2D.BoxCast(
                    castOriginY,
                    boxSize,
                    0f,
                    moveDirY,
                    contactFilter,
                    hitBuffer,
                    yDist
                );

                int validHitIndex = -1;
                for (int i = 0; i < yHits; i++)
                {
                    if (Vector2.Dot(hitBuffer[i].normal, moveDirY) < -0.3f && hitBuffer[i].distance > 0.0005f)
                    {
                        validHitIndex = i;
                        break;
                    }
                }

                if (validHitIndex >= 0)
                {
                    RaycastHit2D hit = hitBuffer[validHitIndex];
                    float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);
                    position.y += yDir * actualDist;

                    Vector2 snappedNormal = SnapToCardinal(hit.normal);

                    if (CanAttachTo(snappedNormal))
                    {
                        TargetSurfaceNormal = snappedNormal;
                        CurrentSurfaceNormal = snappedNormal;
                        Velocity = Vector2.zero;
                        IsGrounded = true;
                        StateMachine.ChangeState(GroundedState);
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
            Vector2 castOriginX = position + boxCollider.offset;
            if (Mathf.Abs(deltaMovement.x) > 0.0001f)
            {
                float xDist = Mathf.Abs(deltaMovement.x) + data.skinWidth;
                float xDir = Mathf.Sign(deltaMovement.x);
                Vector2 moveDirX = Vector2.right * xDir;

                int xHits = Physics2D.BoxCast(
                    castOriginX,
                    boxSize,
                    0f,
                    moveDirX,
                    contactFilter,
                    hitBuffer,
                    xDist
                );

                int validHitIndex = -1;
                for (int i = 0; i < xHits; i++)
                {
                    if (Vector2.Dot(hitBuffer[i].normal, moveDirX) < -0.3f && hitBuffer[i].distance > 0.0005f)
                    {
                        validHitIndex = i;
                        break;
                    }
                }

                if (validHitIndex >= 0)
                {
                    RaycastHit2D hit = hitBuffer[validHitIndex];
                    float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);
                    position.x += xDir * actualDist;

                    Vector2 snappedNormal = SnapToCardinal(hit.normal);

                    if (CanAttachTo(snappedNormal))
                    {
                        HitWall = true;
                        TargetSurfaceNormal = snappedNormal;
                        CurrentSurfaceNormal = snappedNormal;
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

/// <summary>
    /// Schnittstelle für Fallen/Gegner: Bricht Meditation oder Karte sofort ab.
    /// </summary>
    public void TakeDamage()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.InterruptMeditation();

            // Falls die Karte offen ist: Sofort schließen und zurück zum Slime
            if (GameManager.Instance.currentState == GameState.MapView)
            {
                GameManager.Instance.ChangeState(GameState.FreeRoam);
            }
        }

        // Hier folgen später Lebenspunkte-Abzug oder Respawn
    }
}
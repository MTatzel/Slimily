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

    // --- Modulare Sub-Komponenten ---
    public SlimeInputHandler InputHandler { get; private set; }
    public SlimeAbilities Abilities { get; private set; }
    public SlimeVisuals Visuals { get; private set; }

    // --- State Machine & Zustände ---
    public PlatformerStateMachine StateMachine { get; private set; }
    public PlatformerGroundedState GroundedState { get; private set; }
    public PlatformerAirborneState AirborneState { get; private set; }
    public PlatformerDashState DashState { get; private set; }

    // --- Weitergeleitete Input-Properties (für Abwärtskompatibilität) ---
    public Vector2 MoveInput => InputHandler != null ? InputHandler.MoveInput : Vector2.zero;
    public bool JumpHeld => InputHandler != null && InputHandler.JumpHeld;
    public bool HasBufferedJump => InputHandler != null && InputHandler.HasBufferedJump;

    // --- Kollisions- & Orientierungsstatus ---
    public Vector2 Velocity { get; set; }
    public bool IsGrounded { get; set; }
    public bool HitCeiling { get; private set; }
    public bool HitWall { get; private set; }
    public Vector2 TargetSurfaceNormal { get; set; } = Vector2.up;
    public Vector2 CurrentSurfaceNormal { get; private set; } = Vector2.up;

    // --- Äußere & Innere Ecken Status ---
    public bool IsOnOuterCorner { get; private set; }
    public Vector2 OuterCornerVertex { get; private set; }
    public Vector2 OuterCornerNormalA { get; private set; } = Vector2.up;
    public Vector2 OuterCornerNormalB { get; private set; } = Vector2.right;
    [Range(0f, 1f)] public float OuterCornerProgress { get; private set; }

    public bool IsInInnerCornerPocket { get; private set; }
    public Vector2 InnerCornerAdjacentNormal { get; private set; } = Vector2.zero;
    private float innerCornerSlowdownTimer = 0f;

    public Vector2 EffectiveSurfaceNormal
    {
        get
        {
            if (IsOnOuterCorner)
            {
                float angleRad = Mathf.Clamp01(OuterCornerProgress) * Mathf.PI * 0.5f;
                return (OuterCornerNormalA * Mathf.Cos(angleRad) + OuterCornerNormalB * Mathf.Sin(angleRad)).normalized;
            }
            return CurrentSurfaceNormal;
        }
    }

    public Vector2 OuterCornerDiagonalNormal => (OuterCornerNormalA + OuterCornerNormalB).normalized;

    public Vector2 OuterCornerDiagonalTangent
    {
        get
        {
            Vector2 diag = OuterCornerDiagonalNormal;
            return new Vector2(diag.y, -diag.x);
        }
    }

    public float CornerSpeedMultiplier
    {
        get
        {
            if (IsOnOuterCorner)
            {
                return (data != null && data.outerCornerSpeedMultiplier > 0.1f) ? data.outerCornerSpeedMultiplier : 0.78f;
            }
            if (innerCornerSlowdownTimer > 0f)
            {
                return (data != null && data.innerCornerSpeedMultiplier > 0.1f) ? data.innerCornerSpeedMultiplier : 0.82f;
            }
            return 1f;
        }
    }

    // --- Meditations- & Interaktionsstatus ---
    public bool IsViewingMap { get; set; }
    public bool IsMeditating { get; set; }

    private float detachCooldownTimer;
    private Vector2 lastDetachedNormal = Vector2.zero;
    private Vector2 secondaryDetachedNormal = Vector2.zero;

    [Header("Juice & Buffers")]
    [SerializeField] private float jumpBufferDuration = 0.12f;
    private float coyoteTimer;

    [Header("Treffer & Unverwundbarkeit")]
    [SerializeField] private float damageInvincibilityDuration = 0.75f;
    [SerializeField] private bool enableDebugDamageKeyK = true;
    private float invincibilityTimer = 0f;

    [Header("Controller Settings")]
    [SerializeField] private float stickDeadzone = 0.2f;

    public bool HasCoyoteTime => coyoteTimer > 0f;
    public bool IsInvincible => invincibilityTimer > 0f;

    public void ConsumeJumpBuffer()
    {
        if (InputHandler != null) InputHandler.ConsumeJumpBuffer();
        coyoteTimer = 0f;
    }

    public void ApplySquashAndStretch(float scaleX, float scaleY)
    {
        if (Visuals != null) Visuals.ApplySquashAndStretch(scaleX, scaleY);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Modulare Komponenten automatisch holen oder anlegen
        InputHandler = GetComponent<SlimeInputHandler>() ?? gameObject.AddComponent<SlimeInputHandler>();
        Abilities = GetComponent<SlimeAbilities>() ?? gameObject.AddComponent<SlimeAbilities>();
        Visuals = GetComponent<SlimeVisuals>() ?? gameObject.AddComponent<SlimeVisuals>();

        InputHandler.Initialize(stickDeadzone, jumpBufferDuration);
        Abilities.Initialize(data);
        Visuals.Initialize(this, visualTransform, boxCollider, data);

        contactFilter = new ContactFilter2D
        {
            layerMask = data.groundLayer,
            useLayerMask = true,
            useTriggers = false
        };

        StateMachine = new PlatformerStateMachine();
        GroundedState = new PlatformerGroundedState(this, StateMachine, data);
        AirborneState = new PlatformerAirborneState(this, StateMachine, data);
        DashState = new PlatformerDashState(this, StateMachine, data);
    }

    private void Start()
    {
        Tilemap[] allTilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
        foreach (var tm in allTilemaps)
        {
            tm.RefreshAllTiles();
        }

        StateMachine.Initialize(AirborneState);
    }

    private void OnDisable()
    {
        if (Visuals != null) Visuals.ResetVisuals();
    }

    private void Update()
    {
        if (enableDebugDamageKeyK && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame)
        {
            TakeDamage();
            if (!enabled) return;
        }

        InputHandler.ReadInput(IsMeditating || IsViewingMap);
        Abilities.TickTimers(Time.deltaTime);

        if (IsGrounded)
        {
            coyoteTimer = (data != null && data.coyoteTimeDuration > 0f) ? data.coyoteTimeDuration : 0.1f;
        }
        else if (coyoteTimer > 0f)
        {
            coyoteTimer -= Time.deltaTime;
        }

        if (innerCornerSlowdownTimer > 0f)
        {
            innerCornerSlowdownTimer -= Time.deltaTime;
        }

        if (detachCooldownTimer > 0f)
        {
            detachCooldownTimer -= Time.deltaTime;
        }

        if (invincibilityTimer > 0f)
        {
            invincibilityTimer -= Time.deltaTime;
        }

        StateMachine.Update();
        Visuals.UpdateVisuals(invincibilityTimer);
    }

    private void FixedUpdate()
    {
        StateMachine.FixedUpdate();
        MoveAndResolveCollisions(Velocity * Time.fixedDeltaTime);
    }

    public void ZeroOutMomentum()
    {
        Velocity = Vector2.zero;
        if (InputHandler != null) InputHandler.ClearAllInputs();
        if (Abilities != null)
        {
            Abilities.IsDashing = false;
            Abilities.IsSprinting = false;
            Abilities.IsBalloonFloating = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (Visuals != null)
        {
            Visuals.ResetVisuals();
        }
    }

    public void ClearCornerState()
    {
        IsOnOuterCorner = false;
        OuterCornerProgress = 0f;
        IsInInnerCornerPocket = false;
        InnerCornerAdjacentNormal = Vector2.zero;
    }

    public void TriggerDetach(float duration = 0.15f)
    {
        if (IsOnOuterCorner)
        {
            TriggerCornerDetach(OuterCornerNormalA, OuterCornerNormalB, duration);
        }
        else
        {
            TriggerDetach(CurrentSurfaceNormal, duration);
        }
    }

    public void TriggerDetach(Vector2 fromSurfaceNormal, float duration = 0.15f)
    {
        detachCooldownTimer = duration;
        lastDetachedNormal = SnapToCardinal(fromSurfaceNormal);
        secondaryDetachedNormal = IsInInnerCornerPocket ? InnerCornerAdjacentNormal : Vector2.zero;

        ClearCornerState();
        IsGrounded = false;
        TargetSurfaceNormal = Vector2.up;
        CurrentSurfaceNormal = Vector2.up;

        rb.position += fromSurfaceNormal.normalized * (data.skinWidth * 3f + 0.035f);
    }

    public void TriggerCornerDetach(Vector2 normalA, Vector2 normalB, float duration = 0.16f)
    {
        detachCooldownTimer = duration;
        lastDetachedNormal = SnapToCardinal(normalA);
        secondaryDetachedNormal = SnapToCardinal(normalB);

        Vector2 pushDir = EffectiveSurfaceNormal;
        ClearCornerState();
        IsGrounded = false;
        TargetSurfaceNormal = Vector2.up;
        CurrentSurfaceNormal = Vector2.up;

        rb.position += pushDir * (data.skinWidth * 4f + 0.045f);
    }

    /// <summary>
    /// Prüft unmittelbar nach einem Boden-Dash, ob direkt unter dem Slime eine Fläche zum nahtlosen Weiterrennen liegt.
    /// </summary>
    public void TrySnapToNearbySurfaceAfterDash()
    {
        Vector2 boxSize = boxCollider.size - (Vector2.one * (data.skinWidth * 2f));
        Vector2 origin = rb.position + boxCollider.offset;
        Vector2 checkNormal = SnapToCardinal(CurrentSurfaceNormal);
        Vector2 snapDir = -checkNormal;

        int hits = Physics2D.BoxCast(origin, boxSize, 0f, snapDir, contactFilter, hitBuffer, 0.22f);
        for (int i = 0; i < hits; i++)
        {
            Vector2 hitNormal = SnapToCardinal(hitBuffer[i].normal);
            if (hitBuffer[i].distance > 0.0001f && CanAttachTo(hitNormal))
            {
                float dist = Mathf.Max(0f, hitBuffer[i].distance - data.skinWidth);
                rb.position += snapDir * dist;
                CurrentSurfaceNormal = hitNormal;
                TargetSurfaceNormal = hitNormal;
                IsGrounded = true;
                return;
            }
        }
    }

    public bool CanAttach() => detachCooldownTimer <= 0f;

    public bool CanAttachTo(Vector2 candidateNormal)
    {
        if (detachCooldownTimer <= 0f) return true;

        Vector2 snappedCandidate = SnapToCardinal(candidateNormal);
        if (Vector2.Dot(snappedCandidate, lastDetachedNormal) > 0.5f) return false;
        if (secondaryDetachedNormal.sqrMagnitude > 0.1f && Vector2.Dot(snappedCandidate, secondaryDetachedNormal) > 0.5f) return false;

        return true;
    }

    public float GetColliderExtent(Vector2 direction)
    {
        Vector2 halfSize = boxCollider.size * 0.5f;
        return Mathf.Abs(direction.x) * halfSize.x + Mathf.Abs(direction.y) * halfSize.y;
    }

    public Vector2 SnapToCardinal(Vector2 rawNormal)
    {
        if (Mathf.Abs(rawNormal.x) > Mathf.Abs(rawNormal.y))
        {
            return new Vector2(Mathf.Sign(rawNormal.x), 0f);
        }
        return new Vector2(0f, Mathf.Sign(rawNormal.y));
    }

    private Vector2 CalculateOuterCornerCenterPosition(Vector2 vertex, Vector2 normalA, Vector2 normalB, float progress)
    {
        float clampedT = Mathf.Clamp01(progress);
        float angleRad = clampedT * Mathf.PI * 0.5f;

        float curvePower = (data != null && data.outerCornerCurvePower > 0.3f) ? data.outerCornerCurvePower : 0.82f;
        float cosVal = Mathf.Clamp01(Mathf.Cos(angleRad));
        float sinVal = Mathf.Clamp01(Mathf.Sin(angleRad));

        float weightA = Mathf.Pow(cosVal, curvePower);
        float weightB = Mathf.Pow(sinVal, curvePower);

        float extentA = GetColliderExtent(-normalA) + data.skinWidth;
        float extentB = GetColliderExtent(-normalB) + data.skinWidth;

        return vertex + normalA * (weightA * extentA) + normalB * (weightB * extentB);
    }

    private bool TryFindAdjacentOuterCornerWall(
        Vector2 currentCenter,
        Vector2 tentativeCenter,
        Vector2 currentNormal,
        Vector2 moveDir,
        float extentNormal,
        float extentTangent,
        float stepDist,
        float floorCheckDist,
        out Vector2 cornerPoint,
        out Vector2 newNormal)
    {
        cornerPoint = Vector2.zero;
        newNormal = Vector2.zero;

        float[] depthFactors = { 0.25f, 0.50f, 0.12f };
        float probeForwardOffset = Mathf.Max(0.12f, extentTangent * 0.55f);
        float probeDist = probeForwardOffset + extentTangent + stepDist + 0.5f;

        RaycastHit2D validWallHit = default;
        bool foundWall = false;

        for (int i = 0; i < depthFactors.Length; i++)
        {
            float probeDepth = Mathf.Max(0.05f, extentNormal * depthFactors[i]);
            Vector2 probeOrigin = tentativeCenter + moveDir * probeForwardOffset - currentNormal * (extentNormal + probeDepth);

            RaycastHit2D wallHit = Physics2D.Raycast(probeOrigin, -moveDir, probeDist, data.groundLayer);
            if (wallHit.collider != null && wallHit.distance > 0.0005f && Vector2.Dot(SnapToCardinal(wallHit.normal), moveDir) > 0.5f)
            {
                validWallHit = wallHit;
                foundWall = true;
                break;
            }
        }

        if (!foundWall) return false;

        newNormal = SnapToCardinal(validWallHit.normal);

        RaycastHit2D safeFloorHit = Physics2D.Raycast(
            currentCenter - moveDir * (extentTangent * 0.65f),
            -currentNormal,
            floorCheckDist,
            data.groundLayer
        );

        float cornerAlongNormal = (safeFloorHit.collider != null && safeFloorHit.distance > 0.0005f)
            ? Vector2.Dot(safeFloorHit.point, currentNormal)
            : Vector2.Dot(currentCenter - currentNormal * (extentNormal + data.skinWidth), currentNormal);

        float cornerAlongWall = Vector2.Dot(validWallHit.point, newNormal);
        cornerPoint = currentNormal * cornerAlongNormal + newNormal * cornerAlongWall;
        return true;
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

    private bool TryDetectOuterCornerUnderneath(
        Vector2 center,
        Vector2 surfaceNormal,
        out Vector2 cornerVertex,
        out Vector2 adjacentWallNormal,
        out float initialProgress)
    {
        cornerVertex = Vector2.zero;
        adjacentWallNormal = Vector2.zero;
        initialProgress = 0f;

        float extentNormal = GetColliderExtent(-surfaceNormal);
        float floorDist = extentNormal + Mathf.Max(0.25f, data.skinWidth * 8f);

        RaycastHit2D centerFloorHit = Physics2D.Raycast(center, -surfaceNormal, floorDist, data.groundLayer);
        if (centerFloorHit.collider != null && centerFloorHit.distance > 0.0005f && Vector2.Dot(SnapToCardinal(centerFloorHit.normal), surfaceNormal) > 0.6f)
        {
            return false;
        }

        Vector2 tangentCW = new Vector2(surfaceNormal.y, -surfaceNormal.x);
        float extentTangent = GetColliderExtent(tangentCW);

        int[] signs = { 1, -1 };
        foreach (int sign in signs)
        {
            Vector2 overhangDir = tangentCW * sign;
            Vector2 supportSideOrigin = center - overhangDir * (extentTangent * 0.85f);

            RaycastHit2D supportHit = Physics2D.Raycast(supportSideOrigin, -surfaceNormal, floorDist, data.groundLayer);
            if (supportHit.collider != null && supportHit.distance > 0.0005f && Vector2.Dot(SnapToCardinal(supportHit.normal), surfaceNormal) > 0.6f)
            {
                if (TryFindAdjacentOuterCornerWall(
                    supportSideOrigin,
                    center,
                    surfaceNormal,
                    overhangDir,
                    extentNormal,
                    extentTangent,
                    0.05f,
                    floorDist,
                    out cornerVertex,
                    out adjacentWallNormal))
                {
                    float overhangAmount = Mathf.Max(0f, Vector2.Dot(center - cornerVertex, overhangDir));
                    initialProgress = Mathf.Clamp(overhangAmount / Mathf.Max(0.1f, extentTangent), 0.08f, 0.50f);
                    return true;
                }
            }
        }

        return false;
    }

    private void MoveAndResolveCollisions(Vector2 deltaMovement)
    {
        IsGrounded = false;
        HitCeiling = false;
        HitWall = false;
        IsInInnerCornerPocket = false;
        InnerCornerAdjacentNormal = Vector2.zero;

        Vector2 boxSize = boxCollider.size - (Vector2.one * (data.skinWidth * 2f));
        Vector2 position = rb.position;

        bool isGroundedMode = StateMachine.CurrentState == GroundedState;

        if (isGroundedMode)
        {
            float moveAmount = deltaMovement.x;
            float remainingMove = Mathf.Abs(moveAmount);
            float moveSign = Mathf.Sign(moveAmount);

            int maxIterations = 4;
            while (remainingMove > 0.0001f && maxIterations > 0)
            {
                maxIterations--;

                if (IsOnOuterCorner)
                {
                    Vector2 tangentA = new Vector2(OuterCornerNormalA.y, -OuterCornerNormalA.x);
                    float dirFromAToB = Mathf.Sign(Vector2.Dot(tangentA, OuterCornerNormalB));
                    float progressDir = moveSign * dirFromAToB;

                    float arcLength = (data != null && data.outerCornerArcLength > 0.1f) ? data.outerCornerArcLength : 0.44f;
                    float deltaProgress = (remainingMove / arcLength) * progressDir;
                    float nextProgress = OuterCornerProgress + deltaProgress;

                    if (nextProgress >= 1f)
                    {
                        float usedRatio = (1f - OuterCornerProgress) / Mathf.Max(0.0001f, Mathf.Abs(deltaProgress));
                        remainingMove = Mathf.Max(0f, remainingMove * (1f - Mathf.Clamp01(usedRatio)));

                        CurrentSurfaceNormal = OuterCornerNormalB;
                        TargetSurfaceNormal = OuterCornerNormalB;

                        Vector2 exitCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, 1f)
                                             - OuterCornerNormalA * 0.02f;
                        position = exitCenter - boxCollider.offset;

                        IsOnOuterCorner = false;
                        OuterCornerProgress = 0f;
                        IsGrounded = true;
                        continue;
                    }
                    else if (nextProgress <= 0f)
                    {
                        float usedRatio = OuterCornerProgress / Mathf.Max(0.0001f, Mathf.Abs(deltaProgress));
                        remainingMove = Mathf.Max(0f, remainingMove * (1f - Mathf.Clamp01(usedRatio)));

                        CurrentSurfaceNormal = OuterCornerNormalA;
                        TargetSurfaceNormal = OuterCornerNormalA;

                        Vector2 exitCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, 0f)
                                             - OuterCornerNormalB * 0.02f;
                        position = exitCenter - boxCollider.offset;

                        IsOnOuterCorner = false;
                        OuterCornerProgress = 0f;
                        IsGrounded = true;
                        continue;
                    }
                    else
                    {
                        OuterCornerProgress = nextProgress;
                        Vector2 arcCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, OuterCornerProgress);
                        position = arcCenter - boxCollider.offset;
                        TargetSurfaceNormal = EffectiveSurfaceNormal;
                        IsGrounded = true;
                        remainingMove = 0f;
                        break;
                    }
                }

                Vector2 currentNormal = SnapToCardinal(CurrentSurfaceNormal);
                Vector2 currentTangent = new Vector2(currentNormal.y, -currentNormal.x);
                Vector2 moveDir = currentTangent * moveSign;

                Vector2 currentCenter = position + boxCollider.offset;
                float extentNormal = GetColliderExtent(-currentNormal);
                float extentTangent = GetColliderExtent(moveDir);

                float innerPreviewDist = (data != null && data.innerCornerBlendDistance > 0.05f) ? data.innerCornerBlendDistance : 0.40f;
                float lookAheadDist = Mathf.Max(remainingMove, innerPreviewDist) + extentTangent + data.skinWidth * 2f;

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

                    if (actualDistToWall <= innerPreviewDist && Mathf.Abs(Vector2.Dot(currentNormal, hitWallNormal)) < 0.35f)
                    {
                        float blendT = 1f - Mathf.Clamp01(actualDistToWall / innerPreviewDist);
                        TargetSurfaceNormal = Vector2.Lerp(currentNormal, hitWallNormal, blendT * 0.5f).normalized;
                    }

                    if (actualDistToWall <= remainingMove + 0.005f)
                    {
                        position += moveDir * actualDistToWall;

                        bool isInnerCorner = Mathf.Abs(Vector2.Dot(currentNormal, hitWallNormal)) < 0.35f;

                        if (isInnerCorner)
                        {
                            TargetSurfaceNormal = hitWallNormal;
                            CurrentSurfaceNormal = hitWallNormal;
                            HitWall = true;
                            IsInInnerCornerPocket = true;
                            InnerCornerAdjacentNormal = currentNormal;

                            innerCornerSlowdownTimer = (data != null && data.innerCornerSlowdownDuration > 0f)
                                ? data.innerCornerSlowdownDuration
                                : 0.06f;

                            remainingMove = Mathf.Max(0f, remainingMove - actualDistToWall);
                            continue;
                        }
                        else
                        {
                            HitWall = true;
                            Velocity = Vector2.zero;
                            remainingMove = 0f;
                            break;
                        }
                    }
                }

                float stepDist = remainingMove;
                Vector2 tentativePos = position + moveDir * stepDist;
                Vector2 tentativeCenter = tentativePos + boxCollider.offset;

                float floorCheckDist = extentNormal + Mathf.Max(0.25f, data.skinWidth * 8f);

                RaycastHit2D centerFloorCheck = Physics2D.Raycast(tentativeCenter, -currentNormal, floorCheckDist, data.groundLayer);

                if (centerFloorCheck.collider != null && centerFloorCheck.distance > 0.0005f && Vector2.Dot(SnapToCardinal(centerFloorCheck.normal), currentNormal) > 0.6f)
                {
                    position = tentativePos;
                    remainingMove = 0f;
                }
                else
                {
                    if (TryFindAdjacentOuterCornerWall(
                        currentCenter,
                        tentativeCenter,
                        currentNormal,
                        moveDir,
                        extentNormal,
                        extentTangent,
                        stepDist,
                        floorCheckDist,
                        out Vector2 cornerPoint,
                        out Vector2 newNormal))
                    {
                        float distToCornerVertex = Mathf.Max(0f, Vector2.Dot(cornerPoint - currentCenter, moveDir));
                        float pastVertexDist = Mathf.Max(0.002f, stepDist - distToCornerVertex);

                        IsOnOuterCorner = true;
                        OuterCornerVertex = cornerPoint;
                        OuterCornerNormalA = currentNormal;
                        OuterCornerNormalB = newNormal;
                        OuterCornerProgress = 0f;

                        Vector2 entryCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, 0f);
                        position = entryCenter - boxCollider.offset;
                        IsGrounded = true;

                        remainingMove = pastVertexDist;
                        continue;
                    }
                    else
                    {
                        position = tentativePos;
                        remainingMove = 0f;
                    }
                }
            }

            if (IsOnOuterCorner)
            {
                if (data != null && !data.allowStoppingOnOuterCorners && MoveInput.sqrMagnitude < 0.04f)
                {
                    float autoTarget = OuterCornerProgress >= 0.5f ? 1f : 0f;
                    OuterCornerProgress = Mathf.MoveTowards(OuterCornerProgress, autoTarget, 4f * Time.fixedDeltaTime);
                    if (OuterCornerProgress >= 0.99f)
                    {
                        CurrentSurfaceNormal = OuterCornerNormalB;
                        TargetSurfaceNormal = OuterCornerNormalB;
                        Vector2 exitCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, 1f)
                                             - OuterCornerNormalA * 0.02f;
                        position = exitCenter - boxCollider.offset;
                        IsOnOuterCorner = false;
                    }
                    else if (OuterCornerProgress <= 0.01f)
                    {
                        CurrentSurfaceNormal = OuterCornerNormalA;
                        TargetSurfaceNormal = OuterCornerNormalA;
                        Vector2 exitCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, 0f)
                                             - OuterCornerNormalB * 0.02f;
                        position = exitCenter - boxCollider.offset;
                        IsOnOuterCorner = false;
                    }
                }

                if (IsOnOuterCorner)
                {
                    Vector2 arcCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, OuterCornerProgress);
                    position = arcCenter - boxCollider.offset;
                    TargetSurfaceNormal = EffectiveSurfaceNormal;
                    IsGrounded = true;
                }
            }

            if (!IsOnOuterCorner)
            {
                Vector2 finalNormal = SnapToCardinal(CurrentSurfaceNormal);
                Vector2 snapDir = -finalNormal;
                Vector2 snapOrigin = position + boxCollider.offset;
                float snapDist = Mathf.Max(0.20f, data.skinWidth * 8f);

                if ((data == null || data.allowStoppingOnOuterCorners) &&
                    TryDetectOuterCornerUnderneath(snapOrigin, finalNormal, out Vector2 detectedVertex, out Vector2 detectedWallNormal, out float detectedProgress))
                {
                    IsOnOuterCorner = true;
                    OuterCornerVertex = detectedVertex;
                    OuterCornerNormalA = finalNormal;
                    OuterCornerNormalB = detectedWallNormal;
                    OuterCornerProgress = detectedProgress;

                    Vector2 cornerCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, OuterCornerProgress);
                    position = cornerCenter - boxCollider.offset;
                    TargetSurfaceNormal = EffectiveSurfaceNormal;
                    IsGrounded = true;
                }
                else
                {
                    int groundHits = Physics2D.BoxCast(
                        snapOrigin,
                        boxSize,
                        0f,
                        snapDir,
                        contactFilter,
                        hitBuffer,
                        snapDist
                    );

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
                        CurrentSurfaceNormal = finalNormal;
                    }
                    else
                    {
                        RaycastHit2D centerGroundHit = Physics2D.Raycast(snapOrigin, snapDir, GetColliderExtent(snapDir) + snapDist, data.groundLayer);
                        if (centerGroundHit.collider != null && centerGroundHit.distance > 0.0005f && Vector2.Dot(SnapToCardinal(centerGroundHit.normal), finalNormal) > 0.7f)
                        {
                            float dist = Mathf.Max(0f, centerGroundHit.distance - GetColliderExtent(snapDir) - data.skinWidth);
                            position += snapDir * dist;
                            IsGrounded = true;
                            CurrentSurfaceNormal = finalNormal;
                        }
                        else if (groundHits > 0)
                        {
                            IsGrounded = true;
                            CurrentSurfaceNormal = finalNormal;
                        }
                        else
                        {
                            IsGrounded = false;
                        }
                    }

                    if (IsGrounded)
                    {
                        CheckInnerCornerPocket(position + boxCollider.offset, finalNormal);
                        if (!IsInInnerCornerPocket && MoveInput.sqrMagnitude < 0.04f)
                        {
                            TargetSurfaceNormal = finalNormal;
                        }
                    }
                }
            }
        }
        else
        {
            // --- FLUG- & DASH-MODUS (Welt-Koordinaten) ---

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
                        AttachToSurfaceFromAir(ref position, snappedNormal);
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
            if (!IsGrounded)
            {
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
                            AttachToSurfaceFromAir(ref position, snappedNormal);
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
        }

        rb.MovePosition(position);
    }

    private void AttachToSurfaceFromAir(ref Vector2 position, Vector2 landedNormal)
    {
        TargetSurfaceNormal = landedNormal;
        CurrentSurfaceNormal = landedNormal;
        IsGrounded = true;

        float squash = (data != null) ? data.landSquashAmount : 0.18f;
        ApplySquashAndStretch(1f + squash, 1f - squash);

        if (data == null || data.allowStoppingOnOuterCorners)
        {
            Vector2 center = position + boxCollider.offset;
            if (TryDetectOuterCornerUnderneath(center, landedNormal, out Vector2 cornerVert, out Vector2 adjWallNormal, out float initProg))
            {
                IsOnOuterCorner = true;
                OuterCornerVertex = cornerVert;
                OuterCornerNormalA = landedNormal;
                OuterCornerNormalB = adjWallNormal;
                OuterCornerProgress = initProg;

                Vector2 cornerCenter = CalculateOuterCornerCenterPosition(OuterCornerVertex, OuterCornerNormalA, OuterCornerNormalB, OuterCornerProgress);
                position = cornerCenter - boxCollider.offset;
                TargetSurfaceNormal = EffectiveSurfaceNormal;
            }
        }

        StateMachine.ChangeState(GroundedState);
    }

    private void CheckInnerCornerPocket(Vector2 center, Vector2 surfaceNormal)
    {
        Vector2 tangent = new Vector2(surfaceNormal.y, -surfaceNormal.x);
        float extentTangent = GetColliderExtent(tangent);
        float pocketCheckDist = extentTangent + data.skinWidth * 4f + 0.05f;

        RaycastHit2D hitCW = Physics2D.Raycast(center, tangent, pocketCheckDist, data.groundLayer);
        if (hitCW.collider != null && hitCW.distance > 0.0005f && Mathf.Abs(Vector2.Dot(SnapToCardinal(hitCW.normal), surfaceNormal)) < 0.25f)
        {
            IsInInnerCornerPocket = true;
            InnerCornerAdjacentNormal = SnapToCardinal(hitCW.normal);
            return;
        }

        RaycastHit2D hitCCW = Physics2D.Raycast(center, -tangent, pocketCheckDist, data.groundLayer);
        if (hitCCW.collider != null && hitCCW.distance > 0.0005f && Mathf.Abs(Vector2.Dot(SnapToCardinal(hitCCW.normal), surfaceNormal)) < 0.25f)
        {
            IsInInnerCornerPocket = true;
            InnerCornerAdjacentNormal = SnapToCardinal(hitCCW.normal);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Hazard")) TakeDamage();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.CompareTag("Hazard")) TakeDamage();
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;

        Gizmos.color = IsOnOuterCorner ? Color.yellow : (IsGrounded ? Color.green : Color.cyan);
        Vector2 center = (Vector2)transform.position + col.offset;
        Gizmos.DrawWireCube(center, col.size);

        if (IsOnOuterCorner)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(OuterCornerVertex, 0.08f);
            Gizmos.DrawLine(center, center + EffectiveSurfaceNormal * 0.6f);
        }
    }

    public void TakeDamage()
    {
        if (IsInvincible) return;
        invincibilityTimer = damageInvincibilityDuration;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnSlimeDamaged();
        }
    }
}
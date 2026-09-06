using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class PlatformerSlimeController : MonoBehaviour
{
    [SerializeField] private PlatformerPhysicsData data;

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
    public bool IsGrounded { get; private set; }
    public bool HitCeiling { get; private set; }
    public bool HitWall { get; private set; }
    public Vector2 TargetSurfaceNormal { get; set; } = Vector2.up;
    public Vector2 CurrentSurfaceNormal { get; private set; } = Vector2.up;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();

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
    }

    private void FixedUpdate()
    {
        StateMachine.FixedUpdate();
        MoveAndResolveCollisions(Velocity * Time.fixedDeltaTime);
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

            JumpTriggered = keyboard.spaceKey.wasPressedThisFrame;
            JumpHeld = keyboard.spaceKey.isPressed;
        }

        MoveInput = input;
    }

    private void UpdateSurfaceRotation()
    {
        float targetAngle = Mathf.Atan2(TargetSurfaceNormal.y, TargetSurfaceNormal.x) * Mathf.Rad2Deg - 90f;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);

        transform.rotation = targetRotation;
        CurrentSurfaceNormal = transform.up;
    }

    private void MoveAndResolveCollisions(Vector2 deltaMovement)
    {
        IsGrounded = false;
        HitCeiling = false;
        HitWall = false;

        Vector2 boxSize = boxCollider.size - (Vector2.one * (data.skinWidth * 2f));
        Vector2 position = rb.position;
        Vector2 castOrigin = position + (Vector2)(transform.rotation * boxCollider.offset);

        // Lokale Achsen des Slimes
        Vector2 localUp = transform.up;
        Vector2 localRight = transform.right;

        // --- VERTIKALE / OBERFLÄCHEN-KOLLISION (Relativ zu den Füßen des Slimes) ---
        float yDist = Mathf.Abs(deltaMovement.y) + data.skinWidth;
        float yDir = Mathf.Sign(deltaMovement.y);

        // Prüfe immer leicht in Richtung der Füße, um die Haftung nicht zu verlieren
        if (Mathf.Abs(deltaMovement.y) < 0.0001f)
        {
            yDist = data.skinWidth * 2f;
            yDir = -1f;
        }

        Vector2 surfaceCheckDir = localUp * yDir;

        int yHits = Physics2D.BoxCast(
            castOrigin,
            boxSize,
            transform.eulerAngles.z,
            surfaceCheckDir,
            contactFilter,
            hitBuffer,
            yDist
        );

        if (yHits > 0)
        {
            RaycastHit2D hit = hitBuffer[0];
            float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);

            if (yDir < 0f) // Kontakt an den Füßen (egal ob Boden, Wand oder Decke)
            {
                deltaMovement.y = -actualDist;
                IsGrounded = true;
                TargetSurfaceNormal = hit.normal;

                Vector2 v = Velocity;
                if (v.y < 0f) v.y = 0f;
                Velocity = v;
            }
            else if (yDir > 0f) // Kontakt am Kopf
            {
                deltaMovement.y = actualDist;
                HitCeiling = true;

                Vector2 v = Velocity;
                if (v.y > 0f) v.y = 0f;
                Velocity = v;
            }
        }

        // --- HORIZONTALE KOLLISION (Entlang der Fläche) ---
        if (Mathf.Abs(deltaMovement.x) > 0.0001f)
        {
            float xDist = Mathf.Abs(deltaMovement.x) + data.skinWidth;
            float xDir = Mathf.Sign(deltaMovement.x);

            Vector2 moveCheckDir = localRight * xDir;
            Vector2 xCastOrigin = castOrigin + (localUp * deltaMovement.y);

            int xHits = Physics2D.BoxCast(
                xCastOrigin,
                boxSize,
                transform.eulerAngles.z,
                moveCheckDir,
                contactFilter,
                hitBuffer,
                xDist
            );

            if (xHits > 0)
            {
                RaycastHit2D hit = hitBuffer[0];
                float actualDist = Mathf.Max(0f, hit.distance - data.skinWidth);
                deltaMovement.x = actualDist * xDir;
                HitWall = true;

                // Trifft er vor sich auf eine neue Wand (z.B. Innenkante), merkt er sich deren Normale
                TargetSurfaceNormal = hit.normal;
                Velocity = Vector2.zero;
            }
        }

        // Bewegung relativ zur Ausrichtung anwenden
        Vector2 finalDelta = (localRight * deltaMovement.x) + (localUp * deltaMovement.y);
        rb.MovePosition(position + finalDelta);
    }
}
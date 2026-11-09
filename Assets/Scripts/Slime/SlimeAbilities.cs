using UnityEngine;

public enum DashDirectionMode
{
    HollowKnightHorizontal, // Klassisch: In der Luft links/rechts, an Flächen entlang der Fläche (oder von Wand weg)
    EightWay,               // 8 Richtungen (Horizontal, Vertikal, 45°-Diagonalen)
    Free360                 // Exakter 360°-Winkel des Analogsticks
}

public class SlimeAbilities : MonoBehaviour
{
    [Header("Fähigkeiten an- und ausschalten")]
    [Tooltip("Erlaubt einen oder mehrere Zusatzsprünge in der Luft")]
    public bool enableDoubleJump = true;

    [Tooltip("Erlaubt den kurzen Dash-Stoß (Shift / R2 / Viereck)")]
    public bool enableDash = true;

    [Tooltip("Wenn aktiv, rennt der Slime nach dem Dash (oder beim Halten der Dash-Taste) schneller weiter")]
    public bool enableSprint = true;

    [Tooltip("Wenn aktiv, bläst sich der Slime beim Halten der Sprungtaste in der Luft auf und schwebt")]
    public bool enableBalloonFloat = true;

    [Header("Dash-Modus Auswahl")]
    [Tooltip("HollowKnightHorizontal = Links/Rechts (bzw. entlang Fläche) | EightWay = 8 Richtungen | Free360 = Freier Stick-Winkel")]
    public DashDirectionMode dashDirectionMode = DashDirectionMode.HollowKnightHorizontal;

    // --- Laufzeit-Status ---
    public int RemainingAirJumps { get; private set; }
    public int RemainingAirDashes { get; private set; }
    public float RemainingFloatTime { get; private set; }
    public float DashCooldownTimer { get; private set; }

    public bool IsDashing { get; set; }
    public bool IsSprinting { get; set; }
    public bool IsBalloonFloating { get; set; }

    /// <summary>
    /// Merkt sich die letzte Blick-/Bewegungsrichtung für neutrale Dashes ohne Richtungseingabe.
    /// </summary>
    public Vector2 LastFacingDirection { get; private set; } = Vector2.right;

    private PlatformerPhysicsData data;

    public void Initialize(PlatformerPhysicsData physicsData)
    {
        data = physicsData;
        ResetAllCharges();
    }

    public void TickTimers(float deltaTime)
    {
        if (DashCooldownTimer > 0f)
        {
            DashCooldownTimer -= deltaTime;
        }
    }

    public void UpdateFacingDirection(Vector2 moveInput, Vector2 currentTangent, float currentTraverseSign)
    {
        if (Mathf.Abs(currentTraverseSign) > 0.01f)
        {
            Vector2 dir = (currentTangent * Mathf.Sign(currentTraverseSign)).normalized;
            if (dir.sqrMagnitude > 0.1f)
            {
                LastFacingDirection = dir;
            }
        }
        else if (Mathf.Abs(moveInput.x) > 0.15f)
        {
            LastFacingDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
        }
    }

    /// <summary>
    /// Lädt Doppelsprung, Air-Dash und Ballon-Schwebezeit bei Flächenkontakt wieder voll auf.
    /// </summary>
    public void ResetAllCharges()
    {
        int maxJumps = (data != null) ? Mathf.Max(1, data.maxAirJumps) : 1;
        int maxDashes = (data != null) ? Mathf.Max(1, data.maxAirDashes) : 1;
        float maxFloat = (data != null && data.maxFloatDuration > 0f) ? data.maxFloatDuration : 1.2f;

        RemainingAirJumps = maxJumps;
        RemainingAirDashes = maxDashes;
        RemainingFloatTime = maxFloat;
        IsBalloonFloating = false;
    }

    // --- Doppelsprung ---
    public bool CanUseDoubleJump()
    {
        return enableDoubleJump && RemainingAirJumps > 0;
    }

    public void ConsumeDoubleJump()
    {
        RemainingAirJumps = Mathf.Max(0, RemainingAirJumps - 1);
    }

    // --- Dash ---
    public bool CanUseDash(bool isGrounded)
    {
        if (!enableDash || IsDashing || DashCooldownTimer > 0f) return false;
        return isGrounded || RemainingAirDashes > 0;
    }

    public void ConsumeDash(bool isGrounded)
    {
        if (!isGrounded)
        {
            RemainingAirDashes = Mathf.Max(0, RemainingAirDashes - 1);
        }

        DashCooldownTimer = (data != null && data.dashCooldown > 0f) ? data.dashCooldown : 0.35f;
        IsBalloonFloating = false;
    }

    // --- Ballon-Schweben ---
    public bool CanUseBalloonFloat()
    {
        return enableBalloonFloat && RemainingFloatTime > 0f;
    }

    public void ConsumeFloatTime(float deltaTime)
    {
        RemainingFloatTime = Mathf.Max(0f, RemainingFloatTime - deltaTime);
        if (RemainingFloatTime <= 0f)
        {
            IsBalloonFloating = false;
        }
    }

    /// <summary>
    /// Berechnet die Dash-Richtung abhängig vom gewählten DashDirectionMode.
    /// </summary>
    public Vector2 CalculateDashDirection(
        Vector2 moveInput,
        bool isGrounded,
        Vector2 surfaceNormal,
        bool isOnOuterCorner,
        Vector2 outerCornerTangent)
    {
        Vector2 fallbackHorizontal = new Vector2(Mathf.Abs(LastFacingDirection.x) > 0.1f ? Mathf.Sign(LastFacingDirection.x) : 1f, 0f);

        switch (dashDirectionMode)
        {
            case DashDirectionMode.Free360:
            {
                if (moveInput.sqrMagnitude > 0.1f)
                {
                    return moveInput.normalized;
                }
                return isGrounded ? LastFacingDirection.normalized : fallbackHorizontal;
            }

            case DashDirectionMode.EightWay:
            {
                if (moveInput.sqrMagnitude > 0.1f)
                {
                    float angle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;
                    float snappedAngle = Mathf.Round(angle / 45f) * 45f * Mathf.Deg2Rad;
                    return new Vector2(Mathf.Cos(snappedAngle), Mathf.Sin(snappedAngle)).normalized;
                }
                return isGrounded ? LastFacingDirection.normalized : fallbackHorizontal;
            }

            case DashDirectionMode.HollowKnightHorizontal:
            default:
            {
                // 1. An einer vertikalen Wand:
                if (isGrounded && !isOnOuterCorner && Mathf.Abs(surfaceNormal.x) > 0.5f)
                {
                    // Drückt der Spieler von der Wand weg oder neutral -> Horizontaler Wall-Dash weg von der Wand (wie in Hollow Knight)
                    if (moveInput.sqrMagnitude < 0.1f || Vector2.Dot(moveInput, surfaceNormal) > 0.2f)
                    {
                        LastFacingDirection = new Vector2(Mathf.Sign(surfaceNormal.x), 0f);
                        return LastFacingDirection;
                    }
                    // Drückt der Spieler gezielt an der Wand hoch/runter -> Dash entlang der Wand
                    if (Mathf.Abs(moveInput.y) > 0.25f)
                    {
                        return new Vector2(0f, Mathf.Sign(moveInput.y));
                    }
                }

                // 2. Auf Boden, Decke oder in der Luft -> Strikt horizontal links/rechts
                if (Mathf.Abs(moveInput.x) > 0.15f)
                {
                    LastFacingDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
                    return LastFacingDirection;
                }

                return fallbackHorizontal;
            }
        }
    }
}
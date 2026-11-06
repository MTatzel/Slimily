using UnityEngine;

[CreateAssetMenu(fileName = "PlatformerPhysicsData", menuName = "Slime Platformer/Physics Data")]
public class PlatformerPhysicsData : ScriptableObject
{
    [Header("Gravity & Air")]
    public float gravity = -35f;
    public float maxFallSpeed = 20f;

    [Tooltip("Ab welcher vertikalen Geschwindigkeit nahe 0 der Scheitelpunkt (Apex) aktiv wird")]
    public float apexVelocityThreshold = 2.5f;
    [Tooltip("Schwerkraft-Faktor am höchsten Punkt des Sprungs (kleiner = mehr Kontrolle bei Kantenlandungen)")]
    [Range(0.3f, 1f)] public float apexGravityMultiplier = 0.72f;
    [Tooltip("Zusätzliche Luftbeschleunigung am Scheitelpunkt des Sprungs")]
    public float apexAirAccelerationMultiplier = 1.25f;

    [Header("Movement")]
    public float moveSpeed = 8f;
    public float groundAcceleration = 70f;
    public float groundDeceleration = 65f;
    public float airAcceleration = 38f;
    [Tooltip("Sanfter Luftwiderstand ohne Richtungseingabe, damit die Sprunggeschwindigkeit in Sprungrichtung erhalten bleibt")]
    public float airDeceleration = 5f;

    [Header("Jumping & Coyote Time")]
    public float jumpVelocity = 14f;
    [Tooltip("Sanfte Dämpfung bei sehr frühem Loslassen der Sprungtaste (höher = natürlicherer Bogen)")]
    [Range(0.5f, 1.0f)] public float jumpCutMultiplier = 0.85f;
    [Tooltip("Erhöhte Schwerkraft im Aufstieg nach dem Loslassen der Sprungtaste für eine geschmeidige Sprungparabel")]
    [Range(1.0f, 3.0f)] public float lowJumpGravityMultiplier = 1.75f;
    [Tooltip("Kulanzzeit in Sekunden, in der man kurz nach dem Verlassen einer Kante noch abspringen kann")]
    public float coyoteTimeDuration = 0.1f;

    [Header("Äußere Ecken (270° Wrapping & Stehenbleiben)")]
    [Tooltip("Geschwindigkeits-Faktor beim Umrunden einer äußeren Ecke (kleiner = langsamer & kontrollierter)")]
    [Range(0.3f, 1.2f)] public float outerCornerSpeedMultiplier = 0.78f;
    [Tooltip("Länge des Bogens um eine äußere Ecke (größer = sanfterer, längerer Übergang)")]
    [Range(0.25f, 1.0f)] public float outerCornerArcLength = 0.44f;
    [Tooltip("Wie eng sich der Slime an die äußere Kante schmiegt (0.75 = eng anliegend, 1.0 = weiter Kreisbogen)")]
    [Range(0.55f, 1.0f)] public float outerCornerCurvePower = 0.82f;
    [Tooltip("Erlaubt dem Slime, mitten auf äußeren Ecken (z. B. im 45°-Winkel) stehenzubleiben oder dort zu landen")]
    public bool allowStoppingOnOuterCorners = true;

    [Header("Innere Ecken (90° Übergang)")]
    [Tooltip("Geschwindigkeits-Faktor direkt beim Wechsel durch eine 90°-Innenecke")]
    [Range(0.3f, 1.0f)] public float innerCornerSpeedMultiplier = 0.82f;
    [Tooltip("Dauer der leichten Abbremsung in einer Innenecke in Sekunden")]
    public float innerCornerSlowdownDuration = 0.06f;
    [Tooltip("Abstand vor einer Innenecke, ab dem sich der Slime optisch schon sanft in die Ecke neigt")]
    public float innerCornerBlendDistance = 0.40f;

    [Header("Sensors & Collision")]
    public LayerMask groundLayer;
    public float skinWidth = 0.015f;
    public float alignRotationSpeed = 680f;

    [Header("Ceiling Detach")]
    [Tooltip("Dauer in Sekunden, die der Stick/Taste stur nach unten gehalten werden muss, um von der Decke zu fallen.")]
    public float ceilingDetachHoldDuration = 0.25f;

    [Header("Juice (Squash & Stretch)")]
    public bool enableSquashAndStretch = true;
    [Range(0f, 0.4f)] public float jumpStretchAmount = 0.16f;
    [Range(0f, 0.4f)] public float landSquashAmount = 0.18f;
    public float squashRecoverySpeed = 12f;
}
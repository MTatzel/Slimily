using UnityEngine;

[CreateAssetMenu(fileName = "PlatformerPhysicsData", menuName = "Slime Platformer/Physics Data")]
public class PlatformerPhysicsData : ScriptableObject
{
    [Header("Gravity & Air")]
    public float gravity = -35f;
    public float maxFallSpeed = 20f;

    [Header("Movement")]
    public float moveSpeed = 8f;
    public float groundAcceleration = 70f;
    public float groundDeceleration = 60f;
    public float airAcceleration = 35f;

    [Header("Jumping")]
    public float jumpVelocity = 14f;

    [Tooltip("Anteil des tangentialen Impulses, der beim Absprung mitgenommen wird (0 = exakt senkrecht von der Oberfläche weg).")]
    public float jumpTangentMomentumMultiplier = 0f;

    [Tooltip("Abfall-Geschwindigkeit beim manuellen Lösen von der Decke (Taste S).")]
    public float ceilingDropVelocity = 3f;

    [Header("Sensors & Collision")]
    public LayerMask groundLayer;
    public float skinWidth = 0.015f;
    public float alignRotationSpeed = 720f; // Grad pro Sekunde für sanftes Ausrichten

    [Header("360° Surface Climbing & Corners")]
    [Tooltip("Distanz für die Bodenhaftung senkrecht zur Oberfläche.")]
    public float surfaceSnapDistance = 0.35f;

    [Tooltip("Suchdistanz um Außenkanten herum, um die nächste Fläche nahtlos zu greifen.")]
    public float cornerCheckDistance = 0.5f;

    [Tooltip("Maximaler Radius um den Slime, innerhalb dessen eine Fläche ihn hält.")]
    public float surfaceRadius = 0.65f;

    [Tooltip("Cooldown nach einem Absprung oder manuellem Detach, bevor wieder angeheftet werden darf.")]
    public float detachCooldown = 0.15f;
}
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

    [Header("Sensors & Collision")]
    public LayerMask groundLayer;
    public float skinWidth = 0.015f;
    public float alignRotationSpeed = 720f; // Grad pro Sekunde für sanftes Ausrichten

    [Header("Ceiling Detach")]
    [Tooltip("Dauer in Sekunden, die der Stick/Taste stur nach unten gehalten werden muss, um von der Decke zu fallen.")]
    public float ceilingDetachHoldDuration = 0.25f;
}
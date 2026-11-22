using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class BouncyEgg : MonoBehaviour
{
    [Header("Test-Start")]
    [Tooltip("Startgeschwindigkeit beim Play-Drücken (zum schnellen Testen).")]
    [SerializeField] private Vector2 initialVelocity = new Vector2(8f, 5f);

    [Header("Abprall-Verhalten")]
    [Tooltip("Faktor beim Abprallen (1.0 = gleich schnell, 1.1 = leichter Gummi-Boost).")]
    [SerializeField] private float bounciness = 1.05f;

    [Tooltip("Minimale Geschwindigkeit nach einem Abpraller, damit das Ei nicht liegen bleibt.")]
    [SerializeField] private float minBounceSpeed = 6f;

    [Tooltip("Maximale Geschwindigkeit, damit es durch den Boost nicht unendlich schnell wird.")]
    [SerializeField] private float maxSpeed = 16f;

    [Header("Wand-Verformung")]
    [Tooltip("Wie stark das Ei die Magenwand eindrückt.")]
    [SerializeField] private float wallImpulseMultiplier = 0.8f;

    [Tooltip("Radius der Delle in der Magenwand.")]
    [SerializeField] private float impactRadius = 1.4f;

    private Rigidbody2D _rb;
    private Vector2 _lastVelocity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        // Continuous verhindert Tunneling bei schnellen Eiern
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Start()
    {
        SetVelocity(initialVelocity);
    }

    private void FixedUpdate()
    {
        // Geschwindigkeit vor einer möglichen Kollision sichern
        _lastVelocity = GetVelocity();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.TryGetComponent(out ElasticStomachArena stomach))
            return;

        Vector2 hitPoint = collision.GetContact(0).point;

        // 1. Magenwand in Flugrichtung eindrücken
        stomach.ApplyImpulseAtPoint(hitPoint, _lastVelocity * wallImpulseMultiplier, impactRadius);

        // 2. Sauber über die nach innen zeigende Wand-Normale abprallen
        Vector2 normal = stomach.GetInwardNormalAtPoint(hitPoint);
        Vector2 reflectedDir = Vector2.Reflect(_lastVelocity.normalized, normal);

        float newSpeed = Mathf.Clamp(_lastVelocity.magnitude * bounciness, minBounceSpeed, maxSpeed);
        SetVelocity(reflectedDir * newSpeed);
    }

    // Kompatibel mit Unity 2022/2023 und Unity 6
    private Vector2 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return _rb.linearVelocity;
#else
        return _rb.velocity;
#endif
    }

    private void SetVelocity(Vector2 vel)
    {
#if UNITY_6000_0_OR_NEWER
        _rb.linearVelocity = vel;
#else
        _rb.velocity = vel;
#endif
    }
}
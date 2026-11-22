using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(EdgeCollider2D))]
[RequireComponent(typeof(LineRenderer))]
public class ElasticStomachArena : MonoBehaviour
{
    [Header("Auflösung")]
    [Tooltip("Anzahl der Federpunkte entlang der Magenwand (80-120 ist meist ideal).")]
    [SerializeField] private int pointCount = 100;

    [Header("Feder-Physik")]
    [Tooltip("Wie stark die Wand zurück in ihre Grundform zieht.")]
    [SerializeField] private float stiffness = 180f;

    [Tooltip("Wie schnell das Nachschwingen abklingt.")]
    [SerializeField] private float damping = 6f;

    [Tooltip("Wie stark Nachbarpunkte mitgezogen werden (Wellenausbreitung).")]
    [SerializeField] private float neighborCoupling = 120f;

    [Tooltip("Maximale Auslenkung in Units, damit nichts explodiert.")]
    [SerializeField] private float maxDisplacement = 1.5f;

    [Header("Boden-Stabilität")]
    [Tooltip("Faktor für den Boden (Punkte, deren Normale nach oben zeigt). > 1 = festerer Stand.")]
    [SerializeField] private float floorStiffnessMultiplier = 1.6f;

    private PolygonCollider2D _shapeTemplate;
    private EdgeCollider2D _edgeCollider;
    private LineRenderer _lineRenderer;

    private Vector2[] _restPositions;
    private Vector2[] _currentPositions;
    private Vector2[] _velocities;
    private Vector2[] _inwardNormals;
    private Vector2[] _colliderPoints; // pointCount + 1 (geschlossener Ring)
    private Vector3[] _linePoints;

    private void Awake()
    {
        _shapeTemplate = GetComponent<PolygonCollider2D>();
        _edgeCollider = GetComponent<EdgeCollider2D>();
        _lineRenderer = GetComponent<LineRenderer>();

        BuildRingFromTemplate();

        // Schablone deaktivieren – ab jetzt übernimmt der EdgeCollider2D
        _shapeTemplate.enabled = false;
    }

    private void BuildRingFromTemplate()
    {
        Vector2[] rawPath = _shapeTemplate.GetPath(0);
        _restPositions = ResampleClosedPath(rawPath, pointCount);
        _currentPositions = new Vector2[pointCount];
        _velocities = new Vector2[pointCount];
        _inwardNormals = new Vector2[pointCount];
        _colliderPoints = new Vector2[pointCount + 1];
        _linePoints = new Vector3[pointCount];

        // Winding-Order bestimmen, damit Normalen garantiert nach INNEN zeigen
        float signedArea = ComputeSignedArea(_restPositions);
        float normalSign = signedArea >= 0f ? 1f : -1f;

        for (int i = 0; i < pointCount; i++)
        {
            _currentPositions[i] = _restPositions[i];

            Vector2 prev = _restPositions[(i - 1 + pointCount) % pointCount];
            Vector2 next = _restPositions[(i + 1) % pointCount];
            Vector2 tangent = (next - prev).normalized;
            _inwardNormals[i] = new Vector2(-tangent.y, tangent.x) * normalSign;
        }

        _lineRenderer.loop = true;
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.positionCount = pointCount;

        SyncColliderAndVisuals();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        float dampingFactor = Mathf.Exp(-damping * dt);

        // 1. Kräfte berechnen (Hooke + Nachbar-Wellen)
        for (int i = 0; i < pointCount; i++)
        {
            int prev = (i - 1 + pointCount) % pointCount;
            int next = (i + 1) % pointCount;

            Vector2 disp = _currentPositions[i] - _restPositions[i];
            Vector2 dispPrev = _currentPositions[prev] - _restPositions[prev];
            Vector2 dispNext = _currentPositions[next] - _restPositions[next];

            // Boden optional etwas steifer machen
            float localStiffness = _inwardNormals[i].y > 0.5f
                ? stiffness * floorStiffnessMultiplier
                : stiffness;

            Vector2 springForce = -localStiffness * disp;
            Vector2 waveForce = neighborCoupling * (dispPrev + dispNext - 2f * disp);

            _velocities[i] += (springForce + waveForce) * dt;
            _velocities[i] *= dampingFactor;
        }

        // 2. Positionen integrieren & begrenzen
        for (int i = 0; i < pointCount; i++)
        {
            _currentPositions[i] += _velocities[i] * dt;

            Vector2 offset = _currentPositions[i] - _restPositions[i];
            if (offset.sqrMagnitude > maxDisplacement * maxDisplacement)
            {
                _currentPositions[i] = _restPositions[i] + offset.normalized * maxDisplacement;
                _velocities[i] *= 0.5f;
            }
        }

        SyncColliderAndVisuals();
    }

    private void SyncColliderAndVisuals()
    {
        for (int i = 0; i < pointCount; i++)
        {
            _colliderPoints[i] = _currentPositions[i];
            _linePoints[i] = _currentPositions[i];
        }

        _colliderPoints[pointCount] = _currentPositions[0]; // Ring schließen
        _edgeCollider.points = _colliderPoints;
        _lineRenderer.SetPositions(_linePoints);
    }

    // =========================================================================
    // ÖFFENTLICHE API FÜR SPIELER & OBJEKTE (DASH, STEHEN, EIER/PROJEKTILE)
    // =========================================================================

    /// <summary>
    /// Einmaliger Impuls – ideal für Dash-Aufprall oder abprallende Objekte (wie Yoshi-Eier).
    /// </summary>
    public void ApplyImpulseAtPoint(Vector2 worldPoint, Vector2 worldImpulse, float radius = 1.5f)
    {
        Vector2 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector2 localImpulse = transform.InverseTransformVector(worldImpulse);
        float radiusSq = radius * radius;

        for (int i = 0; i < pointCount; i++)
        {
            float distSq = (_currentPositions[i] - localPoint).sqrMagnitude;
            if (distSq < radiusSq)
            {
                // Weicher Falloff (Cosinus-ähnlich) für organische Delle
                float t = 1f - Mathf.Sqrt(distSq) / radius;
                float weight = t * t;
                _velocities[i] += localImpulse * weight;
            }
        }
    }

    /// <summary>
    /// Kontinuierlicher Druck – ideal in FixedUpdate aufrufen, solange der Spieler auf der Wand steht.
    /// </summary>
    public void ApplyWeightAtPoint(Vector2 worldPoint, Vector2 worldForce, float radius = 1.2f)
    {
        ApplyImpulseAtPoint(worldPoint, worldForce * Time.fixedDeltaTime, radius);
    }

    /// <summary>
    /// Gibt die nach innen zeigende Wand-Normale am nächsten Punkt zurück (perfekt für sauberes Abprallen).
    /// </summary>
    public Vector2 GetInwardNormalAtPoint(Vector2 worldPoint)
    {
        Vector2 localPoint = transform.InverseTransformPoint(worldPoint);
        int closestIdx = 0;
        float minDistSq = float.MaxValue;

        for (int i = 0; i < pointCount; i++)
        {
            float dSq = (_currentPositions[i] - localPoint).sqrMagnitude;
            if (dSq < minDistSq)
            {
                minDistSq = dSq;
                closestIdx = i;
            }
        }

        return transform.TransformDirection(_inwardNormals[closestIdx]).normalized;
    }

    // =========================================================================
    // HILFSMETHODEN FÜR GEOMETRIE
    // =========================================================================

    private static Vector2[] ResampleClosedPath(Vector2[] path, int count)
    {
        int n = path.Length;
        float[] cumLengths = new float[n + 1];
        cumLengths[0] = 0f;

        for (int i = 0; i < n; i++)
        {
            cumLengths[i + 1] = cumLengths[i] + Vector2.Distance(path[i], path[(i + 1) % n]);
        }

        float totalLength = cumLengths[n];
        Vector2[] result = new Vector2[count];
        int seg = 0;

        for (int i = 0; i < count; i++)
        {
            float targetDist = (i / (float)count) * totalLength;
            while (seg < n - 1 && cumLengths[seg + 1] < targetDist)
                seg++;

            float segLen = cumLengths[seg + 1] - cumLengths[seg];
            float t = segLen > 0.0001f ? (targetDist - cumLengths[seg]) / segLen : 0f;
            result[i] = Vector2.Lerp(path[seg], path[(seg + 1) % n], t);
        }

        return result;
    }

    private static float ComputeSignedArea(Vector2[] pts)
    {
        float area = 0f;
        for (int i = 0; i < pts.Length; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % pts.Length];
            area += (a.x * b.y - b.x * a.y);
        }
        return area * 0.5f;
    }
}
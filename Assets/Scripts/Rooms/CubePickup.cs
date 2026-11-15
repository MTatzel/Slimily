using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CubePickup : MonoBehaviour
{
    [Header("Visuelles Schweben")]
    [SerializeField] private float bobAmplitude = 0.18f;
    [SerializeField] private float bobFrequency = 2.8f;
    [SerializeField] private float rotateSpeed = 25f;

    [Header("Farb-Hinweis")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color normalCubeTint = new Color(0.35f, 0.95f, 1f, 1f);
    [SerializeField] private Color secretCubeTint = new Color(1f, 0.82f, 0.2f, 1f);

    private Vector2Int roomCoordinate;
    private bool isSecretCube;
    private Vector3 baseLocalPos;
    private bool isInitialized = false;
    private bool isCollected = false;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    public void Initialize(Vector2Int coord, bool secret)
    {
        roomCoordinate = coord;
        isSecretCube = secret;
        baseLocalPos = transform.localPosition;
        isInitialized = true;
        isCollected = false;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = isSecretCube ? secretCubeTint : normalCubeTint;
        }
    }

    private void Start()
    {
        if (!isInitialized)
        {
            baseLocalPos = transform.localPosition;
        }
    }

    private void Update()
    {
        // Sanftes Auf- und Abschweben im Raum
        float offsetY = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
        transform.localPosition = baseLocalPos + new Vector3(0f, offsetY, 0f);

        if (rotateSpeed != 0f)
        {
            transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCollected) return;

        bool isPlayer = other.CompareTag("Player") || other.GetComponent<PlatformerSlimeController>() != null;
        if (!isPlayer) return;

        if (CubeLootManager.Instance != null)
        {
            bool pickedUp = CubeLootManager.Instance.TryCollectCubeFromRoom(roomCoordinate, isSecretCube);
            if (pickedUp)
            {
                isCollected = true;
                Destroy(gameObject);
            }
        }
    }
}
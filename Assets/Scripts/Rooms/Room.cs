using System;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    public static event Action<Room, Direction> OnExitTriggered;

    [Header("Grid Koordinate")]
    [Tooltip("Koordinate dieses Raumes im MacroGrid")]
    [SerializeField] private Vector2Int gridCoordinate;
    public Vector2Int GridCoordinate
    {
        get => gridCoordinate;
        set => gridCoordinate = value;
    }

    [System.Serializable]
    public struct DoorSetup
    {
        public Direction direction;
        [Tooltip("Das Tür-Prefab in der 6-Tile-Lücke (Sprite + Collider auf Layer 'Ground')")]
        public GameObject wallBlocker;
        [Tooltip("Einstiegspunkt, wenn man aus dieser Richtung kommt")]
        public Transform spawnPoint;
        [Tooltip("Optional: Eigener SpriteRenderer für rotes Gefahren-Leuchten an dieser Tür")]
        public SpriteRenderer warningRenderer;
    }

    [Header("Exits & Spawns")]
    [SerializeField] private DoorSetup[] doors;

    [Header("Kamera-Begrenzung")]
    [Tooltip("Der Trigger-Collider (Box oder Polygon) für CinemachineConfiner2D")]
    [SerializeField] private Collider2D cameraBounds;
    public Collider2D CameraBounds => cameraBounds;

    [Header("Debug / Erzwingen")]
    [SerializeField] private bool debugForceOpenNorth;
    [SerializeField] private bool debugForceOpenEast;
    [SerializeField] private bool debugForceOpenSouth;
    [SerializeField] private bool debugForceOpenWest;

    // Automatisch erzeugte Fallback-Warnlichter pro Himmelsrichtung (falls warningRenderer leer ist)
    private readonly Dictionary<Direction, SpriteRenderer> runtimeWarningGlows = new Dictionary<Direction, SpriteRenderer>();
    private readonly HashSet<Direction> activeWarningDirections = new HashSet<Direction>();
    private static Sprite fallbackGlowSprite;

    private void Awake()
    {
        if (cameraBounds == null)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    cameraBounds = col;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        if (activeWarningDirections.Count == 0) return;

        // Sanftes rotes Pulsieren für alle bedrohten Nachbartüren
        float pulse = Mathf.PingPong(Time.time * 4.5f, 0.55f) + 0.25f;
        Color warnColor = new Color(1f, 0.15f, 0.1f, pulse);

        foreach (Direction dir in activeWarningDirections)
        {
            SpriteRenderer sr = GetOrCreateWarningRenderer(dir);
            if (sr != null)
            {
                sr.color = warnColor;
            }
        }
    }

    public void SetDoorOpen(Direction direction, bool isOpen)
    {
        if (doors == null) return;

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].direction == direction)
            {
                if (doors[i].wallBlocker != null)
                {
                    doors[i].wallBlocker.SetActive(!isOpen);
                }
                return;
            }
        }
    }

    /// <summary>
    /// Schaltet die rote Umwelt-Telegrafie an einer Tür an oder aus,
    /// wenn der Nachbarraum in dieser Richtung vom Boss anvisiert wird.
    /// </summary>
    public void SetDoorWarning(Direction direction, bool warn)
    {
        if (warn)
        {
            activeWarningDirections.Add(direction);
            SpriteRenderer sr = GetOrCreateWarningRenderer(direction);
            if (sr != null)
            {
                sr.gameObject.SetActive(true);
            }
        }
        else
        {
            activeWarningDirections.Remove(direction);
            if (runtimeWarningGlows.TryGetValue(direction, out SpriteRenderer sr) && sr != null)
            {
                sr.gameObject.SetActive(false);
            }
        }
    }

    private SpriteRenderer GetOrCreateWarningRenderer(Direction direction)
    {
        if (runtimeWarningGlows.TryGetValue(direction, out SpriteRenderer cached) && cached != null)
        {
            return cached;
        }

        if (doors == null) return null;

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].direction == direction)
            {
                if (doors[i].warningRenderer != null)
                {
                    runtimeWarningGlows[direction] = doors[i].warningRenderer;
                    return doors[i].warningRenderer;
                }

                // Kein eigenes Sprite zugewiesen -> Automatischen Warn-Schein am SpawnPoint erzeugen
                Transform anchor = doors[i].spawnPoint != null ? doors[i].spawnPoint : transform;
                GameObject glowObj = new GameObject($"DoorWarningGlow_{direction}");
                glowObj.transform.SetParent(anchor, false);
                glowObj.transform.localPosition = Vector3.zero;

                // Je nach Tür-Ausrichtung als schmalen Warn-Streifen im Durchgang skalieren
                bool isVerticalDoor = (direction == Direction.East || direction == Direction.West);
                glowObj.transform.localScale = isVerticalDoor ? new Vector3(1.4f, 5.2f, 1f) : new Vector3(5.2f, 1.4f, 1f);

                SpriteRenderer sr = glowObj.AddComponent<SpriteRenderer>();
                sr.sprite = GetFallbackGlowSprite();
                sr.sortingOrder = 15;
                sr.color = new Color(1f, 0.15f, 0.1f, 0.5f);

                runtimeWarningGlows[direction] = sr;
                return sr;
            }
        }

        return null;
    }

    private static Sprite GetFallbackGlowSprite()
    {
        if (fallbackGlowSprite != null) return fallbackGlowSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxRadius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        fallbackGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return fallbackGlowSprite;
    }

    public Transform GetSpawnPoint(Direction enterFromDirection)
    {
        if (doors != null)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i].direction == enterFromDirection)
                {
                    return doors[i].spawnPoint;
                }
            }
        }
        return transform;
    }

    public void TriggerExit(Direction dir)
    {
        OnExitTriggered?.Invoke(this, dir);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SetDoorOpen(Direction.North, debugForceOpenNorth);
        SetDoorOpen(Direction.East, debugForceOpenEast);
        SetDoorOpen(Direction.South, debugForceOpenSouth);
        SetDoorOpen(Direction.West, debugForceOpenWest);
    }
#endif
}
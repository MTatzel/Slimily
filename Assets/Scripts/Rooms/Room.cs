using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

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
        [Tooltip("Das Tür-Prefab in der Lücke (Door_North/South/East/West)")]
        public GameObject wallBlocker;
        [Tooltip("Fallback-Einstiegspunkt")]
        public Transform spawnPoint;
        [Tooltip("Optional: Eigener SpriteRenderer für rotes Gefahren-Leuchten an dieser Tür")]
        public SpriteRenderer warningRenderer;
    }

    [Header("Exits & Spawns")]
    [SerializeField] private DoorSetup[] doors;
    [Tooltip("Passt die Exit-Trigger beim Start automatisch an die volle Breite der Door_...-Blöcke an.")]
    [SerializeField] private bool autoAlignExitTriggersToDoors = true;

    [Header("Würfel-Spawnpunkte (Es wird maximal 1 pro Kategorie aktiv!)")]
    [SerializeField] private Transform[] normalCubeSpawnPoints;
    [SerializeField] private Transform[] secretCubeSpawnPoints;

    [Header("Altar-Spawnpunkt (Optional - sonst Raummitte)")]
    [SerializeField] private Transform altarSpawnPoint;

    [Header("Kamera-Begrenzung")]
    [SerializeField] private Collider2D cameraBounds;
    public Collider2D CameraBounds => cameraBounds;

    [Header("Debug / Erzwingen")]
    [SerializeField] private bool debugForceOpenNorth;
    [SerializeField] private bool debugForceOpenEast;
    [SerializeField] private bool debugForceOpenSouth;
    [SerializeField] private bool debugForceOpenWest;

    private readonly Dictionary<Direction, SpriteRenderer> runtimeWarningGlows = new Dictionary<Direction, SpriteRenderer>();
    private readonly HashSet<Direction> activeWarningDirections = new HashSet<Direction>();
    private readonly List<CubePickup> activePickups = new List<CubePickup>();
    private readonly Dictionary<Direction, Bounds> localDoorTunnelBounds = new Dictionary<Direction, Bounds>();
    private bool doorBoundsCached = false;
    private static Sprite fallbackGlowSprite;

    public AltarInteractable ActiveAltar { get; private set; }
    public int NormalSpawnPointCount => (normalCubeSpawnPoints != null && normalCubeSpawnPoints.Length > 0) ? normalCubeSpawnPoints.Length : 1;
    public int SecretSpawnPointCount => (secretCubeSpawnPoints != null && secretCubeSpawnPoints.Length > 0) ? secretCubeSpawnPoints.Length : 1;

    private void Awake()
    {
        if (cameraBounds == null)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            foreach (var col in colliders)
            {
                if (col.isTrigger && col.GetComponent<RoomExitTrigger>() == null)
                {
                    cameraBounds = col;
                    break;
                }
            }
        }

        CacheAllDoorTunnelBounds();
    }

    /// <summary>
    /// Liest einmalig die exakte 4x2-Türrahmen-Geometrie aus Door_North/South/East/West (wallBlocker) aus,
    /// völlig unabhängig davon, wie groß oder L-förmig der Raum ist.
    /// </summary>
    public void CacheAllDoorTunnelBounds()
    {
        if (doorBoundsCached || doors == null) return;

        for (int i = 0; i < doors.Length; i++)
        {
            Direction dir = doors[i].direction;
            GameObject blocker = doors[i].wallBlocker;
            Bounds localB = ExtractLocalBoundsFromBlocker(blocker, doors[i].spawnPoint);
            localDoorTunnelBounds[dir] = localB;
        }

        doorBoundsCached = true;

        if (autoAlignExitTriggersToDoors)
        {
            AlignExitTriggersToDoorTunnels();
        }
    }

    private Bounds ExtractLocalBoundsFromBlocker(GameObject blocker, Transform fallbackSpawn)
    {
        if (blocker != null)
        {
            bool wasActive = blocker.activeSelf;
            if (!wasActive) blocker.SetActive(true);
            Physics2D.SyncTransforms();

            Bounds worldBounds = default;
            bool found = false;

            // 1. Prüfen ob Door_... eine Tilemap nutzt
            Tilemap tm = blocker.GetComponentInChildren<Tilemap>();
            if (tm != null)
            {
                tm.CompressBounds();
                if (tm.cellBounds.size.x > 0 && tm.cellBounds.size.y > 0)
                {
                    Vector3 minW = tm.transform.TransformPoint(tm.localBounds.min);
                    Vector3 maxW = tm.transform.TransformPoint(tm.localBounds.max);
                    worldBounds.SetMinMax(Vector3.Min(minW, maxW), Vector3.Max(minW, maxW));
                    found = true;
                }
            }

            // 2. Prüfen ob Door_... einen Collider2D hat
            if (!found)
            {
                Collider2D col = blocker.GetComponentInChildren<Collider2D>();
                if (col != null && col.bounds.size.sqrMagnitude > 0.05f)
                {
                    worldBounds = col.bounds;
                    found = true;
                }
            }

            // 3. Prüfen ob Door_... einen SpriteRenderer hat
            if (!found)
            {
                Renderer rend = blocker.GetComponentInChildren<Renderer>();
                if (rend != null && rend.bounds.size.sqrMagnitude > 0.05f)
                {
                    worldBounds = rend.bounds;
                    found = true;
                }
            }

            if (!wasActive) blocker.SetActive(false);

            if (found)
            {
                Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
                return new Bounds(localCenter, worldBounds.size);
            }
        }

        if (fallbackSpawn != null)
        {
            Vector3 localPos = transform.InverseTransformPoint(fallbackSpawn.position);
            return new Bounds(localPos, new Vector3(4f, 2f, 1f));
        }

        return new Bounds(Vector3.zero, new Vector3(4f, 2f, 1f));
    }

    /// <summary>
    /// Passt die Exit_North/South/East/West Trigger automatisch an die äußere Kante des 4x2-Türrahmens an,
    /// damit man auf jeder Höhe/Seite der Tür zuverlässig den Übergang auslöst.
    /// </summary>
    private void AlignExitTriggersToDoorTunnels()
    {
        RoomExitTrigger[] triggers = GetComponentsInChildren<RoomExitTrigger>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            RoomExitTrigger trig = triggers[i];
            if (trig == null) continue;

            BoxCollider2D box = trig.GetComponent<BoxCollider2D>();
            if (box == null) continue;

            Direction dir = trig.ExitDirection;
            if (!localDoorTunnelBounds.TryGetValue(dir, out Bounds tunnel)) continue;

            Vector3 worldTunnelCenter = transform.TransformPoint(tunnel.center);
            Vector3 triggerWorldCenter = worldTunnelCenter;
            Vector2 triggerSize = box.size;

            if (dir == Direction.North)
            {
                float stripH = Mathf.Min(0.75f, tunnel.size.y * 0.45f);
                triggerWorldCenter.y = (worldTunnelCenter.y + tunnel.extents.y) - stripH * 0.5f;
                triggerSize = new Vector2(tunnel.size.x, stripH);
            }
            else if (dir == Direction.South)
            {
                float stripH = Mathf.Min(0.75f, tunnel.size.y * 0.45f);
                triggerWorldCenter.y = (worldTunnelCenter.y - tunnel.extents.y) + stripH * 0.5f;
                triggerSize = new Vector2(tunnel.size.x, stripH);
            }
            else if (dir == Direction.East)
            {
                float stripW = Mathf.Min(0.75f, tunnel.size.x * 0.45f);
                triggerWorldCenter.x = (worldTunnelCenter.x + tunnel.extents.x) - stripW * 0.5f;
                triggerSize = new Vector2(stripW, tunnel.size.y);
            }
            else if (dir == Direction.West)
            {
                float stripW = Mathf.Min(0.75f, tunnel.size.x * 0.45f);
                triggerWorldCenter.x = (worldTunnelCenter.x - tunnel.extents.x) + stripW * 0.5f;
                triggerSize = new Vector2(stripW, tunnel.size.y);
            }

            box.transform.position = triggerWorldCenter;
            box.offset = Vector2.zero;
            box.size = triggerSize;
            box.isTrigger = true;
        }
    }

    public Bounds GetDoorTunnelWorldBounds(Direction dir)
    {
        if (!doorBoundsCached)
        {
            CacheAllDoorTunnelBounds();
        }

        if (localDoorTunnelBounds.TryGetValue(dir, out Bounds localB))
        {
            return new Bounds(transform.TransformPoint(localB.center), localB.size);
        }

        Transform sp = GetSpawnPoint(dir);
        return new Bounds(sp != null ? sp.position : transform.position, new Vector3(4f, 2f, 1f));
    }

    private void Update()
    {
        if (activeWarningDirections.Count == 0) return;

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

    public Transform GetNormalSpawnPoint(int index)
    {
        if (normalCubeSpawnPoints != null && normalCubeSpawnPoints.Length > 0)
        {
            int safeIndex = Mathf.Clamp(index, 0, normalCubeSpawnPoints.Length - 1);
            if (normalCubeSpawnPoints[safeIndex] != null)
            {
                return normalCubeSpawnPoints[safeIndex];
            }
        }
        return transform;
    }

    public Transform GetSecretSpawnPoint(int index)
    {
        if (secretCubeSpawnPoints != null && secretCubeSpawnPoints.Length > 0)
        {
            int safeIndex = Mathf.Clamp(index, 0, secretCubeSpawnPoints.Length - 1);
            if (secretCubeSpawnPoints[safeIndex] != null)
            {
                return secretCubeSpawnPoints[safeIndex];
            }
        }
        return transform;
    }

    public Vector3 GetAltarWorldPosition()
    {
        return altarSpawnPoint != null ? altarSpawnPoint.position : transform.position;
    }

    public void RegisterSpawnedAltar(AltarInteractable altar)
    {
        ActiveAltar = altar;
    }

    public void ClearSpawnedAltar()
    {
        if (ActiveAltar != null)
        {
            Destroy(ActiveAltar.gameObject);
            ActiveAltar = null;
        }
    }

    public void RegisterSpawnedPickup(CubePickup pickup)
    {
        if (pickup != null)
        {
            activePickups.Add(pickup);
        }
    }

    public void ClearSpawnedPickups()
    {
        for (int i = activePickups.Count - 1; i >= 0; i--)
        {
            if (activePickups[i] != null)
            {
                Destroy(activePickups[i].gameObject);
            }
        }
        activePickups.Clear();
    }

    [ContextMenu("Nächsten Spawnpunkt in diesem Raum testen (Switch)")]
    public void DebugSwitchSpawnPoint()
    {
        if (CubeLootManager.Instance != null)
        {
            CubeLootManager.Instance.CycleActiveRoomSpawnPoint(this);
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

                Bounds tunnel = GetDoorTunnelWorldBounds(direction);
                GameObject glowObj = new GameObject($"DoorWarningGlow_{direction}");
                glowObj.transform.SetParent(transform, true);
                glowObj.transform.position = tunnel.center;

                bool isVerticalDoor = (direction == Direction.East || direction == Direction.West);
                glowObj.transform.localScale = isVerticalDoor ? new Vector3(1.4f, 4.5f, 1f) : new Vector3(4.5f, 1.4f, 1f);

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

    private void OnDrawGizmos()
    {
        if (normalCubeSpawnPoints != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < normalCubeSpawnPoints.Length; i++)
            {
                if (normalCubeSpawnPoints[i] != null)
                {
                    Gizmos.DrawWireSphere(normalCubeSpawnPoints[i].position, 0.45f);
                    Gizmos.DrawWireCube(normalCubeSpawnPoints[i].position, new Vector3(0.5f, 0.5f, 0.5f));
                }
            }
        }

        if (secretCubeSpawnPoints != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 1f);
            for (int i = 0; i < secretCubeSpawnPoints.Length; i++)
            {
                if (secretCubeSpawnPoints[i] != null)
                {
                    Gizmos.DrawWireSphere(secretCubeSpawnPoints[i].position, 0.5f);
                    Gizmos.DrawWireCube(secretCubeSpawnPoints[i].position, new Vector3(0.6f, 0.6f, 0.6f));
                }
            }
        }

        if (altarSpawnPoint != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.9f, 1f);
            Gizmos.DrawWireSphere(altarSpawnPoint.position, 0.7f);
            Gizmos.DrawWireCube(altarSpawnPoint.position, new Vector3(0.8f, 1.2f, 0.5f));
        }
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
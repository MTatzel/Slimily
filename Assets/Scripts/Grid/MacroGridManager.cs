using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class MacroGridManager : MonoBehaviour
{
    public static MacroGridManager Instance { get; private set; }

    [System.Serializable]
    public struct RoomEntry
    {
        public Vector2Int coordinate;
        public Room roomPrefab;
    }

    [Header("Raum-Konfiguration")]
    [Tooltip("Liste aller vordefinierten Räume im Grid und ihrer Prefabs")]
    [SerializeField] private List<RoomEntry> roomEntries = new List<RoomEntry>();

    [Tooltip("Standard-Prefab, falls für eine Koordinate kein spezifisches Prefab hinterlegt ist")]
    [SerializeField] private Room defaultRoomPrefab;

    [Header("Streaming & Platzierung")]
    [Tooltip("Welt-Abstand zwischen Räumen (Standard: 100 Einheiten)")]
    [SerializeField] private float roomSpacing = 100f;

    [Tooltip("Startkoordinate des Slimes beim Spielstart")]
    [SerializeField] private Vector2Int startCoordinate = Vector2Int.zero;

    [Tooltip("Wenn aktiv: Entfernte Räume werden zerstört statt deaktiviert")]
    [SerializeField] private bool destroyFarRooms = false;

    [Header("Spieler-Referenz")]
    [Tooltip("Referenz zum Slime / Spieler (wird automatisch gesucht, falls leer)")]
    [SerializeField] private Transform playerTransform;

    [Header("Cinemachine 3.x Kamera")]
    [Tooltip("Haupt-CinemachineCamera (wird automatisch gesucht, falls leer)")]
    [SerializeField] private CinemachineCamera cinemachineCam;

    [Tooltip("Confiner2D Komponente der Kamera (wird automatisch gesucht, falls leer)")]
    [SerializeField] private CinemachineConfiner2D cinemachineConfiner;

    // Das 2D-Raster (Koordinate -> Raum-Instanz)
    private readonly Dictionary<Vector2Int, Room> grid = new Dictionary<Vector2Int, Room>();

    // Vorlagen pro Koordinate für das spätere Instanziieren
    private readonly Dictionary<Vector2Int, Room> roomPrefabs = new Dictionary<Vector2Int, Room>();

    private Vector2Int currentCoordinate;
    private float lastTransitionTime = -1f;
    private const float TransitionCooldown = 0.25f;

    public Vector2Int CurrentCoordinate => currentCoordinate;
    public IReadOnlyDictionary<Vector2Int, Room> Grid => grid;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        Room.OnExitTriggered += HandleRoomExit;
    }

    private void OnDisable()
    {
        Room.OnExitTriggered -= HandleRoomExit;
    }

    /// <summary>
    /// Wird vom GameManager aufgerufen, um das Level datenbasiert aufzubauen.
    /// </summary>
    public void Initialize(LevelConfiguration config)
    {
        // 1. Werte aus der Config übernehmen
        if (config != null)
        {
            roomSpacing = config.roomSpacing;
            if (config.defaultRoomPrefab != null)
            {
                defaultRoomPrefab = config.defaultRoomPrefab;
            }
            startCoordinate = config.startCoordinate;
        }

        FindPlayerIfNeeded();
        FindCameraIfNeeded();

        // 2. Bestehende Räume aufräumen (falls vorher schon ein Level lief)
        foreach (var room in grid.Values)
        {
            if (room != null)
            {
                Destroy(room.gameObject);
            }
        }
        grid.Clear();
        roomPrefabs.Clear();

        // 3. Vorlagen aus Inspector-Liste neu registrieren
        InitializeGrid();

        // 4. Startraum registrieren (falls noch nicht in roomEntries vorhanden)
        if (!roomPrefabs.ContainsKey(startCoordinate) && defaultRoomPrefab != null)
        {
            RegisterRoom(startCoordinate, defaultRoomPrefab);
        }

        // 5. Startraum und Umfeld aktivieren
        currentCoordinate = startCoordinate;
        UpdateActiveRooms(currentCoordinate);

        // 6. Makro-Marker auf der Karte synchronisieren
        if (MacroSlimeMarker.Instance != null)
        {
            MacroSlimeMarker.Instance.SetGridPosition(currentCoordinate);
        }

        // 7. Kamera initial auf den Startraum eichen
        if (grid.TryGetValue(currentCoordinate, out Room startRoom) && startRoom != null)
        {
            UpdateCameraConfiner(startRoom);
        }
    }

    private void InitializeGrid()
    {
        foreach (var entry in roomEntries)
        {
            if (entry.roomPrefab != null)
            {
                if (entry.roomPrefab.gameObject.scene.IsValid())
                {
                    entry.roomPrefab.GridCoordinate = entry.coordinate;
                    entry.roomPrefab.transform.position = GetWorldPosition(entry.coordinate);
                    grid[entry.coordinate] = entry.roomPrefab;
                }
                else
                {
                    roomPrefabs[entry.coordinate] = entry.roomPrefab;
                    grid[entry.coordinate] = null;
                }
            }
        }
    }

    /// <summary>
    /// Aktualisiert das 5er-Fenster (aktueller Raum + 4 Nachbarn).
    /// </summary>
    public void UpdateActiveRooms(Vector2Int center)
    {
        currentCoordinate = center;
        HashSet<Vector2Int> targetCoords = GetActiveWindowCoordinates(center);

        // 1. Die 5 aktiven Räume instanziieren / aktivieren
        foreach (Vector2Int coord in targetCoords)
        {
            if (HasRoom(coord))
            {
                Room room = GetOrCreateRoom(coord);
                if (room != null)
                {
                    room.gameObject.SetActive(true);
                    room.transform.position = GetWorldPosition(coord);
                    room.GridCoordinate = coord;
                }
            }
        }

        // 2. Weiter entfernte Räume deaktivieren oder zerstören
        List<Vector2Int> loadedCoords = new List<Vector2Int>(grid.Keys);
        foreach (Vector2Int coord in loadedCoords)
        {
            if (!targetCoords.Contains(coord))
            {
                Room room = grid[coord];
                if (room != null)
                {
                    if (destroyFarRooms)
                    {
                        Destroy(room.gameObject);
                        grid[coord] = null;
                    }
                    else
                    {
                        room.gameObject.SetActive(false);
                    }
                }
            }
        }

        // 3. Türen für alle aktiven Räume anpassen
        foreach (Vector2Int coord in targetCoords)
        {
            if (grid.TryGetValue(coord, out Room room) && room != null && room.gameObject.activeSelf)
            {
                UpdateRoomDoors(room, coord);
            }
        }
    }

    private HashSet<Vector2Int> GetActiveWindowCoordinates(Vector2Int center)
    {
        return new HashSet<Vector2Int>
        {
            center,
            center + Vector2Int.up,    // Nord
            center + Vector2Int.right, // Ost
            center + Vector2Int.down,  // Süd
            center + Vector2Int.left   // West
        };
    }

    private Room GetOrCreateRoom(Vector2Int coord)
    {
        if (grid.TryGetValue(coord, out Room existingRoom) && existingRoom != null)
        {
            return existingRoom;
        }

        Room prefabToSpawn = null;
        if (roomPrefabs.TryGetValue(coord, out Room specificPrefab) && specificPrefab != null)
        {
            prefabToSpawn = specificPrefab;
        }
        else if (defaultRoomPrefab != null)
        {
            prefabToSpawn = defaultRoomPrefab;
        }

        if (prefabToSpawn != null)
        {
            Vector3 worldPos = GetWorldPosition(coord);
            Room newRoom = Instantiate(prefabToSpawn, worldPos, Quaternion.identity, transform);
            newRoom.GridCoordinate = coord;
            newRoom.name = $"{prefabToSpawn.name}_[{coord.x},{coord.y}]";
            grid[coord] = newRoom;
            return newRoom;
        }

        return null;
    }

    private void UpdateRoomDoors(Room room, Vector2Int coord)
    {
        room.SetDoorOpen(Direction.North, HasRoom(coord + Vector2Int.up));
        room.SetDoorOpen(Direction.East, HasRoom(coord + Vector2Int.right));
        room.SetDoorOpen(Direction.South, HasRoom(coord + Vector2Int.down));
        room.SetDoorOpen(Direction.West, HasRoom(coord + Vector2Int.left));
    }

    private void HandleRoomExit(Room fromRoom, Direction exitDirection)
    {
        if (Time.time - lastTransitionTime < TransitionCooldown)
        {
            return;
        }

        Vector2Int sourceCoord = (fromRoom != null) ? fromRoom.GridCoordinate : currentCoordinate;
        Vector2Int targetCoord = sourceCoord + DirectionToOffset(exitDirection);

        if (!HasRoom(targetCoord))
        {
            Debug.LogWarning($"[MacroGridManager] Kein Raum in Richtung {exitDirection} bei Koordinate {targetCoord} vorhanden!");
            return;
        }

        lastTransitionTime = Time.time;

        // 1. 5er-Fenster aktualisieren
        UpdateActiveRooms(targetCoord);

        // 2. Makro-Marker auf der Karte nachziehen
        if (MacroSlimeMarker.Instance != null)
        {
            MacroSlimeMarker.Instance.SetGridPosition(targetCoord);
        }

        if (!grid.TryGetValue(targetCoord, out Room targetRoom) || targetRoom == null)
        {
            Debug.LogError($"[MacroGridManager] Zielraum bei {targetCoord} nicht auffindbar!");
            return;
        }

        // 3. Gegenüberliegende Tür als Einstiegspunkt wählen
        Direction enterFromDirection = GetOppositeDirection(exitDirection);
        Transform spawnPoint = targetRoom.GetSpawnPoint(enterFromDirection);

        // 4. Teleportation des Spielers durchführen
        Vector3 previousPos = (playerTransform != null) ? playerTransform.position : Vector3.zero;
        Vector3 targetPos = (spawnPoint != null) ? spawnPoint.position : GetWorldPosition(currentCoordinate);
        
        TeleportPlayer(targetPos, exitDirection);

        // 5. Cinemachine 3.x anpassen & warpen
        Vector3 deltaPosition = targetPos - previousPos;
        UpdateCameraForTransition(targetRoom, deltaPosition);
    }

    private void TeleportPlayer(Vector3 targetPos, Direction exitDirection)
    {
        FindPlayerIfNeeded();

        if (playerTransform == null)
        {
            Debug.LogError("[MacroGridManager] Kein Spieler gefunden zum Teleportieren!");
            return;
        }

        playerTransform.position = targetPos;

        if (playerTransform.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.position = targetPos;

            Vector2 forwardDir = DirectionToOffset(exitDirection);
            if (Vector2.Dot(rb.linearVelocity, forwardDir) < 2f)
            {
                rb.linearVelocity = forwardDir * 4f; 
            }
        }

        if (playerTransform.TryGetComponent<PlatformerSlimeController>(out var slimeController))
        {
            slimeController.TriggerDetach(0.15f);
        }
    }

    private void UpdateCameraConfiner(Room targetRoom)
    {
        FindCameraIfNeeded();

        if (cinemachineConfiner != null && targetRoom != null && targetRoom.CameraBounds != null)
        {
            cinemachineConfiner.BoundingShape2D = targetRoom.CameraBounds;
            cinemachineConfiner.InvalidateBoundingShapeCache();
        }
    }

    private void UpdateCameraForTransition(Room targetRoom, Vector3 deltaPosition)
    {
        FindCameraIfNeeded();

        UpdateCameraConfiner(targetRoom);

        if (cinemachineCam != null && playerTransform != null)
        {
            cinemachineCam.OnTargetObjectWarped(playerTransform, deltaPosition);
        }
    }

    public Vector3 GetWorldPosition(Vector2Int gridCoord)
    {
        return new Vector3(gridCoord.x * roomSpacing, gridCoord.y * roomSpacing, 0f);
    }

    public bool HasRoom(Vector2Int coord)
    {
        return roomPrefabs.ContainsKey(coord) || (grid.TryGetValue(coord, out Room r) && r != null);
    }

    public void RegisterRoom(Vector2Int coord, Room roomPrefab)
    {
        roomPrefabs[coord] = roomPrefab;
        if (!grid.ContainsKey(coord))
        {
            grid[coord] = null;
        }
    }

    /// <summary>
    /// Zerstört einen Raum an der angegebenen Koordinate und entfernt ihn aus dem Grid.
    /// Wird unter anderem bei Boss-Angriffen aufgerufen.
    /// </summary>
    public void RemoveRoomAt(Vector2Int coord)
    {
        if (grid.TryGetValue(coord, out Room roomInstance) && roomInstance != null)
        {
            Destroy(roomInstance.gameObject);
        }

        grid.Remove(coord);
        roomPrefabs.Remove(coord);

        // Türen der umgebenden Räume aktualisieren, damit geschlossene Wände entstehen
        UpdateActiveRooms(currentCoordinate);
    }

    public static Vector2Int DirectionToOffset(Direction dir)
    {
        return dir switch
        {
            Direction.North => Vector2Int.up,
            Direction.South => Vector2Int.down,
            Direction.East => Vector2Int.right,
            Direction.West => Vector2Int.left,
            _ => Vector2Int.zero
        };
    }

    public static Direction GetOppositeDirection(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => dir
        };
    }

    private void FindPlayerIfNeeded()
    {
        if (playerTransform != null) return;

        var slime = FindAnyObjectByType<PlatformerSlimeController>();
        if (slime != null)
        {
            playerTransform = slime.transform;
            return;
        }

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
    }

    private void FindCameraIfNeeded()
    {
        if (cinemachineCam == null)
        {
            cinemachineCam = FindAnyObjectByType<CinemachineCamera>();
        }

        if (cinemachineConfiner == null && cinemachineCam != null)
        {
            cinemachineConfiner = cinemachineCam.GetComponent<CinemachineConfiner2D>();
        }
    }

    /// <summary>
    /// Teleportiert den Slime in den Startraum anhand der Grid-Koordinate.
    /// </summary>
    public void PlaceSlimeInRoom(Vector2Int coord, Transform slimeTransform)
    {
        if (slimeTransform == null) return;

        Vector3 targetPos = GetWorldPosition(coord) + new Vector3(0f, 2.5f, 0f);
        slimeTransform.position = targetPos;

        if (slimeTransform.TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.position = targetPos;
            rb.linearVelocity = Vector2.zero;
        }

        FindCameraIfNeeded();
        if (cinemachineCam != null)
        {
            cinemachineCam.OnTargetObjectWarped(slimeTransform, targetPos - slimeTransform.position);
        }
    }
}
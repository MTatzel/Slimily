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
    [SerializeField] private List<RoomEntry> roomEntries = new List<RoomEntry>();
    [SerializeField] private Room defaultRoomPrefab;

    [Header("Streaming & Platzierung")]
    [SerializeField] private float roomSpacing = 100f;
    [SerializeField] private Vector2Int startCoordinate = Vector2Int.zero;
    [SerializeField] private bool destroyFarRooms = false;

    [Header("Spieler-Referenz")]
    [SerializeField] private Transform playerTransform;

    [Header("Cinemachine 3.x Kamera")]
    [SerializeField] private CinemachineCamera cinemachineCam;
    [SerializeField] private CinemachineConfiner2D cinemachineConfiner;

    private readonly Dictionary<Vector2Int, Room> grid = new Dictionary<Vector2Int, Room>();
    private readonly Dictionary<Vector2Int, Room> roomPrefabs = new Dictionary<Vector2Int, Room>();
    private readonly HashSet<Vector2Int> visitedRooms = new HashSet<Vector2Int>();

    private Vector2Int currentCoordinate;
    private float lastTransitionTime = -1f;
    private const float TransitionCooldown = 0.25f;

    public Vector2Int CurrentCoordinate => currentCoordinate;
    public IReadOnlyDictionary<Vector2Int, Room> Grid => grid;
    public int VisitedRoomCount => visitedRooms.Count;

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

    public bool IsRoomVisited(Vector2Int coord)
    {
        return visitedRooms.Contains(coord);
    }

    public int GetRoomEntryFocusCost(Vector2Int targetCoord)
    {
        if (GameManager.Instance == null) return 0;

        return IsRoomVisited(targetCoord)
            ? GameManager.Instance.EnterClearedRoomFocusCost
            : GameManager.Instance.EnterNewRoomFocusCost;
    }

    public bool CanEnterRoomAt(Vector2Int targetCoord)
    {
        return HasRoom(targetCoord);
    }

    /// <summary>
    /// Prüft auf dem Makro-Grid, ob eine bestimmte Koordinate aktuell vom Boss anvisiert wird.
    /// </summary>
    public bool IsCellTargetedByBoss(Vector2Int coord)
    {
        if (GridManager.Instance == null || GridManager.Instance.grid == null) return false;
        if (coord.x < 0 || coord.x >= GridManager.Instance.width ||
            coord.y < 0 || coord.y >= GridManager.Instance.height)
        {
            return false;
        }

        GridCell cell = GridManager.Instance.grid[coord.x, coord.y];
        return cell != null && cell.isTargeted;
    }

    public bool IsCurrentRoomTargeted()
    {
        return IsCellTargetedByBoss(currentCoordinate);
    }

    public void Initialize(LevelConfiguration config)
    {
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

        foreach (var room in grid.Values)
        {
            if (room != null)
            {
                Destroy(room.gameObject);
            }
        }
        grid.Clear();
        roomPrefabs.Clear();
        visitedRooms.Clear();

        InitializeGrid();

        if (!roomPrefabs.ContainsKey(startCoordinate) && defaultRoomPrefab != null)
        {
            RegisterRoom(startCoordinate, defaultRoomPrefab);
        }

        currentCoordinate = startCoordinate;
        visitedRooms.Add(startCoordinate);

        UpdateActiveRooms(currentCoordinate);

        if (MacroSlimeMarker.Instance != null)
        {
            MacroSlimeMarker.Instance.SetGridPosition(currentCoordinate);
        }

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

    public void UpdateActiveRooms(Vector2Int center)
    {
        currentCoordinate = center;
        HashSet<Vector2Int> targetCoords = GetActiveWindowCoordinates(center);

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

        RefreshActiveRoomDoors();
    }

    public void RefreshActiveRoomDoors()
    {
        HashSet<Vector2Int> targetCoords = GetActiveWindowCoordinates(currentCoordinate);
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
            center + Vector2Int.up,
            center + Vector2Int.right,
            center + Vector2Int.down,
            center + Vector2Int.left
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
        Vector2Int north = coord + Vector2Int.up;
        Vector2Int east = coord + Vector2Int.right;
        Vector2Int south = coord + Vector2Int.down;
        Vector2Int west = coord + Vector2Int.left;

        bool openNorth = HasRoom(north);
        bool openEast = HasRoom(east);
        bool openSouth = HasRoom(south);
        bool openWest = HasRoom(west);

        // 1. Türen zu gebauten Nachbarräumen öffnen
        room.SetDoorOpen(Direction.North, openNorth);
        room.SetDoorOpen(Direction.East, openEast);
        room.SetDoorOpen(Direction.South, openSouth);
        room.SetDoorOpen(Direction.West, openWest);

        // 2. UMWELT-TELEGRAFIE: Wenn ein Nachbarraum vom Boss anvisiert ist, glüht die Tür rot!
        room.SetDoorWarning(Direction.North, openNorth && IsCellTargetedByBoss(north));
        room.SetDoorWarning(Direction.East, openEast && IsCellTargetedByBoss(east));
        room.SetDoorWarning(Direction.South, openSouth && IsCellTargetedByBoss(south));
        room.SetDoorWarning(Direction.West, openWest && IsCellTargetedByBoss(west));
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

        bool wasAlreadyVisited = IsRoomVisited(targetCoord);
        int entryTickCost = GetRoomEntryFocusCost(targetCoord);

        lastTransitionTime = Time.time;
        visitedRooms.Add(targetCoord);

        UpdateActiveRooms(targetCoord);

        if (MacroSlimeMarker.Instance != null)
        {
            MacroSlimeMarker.Instance.SetGridPosition(targetCoord);
        }

        if (grid.TryGetValue(targetCoord, out Room targetRoom) && targetRoom != null)
        {
            Direction enterFromDirection = GetOppositeDirection(exitDirection);
            Transform spawnPoint = targetRoom.GetSpawnPoint(enterFromDirection);

            Vector3 previousPos = (playerTransform != null) ? playerTransform.position : Vector3.zero;
            Vector3 targetPos = (spawnPoint != null) ? spawnPoint.position : GetWorldPosition(currentCoordinate);

            TeleportPlayer(targetPos, exitDirection);

            Vector3 deltaPosition = targetPos - previousPos;
            UpdateCameraForTransition(targetRoom, deltaPosition);
        }

        if (!wasAlreadyVisited)
        {
            if (GameManager.Instance != null && GameManager.Instance.inventoryManager != null)
            {
                GameManager.Instance.inventoryManager.AddRandomShape();
                GameManager.Instance.inventoryManager.RefreshAllPreviews();
                Debug.Log($"[Clear-Draw] Neuer Raum {targetCoord} betreten -> +1 Karte auf die Hand gezogen!");
            }
        }

        if (GameManager.Instance != null && entryTickCost > 0)
        {
            GameManager.Instance.AdvanceTicks(entryTickCost, $"Neuen Raum {targetCoord} betreten");
        }
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

    public void RemoveRoomAt(Vector2Int coord)
    {
        if (coord == startCoordinate)
        {
            Debug.Log("[MacroGridManager] Der Hub-Raum ist unzerstörbar!");
            return;
        }

        bool slimeWasInDestroyedRoom = (coord == currentCoordinate);

        if (grid.TryGetValue(coord, out Room roomInstance) && roomInstance != null)
        {
            Destroy(roomInstance.gameObject);
        }

        grid.Remove(coord);
        roomPrefabs.Remove(coord);
        visitedRooms.Remove(coord);

        if (GridManager.Instance != null && GridManager.Instance.grid != null &&
            coord.x >= 0 && coord.x < GridManager.Instance.width &&
            coord.y >= 0 && coord.y < GridManager.Instance.height)
        {
            GridManager.Instance.grid[coord.x, coord.y].DestroyBlock();
        }

        if (slimeWasInDestroyedRoom)
        {
            Vector2Int fallbackCoord = FindNearestBuiltRoom(coord);
            Debug.LogWarning($"[Boss-Treffer] Raum {coord} unter dem Slime zerstört! Slime wird nach {fallbackCoord} geschleudert.");

            currentCoordinate = fallbackCoord;
            visitedRooms.Add(fallbackCoord);
            UpdateActiveRooms(currentCoordinate);

            if (MacroSlimeMarker.Instance != null)
            {
                MacroSlimeMarker.Instance.SetGridPosition(currentCoordinate);
            }

            FindPlayerIfNeeded();
            PlaceSlimeInRoom(currentCoordinate, playerTransform);

            if (grid.TryGetValue(currentCoordinate, out Room fallbackRoom) && fallbackRoom != null)
            {
                UpdateCameraConfiner(fallbackRoom);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddNightmare(GameManager.Instance.NightmareOnBossRoomDestroy, "Raum unter Slime vom Boss zerstört");
            }
        }
        else
        {
            UpdateActiveRooms(currentCoordinate);
        }
    }

    private Vector2Int FindNearestBuiltRoom(Vector2Int fromCoord)
    {
        Vector2Int[] neighbors = new Vector2Int[]
        {
            fromCoord + Vector2Int.down,
            fromCoord + Vector2Int.up,
            fromCoord + Vector2Int.left,
            fromCoord + Vector2Int.right
        };

        foreach (var n in neighbors)
        {
            if (HasRoom(n)) return n;
        }

        float bestDist = float.MaxValue;
        Vector2Int bestCoord = startCoordinate;

        foreach (var kvp in roomPrefabs)
        {
            float d = Vector2Int.Distance(fromCoord, kvp.Key);
            if (d < bestDist)
            {
                bestDist = d;
                bestCoord = kvp.Key;
            }
        }

        if (!HasRoom(bestCoord) && defaultRoomPrefab != null)
        {
            RegisterRoom(startCoordinate, defaultRoomPrefab);
            if (GridManager.Instance != null)
            {
                GridManager.Instance.SetCellState(startCoordinate.x, startCoordinate.y, CellState.Built);
            }
            return startCoordinate;
        }

        return bestCoord;
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
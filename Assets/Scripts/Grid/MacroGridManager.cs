using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

public class MacroGridManager : MonoBehaviour
{
    public static MacroGridManager Instance { get; private set; }

    [System.Serializable]
    public struct RoomEntry
    {
        public Vector2Int coordinate;
        public Room roomPrefab;
    }

    [Header("Raum-Konfiguration & Fallbacks")]
    [SerializeField] private List<RoomEntry> roomEntries = new List<RoomEntry>();
    [Tooltip("Wird für den Start-Hub und als Fallback genutzt, falls ein Biom-Ordner noch leer ist.")]
    [SerializeField] private Room defaultRoomPrefab;

    [Header("Biom- & Attribut-Pools")]
    [SerializeField] private BiomeRoomPoolManager roomPoolManager;

    [Header("Streaming & Platzierung (Weit getrennt gegen Überlappung)")]
    [SerializeField] private float roomSpacing = 200f;
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
    private bool isRoomTransitionRunning = false;
    private const float TransitionCooldown = 0.18f;

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

        EnsurePoolManager();
    }

    private void EnsurePoolManager()
    {
        if (roomPoolManager == null)
        {
            roomPoolManager = GetComponent<BiomeRoomPoolManager>();
            if (roomPoolManager == null)
            {
                roomPoolManager = gameObject.AddComponent<BiomeRoomPoolManager>();
            }
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

    public Room GetRoomPrefabForCell(ZoneType primaryBiome, ZoneType fallbackResonanceBiome, RoomAttributeType attribute)
    {
        EnsurePoolManager();
        return roomPoolManager.DrawRoomForCell(primaryBiome, fallbackResonanceBiome, attribute, defaultRoomPrefab);
    }

    public Room DrawRoomPrefabForBiome(ZoneType primaryBiome, ZoneType fallbackResonanceBiome = ZoneType.Forest)
    {
        return GetRoomPrefabForCell(primaryBiome, fallbackResonanceBiome, RoomAttributeType.None);
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
            roomSpacing = Mathf.Max(100f, config.roomSpacing);
            if (config.defaultRoomPrefab != null)
            {
                defaultRoomPrefab = config.defaultRoomPrefab;
            }
            startCoordinate = config.startCoordinate;
        }

        FindPlayerIfNeeded();
        FindCameraIfNeeded();
        EnsurePoolManager();

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

        if (CubeLootManager.Instance != null)
        {
            CubeLootManager.Instance.ClearAllLootStates();
        }
        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.ClearAllAltars();
        }

        roomPoolManager.ResetAndShuffleAllPools();

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
                    bool wasInactive = !room.gameObject.activeSelf;
                    room.gameObject.SetActive(true);
                    room.transform.position = GetWorldPosition(coord);
                    room.GridCoordinate = coord;
                    room.CacheAllDoorTunnelBounds();

                    if (wasInactive && CubeLootManager.Instance != null)
                    {
                        CubeLootManager.Instance.RefreshRoomLoot(room, coord);
                    }
                    if (wasInactive && AltarManager.Instance != null)
                    {
                        AltarManager.Instance.RefreshRoomAltar(room, coord);
                    }
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
        Physics2D.SyncTransforms();
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
        else if (GridManager.Instance != null && GridManager.Instance.grid != null &&
                 coord.x >= 0 && coord.x < GridManager.Instance.width &&
                 coord.y >= 0 && coord.y < GridManager.Instance.height &&
                 coord != startCoordinate)
        {
            GridCell cell = GridManager.Instance.grid[coord.x, coord.y];
            prefabToSpawn = GetRoomPrefabForCell(cell.zoneType, cell.zoneType, cell.currentAttribute);
            roomPrefabs[coord] = prefabToSpawn;
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
            newRoom.CacheAllDoorTunnelBounds();
            grid[coord] = newRoom;

            if (CubeLootManager.Instance != null)
            {
                CubeLootManager.Instance.RefreshRoomLoot(newRoom, coord);
            }
            if (AltarManager.Instance != null)
            {
                AltarManager.Instance.RefreshRoomAltar(newRoom, coord);
            }

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

        room.SetDoorOpen(Direction.North, openNorth);
        room.SetDoorOpen(Direction.East, openEast);
        room.SetDoorOpen(Direction.South, openSouth);
        room.SetDoorOpen(Direction.West, openWest);

        room.SetDoorWarning(Direction.North, openNorth && IsCellTargetedByBoss(north));
        room.SetDoorWarning(Direction.East, openEast && IsCellTargetedByBoss(east));
        room.SetDoorWarning(Direction.South, openSouth && IsCellTargetedByBoss(south));
        room.SetDoorWarning(Direction.West, openWest && IsCellTargetedByBoss(west));
    }

 private void HandleRoomExit(Room fromRoom, Direction exitDirection)
    {
        if (isRoomTransitionRunning || Time.time - lastTransitionTime < TransitionCooldown)
        {
            return;
        }

        Vector2Int sourceCoord = (fromRoom != null) ? fromRoom.GridCoordinate : currentCoordinate;
        Vector2Int targetCoord = sourceCoord + DirectionToOffset(exitDirection);

        if (!HasRoom(targetCoord))
        {
            return;
        }

        StartCoroutine(RoomTransitionRoutine(fromRoom, exitDirection, targetCoord));
    }

private IEnumerator RoomTransitionRoutine(Room fromRoom, Direction exitDirection, Vector2Int targetCoord)
    {
        isRoomTransitionRunning = true;
        lastTransitionTime = Time.time;

        FindPlayerIfNeeded();
        PlatformerSlimeController slimeController = playerTransform != null ? playerTransform.GetComponent<PlatformerSlimeController>() : null;
        Rigidbody2D slimeRb = playerTransform != null ? playerTransform.GetComponent<Rigidbody2D>() : null;

        // 1. Slime sofort einfrieren (bevor irgendetwas passiert)
        if (slimeController != null) slimeController.enabled = false;
        if (slimeRb != null) slimeRb.simulated = false;

        // 2. Komplett auf Schwarz ausblenden
        if (RoomTransitionFader.Instance != null)
        {
            yield return StartCoroutine(RoomTransitionFader.Instance.FadeOutRoutine(exitDirection));
        }

        // Sicherstellen, dass mindestens 1 komplett schwarzer Frame gerendert wurde
        yield return new WaitForEndOfFrame();

        // 3. Raumwechsel, Teleport & Kamera-Update im komplett schwarzen Bild
        bool wasAlreadyVisited = IsRoomVisited(targetCoord);
        int entryTickCost = GetRoomEntryFocusCost(targetCoord);
        visitedRooms.Add(targetCoord);

        Vector3 previousPos = (playerTransform != null) ? playerTransform.position : Vector3.zero;

        UpdateActiveRooms(targetCoord);

        if (MacroSlimeMarker.Instance != null)
        {
            MacroSlimeMarker.Instance.SetGridPosition(targetCoord);
        }

        if (grid.TryGetValue(targetCoord, out Room targetRoom) && targetRoom != null)
        {
            Vector3 finalTargetPos = targetRoom.transform.position;

            if (slimeController != null)
            {
                finalTargetPos = slimeController.ExecuteRoomTransition(fromRoom, targetRoom, exitDirection, previousPos);
            }

            Vector3 deltaPosition = finalTargetPos - previousPos;
            UpdateCameraForTransition(targetRoom, deltaPosition);

            // Cinemachine zwingen, Damping zu überspringen und sofort einzurasten
            if (cinemachineCam != null)
            {
                cinemachineCam.PreviousStateIsValid = false;
            }
        }

        if (BiomeResonanceManager.Instance != null)
        {
            BiomeResonanceManager.Instance.OnPlayerMovedInDirection(exitDirection, !wasAlreadyVisited);
        }

        if (GameManager.Instance != null && entryTickCost > 0)
        {
            GameManager.Instance.AdvanceTicks(entryTickCost, $"Raum {targetCoord} betreten");
        }

        // 4. Im Dunkeln 2 Frames warten, bis Cinemachine + Confiner komplett auf dem neuen Raum eingerastet sind
        yield return null;
        yield return null;

        // 5. Erst jetzt wieder hell werden (Slime bleibt währenddessen noch eingefroren!)
        if (RoomTransitionFader.Instance != null)
        {
            yield return StartCoroutine(RoomTransitionFader.Instance.FadeInRoutine(exitDirection));
        }

        // 6. Bild ist wieder da -> Slime freigeben
        if (slimeRb != null) slimeRb.simulated = true;
        if (slimeController != null) slimeController.enabled = true;

        lastTransitionTime = Time.time;
        isRoomTransitionRunning = false;
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
        roomPrefabs[coord] = roomPrefab != null ? roomPrefab : defaultRoomPrefab;
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

        if (CubeLootManager.Instance != null)
        {
            CubeLootManager.Instance.OnRoomDestroyedByBoss(coord);
        }
        if (AltarManager.Instance != null)
        {
            AltarManager.Instance.OnRoomDestroyedByBoss(coord);
        }

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

        Vector3 oldPos = slimeTransform.position;
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
            cinemachineCam.OnTargetObjectWarped(slimeTransform, targetPos - oldPos);
        }
    }
}
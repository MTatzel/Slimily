using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CoordinateLootState
{
    public Vector2Int coordinate;

    public bool hasNormalCube;
    public bool normalCollected;
    public int normalSpawnIndex = -1;

    public bool hasSecretCube;
    public bool secretCollected;
    public int secretSpawnIndex = -1;
}

public class CubeLootManager : MonoBehaviour
{
    public static CubeLootManager Instance { get; private set; }

    [Header("Pickup-Prefabs (Optional - hat automatischen Fallback!)")]
    [Tooltip("Dein CubePickup-Prefab für den garantierten Würfel im Raum.")]
    public CubePickup normalCubePickupPrefab;
    [Tooltip("Optionales eigenes Prefab für versteckte Bonus-Würfel (nutzt sonst normalCubePickupPrefab).")]
    public CubePickup secretCubePickupPrefab;

    [Header("Spawn-Regeln pro gelegtem 6er-Würfel")]
    [Tooltip("Wie viele garantierte Würfel in jedem gelegten 6er-Block spawnen (Standard: 1).")]
    [Range(1, 3)] public int guaranteedCubesPerShape = 1;

    [Tooltip("Wahrscheinlichkeit (0 bis 1), dass in einem der anderen Räume des 6er-Blocks zusätzlich ein versteckter Bonus-Würfel spawnt.")]
    [Range(0f, 1f)] public float secretCubeChancePerShape = 1.0f;

    [Header("Debug / Testen")]
    [Tooltip("Erlaubt mit Taste P im Raum das Umschalten des aktiven Spawnpunkts zum schnellen Testen.")]
    public bool enableSpawnSwitchKeyP = true;

    // Speichert den Loot-Zustand pro Grid-Koordinate (damit mehrfach genutzte Raum-Prefabs aus dem Pool nie kollidieren)
    private readonly Dictionary<Vector2Int, CoordinateLootState> lootByCoord = new Dictionary<Vector2Int, CoordinateLootState>();

    private static Sprite fallbackCubeSprite;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    private void Update()
    {
        // Mit Taste P im aktuellen Raum den Spawnpunkt live durchschalten
        if (enableSpawnSwitchKeyP && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            CycleActiveRoomSpawnPoint();
        }
    }

    public void ClearAllLootStates()
    {
        lootByCoord.Clear();
    }

    /// <summary>
    /// Wird vom PlacementManager aufgerufen, sobald ein 6er-Würfel auf das Grid gelegt wird.
    /// Wählt aus den 6 Koordinaten genau die Räume aus, die tatsächlich einen Würfel erhalten!
    /// </summary>
    public void RegisterPlacedCluster(List<Vector2Int> clusterCoords)
    {
        if (clusterCoords == null || clusterCoords.Count == 0) return;

        List<Vector2Int> availableCoords = new List<Vector2Int>(clusterCoords);
        ShuffleCoords(availableCoords);

        // 1. Alle 6 Koordinaten des gelegten Würfels initialisieren (Standard: leer)
        foreach (Vector2Int coord in clusterCoords)
        {
            lootByCoord[coord] = new CoordinateLootState
            {
                coordinate = coord,
                hasNormalCube = false,
                normalCollected = false,
                normalSpawnIndex = -1,
                hasSecretCube = false,
                secretCollected = false,
                secretSpawnIndex = -1
            };
        }

        int index = 0;

        // 2. Garantierte(n) Würfel auf 1 (oder X) der 6 Räume verteilen
        int normalCount = Mathf.Min(guaranteedCubesPerShape, availableCoords.Count);
        for (int i = 0; i < normalCount; i++)
        {
            Vector2Int chosen = availableCoords[index++];
            lootByCoord[chosen].hasNormalCube = true;
            Debug.Log($"[CubeLootManager] Garantierter Würfel zugewiesen an Raum {chosen}.");
        }

        // 3. Optionalen Geheim-/Bonus-Würfel auf einen der verbleibenden Räume des 6er-Blocks legen
        if (index < availableCoords.Count && Random.value <= secretCubeChancePerShape)
        {
            Vector2Int secretChosen = availableCoords[index++];
            lootByCoord[secretChosen].hasSecretCube = true;
            Debug.Log($"[CubeLootManager] Versteckter Bonus-Würfel zugewiesen an Raum {secretChosen}.");
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn MacroGridManager einen Raum aktiviert oder spawnt.
    /// </summary>
    public void RefreshRoomLoot(Room room, Vector2Int coord)
    {
        if (room == null) return;

        // Zuerst alte Pickup-Instanzen in dieser Raum-Instanz aufräumen
        room.ClearSpawnedPickups();

        if (!lootByCoord.TryGetValue(coord, out CoordinateLootState state))
        {
            // Raum hat keinen Loot-Eintrag (z.B. Start-Hub)
            return;
        }

        // 1. Normaler Würfel
        if (state.hasNormalCube && !state.normalCollected)
        {
            int spawnCount = room.NormalSpawnPointCount;
            if (spawnCount > 0)
            {
                if (state.normalSpawnIndex < 0 || state.normalSpawnIndex >= spawnCount)
                {
                    state.normalSpawnIndex = Random.Range(0, spawnCount);
                }

                Transform spawnPoint = room.GetNormalSpawnPoint(state.normalSpawnIndex);
                CubePickup pickup = CreatePickupInstance(spawnPoint, room.transform, isSecret: false);
                pickup.Initialize(coord, secret: false);
                room.RegisterSpawnedPickup(pickup);
            }
        }

        // 2. Versteckter Bonus-Würfel
        if (state.hasSecretCube && !state.secretCollected)
        {
            int secretCount = room.SecretSpawnPointCount;
            if (secretCount > 0)
            {
                if (state.secretSpawnIndex < 0 || state.secretSpawnIndex >= secretCount)
                {
                    state.secretSpawnIndex = Random.Range(0, secretCount);
                }

                Transform spawnPoint = room.GetSecretSpawnPoint(state.secretSpawnIndex);
                CubePickup pickup = CreatePickupInstance(spawnPoint, room.transform, isSecret: true);
                pickup.Initialize(coord, secret: true);
                room.RegisterSpawnedPickup(pickup);
            }
        }
    }

    /// <summary>
    /// Wird von CubePickup aufgerufen, sobald der Slime den Würfel im Raum berührt.
    /// </summary>
    public bool TryCollectCubeFromRoom(Vector2Int coord, bool isSecret)
    {
        InventoryManager inv = GameManager.Instance != null ? GameManager.Instance.inventoryManager : FindAnyObjectByType<InventoryManager>();
        if (inv == null) return false;

        if (!inv.CanAddCube())
        {
            Debug.Log("[CubeLootManager] Inventar ist voll! Baue zuerst einen Würfel, bevor du diesen einsammelst.");
            return false;
        }

        if (lootByCoord.TryGetValue(coord, out CoordinateLootState state))
        {
            if (isSecret) state.secretCollected = true;
            else state.normalCollected = true;
        }

        inv.AddFoundCubeToInventory(isSecret);
        return true;
    }

    /// <summary>
    /// Schaltet im aktuellen Raum den Spawnpunkt auf den nächsten Platz weiter (zum Testen mit Taste P oder über den Inspector).
    /// </summary>
    public void CycleActiveRoomSpawnPoint(Room specificRoom = null)
    {
        if (MacroGridManager.Instance == null) return;

        Vector2Int coord = specificRoom != null ? specificRoom.GridCoordinate : MacroGridManager.Instance.CurrentCoordinate;
        if (!MacroGridManager.Instance.Grid.TryGetValue(coord, out Room room) || room == null) return;

        if (!lootByCoord.TryGetValue(coord, out CoordinateLootState state))
        {
            // Falls wir im Hub testen wollen, legen wir kurz einen Test-Eintrag an
            state = new CoordinateLootState
            {
                coordinate = coord,
                hasNormalCube = true,
                normalCollected = false,
                normalSpawnIndex = 0
            };
            lootByCoord[coord] = state;
        }

        if (room.NormalSpawnPointCount > 0)
        {
            state.hasNormalCube = true;
            state.normalCollected = false;
            state.normalSpawnIndex = (state.normalSpawnIndex + 1) % room.NormalSpawnPointCount;
            Debug.Log($"[CubeLootManager] Raum {coord}: Normal-Spawnpunkt gewechselt auf Index {state.normalSpawnIndex}.");
        }

        if (room.SecretSpawnPointCount > 0 && state.hasSecretCube)
        {
            state.secretCollected = false;
            state.secretSpawnIndex = (state.secretSpawnIndex + 1) % room.SecretSpawnPointCount;
        }

        RefreshRoomLoot(room, coord);
    }

    public void OnRoomDestroyedByBoss(Vector2Int coord)
    {
        lootByCoord.Remove(coord);
    }

    private CubePickup CreatePickupInstance(Transform spawnPoint, Transform roomTransform, bool isSecret)
    {
        Vector3 worldPos = spawnPoint != null ? spawnPoint.position : roomTransform.position;
        CubePickup prefabToUse = isSecret && secretCubePickupPrefab != null ? secretCubePickupPrefab : normalCubePickupPrefab;

        if (prefabToUse != null)
        {
            return Instantiate(prefabToUse, worldPos, Quaternion.identity, roomTransform);
        }

        // Automatischer Fallback, falls im Inspector noch kein Prefab zugewiesen wurde:
        GameObject fallbackObj = new GameObject(isSecret ? "SecretCubePickup_Auto" : "NormalCubePickup_Auto");
        fallbackObj.transform.SetParent(roomTransform, false);
        fallbackObj.transform.position = worldPos;
        fallbackObj.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

        SpriteRenderer sr = fallbackObj.AddComponent<SpriteRenderer>();
        sr.sprite = GetFallbackCubeSprite();
        sr.sortingOrder = 18;

        BoxCollider2D col = fallbackObj.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 0.9f);
        col.isTrigger = true;

        return fallbackObj.AddComponent<CubePickup>();
    }

    private static Sprite GetFallbackCubeSprite()
    {
        if (fallbackCubeSprite != null) return fallbackCubeSprite;

        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = (x < 3 || x >= size - 3 || y < 3 || y >= size - 3);
                bool isInnerCore = (x >= 10 && x < size - 10 && y >= 10 && y < size - 10);

                if (isBorder)
                    tex.SetPixel(x, y, Color.white);
                else if (isInnerCore)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0.95f));
                else
                    tex.SetPixel(x, y, new Color(0.7f, 0.9f, 1f, 0.45f));
            }
        }
        tex.Apply();

        fallbackCubeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        return fallbackCubeSprite;
    }

    private void ShuffleCoords(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            Vector2Int tmp = list[i];
            list[i] = list[swap];
            list[swap] = tmp;
        }
    }
}
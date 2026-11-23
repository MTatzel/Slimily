using System;
using System.Collections.Generic;
using UnityEngine;

public class BossDirector : MonoBehaviour
{
    public static BossDirector Instance { get; private set; }

    [Header("Verfügbare Boss-Profile")]
    [Tooltip("Alle Boss-Profile, die in diesem Level/Run erscheinen können.")]
    [SerializeField] private List<BossProfile> availableBosses = new List<BossProfile>();

    [Header("Laufzeit-Status (Read-Only)")]
    [SerializeField] private BossProfile activeProfile;
    [SerializeField] private GameObject activeBossObject;
    [SerializeField] private Vector2Int activeBossCoord;

    // Events für UI-Banner, ThreatTracker, Sound oder Kamera-Rumble
    public static event Action<BossProfile, GameObject, Vector2Int> OnBossSpawned;
    public static event Action<BossProfile, string> OnBossDespawned;

    public bool HasActiveBoss => activeBossObject != null;
    public BossProfile ActiveProfile => activeProfile;
    public GameObject ActiveBossObject => activeBossObject;
    public Vector2Int ActiveBossCoord => activeBossCoord;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    /// <summary>
    /// Zentraler Einstiegspunkt für alle Trigger (Albtraum, Ticks, Events, Debug).
    /// Erzwingt die harte 1-Boss-Regel.
    /// </summary>
    public bool TryTriggerBoss(BossProfile specificProfile = null, string triggerReason = "Unbekannt")
    {
        // Harte 1-Boss-Regel
        if (HasActiveBoss)
        {
            Debug.Log($"[BossDirector] Trigger '{triggerReason}' ignoriert – Boss '{activeProfile?.displayName}' ist bereits aktiv.");
            return false;
        }

        BossProfile profileToSpawn = specificProfile != null ? specificProfile : PickRandomProfile();
        if (profileToSpawn == null)
        {
            Debug.LogWarning("[BossDirector] Kein BossProfile zugewiesen!");
            return false;
        }

        if (!TryFindSpawnCoordinate(profileToSpawn, out Vector2Int spawnCoord))
        {
            Debug.LogWarning($"[BossDirector] Keine gültige Spawn-Koordinate für '{profileToSpawn.displayName}' gefunden.");
            return false;
        }

        SpawnBossAt(profileToSpawn, spawnCoord, triggerReason);
        return true;
    }

    /// <summary>
    /// Entfernt den aktiven Boss sauber (z. B. nach Sieg, Rückzug oder Level-Reset).
    /// </summary>
    public void DespawnActiveBoss(string reason = "Besiegt")
    {
        if (!HasActiveBoss) return;

        BossProfile oldProfile = activeProfile;
        Destroy(activeBossObject);

        activeBossObject = null;
        activeProfile = null;
        activeBossCoord = Vector2Int.zero;

        Debug.Log($"[BossDirector] Boss '{oldProfile?.displayName}' entfernt ({reason}).");
        OnBossDespawned?.Invoke(oldProfile, reason);
    }

    public void UpdateActiveBossCoordinate(Vector2Int newCoord)
    {
        activeBossCoord = newCoord;
    }

    private void SpawnBossAt(BossProfile profile, Vector2Int coord, string triggerReason)
    {
        if (GridManager.Instance == null) return;

        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        Vector3 localPos = new Vector3(coord.x * offset, coord.y * offset, -0.6f);

        GameObject bossObj;
        if (profile.bossPrefab != null)
        {
            bossObj = Instantiate(profile.bossPrefab, GridManager.Instance.transform);
            bossObj.transform.localPosition = localPos;
        }
        else
        {
            // Fallback-Platzhalter, falls noch kein Prefab im Profil hinterlegt ist
            bossObj = CreateFallbackBossVisual(profile, localPos);
        }

        bossObj.name = $"ActiveBoss_{profile.bossId}";

        activeProfile = profile;
        activeBossObject = bossObj;
        activeBossCoord = coord;

        Debug.Log($"[BossDirector] '{profile.displayName}' gespawnt bei {coord} (Auslöser: {triggerReason}).");
        OnBossSpawned?.Invoke(profile, bossObj, coord);
    }

    private bool TryFindSpawnCoordinate(BossProfile profile, out Vector2Int resultCoord)
    {
        resultCoord = Vector2Int.zero;
        if (GridManager.Instance == null || GridManager.Instance.grid == null) return false;

        Vector2Int slimeCoord = MacroGridManager.Instance != null
            ? MacroGridManager.Instance.CurrentCoordinate
            : new Vector2Int(GridManager.Instance.width / 2, GridManager.Instance.height / 2);

        Vector2Int hubCoord = MacroGridManager.Instance != null
            ? MacroGridManager.Instance.StartCoordinate
            : slimeCoord;

        int minDist = Mathf.Min(profile.minSpawnDistanceFromSlime, profile.maxSpawnDistanceFromSlime);
        int maxDist = Mathf.Max(profile.minSpawnDistanceFromSlime, profile.maxSpawnDistanceFromSlime);

        List<Vector2Int> candidates = new List<Vector2Int>();

        for (int x = 0; x < GridManager.Instance.width; x++)
        {
            for (int y = 0; y < GridManager.Instance.height; y++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                if (coord == slimeCoord || coord == hubCoord) continue;

                int distToSlime = GetChebyshevDistance(coord, slimeCoord);
                if (distToSlime < minDist || distToSlime > maxDist) continue;

                if (GetChebyshevDistance(coord, hubCoord) < profile.minDistanceFromHub) continue;

                if (!MatchesSurfaceRule(coord, profile.surfaceRule)) continue;

                candidates.Add(coord);
            }
        }

        if (candidates.Count == 0)
        {
            // Fallback: Oberflächenregel lockern, falls z. B. noch keine gebauten Räume im Ring liegen
            for (int x = 0; x < GridManager.Instance.width; x++)
            {
                for (int y = 0; y < GridManager.Instance.height; y++)
                {
                    Vector2Int coord = new Vector2Int(x, y);
                    if (coord == slimeCoord || coord == hubCoord) continue;

                    int distToSlime = GetChebyshevDistance(coord, slimeCoord);
                    if (distToSlime >= minDist && distToSlime <= maxDist)
                    {
                        candidates.Add(coord);
                    }
                }
            }
        }

        if (candidates.Count == 0) return false;

        resultCoord = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    private bool MatchesSurfaceRule(Vector2Int coord, BossSpawnSurfaceRule rule)
    {
        GridCell cell = GridManager.Instance.grid[coord.x, coord.y];
        if (cell == null) return false;

        bool isBuilt = cell.currentState == CellState.Built;

        return rule switch
        {
            BossSpawnSurfaceRule.VoidOnly => !isBuilt,
            BossSpawnSurfaceRule.BuiltRoomsOnly => isBuilt,
            _ => true
        };
    }

    private BossProfile PickRandomProfile()
    {
        List<BossProfile> valid = availableBosses.FindAll(p => p != null);
        if (valid.Count == 0) return null;
        return valid[UnityEngine.Random.Range(0, valid.Count)];
    }

    private int GetChebyshevDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private GameObject CreateFallbackBossVisual(BossProfile profile, Vector3 localPos)
    {
        GameObject obj = new GameObject("Boss_Fallback");
        obj.transform.SetParent(GridManager.Instance.transform, false);
        obj.transform.localPosition = localPos;

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        sr.color = profile.themeColor;
        sr.sortingOrder = 20;
        return obj;
    }

    // --- Inspector Debug-Buttons (Rechtsklick auf die Komponente im Play-Mode) ---

    [ContextMenu("Debug: Zufälligen Boss jetzt spawnen")]
    private void DebugSpawnRandomBoss()
    {
        TryTriggerBoss(null, "Inspector-Debug");
    }

    [ContextMenu("Debug: Aktiven Boss entfernen")]
    private void DebugDespawnBoss()
    {
        DespawnActiveBoss("Inspector-Debug");
    }
}
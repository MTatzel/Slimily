using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BiomeWeightEntry
{
    public ZoneType zoneType;
    public string displayName;
    [Range(0, 100)] public int percentage;
    [HideInInspector] public int builtTilesOnGrid;

    public BiomeWeightEntry(ZoneType type, string name, int startPercent)
    {
        zoneType = type;
        displayName = name;
        percentage = startPercent;
        builtTilesOnGrid = 0;
    }
}

public class BiomeResonanceManager : MonoBehaviour
{
    public static BiomeResonanceManager Instance { get; private set; }

    [Header("Alle verfügbaren Biome (0% wird im HUD ausgeblendet!)")]
    public List<BiomeWeightEntry> resonances = new List<BiomeWeightEntry>();

    [Header("Feste Prozent-Schritte (z.B. 20% -> 0, 20, 40, 60, 80, 100)")]
    public int stepSize = 20;

    [Header("Trigger 1: Distanz & Himmelsrichtung")]
    public int hubSafeRadius = 2;
    public bool onlyShiftOnNewRooms = true;
    public ZoneType northBiome = ZoneType.Ruins;
    public ZoneType eastBiome = ZoneType.Ocean;
    public ZoneType southBiome = ZoneType.Forest;
    public ZoneType westBiome = ZoneType.Swamp;

    [Header("Trigger 2: Boss manipuliert die Leiste")]
    public bool bossInfluencesResonance = true;
    public ZoneType bossInjectedBiome = ZoneType.PurpleNightmare;

    [Header("Trigger 3: Hoher Albtraum-Druck")]
    public bool nightmareBleedsIntoResonance = true;
    [Range(30f, 90f)] public float nightmareBleedThreshold = 60f;

    public event Action OnResonanceChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        ResetToDefaultResonances();
    }

    public void ResetToDefaultResonances()
    {
        resonances.Clear();
        resonances.Add(new BiomeWeightEntry(ZoneType.Forest, "Wald", 100));
        resonances.Add(new BiomeWeightEntry(ZoneType.Ruins, "Ruinen", 0));
        resonances.Add(new BiomeWeightEntry(ZoneType.Ocean, "Meer", 0));
        resonances.Add(new BiomeWeightEntry(ZoneType.Swamp, "Sumpf", 0));
        resonances.Add(new BiomeWeightEntry(ZoneType.PurpleNightmare, "Albtraum", 0));

        OnResonanceChanged?.Invoke();
    }

    public void OnPlayerMovedInDirection(Direction dir, bool isNewRoom)
    {
        if (onlyShiftOnNewRooms && !isNewRoom) return;

        if (MacroGridManager.Instance != null && GridManager.Instance != null)
        {
            Vector2Int current = MacroGridManager.Instance.CurrentCoordinate;
            Vector2Int hub = new Vector2Int(GridManager.Instance.width / 2, GridManager.Instance.height / 2);
            int distFromHub = Mathf.Max(Mathf.Abs(current.x - hub.x), Mathf.Abs(current.y - hub.y));

            if (distFromHub <= hubSafeRadius)
            {
                ShiftResonanceByStep(ZoneType.Forest, 1);
                return;
            }
        }

        ZoneType targetBiome = dir switch
        {
            Direction.North => northBiome,
            Direction.East => eastBiome,
            Direction.South => southBiome,
            Direction.West => westBiome,
            _ => ZoneType.Forest
        };

        ShiftResonanceByStep(targetBiome, 1);
    }

    public void OnBossPulseTriggered()
    {
        if (!bossInfluencesResonance) return;
        ShiftResonanceByStep(bossInjectedBiome, 1);
    }

    public void ShiftResonanceByStep(ZoneType targetZone, int steps = 1)
    {
        int safeStep = Mathf.Clamp(stepSize, 5, 50);
        BiomeWeightEntry target = GetOrAddEntry(targetZone);

        for (int s = 0; s < steps; s++)
        {
            if (target.percentage >= 100) break;

            BiomeWeightEntry donor = null;
            int maxOtherPct = 0;

            for (int i = 0; i < resonances.Count; i++)
            {
                BiomeWeightEntry candidate = resonances[i];
                if (candidate.zoneType != targetZone && candidate.percentage > maxOtherPct)
                {
                    maxOtherPct = candidate.percentage;
                    donor = candidate;
                }
            }

            if (donor != null && donor.percentage > 0)
            {
                int transfer = Mathf.Min(safeStep, donor.percentage);
                donor.percentage -= transfer;
                target.percentage += transfer;
            }
        }

        OnResonanceChanged?.Invoke();
    }

    public void ShiftResonance(ZoneType targetZone, float deltaAmount)
    {
        int steps = Mathf.Max(1, Mathf.RoundToInt(deltaAmount / Mathf.Max(5f, stepSize)));
        ShiftResonanceByStep(targetZone, steps);
    }

    public void AssignBiomesToShape(ShapeData shape)
    {
        if (shape == null || shape.category != CardCategory.CubeShape) return;

        foreach (ShapeCell cell in shape.cells)
        {
            if (!cell.hasAssignedZone)
            {
                cell.assignedZone = RollBiomeForSingleCell();
                cell.hasAssignedZone = true;
            }
        }
    }

    public ZoneType RollBiomeForSingleCell()
    {
        if (resonances.Count == 0) return ZoneType.Forest;

        int total = 0;
        for (int i = 0; i < resonances.Count; i++)
        {
            total += Mathf.Max(0, resonances[i].percentage);
        }

        if (total <= 0) return ZoneType.Forest;

        int roll = UnityEngine.Random.Range(0, total);
        int cumulative = 0;

        for (int i = 0; i < resonances.Count; i++)
        {
            int pct = Mathf.Max(0, resonances[i].percentage);
            if (pct == 0) continue;

            cumulative += pct;
            if (roll < cumulative)
            {
                return resonances[i].zoneType;
            }
        }

        return ZoneType.Forest;
    }

    public void RegisterBuiltTile(ZoneType zone)
    {
        if (zone == ZoneType.Hub) return;
        BiomeWeightEntry entry = GetOrAddEntry(zone);
        entry.builtTilesOnGrid++;
        OnResonanceChanged?.Invoke();
    }

    public void UnregisterBuiltTile(ZoneType zone)
    {
        if (zone == ZoneType.Hub) return;
        BiomeWeightEntry entry = GetOrAddEntry(zone);
        entry.builtTilesOnGrid = Mathf.Max(0, entry.builtTilesOnGrid - 1);
        OnResonanceChanged?.Invoke();
    }

    public int GetBuiltTileCount(ZoneType zone)
    {
        for (int i = 0; i < resonances.Count; i++)
        {
            if (resonances[i].zoneType == zone) return resonances[i].builtTilesOnGrid;
        }
        return 0;
    }

    private BiomeWeightEntry GetOrAddEntry(ZoneType zone)
    {
        for (int i = 0; i < resonances.Count; i++)
        {
            if (resonances[i].zoneType == zone) return resonances[i];
        }

        string label = zone switch
        {
            ZoneType.Forest => "Wald",
            ZoneType.Ocean => "Meer",
            ZoneType.Ruins => "Ruinen",
            ZoneType.Swamp => "Sumpf",
            ZoneType.PurpleNightmare => "Albtraum",
            _ => zone.ToString()
        };

        BiomeWeightEntry newEntry = new BiomeWeightEntry(zone, label, 0);
        resonances.Add(newEntry);
        return newEntry;
    }
}
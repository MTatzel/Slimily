using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BiomeWeightEntry
{
    public ZoneType zoneType;
    public string displayName;
    [Range(0f, 100f)] public float percentage;
    [HideInInspector] public int builtTilesOnGrid;

    public BiomeWeightEntry(ZoneType type, string name, float startPercent)
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

    [Header("Aktive Biom-Resonanzen (Werden automatisch auf 100% normiert)")]
    public List<BiomeWeightEntry> resonances = new List<BiomeWeightEntry>();

    [Header("Richtungs-Einfluss beim Laufen (Im Inspector frei einstellbar)")]
    [Tooltip("Welches Biom wird stärker, wenn der Spieler nach NORDEN läuft?")]
    public ZoneType northBiome = ZoneType.Ruins;
    [Tooltip("Welches Biom wird stärker, wenn der Spieler nach OSTEN läuft?")]
    public ZoneType eastBiome = ZoneType.Ocean;
    [Tooltip("Welches Biom wird stärker, wenn der Spieler nach SÜDEN läuft?")]
    public ZoneType southBiome = ZoneType.Forest;
    [Tooltip("Welches Biom wird stärker, wenn der Spieler nach WESTEN läuft?")]
    public ZoneType westBiome = ZoneType.Forest;

    [Header("Änderungs-Stärke")]
    [Tooltip("Um wie viel Prozent verschiebt sich die Resonanz bei einem Schritt in eine Richtung?")]
    [Range(1f, 35f)] public float shiftPerRoomStep = 8f;
    [Tooltip("Wenn aktiv, ändert sich die Resonanz nur beim Erschließen NEUER Räume (verhindert Farmen durch Hin-und-Her-Laufen).")]
    public bool onlyShiftOnNewRooms = false;

    public event Action OnResonanceChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);

        if (resonances.Count == 0)
        {
            ResetToDefaultResonances();
        }
        NormalizePercentages();
    }

    public void ResetToDefaultResonances()
    {
        resonances.Clear();
        resonances.Add(new BiomeWeightEntry(ZoneType.Forest, "Wald", 80f));
        resonances.Add(new BiomeWeightEntry(ZoneType.Ruins, "Ruinen", 10f));
        resonances.Add(new BiomeWeightEntry(ZoneType.Ocean, "Meer", 10f));
        NormalizePercentages();
        OnResonanceChanged?.Invoke();
    }

    /// <summary>
    /// Wird vom MacroGridManager aufgerufen, sobald der Slime einen Raum in Richtung 'dir' verlässt.
    /// </summary>
    public void OnPlayerMovedInDirection(Direction dir, bool isNewRoom)
    {
        if (onlyShiftOnNewRooms && !isNewRoom) return;

        ZoneType targetBiome = dir switch
        {
            Direction.North => northBiome,
            Direction.East => eastBiome,
            Direction.South => southBiome,
            Direction.West => westBiome,
            _ => ZoneType.Forest
        };

        ShiftResonance(targetBiome, shiftPerRoomStep);
    }

    /// <summary>
    /// Modulare Schnittstelle: Verschiebt die Resonanz zugunsten von 'targetZone'
    /// (nutzbar für Richtungswechsel, Boss-Aktionen, Schreine oder Albtraum-Schwellenwerte).
    /// </summary>
    public void ShiftResonance(ZoneType targetZone, float deltaAmount)
    {
        if (deltaAmount <= 0f || resonances.Count == 0) return;

        BiomeWeightEntry targetEntry = GetOrAddEntry(targetZone);
        targetEntry.percentage += deltaAmount;

        NormalizePercentages();
        OnResonanceChanged?.Invoke();
    }

    /// <summary>
    /// Würfelt für alle 6 Blöcke einer gezogenen Form die Biom-Farben anhand der aktuellen Resonanz aus.
    /// </summary>
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

        float total = 0f;
        for (int i = 0; i < resonances.Count; i++)
        {
            total += Mathf.Max(0f, resonances[i].percentage);
        }

        if (total <= 0.01f) return resonances[0].zoneType;

        float roll = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;

        for (int i = 0; i < resonances.Count; i++)
        {
            cumulative += Mathf.Max(0f, resonances[i].percentage);
            if (roll <= cumulative)
            {
                return resonances[i].zoneType;
            }
        }

        return resonances[resonances.Count - 1].zoneType;
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

        BiomeWeightEntry newEntry = new BiomeWeightEntry(zone, label, 0f);
        resonances.Add(newEntry);
        return newEntry;
    }

    private void NormalizePercentages()
    {
        float sum = 0f;
        for (int i = 0; i < resonances.Count; i++)
        {
            resonances[i].percentage = Mathf.Max(0f, resonances[i].percentage);
            sum += resonances[i].percentage;
        }

        if (sum <= 0.001f)
        {
            if (resonances.Count > 0) resonances[0].percentage = 100f;
            return;
        }

        for (int i = 0; i < resonances.Count; i++)
        {
            resonances[i].percentage = (resonances[i].percentage / sum) * 100f;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (resonances != null && resonances.Count > 0)
        {
            NormalizePercentages();
        }
    }
#endif
}
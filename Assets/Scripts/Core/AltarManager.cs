using System;
using System.Collections.Generic;
using UnityEngine;

public enum AltarUsageMode
{
    Combo_OneTimeLoot_CooldownTeleport,
    FixedCharges,
    TickCooldown
}

public enum AltarStatus
{
    Inactive,
    Active,
    OnCooldown,
    Exhausted,
    Corrupted
}

[Serializable]
public class AltarStateData
{
    public Vector2Int coordinate;
    public ZoneType biome;
    public AltarStatus status = AltarStatus.Inactive;
    public bool hasGrantedInitialLoot = false;
    public int remainingCharges;
    public int cooldownTicksRemaining;
}

public class AltarManager : MonoBehaviour
{
    public static AltarManager Instance { get; private set; }

    [Header("Altar-Prefab (Optional - hat automatischen Fallback!)")]
    [Tooltip("Dein eigenes Altar-Prefab. Wenn leer, wird automatisch ein schwebender Kristall-Altar erzeugt.")]
    public AltarInteractable altarPrefab;

    [Header("Dynamische Spielregeln (Live umschaltbar)")]
    public AltarUsageMode usageMode = AltarUsageMode.Combo_OneTimeLoot_CooldownTeleport;

    [Tooltip("Albtraum-Reduktion beim erstmaligen Erwecken des Altars.")]
    [Range(0f, 40f)] public float nightmareHealOnAwaken = 12f;

    [Tooltip("Gibt beim Erwecken 1 neuen Würfel direkt ins Inventar (falls Platz vorhanden).")]
    public bool grantCubeOnAwaken = true;

    [Tooltip("Anzahl Ladungen, falls Modus = FixedCharges.")]
    [Range(1, 5)] public int maxChargesPerAltar = 2;

    [Tooltip("Cooldown in Ticks nach einer Nutzung (bei TickCooldown oder Combo).")]
    [Range(0, 12)] public int cooldownTicks = 4;

    [Header("Boss-Korrumpierung")]
    [Tooltip("Tick-Kosten, um einen korrumpierten Altar im Raum wieder zu reinigen.")]
    [Range(0, 3)] public int cleanseTickCost = 1;

    private AltarBiomeBonusHandler biomeBonusHandler;
    private readonly Dictionary<Vector2Int, AltarStateData> altarsByCoord = new Dictionary<Vector2Int, AltarStateData>();
    private static Sprite fallbackAltarSprite;

    public IReadOnlyDictionary<Vector2Int, AltarStateData> Altars => altarsByCoord;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }

        if (GetComponent<AltarTeleportController>() == null)
        {
            gameObject.AddComponent<AltarTeleportController>();
        }

        biomeBonusHandler = GetComponent<AltarBiomeBonusHandler>();
        if (biomeBonusHandler == null)
        {
            biomeBonusHandler = gameObject.AddComponent<AltarBiomeBonusHandler>();
        }
    }

    public void ClearAllAltars()
    {
        altarsByCoord.Clear();
    }

    public void RegisterAltar(Vector2Int coord, ZoneType biome)
    {
        altarsByCoord[coord] = new AltarStateData
        {
            coordinate = coord,
            biome = biome,
            status = AltarStatus.Inactive,
            hasGrantedInitialLoot = false,
            remainingCharges = maxChargesPerAltar,
            cooldownTicksRemaining = 0
        };
    }

    public AltarStateData GetAltarState(Vector2Int coord)
    {
        altarsByCoord.TryGetValue(coord, out AltarStateData state);
        return state;
    }

    public bool IsAltarCorrupted(Vector2Int coord)
    {
        return altarsByCoord.TryGetValue(coord, out AltarStateData state) && state.status == AltarStatus.Corrupted;
    }

    /// <summary>
    /// Kann gezielt von bestimmten Boss-Angriffen aufgerufen werden, um einen Altar zu korrumpieren.
    /// Gibt true zurück, wenn auf diesem Feld tatsächlich ein Altar korrumpiert wurde.
    /// </summary>
    public bool TryCorruptAltar(Vector2Int coord)
    {
        if (!altarsByCoord.TryGetValue(coord, out AltarStateData state)) return false;
        if (state.status == AltarStatus.Corrupted) return false;

        state.status = AltarStatus.Corrupted;
        state.cooldownTicksRemaining = 0;

        if (GridManager.Instance != null)
        {
            GridManager.Instance.SetCellAttribute(coord, RoomAttributeType.Corrupted);
        }

        RefreshActiveRoomVisual(coord);
        Debug.Log($"[AltarManager] Altar bei {coord} wurde vom Boss korrumpiert!");
        return true;
    }

    /// <summary>
    /// Praktischer Helfer für Flächen-Angriffe von Bossen (korrumpiert alle Altäre im Radius).
    /// </summary>
    public int CorruptAltarsInRadius(Vector2Int center, int radius)
    {
        int corruptedCount = 0;
        foreach (var kvp in altarsByCoord)
        {
            Vector2Int coord = kvp.Key;
            int dist = Mathf.Max(Mathf.Abs(coord.x - center.x), Mathf.Abs(coord.y - center.y));
            if (dist <= radius && TryCorruptAltar(coord))
            {
                corruptedCount++;
            }
        }
        return corruptedCount;
    }

    public void RefreshRoomAltar(Room room, Vector2Int coord)
    {
        if (room == null) return;

        room.ClearSpawnedAltar();

        if (!altarsByCoord.TryGetValue(coord, out AltarStateData state))
        {
            RoomAttributeType attr = GridManager.Instance != null
                ? GridManager.Instance.GetCellAttribute(coord)
                : RoomAttributeType.None;

            if (attr != RoomAttributeType.Altar && attr != RoomAttributeType.Corrupted) return;

            ZoneType cellBiome = (GridManager.Instance != null && GridManager.Instance.grid != null &&
                                  coord.x >= 0 && coord.x < GridManager.Instance.width &&
                                  coord.y >= 0 && coord.y < GridManager.Instance.height)
                ? GridManager.Instance.grid[coord.x, coord.y].zoneType
                : ZoneType.Forest;

            RegisterAltar(coord, cellBiome);
            state = altarsByCoord[coord];
            if (attr == RoomAttributeType.Corrupted)
            {
                state.status = AltarStatus.Corrupted;
            }
        }

        Vector3 spawnPos = room.GetAltarWorldPosition();
        spawnPos.z = -0.5f;

        AltarInteractable altarObj = CreateAltarInstance(spawnPos, room.transform);
        altarObj.Initialize(coord, state);
        room.RegisterSpawnedAltar(altarObj);
    }

    public bool TryInteractWithAltar(Vector2Int coord, out string feedbackMessage)
    {
        feedbackMessage = "";

        if (!altarsByCoord.TryGetValue(coord, out AltarStateData state))
        {
            return false;
        }

        switch (state.status)
        {
            case AltarStatus.Inactive:
                return AwakenAltar(state, out feedbackMessage);

            case AltarStatus.Corrupted:
                return CleanseAltar(state, out feedbackMessage);

            case AltarStatus.OnCooldown:
                feedbackMessage = $"Lädt auf ({state.cooldownTicksRemaining} Ticks)";
                return false;

            case AltarStatus.Exhausted:
                feedbackMessage = "Altar erschöpft";
                return false;

            case AltarStatus.Active:
                if (AltarTeleportController.Instance != null)
                {
                    return AltarTeleportController.Instance.TryBeginTeleportSelection(coord, out feedbackMessage);
                }
                feedbackMessage = "Teleport-Controller fehlt!";
                return false;

            default:
                return false;
        }
    }

    private bool CleanseAltar(AltarStateData state, out string feedbackMessage)
    {
        if (cleanseTickCost > 0 && GameManager.Instance != null)
        {
            GameManager.Instance.AdvanceTicks(cleanseTickCost, $"Altar gereinigt ({state.coordinate})");
        }

        state.status = state.hasGrantedInitialLoot ? AltarStatus.Active : AltarStatus.Inactive;

        if (GridManager.Instance != null)
        {
            GridManager.Instance.SetCellAttribute(state.coordinate, RoomAttributeType.Altar);
        }

        RefreshActiveRoomVisual(state.coordinate);
        feedbackMessage = cleanseTickCost > 0
            ? $"Altar gereinigt! ({cleanseTickCost} Tick)"
            : "Altar gereinigt!";
        return true;
    }

    public void ConsumeTeleportUse(Vector2Int coord)
    {
        if (!altarsByCoord.TryGetValue(coord, out AltarStateData state)) return;

        if (usageMode == AltarUsageMode.FixedCharges)
        {
            state.remainingCharges = Mathf.Max(0, state.remainingCharges - 1);
            if (state.remainingCharges <= 0)
            {
                state.status = AltarStatus.Exhausted;
            }
        }
        else if (cooldownTicks > 0)
        {
            state.cooldownTicksRemaining = cooldownTicks;
            state.status = AltarStatus.OnCooldown;
        }

        RefreshActiveRoomVisual(coord);
    }

    private bool AwakenAltar(AltarStateData state, out string feedbackMessage)
    {
        if (nightmareHealOnAwaken > 0f && GameManager.Instance != null)
        {
            GameManager.Instance.AddNightmare(-nightmareHealOnAwaken, $"Altar erweckt ({state.coordinate})");
        }

        bool cubeGranted = false;
        if (grantCubeOnAwaken && !state.hasGrantedInitialLoot)
        {
            InventoryManager inv = GameManager.Instance != null
                ? GameManager.Instance.inventoryManager
                : FindAnyObjectByType<InventoryManager>();

            if (inv != null && inv.CanAddCube())
            {
                cubeGranted = inv.AddFoundCubeToInventory(isSecretBonus: false);
            }
            state.hasGrantedInitialLoot = true;
        }

        string biomeBonusText = biomeBonusHandler != null
            ? biomeBonusHandler.ApplyAwakenBonus(state.biome, state.coordinate)
            : string.Empty;

        if (usageMode == AltarUsageMode.FixedCharges)
        {
            state.remainingCharges = Mathf.Max(0, state.remainingCharges - 1);
            state.status = state.remainingCharges > 0 ? AltarStatus.Active : AltarStatus.Exhausted;
        }
        else if (usageMode == AltarUsageMode.TickCooldown && cooldownTicks > 0)
        {
            state.cooldownTicksRemaining = cooldownTicks;
            state.status = AltarStatus.OnCooldown;
        }
        else
        {
            state.status = AltarStatus.Active;
        }

        feedbackMessage = cubeGranted
            ? $"Erweckt! -{nightmareHealOnAwaken:0}% Albtraum & +1 Würfel"
            : $"Erweckt! -{nightmareHealOnAwaken:0}% Albtraum";

        if (!string.IsNullOrEmpty(biomeBonusText))
        {
            feedbackMessage += $" | {biomeBonusText}";
        }

        RefreshActiveRoomVisual(state.coordinate);
        return true;
    }

    public void OnTicksAdvanced(int ticks)
    {
        if (ticks <= 0 || altarsByCoord.Count == 0) return;

        foreach (var kvp in altarsByCoord)
        {
            AltarStateData state = kvp.Value;
            if (state.status == AltarStatus.OnCooldown)
            {
                state.cooldownTicksRemaining -= ticks;
                if (state.cooldownTicksRemaining <= 0)
                {
                    state.cooldownTicksRemaining = 0;
                    state.status = AltarStatus.Active;
                    RefreshActiveRoomVisual(state.coordinate);
                }
            }
        }
    }

    public void OnRoomDestroyedByBoss(Vector2Int coord)
    {
        altarsByCoord.Remove(coord);
    }

    private void RefreshActiveRoomVisual(Vector2Int coord)
    {
        if (MacroGridManager.Instance != null &&
            MacroGridManager.Instance.Grid.TryGetValue(coord, out Room room) &&
            room != null && room.ActiveAltar != null &&
            altarsByCoord.TryGetValue(coord, out AltarStateData state))
        {
            room.ActiveAltar.SyncVisualState(state);
        }
    }

    private AltarInteractable CreateAltarInstance(Vector3 worldPos, Transform parentRoom)
    {
        if (altarPrefab != null)
        {
            return Instantiate(altarPrefab, worldPos, Quaternion.identity, parentRoom);
        }

        GameObject obj = new GameObject("Altar_Auto");
        obj.transform.SetParent(parentRoom, false);
        obj.transform.position = worldPos;
        obj.transform.localScale = new Vector3(1.6f, 1.6f, 1f);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = GetFallbackAltarSprite();
        sr.sortingOrder = 14;

        CircleCollider2D col = obj.AddComponent<CircleCollider2D>();
        col.radius = 1.25f;
        col.isTrigger = true;

        return obj.AddComponent<AltarInteractable>();
    }

    private static Sprite GetFallbackAltarSprite()
    {
        if (fallbackAltarSprite != null) return fallbackAltarSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBase = (y >= 2 && y <= 9 && x >= 6 && x <= 25);
                bool isPillar = (y >= 10 && y <= 14 && x >= 10 && x <= 21);

                int dx = Mathf.Abs(x - 16);
                int dy = Mathf.Abs(y - 23);
                bool isCrystal = (dx + dy <= 6);

                if (isCrystal)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                else if (isBase || isPillar)
                    tex.SetPixel(x, y, new Color(0.75f, 0.78f, 0.85f, 1f));
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }
        tex.Apply();

        fallbackAltarSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
        return fallbackAltarSprite;
    }
}
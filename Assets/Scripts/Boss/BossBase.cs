using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BossHazardType
{
    DestroyRoom,    // Raum komplett zerstören und vom Grid entfernen
    HazardSpikes,   // Stacheln auf dem Kachel-Boden aktivieren
    HazardPoison,   // Giftzone: Slime nimmt Schaden über Zeit
    LockDoors,      // Verbindungen für eine bestimmte Rundenzahl blockieren
    Custom          // Eigener boss-spezifischer Callback
}

[System.Serializable]
public class BossTelegraphZone
{
    public List<Vector2Int> targetCoordinates = new List<Vector2Int>();
    public int turnsRemaining = 1;
    public BossHazardType hazardType;

    public BossTelegraphZone(List<Vector2Int> coords, BossHazardType type, int turns)
    {
        targetCoordinates = new List<Vector2Int>(coords);
        hazardType = type;
        turnsRemaining = turns;
    }
}

public abstract class BossBase : MonoBehaviour
{
    [Header("Boss Einstellungen")]
    [SerializeField] protected string bossName = "Unbekannter Boss";

    [Header("Aktive Telegrafie-Zonen")]
    [SerializeField] protected List<BossTelegraphZone> activeZones = new List<BossTelegraphZone>();

    [Header("Visuelle Farben für Warnung")]
    [SerializeField] protected Color warningColor = new Color(1f, 0.75f, 0.1f, 0.8f); // Orange/Gelb
    [SerializeField] protected Color strikeColor = new Color(0.9f, 0.15f, 0.1f, 0.9f);   // Rot für Einschlag

    public string BossName => bossName;
    public IReadOnlyList<BossTelegraphZone> ActiveZones => activeZones;

    /// <summary>
    /// Wird vom EventManager in der Gegnerphase aufgerufen.
    /// Jeder Boss steuert hier seine Reihenfolge aus Einschlag, Bewegung und Vorwarnung.
    /// </summary>
    public abstract IEnumerator ExecuteTurn();

    /// <summary>
    /// Kündigt einen Angriff auf bestimmten Koordinaten an und färbt die Kacheln ein.
    /// </summary>
    public virtual void Telegraph(List<Vector2Int> coords, BossHazardType type, int turns = 1)
    {
        if (coords == null || coords.Count == 0) return;

        BossTelegraphZone zone = new BossTelegraphZone(coords, type, turns);
        activeZones.Add(zone);

        HighlightCoordinates(coords, warningColor);
    }

    /// <summary>
    /// Zählt Timer herunter, lässt abgelaufene Zonen einschlagen und bereinigt die Liste.
    /// </summary>
    public virtual IEnumerator ResolveTelegraphsRoutine()
    {
        List<BossTelegraphZone> expiredZones = new List<BossTelegraphZone>();

        for (int i = 0; i < activeZones.Count; i++)
        {
            BossTelegraphZone zone = activeZones[i];
            zone.turnsRemaining--;

            if (zone.turnsRemaining <= 0)
            {
                expiredZones.Add(zone);

                // Kurzes rotes Aufblitzen vor dem Einschlag
                HighlightCoordinates(zone.targetCoordinates, strikeColor);
            }
        }

        if (expiredZones.Count > 0)
        {
            yield return new WaitForSeconds(0.4f);

            foreach (BossTelegraphZone zone in expiredZones)
            {
                foreach (Vector2Int coord in zone.targetCoordinates)
                {
                    ApplyHazardToCoord(coord, zone.hazardType);
                }

                ResetCoordinatesHighlight(zone.targetCoordinates);
                activeZones.Remove(zone);
            }
        }

        yield return null;
    }

    /// <summary>
    /// Wendet den gewählten Effekt auf eine Kachel an.
    /// </summary>
    protected virtual void ApplyHazardToCoord(Vector2Int coord, BossHazardType type)
    {
        switch (type)
        {
            case BossHazardType.DestroyRoom:
                if (MacroGridManager.Instance != null)
                {
                    MacroGridManager.Instance.RemoveRoomAt(coord);
                }
                break;

            case BossHazardType.HazardSpikes:
                Debug.Log($"[BossBase] Stacheln auf {coord} aktiviert.");
                break;

            case BossHazardType.HazardPoison:
                Debug.Log($"[BossBase] Giftgas auf {coord} freigesetzt.");
                break;

            case BossHazardType.LockDoors:
                Debug.Log($"[BossBase] Türen auf {coord} verriegelt.");
                break;

            case BossHazardType.Custom:
                ExecuteCustomHazard(coord);
                break;
        }
    }

    /// <summary>
    /// Virtuelle Schnittstelle für Effekte, die nur ein ganz bestimmter Boss beherrscht.
    /// </summary>
    protected virtual void ExecuteCustomHazard(Vector2Int coord) { }

    /// <summary>
    /// Hook für visuelle Kachel-Hervorhebung auf dem Makro-Grid.
    /// </summary>
    protected virtual void HighlightCoordinates(List<Vector2Int> coords, Color color)
    {
        if (GridManager.Instance != null)
        {
            foreach (Vector2Int pos in coords)
            {
                GridManager.Instance.HighlightCell(pos, color);
            }
        }
    }

    /// <summary>
    /// Setzt die Kachel-Optik nach dem Einschlag wieder zurück.
    /// </summary>
    protected virtual void ResetCoordinatesHighlight(List<Vector2Int> coords)
    {
        if (GridManager.Instance != null)
        {
            foreach (Vector2Int pos in coords)
            {
                GridManager.Instance.ResetCellHighlight(pos);
            }
        }
    }

    public virtual void OnPhaseTransition(int newPhase) { }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TimelineEventType
{
    AmbientNightmareRipple, // Kleines Umwelt-Event zwischen den Boss-Zügen
    BossMajorPulse          // Großer Boss-Zug (Einschlag + Bewegung + neue Telegrafie)
}

public class EventManager : MonoBehaviour
{
    public GridManager gridManager;
    public MacroSlimeMarker slime;
    public GameManager gameManager;
    public PlacementManager placementManager;
    public InventoryManager inventoryManager;
    public BossBase boss;

    private bool isPulseRunning = false;
    private readonly Queue<TimelineEventType> pendingEvents = new Queue<TimelineEventType>();

    /// <summary>
    /// Reiht ein spezifisches Timeline-Event in die Hintergrund-Warteschlange ein.
    /// </summary>
    public void EnqueueTimelineEvent(TimelineEventType eventType)
    {
        pendingEvents.Enqueue(eventType);
        if (!isPulseRunning)
        {
            StartCoroutine(ProcessEventQueueRoutine());
        }
    }

    // Abwärtskompatible Aufrufe
    public void EnqueueTimelinePulse()
    {
        EnqueueTimelineEvent(TimelineEventType.BossMajorPulse);
    }

    public void TriggerEndTurn(bool forcedByZeroFocus = false)
    {
        EnqueueTimelineEvent(TimelineEventType.BossMajorPulse);
    }

    private IEnumerator ProcessEventQueueRoutine()
    {
        isPulseRunning = true;

        while (pendingEvents.Count > 0)
        {
            TimelineEventType nextEvent = pendingEvents.Dequeue();
            if (nextEvent == TimelineEventType.AmbientNightmareRipple)
            {
                yield return StartCoroutine(ExecuteAmbientRippleRoutine());
            }
            else
            {
                yield return StartCoroutine(ExecuteBossPulseRoutine());
            }
        }

        isPulseRunning = false;
    }

    private IEnumerator ExecuteAmbientRippleRoutine()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gridManager == null) gridManager = GridManager.Instance;

        Debug.Log("--- [TIMELINE-EVENT] UMWELT-BEBEN (ALBTRAUM-SCHAUER) ---");

        // Dezentes Raum-Rumpeln im Platformer
        if (gameManager != null)
        {
            gameManager.TriggerMicroCameraRumble(intensity: 0.14f, duration: 0.22f);
            float rippleAmount = Mathf.Max(1f, gameManager.NightmarePerTurn * 0.5f);
            gameManager.AddNightmare(rippleAmount, "Albtraum-Schauer (Timeline)");
        }

        CheckHazardousStandingTile();

        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.RefreshActiveRoomDoors();
        }

        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator ExecuteBossPulseRoutine()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gridManager == null) gridManager = GridManager.Instance;

        Debug.Log("--- [TIMELINE-EVENT] ⚡ GROSSER BOSS-PULS IM HINTERGRUND ---");

        // Kräftigeres Raum-Beben im Platformer, wenn der Boss zieht
        if (gameManager != null)
        {
            gameManager.TriggerMicroCameraRumble(intensity: 0.32f, duration: 0.38f);
        }

        // 1. Boss-Aktion im Hintergrund ausführen (Einschläge + Bewegung + neue Warnung)
        if (boss != null)
        {
            yield return StartCoroutine(boss.ExecuteTurn());
        }

        // 2. Regulärer Albtraum-Anstieg beim Boss-Zug
        if (gameManager != null && gameManager.NightmarePerTurn > 0f)
        {
            gameManager.AddNightmare(gameManager.NightmarePerTurn, "Boss-Puls auf Timeline");
        }

        CheckHazardousStandingTile();

        if (gameManager != null && gameManager.CurrentNightmare >= gameManager.MaxNightmare)
        {
            pendingEvents.Clear();
            yield break;
        }

        // 3. Platzierungszähler & Raumtüren (inkl. roter Warn-Telegrafie!) aktualisieren
        if (placementManager != null)
        {
            placementManager.ResetTurnPlacements();
        }

        if (inventoryManager != null)
        {
            inventoryManager.CheckAndRefillIfEmpty();
        }

        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.RefreshActiveRoomDoors();
        }
    }

    private void CheckHazardousStandingTile()
    {
        if (gameManager == null || MacroGridManager.Instance == null || gridManager == null || gridManager.grid == null)
            return;

        Vector2Int slimeCoord = MacroGridManager.Instance.CurrentCoordinate;
        if (slimeCoord.x >= 0 && slimeCoord.x < gridManager.width &&
            slimeCoord.y >= 0 && slimeCoord.y < gridManager.height)
        {
            GridCell cell = gridManager.grid[slimeCoord.x, slimeCoord.y];
            if (cell != null)
            {
                if (cell.zoneType == ZoneType.PurpleNightmare)
                {
                    gameManager.AddNightmare(gameManager.NightmareOnPurpleTile, "Aufenthalt im lila Albtraum-Rand");
                }
                else if (cell.zoneType == ZoneType.BlackVoid)
                {
                    gameManager.AddNightmare(gameManager.NightmareOnBlackVoidTile, "Aufenthalt im schwarzen Rand");
                }
            }
        }
    }
}
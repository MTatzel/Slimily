using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TimelineEventType
{
    MinionSlabTriggered,
    AmbientNightmareRipple,
    BossMajorPulse
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

    public void EnqueueTimelineEvent(TimelineEventType eventType)
    {
        pendingEvents.Enqueue(eventType);
        if (!isPulseRunning)
        {
            StartCoroutine(ProcessEventQueueRoutine());
        }
    }

    public void EnqueueTimelinePulse() => EnqueueTimelineEvent(TimelineEventType.BossMajorPulse);
    public void TriggerEndTurn(bool forcedByZeroFocus = false) => EnqueueTimelineEvent(TimelineEventType.BossMajorPulse);

    private IEnumerator ProcessEventQueueRoutine()
    {
        isPulseRunning = true;

        while (pendingEvents.Count > 0)
        {
            TimelineEventType nextEvent = pendingEvents.Dequeue();

            switch (nextEvent)
            {
                case TimelineEventType.MinionSlabTriggered:
                    yield return StartCoroutine(ExecuteMinionThreatRoutine());
                    break;

                case TimelineEventType.AmbientNightmareRipple:
                    yield return StartCoroutine(ExecuteAmbientRippleRoutine());
                    break;

                case TimelineEventType.BossMajorPulse:
                    yield return StartCoroutine(ExecuteBossPulseRoutine());
                    break;
            }
        }

        isPulseRunning = false;
    }

    private IEnumerator ExecuteMinionThreatRoutine()
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        if (gameManager != null)
        {
            gameManager.TriggerMicroCameraRumble(intensity: 0.22f, duration: 0.25f);
            gameManager.AddNightmare(4f, "Schergen-Symbol bei 0");
        }

        CheckHazardousStandingTile();
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator ExecuteAmbientRippleRoutine()
    {
        if (gameManager == null) gameManager = GameManager.Instance;

        if (gameManager != null)
        {
            gameManager.TriggerMicroCameraRumble(intensity: 0.16f, duration: 0.22f);
            float rippleAmount = Mathf.Max(2f, gameManager.NightmarePerTurn * 0.6f);
            gameManager.AddNightmare(rippleAmount, "Umwelt-Symbol bei 0");
        }

        CheckHazardousStandingTile();
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator ExecuteBossPulseRoutine()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (gridManager == null) gridManager = GridManager.Instance;

        if (gameManager != null)
        {
            gameManager.TriggerMicroCameraRumble(intensity: 0.35f, duration: 0.38f);
        }

        if (boss != null)
        {
            yield return StartCoroutine(boss.ExecuteTurn());
        }

        if (BiomeResonanceManager.Instance != null)
        {
            BiomeResonanceManager.Instance.OnBossPulseTriggered();
        }

        if (gameManager != null && gameManager.NightmarePerTurn > 0f)
        {
            gameManager.AddNightmare(gameManager.NightmarePerTurn, "Boss-Symbol bei 0");
        }

        CheckHazardousStandingTile();

        if (gameManager != null && gameManager.CurrentNightmare >= gameManager.MaxNightmare)
        {
            pendingEvents.Clear();
            yield break;
        }

        if (placementManager != null) placementManager.ResetTurnPlacements();
        if (inventoryManager != null) inventoryManager.CheckAndRefillIfEmpty();
        if (MacroGridManager.Instance != null) MacroGridManager.Instance.RefreshActiveRoomDoors();
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
                    gameManager.AddNightmare(gameManager.NightmareOnPurpleTile, "Aufenthalt im lila Albtraum-Rand");
                else if (cell.zoneType == ZoneType.BlackVoid)
                    gameManager.AddNightmare(gameManager.NightmareOnBlackVoidTile, "Aufenthalt im schwarzen Rand");
            }
        }
    }
}
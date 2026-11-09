using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class ThreatOrbEntry
{
    public TimelineEventType eventType = TimelineEventType.BossMajorPulse;
    public string iconSymbol = "💀";
    public string title = "Boss-Aktion";
    public string description = "";
    public Color orbColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public int countdown = 4;
    public int maxInterval = 4;
    public Vector2Int targetGridCoord;
    public bool highlightTargetOnGrid = false;

    [HideInInspector] public float pulseAnimTimer;
}

public class ThreatTrackerManager : MonoBehaviour
{
    public static ThreatTrackerManager Instance { get; private set; }

    [Header("Dynamische Boss-Anbindung")]
    public bool trackActiveBoss = true;
    public Color defaultBossOrbColor = new Color(0.92f, 0.18f, 0.18f, 1f);

    [Header("Optionale Test-Symbole (Inspector)")]
    public List<ThreatOrbEntry> manualInspectorThreats = new List<ThreatOrbEntry>();

    [Header("Live-Kamera-Ausschnitt (PiP) & R2-Inspektion")]
    [Tooltip("Zeigt bei Countdown = 0 automatisch das kleine Live-Grid-Fenster oben rechts an.")]
    public bool showPiPWindowOnZero = true;
    [Tooltip("Wie lange das Live-Fenster voll sichtbar bleibt (in Sekunden), bevor es langsam verblasst.")]
    public float pipHoldDuration = 2.5f;
    [Tooltip("Wie lange das sanfte Ausblenden (Fade-Out) dauert (in Sekunden).")]
    public float pipFadeDuration = 1.5f;
    public bool allowR2Inspection = true;

    private readonly List<ThreatOrbEntry> activeOrbs = new List<ThreatOrbEntry>();
    private int selectedOrbIndex = -1;

    public float PipActiveTimer { get; private set; } = 0f;
    public string PipEventTitle { get; private set; } = "";
    public Color PipFrameColor { get; private set; } = Color.red;
    public Vector2Int PipFocusCoord { get; private set; } = new Vector2Int(25, 25);

    /// <summary>
    /// Berechnet den aktuellen Alpha-Wert (1.0 während der Haltezeit, danach sanftes Ausblenden auf 0.0).
    /// </summary>
    public float CurrentPipAlpha
    {
        get
        {
            if (SelectedOrb != null) return 1f;
            if (PipActiveTimer <= 0f) return 0f;
            if (pipFadeDuration <= 0.01f) return 1f;
            return Mathf.Clamp01(PipActiveTimer / pipFadeDuration);
        }
    }

    public IReadOnlyList<ThreatOrbEntry> ActiveOrbs => activeOrbs;
    public int SelectedOrbIndex => selectedOrbIndex;
    public ThreatOrbEntry SelectedOrb => (selectedOrbIndex >= 0 && selectedOrbIndex < activeOrbs.Count) ? activeOrbs[selectedOrbIndex] : null;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    private void Update()
    {
        if (allowR2Inspection)
        {
            HandleR2InspectionInput();
        }
        else
        {
            selectedOrbIndex = -1;
        }

        for (int i = 0; i < activeOrbs.Count; i++)
        {
            if (activeOrbs[i].pulseAnimTimer > 0f)
            {
                activeOrbs[i].pulseAnimTimer = Mathf.MoveTowards(activeOrbs[i].pulseAnimTimer, 0f, Time.deltaTime * 2.5f);
            }
        }

        if (PipActiveTimer > 0f)
        {
            PipActiveTimer = Mathf.MoveTowards(PipActiveTimer, 0f, Time.deltaTime);
        }

        SyncBossTargetCoordinate();
    }

    private void HandleR2InspectionInput()
    {
        var gamepad = Gamepad.current;
        var keyboard = Keyboard.current;

        bool r2Pressed = (gamepad != null && gamepad.rightTrigger.wasPressedThisFrame) ||
                         (keyboard != null && keyboard.tabKey.wasPressedThisFrame);

        if (r2Pressed && activeOrbs.Count > 0)
        {
            selectedOrbIndex++;
            if (selectedOrbIndex >= activeOrbs.Count)
            {
                selectedOrbIndex = -1;
            }
        }
    }

    public void InitializeOrbs(int bossInterval = 4)
    {
        ClearManualHighlights();
        activeOrbs.Clear();
        selectedOrbIndex = -1;
        PipActiveTimer = 0f;

        int safeBossInterval = Mathf.Max(1, bossInterval);

        bool hasBossInLevel = GameManager.Instance != null && GameManager.Instance.ActiveBoss != null;
        if (trackActiveBoss && hasBossInLevel)
        {
            BossBase boss = GameManager.Instance.ActiveBoss;
            activeOrbs.Add(new ThreatOrbEntry
            {
                eventType = TimelineEventType.BossMajorPulse,
                iconSymbol = "💀",
                title = boss.BossName,
                description = "Führt bei 0 den nächsten Boss-Zug auf dem Grid aus.",
                orbColor = defaultBossOrbColor,
                countdown = safeBossInterval,
                maxInterval = safeBossInterval,
                targetGridCoord = GetCurrentBossGridCoord(),
                highlightTargetOnGrid = false
            });
        }

        if (manualInspectorThreats != null)
        {
            foreach (ThreatOrbEntry manual in manualInspectorThreats)
            {
                if (manual == null) continue;
                ThreatOrbEntry copy = new ThreatOrbEntry
                {
                    eventType = manual.eventType,
                    iconSymbol = string.IsNullOrEmpty(manual.iconSymbol) ? "!" : manual.iconSymbol,
                    title = manual.title,
                    description = manual.description,
                    orbColor = manual.orbColor,
                    countdown = Mathf.Max(1, manual.countdown),
                    maxInterval = Mathf.Max(1, manual.maxInterval),
                    targetGridCoord = manual.targetGridCoord,
                    highlightTargetOnGrid = manual.highlightTargetOnGrid
                };
                activeOrbs.Add(copy);

                if (copy.highlightTargetOnGrid && GridManager.Instance != null)
                {
                    GridManager.Instance.HighlightCell(copy.targetGridCoord, copy.orbColor);
                }
            }
        }
    }

    [ContextMenu("Inspector-Symbole jetzt neu laden")]
    public void ReloadOrbsFromInspector()
    {
        int interval = GameManager.Instance != null ? GameManager.Instance.MaxFocusPerTurn : 4;
        InitializeOrbs(interval);
    }

    public void AdvanceAllOrbs(int ticks, EventManager eventManager)
    {
        if (ticks <= 0) return;

        if (activeOrbs.Count == 0 && trackActiveBoss && GameManager.Instance != null && GameManager.Instance.ActiveBoss != null)
        {
            InitializeOrbs(GameManager.Instance.MaxFocusPerTurn);
        }

        for (int step = 0; step < ticks; step++)
        {
            for (int i = 0; i < activeOrbs.Count; i++)
            {
                ThreatOrbEntry orb = activeOrbs[i];
                orb.countdown--;
                orb.pulseAnimTimer = 0.5f;

                if (orb.countdown <= 0)
                {
                    TriggerOrbZeroEffect(orb, eventManager);
                    orb.countdown = Mathf.Max(1, orb.maxInterval);
                    orb.pulseAnimTimer = 0.9f;
                }
            }
        }
    }

    private void TriggerOrbZeroEffect(ThreatOrbEntry orb, EventManager eventManager)
    {
        if (showPiPWindowOnZero)
        {
            PipFocusCoord = orb.eventType == TimelineEventType.BossMajorPulse
                ? GetCurrentBossGridCoord()
                : orb.targetGridCoord;
            PipEventTitle = $"⚡ {orb.title}";
            PipFrameColor = orb.orbColor;
            PipActiveTimer = pipHoldDuration + pipFadeDuration;
        }

        if (eventManager != null)
        {
            eventManager.EnqueueTimelineEvent(orb.eventType);
        }
    }

    public void DelayAllOrbs(int extraTicks)
    {
        if (extraTicks <= 0) return;
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            activeOrbs[i].countdown += extraTicks;
            activeOrbs[i].pulseAnimTimer = 0.6f;
        }
    }

    public int GetBossOrbCountdown()
    {
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            if (activeOrbs[i].eventType == TimelineEventType.BossMajorPulse)
            {
                return activeOrbs[i].countdown;
            }
        }
        return GameManager.Instance != null ? GameManager.Instance.MaxFocusPerTurn : 4;
    }

    private void SyncBossTargetCoordinate()
    {
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            if (activeOrbs[i].eventType == TimelineEventType.BossMajorPulse)
            {
                activeOrbs[i].targetGridCoord = GetCurrentBossGridCoord();
            }
        }
    }

    private Vector2Int GetCurrentBossGridCoord()
    {
        if (GameManager.Instance != null && GameManager.Instance.ActiveBoss != null)
        {
            BossBase boss = GameManager.Instance.ActiveBoss;
            if (boss.ActiveZones != null && boss.ActiveZones.Count > 0 && boss.ActiveZones[0].targetCoordinates.Count > 0)
            {
                return boss.ActiveZones[0].targetCoordinates[0];
            }

            float offset = (GridManager.Instance != null && GridManager.Instance.offset > 0.01f) ? GridManager.Instance.offset : 1.1f;
            int bx = Mathf.RoundToInt(boss.transform.localPosition.x / offset);
            int by = Mathf.RoundToInt(boss.transform.localPosition.y / offset);
            return new Vector2Int(bx, by);
        }

        return MacroGridManager.Instance != null ? MacroGridManager.Instance.CurrentCoordinate : new Vector2Int(25, 25);
    }

    private void ClearManualHighlights()
    {
        if (GridManager.Instance == null) return;
        foreach (var orb in activeOrbs)
        {
            if (orb.highlightTargetOnGrid)
            {
                GridManager.Instance.ResetCellHighlight(orb.targetGridCoord);
            }
        }
    }
}
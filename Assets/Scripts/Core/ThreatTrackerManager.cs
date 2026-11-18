using System;
using System.Collections;
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

    [Header("Live-Kamera-Ausschnitt (PiP) & Timing")]
    [Tooltip("Zeigt bei Countdown = 0 automatisch das kleine Live-Grid-Fenster oben rechts an.")]
    public bool showPiPWindowOnZero = true;
    [Tooltip("1. Phase: Dauer des langsamen Einblendens (Fade-In) in Sekunden.")]
    public float pipFadeInDuration = 0.8f;
    [Tooltip("2. Phase: Wartezeit NACH dem Einblenden, BEVOR der Boss seine Aktion startet.")]
    public float pipPreActionDelay = 1.5f;
    [Tooltip("4. Phase: Wie lange das Fenster NACH der Boss-Aktion noch voll sichtbar bleibt.")]
    public float pipHoldDuration = 2.0f;
    [Tooltip("5. Phase: Wie lange das langsame Ausblenden (Fade-Out) am Ende dauert.")]
    public float pipFadeDuration = 1.6f;
    public bool allowR2Inspection = true;

    private readonly List<ThreatOrbEntry> activeOrbs = new List<ThreatOrbEntry>();
    private readonly Queue<ThreatOrbEntry> pendingZeroOrbs = new Queue<ThreatOrbEntry>();
    private int selectedOrbIndex = -1;

    private bool isPipSequenceRunning = false;
    private float currentSequenceAlpha = 0f;
    private Coroutine activeSequenceCoroutine;

    // Bleibt > 0 solange die Sequenz läuft, damit ThreatOrbsAndPiPHUD die Kamera aktiviert
    public float PipActiveTimer => isPipSequenceRunning ? 1f : 0f;
    public string PipEventTitle { get; private set; } = "";
    public Color PipFrameColor { get; private set; } = Color.red;
    public Vector2Int PipFocusCoord { get; private set; } = new Vector2Int(25, 25);

    public float CurrentPipAlpha
    {
        get
        {
            if (SelectedOrb != null) return 1f;
            return isPipSequenceRunning ? currentSequenceAlpha : 0f;
        }
    }

    public IReadOnlyList<ThreatOrbEntry> ActiveOrbs => activeOrbs;
    public int SelectedOrbIndex => selectedOrbIndex;
    public ThreatOrbEntry SelectedOrb => (selectedOrbIndex >= 0 && selectedOrbIndex < activeOrbs.Count) ? activeOrbs[selectedOrbIndex] : null;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Debug.LogWarning($"[ThreatTrackerManager] Doppelte Komponente auf '{gameObject.name}' gefunden und entfernt. Aktiv ist die Instanz auf '{Instance.gameObject.name}'.");
            Destroy(this);
        }
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
        if (activeSequenceCoroutine != null)
        {
            StopCoroutine(activeSequenceCoroutine);
            activeSequenceCoroutine = null;
        }

        ClearManualHighlights();
        activeOrbs.Clear();
        pendingZeroOrbs.Clear();
        selectedOrbIndex = -1;
        isPipSequenceRunning = false;
        currentSequenceAlpha = 0f;

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
                    orb.countdown = Mathf.Max(1, orb.maxInterval);
                    orb.pulseAnimTimer = 0.9f;
                    pendingZeroOrbs.Enqueue(orb);
                }
            }
        }

        if (pendingZeroOrbs.Count > 0 && activeSequenceCoroutine == null)
        {
            activeSequenceCoroutine = StartCoroutine(RunPipAndEventSequenceRoutine(eventManager));
        }
    }

    /// <summary>
    /// Führt die 5 Phasen strikt nacheinander aus:
    /// 1. Einblenden -> 2. Warten -> 3. Boss-Aktion -> 4. Halten -> 5. Ausblenden.
    /// </summary>
    private IEnumerator RunPipAndEventSequenceRoutine(EventManager eventManager)
    {
        if (eventManager == null) eventManager = FindAnyObjectByType<EventManager>();

        if (!showPiPWindowOnZero)
        {
            while (pendingZeroOrbs.Count > 0)
            {
                ThreatOrbEntry orb = pendingZeroOrbs.Dequeue();
                if (eventManager != null)
                {
                    yield return StartCoroutine(eventManager.ExecuteSingleEventRoutine(orb.eventType));
                }
            }
            activeSequenceCoroutine = null;
            yield break;
        }

        isPipSequenceRunning = true;
        currentSequenceAlpha = 0f;

        ThreatOrbEntry firstOrb = pendingZeroOrbs.Peek();
        UpdatePipMetadataForOrb(firstOrb);

        // PHASE 1: Langsames Einblenden (Fade-In)
        if (pipFadeInDuration > 0.01f)
        {
            float elapsed = 0f;
            while (elapsed < pipFadeInDuration)
            {
                elapsed += Time.deltaTime;
                currentSequenceAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pipFadeInDuration));
                yield return null;
            }
        }
        currentSequenceAlpha = 1f;

        // PHASE 2: Warten BEVOR der Boss seine Aktion macht
        if (pipPreActionDelay > 0.01f)
        {
            yield return new WaitForSeconds(pipPreActionDelay);
        }

        // PHASE 3: Boss-Aktion (bzw. Event) jetzt live im geöffneten Fenster ausführen!
        while (pendingZeroOrbs.Count > 0)
        {
            ThreatOrbEntry currentOrb = pendingZeroOrbs.Dequeue();
            UpdatePipMetadataForOrb(currentOrb);

            if (eventManager != null)
            {
                yield return StartCoroutine(eventManager.ExecuteSingleEventRoutine(currentOrb.eventType));
            }

            // Fokus nach dem Boss-Zug auf die neu markierte Koordinate aktualisieren
            if (currentOrb.eventType == TimelineEventType.BossMajorPulse)
            {
                PipFocusCoord = GetCurrentBossGridCoord();
            }
        }

        // PHASE 4: Nachhaltezeit nach der Aktion
        if (pipHoldDuration > 0.01f)
        {
            yield return new WaitForSeconds(pipHoldDuration);
        }

        // PHASE 5: Langsames Ausblenden (Fade-Out)
        if (pipFadeDuration > 0.01f)
        {
            float elapsed = 0f;
            while (elapsed < pipFadeDuration)
            {
                elapsed += Time.deltaTime;
                currentSequenceAlpha = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(elapsed / pipFadeDuration));
                yield return null;
            }
        }

        currentSequenceAlpha = 0f;
        isPipSequenceRunning = false;
        activeSequenceCoroutine = null;
    }

    private void UpdatePipMetadataForOrb(ThreatOrbEntry orb)
    {
        if (orb == null) return;
        PipFocusCoord = orb.eventType == TimelineEventType.BossMajorPulse
            ? GetCurrentBossGridCoord()
            : orb.targetGridCoord;
        PipEventTitle = $"⚡ {orb.title}";
        PipFrameColor = orb.orbColor;
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
        Vector2Int currentBossTarget = GetCurrentBossGridCoord();

        for (int i = 0; i < activeOrbs.Count; i++)
        {
            if (activeOrbs[i].eventType == TimelineEventType.BossMajorPulse)
            {
                activeOrbs[i].targetGridCoord = currentBossTarget;
            }
        }

        if (isPipSequenceRunning)
        {
            PipFocusCoord = currentBossTarget;
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
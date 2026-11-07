using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class ThreatOrbEntry
{
    public TimelineEventType eventType;
    public string iconSymbol;
    public string title;
    public string description;
    public Color orbColor;
    public int countdown;
    public int maxInterval;
    public Vector2Int targetGridCoord;
    public bool hasSpecificGridTarget;

    // Kurzer Puls-Timer, wenn der Zähler tickt oder bei 0 auslöst
    public float pulseAnimTimer;
}

public class ThreatTrackerManager : MonoBehaviour
{
    public static ThreatTrackerManager Instance { get; private set; }

    [Header("Farb-Codierung (Identisch auf UI-Symbol & Grid!)")]
    public Color bossThreatColor = new Color(0.92f, 0.18f, 0.18f, 1f);    // Karmesinrot (Boss)
    public Color minionThreatColor = new Color(0.95f, 0.65f, 0.12f, 1f);  // Bernstein/Gelb (Minion)
    public Color ambientThreatColor = new Color(0.68f, 0.22f, 0.92f, 1f); // Violett (Umwelt/Nebel)

    private readonly List<ThreatOrbEntry> activeOrbs = new List<ThreatOrbEntry>();

    // R2-Inspektor: -1 = nichts ausgewählt, 0..2 = Index des ausgewählten Symbols oben rechts
    private int selectedOrbIndex = -1;

    // Live-PiP-Kamera-Trigger (wenn ein Zähler 0 erreicht)
    public float PipActiveTimer { get; private set; } = 0f;
    public float PipMaxDuration { get; private set; } = 2.1f;
    public string PipEventTitle { get; private set; } = "";
    public Color PipFrameColor { get; private set; } = Color.red;
    public Vector2Int PipFocusCoord { get; private set; } = new Vector2Int(25, 25);

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
        HandleR2InspectionInput();

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

    /// <summary>
    /// R2 auf dem Controller (oder Tab auf der Tastatur) schaltet durch die Gefahren-Symbole oben rechts!
    /// </summary>
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
                selectedOrbIndex = -1; // Nach dem letzten Symbol wieder schließen
            }
        }
    }

    public void InitializeOrbs(int bossInterval = 4)
    {
        ClearMinionHighlightOnGrid();
        activeOrbs.Clear();
        selectedOrbIndex = -1;
        PipActiveTimer = 0f;

        int safeBossInterval = Mathf.Max(2, bossInterval);

        // 1. BOSS-SYMBOL (Karmesinrot)
        activeOrbs.Add(new ThreatOrbEntry
        {
            eventType = TimelineEventType.BossMajorPulse,
            iconSymbol = "💀",
            title = "BOSS-AKTION",
            description = "Der Boss bewegt sich 1 Feld auf dich zu, lässt rot markierte Felder einschlagen und visiert ein neues Ziel an.",
            orbColor = bossThreatColor,
            countdown = safeBossInterval,
            maxInterval = safeBossInterval,
            targetGridCoord = GetCurrentBossGridCoord(),
            hasSpecificGridTarget = true
        });

        // 2. SCHERGEN-/MINION-SYMBOL (Bernstein-Gelb)
        Vector2Int minionTarget = PickMinionTargetCoordinate();
        activeOrbs.Add(new ThreatOrbEntry
        {
            eventType = TimelineEventType.MinionSlabTriggered,
            iconSymbol = "🕷️",
            title = "SCHERGEN-HINTERHALT",
            description = "Ein Scherge attackiert die gelb markierte Kachel auf dem Grid (+4% Albtraum, +10% falls du dort stehst).",
            orbColor = minionThreatColor,
            countdown = 3,
            maxInterval = 3,
            targetGridCoord = minionTarget,
            hasSpecificGridTarget = true
        });
        HighlightMinionTargetOnGrid(minionTarget, true);

        // 3. UMWELT-/NEBEL-SYMBOL (Violett)
        activeOrbs.Add(new ThreatOrbEntry
        {
            eventType = TimelineEventType.AmbientNightmareRipple,
            iconSymbol = "🌫️",
            title = "ALBTRAUM-FLUT",
            description = "Dichter Schattennebel zieht über das gesamte Raster (+Albtraum-Druck, besonders stark auf lila/schwarzen Randfeldern).",
            orbColor = ambientThreatColor,
            countdown = 5,
            maxInterval = 5,
            targetGridCoord = MacroGridManager.Instance != null ? MacroGridManager.Instance.CurrentCoordinate : new Vector2Int(25, 25),
            hasSpecificGridTarget = false
        });
    }

    /// <summary>
    /// Zählt alle Gefahren-Symbole oben rechts um 'ticks' herunter.
    /// Erreicht ein Symbol 0, blitzt es auf, startet den Live-Kamera-Ausschnitt (PiP) und führt die Aktion aus!
    /// </summary>
    public void AdvanceAllOrbs(int ticks, EventManager eventManager)
    {
        if (ticks <= 0) return;
        if (activeOrbs.Count == 0) InitializeOrbs();

        for (int step = 0; step < ticks; step++)
        {
            for (int i = 0; i < activeOrbs.Count; i++)
            {
                ThreatOrbEntry orb = activeOrbs[i];
                orb.countdown--;
                orb.pulseAnimTimer = 0.6f;

                if (orb.countdown <= 0)
                {
                    TriggerOrbZeroEffect(orb, eventManager);
                    orb.countdown = orb.maxInterval;
                    orb.pulseAnimTimer = 1f;
                }
            }
        }
    }

    private void TriggerOrbZeroEffect(ThreatOrbEntry orb, EventManager eventManager)
    {
        // Fokus-Koordinate für den Live-Kamera-Ausschnitt (PiP) bestimmen
        Vector2Int focusCoord = orb.targetGridCoord;

        if (orb.eventType == TimelineEventType.BossMajorPulse)
        {
            focusCoord = GetCurrentBossGridCoord();
        }
        else if (orb.eventType == TimelineEventType.MinionSlabTriggered)
        {
            focusCoord = orb.targetGridCoord;
            HighlightMinionTargetOnGrid(orb.targetGridCoord, false);

            // Neues gelbes Ziel für den nächsten Minion-Zyklus wählen
            orb.targetGridCoord = PickMinionTargetCoordinate();
            HighlightMinionTargetOnGrid(orb.targetGridCoord, true);
        }
        else if (MacroGridManager.Instance != null)
        {
            focusCoord = MacroGridManager.Instance.CurrentCoordinate;
        }

        // Live-Picture-in-Picture-Fenster oben rechts für 2.1 Sekunden aktivieren!
        PipFocusCoord = focusCoord;
        PipEventTitle = $"⚡ {orb.title} AUSGELÖST!";
        PipFrameColor = orb.orbColor;
        PipActiveTimer = PipMaxDuration;

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
            activeOrbs[i].pulseAnimTimer = 0.8f;
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
        return 4;
    }

    private void SyncBossTargetCoordinate()
    {
        if (activeOrbs.Count == 0) return;

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

    private Vector2Int PickMinionTargetCoordinate()
    {
        Vector2Int baseCoord = MacroGridManager.Instance != null ? MacroGridManager.Instance.CurrentCoordinate : new Vector2Int(25, 25);
        Vector2Int[] offsets = new Vector2Int[]
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(-1, 1)
        };

        Vector2Int chosen = baseCoord + offsets[UnityEngine.Random.Range(0, offsets.Length)];
        if (GridManager.Instance != null)
        {
            chosen.x = Mathf.Clamp(chosen.x, 1, GridManager.Instance.width - 2);
            chosen.y = Mathf.Clamp(chosen.y, 1, GridManager.Instance.height - 2);
        }
        return chosen;
    }

    private void HighlightMinionTargetOnGrid(Vector2Int coord, bool active)
    {
        if (GridManager.Instance == null) return;
        if (active)
            GridManager.Instance.HighlightCell(coord, minionThreatColor);
        else
            GridManager.Instance.ResetCellHighlight(coord);
    }

    private void ClearMinionHighlightOnGrid()
    {
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            if (activeOrbs[i].eventType == TimelineEventType.MinionSlabTriggered)
            {
                HighlightMinionTargetOnGrid(activeOrbs[i].targetGridCoord, false);
            }
        }
    }
}
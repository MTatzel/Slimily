using System;
using UnityEngine;

public class BossTriggerController : MonoBehaviour
{
    public static BossTriggerController Instance { get; private set; }

    [Header("Allgemeine Steuerung")]
    [Tooltip("Schaltet alle automatischen Boss-Trigger global an oder aus.")]
    public bool enableAutomaticTriggers = true;

    [Tooltip("Mindestanzahl Ticks Ruhepause, nachdem ein Boss besiegt wurde oder sich zurückgezogen hat.")]
    [Range(0, 30)] public int cooldownTicksAfterDespawn = 6;

    [Header("1. Albtraum-Trigger")]
    public bool triggerOnNightmare = true;
    [Range(10f, 100f)] public float nightmareThreshold = 40f;
    [Tooltip("Optional: Spezifischer Boss für diesen Trigger (leer = zufälliges Profil aus BossDirector).")]
    public BossProfile nightmareBossOverride;

    [Header("2. Tick-Trigger (Zeitdruck)")]
    public bool triggerOnTotalTicks = true;
    [Range(1, 50)] public int ticksUntilBossSpawn = 12;
    public BossProfile tickBossOverride;

    [Header("3. Erkundungs-Trigger (Besuchte Räume)")]
    public bool triggerOnVisitedRooms = false;
    [Range(2, 30)] public int visitedRoomsThreshold = 6;
    public BossProfile explorationBossOverride;

    [Header("4. Distanz-Trigger (Abstand zum Start-Hub)")]
    public bool triggerOnHubDistance = false;
    [Range(2, 20)] public int hubDistanceThreshold = 5;
    public BossProfile distanceBossOverride;

    [Header("Laufzeit-Status (Read-Only)")]
    [SerializeField] private int ticksSinceLastBoss = 0;
    [SerializeField] private int cooldownTicksRemaining = 0;
    [SerializeField] private float lastKnownNightmare = 0f;
    [SerializeField] private bool hasTriggeredExploration = false;
    [SerializeField] private bool hasTriggeredDistance = false;

    private float checkTimer = 0f;
    private const float CheckInterval = 0.25f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    private void OnEnable()
    {
        BossDirector.OnBossDespawned += HandleBossDespawned;
    }

    private void OnDisable()
    {
        BossDirector.OnBossDespawned -= HandleBossDespawned;
    }

    private void Update()
    {
        if (!enableAutomaticTriggers) return;

        checkTimer += Time.deltaTime;
        if (checkTimer < CheckInterval) return;
        checkTimer = 0f;

        CheckMacroGridTriggers();
    }

    /// <summary>
    /// Setzt alle Zähler beim Start eines neuen Levels/Runs zurück.
    /// </summary>
    public void ResetTriggers()
    {
        ticksSinceLastBoss = 0;
        cooldownTicksRemaining = 0;
        lastKnownNightmare = 0f;
        hasTriggeredExploration = false;
        hasTriggeredDistance = false;
    }

    /// <summary>
    /// Im GameManager in AdvanceTicks(...) aufrufen.
    /// </summary>
    public void NotifyTicksAdvanced(int ticks)
    {
        if (ticks <= 0) return;

        if (BossDirector.Instance != null && BossDirector.Instance.HasActiveBoss)
        {
            return; // Während ein Boss aktiv ist, laufen die Spawn-Zähler nicht weiter
        }

        if (cooldownTicksRemaining > 0)
        {
            cooldownTicksRemaining = Mathf.Max(0, cooldownTicksRemaining - ticks);
            return;
        }

        ticksSinceLastBoss += ticks;

        if (enableAutomaticTriggers && triggerOnTotalTicks && ticksSinceLastBoss >= ticksUntilBossSpawn)
        {
            TryFireTrigger(tickBossOverride, $"Tick-Schwelle ({ticksSinceLastBoss}/{ticksUntilBossSpawn})");
        }
    }

    /// <summary>
    /// Im GameManager in AddNightmare(...) aufrufen (mit dem aktuellen Albtraum-Wert).
    /// </summary>
    public void NotifyNightmareValue(float currentNightmare)
    {
        lastKnownNightmare = currentNightmare;

        if (!enableAutomaticTriggers || !triggerOnNightmare) return;
        if (!CanTriggerNow()) return;

        if (lastKnownNightmare >= nightmareThreshold)
        {
            TryFireTrigger(nightmareBossOverride, $"Albtraum-Schwelle ({lastKnownNightmare:0}% >= {nightmareThreshold:0}%)");
        }
    }

    /// <summary>
    /// Für manuelle Events (z. B. Mystery-Raum, verfluchter Altar, Story-Event).
    /// </summary>
    public bool TriggerCustomEvent(string eventName, BossProfile specificProfile = null, bool ignoreCooldown = true)
    {
        if (!ignoreCooldown && !CanTriggerNow()) return false;
        return TryFireTrigger(specificProfile, $"Event: {eventName}");
    }

    private void CheckMacroGridTriggers()
    {
        if (!CanTriggerNow() || MacroGridManager.Instance == null) return;

        // 3. Besuchte Räume prüfen
        if (triggerOnVisitedRooms && !hasTriggeredExploration)
        {
            if (MacroGridManager.Instance.VisitedRoomCount >= visitedRoomsThreshold)
            {
                if (TryFireTrigger(explorationBossOverride, $"Erkundung ({MacroGridManager.Instance.VisitedRoomCount} Räume)"))
                {
                    hasTriggeredExploration = true;
                    return;
                }
            }
        }

        // 4. Abstand zum Start-Hub prüfen
        if (triggerOnHubDistance && !hasTriggeredDistance)
        {
            Vector2Int current = MacroGridManager.Instance.CurrentCoordinate;
            Vector2Int hub = MacroGridManager.Instance.StartCoordinate;
            int dist = Mathf.Max(Mathf.Abs(current.x - hub.x), Mathf.Abs(current.y - hub.y));

            if (dist >= hubDistanceThreshold)
            {
                if (TryFireTrigger(distanceBossOverride, $"Hub-Distanz ({dist} Felder)"))
                {
                    hasTriggeredDistance = true;
                }
            }
        }
    }

    private bool CanTriggerNow()
    {
        if (BossDirector.Instance == null) return false;
        if (BossDirector.Instance.HasActiveBoss) return false;
        if (cooldownTicksRemaining > 0) return false;
        return true;
    }

    private bool TryFireTrigger(BossProfile profileOverride, string reason)
    {
        if (BossDirector.Instance == null) return false;

        bool spawned = BossDirector.Instance.TryTriggerBoss(profileOverride, reason);
        if (spawned)
        {
            ticksSinceLastBoss = 0;
        }
        return spawned;
    }

    private void HandleBossDespawned(BossProfile profile, string reason)
    {
        ticksSinceLastBoss = 0;
        cooldownTicksRemaining = cooldownTicksAfterDespawn;
    }
}
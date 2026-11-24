using System;
using UnityEngine;

public class BossMacroBehaviorController : MonoBehaviour
{
    public static BossMacroBehaviorController Instance { get; private set; }

    [Header("Bewegung & Visuals")]
    [SerializeField] private float visualMoveSpeed = 6f;

    [Header("Laufzeit-Status (Read-Only)")]
    [SerializeField] private string currentPhaseName = "-";
    [SerializeField] private int ticksUntilNextAction = 0;
    [SerializeField] private int actionsPerformed = 0;
    [SerializeField] private Vector2Int currentTargetCoord;
    [SerializeField] private bool hasValidTarget = false;

    public static event Action<BossProfile, BossPhaseConfig> OnBossPhaseChanged;

    private BossPhaseConfig currentPhase;
    private Vector3 desiredLocalPos;
    private bool isMovingVisually = false;

    public int TicksUntilNextAction => ticksUntilNextAction;
    public Vector2Int CurrentTargetCoord => currentTargetCoord;
    public bool HasValidTarget => hasValidTarget;
    public BossPhaseConfig CurrentPhase => currentPhase;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(this); return; }
    }

    private void OnEnable()
    {
        BossDirector.OnBossSpawned += HandleBossSpawned;
        BossDirector.OnBossDespawned += HandleBossDespawned;
    }

    private void OnDisable()
    {
        BossDirector.OnBossSpawned -= HandleBossSpawned;
        BossDirector.OnBossDespawned -= HandleBossDespawned;
    }

    private void Update()
    {
        if (!isMovingVisually || BossDirector.Instance == null || !BossDirector.Instance.HasActiveBoss)
            return;

        Transform bossTf = BossDirector.Instance.ActiveBossObject.transform;
        bossTf.localPosition = Vector3.MoveTowards(bossTf.localPosition, desiredLocalPos, visualMoveSpeed * Time.deltaTime);

        if ((bossTf.localPosition - desiredLocalPos).sqrMagnitude < 0.0001f)
        {
            bossTf.localPosition = desiredLocalPos;
            isMovingVisually = false;
        }
    }

    public void NotifyTicksAdvanced(int ticks)
    {
        if (ticks <= 0 || BossDirector.Instance == null || !BossDirector.Instance.HasActiveBoss)
            return;

        if (CheckVictoryConditions()) return;

        ticksUntilNextAction -= ticks;

        if (ticksUntilNextAction <= 0)
        {
            ExecuteBossTurn();
        }
    }

    private void HandleBossSpawned(BossProfile profile, GameObject bossObj, Vector2Int spawnCoord)
    {
        actionsPerformed = 0;
        isMovingVisually = false;

        UpdatePhase(profile, forceNotify: true);
        ticksUntilNextAction = currentPhase != null ? Mathf.Max(1, currentPhase.actionTickInterval) : 4;

        SelectAndTelegraphNextTarget();
    }

    private void HandleBossDespawned(BossProfile profile, string reason)
    {
        ClearCurrentTelegraph();
        currentPhase = null;
        currentPhaseName = "-";
        ticksUntilNextAction = 0;
        actionsPerformed = 0;
        isMovingVisually = false;
    }

    private void ExecuteBossTurn()
    {
        BossProfile profile = BossDirector.Instance.ActiveProfile;
        if (profile == null) return;

        UpdatePhase(profile, forceNotify: false);

        Vector2Int bossCoord = BossDirector.Instance.ActiveBossCoord;
        Vector2Int actionTarget = hasValidTarget ? currentTargetCoord : bossCoord;

        ClearCurrentTelegraph();

        // 1. Bewegung gemäß Phasen-Konfiguration
        int steps = currentPhase != null ? currentPhase.stepsPerTurn : 1;
        for (int s = 0; s < steps; s++)
        {
            Vector2Int nextStep = StepTowards(bossCoord, actionTarget);
            if (nextStep == bossCoord) break;
            bossCoord = nextStep;
        }

        if (bossCoord != BossDirector.Instance.ActiveBossCoord)
        {
            MoveBossToCoordinate(bossCoord);
        }

        // 2. Alle eingehängten Action-Bausteine der aktuellen Phase ausführen
        BossActionContext ctx = BuildContext(profile, bossCoord, actionTarget);
        if (currentPhase != null && currentPhase.actions != null)
        {
            for (int i = 0; i < currentPhase.actions.Count; i++)
            {
                if (currentPhase.actions[i] != null)
                {
                    currentPhase.actions[i].Execute(ctx);
                }
            }
        }

        actionsPerformed++;

        // 3. Sieg-/Rückzugsbedingung prüfen
        if (CheckVictoryConditions()) return;

        // 4. Prüfen, ob jetzt eine neue Phase beginnt
        UpdatePhase(profile, forceNotify: false);

        // 5. Nächsten Countdown starten & neues Ziel ankündigen
        ticksUntilNextAction = currentPhase != null ? Mathf.Max(1, currentPhase.actionTickInterval) : 4;
        SelectAndTelegraphNextTarget();
    }

    private bool CheckVictoryConditions()
    {
        BossProfile profile = BossDirector.Instance.ActiveProfile;
        if (profile == null) return false;

        if (profile.victoryCondition == BossVictoryConditionType.SurviveActions &&
            actionsPerformed >= profile.victoryTargetValue)
        {
            BossDirector.Instance.DespawnActiveBoss($"Überlebt ({actionsPerformed} Aktionen)");
            return true;
        }

        if (profile.victoryCondition == BossVictoryConditionType.AltarBanishment && AltarManager.Instance != null)
        {
            int activeAltars = 0;
            foreach (var kvp in AltarManager.Instance.Altars)
            {
                if (kvp.Value != null && kvp.Value.status == AltarStatus.Active)
                    activeAltars++;
            }

            if (activeAltars >= profile.victoryTargetValue)
            {
                BossDirector.Instance.DespawnActiveBoss($"Durch {activeAltars} aktive Altäre gebannt!");
                return true;
            }
        }

        return false;
    }

    private void UpdatePhase(BossProfile profile, bool forceNotify)
    {
        BossPhaseConfig nextPhase = profile.GetPhaseForActionCount(actionsPerformed);
        if (nextPhase == null) return;

        if (forceNotify || nextPhase != currentPhase)
        {
            currentPhase = nextPhase;
            currentPhaseName = currentPhase.phaseName;

            // Farbe am Boss-Sprite anpassen (falls vorhanden)
            if (BossDirector.Instance.ActiveBossObject != null)
            {
                var sr = BossDirector.Instance.ActiveBossObject.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.color = currentPhase.phaseColor;
            }

            Debug.Log($"[BossPhase] {profile.displayName} wechselt in '{currentPhase.phaseName}'!");
            OnBossPhaseChanged?.Invoke(profile, currentPhase);
        }
    }

    private void SelectAndTelegraphNextTarget()
    {
        ClearCurrentTelegraph();
        if (BossDirector.Instance == null || !BossDirector.Instance.HasActiveBoss) return;

        BossProfile profile = BossDirector.Instance.ActiveProfile;
        Vector2Int bossCoord = BossDirector.Instance.ActiveBossCoord;
        BossActionContext ctx = BuildContext(profile, bossCoord, bossCoord);

        // Erste Action in der Phase, die ein Ziel vorschlägt, bestimmt das Telegraphing
        if (currentPhase != null && currentPhase.actions != null)
        {
            for (int i = 0; i < currentPhase.actions.Count; i++)
            {
                if (currentPhase.actions[i] != null &&
                    currentPhase.actions[i].TrySelectTarget(ctx, out Vector2Int chosenTarget))
                {
                    currentTargetCoord = chosenTarget;
                    hasValidTarget = true;
                    SetCellTargeted(currentTargetCoord, true);
                    return;
                }
            }
        }

        // Fallback: Spieler-Position anvisieren
        currentTargetCoord = ctx.SlimeCoord;
        hasValidTarget = true;
        SetCellTargeted(currentTargetCoord, true);
    }

    private BossActionContext BuildContext(BossProfile profile, Vector2Int bossCoord, Vector2Int targetCoord)
    {
        Vector2Int slimeCoord = MacroGridManager.Instance != null ? MacroGridManager.Instance.CurrentCoordinate : Vector2Int.zero;
        Vector2Int hubCoord = MacroGridManager.Instance != null ? MacroGridManager.Instance.StartCoordinate : Vector2Int.zero;

        return new BossActionContext
        {
            Profile = profile,
            CurrentPhase = currentPhase,
            BossCoord = bossCoord,
            TargetCoord = targetCoord,
            SlimeCoord = slimeCoord,
            HubCoord = hubCoord,
            TotalActionsPerformed = actionsPerformed
        };
    }

    private Vector2Int StepTowards(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;
        if (delta == Vector2Int.zero) return from;

        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            return from + new Vector2Int(delta.x > 0 ? 1 : -1, 0);
        else
            return from + new Vector2Int(0, delta.y > 0 ? 1 : -1);
    }

    private void MoveBossToCoordinate(Vector2Int newCoord)
    {
        if (GridManager.Instance == null || BossDirector.Instance == null) return;

        BossDirector.Instance.UpdateActiveBossCoordinate(newCoord);

        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        desiredLocalPos = new Vector3(newCoord.x * offset, newCoord.y * offset, -0.6f);
        isMovingVisually = true;
    }

    private void ClearCurrentTelegraph()
    {
        if (hasValidTarget)
        {
            SetCellTargeted(currentTargetCoord, false);
            hasValidTarget = false;
        }
    }

    private void SetCellTargeted(Vector2Int coord, bool targeted)
    {
        if (GridManager.Instance == null || GridManager.Instance.grid == null) return;
        if (coord.x < 0 || coord.x >= GridManager.Instance.width ||
            coord.y < 0 || coord.y >= GridManager.Instance.height) return;

        GridCell cell = GridManager.Instance.grid[coord.x, coord.y];
        if (cell != null) cell.isTargeted = targeted;

        if (MacroGridManager.Instance != null)
        {
            MacroGridManager.Instance.RefreshActiveRoomDoors();
        }
    }
}
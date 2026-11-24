using System;
using System.Collections.Generic;
using UnityEngine;

public enum BossSpawnSurfaceRule
{
    VoidOnly,
    BuiltRoomsOnly,
    AnyNonHub
}

public enum BossVictoryConditionType
{
    ArenaFightOnly,       // Wird erst im Platformer-Bosskampf besiegt
    SurviveActions,       // Zieht sich nach X eigenen Aktionen zurück
    AltarBanishment       // Wird gebannt, sobald X Altäre aktiv/gereinigt sind
}

[Serializable]
public class BossPhaseConfig
{
    public string phaseName = "Phase 1";
    public string phaseBannerText = "";
    public Color phaseColor = new Color(0.8f, 0.15f, 0.35f, 1f);

    [Tooltip("Ab wie vielen ausgeführten Boss-Aktionen startet diese Phase? (Phase 1 = 0)")]
    [Min(0)] public int startAtActionCount = 0;

    [Tooltip("Alle wie viele Ticks handelt der Boss in dieser Phase?")]
    [Range(1, 15)] public int actionTickInterval = 4;

    [Tooltip("Wie viele Felder bewegt sich der Boss pro Zug auf das Ziel zu? (0 = bleibt stehen)")]
    [Range(0, 4)] public int stepsPerTurn = 1;

    [Tooltip("Liste der Aktionen, die der Boss in dieser Phase bei Tick 0 der Reihe nach ausführt.")]
    public List<BossMacroActionSO> actions = new List<BossMacroActionSO>();
}

[CreateAssetMenu(fileName = "NewBossProfile", menuName = "Slimily/Boss/Boss Profile")]
public class BossProfile : ScriptableObject
{
    [Header("Identität")]
    public string bossId = "boss_devourer";
    public string displayName = "Der Verschlinger";
    public string spawnBannerTitle = "DER VERSCHLINGER ERWACHT";
    public Color themeColor = new Color(0.75f, 0.15f, 0.35f, 1f);

    [Header("Prefab")]
    [Tooltip("Das Macro-Boss-Prefab, das auf dem Grid gespawnt wird.")]
    public GameObject bossPrefab;

    [Header("Spawn-Regeln (Ring um den Spieler)")]
    public BossSpawnSurfaceRule surfaceRule = BossSpawnSurfaceRule.VoidOnly;
    [Range(2, 12)] public int minSpawnDistanceFromSlime = 4;
    [Range(3, 20)] public int maxSpawnDistanceFromSlime = 8;
    [Range(1, 10)] public int minDistanceFromHub = 3;

    [Header("Sieg- / Bann-Bedingung")]
    public BossVictoryConditionType victoryCondition = BossVictoryConditionType.ArenaFightOnly;
    [Tooltip("Bei 'SurviveActions': Anzahl Aktionen bis Rückzug. Bei 'AltarBanishment': Benötigte aktive Altäre.")]
    [Min(1)] public int victoryTargetValue = 5;

    [Header("Phasen-Baukasten")]
    [Tooltip("Definiere beliebig viele Phasen. Die Phase mit dem höchsten erreichten 'startAtActionCount' ist aktiv.")]
    public List<BossPhaseConfig> phases = new List<BossPhaseConfig>();

    public BossPhaseConfig GetPhaseForActionCount(int actionsPerformed)
    {
        if (phases == null || phases.Count == 0) return null;

        BossPhaseConfig best = phases[0];
        for (int i = 0; i < phases.Count; i++)
        {
            if (phases[i] != null && actionsPerformed >= phases[i].startAtActionCount)
            {
                if (phases[i].startAtActionCount >= best.startAtActionCount)
                {
                    best = phases[i];
                }
            }
        }
        return best;
    }
}
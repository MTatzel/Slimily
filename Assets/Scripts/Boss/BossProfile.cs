using UnityEngine;

public enum BossSpawnSurfaceRule
{
    VoidOnly,        // Spawnt nur auf unbebauten Feldern (frisst sich von außen heran)
    BuiltRoomsOnly,  // Spawnt nur auf bereits gebauten Räumen (nicht auf dem Hub/Slime)
    AnyNonHub        // Egal ob gebaut oder leer, Hauptsache nicht auf Hub oder Slime
}

public enum BossMacroBehaviorType
{
    Destroyer,       // Zerstört markierte Räume
    Corruptor,       // Korrumpiert Altäre & verseucht Räume
    Hunter           // Jagt gezielt den Spieler
}

[CreateAssetMenu(fileName = "NewBossProfile", menuName = "Slimily/Boss/Boss Profile")]
public class BossProfile : ScriptableObject
{
    [Header("Identität")]
    public string bossId = "boss_devourer";
    public string displayName = "Der Verschlinger";
    public string spawnBannerTitle = "DER VERSCHLINGER ERWACHT";
    public Color themeColor = new Color(0.75f, 0.15f, 0.35f, 1f);

    [Header("Prefab & Verhalten")]
    [Tooltip("Das Macro-Boss-Prefab, das auf dem Grid gespawnt wird.")]
    public GameObject bossPrefab;
    public BossMacroBehaviorType behaviorType = BossMacroBehaviorType.Destroyer;

    [Tooltip("Alle wie viele Ticks führt der Boss eine Macro-Aktion aus?")]
    [Range(1, 15)] public int actionTickInterval = 4;

    [Header("Spawn-Regeln (Ring um den Spieler)")]
    public BossSpawnSurfaceRule surfaceRule = BossSpawnSurfaceRule.VoidOnly;

    [Tooltip("Minimaler Abstand (in Grid-Feldern) zum Slime beim Spawnen.")]
    [Range(2, 12)] public int minSpawnDistanceFromSlime = 4;

    [Tooltip("Maximaler Abstand (in Grid-Feldern) zum Slime beim Spawnen.")]
    [Range(3, 20)] public int maxSpawnDistanceFromSlime = 8;

    [Tooltip("Mindestabstand zum Start-Hub.")]
    [Range(1, 10)] public int minDistanceFromHub = 3;

    [Header("Lebensdauer (Optional)")]
    [Tooltip("0 = bleibt bis besiegt. >0 = verschwindet nach X eigenen Boss-Aktionen wieder.")]
    [Range(0, 20)] public int maxActionsBeforeRetreat = 0;
}
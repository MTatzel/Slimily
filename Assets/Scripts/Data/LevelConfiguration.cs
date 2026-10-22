using System.Collections.Generic;
using UnityEngine;

public enum LevelGoalType
{
    DefeatBoss,      // Ziel: Boss besiegen
    ReachExit,       // Ziel: Von Start zu Zielkoordinate gelangen
    SurviveTurns,    // Ziel: Bestimmte Rundenanzahl überleben
    ClearMinions     // Ziel: Alle Minions auf dem Feld besiegen
}

[CreateAssetMenu(fileName = "LevelConfig_New", menuName = "Slimily/Level Configuration")]
public class LevelConfiguration : ScriptableObject
{
    [Header("Level-Ziel & Bedingungen")]
    public LevelGoalType goalType = LevelGoalType.DefeatBoss;
    public Vector2Int exitCoordinate = new Vector2Int(45, 45);
    public int turnsToSurvive = 5;

    [Header("Grid-Dimensionen")]
    public int gridWidth = 50;
    public int gridHeight = 50;
    public float cellOffset = 1.1f;
    public Vector2Int startCoordinate = new Vector2Int(25, 25);

    [Header("Räume & Layout")]
    public Room defaultRoomPrefab;
    public float roomSpacing = 100f;

    [Header("Boss & Encounter")]
    public BossBase bossPrefab;
    public Vector2Int bossSpawnCoordinate = new Vector2Int(25, 40);
    public List<GameObject> minionPrefabs = new List<GameObject>();

    [Header("Optik & Theming")]
    public Sprite backgroundSprite;
    public Color gridEmptyColor = Color.white;
}
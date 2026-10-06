using UnityEngine;

[CreateAssetMenu(fileName = "LevelConfig_New", menuName = "Slimily/Level Configuration")]
public class LevelConfiguration : ScriptableObject
{
    [Header("Grid-Dimensionen")]
    public int gridWidth = 50;
    public int gridHeight = 50;
    public float cellOffset = 1.1f;
    public Vector2Int startCoordinate = new Vector2Int(25, 25);

    [Header("Räume & Layout")]
    public Room defaultRoomPrefab;
    public float roomSpacing = 100f;

    [Header("Boss & Encounter")]
    public GameObject bossPrefab;
    public Vector2Int bossSpawnCoordinate = new Vector2Int(25, 40);

    [Header("Optik & Theming")]
    public Sprite backgroundSprite;
    public Color gridEmptyColor = Color.white;
}
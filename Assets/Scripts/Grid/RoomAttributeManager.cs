using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hier kannst du jederzeit neue Raum-Attribute / Symbole ergänzen.
/// </summary>
public enum RoomAttributeType
{
    None = 0,
    Altar = 1,          // Altar / Schrein
    Survival = 2,       // Überlebensraum (Sanduhr)
    Mystery = 3,        // Unbekanntes Event (Fragezeichen)
    Loot = 4,           // Beute-Raum (Truhe)
    Corrupted = 5       // Vom Boss manipuliertes / verfluchtes Feld
}

[Serializable]
public class RoomAttributeDefinition
{
    public RoomAttributeType type;
    public string displayName;
    [Tooltip("Optional: Ziehe hier später dein eigenes Sprite rein. Wenn leer, wird automatisch das Pixel-Art-Symbol genutzt.")]
    public Sprite customSprite;
    public Color iconColor = Color.white;
    [Tooltip("Relative Gewichtung, falls beim Einsammeln eines neuen Würfels zufällige Symbole ausgewürfelt werden.")]
    [Range(0, 100)] public int randomRollWeight = 25;
}

public class RoomAttributeManager : MonoBehaviour
{
    private static RoomAttributeManager instance;
    public static RoomAttributeManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<RoomAttributeManager>();
                if (instance == null && GameManager.Instance != null)
                {
                    instance = GameManager.Instance.gameObject.AddComponent<RoomAttributeManager>();
                }
            }
            return instance;
        }
    }

    [Header("Zufällige Symbol-Verteilung auf neuen 6er-Würfeln")]
    [Tooltip("Wenn aktiv, erhalten neu gefundene 6er-Würfel automatisch 1 bis X zufällige Symbole auf ihren Blöcken.")]
    public bool rollRandomAttributesOnNewCubes = true;
    [Range(0, 4)] public int minSymbolsPerCube = 1;
    [Range(0, 4)] public int maxSymbolsPerCube = 2;

    [Header("Attribut-Katalog (Erweiterbar im Inspector)")]
    public List<RoomAttributeDefinition> attributeDefinitions = new List<RoomAttributeDefinition>();

    private readonly Dictionary<RoomAttributeType, Sprite> proceduralFallbackSprites = new Dictionary<RoomAttributeType, Sprite>();

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) { Destroy(this); return; }

        EnsureDefaultDefinitions();
        GenerateAllFallbackSprites();
    }

    private void EnsureDefaultDefinitions()
    {
        if (attributeDefinitions != null && attributeDefinitions.Count > 0) return;

        attributeDefinitions = new List<RoomAttributeDefinition>
        {
            new RoomAttributeDefinition
            {
                type = RoomAttributeType.Altar,
                displayName = "Altar",
                iconColor = new Color(0.55f, 0.95f, 1f, 1f),    // Helles Cyan
                randomRollWeight = 25
            },
            new RoomAttributeDefinition
            {
                type = RoomAttributeType.Survival,
                displayName = "Überleben (Sanduhr)",
                iconColor = new Color(1f, 0.85f, 0.35f, 1f),   // Sand-Gold
                randomRollWeight = 30
            },
            new RoomAttributeDefinition
            {
                type = RoomAttributeType.Mystery,
                displayName = "Unbekannt (?)",
                iconColor = new Color(0.92f, 0.65f, 1f, 1f),   // Helles Violett
                randomRollWeight = 30
            },
            new RoomAttributeDefinition
            {
                type = RoomAttributeType.Loot,
                displayName = "Beute (Truhe)",
                iconColor = new Color(1f, 0.92f, 0.25f, 1f),   // Schatz-Gelb
                randomRollWeight = 20
            },
            new RoomAttributeDefinition
            {
                type = RoomAttributeType.Corrupted,
                displayName = "Boss-Fluch",
                iconColor = new Color(1f, 0.28f, 0.35f, 1f),   // Karmesinrot (nur durch Boss, Gewicht 0)
                randomRollWeight = 0
            }
        };
    }

    public RoomAttributeDefinition GetDefinition(RoomAttributeType type)
    {
        EnsureDefaultDefinitions();
        for (int i = 0; i < attributeDefinitions.Count; i++)
        {
            if (attributeDefinitions[i].type == type)
            {
                return attributeDefinitions[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Gibt das zugewiesene Custom-Sprite aus dem Inspector zurück – oder das prozedurale Fallback-Symbol.
    /// </summary>
    public Sprite GetIcon(RoomAttributeType type)
    {
        if (type == RoomAttributeType.None) return null;

        RoomAttributeDefinition def = GetDefinition(type);
        if (def != null && def.customSprite != null)
        {
            return def.customSprite;
        }

        if (proceduralFallbackSprites.Count == 0)
        {
            GenerateAllFallbackSprites();
        }

        if (proceduralFallbackSprites.TryGetValue(type, out Sprite fallback))
        {
            return fallback;
        }

        return proceduralFallbackSprites.TryGetValue(RoomAttributeType.Mystery, out Sprite defaultIcon) ? defaultIcon : null;
    }

    public Color GetIconColor(RoomAttributeType type)
    {
        if (type == RoomAttributeType.None) return Color.clear;
        RoomAttributeDefinition def = GetDefinition(type);
        return def != null ? def.iconColor : Color.white;
    }

    /// <summary>
    /// Weist einem 6er-Würfel beim Einsammeln Symbole zu (übernimmt feste Vorlagen oder würfelt neue aus).
    /// </summary>
    public void AssignAttributesToShape(ShapeData shape)
    {
        if (shape == null || shape.category != CardCategory.CubeShape || shape.cells == null || shape.cells.Count == 0)
            return;

        // 1. Prüfen, ob aus der Vorlage bereits ein BlockModifier.Shrine vorhanden ist
        bool alreadyHasAttributes = false;
        for (int i = 0; i < shape.cells.Count; i++)
        {
            ShapeCell cell = shape.cells[i];
            if (cell.attribute == RoomAttributeType.None && cell.modifiers != null && cell.modifiers.Contains(BlockModifier.Shrine))
            {
                cell.attribute = RoomAttributeType.Altar;
            }

            if (cell.attribute != RoomAttributeType.None)
            {
                alreadyHasAttributes = true;
            }
        }

        if (!rollRandomAttributesOnNewCubes || alreadyHasAttributes) return;

        int symbolCount = UnityEngine.Random.Range(minSymbolsPerCube, maxSymbolsPerCube + 1);
        symbolCount = Mathf.Clamp(symbolCount, 0, shape.cells.Count);
        if (symbolCount <= 0) return;

        List<int> cellIndices = new List<int>();
        for (int i = 0; i < shape.cells.Count; i++) cellIndices.Add(i);

        for (int i = cellIndices.Count - 1; i > 0; i--)
        {
            int swap = UnityEngine.Random.Range(0, i + 1);
            int tmp = cellIndices[i];
            cellIndices[i] = cellIndices[swap];
            cellIndices[swap] = tmp;
        }

        for (int s = 0; s < symbolCount; s++)
        {
            RoomAttributeType rolled = RollRandomAttributeType();
            if (rolled != RoomAttributeType.None)
            {
                shape.cells[cellIndices[s]].attribute = rolled;
            }
        }
    }

    public RoomAttributeType RollRandomAttributeType()
    {
        EnsureDefaultDefinitions();

        int totalWeight = 0;
        for (int i = 0; i < attributeDefinitions.Count; i++)
        {
            totalWeight += Mathf.Max(0, attributeDefinitions[i].randomRollWeight);
        }

        if (totalWeight <= 0) return RoomAttributeType.None;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int current = 0;

        for (int i = 0; i < attributeDefinitions.Count; i++)
        {
            int w = Mathf.Max(0, attributeDefinitions[i].randomRollWeight);
            if (w <= 0) continue;

            current += w;
            if (roll < current)
            {
                return attributeDefinitions[i].type;
            }
        }

        return RoomAttributeType.None;
    }

    // =========================================================================
    // PROZEDURALE PIXEL-ART SYMBOLE (FALLBACK SOLANGE KEIN SPRITE ZUGEWIESEN)
    // =========================================================================

    private void GenerateAllFallbackSprites()
    {
        // 1. Sanduhr (Survival)
        proceduralFallbackSprites[RoomAttributeType.Survival] = CreateIconFromPattern(new[]
        {
            "................",
            "..############..",
            "..############..",
            "...##......##...",
            "....##.oo.##....",
            ".....##oo##.....",
            "......####......",
            ".......##.......",
            ".......##.......",
            "......####......",
            ".....##..##.....",
            "....##.oo.##....",
            "...##.oooo.##...",
            "..############..",
            "..############..",
            "................"
        });

        // 2. Fragezeichen (Mystery)
        proceduralFallbackSprites[RoomAttributeType.Mystery] = CreateIconFromPattern(new[]
        {
            "................",
            ".....######.....",
            "...##########...",
            "...###....###...",
            "...###....###...",
            "..........###...",
            "........####....",
            ".......####.....",
            "......###.......",
            "......###.......",
            "................",
            "......###.......",
            ".....#####......",
            ".....#####......",
            "......###.......",
            "................"
        });

        // 3. Altar / Schrein (Kristall über Podest)
        proceduralFallbackSprites[RoomAttributeType.Altar] = CreateIconFromPattern(new[]
        {
            "................",
            ".......##.......",
            "......####......",
            ".....##oo##.....",
            "....###oo###....",
            ".....##oo##.....",
            "......####......",
            ".......##.......",
            "................",
            "....########....",
            "...##########...",
            ".....######.....",
            ".....######.....",
            "...##########...",
            "..############..",
            "................"
        });

        // 4. Truhe (Loot)
        proceduralFallbackSprites[RoomAttributeType.Loot] = CreateIconFromPattern(new[]
        {
            "................",
            "................",
            "...##########...",
            "..############..",
            "..###......###..",
            "..############..",
            "..#####oo#####..",
            ".......oo.......",
            "..#####oo#####..",
            "..#####oo#####..",
            "..###......###..",
            "..###......###..",
            "..############..",
            "..############..",
            "................",
            "................"
        });

        // 5. Boss-Fluch / Korrumpiert (Gezacktes Mal)
        proceduralFallbackSprites[RoomAttributeType.Corrupted] = CreateIconFromPattern(new[]
        {
            "................",
            "..##........##..",
            "...##..##..##...",
            "....########....",
            "..############..",
            "...###.oo.###...",
            "..####oooo####..",
            "..####oooo####..",
            "...###.oo.###...",
            "..############..",
            "....########....",
            "...##..##..##...",
            "..##........##..",
            "................",
            "................",
            "................"
        });
    }

    private static Sprite CreateIconFromPattern(string[] rows)
    {
        const int gridSize = 16;
        const int scale = 2; // 32x32 Textur
        const int texSize = gridSize * scale;

        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        bool[,] isMain = new bool[gridSize, gridSize];
        bool[,] isAccent = new bool[gridSize, gridSize];

        for (int r = 0; r < gridSize; r++)
        {
            string rowStr = rows[gridSize - 1 - r]; // Y umkehren für Unity-Texturkoordinaten
            for (int c = 0; c < gridSize; c++)
            {
                char ch = c < rowStr.Length ? rowStr[c] : '.';
                if (ch == '#') isMain[c, r] = true;
                else if (ch == 'o') isAccent[c, r] = true;
            }
        }

        Color outlineCol = new Color(0.04f, 0.04f, 0.07f, 0.96f);
        Color mainCol = Color.white;
        Color accentCol = new Color(1f, 0.95f, 0.75f, 1f);

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                Color pixelCol = Color.clear;

                if (isMain[x, y])
                {
                    pixelCol = mainCol;
                }
                else if (isAccent[x, y])
                {
                    pixelCol = accentCol;
                }
                else
                {
                    // Dunkle Kontur um alle Nachbarpixel legen, damit das Symbol auf jeder Biom-Farbe lesbar ist
                    bool nearFilled = false;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < gridSize && ny >= 0 && ny < gridSize)
                            {
                                if (isMain[nx, ny] || isAccent[nx, ny])
                                {
                                    nearFilled = true;
                                    break;
                                }
                            }
                        }
                        if (nearFilled) break;
                    }

                    if (nearFilled)
                    {
                        pixelCol = outlineCol;
                    }
                }

                for (int sy = 0; sy < scale; sy++)
                {
                    for (int sx = 0; sx < scale; sx++)
                    {
                        tex.SetPixel(x * scale + sx, y * scale + sy, pixelCol);
                    }
                }
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), texSize * 1.35f);
    }
}
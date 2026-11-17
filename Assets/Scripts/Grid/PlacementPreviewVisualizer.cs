using System.Collections.Generic;
using UnityEngine;

public class PlacementPreviewVisualizer : MonoBehaviour
{
    [Header("Transparenz & Größe der Vorschau")]
    [Tooltip("Deckkraft der Biom-Farbe, wenn die Form gültig platziert werden kann.")]
    [Range(0.2f, 0.9f)] public float validFillAlpha = 0.58f;

    [Tooltip("Deckkraft der Biom-Farbe, wenn die Position ungültig ist.")]
    [Range(0.1f, 0.7f)] public float invalidFillAlpha = 0.30f;

    [Tooltip("Wie stark die Biom-Farbe bei ungültiger Position ausgegraut wird (0 = volle Farbe, 1 = komplett grau).")]
    [Range(0f, 0.85f)] public float invalidDesaturation = 0.50f;

    [Tooltip("Größe der Vorschau-Kachel relativ zur Grid-Zelle (etwas kleiner hebt sie klar von gebauten Räumen ab).")]
    [Range(0.75f, 1.0f)] public float ghostCellScale = 0.88f;

    [Header("Pulsieren & Rahmen")]
    public bool enableBreathingPulse = true;
    public float pulseSpeed = 4.5f;
    [Range(0f, 0.2f)] public float pulseAlphaAmplitude = 0.08f;

    public Color validBorderColor = new Color(1f, 1f, 1f, 0.90f);
    public Color invalidCrossColor = new Color(0.95f, 0.95f, 0.98f, 0.75f);
    public Color blockedCellCrossColor = new Color(1f, 1f, 1f, 0.98f);

    private class GhostCellVisual
    {
        public GameObject rootObj;
        public SpriteRenderer fillRenderer;
        public SpriteRenderer frameRenderer;
        public Color baseBiomeColor;
        public bool isOverallValid;
        public bool isIndividuallyBlocked;
    }

    private readonly List<GhostCellVisual> activeGhostCells = new List<GhostCellVisual>();
    private GridManager gridManager;

    private static Sprite fillSprite;
    private static Sprite validFrameSprite;
    private static Sprite invalidCrossFrameSprite;

    private void Awake()
    {
        EnsureProceduralSprites();
    }

    private void Update()
    {
        if (!enableBreathingPulse || activeGhostCells.Count == 0) return;

        float pulseOffset = Mathf.Sin(Time.time * pulseSpeed) * pulseAlphaAmplitude;

        for (int i = 0; i < activeGhostCells.Count; i++)
        {
            GhostCellVisual ghost = activeGhostCells[i];
            if (ghost.rootObj == null || !ghost.rootObj.activeSelf) continue;

            ApplyColorsToGhost(ghost, pulseOffset);
        }
    }

    /// <summary>
    /// Zeigt die aktuelle 6er-Form mit ihren echten Biom-Farben halbtransparent auf dem Grid an.
    /// </summary>
    public void ShowPreview(GridManager gm, ShapeData shape, int startX, int startY, bool isOverallValid)
    {
        if (gm == null || shape == null || shape.cells == null)
        {
            HidePreview();
            return;
        }

        gridManager = gm;
        EnsureProceduralSprites();
        EnsurePoolSize(shape.cells.Count);

        int sortingLayerID = 0;
        int baseSortingOrder = 0;
        if (gridManager.cellPrefab != null)
        {
            SpriteRenderer prefabSr = gridManager.cellPrefab.GetComponentInChildren<SpriteRenderer>();
            if (prefabSr != null)
            {
                sortingLayerID = prefabSr.sortingLayerID;
                baseSortingOrder = prefabSr.sortingOrder;
            }
        }

        float offset = gridManager.offset > 0.01f ? gridManager.offset : 1.1f;

        for (int i = 0; i < activeGhostCells.Count; i++)
        {
            if (i >= shape.cells.Count)
            {
                activeGhostCells[i].rootObj.SetActive(false);
                continue;
            }

            ShapeCell cell = shape.cells[i];
            int gridX = startX + cell.localPosition.x;
            int gridY = startY + cell.localPosition.y;

            bool isOutOfBounds = (gridX < 0 || gridX >= gridManager.width || gridY < 0 || gridY >= gridManager.height);
            bool isCellBlocked = isOutOfBounds || !gridManager.grid[gridX, gridY].IsBuildable;

            ZoneType biome = cell.hasAssignedZone ? cell.assignedZone : ZoneType.Forest;
            Color biomeColor = gridManager.GetZoneColor(biome);

            GhostCellVisual ghost = activeGhostCells[i];
            ghost.rootObj.SetActive(true);
            ghost.rootObj.transform.localPosition = new Vector3(gridX * offset, gridY * offset, -0.35f);
            ghost.rootObj.transform.localScale = new Vector3(ghostCellScale, ghostCellScale, 1f);

            ghost.fillRenderer.sortingLayerID = sortingLayerID;
            ghost.fillRenderer.sortingOrder = baseSortingOrder + 5;

            ghost.frameRenderer.sortingLayerID = sortingLayerID;
            ghost.frameRenderer.sortingOrder = baseSortingOrder + 6;
            ghost.frameRenderer.sprite = isOverallValid ? validFrameSprite : invalidCrossFrameSprite;

            ghost.baseBiomeColor = biomeColor;
            ghost.isOverallValid = isOverallValid;
            ghost.isIndividuallyBlocked = isCellBlocked;

            ApplyColorsToGhost(ghost, 0f);
        }
    }

    public void HidePreview()
    {
        for (int i = 0; i < activeGhostCells.Count; i++)
        {
            if (activeGhostCells[i].rootObj != null)
            {
                activeGhostCells[i].rootObj.SetActive(false);
            }
        }
    }

    private void ApplyColorsToGhost(GhostCellVisual ghost, float pulseOffset)
    {
        if (ghost.isOverallValid)
        {
            Color fillCol = ghost.baseBiomeColor;
            fillCol.a = Mathf.Clamp01(validFillAlpha + pulseOffset);
            ghost.fillRenderer.color = fillCol;

            Color frameCol = validBorderColor;
            frameCol.a = Mathf.Clamp01(validBorderColor.a + pulseOffset * 0.5f);
            ghost.frameRenderer.color = frameCol;
        }
        else
        {
            // Biom-Farbe entsättigen, damit man das Biom noch erkennt, aber sofort sieht, dass es inaktiv ist
            float lum = ghost.baseBiomeColor.r * 0.299f + ghost.baseBiomeColor.g * 0.587f + ghost.baseBiomeColor.b * 0.114f;
            Color greyCol = new Color(lum, lum, lum, 1f);
            Color desaturatedFill = Color.Lerp(ghost.baseBiomeColor, greyCol, invalidDesaturation);

            desaturatedFill.a = Mathf.Clamp01(invalidFillAlpha + pulseOffset * 0.4f);
            ghost.fillRenderer.color = desaturatedFill;

            ghost.frameRenderer.color = ghost.isIndividuallyBlocked ? blockedCellCrossColor : invalidCrossColor;
        }
    }

    private void EnsurePoolSize(int requiredCount)
    {
        Transform parentTransform = gridManager != null ? gridManager.transform : transform;

        while (activeGhostCells.Count < requiredCount)
        {
            int idx = activeGhostCells.Count;
            GameObject root = new GameObject($"PlacementGhostCell_{idx}");
            root.transform.SetParent(parentTransform, false);

            GameObject fillObj = new GameObject("BiomeFill");
            fillObj.transform.SetParent(root.transform, false);
            SpriteRenderer fillSr = fillObj.AddComponent<SpriteRenderer>();
            fillSr.sprite = fillSprite;

            GameObject frameObj = new GameObject("FrameAndStatus");
            frameObj.transform.SetParent(root.transform, false);
            SpriteRenderer frameSr = frameObj.AddComponent<SpriteRenderer>();
            frameSr.sprite = validFrameSprite;

            activeGhostCells.Add(new GhostCellVisual
            {
                rootObj = root,
                fillRenderer = fillSr,
                frameRenderer = frameSr
            });
        }
    }

    private static void EnsureProceduralSprites()
    {
        if (fillSprite != null && validFrameSprite != null && invalidCrossFrameSprite != null) return;

        const int size = 64;

        // 1. Innere Biom-Füllung (mit leichtem Randabstand für den Rahmen)
        Texture2D fillTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        fillTex.filterMode = FilterMode.Point;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = (x >= 3 && x < size - 3 && y >= 3 && y < size - 3);
                fillTex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }
        fillTex.Apply();
        fillSprite = Sprite.Create(fillTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        // 2. Gültiger Rahmen (Durchgehender klarer Rand)
        Texture2D validTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        validTex.filterMode = FilterMode.Point;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = (x < 4 || x >= size - 4 || y < 4 || y >= size - 4);
                validTex.SetPixel(x, y, isBorder ? Color.white : Color.clear);
            }
        }
        validTex.Apply();
        validFrameSprite = Sprite.Create(validTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

        // 3. Ungültiger Rahmen (Gestrichelter Rand + Diagonales X-Kreuz in der Mitte)
        Texture2D invalidTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        invalidTex.filterMode = FilterMode.Point;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = (x < 3 || x >= size - 3 || y < 3 || y >= size - 3);
                bool dashPattern = ((x + y) / 6) % 2 == 0;

                bool inCrossBounds = (x >= 14 && x < size - 14 && y >= 14 && y < size - 14);
                bool isDiag1 = Mathf.Abs(x - y) <= 2;
                bool isDiag2 = Mathf.Abs(x - (size - 1 - y)) <= 2;

                bool drawPixel = (isBorder && dashPattern) || (inCrossBounds && (isDiag1 || isDiag2));
                invalidTex.SetPixel(x, y, drawPixel ? Color.white : Color.clear);
            }
        }
        invalidTex.Apply();
        invalidCrossFrameSprite = Sprite.Create(invalidTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
using System.Collections.Generic;
using UnityEngine;

public class AltarTeleportVisualizer : MonoBehaviour
{
    [Header("Farben auf dem Macro-Grid")]
    [SerializeField] private Color sourceMarkerColor = new Color(0.5f, 0.9f, 1f, 0.75f);
    [SerializeField] private Color availableTargetColor = new Color(0.2f, 0.95f, 0.85f, 0.85f);
    [SerializeField] private Color selectedTargetColor = new Color(1f, 0.9f, 0.25f, 1f);
    [SerializeField] private Color lineColor = new Color(0.35f, 0.98f, 1f, 0.7f);

    private readonly List<GameObject> spawnedMarkers = new List<GameObject>();
    private readonly List<SpriteRenderer> targetRenderers = new List<SpriteRenderer>();
    private LineRenderer connectionLine;
    private static Sprite ringSprite;

    private bool isVisible = false;
    private Vector2Int sourceCoord;
    private List<Vector2Int> targets = new List<Vector2Int>();
    private int selectedIdx = 0;
    private int tickCost = 1;
    private bool hubBlocked = false;

    public void ShowSelection(Vector2Int fromCoord, List<Vector2Int> reachableTargets, int initialIndex, int cost, bool isHubBlocked)
    {
        ClearMarkers();

        sourceCoord = fromCoord;
        targets = new List<Vector2Int>(reachableTargets);
        selectedIdx = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, targets.Count - 1));
        tickCost = cost;
        hubBlocked = isHubBlocked;
        isVisible = true;

        if (GridManager.Instance == null) return;

        // Quell-Altar markieren
        SpawnMarker(sourceCoord, sourceMarkerColor, 1.05f, addToTargetList: false);

        // Alle erreichbaren Ziele markieren
        for (int i = 0; i < targets.Count; i++)
        {
            bool isSelected = (i == selectedIdx);
            SpawnMarker(targets[i], isSelected ? selectedTargetColor : availableTargetColor, isSelected ? 1.35f : 1.0f, addToTargetList: true);
        }

        EnsureConnectionLine();
        UpdateConnectionLine();
    }

    public void UpdateSelectedIndex(int newIndex)
    {
        selectedIdx = Mathf.Clamp(newIndex, 0, Mathf.Max(0, targets.Count - 1));

        for (int i = 0; i < targetRenderers.Count; i++)
        {
            if (targetRenderers[i] == null) continue;
            bool isSelected = (i == selectedIdx);
            targetRenderers[i].color = isSelected ? selectedTargetColor : availableTargetColor;
            targetRenderers[i].transform.localScale = Vector3.one * (isSelected ? 1.35f : 1.0f);
        }

        UpdateConnectionLine();
    }

    public void HideSelection()
    {
        isVisible = false;
        ClearMarkers();
        if (connectionLine != null) connectionLine.enabled = false;
    }

    private void Update()
    {
        if (!isVisible || targetRenderers.Count == 0) return;

        if (selectedIdx >= 0 && selectedIdx < targetRenderers.Count && targetRenderers[selectedIdx] != null)
        {
            float pulse = 1.25f + Mathf.Sin(Time.unscaledTime * 6f) * 0.14f;
            targetRenderers[selectedIdx].transform.localScale = Vector3.one * pulse;
        }
    }

    private void SpawnMarker(Vector2Int coord, Color color, float scale, bool addToTargetList)
    {
        GameObject marker = new GameObject($"TeleportMarker_{coord.x}_{coord.y}");
        marker.transform.SetParent(GridManager.Instance.transform, false);

        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        marker.transform.localPosition = new Vector3(coord.x * offset, coord.y * offset, -0.8f);
        marker.transform.localScale = Vector3.one * scale;

        SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
        sr.sprite = GetRingSprite();
        sr.color = color;
        sr.sortingOrder = 30;

        spawnedMarkers.Add(marker);
        if (addToTargetList) targetRenderers.Add(sr);
    }

    private void EnsureConnectionLine()
    {
        if (connectionLine == null)
        {
            GameObject lineObj = new GameObject("AltarTeleportLine");
            lineObj.transform.SetParent(GridManager.Instance.transform, false);
            connectionLine = lineObj.AddComponent<LineRenderer>();
            connectionLine.useWorldSpace = false;
            connectionLine.positionCount = 2;
            connectionLine.startWidth = 0.12f;
            connectionLine.endWidth = 0.12f;
            connectionLine.sortingOrder = 25;
            connectionLine.material = new Material(Shader.Find("Sprites/Default"));
            connectionLine.startColor = lineColor;
            connectionLine.endColor = selectedTargetColor;
        }
        connectionLine.enabled = true;
    }

    private void UpdateConnectionLine()
    {
        if (connectionLine == null || targets.Count == 0 || GridManager.Instance == null) return;

        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        Vector2Int target = targets[selectedIdx];

        connectionLine.SetPosition(0, new Vector3(sourceCoord.x * offset, sourceCoord.y * offset, -0.7f));
        connectionLine.SetPosition(1, new Vector3(target.x * offset, target.y * offset, -0.7f));
    }

    private void ClearMarkers()
    {
        for (int i = 0; i < spawnedMarkers.Count; i++)
        {
            if (spawnedMarkers[i] != null) Destroy(spawnedMarkers[i]);
        }
        spawnedMarkers.Clear();
        targetRenderers.Clear();
    }

    private void OnGUI()
    {
        if (!isVisible || targets.Count == 0) return;

        Vector2Int target = targets[selectedIdx];
        bool isHub = MacroGridManager.Instance != null && target == MacroGridManager.Instance.StartCoordinate;
        string targetLabel = isHub ? $"START-HUB [{target.x},{target.y}]" : $"ALTAR [{target.x},{target.y}]";
        string bossWarn = hubBlocked ? "  |  Hub durch Boss blockiert!" : "";

        string info = $"ALTAR-TELEPORT  |  Ziel: {targetLabel} ({selectedIdx + 1}/{targets.Count})  |  Kosten: {tickCost} Tick(s){bossWarn}\n" +
                      "[A / Enter / Klick] Teleportieren   |   [Stick / A-D] Ziel wechseln   |   [B / Esc] Abbrechen";

        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.white;

        float width = Mathf.Min(Screen.width - 40f, 760f);
        float height = 56f;
        GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height - height - 24f, width, height), info, style);
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite != null) return ringSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                bool isRing = (dist >= 22f && dist <= 29f);
                tex.SetPixel(x, y, isRing ? Color.white : Color.clear);
            }
        }
        tex.Apply();

        ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 58f);
        return ringSprite;
    }
}
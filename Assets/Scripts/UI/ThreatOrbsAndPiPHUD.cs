using System.Collections.Generic;
using UnityEngine;

public class ThreatOrbsAndPiPHUD : MonoBehaviour
{
    [Header("HUD-Elemente Ein-/Ausschalten (Inspector)")]
    public bool showTopLeftResonanceAndNightmare = true;
    public bool showTopRightThreatOrbs = true;
    public bool showSmallCostBadgeOnSlots = true;

    [Header("Optionale Custom-Sprites")]
    public Texture2D customOrbFrameTexture;
    public Texture2D customDefaultActionIcon;

    private GameManager gameManager;
    private ThreatTrackerManager tracker;
    private BiomeResonanceManager resonanceManager;
    private InventoryManager inventoryManager;

    private static Texture2D circleTex;
    private Camera pipCamera;
    private RenderTexture pipRenderTexture;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
        tracker = GetComponent<ThreatTrackerManager>();
        resonanceManager = GetComponent<BiomeResonanceManager>();
        EnsureCircleTexture();
    }

    private void OnDestroy()
    {
        if (pipRenderTexture != null)
        {
            pipRenderTexture.Release();
            Destroy(pipRenderTexture);
        }
        if (pipCamera != null)
        {
            Destroy(pipCamera.gameObject);
        }
    }

    private void Update()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (tracker == null) tracker = ThreatTrackerManager.Instance;
        if (resonanceManager == null) resonanceManager = BiomeResonanceManager.Instance;

        UpdatePiPCameraPosition();
    }

    private void EnsurePiPCameraReady()
    {
        if (pipRenderTexture == null)
        {
            pipRenderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            pipRenderTexture.Create();
        }

        if (pipCamera == null && GridManager.Instance != null)
        {
            GameObject camObj = new GameObject("PiP_LiveGridCamera");
            camObj.transform.SetParent(GridManager.Instance.transform, false);

            pipCamera = camObj.AddComponent<Camera>();
            pipCamera.orthographic = true;
            pipCamera.orthographicSize = 4.2f;
            pipCamera.clearFlags = CameraClearFlags.SolidColor;
            pipCamera.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            pipCamera.targetTexture = pipRenderTexture;
            pipCamera.depth = -5;
            pipCamera.enabled = false;
        }
    }

    private void UpdatePiPCameraPosition()
    {
        if (tracker == null || GridManager.Instance == null) return;

        bool shouldRenderPiP = tracker.PipActiveTimer > 0.01f || tracker.SelectedOrb != null;
        if (!shouldRenderPiP)
        {
            if (pipCamera != null && pipCamera.enabled) pipCamera.enabled = false;
            return;
        }

        EnsurePiPCameraReady();
        if (pipCamera == null) return;

        pipCamera.enabled = true;

        Vector2Int targetCoord = tracker.PipActiveTimer > 0.01f
            ? tracker.PipFocusCoord
            : tracker.SelectedOrb.targetGridCoord;

        float offset = GridManager.Instance.offset > 0.01f ? GridManager.Instance.offset : 1.1f;
        Vector3 desiredLocalPos = new Vector3(targetCoord.x * offset, targetCoord.y * offset, -10f);

        pipCamera.transform.localPosition = Vector3.Lerp(pipCamera.transform.localPosition, desiredLocalPos, Time.deltaTime * 12f);
    }

    private void OnGUI()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (tracker == null) tracker = ThreatTrackerManager.Instance;
        if (resonanceManager == null) resonanceManager = BiomeResonanceManager.Instance;
        if (gameManager == null || !gameManager.ShowFallbackHUD) return;

        if (inventoryManager == null) inventoryManager = gameManager.inventoryManager;

        Color prevColor = GUI.color;

        if (showTopLeftResonanceAndNightmare)
        {
            DrawTopLeftResonanceAndNightmare();
        }

        if (showTopRightThreatOrbs && tracker != null)
        {
            DrawTopRightThreatOrbsAndPiP();
        }

        if (showSmallCostBadgeOnSlots && gameManager.currentState == GameState.MacroPlan && inventoryManager != null)
        {
            DrawMinimalSlotCostBadges();
        }

        GUI.color = prevColor;
    }

    private void DrawTopLeftResonanceAndNightmare()
    {
        float panelX = 16f;
        float panelY = 16f;
        float panelWidth = 320f;

        List<BiomeWeightEntry> visibleBiomes = new List<BiomeWeightEntry>();
        if (resonanceManager != null && resonanceManager.resonances != null)
        {
            for (int i = 0; i < resonanceManager.resonances.Count; i++)
            {
                if (resonanceManager.resonances[i].percentage > 0)
                {
                    visibleBiomes.Add(resonanceManager.resonances[i]);
                }
            }
        }

        int biomeCount = visibleBiomes.Count;
        float resonanceSectionHeight = biomeCount > 0 ? 32f : 0f;
        float totalHeight = resonanceSectionHeight + 36f;

        GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        GUI.Box(new Rect(panelX, panelY, panelWidth, totalHeight), GUIContent.none);

        float currentY = panelY + 6f;

        if (biomeCount > 0)
        {
            EnsureCircleTexture();
            float slotWidth = (panelWidth - 16f) / biomeCount;

            GUIStyle resStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            for (int i = 0; i < biomeCount; i++)
            {
                BiomeWeightEntry entry = visibleBiomes[i];
                float itemX = panelX + 8f + i * slotWidth;

                Color biomeCol = GridManager.Instance != null ? GridManager.Instance.GetZoneColor(entry.zoneType) : Color.green;
                GUI.color = biomeCol;
                GUI.DrawTexture(new Rect(itemX, currentY + 4f, 16f, 16f), circleTex);

                GUI.color = Color.white;
                string text = $"{entry.displayName} {entry.percentage}%";
                if (entry.builtTilesOnGrid > 0)
                {
                    text += $" [{entry.builtTilesOnGrid}]";
                }
                GUI.Label(new Rect(itemX + 20f, currentY, slotWidth - 20f, 24f), text, resStyle);
            }

            currentY += 28f;
        }

        Rect barBgRect = new Rect(panelX + 8f, currentY, panelWidth - 16f, 22f);
        GUI.color = new Color(0.14f, 0.05f, 0.20f, 0.98f);
        GUI.Box(barBgRect, GUIContent.none);

        float fillWidth = (barBgRect.width - 4f) * gameManager.NightmareRatio;
        if (fillWidth > 1f)
        {
            GUI.color = Color.Lerp(new Color(0.55f, 0.15f, 0.85f), new Color(0.95f, 0.15f, 0.25f), gameManager.NightmareRatio);
            GUI.Box(new Rect(barBgRect.x + 2f, barBgRect.y + 2f, fillWidth, barBgRect.height - 4f), GUIContent.none);
        }

        GUI.color = Color.white;
        GUIStyle barLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 11
        };
        GUI.Label(barBgRect, $"Albtraum: {gameManager.CurrentNightmare:0}%", barLabelStyle);
    }

    private void DrawTopRightThreatOrbsAndPiP()
    {
        IReadOnlyList<ThreatOrbEntry> orbs = tracker.ActiveOrbs;
        if (orbs == null || orbs.Count == 0) return;

        EnsureCircleTexture();

        float orbSize = 62f;
        float spacing = 12f;
        float rightMargin = 20f;
        float topY = 16f;

        float totalWidth = orbs.Count * orbSize + (orbs.Count - 1) * spacing;
        float startX = Screen.width - totalWidth - rightMargin;

        for (int i = 0; i < orbs.Count; i++)
        {
            ThreatOrbEntry orb = orbs[i];
            bool isSelected = (tracker.SelectedOrbIndex == i);

            float scaleBoost = orb.pulseAnimTimer * 8f + (isSelected ? 6f : 0f);
            float currentSize = orbSize + scaleBoost;

            float cx = startX + i * (orbSize + spacing) - scaleBoost * 0.5f;
            float cy = topY - scaleBoost * 0.5f;
            Rect orbRect = new Rect(cx, cy, currentSize, currentSize);

            if (isSelected)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(orbRect.x - 4f, orbRect.y - 4f, orbRect.width + 8f, orbRect.height + 8f), circleTex);
            }

            GUI.color = orb.orbColor;
            GUI.DrawTexture(new Rect(orbRect.x + 4f, orbRect.y + 4f, orbRect.width - 8f, orbRect.height - 8f), circleTex);

            if (customOrbFrameTexture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(orbRect, customOrbFrameTexture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.75f);
                GUI.DrawTexture(new Rect(orbRect.x + 7f, orbRect.y + 7f, orbRect.width - 14f, orbRect.height - 14f), circleTex);
            }

            if (customDefaultActionIcon != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(orbRect.x + 14f, orbRect.y + 10f, orbRect.width - 28f, orbRect.height - 28f), customDefaultActionIcon, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.color = Color.white;
                GUIStyle iconStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    alignment = TextAnchor.UpperCenter,
                    fontStyle = FontStyle.Bold
                };
                GUI.Label(new Rect(orbRect.x, orbRect.y + 6f, orbRect.width, 26f), orb.iconSymbol, iconStyle);
            }

            GUIStyle countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.LowerCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.color = orb.countdown <= 1 ? new Color(1f, 0.9f, 0.2f) : Color.white;
            GUI.Label(new Rect(orbRect.x, orbRect.y + 22f, orbRect.width, 34f), orb.countdown.ToString(), countStyle);
        }

        float alpha = tracker.CurrentPipAlpha;
        ThreatOrbEntry inspectedOrb = tracker.SelectedOrb;

        if (alpha > 0.01f)
        {
            bool isAutoTrigger = tracker.PipActiveTimer > 0.01f;
            float frameWidth = 216f;
            float frameHeight = 206f;
            float frameX = Screen.width - frameWidth - rightMargin;
            float frameY = topY + orbSize + 14f;

            Color baseBorderColor = isAutoTrigger ? tracker.PipFrameColor : (inspectedOrb != null ? inspectedOrb.orbColor : Color.red);
            string headerText = isAutoTrigger
                ? tracker.PipEventTitle
                : (inspectedOrb != null ? $"{inspectedOrb.title} (in {inspectedOrb.countdown})" : "");

            GUI.color = new Color(baseBorderColor.r, baseBorderColor.g, baseBorderColor.b, alpha);
            GUI.Box(new Rect(frameX - 2f, frameY - 2f, frameWidth + 4f, frameHeight + 4f), GUIContent.none);

            GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.95f * alpha);
            GUI.Box(new Rect(frameX, frameY, frameWidth, frameHeight), GUIContent.none);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(frameX + 4f, frameY + 4f, frameWidth - 8f, 20f), headerText, headerStyle);

            if (pipRenderTexture != null)
            {
                Rect texRect = new Rect(frameX + 10f, frameY + 26f, frameWidth - 20f, 170f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(texRect, pipRenderTexture, ScaleMode.ScaleToFit, false);
            }
        }
    }

    private void DrawMinimalSlotCostBadges()
    {
        if (inventoryManager.uiSlotTransforms == null) return;

        EnsureCircleTexture();

        GUIStyle badgeTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        for (int i = 0; i < inventoryManager.uiSlotTransforms.Length; i++)
        {
            if (i >= inventoryManager.hotbarSlots.Length) break;
            ShapeData card = inventoryManager.hotbarSlots[i];
            RectTransform slotRect = inventoryManager.uiSlotTransforms[i];

            if (card == null || slotRect == null || !slotRect.gameObject.activeInHierarchy) continue;

            Vector3 screenPos = slotRect.position;
            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y + 34f;

            float badgeSize = 22f;
            Rect badgeRect = new Rect(guiX - badgeSize * 0.5f, guiY - badgeSize * 0.5f, badgeSize, badgeSize);

            bool isSelected = (i == inventoryManager.CurrentSelectedIndex);
            GUI.color = isSelected ? new Color(0.2f, 0.75f, 0.95f, 0.95f) : new Color(0.18f, 0.2f, 0.25f, 0.9f);
            GUI.DrawTexture(badgeRect, circleTex);

            GUI.color = Color.white;
            GUI.Label(badgeRect, card.tickCost.ToString(), badgeTextStyle);
        }
    }

    private static void EnsureCircleTexture()
    {
        if (circleTex != null) return;

        int size = 64;
        circleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        circleTex.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.48f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01((radius - dist) * 1.5f);
                circleTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        circleTex.Apply();
    }
}
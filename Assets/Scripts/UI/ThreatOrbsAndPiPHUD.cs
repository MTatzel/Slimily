using System.Collections.Generic;
using UnityEngine;

public class ThreatOrbsAndPiPHUD : MonoBehaviour
{
    private GameManager gameManager;
    private ThreatTrackerManager tracker;
    private InventoryManager inventoryManager;

    // Prozedurale Kreis-Textur für die runden Symbole oben rechts
    private static Texture2D circleTex;

    // Live-Picture-in-Picture (PiP) Kamera & RenderTexture für den Grid-Ausschnitt
    private Camera pipCamera;
    private RenderTexture pipRenderTexture;

    // Sternschnuppen-Animation beim Ziehen neuer Erinnerungen
    private float shootingStarTimer = 0f;
    private int lastHandCount = 0;

    private void Awake()
    {
        gameManager = GetComponent<GameManager>();
        tracker = GetComponent<ThreatTrackerManager>();
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

        if (gameManager != null && gameManager.inventoryManager != null)
        {
            int currentHand = gameManager.inventoryManager.HandCount;
            if (currentHand > lastHandCount)
            {
                shootingStarTimer = 0.65f;
            }
            lastHandCount = currentHand;
        }

        if (shootingStarTimer > 0f)
        {
            shootingStarTimer -= Time.deltaTime;
        }

        UpdatePiPCameraPosition();
    }

    /// <summary>
    /// Erzeugt automatisch eine kleine Live-Kamera über dem Makro-Grid,
    /// die immer genau den Ort des ausgelösten (oder per R2 inspizierten) Symbols filmt!
    /// </summary>
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
            pipCamera.orthographicSize = 4.2f; // Schöner Nah-Ausschnitt (ca. 7x7 Grid-Felder)
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

        // Zielkoordinate bestimmen: Entweder das gerade bei 0 ausgelöste Event oder das per R2 gewählte Symbol
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
        if (gameManager == null || !gameManager.ShowFallbackHUD) return;

        if (inventoryManager == null) inventoryManager = gameManager.inventoryManager;

        Color prevColor = GUI.color;

        // 1. OBEN LINKS: Kompakte Albtraumleiste & Raum-Warnung
        DrawTopLeftNightmareAndStatus();

        // 2. OBEN RECHTS: Die runden, farbcodierten Gefahren-Symbole + R2-Inspektor + Live-PiP-Fenster
        if (tracker != null)
        {
            DrawTopRightThreatOrbsAndPiP();
        }

        // 3. UNTEN (im Baumodus): Sternschnuppen-Erinnerungen
        if (gameManager.currentState == GameState.MacroPlan && inventoryManager != null)
        {
            DrawBottomMemoryStreamBar();
        }

        if (shootingStarTimer > 0f)
        {
            DrawShootingStarFlight();
        }

        GUI.color = prevColor;
    }

    // =====================================================================
    // 1. OBEN RECHTS: RUNDE GEFAHREN-SYMBOLE + R2-INFO + LIVE-GRID-FENSTER
    // =====================================================================

    private void DrawTopRightThreatOrbsAndPiP()
    {
        IReadOnlyList<ThreatOrbEntry> orbs = tracker.ActiveOrbs;
        if (orbs == null || orbs.Count == 0) return;

        EnsureCircleTexture();

        float orbSize = 66f;
        float spacing = 16f;
        float rightMargin = 22f;
        float topY = 20f;

        float totalWidth = orbs.Count * orbSize + (orbs.Count - 1) * spacing;
        float startX = Screen.width - totalWidth - rightMargin;

        // Dezente Hint-Zeile über/neben den Symbolen für R2
        GUI.color = new Color(0.8f, 0.88f, 1f, 0.9f);
        GUIStyle r2HintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        string r2Text = tracker.SelectedOrbIndex >= 0
            ? "[R2 / Tab]: Nächstes Symbol  (Auswahl aktiv)"
            : "[R2 / Tab]: Symbole inspizieren";
        GUI.Label(new Rect(startX - 110f, topY + orbSize + 4f, totalWidth + 110f, 20f), r2Text, r2HintStyle);

        for (int i = 0; i < orbs.Count; i++)
        {
            ThreatOrbEntry orb = orbs[i];
            bool isSelected = (tracker.SelectedOrbIndex == i);

            // Scale-Bounce bei Zähler-Tick oder R2-Selektion
            float scaleBoost = orb.pulseAnimTimer * 10f + (isSelected ? 8f : 0f);
            float currentSize = orbSize + scaleBoost;

            float cx = startX + i * (orbSize + spacing) - scaleBoost * 0.5f;
            float cy = topY - scaleBoost * 0.5f;
            Rect orbRect = new Rect(cx, cy, currentSize, currentSize);

            // Äußerer Selektions- oder Warnring (falls Countdown == 1 oder per R2 selektiert)
            if (isSelected || orb.countdown <= 1)
            {
                float ringPulse = isSelected ? 1f : (Mathf.PingPong(Time.time * 5f, 0.5f) + 0.5f);
                GUI.color = isSelected ? Color.white : new Color(1f, 0.9f, 0.2f, ringPulse);
                GUI.DrawTexture(new Rect(orbRect.x - 5f, orbRect.y - 5f, orbRect.width + 10f, orbRect.height + 10f), circleTex);
            }

            // Dunkler Schatten-Kreis dahinter
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(orbRect.x - 2f, orbRect.y - 2f, orbRect.width + 4f, orbRect.height + 4f), circleTex);

            // Farbiger Hauptkreis (entspricht exakt der Farbe auf dem Grid!)
            GUI.color = orb.orbColor;
            GUI.DrawTexture(orbRect, circleTex);

            // Innerer dunklerer Kern für perfekten Text-Kontrast
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.72f);
            GUI.DrawTexture(new Rect(orbRect.x + 7f, orbRect.y + 7f, orbRect.width - 14f, orbRect.height - 14f), circleTex);

            // Oben im Kreis: Das Symbol (💀 / 🕷️ / 🌫️)
            GUI.color = Color.white;
            GUIStyle iconStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(orbRect.x, orbRect.y + 7f, orbRect.width, 28f), orb.iconSymbol, iconStyle);

            // Unten im Kreis: Die große Countdown-Zahl
            GUIStyle countStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.LowerCenter,
                fontStyle = FontStyle.Bold
            };
            GUI.color = orb.countdown <= 1 ? new Color(1f, 0.95f, 0.3f) : Color.white;
            GUI.Label(new Rect(orbRect.x, orbRect.y + 24f, orbRect.width, 34f), orb.countdown.ToString(), countStyle);
        }

        // =========================================================
        // R2-INSPEKTOR ODER LIVE-PIP-AUSSCHNITT BEI COUNTDOWN = 0
        // =========================================================
        bool showPiPTrigger = tracker.PipActiveTimer > 0.01f;
        ThreatOrbEntry inspectedOrb = tracker.SelectedOrb;

        if (showPiPTrigger || inspectedOrb != null)
        {
            float frameWidth = 240f;
            float frameHeight = 250f;
            float frameX = Screen.width - frameWidth - rightMargin;
            float frameY = topY + orbSize + 28f;

            Color borderColor = showPiPTrigger ? tracker.PipFrameColor : inspectedOrb.orbColor;
            string headerText = showPiPTrigger
                ? tracker.PipEventTitle
                : $"{inspectedOrb.iconSymbol} {inspectedOrb.title} (in {inspectedOrb.countdown})";

            string detailText = showPiPTrigger
                ? $"Live-Übertragung vom Grid [{tracker.PipFocusCoord.x}, {tracker.PipFocusCoord.y}]"
                : $"{inspectedOrb.description}\nZiel-Sektor: [{inspectedOrb.targetGridCoord.x}, {inspectedOrb.targetGridCoord.y}]";

            // Farbiger Außenrahmen (passend zur Symbolfarbe)
            GUI.color = borderColor;
            GUI.Box(new Rect(frameX - 3f, frameY - 3f, frameWidth + 6f, frameHeight + 6f), GUIContent.none);

            // Dunkler Hintergrund des Frames
            GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.96f);
            GUI.Box(new Rect(frameX, frameY, frameWidth, frameHeight), GUIContent.none);

            // Titel-Balken
            GUI.color = borderColor;
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(frameX + 6f, frameY + 4f, frameWidth - 12f, 22f), headerText, headerStyle);

            // LIVE-KAMERA-AUSSCHNITT VOM GRID (RenderTexture)
            if (pipRenderTexture != null)
            {
                Rect texRect = new Rect(frameX + 12f, frameY + 28f, frameWidth - 24f, 156f);
                GUI.color = Color.white;
                GUI.DrawTexture(texRect, pipRenderTexture, ScaleMode.ScaleToFit, false);

                // Fadenkreuz-Markierung in der Mitte des Kamera-Ausschnitts
                GUI.color = new Color(borderColor.r, borderColor.g, borderColor.b, 0.85f);
                GUIStyle crossStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                GUI.Label(texRect, "⌖", crossStyle);
            }

            // Erklärtext unter dem Kamera-Ausschnitt
            GUI.color = new Color(0.9f, 0.92f, 0.96f);
            GUIStyle descStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            GUI.Label(new Rect(frameX + 10f, frameY + 188f, frameWidth - 20f, 58f), detailText, descStyle);
        }
    }

    // =====================================================================
    // 2. OBEN LINKS: ALBTRAUMLEISTE & RAUM-WARNUNG
    // =====================================================================

    private void DrawTopLeftNightmareAndStatus()
    {
        bool currentRoomInDanger;
        string threatWarning = BuildRoomThreatWarning(out currentRoomInDanger);

        GUI.color = new Color(0.07f, 0.07f, 0.11f, 0.94f);
        GUI.Box(new Rect(16f, 16f, 440f, 78f), GUIContent.none);

        GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        string roomInfo = "Raum: Hub";
        if (MacroGridManager.Instance != null)
        {
            Vector2Int c = MacroGridManager.Instance.CurrentCoordinate;
            roomInfo = $"Raum [{c.x},{c.y}] (Erschlossen: {MacroGridManager.Instance.VisitedRoomCount})  |  [E/R3]: Neue Hand (-{gameManager.MulliganTickCost})";
        }

        GUI.color = new Color(0.8f, 0.92f, 1f);
        GUI.Label(new Rect(26f, 20f, 420f, 22f), roomInfo, infoStyle);

        // Albtraum-Füllbalken
        Rect barBgRect = new Rect(26f, 46f, 420f, 22f);
        GUI.color = new Color(0.14f, 0.05f, 0.2f, 0.98f);
        GUI.Box(barBgRect, GUIContent.none);

        float fillWidth = 416f * gameManager.NightmareRatio;
        if (fillWidth > 1f)
        {
            GUI.color = Color.Lerp(new Color(0.55f, 0.15f, 0.85f), new Color(0.95f, 0.1f, 0.25f), gameManager.NightmareRatio);
            GUI.Box(new Rect(28f, 48f, fillWidth, 18f), GUIContent.none);
        }

        GUI.color = Color.white;
        GUIStyle barLabel = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = 11
        };
        GUI.Label(barBgRect, $"ALBTRAUM: {gameManager.CurrentNightmare:0}% / {gameManager.MaxNightmare:0}%", barLabel);

        if (!string.IsNullOrEmpty(threatWarning))
        {
            GUI.color = currentRoomInDanger ? new Color(0.78f, 0.08f, 0.08f, 0.95f) : new Color(0.55f, 0.22f, 0.05f, 0.95f);
            Rect warnRect = new Rect(16f, 100f, 440f, 30f);
            GUI.Box(warnRect, GUIContent.none);

            GUI.color = Color.white;
            GUI.Label(warnRect, threatWarning, barLabel);
        }
    }

    // =====================================================================
    // 3. UNTEN: ERINNERUNGS-STROM & STERNSCHNUPPEN
    // =====================================================================

    private void DrawBottomMemoryStreamBar()
    {
        ShapeData selected = inventoryManager.CurrentlySelectedCard;

        float boxWidth = 760f;
        float boxHeight = 62f;
        float boxX = (Screen.width - boxWidth) * 0.5f;
        float boxY = Screen.height - 235f;

        GUI.color = new Color(0.06f, 0.09f, 0.15f, 0.94f);
        Rect infoRect = new Rect(boxX, boxY, boxWidth, boxHeight);
        GUI.Box(infoRect, GUIContent.none);

        GUIStyle poolStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        GUI.color = new Color(0.45f, 0.85f, 1f);
        GUI.Label(new Rect(boxX + 10f, boxY + 6f, 160f, 50f), $"☄️ ERINNERUNGS-QUELLE\n[{inventoryManager.DrawPileCount}] Sternschnuppen", poolStyle);

        GUI.color = new Color(0.75f, 0.5f, 0.95f);
        GUI.Label(new Rect(boxX + boxWidth - 170f, boxY + 6f, 160f, 50f), $"🌀 VERGESSENS-STRUDEL\n[{inventoryManager.DiscardPileCount}] Verblasst", poolStyle);

        if (selected != null)
        {
            GUI.color = selected.category == CardCategory.TacticalSkill
                ? new Color(1f, 0.85f, 0.35f)
                : new Color(0.55f, 1f, 0.8f);

            GUIStyle centerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            string costLabel = selected.tickCost == 0
                ? "0 Zähler (Gratis)"
                : $"-{selected.tickCost} Zähler oben rechts";

            GUI.Label(
                new Rect(boxX + 175f, boxY + 6f, boxWidth - 350f, 50f),
                $"✦ {selected.shapeName} ({costLabel})\n{selected.description}",
                centerStyle
            );
        }
    }

    private void DrawShootingStarFlight()
    {
        float t = 1f - (shootingStarTimer / 0.65f);
        float startX = 80f;
        float targetX = Screen.width * 0.5f;
        float currentX = Mathf.Lerp(startX, targetX, Mathf.SmoothStep(0f, 1f, t));
        float currentY = Screen.height - 150f - Mathf.Sin(t * Mathf.PI) * 55f;

        GUI.color = new Color(0.5f, 0.95f, 1f, 1f - t * 0.5f);
        GUIStyle starStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        GUI.Label(new Rect(currentX - 110f, currentY, 220f, 36f), "━══✦☄️ +1 ERINNERUNG", starStyle);
    }

    private string BuildRoomThreatWarning(out bool currentRoomInDanger)
    {
        currentRoomInDanger = false;
        if (MacroGridManager.Instance == null) return "";

        Vector2Int current = MacroGridManager.Instance.CurrentCoordinate;
        currentRoomInDanger = MacroGridManager.Instance.IsCellTargetedByBoss(current);

        List<string> dirs = new List<string>();
        if (MacroGridManager.Instance.IsCellTargetedByBoss(current + Vector2Int.up)) dirs.Add("NORD");
        if (MacroGridManager.Instance.IsCellTargetedByBoss(current + Vector2Int.right)) dirs.Add("OST");
        if (MacroGridManager.Instance.IsCellTargetedByBoss(current + Vector2Int.down)) dirs.Add("SÜD");
        if (MacroGridManager.Instance.IsCellTargetedByBoss(current + Vector2Int.left)) dirs.Add("WEST");

        if (currentRoomInDanger)
        {
            int ticks = tracker != null ? tracker.GetBossOrbCountdown() : 1;
            return $"⚠️ AKUTE GEFAHR: DIESER RAUM WIRD IN {ticks} TICKS GETROFFEN!";
        }

        if (dirs.Count > 0)
        {
            return $"🔥 TÜR GLÜHT ROT [{string.Join(", ", dirs)}] -> Nachbarraum ist markiert!";
        }

        return "";
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
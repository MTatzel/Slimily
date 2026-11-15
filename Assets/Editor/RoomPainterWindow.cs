#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class RoomJsonData
{
    public string roomName = "Room_New";
    public string biome = "Forest";
    public int width = 30;
    public int height = 20;
    public int doorGap = 6;
    public List<string> layout = new List<string>();
}

public class RoomPainterWindow : EditorWindow
{
    private enum BrushType
    {
        Wall = '#',
        Empty = '.',
        Hazard = '^',
        NormalCube = 'C',
        SecretCube = 'X'
    }

    // --- Raum-Einstellungen ---
    private string roomName = "Room_Forest_01";
    private string biomeName = "Forest";
    private int gridWidth = 30;
    private int gridHeight = 20;
    private int doorGapSize = 6;

    // --- Vorlagen & Tiles ---
    private Room templateRoomPrefab;
    private TileBase groundTile;
    private TileBase hazardTile;
    private string groundLayerName = "Ground";
    private string hazardTagName = "Hazard";

    // --- Zeichen-Status ---
    private char[,] cells;
    private BrushType currentBrush = BrushType.Wall;
    private bool useRectTool = false;
    private Vector2Int? rectStartCell = null;

    // --- UI Scroll & JSON ---
    private Vector2 canvasScroll;
    private Vector2 mainScroll;
    private bool showJsonSection = false;
    private string jsonBuffer = "";
    private float cellPixelSize = 22f;

    [MenuItem("Tools/Slimily Room Painter")]
    public static void OpenWindow()
    {
        RoomPainterWindow window = GetWindow<RoomPainterWindow>("Room Painter");
        window.minSize = new Vector2(680, 560);
        window.Show();
    }

    private void OnEnable()
    {
        if (cells == null || cells.GetLength(0) != gridWidth || cells.GetLength(1) != gridHeight)
        {
            InitializeGridWithFrame(gridWidth, gridHeight, doorGapSize);
        }
    }

    private void OnGUI()
    {
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        DrawTopSettingsBar();
        EditorGUILayout.Space(6);
        DrawBrushToolbar();
        EditorGUILayout.Space(6);
        DrawPaintCanvas();
        EditorGUILayout.Space(8);
        DrawBuildAndLoadSection();
        EditorGUILayout.Space(6);
        DrawJsonSection();

        EditorGUILayout.EndScrollView();
    }

    // =====================================================================
    // 1. EINSTELLUNGEN (RAUMGRÖSSE, TÜREN, TILES)
    // =====================================================================

    private void DrawTopSettingsBar()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("1. Raum-Maße & Tilemap-Setup (Jederzeit anpassbar)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        roomName = EditorGUILayout.TextField("Raum-Name", roomName);
        biomeName = EditorGUILayout.TextField("Biom-Ordner", biomeName, GUILayout.MaxWidth(220));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        int newW = Mathf.Clamp(EditorGUILayout.IntField("Breite (Tiles)", gridWidth), 12, 120);
        int newH = Mathf.Clamp(EditorGUILayout.IntField("Höhe (Tiles)", gridHeight), 12, 120);
        int newDoor = Mathf.Clamp(EditorGUILayout.IntField("Tür-Lücke (Tiles)", doorGapSize), 2, 16);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Neues leeres Raster (Mit Rahmen & 4 Türen)"))
        {
            gridWidth = newW;
            gridHeight = newH;
            doorGapSize = newDoor;
            InitializeGridWithFrame(gridWidth, gridHeight, doorGapSize);
            SyncGridToJsonBuffer();
        }

        if (GUILayout.Button("Größe ändern (Zeichnung behalten)"))
        {
            ResizeGridKeepContent(newW, newH, newDoor);
            SyncGridToJsonBuffer();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        templateRoomPrefab = (Room)EditorGUILayout.ObjectField(
            new GUIContent("Basis Room-Prefab (Optional)", "Wenn zugewiesen, werden Türen/Blocker aus diesem Prefab übernommen."),
            templateRoomPrefab, typeof(Room), false);

        groundLayerName = EditorGUILayout.TextField("Wand-Layer", groundLayerName, GUILayout.MaxWidth(200));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        groundTile = (TileBase)EditorGUILayout.ObjectField("Wand-Tile (Leer = Auto)", groundTile, typeof(TileBase), false);
        hazardTile = (TileBase)EditorGUILayout.ObjectField("Hazard-Tile (Leer = Auto)", hazardTile, typeof(TileBase), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // =====================================================================
    // 2. WERKZEUGLEISTE (PINSEL & AKTIONEN)
    // =====================================================================

    private void DrawBrushToolbar()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("2. Pinsel (Linksklick = Malen | Rechtsklick = Radieren)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawBrushButton(BrushType.Wall, "⬛ Wand (#)", new Color(0.28f, 0.32f, 0.38f));
        DrawBrushButton(BrushType.Hazard, "🟥 Stacheln (^)", new Color(0.85f, 0.22f, 0.22f));
        DrawBrushButton(BrushType.NormalCube, "🟦 Würfel-Spawn (C)", new Color(0.15f, 0.75f, 0.90f));
        DrawBrushButton(BrushType.SecretCube, "🟨 Geheim-Spawn (X)", new Color(0.95f, 0.78f, 0.15f));
        DrawBrushButton(BrushType.Empty, "⬜ Radierer (.)", new Color(0.18f, 0.18f, 0.20f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        useRectTool = GUILayout.Toggle(useRectTool, "📐 Rechteck-Füllen (Start- & Endpunkt klicken)", "Button", GUILayout.Height(22));

        if (GUILayout.Button("↔ Horizontal spiegeln", GUILayout.Height(22)))
        {
            MirrorHorizontal();
        }
        if (GUILayout.Button("↕ Außenrahmen + 4 Türen erneuern", GUILayout.Height(22)))
        {
            ApplyOuterBorderAndDoors(doorGapSize);
        }
        cellPixelSize = EditorGUILayout.Slider("Zoom", cellPixelSize, 12f, 36f, GUILayout.MaxWidth(200));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawBrushButton(BrushType type, string label, Color tint)
    {
        Color prevBg = GUI.backgroundColor;
        bool isSelected = (currentBrush == type);
        GUI.backgroundColor = isSelected ? Color.Lerp(tint, Color.white, 0.35f) : tint * 0.7f;

        GUIStyle style = new GUIStyle(GUI.skin.button)
        {
            fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal,
            fixedHeight = 28
        };

        if (GUILayout.Button(label, style))
        {
            currentBrush = type;
            rectStartCell = null;
        }

        GUI.backgroundColor = prevBg;
    }

    // =====================================================================
    // 3. ZEICHENFLÄCHE (GRID-CANVAS)
    // =====================================================================

    private void DrawPaintCanvas()
    {
        if (cells == null) return;

        float totalW = gridWidth * cellPixelSize;
        float totalH = gridHeight * cellPixelSize;

        canvasScroll = EditorGUILayout.BeginScrollView(
            canvasScroll,
            GUILayout.Height(Mathf.Clamp(totalH + 28f, 240f, 440f))
        );

        Rect canvasRect = GUILayoutUtility.GetRect(totalW + 10f, totalH + 10f);
        Rect gridOrigin = new Rect(canvasRect.x + 5f, canvasRect.y + 5f, totalW, totalH);

        EditorGUI.DrawRect(gridOrigin, new Color(0.08f, 0.09f, 0.12f));

        Event e = Event.current;
        if (gridOrigin.Contains(e.mousePosition))
        {
            int gx = Mathf.FloorToInt((e.mousePosition.x - gridOrigin.x) / cellPixelSize);
            int gyFromTop = Mathf.FloorToInt((e.mousePosition.y - gridOrigin.y) / cellPixelSize);
            int gy = (gridHeight - 1) - gyFromTop;

            if (gx >= 0 && gx < gridWidth && gy >= 0 && gy < gridHeight)
            {
                HandleCanvasMouseInput(e, new Vector2Int(gx, gy));
            }
        }

        int halfW = gridWidth / 2;
        int halfH = gridHeight / 2;
        int doorMinX = halfW - doorGapSize / 2;
        int doorMaxX = doorMinX + doorGapSize - 1;
        int doorMinY = halfH - doorGapSize / 2;
        int doorMaxY = doorMinY + doorGapSize - 1;

        for (int y = 0; y < gridHeight; y++)
        {
            int drawRow = (gridHeight - 1) - y;
            for (int x = 0; x < gridWidth; x++)
            {
                Rect cellRect = new Rect(
                    gridOrigin.x + x * cellPixelSize,
                    gridOrigin.y + drawRow * cellPixelSize,
                    cellPixelSize - 1f,
                    cellPixelSize - 1f
                );

                char c = cells[x, y];
                bool isDoorGap = ((y == 0 || y == gridHeight - 1) && x >= doorMinX && x <= doorMaxX) ||
                                 ((x == 0 || x == gridWidth - 1) && y >= doorMinY && y <= doorMaxY);

                Color cellCol = GetColorForChar(c, isDoorGap);
                EditorGUI.DrawRect(cellRect, cellCol);

                if (c == 'C' || c == 'X' || c == '^')
                {
                    GUIStyle charStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = Color.black }
                    };
                    GUI.Label(cellRect, c.ToString(), charStyle);
                }
            }
        }

        // Rechteck-Vorschau
        if (useRectTool && rectStartCell.HasValue)
        {
            int rx = rectStartCell.Value.x;
            int ry = (gridHeight - 1) - rectStartCell.Value.y;
            Rect startMarker = new Rect(
                gridOrigin.x + rx * cellPixelSize,
                gridOrigin.y + ry * cellPixelSize,
                cellPixelSize,
                cellPixelSize
            );
            EditorGUI.DrawRect(startMarker, new Color(1f, 1f, 1f, 0.45f));
        }

        EditorGUILayout.EndScrollView();
    }

    private void HandleCanvasMouseInput(Event e, Vector2Int cellPos)
    {
        if (useRectTool)
        {
            if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
            {
                char paintChar = (e.button == 1) ? '.' : (char)currentBrush;
                if (!rectStartCell.HasValue)
                {
                    rectStartCell = cellPos;
                }
                else
                {
                    FillRect(rectStartCell.Value, cellPos, paintChar);
                    rectStartCell = null;
                    SyncGridToJsonBuffer();
                }
                e.Use();
                Repaint();
            }
            return;
        }

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && (e.button == 0 || e.button == 1))
        {
            char paintChar = (e.button == 1) ? '.' : (char)currentBrush;
            cells[cellPos.x, cellPos.y] = paintChar;
            e.Use();
            Repaint();
        }
    }

    private Color GetColorForChar(char c, bool isDoorSlot)
    {
        return c switch
        {
            '#' => new Color(0.32f, 0.38f, 0.45f),
            '^' => new Color(0.92f, 0.22f, 0.22f),
            'C' => new Color(0.20f, 0.85f, 0.98f),
            'X' => new Color(1.00f, 0.82f, 0.18f),
            _ => isDoorSlot ? new Color(0.14f, 0.24f, 0.16f) : new Color(0.13f, 0.14f, 0.18f)
        };
    }

    // =====================================================================
    // 4. GENERIEREN, AKTUALISIEREN & BESTEHENDE RÄUME EINLESEN
    // =====================================================================

    private void DrawBuildAndLoadSection()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("3. Raum erstellen oder bestehenden Raum nachträglich anpassen", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
        if (GUILayout.Button("✨ Neuen Raum in Szene + als Prefab bauen", GUILayout.Height(32)))
        {
            BuildOrUpdateRoom(targetExistingRoom: null, saveAsPrefab: true);
        }

        GUI.backgroundColor = new Color(0.35f, 0.70f, 0.95f);
        if (GUILayout.Button("🔄 Auf ausgewählten Raum in Szene anwenden", GUILayout.Height(32)))
        {
            Room selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Room>() : null;
            if (selected != null)
            {
                BuildOrUpdateRoom(targetExistingRoom: selected, saveAsPrefab: false);
            }
            else
            {
                EditorUtility.DisplayDialog("Kein Room ausgewählt", "Bitte wähle zuerst ein Room-Objekt in der Hierarchy aus.", "OK");
            }
        }

        GUI.backgroundColor = new Color(0.95f, 0.75f, 0.25f);
        if (GUILayout.Button("📥 Ausgewählten Raum in Painter einlesen", GUILayout.Height(32)))
        {
            Room selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Room>() : null;
            if (selected != null)
            {
                LoadFromExistingRoom(selected);
            }
            else
            {
                EditorUtility.DisplayDialog("Kein Room ausgewählt", "Bitte wähle zuerst ein Room-Objekt in der Hierarchy oder im Projekt aus.", "OK");
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void BuildOrUpdateRoom(Room targetExistingRoom, bool saveAsPrefab)
    {
        TileBase wallTileToUse = groundTile != null ? groundTile : GetOrCreateFallbackTile("Fallback_WallTile", new Color(0.35f, 0.42f, 0.48f));
        TileBase spikeTileToUse = hazardTile != null ? hazardTile : GetOrCreateFallbackTile("Fallback_HazardTile", new Color(0.95f, 0.2f, 0.2f));

        Room roomInstance = targetExistingRoom;

        if (roomInstance == null)
        {
            if (templateRoomPrefab != null)
            {
                GameObject cloned = (GameObject)PrefabUtility.InstantiatePrefab(templateRoomPrefab.gameObject);
                PrefabUtility.UnpackPrefabInstance(cloned, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                roomInstance = cloned.GetComponent<Room>();
                roomInstance.name = roomName;
            }
            else
            {
                GameObject newRoomObj = new GameObject(roomName);
                roomInstance = newRoomObj.AddComponent<Room>();
            }
        }

        Undo.RegisterFullObjectHierarchyUndo(roomInstance.gameObject, "Build Room From Painter");

        // 1. Grid sicherstellen
        Grid grid = roomInstance.GetComponentInChildren<Grid>();
        if (grid == null)
        {
            GameObject gridObj = new GameObject("Grid");
            gridObj.transform.SetParent(roomInstance.transform, false);
            grid = gridObj.AddComponent<Grid>();
        }

        // 2. Ground-Tilemap + Hazard-Tilemap getrennt aufbauen (jederzeit im Nachhinein änderbar!)
        Tilemap groundMap = GetOrCreateChildTilemap(grid.transform, "Ground", groundLayerName, null, isTrigger: false);
        Tilemap hazardMap = GetOrCreateChildTilemap(grid.transform, "Hazards", "Default", hazardTagName, isTrigger: true);

        groundMap.ClearAllTiles();
        hazardMap.ClearAllTiles();

        // Zentrierter Offset, damit (0,0) genau in der Raummitte liegt
        int offsetX = -gridWidth / 2;
        int offsetY = -gridHeight / 2;

        List<Vector3> normalSpawnPositions = new List<Vector3>();
        List<Vector3> secretSpawnPositions = new List<Vector3>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3Int tilePos = new Vector3Int(x + offsetX, y + offsetY, 0);
                Vector3 worldCenter = roomInstance.transform.TransformPoint(new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f));

                char c = cells[x, y];
                if (c == '#')
                {
                    groundMap.SetTile(tilePos, wallTileToUse);
                }
                else if (c == '^')
                {
                    hazardMap.SetTile(tilePos, spikeTileToUse);
                }
                else if (c == 'C')
                {
                    normalSpawnPositions.Add(worldCenter);
                }
                else if (c == 'X')
                {
                    secretSpawnPositions.Add(worldCenter);
                }
            }
        }

        groundMap.RefreshAllTiles();
        hazardMap.RefreshAllTiles();

        // 3. Würfel-Spawnpunkte anlegen & im Room-Skript verknüpfen
        Transform spawnContainer = roomInstance.transform.Find("CubeSpawnPoints");
        if (spawnContainer != null) DestroyImmediate(spawnContainer.gameObject);

        spawnContainer = new GameObject("CubeSpawnPoints").transform;
        spawnContainer.SetParent(roomInstance.transform, false);

        List<Transform> normalTransforms = new List<Transform>();
        for (int i = 0; i < normalSpawnPositions.Count; i++)
        {
            GameObject sp = new GameObject($"Spawn_Normal_{i + 1}");
            sp.transform.SetParent(spawnContainer, false);
            sp.transform.position = normalSpawnPositions[i];
            normalTransforms.Add(sp.transform);
        }

        List<Transform> secretTransforms = new List<Transform>();
        for (int i = 0; i < secretSpawnPositions.Count; i++)
        {
            GameObject sp = new GameObject($"Spawn_Secret_{i + 1}");
            sp.transform.SetParent(spawnContainer, false);
            sp.transform.position = secretSpawnPositions[i];
            secretTransforms.Add(sp.transform);
        }

        // 4. CameraBounds automatisch auf Raumgröße anpassen
        BoxCollider2D camBounds = roomInstance.GetComponent<BoxCollider2D>();
        if (camBounds == null)
        {
            camBounds = roomInstance.gameObject.AddComponent<BoxCollider2D>();
        }
        camBounds.isTrigger = true;
        camBounds.offset = Vector2.zero;
        camBounds.size = new Vector2(gridWidth, gridHeight);

        // 5. Über SerializedObject die privaten Listen in Room.cs sauber befüllen
        SerializedObject so = new SerializedObject(roomInstance);
        so.Update();

        SerializedProperty camProp = so.FindProperty("cameraBounds");
        if (camProp != null) camProp.objectReferenceValue = camBounds;

        FillTransformArrayProperty(so.FindProperty("normalCubeSpawnPoints"), normalTransforms);
        FillTransformArrayProperty(so.FindProperty("secretCubeSpawnPoints"), secretTransforms);

        // Falls der Raum komplett neu (ohne Template) erstellt wurde, Standard-Türen anlegen
        SerializedProperty doorsProp = so.FindProperty("doors");
        if (doorsProp != null && doorsProp.arraySize == 0)
        {
            SetupDefaultDoorsOnNewRoom(roomInstance, doorsProp, wallTileToUse);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(roomInstance);

        if (saveAsPrefab)
        {
            string folderPath = $"Assets/Prefabs/Rooms/{biomeName}";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{roomName}.prefab");
            PrefabUtility.SaveAsPrefabAssetAndConnect(roomInstance.gameObject, prefabPath, InteractionMode.UserAction);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RoomPainter] Raum '{roomName}' erfolgreich gebaut und gespeichert unter: {prefabPath}");
        }
        else
        {
            Debug.Log($"[RoomPainter] Bestehender Raum '{roomInstance.name}' wurde aktualisiert!");
        }

        Selection.activeGameObject = roomInstance.gameObject;
    }

    /// <summary>
    /// Liest einen bereits gebauten Raum (Tilemaps + Spawnpunkte) wieder zurück in das Mal-Fenster!
    /// </summary>
    private void LoadFromExistingRoom(Room room)
    {
        Tilemap[] maps = room.GetComponentsInChildren<Tilemap>();
        if (maps.Length == 0)
        {
            EditorUtility.DisplayDialog("Keine Tilemap gefunden", "Dieser Raum enthält noch keine Tilemap.", "OK");
            return;
        }

        Tilemap groundMap = null;
        Tilemap hazardMap = null;

        foreach (Tilemap tm in maps)
        {
            if (tm.CompareTag(hazardTagName) || tm.name.ToLower().Contains("hazard"))
                hazardMap = tm;
            else if (groundMap == null)
                groundMap = tm;
        }

        BoxCollider2D boundsCol = room.GetComponent<BoxCollider2D>();
        if (boundsCol != null && boundsCol.size.x >= 12 && boundsCol.size.y >= 12)
        {
            gridWidth = Mathf.RoundToInt(boundsCol.size.x);
            gridHeight = Mathf.RoundToInt(boundsCol.size.y);
        }

        roomName = room.name;
        cells = new char[gridWidth, gridHeight];
        int offsetX = -gridWidth / 2;
        int offsetY = -gridHeight / 2;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3Int tilePos = new Vector3Int(x + offsetX, y + offsetY, 0);
                if (hazardMap != null && hazardMap.HasTile(tilePos))
                    cells[x, y] = '^';
                else if (groundMap != null && groundMap.HasTile(tilePos))
                    cells[x, y] = '#';
                else
                    cells[x, y] = '.';
            }
        }

        SerializedObject so = new SerializedObject(room);
        ReadSpawnsIntoGrid(room.transform, so.FindProperty("normalCubeSpawnPoints"), 'C', offsetX, offsetY);
        ReadSpawnsIntoGrid(room.transform, so.FindProperty("secretCubeSpawnPoints"), 'X', offsetX, offsetY);

        SyncGridToJsonBuffer();
        Repaint();
        Debug.Log($"[RoomPainter] Raum '{room.name}' ({gridWidth}x{gridHeight}) zum Bearbeiten geladen.");
    }

    // =====================================================================
    // 5. JSON / KI-AUSTAUSCH & PRESET
    // =====================================================================

    private void DrawJsonSection()
    {
        EditorGUILayout.BeginVertical("box");
        showJsonSection = EditorGUILayout.Foldout(showJsonSection, "4. JSON / ASCII Export & KI-Import (Aufklappen)", true);

        if (showJsonSection)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Aktualisiere JSON aus Zeichnung"))
            {
                SyncGridToJsonBuffer();
            }
            if (GUILayout.Button("JSON in Zwischenablage kopieren"))
            {
                SyncGridToJsonBuffer();
                EditorGUIUtility.systemCopyBuffer = jsonBuffer;
            }
            if (GUILayout.Button("JSON aus Textfeld auf Raster anwenden"))
            {
                LoadGridFromJsonBuffer(jsonBuffer);
            }
            if (GUILayout.Button("🌲 Beispiel-Raum laden"))
            {
                LoadSampleCeilingVaultRoom();
            }
            EditorGUILayout.EndHorizontal();

            jsonBuffer = EditorGUILayout.TextArea(jsonBuffer, GUILayout.Height(140));
        }

        EditorGUILayout.EndVertical();
    }

    private void SyncGridToJsonBuffer()
    {
        RoomJsonData data = new RoomJsonData
        {
            roomName = this.roomName,
            biome = this.biomeName,
            width = this.gridWidth,
            height = this.gridHeight,
            doorGap = this.doorGapSize
        };

        for (int y = gridHeight - 1; y >= 0; y--)
        {
            char[] row = new char[gridWidth];
            for (int x = 0; x < gridWidth; x++)
            {
                row[x] = cells[x, y];
            }
            data.layout.Add(new string(row));
        }

        jsonBuffer = JsonUtility.ToJson(data, true);
    }

    private void LoadGridFromJsonBuffer(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            RoomJsonData data = JsonUtility.FromJson<RoomJsonData>(json);
            if (data == null || data.layout == null || data.layout.Count == 0) return;

            roomName = data.roomName;
            biomeName = data.biome;
            gridHeight = data.layout.Count;
            gridWidth = data.layout[0].Length;
            if (data.doorGap > 0) doorGapSize = data.doorGap;

            cells = new char[gridWidth, gridHeight];

            for (int rowIdx = 0; rowIdx < gridHeight; rowIdx++)
            {
                int y = (gridHeight - 1) - rowIdx;
                string line = data.layout[rowIdx];
                for (int x = 0; x < gridWidth; x++)
                {
                    cells[x, y] = (x < line.Length) ? line[x] : '.';
                }
            }

            Repaint();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomPainter] Fehler beim Lesen des JSON: {ex.Message}");
        }
    }

    // =====================================================================
    // HILFSMETHODEN FÜR RASTER & TILEMAPS
    // =====================================================================

    private void InitializeGridWithFrame(int w, int h, int doorGap)
    {
        cells = new char[w, h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                cells[x, y] = '.';
            }
        }
        ApplyOuterBorderAndDoors(doorGap);
    }

    private void ApplyOuterBorderAndDoors(int doorGap)
    {
        int halfW = gridWidth / 2;
        int halfH = gridHeight / 2;
        int doorMinX = halfW - doorGap / 2;
        int doorMaxX = doorMinX + doorGap - 1;
        int doorMinY = halfH - doorGap / 2;
        int doorMaxY = doorMinY + doorGap - 1;

        for (int x = 0; x < gridWidth; x++)
        {
            bool isDoorX = (x >= doorMinX && x <= doorMaxX);
            cells[x, 0] = isDoorX ? '.' : '#';
            cells[x, gridHeight - 1] = isDoorX ? '.' : '#';
        }

        for (int y = 0; y < gridHeight; y++)
        {
            bool isDoorY = (y >= doorMinY && y <= doorMaxY);
            cells[0, y] = isDoorY ? '.' : '#';
            cells[gridWidth - 1, y] = isDoorY ? '.' : '#';
        }
    }

    private void ResizeGridKeepContent(int newW, int newH, int newDoor)
    {
        char[,] newCells = new char[newW, newH];
        for (int x = 0; x < newW; x++)
        {
            for (int y = 0; y < newH; y++)
            {
                if (x > 0 && x < gridWidth - 1 && y > 0 && y < gridHeight - 1 && x < newW - 1 && y < newH - 1)
                    newCells[x, y] = cells[x, y];
                else
                    newCells[x, y] = '.';
            }
        }

        gridWidth = newW;
        gridHeight = newH;
        doorGapSize = newDoor;
        cells = newCells;
        ApplyOuterBorderAndDoors(doorGapSize);
    }

    private void FillRect(Vector2Int a, Vector2Int b, char paintChar)
    {
        int minX = Mathf.Min(a.x, b.x);
        int maxX = Mathf.Max(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y);
        int maxY = Mathf.Max(a.y, b.y);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                cells[x, y] = paintChar;
            }
        }
    }

    private void MirrorHorizontal()
    {
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth / 2; x++)
            {
                cells[gridWidth - 1 - x, y] = cells[x, y];
            }
        }
        Repaint();
    }

    private Tilemap GetOrCreateChildTilemap(Transform gridParent, string childName, string layerName, string tagName, bool isTrigger)
    {
        Transform existing = gridParent.Find(childName);
        GameObject mapObj = existing != null ? existing.gameObject : new GameObject(childName);
        mapObj.transform.SetParent(gridParent, false);

        int layerId = LayerMask.NameToLayer(layerName);
        if (layerId >= 0) mapObj.layer = layerId;

        if (!string.IsNullOrEmpty(tagName))
        {
            try { mapObj.tag = tagName; }
            catch { Debug.LogWarning($"[RoomPainter] Tag '{tagName}' existiert noch nicht in den Project Settings."); }
        }

        Tilemap tm = mapObj.GetComponent<Tilemap>();
        if (tm == null) tm = mapObj.AddComponent<Tilemap>();

        TilemapRenderer tr = mapObj.GetComponent<TilemapRenderer>();
        if (tr == null) tr = mapObj.AddComponent<TilemapRenderer>();

        TilemapCollider2D col = mapObj.GetComponent<TilemapCollider2D>();
        if (col == null) col = mapObj.AddComponent<TilemapCollider2D>();
        col.isTrigger = isTrigger;

        return tm;
    }

    private void FillTransformArrayProperty(SerializedProperty arrayProp, List<Transform> transforms)
    {
        if (arrayProp == null) return;
        arrayProp.ClearArray();
        for (int i = 0; i < transforms.Count; i++)
        {
            arrayProp.InsertArrayElementAtIndex(i);
            arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = transforms[i];
        }
    }

    private void ReadSpawnsIntoGrid(Transform roomRoot, SerializedProperty prop, char symbol, int offsetX, int offsetY)
    {
        if (prop == null) return;
        for (int i = 0; i < prop.arraySize; i++)
        {
            Transform t = prop.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (t == null) continue;

            Vector3 local = roomRoot.InverseTransformPoint(t.position);
            int gx = Mathf.Clamp(Mathf.FloorToInt(local.x) - offsetX, 0, gridWidth - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt(local.y) - offsetY, 0, gridHeight - 1);
            cells[gx, gy] = symbol;
        }
    }

    private void SetupDefaultDoorsOnNewRoom(Room room, SerializedProperty doorsProp, TileBase wallTile)
    {
        Direction[] dirs = { Direction.North, Direction.East, Direction.South, Direction.West };
        doorsProp.ClearArray();

        Transform doorsRoot = new GameObject("Doors").transform;
        doorsRoot.SetParent(room.transform, false);

        for (int i = 0; i < dirs.Length; i++)
        {
            Direction dir = dirs[i];
            GameObject doorHolder = new GameObject($"Door_{dir}");
            doorHolder.transform.SetParent(doorsRoot, false);

            GameObject spawnPt = new GameObject($"SpawnPoint_{dir}");
            spawnPt.transform.SetParent(doorHolder.transform, false);

            float halfW = gridWidth * 0.5f;
            float halfH = gridHeight * 0.5f;

            spawnPt.transform.localPosition = dir switch
            {
                Direction.North => new Vector3(0f, halfH - 2f, 0f),
                Direction.South => new Vector3(0f, -halfH + 2f, 0f),
                Direction.East => new Vector3(halfW - 2f, 0f, 0f),
                Direction.West => new Vector3(-halfW + 2f, 0f, 0f),
                _ => Vector3.zero
            };

            doorsProp.InsertArrayElementAtIndex(i);
            SerializedProperty entry = doorsProp.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("direction").enumValueIndex = (int)dir;
            entry.FindPropertyRelative("spawnPoint").objectReferenceValue = spawnPt.transform;
        }
    }

    private TileBase GetOrCreateFallbackTile(string fileName, Color color)
    {
        string dir = "Assets/Editor/GeneratedTiles";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string tilePath = $"{dir}/{fileName}.asset";
        Tile existingTile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
        if (existingTile != null) return existingTile;

        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool border = (x == 0 || y == 0 || x == size - 1 || y == size - 1);
                tex.SetPixel(x, y, border ? color * 0.75f : color);
            }
        }
        tex.Apply();

        string pngPath = $"{dir}/{fileName}.png";
        File.WriteAllBytes(pngPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(pngPath);

        TextureImporter importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer != null)
        {
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        Tile newTile = CreateInstance<Tile>();
        newTile.sprite = sprite;
        newTile.colliderType = Tile.ColliderType.Sprite;

        AssetDatabase.CreateAsset(newTile, tilePath);
        AssetDatabase.SaveAssets();
        return newTile;
    }

    private void LoadSampleCeilingVaultRoom()
    {
        InitializeGridWithFrame(30, 20, doorGapSize);

        // Kleine 360°-Kletter-Insel in der Mitte + Stachelgrube + Geheim-Nische oben rechts
        FillRect(new Vector2Int(9, 6), new Vector2Int(20, 7), '#');
        FillRect(new Vector2Int(13, 8), new Vector2Int(16, 13), '#');
        FillRect(new Vector2Int(10, 1), new Vector2Int(19, 1), '^');
        FillRect(new Vector2Int(22, 14), new Vector2Int(27, 14), '#');

        cells[11, 8] = 'C';
        cells[18, 8] = 'C';
        cells[26, 16] = 'X';

        SyncGridToJsonBuffer();
        Repaint();
    }
}
#endif
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
    public string roomName = "Room_Forest_01";
    public string biome = "Forest";
    public int width = 30;
    public int height = 20;
    public int doorGap = 4;
    public int wallThickness = 2;
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

    // --- 1. Raum-Einstellungen (Standard passend zu Room1: 4er Tür, 2er Wanddicke) ---
    private string roomName = "Room_Forest_01";
    private string biomeName = "Forest";
    private string basePrefabFolder = "Assets/Prefabs/Rooms";
    private int gridWidth = 30;
    private int gridHeight = 20;
    private int doorGapSize = 4;
    private int outerWallThickness = 2;

    // --- 2. Vorlage & Tileset / RuleTile ---
    private Room referenceRoomPrefab;
    private TileBase wallRuleTile;
    private TileBase hazardRuleTile;
    private string groundLayerName = "Ground";
    private string hazardTagName = "Hazard";

    // --- 3. Zeichen-Status ---
    private char[,] cells;
    private BrushType currentBrush = BrushType.Wall;
    private bool useRectTool = false;
    private Vector2Int? rectStartCell = null;

    // --- 4. UI-Status ---
    private Vector2 canvasScroll;
    private Vector2 mainScroll;
    private bool showJsonSection = false;
    private string jsonBuffer = "";
    private float cellPixelSize = 22f;

    [MenuItem("Tools/Slimily Room Painter")]
    public static void OpenWindow()
    {
        RoomPainterWindow window = GetWindow<RoomPainterWindow>("Slimily Room Painter");
        window.minSize = new Vector2(720, 620);
        window.Show();
    }

    private void OnEnable()
    {
        TryAutoDetectExistingRoomAndTiles();

        if (cells == null || cells.GetLength(0) != gridWidth || cells.GetLength(1) != gridHeight)
        {
            InitializeGridWithFrame(gridWidth, gridHeight, doorGapSize, outerWallThickness);
            SyncGridToJsonBuffer();
        }
    }

    /// <summary>
    /// Sucht beim Öffnen automatisch nach deinem bestehenden Room-Prefab (z. B. Room1),
    /// um dessen echtes Tileset/RuleTile direkt voreinzustellen.
    /// </summary>
    private void TryAutoDetectExistingRoomAndTiles()
    {
        if (referenceRoomPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && prefab.GetComponent<Room>() != null)
                {
                    referenceRoomPrefab = prefab.GetComponent<Room>();
                    if (prefab.name.Contains("Room1")) break;
                }
            }
        }

        if (referenceRoomPrefab != null && wallRuleTile == null)
        {
            ExtractTilesFromReferenceRoom(referenceRoomPrefab);
        }
    }

    private void ExtractTilesFromReferenceRoom(Room refRoom)
    {
        if (refRoom == null) return;

        Tilemap[] maps = refRoom.GetComponentsInChildren<Tilemap>(true);
        foreach (Tilemap tm in maps)
        {
            bool isHazardMap = tm.name.ToLower().Contains("hazard");
            BoundsInt bounds = tm.cellBounds;

            foreach (Vector3Int pos in bounds.allPositionsWithin)
            {
                TileBase tile = tm.GetTile(pos);
                if (tile != null)
                {
                    if (isHazardMap && hazardRuleTile == null)
                    {
                        hazardRuleTile = tile;
                    }
                    else if (!isHazardMap && wallRuleTile == null)
                    {
                        wallRuleTile = tile;
                    }
                    break;
                }
            }
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

    // =========================================================================
    // 1. EINSTELLUNGEN, MAßE & TILESET
    // =========================================================================

    private void DrawTopSettingsBar()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("1. Raum-Maße, Speicherort & Tileset / RuleTile", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        roomName = EditorGUILayout.TextField("Prefab-Name", roomName);
        biomeName = EditorGUILayout.TextField("Biom-Unterordner", biomeName, GUILayout.MaxWidth(220));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        basePrefabFolder = EditorGUILayout.TextField("Basis-Ordner", basePrefabFolder);
        if (GUILayout.Button("Ordner wählen...", GUILayout.Width(120)))
        {
            string selected = EditorUtility.OpenFolderPanel("Zielordner für Raum-Prefabs wählen", "Assets", "");
            if (!string.IsNullOrEmpty(selected) && selected.Contains(Application.dataPath))
            {
                basePrefabFolder = "Assets" + selected.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(3);

        EditorGUILayout.BeginHorizontal();
        int newW = Mathf.Clamp(EditorGUILayout.IntField("Breite (Tiles)", gridWidth), 12, 120);
        int newH = Mathf.Clamp(EditorGUILayout.IntField("Höhe (Tiles)", gridHeight), 12, 120);
        int newDoor = Mathf.Clamp(EditorGUILayout.IntField("Tür-Breite (Tiles)", doorGapSize), 2, 12);
        int newWall = Mathf.Clamp(EditorGUILayout.IntField("Wand-Dicke (Tiles)", outerWallThickness), 1, 5);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Neues leeres Raster (Mit Rahmen & 4 Türen)"))
        {
            gridWidth = newW;
            gridHeight = newH;
            doorGapSize = newDoor;
            outerWallThickness = newWall;
            InitializeGridWithFrame(gridWidth, gridHeight, doorGapSize, outerWallThickness);
            SyncGridToJsonBuffer();
        }

        if (GUILayout.Button("Größe/Türen anpassen (Innen-Zeichnung behalten)"))
        {
            ResizeGridKeepContent(newW, newH, newDoor, newWall);
            SyncGridToJsonBuffer();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        referenceRoomPrefab = (Room)EditorGUILayout.ObjectField(
            new GUIContent("Vorlage Room-Prefab (z.B. Room1)", "Übernimmt automatisch das verwendete RuleTile/Tile aus diesem Raum."),
            referenceRoomPrefab, typeof(Room), false);
        if (EditorGUI.EndChangeCheck() && referenceRoomPrefab != null)
        {
            ExtractTilesFromReferenceRoom(referenceRoomPrefab);
        }

        if (referenceRoomPrefab != null && GUILayout.Button("Tiles aus Vorlage holen", GUILayout.Width(150)))
        {
            wallRuleTile = null;
            ExtractTilesFromReferenceRoom(referenceRoomPrefab);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        wallRuleTile = (TileBase)EditorGUILayout.ObjectField("Wand RuleTile / Tile", wallRuleTile, typeof(TileBase), false);
        hazardRuleTile = (TileBase)EditorGUILayout.ObjectField("Hazard RuleTile / Tile", hazardRuleTile, typeof(TileBase), false);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // =========================================================================
    // 2. PINSEL & WERKZEUGE
    // =========================================================================

    private void DrawBrushToolbar()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("2. Pinsel (Linksklick ziehen = Malen | Rechtsklick ziehen = Radieren)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        DrawBrushButton(BrushType.Wall, "⬛ Wand (#)", new Color(0.28f, 0.35f, 0.42f));
        DrawBrushButton(BrushType.Hazard, "🟥 Stacheln (^)", new Color(0.85f, 0.22f, 0.22f));
        DrawBrushButton(BrushType.NormalCube, "🟦 Würfel-Spawn (C)", new Color(0.15f, 0.75f, 0.90f));
        DrawBrushButton(BrushType.SecretCube, "🟨 Geheim-Spawn (X)", new Color(0.95f, 0.78f, 0.15f));
        DrawBrushButton(BrushType.Empty, "⬜ Radierer (.)", new Color(0.18f, 0.18f, 0.20f));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        useRectTool = GUILayout.Toggle(useRectTool, "📐 Rechteck-Werkzeug (2 Ecken klicken)", "Button", GUILayout.Height(22));

        if (GUILayout.Button("↔ Horizontal spiegeln", GUILayout.Height(22)))
        {
            MirrorHorizontal();
        }
        if (GUILayout.Button("↕ Außenrahmen + 4 Türen erneuern", GUILayout.Height(22)))
        {
            ApplyOuterBorderAndDoors(doorGapSize, outerWallThickness);
            Repaint();
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

    // =========================================================================
    // 3. ZEICHEN-RASTER (CANVAS)
    // =========================================================================

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
                bool isDoorTunnel = IsCellInsideAnyDoorTunnel(x, y);

                EditorGUI.DrawRect(cellRect, GetColorForChar(c, isDoorTunnel));

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

        if (useRectTool && rectStartCell.HasValue)
        {
            int rx = rectStartCell.Value.x;
            int ry = (gridHeight - 1) - rectStartCell.Value.y;
            Rect startMarker = new Rect(gridOrigin.x + rx * cellPixelSize, gridOrigin.y + ry * cellPixelSize, cellPixelSize, cellPixelSize);
            EditorGUI.DrawRect(startMarker, new Color(1f, 1f, 1f, 0.45f));
        }

        EditorGUILayout.EndScrollView();
    }

    private bool IsCellInsideAnyDoorTunnel(int x, int y)
    {
        int doorMinX = (gridWidth / 2) - (doorGapSize / 2);
        int doorMaxX = doorMinX + doorGapSize - 1;
        int doorMinY = (gridHeight / 2) - (doorGapSize / 2);
        int doorMaxY = doorMinY + doorGapSize - 1;

        bool inNorthOrSouth = (y < outerWallThickness || y >= gridHeight - outerWallThickness) && (x >= doorMinX && x <= doorMaxX);
        bool inWestOrEast = (x < outerWallThickness || x >= gridWidth - outerWallThickness) && (y >= doorMinY && y <= doorMaxY);
        return inNorthOrSouth || inWestOrEast;
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
            '#' => new Color(0.32f, 0.44f, 0.36f),
            '^' => new Color(0.92f, 0.22f, 0.22f),
            'C' => new Color(0.20f, 0.85f, 0.98f),
            'X' => new Color(1.00f, 0.82f, 0.18f),
            _ => isDoorSlot ? new Color(0.16f, 0.28f, 0.18f) : new Color(0.13f, 0.14f, 0.18f)
        };
    }

    // =========================================================================
    // 4. PREFAB GENERIEREN (EXAKTE ROOM1-STRUKTUR & SICHERES SPEICHERN)
    // =========================================================================

    private void DrawBuildAndLoadSection()
    {
        EditorGUILayout.BeginVertical("box");
        string previewPath = $"{basePrefabFolder}/{biomeName}/{roomName}.prefab";
        EditorGUILayout.LabelField($"3. Ziel-Pfad: {previewPath}", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.25f, 0.85f, 0.45f);
        if (GUILayout.Button("✨ Raum-Prefab im Ordner erstellen (Saubere Room1-Struktur)", GUILayout.Height(36)))
        {
            CreateAndSaveRoomPrefab();
        }

        GUI.backgroundColor = new Color(0.35f, 0.70f, 0.95f);
        if (GUILayout.Button("🔄 Ausgewählten Raum aktualisieren", GUILayout.Height(36)))
        {
            Room selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Room>() : null;
            if (selected != null)
            {
                UpdateExistingRoomObject(selected);
            }
            else
            {
                EditorUtility.DisplayDialog("Kein Room ausgewählt", "Bitte wähle zuerst ein Room-Objekt in der Hierarchy aus.", "OK");
            }
        }

        GUI.backgroundColor = new Color(0.95f, 0.75f, 0.25f);
        if (GUILayout.Button("📥 Ausgewählten Raum in Painter einlesen", GUILayout.Height(36)))
        {
            Room selected = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<Room>() : null;
            if (selected == null && Selection.activeObject is GameObject go)
            {
                selected = go.GetComponent<Room>();
            }

            if (selected != null)
            {
                LoadFromExistingRoom(selected);
            }
            else
            {
                EditorUtility.DisplayDialog("Kein Room ausgewählt", "Bitte wähle ein Room-Prefab im Projekt oder ein Room-Objekt in der Szene aus.", "OK");
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void CreateAndSaveRoomPrefab()
    {
        EnsureTagExists(hazardTagName);

        string targetFolder = $"{basePrefabFolder.TrimEnd('/')}/{biomeName.Trim()}";
        EnsureAssetFolderExists(targetFolder);

        GameObject tempRoot = new GameObject(roomName);
        try
        {
            Room roomComp = tempRoot.AddComponent<Room>();
            PopulateRoomHierarchy(roomComp);

            string rawPrefabPath = $"{targetFolder}/{roomName}.prefab";
            string uniquePrefabPath = AssetDatabase.GenerateUniqueAssetPath(rawPrefabPath);

            bool saveSuccess;
            GameObject savedAsset = PrefabUtility.SaveAsPrefabAsset(tempRoot, uniquePrefabPath, out saveSuccess);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saveSuccess && savedAsset != null)
            {
                Selection.activeObject = savedAsset;
                EditorGUIUtility.PingObject(savedAsset);
                Debug.Log($"[RoomPainter] ✅ Raum-Prefab erfolgreich erstellt unter: {uniquePrefabPath}");
            }
            else
            {
                Debug.LogError($"[RoomPainter] ❌ Konnte Prefab nicht unter '{uniquePrefabPath}' speichern!");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomPainter] ❌ Fehler beim Bauen des Raums: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            // Garantiert: Das temporäre Objekt wird IMMER aus der MainGame-Szene entfernt!
            if (tempRoot != null)
            {
                DestroyImmediate(tempRoot);
            }
        }
    }

    private void UpdateExistingRoomObject(Room targetRoom)
    {
        EnsureTagExists(hazardTagName);
        Undo.RegisterFullObjectHierarchyUndo(targetRoom.gameObject, "Update Room From Painter");

        PopulateRoomHierarchy(targetRoom);
        EditorUtility.SetDirty(targetRoom.gameObject);

        if (PrefabUtility.IsPartOfPrefabInstance(targetRoom.gameObject))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetRoom);
        }

        Debug.Log($"[RoomPainter] ✅ Bestehender Raum '{targetRoom.name}' wurde aktualisiert.");
    }

    /// <summary>
    /// Baut die exakte Hierarchie wie in Room1 auf:
    /// Room -> Grid (Doors/Door_..., Tilemap, Hazards), Exits (Exit_...), Spawns (Spawn_...), LevelBounds
    /// </summary>
private void PopulateRoomHierarchy(Room roomInstance)
    {
        if (wallRuleTile == null && referenceRoomPrefab != null)
        {
            ExtractTilesFromReferenceRoom(referenceRoomPrefab);
        }

        TileBase wallTileToUse = wallRuleTile != null
            ? wallRuleTile
            : GetOrCreateFallbackTile("Fallback_WallTile", new Color(0.35f, 0.48f, 0.38f));

        TileBase spikeTileToUse = hazardRuleTile != null
            ? hazardRuleTile
            : GetOrCreateFallbackTile("Fallback_HazardTile", new Color(0.95f, 0.22f, 0.22f));

        int groundLayer = LayerMask.NameToLayer(groundLayerName);
        if (groundLayer < 0) groundLayer = 0;

        // 1. GRID + DOORS + TILEMAP + HAZARDS
        Transform gridTrans = GetOrCreateChild(roomInstance.transform, "Grid");
        if (gridTrans.GetComponent<Grid>() == null) gridTrans.gameObject.AddComponent<Grid>();

        Transform doorsFolder = GetOrCreateChild(gridTrans, "Doors");

        Tilemap groundMap = GetOrCreateTilemapObject(gridTrans, "Tilemap", groundLayer, null, isTrigger: false, sortingOrder: 0);
        Tilemap hazardMap = GetOrCreateTilemapObject(gridTrans, "Hazards", 0, hazardTagName, isTrigger: true, sortingOrder: 2);

        groundMap.ClearAllTiles();
        hazardMap.ClearAllTiles();

        int offsetX = -gridWidth / 2;
        int offsetY = -gridHeight / 2;

        List<Vector3> normalSpawnPositions = new List<Vector3>();
        List<Vector3> secretSpawnPositions = new List<Vector3>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3Int tilePos = new Vector3Int(x + offsetX, y + offsetY, 0);
                Vector3 localCellCenter = new Vector3(tilePos.x + 0.5f, tilePos.y + 0.5f, 0f);

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
                    normalSpawnPositions.Add(localCellCenter);
                }
                else if (c == 'X')
                {
                    secretSpawnPositions.Add(localCellCenter);
                }
            }
        }

        groundMap.RefreshAllTiles();
        hazardMap.RefreshAllTiles();

        // 2. EXITS & SPAWNS & DOORS
        Transform exitsFolder = GetOrCreateChild(roomInstance.transform, "Exits");
        Transform spawnsFolder = GetOrCreateChild(roomInstance.transform, "Spawns");

        Direction[] dirs = { Direction.North, Direction.South, Direction.West, Direction.East };
        List<Room.DoorSetup> doorSetups = new List<Room.DoorSetup>();

        float halfW = gridWidth * 0.5f;
        float halfH = gridHeight * 0.5f;
        float wallThick = Mathf.Max(1, outerWallThickness);
        float doorSpan = Mathf.Max(2, doorGapSize);

        foreach (Direction dir in dirs)
        {
            GameObject doorBlockerObj = BuildDoorBlockerObject(doorsFolder, dir, wallTileToUse, groundLayer, offsetX, offsetY);

            Transform exitTrans = GetOrCreateChild(exitsFolder, $"Exit_{dir}");
            BoxCollider2D exitBox = exitTrans.GetComponent<BoxCollider2D>();
            if (exitBox == null) exitBox = exitTrans.gameObject.AddComponent<BoxCollider2D>();
            exitBox.isTrigger = true;
            exitBox.offset = Vector2.zero;

            RoomExitTrigger exitTrigger = exitTrans.GetComponent<RoomExitTrigger>();
            if (exitTrigger == null) exitTrigger = exitTrans.gameObject.AddComponent<RoomExitTrigger>();
            SerializeExitTriggerFields(exitTrigger, dir, roomInstance);

            Transform spawnTrans = GetOrCreateChild(spawnsFolder, $"Spawn_{dir}");

            switch (dir)
            {
                case Direction.North:
                    exitTrans.localPosition = new Vector3(0f, halfH - 0.35f, 0f);
                    exitBox.size = new Vector2(doorSpan, 0.7f);
                    spawnTrans.localPosition = new Vector3(0f, halfH - wallThick + 0.5f, 0f);
                    break;

                case Direction.South:
                    exitTrans.localPosition = new Vector3(0f, -halfH + 0.35f, 0f);
                    exitBox.size = new Vector2(doorSpan, 0.7f);
                    spawnTrans.localPosition = new Vector3(0f, -halfH + wallThick - 0.5f, 0f);
                    break;

                case Direction.West:
                    exitTrans.localPosition = new Vector3(-halfW + 0.35f, 0f, 0f);
                    exitBox.size = new Vector2(0.7f, doorSpan);
                    spawnTrans.localPosition = new Vector3(-halfW + wallThick - 0.5f, 0f, 0f);
                    break;

                case Direction.East:
                    exitTrans.localPosition = new Vector3(halfW - 0.35f, 0f, 0f);
                    exitBox.size = new Vector2(0.7f, doorSpan);
                    spawnTrans.localPosition = new Vector3(halfW - wallThick + 0.5f, 0f, 0f);
                    break;
            }

            doorSetups.Add(new Room.DoorSetup
            {
                direction = dir,
                wallBlocker = doorBlockerObj,
                spawnPoint = spawnTrans,
                warningRenderer = null
            });
        }

        // 3. WÜRFEL-SPAWNPUNKTE
        ClearOldGeneratedCubeSpawns(spawnsFolder);

        List<Transform> normalTransforms = new List<Transform>();
        for (int i = 0; i < normalSpawnPositions.Count; i++)
        {
            GameObject sp = new GameObject($"Spawn_Normal_{i + 1}");
            sp.transform.SetParent(spawnsFolder, false);
            sp.transform.localPosition = normalSpawnPositions[i];
            normalTransforms.Add(sp.transform);
        }

        List<Transform> secretTransforms = new List<Transform>();
        for (int i = 0; i < secretSpawnPositions.Count; i++)
        {
            GameObject sp = new GameObject($"Spawn_Secret_{i + 1}");
            sp.transform.SetParent(spawnsFolder, false);
            sp.transform.localPosition = secretSpawnPositions[i];
            secretTransforms.Add(sp.transform);
        }

        // 4. LEVELBOUNDS
        Transform levelBoundsTrans = GetOrCreateChild(roomInstance.transform, "LevelBounds");
        levelBoundsTrans.localPosition = Vector3.zero;
        BoxCollider2D levelBoundsCol = levelBoundsTrans.GetComponent<BoxCollider2D>();
        if (levelBoundsCol == null) levelBoundsCol = levelBoundsTrans.gameObject.AddComponent<BoxCollider2D>();
        levelBoundsCol.isTrigger = true;
        levelBoundsCol.offset = Vector2.zero;
        levelBoundsCol.size = new Vector2(gridWidth, gridHeight);

        // 5. SERIALISIEREN
        SerializedObject roomSO = new SerializedObject(roomInstance);
        roomSO.Update();

        SerializedProperty camBoundsProp = roomSO.FindProperty("cameraBounds");
        if (camBoundsProp != null) camBoundsProp.objectReferenceValue = levelBoundsCol;

        SerializedProperty doorsProp = roomSO.FindProperty("doors");
        if (doorsProp != null)
        {
            doorsProp.ClearArray();
            for (int i = 0; i < doorSetups.Count; i++)
            {
                doorsProp.InsertArrayElementAtIndex(i);
                SerializedProperty dElem = doorsProp.GetArrayElementAtIndex(i);
                dElem.FindPropertyRelative("direction").enumValueIndex = (int)doorSetups[i].direction;
                dElem.FindPropertyRelative("wallBlocker").objectReferenceValue = doorSetups[i].wallBlocker;
                dElem.FindPropertyRelative("spawnPoint").objectReferenceValue = doorSetups[i].spawnPoint;
            }
        }

        FillTransformArrayProperty(roomSO.FindProperty("normalCubeSpawnPoints"), normalTransforms);
        FillTransformArrayProperty(roomSO.FindProperty("secretCubeSpawnPoints"), secretTransforms);

        roomSO.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Baut Door_North/South/West/East unter Grid/Doors so auf, dass die Tür im geschlossenen Zustand
    /// die echte Wand-Tilemap + einen BoxCollider2D (z.B. 4x2) auf Layer 'Ground' besitzt!
    /// </summary>
private GameObject BuildDoorBlockerObject(
        Transform doorsFolder,
        Direction dir,
        TileBase wallTile,
        int groundLayer,
        int offsetX,
        int offsetY)
    {
        Transform doorTrans = GetOrCreateChild(doorsFolder, $"Door_{dir}");
        GameObject doorObj = doorTrans.gameObject;
        doorObj.layer = groundLayer;
        doorTrans.localPosition = Vector3.zero;

        Tilemap doorMap = doorObj.GetComponent<Tilemap>();
        if (doorMap == null) doorMap = doorObj.AddComponent<Tilemap>();

        TilemapRenderer doorRenderer = doorObj.GetComponent<TilemapRenderer>();
        if (doorRenderer == null) doorRenderer = doorObj.AddComponent<TilemapRenderer>();
        doorRenderer.sortingOrder = 1;

        BoxCollider2D doorCollider = doorObj.GetComponent<BoxCollider2D>();
        if (doorCollider == null) doorCollider = doorObj.AddComponent<BoxCollider2D>();
        doorCollider.isTrigger = false;

        doorMap.ClearAllTiles();

        int doorMinX = (gridWidth / 2) - (doorGapSize / 2);
        int doorMaxX = doorMinX + doorGapSize - 1;
        int doorMinY = (gridHeight / 2) - (doorGapSize / 2);
        int doorMaxY = doorMinY + doorGapSize - 1;

        float halfW = gridWidth * 0.5f;
        float halfH = gridHeight * 0.5f;
        float thick = Mathf.Max(1, outerWallThickness);

        if (dir == Direction.North)
        {
            for (int x = doorMinX; x <= doorMaxX; x++)
                for (int y = gridHeight - outerWallThickness; y < gridHeight; y++)
                    doorMap.SetTile(new Vector3Int(x + offsetX, y + offsetY, 0), wallTile);

            doorCollider.offset = new Vector2(0f, halfH - thick * 0.5f);
            doorCollider.size = new Vector2(doorGapSize, thick);
        }
        else if (dir == Direction.South)
        {
            for (int x = doorMinX; x <= doorMaxX; x++)
                for (int y = 0; y < outerWallThickness; y++)
                    doorMap.SetTile(new Vector3Int(x + offsetX, y + offsetY, 0), wallTile);

            doorCollider.offset = new Vector2(0f, -halfH + thick * 0.5f);
            doorCollider.size = new Vector2(doorGapSize, thick);
        }
        else if (dir == Direction.West)
        {
            for (int x = 0; x < outerWallThickness; x++)
                for (int y = doorMinY; y <= doorMaxY; y++)
                    doorMap.SetTile(new Vector3Int(x + offsetX, y + offsetY, 0), wallTile);

            doorCollider.offset = new Vector2(-halfW + thick * 0.5f, 0f);
            doorCollider.size = new Vector2(thick, doorGapSize);
        }
        else if (dir == Direction.East)
        {
            for (int x = gridWidth - outerWallThickness; x < gridWidth; x++)
                for (int y = doorMinY; y <= doorMaxY; y++)
                    doorMap.SetTile(new Vector3Int(x + offsetX, y + offsetY, 0), wallTile);

            doorCollider.offset = new Vector2(halfW - thick * 0.5f, 0f);
            doorCollider.size = new Vector2(thick, doorGapSize);
        }

        doorMap.RefreshAllTiles();
        return doorObj;
    }

    private void SerializeExitTriggerFields(RoomExitTrigger trigger, Direction dir, Room parentRoom)
    {
        SerializedObject triggerSO = new SerializedObject(trigger);
        triggerSO.Update();

        SerializedProperty dirProp = triggerSO.FindProperty("exitDirection");
        if (dirProp != null) dirProp.enumValueIndex = (int)dir;

        SerializedProperty roomProp = triggerSO.FindProperty("parentRoom");
        if (roomProp != null) roomProp.objectReferenceValue = parentRoom;

        triggerSO.ApplyModifiedPropertiesWithoutUndo();
    }

    private void ClearOldGeneratedCubeSpawns(Transform spawnsFolder)
    {
        for (int i = spawnsFolder.childCount - 1; i >= 0; i--)
        {
            Transform child = spawnsFolder.GetChild(i);
            if (child.name.StartsWith("Spawn_Normal_") || child.name.StartsWith("Spawn_Secret_"))
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    // =========================================================================
    // 5. BESTEHENDEN RAUM EINLESEN
    // =========================================================================

    private void LoadFromExistingRoom(Room room)
    {
        ExtractTilesFromReferenceRoom(room);

        Tilemap groundMap = null;
        Tilemap hazardMap = null;

        Tilemap[] maps = room.GetComponentsInChildren<Tilemap>(true);
        foreach (Tilemap tm in maps)
        {
            if (tm.transform.parent != null && tm.transform.parent.name == "Doors") continue;

            if (tm.name.ToLower().Contains("hazard")) hazardMap = tm;
            else if (groundMap == null) groundMap = tm;
        }

        if (groundMap == null)
        {
            EditorUtility.DisplayDialog("Keine Tilemap gefunden", "In diesem Raum wurde keine Haupt-Tilemap gefunden.", "OK");
            return;
        }

        groundMap.CompressBounds();
        BoundsInt b = groundMap.cellBounds;
        if (b.size.x >= 10 && b.size.y >= 10)
        {
            gridWidth = b.size.x;
            gridHeight = b.size.y;
        }

        roomName = room.name;
        cells = new char[gridWidth, gridHeight];
        int offsetX = b.xMin;
        int offsetY = b.yMin;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3Int tilePos = new Vector3Int(x + offsetX, y + offsetY, 0);
                if (hazardMap != null && hazardMap.HasTile(tilePos)) cells[x, y] = '^';
                else if (groundMap.HasTile(tilePos)) cells[x, y] = '#';
                else cells[x, y] = '.';
            }
        }

        SerializedObject so = new SerializedObject(room);
        ReadSpawnsIntoGrid(room.transform, so.FindProperty("normalCubeSpawnPoints"), 'C', offsetX, offsetY);
        ReadSpawnsIntoGrid(room.transform, so.FindProperty("secretCubeSpawnPoints"), 'X', offsetX, offsetY);

        SyncGridToJsonBuffer();
        Repaint();
        Debug.Log($"[RoomPainter] Raum '{room.name}' ({gridWidth}x{gridHeight}) inkl. Tileset erfolgreich geladen!");
    }

    // =========================================================================
    // 6. JSON / KI-IMPORT & EXPORT
    // =========================================================================

    private void DrawJsonSection()
    {
        EditorGUILayout.BeginVertical("box");
        showJsonSection = EditorGUILayout.Foldout(showJsonSection, "4. JSON / ASCII Export & KI-Import (Aufklappen)", true);

        if (showJsonSection)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("JSON aus Raster aktualisieren")) SyncGridToJsonBuffer();
            if (GUILayout.Button("JSON kopieren")) { SyncGridToJsonBuffer(); EditorGUIUtility.systemCopyBuffer = jsonBuffer; }
            if (GUILayout.Button("JSON auf Raster anwenden")) LoadGridFromJsonBuffer(jsonBuffer);
            if (GUILayout.Button("🌲 Beispiel-Raum laden")) LoadSampleCeilingVaultRoom();
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
            doorGap = this.doorGapSize,
            wallThickness = this.outerWallThickness
        };

        for (int y = gridHeight - 1; y >= 0; y--)
        {
            char[] row = new char[gridWidth];
            for (int x = 0; x < gridWidth; x++) row[x] = cells[x, y];
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
            if (data.wallThickness > 0) outerWallThickness = data.wallThickness;

            cells = new char[gridWidth, gridHeight];
            for (int rowIdx = 0; rowIdx < gridHeight; rowIdx++)
            {
                int y = (gridHeight - 1) - rowIdx;
                string line = data.layout[rowIdx];
                for (int x = 0; x < gridWidth; x++) cells[x, y] = (x < line.Length) ? line[x] : '.';
            }
            Repaint();
        }
        catch (Exception ex) { Debug.LogError($"[RoomPainter] JSON-Fehler: {ex.Message}"); }
    }

    // =========================================================================
    // HILFSMETHODEN (ORDNER, TAGS, RASTER)
    // =========================================================================

    private void InitializeGridWithFrame(int w, int h, int doorGap, int wallThick)
    {
        cells = new char[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                cells[x, y] = '.';

        ApplyOuterBorderAndDoors(doorGap, wallThick);
    }

    private void ApplyOuterBorderAndDoors(int doorGap, int wallThick)
    {
        int doorMinX = (gridWidth / 2) - (doorGap / 2);
        int doorMaxX = doorMinX + doorGap - 1;
        int doorMinY = (gridHeight / 2) - (doorGap / 2);
        int doorMaxY = doorMinY + doorGap - 1;

        for (int x = 0; x < gridWidth; x++)
        {
            bool isDoorX = (x >= doorMinX && x <= doorMaxX);
            for (int t = 0; t < wallThick; t++)
            {
                if (t < gridHeight) cells[x, t] = isDoorX ? '.' : '#';
                if (gridHeight - 1 - t >= 0) cells[x, gridHeight - 1 - t] = isDoorX ? '.' : '#';
            }
        }

        for (int y = 0; y < gridHeight; y++)
        {
            bool isDoorY = (y >= doorMinY && y <= doorMaxY);
            for (int t = 0; t < wallThick; t++)
            {
                if (t < gridWidth) cells[t, y] = isDoorY ? '.' : '#';
                if (gridWidth - 1 - t >= 0) cells[gridWidth - 1 - t, y] = isDoorY ? '.' : '#';
            }
        }
    }

    private void ResizeGridKeepContent(int newW, int newH, int newDoor, int newWall)
    {
        char[,] newCells = new char[newW, newH];
        int oldWall = outerWallThickness;

        for (int x = 0; x < newW; x++)
        {
            for (int y = 0; y < newH; y++)
            {
                if (x >= oldWall && x < gridWidth - oldWall && y >= oldWall && y < gridHeight - oldWall &&
                    x < newW - newWall && y < newH - newWall)
                {
                    newCells[x, y] = cells[x, y];
                }
                else
                {
                    newCells[x, y] = '.';
                }
            }
        }

        gridWidth = newW;
        gridHeight = newH;
        doorGapSize = newDoor;
        outerWallThickness = newWall;
        cells = newCells;
        ApplyOuterBorderAndDoors(doorGapSize, outerWallThickness);
    }

    private void FillRect(Vector2Int a, Vector2Int b, char paintChar)
    {
        int minX = Mathf.Min(a.x, b.x); int maxX = Mathf.Max(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y); int maxY = Mathf.Max(a.y, b.y);
        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                cells[x, y] = paintChar;
    }

    private void MirrorHorizontal()
    {
        for (int y = 0; y < gridHeight; y++)
            for (int x = 0; x < gridWidth / 2; x++)
                cells[gridWidth - 1 - x, y] = cells[x, y];
        Repaint();
    }

    private Transform GetOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null) return existing;

        GameObject childObj = new GameObject(childName);
        childObj.transform.SetParent(parent, false);
        return childObj.transform;
    }

private Tilemap GetOrCreateTilemapObject(Transform gridParent, string childName, int layerId, string tagName, bool isTrigger, int sortingOrder)
    {
        Transform mapTrans = GetOrCreateChild(gridParent, childName);
        GameObject mapObj = mapTrans.gameObject;
        mapObj.layer = layerId;

        if (!string.IsNullOrEmpty(tagName))
        {
            try { mapObj.tag = tagName; } catch { }
        }

        Tilemap tm = mapObj.GetComponent<Tilemap>();
        if (tm == null) tm = mapObj.AddComponent<Tilemap>();

        TilemapRenderer tr = mapObj.GetComponent<TilemapRenderer>();
        if (tr == null) tr = mapObj.AddComponent<TilemapRenderer>();
        tr.sortingOrder = sortingOrder;

        TilemapCollider2D col = mapObj.GetComponent<TilemapCollider2D>();
        if (col == null) col = mapObj.AddComponent<TilemapCollider2D>();
        col.isTrigger = isTrigger;

        return tm;
    }

    /// <summary>
    /// Stellt sicher, dass der Tag (z.B. "Hazard") in den ProjectSettings existiert,
    /// damit Unity niemals mit einer Exception abbricht!
    /// </summary>
    private static void EnsureTagExists(string tagName)
    {
        if (string.IsNullOrEmpty(tagName)) return;

        UnityEngine.Object[] asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (asset == null || asset.Length == 0) return;

        SerializedObject so = new SerializedObject(asset[0]);
        SerializedProperty tags = so.FindProperty("tags");
        if (tags == null) return;

        for (int i = 0; i < tags.arraySize; i++)
        {
            if (tags.GetArrayElementAtIndex(i).stringValue == tagName) return;
        }

        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tagName;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Erstellt fehlende Projekt-Unterordner schrittweise über AssetDatabase.CreateFolder.
    /// </summary>
    private static void EnsureAssetFolderExists(string assetFolderPath)
    {
        string normalized = assetFolderPath.Replace("\\", "/").Trim('/');
        string[] parts = normalized.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets") return;

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
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

    private TileBase GetOrCreateFallbackTile(string fileName, Color color)
    {
        EnsureAssetFolderExists("Assets/Editor/GeneratedTiles");
        string tilePath = $"Assets/Editor/GeneratedTiles/{fileName}.asset";
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

        string pngPath = $"Assets/Editor/GeneratedTiles/{fileName}.png";
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
        InitializeGridWithFrame(30, 20, doorGapSize, outerWallThickness);
        FillRect(new Vector2Int(9, 6), new Vector2Int(20, 7), '#');
        FillRect(new Vector2Int(13, 8), new Vector2Int(16, 13), '#');
        FillRect(new Vector2Int(10, outerWallThickness), new Vector2Int(19, outerWallThickness), '^');
        FillRect(new Vector2Int(22, 14), new Vector2Int(27, 14), '#');

        cells[11, 8] = 'C';
        cells[18, 8] = 'C';
        cells[25, 16] = 'X';

        SyncGridToJsonBuffer();
        Repaint();
    }
}
#endif
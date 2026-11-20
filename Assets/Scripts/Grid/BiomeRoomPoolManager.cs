using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

public class BiomeRoomPoolManager : MonoBehaviour
{
    [Serializable]
    public class AttributeRoomSubPool
    {
        public RoomAttributeType attributeType;
        [Tooltip("Alle Raum-Prefabs für dieses Attribut (z.B. aus Forest/Normal oder Forest/Altar).")]
        public List<Room> allRoomPrefabs = new List<Room>();

        // Laufzeit-Zieh-Stapel (Ziehen ohne Zurücklegen + Auto-Refill)
        [NonSerialized]
        public List<Room> remainingDrawPool = new List<Room>();
    }

    [Serializable]
    public class BiomeRoomPool
    {
        public ZoneType biome;
        [Tooltip("Unter-Pools pro Raum-Attribut (None = Normal, Altar, Survival, Mystery, Loot, ...).")]
        public List<AttributeRoomSubPool> attributeSubPools = new List<AttributeRoomSubPool>();
    }

    [Header("Ordner-Synchronisation")]
    [Tooltip("Stammordner, in dem die Biom-Ordner (Forest, Ruins, Ocean, Swamp, ...) liegen.")]
    [SerializeField] private string roomsRootFolder = "Assets/Prefabs/Rooms";
    [Tooltip("Synchronisiert beim Start im Editor automatisch alle Unterordner.")]
    [SerializeField] private bool autoSyncFoldersInEditor = true;

    [Header("Geladene Biom- & Attribut-Pools")]
    [SerializeField] private List<BiomeRoomPool> biomePools = new List<BiomeRoomPool>();

#if UNITY_EDITOR
    [ContextMenu("Biom- & Attribut-Pools jetzt aus Ordnern synchronisieren")]
    public void SyncBiomePoolsFromFolders()
    {
        if (string.IsNullOrWhiteSpace(roomsRootFolder) || !AssetDatabase.IsValidFolder(roomsRootFolder))
        {
            return;
        }

        ZoneType[] supportedBiomes =
        {
            ZoneType.Forest,
            ZoneType.Ruins,
            ZoneType.Ocean,
            ZoneType.Swamp,
            ZoneType.PurpleNightmare,
            ZoneType.BlackVoid,
            ZoneType.Hub
        };

        RoomAttributeType[] allAttributes = (RoomAttributeType[])Enum.GetValues(typeof(RoomAttributeType));

        foreach (ZoneType zone in supportedBiomes)
        {
            BiomeRoomPool biomePool = GetOrCreateBiomePool(zone);
            biomePool.attributeSubPools.Clear();

            string[] biomeFolderNames = GetFolderNamesForBiome(zone);
            foreach (string biomeFolder in biomeFolderNames)
            {
                string fullBiomePath = $"{roomsRootFolder.TrimEnd('/')}/{biomeFolder}";
                if (!AssetDatabase.IsValidFolder(fullBiomePath)) continue;

                // 1. Normale Räume (RoomAttributeType.None)
                ScanFolderIntoSubPool(biomePool, RoomAttributeType.None, $"{fullBiomePath}/Normal", includeSubfolders: true);
                ScanFolderIntoSubPool(biomePool, RoomAttributeType.None, fullBiomePath, includeSubfolders: false);

                // 2. Attribut-spezifische Ordner scannen (Altar, Survival, Mystery, Loot, ...)
                foreach (RoomAttributeType attr in allAttributes)
                {
                    if (attr == RoomAttributeType.None) continue;

                    string attrFolderPath = $"{fullBiomePath}/{attr}";
                    ScanFolderIntoSubPool(biomePool, attr, attrFolderPath, includeSubfolders: true);

                    if (attr == RoomAttributeType.Altar)
                    {
                        ScanFolderIntoSubPool(biomePool, attr, $"{fullBiomePath}/Shrine", includeSubfolders: true);
                    }
                }
            }
        }

        EditorUtility.SetDirty(this);
    }

    private void ScanFolderIntoSubPool(BiomeRoomPool biomePool, RoomAttributeType attrType, string targetFolder, bool includeSubfolders)
    {
        if (!AssetDatabase.IsValidFolder(targetFolder)) return;

        string normalizedTarget = targetFolder.TrimEnd('/').Replace('\\', '/');
        AttributeRoomSubPool subPool = GetOrCreateSubPool(biomePool, attrType);

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { normalizedTarget });
        foreach (string guid in prefabGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            string parentDir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;

            bool isDirectChild = parentDir.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase);
            bool isNestedChild = parentDir.StartsWith(normalizedTarget + "/", StringComparison.OrdinalIgnoreCase);

            if (!isDirectChild && !(includeSubfolders && isNestedChild))
            {
                continue;
            }

            GameObject prefabObj = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabObj == null) continue;

            Room roomComp = prefabObj.GetComponent<Room>();
            if (roomComp != null && !subPool.allRoomPrefabs.Contains(roomComp))
            {
                subPool.allRoomPrefabs.Add(roomComp);
            }
        }
    }

    private string[] GetFolderNamesForBiome(ZoneType zone)
    {
        return zone switch
        {
            ZoneType.Forest => new[] { "Forest", "Wald" },
            ZoneType.Ruins => new[] { "Ruins", "Ruinen" },
            ZoneType.Ocean => new[] { "Ocean", "Meer" },
            ZoneType.Swamp => new[] { "Swamp", "Sumpf" },
            ZoneType.PurpleNightmare => new[] { "PurpleNightmare", "Nightmare", "Albtraum" },
            ZoneType.BlackVoid => new[] { "BlackVoid", "Void", "Leere" },
            ZoneType.Hub => new[] { "Hub", "Start" },
            _ => new[] { zone.ToString() }
        };
    }
#endif

    private BiomeRoomPool GetOrCreateBiomePool(ZoneType zone)
    {
        for (int i = 0; i < biomePools.Count; i++)
        {
            if (biomePools[i] != null && biomePools[i].biome == zone)
            {
                return biomePools[i];
            }
        }

        BiomeRoomPool newPool = new BiomeRoomPool { biome = zone };
        biomePools.Add(newPool);
        return newPool;
    }

    private AttributeRoomSubPool GetOrCreateSubPool(BiomeRoomPool biomePool, RoomAttributeType attrType)
    {
        for (int i = 0; i < biomePool.attributeSubPools.Count; i++)
        {
            if (biomePool.attributeSubPools[i] != null && biomePool.attributeSubPools[i].attributeType == attrType)
            {
                return biomePool.attributeSubPools[i];
            }
        }

        AttributeRoomSubPool newSubPool = new AttributeRoomSubPool { attributeType = attrType };
        biomePool.attributeSubPools.Add(newSubPool);
        return newSubPool;
    }

    private AttributeRoomSubPool FindSubPool(ZoneType biome, RoomAttributeType attrType)
    {
        for (int i = 0; i < biomePools.Count; i++)
        {
            BiomeRoomPool pool = biomePools[i];
            if (pool == null || pool.biome != biome || pool.attributeSubPools == null) continue;

            for (int s = 0; s < pool.attributeSubPools.Count; s++)
            {
                AttributeRoomSubPool sub = pool.attributeSubPools[s];
                if (sub != null && sub.attributeType == attrType && sub.allRoomPrefabs != null)
                {
                    sub.allRoomPrefabs.RemoveAll(r => r == null);
                    if (sub.allRoomPrefabs.Count > 0)
                    {
                        return sub;
                    }
                }
            }
        }
        return null;
    }

    public void ResetAndShuffleAllPools()
    {
#if UNITY_EDITOR
        if (autoSyncFoldersInEditor)
        {
            SyncBiomePoolsFromFolders();
        }
#endif
        for (int i = 0; i < biomePools.Count; i++)
        {
            BiomeRoomPool pool = biomePools[i];
            if (pool == null || pool.attributeSubPools == null) continue;

            for (int s = 0; s < pool.attributeSubPools.Count; s++)
            {
                RefillAndShuffleSubPool(pool.attributeSubPools[s]);
            }
        }
    }

    private void RefillAndShuffleSubPool(AttributeRoomSubPool subPool)
    {
        if (subPool == null || subPool.allRoomPrefabs == null) return;

        subPool.allRoomPrefabs.RemoveAll(r => r == null);
        if (subPool.remainingDrawPool == null)
        {
            subPool.remainingDrawPool = new List<Room>();
        }

        subPool.remainingDrawPool.Clear();
        if (subPool.allRoomPrefabs.Count == 0) return;

        subPool.remainingDrawPool.AddRange(subPool.allRoomPrefabs);

        // Fisher-Yates Shuffle
        for (int i = subPool.remainingDrawPool.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            Room tmp = subPool.remainingDrawPool[i];
            subPool.remainingDrawPool[i] = subPool.remainingDrawPool[swapIndex];
            subPool.remainingDrawPool[swapIndex] = tmp;
        }
    }

    /// <summary>
    /// Zieht einen Raum aus dem passenden Biom- & Attribut-Pool (inkl. Auto-Refill & sauberer Fallback-Kette).
    /// </summary>
    public Room DrawRoomForCell(ZoneType primaryBiome, ZoneType fallbackResonanceBiome, RoomAttributeType attribute, Room fallbackDefaultRoom)
    {
        // 1. Exakter Treffer: Gewünschtes Biom + gewünschtes Attribut (z.B. Forest + Altar)
        Room drawn = DrawFromSubPool(FindSubPool(primaryBiome, attribute));
        if (drawn != null) return drawn;

        // 2. Falls auf dem Rand gebaut wurde: Ausgewürfeltes Resonanz-Biom + gewünschtes Attribut
        if (fallbackResonanceBiome != primaryBiome)
        {
            drawn = DrawFromSubPool(FindSubPool(fallbackResonanceBiome, attribute));
            if (drawn != null) return drawn;
        }

        // 3. Fallback auf normale Räume dieses Bioms
        if (attribute != RoomAttributeType.None)
        {
            drawn = DrawFromSubPool(FindSubPool(primaryBiome, RoomAttributeType.None));
            if (drawn != null) return drawn;

            if (fallbackResonanceBiome != primaryBiome)
            {
                drawn = DrawFromSubPool(FindSubPool(fallbackResonanceBiome, RoomAttributeType.None));
                if (drawn != null) return drawn;
            }
        }

        return fallbackDefaultRoom;
    }

    private Room DrawFromSubPool(AttributeRoomSubPool subPool)
    {
        if (subPool == null || subPool.allRoomPrefabs == null || subPool.allRoomPrefabs.Count == 0)
        {
            return null;
        }

        if (subPool.remainingDrawPool == null)
        {
            subPool.remainingDrawPool = new List<Room>();
        }

        subPool.remainingDrawPool.RemoveAll(r => r == null);

        if (subPool.remainingDrawPool.Count == 0)
        {
            RefillAndShuffleSubPool(subPool);
        }

        if (subPool.remainingDrawPool.Count == 0)
        {
            return null;
        }

        int lastIdx = subPool.remainingDrawPool.Count - 1;
        Room result = subPool.remainingDrawPool[lastIdx];
        subPool.remainingDrawPool.RemoveAt(lastIdx);
        return result;
    }
}
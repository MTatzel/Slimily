using System;
using UnityEngine;

public class Room : MonoBehaviour
{
    public static event Action<Room, Direction> OnExitTriggered;

    [Header("Grid Koordinate")]
    [Tooltip("Koordinate dieses Raumes im MacroGrid")]
    [SerializeField] private Vector2Int gridCoordinate;
    public Vector2Int GridCoordinate
    {
        get => gridCoordinate;
        set => gridCoordinate = value;
    }

    [System.Serializable]
    public struct DoorSetup
    {
        public Direction direction;
        [Tooltip("Das Tür-Prefab in der 6-Tile-Lücke (Sprite + Collider auf Layer 'Ground')")]
        public GameObject wallBlocker;  
        [Tooltip("Einstiegspunkt, wenn man aus dieser Richtung kommt")]
        public Transform spawnPoint;    
    }

    [Header("Exits & Spawns")]
    [SerializeField] private DoorSetup[] doors;

    [Header("Kamera-Begrenzung")]
    [Tooltip("Der Trigger-Collider (Box oder Polygon) für CinemachineConfiner2D")]
    [SerializeField] private Collider2D cameraBounds;
    public Collider2D CameraBounds => cameraBounds;

    // --- Editor-Testwerte ---
    [Header("Debug / Erzwingen")]
    [SerializeField] private bool debugForceOpenNorth;
    [SerializeField] private bool debugForceOpenEast;
    [SerializeField] private bool debugForceOpenSouth;
    [SerializeField] private bool debugForceOpenWest;

    private void Awake()
    {
        // Falls im Inspector nicht zugewiesen, sucht das Skript nach einem Trigger-Collider
        if (cameraBounds == null)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            foreach (var col in colliders)
            {
                if (col.isTrigger)
                {
                    cameraBounds = col;
                    break;
                }
            }
        }
    }

    // Wird vom RoomManager aufgerufen, wenn der Raum auf die Bühne gesetzt wird
    public void SetDoorOpen(Direction direction, bool isOpen)
    {
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].direction == direction)
            {
                if (doors[i].wallBlocker != null)
                {
                    // Wenn offen: Blocker aus. Wenn zu: Blocker an (schließt die 6 Tiles)
                    doors[i].wallBlocker.SetActive(!isOpen);
                }
                return;
            }
        }
    }

    public Transform GetSpawnPoint(Direction enterFromDirection)
    {
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].direction == enterFromDirection)
            {
                return doors[i].spawnPoint;
            }
        }
        return transform;
    }

    // Hilfsmethode: Wird vom Trigger am Rand aufgerufen
    public void TriggerExit(Direction dir)
    {
        OnExitTriggered?.Invoke(this, dir);
    }

#if UNITY_EDITOR
    // Erlaubt das Testen direkt über Haken im Inspector während des Editierens oder Play-Modes
    private void OnValidate()
    {
        SetDoorOpen(Direction.North, debugForceOpenNorth);
        SetDoorOpen(Direction.East, debugForceOpenEast);
        SetDoorOpen(Direction.South, debugForceOpenSouth);
        SetDoorOpen(Direction.West, debugForceOpenWest);
    }
#endif
}
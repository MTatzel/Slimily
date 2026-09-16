using System;
using UnityEngine;

public class Room : MonoBehaviour
{
    public static event Action<Direction> OnExitTriggered;

    [System.Serializable]
    public struct DoorSetup
    {
        public Direction direction;
        public GameObject wallBlocker;  // Schließt den Ausgang, wenn kein Nachbar existiert
        public Transform spawnPoint;    // Einstiegspunkt, wenn man aus dieser Richtung kommt
    }

    [Header("Exits & Spawns")]
    [SerializeField] private DoorSetup[] doors;

    // Wird vom RoomManager aufgerufen, wenn der Raum auf die Bühne gesetzt wird
    public void SetDoorOpen(Direction direction, bool isOpen)
    {
        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i].direction == direction)
            {
                if (doors[i].wallBlocker != null)
                {
                    // Wenn offen: Blocker deinstallieren/deaktivieren. Wenn zu: Blocker an
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
        OnExitTriggered?.Invoke(dir);
    }
}
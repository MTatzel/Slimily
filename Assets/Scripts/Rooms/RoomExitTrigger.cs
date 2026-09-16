using UnityEngine;

public class RoomExitTrigger : MonoBehaviour
{
    [SerializeField] private Direction exitDirection;
    [SerializeField] private Room parentRoom;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            parentRoom.TriggerExit(exitDirection);
        }
    }
}
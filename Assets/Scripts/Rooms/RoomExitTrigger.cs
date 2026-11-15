using UnityEngine;

public class RoomExitTrigger : MonoBehaviour
{
    [SerializeField] private Direction exitDirection;
    [SerializeField] private Room parentRoom;

    public Direction ExitDirection => exitDirection;
    public Collider2D TriggerCollider => GetComponent<Collider2D>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            parentRoom.TriggerExit(exitDirection);
        }
    }
}
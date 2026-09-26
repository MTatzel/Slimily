using UnityEngine;

public enum DoorDirection
{
    North,
    East,
    South,
    West
}

[SelectionBase]
public class RoomDoor : MonoBehaviour
{
    [Header("Identifikation")]
    [SerializeField] private DoorDirection direction;
    public DoorDirection Direction => direction;

    [Header("Komponenten")]
    [SerializeField] private Collider2D doorCollider;
    [SerializeField] private SpriteRenderer doorRenderer;

    [Header("Status (Debug / Initial)")]
    [SerializeField] private bool isOpen;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        // Falls im Inspector nicht zugewiesen, automatisch holen
        if (doorCollider == null) doorCollider = GetComponent<Collider2D>();
        if (doorRenderer == null) doorRenderer = GetComponent<SpriteRenderer>();

        UpdateVisualsAndPhysics();
    }

    /// <summary>
    /// Öffnet oder schließt die Tür (wird von der Raum-/Makro-Logik aufgerufen).
    /// </summary>
    public void SetOpen(bool open)
    {
        if (isOpen == open) return;

        isOpen = open;
        UpdateVisualsAndPhysics();
    }

    private void UpdateVisualsAndPhysics()
    {
        if (doorCollider != null)
        {
            // Zu = Collider aktiv (Slime kann klettern/stehen)
            // Offen = Collider inaktiv (Slime kann durchlaufen)
            doorCollider.enabled = !isOpen;
        }

        if (doorRenderer != null)
        {
            // Zu = Tür sichtbar
            // Offen = Ausgeblendet (später hier Animation abspielen)
            doorRenderer.enabled = !isOpen;
        }
    }

#if UNITY_EDITOR
    // Erlaubt das direkte Umschalten im Inspector während der Bearbeitung
    private void OnValidate()
    {
        if (doorCollider == null) doorCollider = GetComponent<Collider2D>();
        if (doorRenderer == null) doorRenderer = GetComponent<SpriteRenderer>();
        UpdateVisualsAndPhysics();
    }
#endif
}
using UnityEngine;
using UnityEngine.InputSystem;

// Das sind unsere drei festen Spielzustände
public enum GameState
{
    FreeRoam,
    Inventory,
    Placement,
    EnemyTurn
}

public class GameManager : MonoBehaviour
{
    // Hier speichern wir den aktuellen Zustand (Start ist immer FreeRoam)
    public GameState currentState = GameState.FreeRoam;

    public GameObject inventoryScreen; // Referenz auf unser UI-Gruppen-Objekt
    public InventoryManager inventoryManager;

    void Start()
    {
        // Zum Spielstart stellen wir sicher, dass das Inventar unsichtbar ist
        inventoryScreen.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        // Wir nutzen die TAB-Taste (oder 'I'), um das Inventar zu öffnen/schließen
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleInventory();
        }
    }

    void ToggleInventory()
{
    if (currentState == GameState.Placement) return;

    if (currentState == GameState.FreeRoam)
    {
        currentState = GameState.Inventory;
        inventoryScreen.SetActive(true);

        // Erzwingt die sofortige Berechnung aller Layout-Positionen vor der Cursor-Ausrichtung
        Canvas.ForceUpdateCanvases();

        if (inventoryManager != null)
        {
            inventoryManager.ResetSelection();
        }
    }
    else if (currentState == GameState.Inventory)
    {
        currentState = GameState.FreeRoam;
        inventoryScreen.SetActive(false);
    }
}
    
    // Eine kleine Hilfsfunktion für unsere anderen Skripte
    public void ChangeState(GameState newState)
    {
        currentState = newState;
    }
}
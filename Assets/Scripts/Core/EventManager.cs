using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class EventManager : MonoBehaviour
{
    public GridManager gridManager;
    public MacroSlimeMarker slime;
    public GameManager gameManager;
    public PlacementManager placementManager;
    public InventoryManager inventoryManager;
    public BossController boss;

    void Update()   
    {
        // 1. Niemals im Platformer (FreeRoam) auf Space/Enter für Zug-Ende reagieren!
        // Der Zug soll nur auf der Makro-Ebene (Inventory) beendet werden können.
        if (gameManager != null && gameManager.currentState != GameState.Inventory) return;
        if (Keyboard.current == null) return;

        // Vorerst: Space komplett entfernen, damit Sprünge sicher frei bleiben.
        // Optional auf eine separate Taste legen (z. B. 'E') oder ganz auskommentieren:
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartCoroutine(TurnSequenceRoutine());
        }
    }

    // Der zentrale Runden-Ablauf
    IEnumerator TurnSequenceRoutine()
    {
        // 1. Spieler-Eingaben sperren
        if (gameManager != null)
        {
            gameManager.ChangeState(GameState.EnemyTurn);
        }

        Debug.Log("--- SPIELER-ZUG BEENDET | GEGNER-PHASE STARTET ---");

        // 2. Boss ausführen und warten, bis er fertig ist
        if (boss != null)
        {
            yield return StartCoroutine(boss.ExecuteBossTurn());
        }

        // 3. Runden-Reset für den Spieler
        if (placementManager != null)
        {
            placementManager.ResetTurnPlacements();
        }

        if (inventoryManager != null)
        {
            inventoryManager.CheckAndRefillIfEmpty();
        }

        // 4. Spieler wieder freigeben (zurück auf Makro-Ebene)
        if (gameManager != null)
        {
            gameManager.ChangeState(GameState.Inventory);
        }

        Debug.Log("--- GEGNER-PHASE BEENDET | SPIELER IST DRAN ---");
    }
}
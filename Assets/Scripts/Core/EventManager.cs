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
        // Zug kann nur beendet werden, wenn der Spieler frei agieren darf
        if (gameManager != null && gameManager.currentState != GameState.FreeRoam) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
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

        // 4. Spieler wieder freigeben
        if (gameManager != null)
        {
            gameManager.ChangeState(GameState.FreeRoam);
        }

        Debug.Log("--- GEGNER-PHASE BEENDET | SPIELER IST DRAN ---");
    }
}
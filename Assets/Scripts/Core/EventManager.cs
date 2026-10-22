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
    public BossBase boss;

    void Update()
    {
        // Ein Zug kann im MacroPlan oder FreeRoam per E-Taste beendet werden
        if (gameManager == null) return;
        if (gameManager.currentState != GameState.MacroPlan && gameManager.currentState != GameState.FreeRoam) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartCoroutine(TurnSequenceRoutine());
        }
    }

    public IEnumerator TurnSequenceRoutine()
    {
        Debug.Log("--- ZUG BEENDET | WECHSEL IN GEGNERPHASE ---");

        // 1. Falls der Spieler im Platformer war: Zuerst nach oben ins Makro schalten
        if (gameManager != null && gameManager.currentState == GameState.FreeRoam)
        {
            gameManager.ChangeState(GameState.EnemyTurn);
            // Kleiner Puffer für die Kamerafahrt
            yield return new WaitForSeconds(0.4f);
        }
        else if (gameManager != null)
        {
            gameManager.ChangeState(GameState.EnemyTurn);
        }

        // 2. Banner "GEGNERPHASE" anzeigen
        if (TurnBannerUI.Instance != null)
        {
            yield return StartCoroutine(TurnBannerUI.Instance.ShowBannerRoutine("GEGNERPHASE", isEnemyTurn: true, duration: 0.8f));
        }
        else
        {
            yield return new WaitForSeconds(0.4f);
        }

        // 3. Boss-Aktion ausführen
        if (boss != null)
        {
            yield return StartCoroutine(boss.ExecuteTurn());
        }

        // 4. Runden-Reset
        if (placementManager != null)
        {
            placementManager.ResetTurnPlacements();
        }

        if (inventoryManager != null)
        {
            inventoryManager.CheckAndRefillIfEmpty();
        }

        // 5. Banner "DEIN ZUG" anzeigen
        if (TurnBannerUI.Instance != null)
        {
            yield return StartCoroutine(TurnBannerUI.Instance.ShowBannerRoutine("DEIN ZUG", isEnemyTurn: false, duration: 0.7f));
        }

        // 6. Spieler startet die neue Runde im Baumodus (MacroPlan)
        if (gameManager != null)
        {
            gameManager.ChangeState(GameState.MacroPlan);
        }

        Debug.Log("--- SPIELER IST WIEDER AM ZUG (BAUMODUS) ---");
    }
}
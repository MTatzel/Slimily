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

    private bool isTurnRunning = false;

    void Update()
    {
        if (gameManager == null || isTurnRunning) return;
        if (gameManager.currentState != GameState.MacroPlan && gameManager.currentState != GameState.FreeRoam) return;
        if (Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TriggerEndTurn(forcedByZeroFocus: false);
        }
    }

    public void TriggerEndTurn(bool forcedByZeroFocus = false)
    {
        if (isTurnRunning) return;
        StartCoroutine(TurnSequenceRoutine(forcedByZeroFocus));
    }

    public IEnumerator TurnSequenceRoutine(bool forcedByZeroFocus = false)
    {
        if (isTurnRunning) yield break;
        isTurnRunning = true;

        if (gameManager == null) gameManager = GameManager.Instance;
        if (gridManager == null) gridManager = GridManager.Instance;

        if (forcedByZeroFocus)
        {
            Debug.Log("--- ZWANGSSTOPP (0 FOKUS) | SOFORTIGER WECHSEL IN GEGNERPHASE ---");
            if (TurnBannerUI.Instance != null)
            {
                yield return StartCoroutine(TurnBannerUI.Instance.ShowBannerRoutine("ZWANGSSTOPP! (0 FOKUS)", isEnemyTurn: true, duration: 0.9f));
            }
            else
            {
                yield return new WaitForSeconds(0.4f);
            }
        }
        else
        {
            Debug.Log("--- ZUG BEENDET | WECHSEL IN GEGNERPHASE ---");
        }

        // 1. Falls der Spieler im Platformer war: Zuerst nach oben ins Makro schalten
        if (gameManager != null && gameManager.currentState == GameState.FreeRoam)
        {
            gameManager.ChangeState(GameState.EnemyTurn);
            yield return new WaitForSeconds(0.45f);
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

        // 3. Boss-Aktion ausführen (Einschläge auf markierten Feldern + Bewegung + neue Warnung)
        if (boss != null)
        {
            yield return StartCoroutine(boss.ExecuteTurn());
        }

        // 4. Albtraumleiste am Rundenende erhöhen (Zeit + evtl. lila/schwarzes Feld unter dem Slime)
        ApplyEndOfTurnNightmare();

        // Falls der Run durch 100% Albtraum zurückgesetzt wird, hier abbrechen
        if (gameManager != null && gameManager.CurrentNightmare >= gameManager.MaxNightmare)
        {
            isTurnRunning = false;
            yield break;
        }

        // 5. Runden-Reset & Fokus wieder auffüllen
        if (placementManager != null)
        {
            placementManager.ResetTurnPlacements();
        }

        if (gameManager != null)
        {
            gameManager.RefillFocus();
        }

        if (inventoryManager != null)
        {
            inventoryManager.CheckAndRefillIfEmpty();
        }

        // 6. Banner "DEIN ZUG" anzeigen
        if (TurnBannerUI.Instance != null)
        {
            yield return StartCoroutine(TurnBannerUI.Instance.ShowBannerRoutine("DEIN ZUG", isEnemyTurn: false, duration: 0.7f));
        }

        // 7. Spieler startet die neue Runde im Baumodus (MacroPlan)
        if (gameManager != null)
        {
            gameManager.ChangeState(GameState.MacroPlan);
        }

        isTurnRunning = false;
        Debug.Log("--- SPIELER IST WIEDER AM ZUG (BAUMODUS) ---");
    }

    private void ApplyEndOfTurnNightmare()
    {
        if (gameManager == null) return;

        // Passiver Runden-Anstieg
        if (gameManager.NightmarePerTurn > 0f)
        {
            gameManager.AddNightmare(gameManager.NightmarePerTurn, "Rundenverlauf");
        }

        // Prüfen, ob der Slime bei Zugende auf einem lila oder schwarzen Feld steht
        if (MacroGridManager.Instance != null && gridManager != null && gridManager.grid != null)
        {
            Vector2Int slimeCoord = MacroGridManager.Instance.CurrentCoordinate;
            if (slimeCoord.x >= 0 && slimeCoord.x < gridManager.width &&
                slimeCoord.y >= 0 && slimeCoord.y < gridManager.height)
            {
                GridCell cell = gridManager.grid[slimeCoord.x, slimeCoord.y];
                if (cell != null)
                {
                    if (cell.zoneType == ZoneType.PurpleNightmare)
                    {
                        gameManager.AddNightmare(gameManager.NightmareOnPurpleTile, "Aufenthalt im lila Albtraum-Rand");
                    }
                    else if (cell.zoneType == ZoneType.BlackVoid)
                    {
                        gameManager.AddNightmare(gameManager.NightmareOnBlackVoidTile, "Aufenthalt im schwarzen Rand");
                    }
                }
            }
        }
    }
}
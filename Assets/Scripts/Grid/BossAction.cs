using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public abstract class BossAction : MonoBehaviour
{
    [Header("Cast-Einstellungen")]
    public int totalWindupTurns = 1;     // Wie viele Runden Vorwarnzeit (1 = nächste Runde, 3 = nach 3 Runden)
    [HideInInspector] public int currentWindup = 0;

    [Header("Visuelle Telegrafie")]
    public Color telegraphColor = new Color(1f, 0.5f, 0f); // Standard: Orange

    [HideInInspector] public List<Vector2Int> targetedCells = new List<Vector2Int>();
    public bool isExecuting => currentWindup > 0;

    // Startet das Vorbereiten / Casten
    public virtual void StartCast(GridManager gridManager, SlimeController slime)
    {
        currentWindup = totalWindupTurns;
        targetedCells = CalculateTargetCells(gridManager, slime);
        
        // Warnung auf dem Grid anschalten
        gridManager.SetCellsWarning(targetedCells, true);
    }

    // Wird jede Runde im Boss-Turn aufgerufen
    public virtual IEnumerator TickTurn(GridManager gridManager, SlimeController slime)
    {
        if (currentWindup > 1)
        {
            currentWindup--; // Zählt z.B. von 3 auf 2 oder 2 auf 1 runter (Boss castet weiter)
            yield break;
        }

        if (currentWindup == 1)
        {
            currentWindup = 0;
            // Warnung abschalten
            gridManager.SetCellsWarning(targetedCells, false);

            // Eigentlichen Effekt ausführen
            yield return StartCoroutine(ExecuteActionEffect(gridManager, slime));
            targetedCells.Clear();
        }
    }

    public virtual void CancelCast(GridManager gridManager)
    {
        currentWindup = 0;
        gridManager.SetCellsWarning(targetedCells, false);
        targetedCells.Clear();
    }

    protected abstract List<Vector2Int> CalculateTargetCells(GridManager gridManager, SlimeController slime);
    protected abstract IEnumerator ExecuteActionEffect(GridManager gridManager, SlimeController slime);
}
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BossController : MonoBehaviour
{
    public GridManager gridManager;
    public GameManager gameManager;
    public SlimeController slime;

    // Linke untere Ecke des 2x2-Bosses auf dem Grid
    public int originX = 35;
    public int originY = 35;

    private List<Vector2Int> occupiedOffsets = new List<Vector2Int>
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1)
    };

    void Start()
    {
        UpdateWorldPosition();
        OccupyGridCells(true);
    }

    // Setzt die 4 Kacheln auf besetzt/unbesetzt
    public void OccupyGridCells(bool occupy)
    {
        if (gridManager == null || gridManager.grid == null) return;

        foreach (var offset in occupiedOffsets)
        {
            int checkX = originX + offset.x;
            int checkY = originY + offset.y;

            if (checkX >= 0 && checkX < gridManager.width && checkY >= 0 && checkY < gridManager.height)
            {
                GridCell cell = gridManager.grid[checkX, checkY];

                if (occupy)
                {
                    cell.SetBlocked();   // Setzt Zustand auf Blocked & färbt Dunkelblau
                }
                else
                {
                    cell.DestroyBlock(); // Setzt Zustand zurück auf Empty & färbt Weiß
                }
            }
        }
    }

    // Zentriert das 2x2 Sprite exakt über den 4 Feldern
    public void UpdateWorldPosition()
    {
        if (gridManager == null) return;

        // + 0.5f sorgt dafür, dass das Sprite genau im Zentrum der 4 Felder liegt
        float worldX = (originX + 0.5f) * gridManager.offset;
        float worldY = (originY + 0.5f) * gridManager.offset;

        transform.position = new Vector3(worldX, worldY, -2f); // -2f, damit er vor Slime und Grid rendert
    }

    // Hier läuft die Rundenlogik des Bosses ab
    public IEnumerator ExecuteBossTurn()
    {
        Debug.Log("Boss beginnt seinen Zug...");

        // Kurze Pause für das Spielgefühl (0.5s warten)
        yield return new WaitForSeconds(0.5f);

        // BEISPIEL-AKTION: 1 Feld in Richtung Slime rücken (falls möglich)
        MoveTowardsSlime();

        yield return new WaitForSeconds(0.3f);
        Debug.Log("Boss hat seinen Zug beendet.");
    }

    void MoveTowardsSlime()
    {
        if (slime == null) return;

        int stepX = 0;
        int stepY = 0;

        if (slime.currentX < originX) stepX = -1;
        else if (slime.currentX > originX) stepX = 1;

        if (slime.currentY < originY) stepY = -1;
        else if (slime.currentY > originY) stepY = 1;

        // Nur eine Achse pro Zug bewegen (z. B. primär X, sonst Y)
        if (stepX != 0) stepY = 0;

        int targetX = originX + stepX;
        int targetY = originY + stepY;

        // Prüfen, ob die 4 Zielfelder innerhalb des Grids liegen
        if (targetX >= 0 && targetX + 1 < gridManager.width &&
            targetY >= 0 && targetY + 1 < gridManager.height)
        {
            // Alte Felder freigeben
            OccupyGridCells(false);

            originX = targetX;
            originY = targetY;

            // Neue Felder blockieren & Sprite bewegen
            OccupyGridCells(true);
            UpdateWorldPosition();
            Debug.Log($"Boss bewegt sich auf Position: {originX}, {originY}");
        }
    }
}
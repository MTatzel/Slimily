using UnityEngine;
using UnityEngine.InputSystem;

public class SlimeController : MonoBehaviour
{
    public GridManager gridManager;
    public GameManager gameManager;

    // Speichert die aktuellen Grid-Koordinaten des Slimes
    public int currentX;
    public int currentY;

    void Start()
    {
        // Wir setzen den Slime zum Start exakt auf das Zentrum (Feld 25, 25)
        currentX = gridManager.width / 2;
        currentY = gridManager.height / 2;
        
        UpdateWorldPosition();
    }

    void Update()
    {
        if (gameManager.currentState != GameState.FreeRoam) return;
        
        if (Keyboard.current == null) return;

        // Pfeiltasten abfragen und die gewünschte Richtung übergeben
        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame) TryMove(0, 1);
        if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame) TryMove(0, -1);
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame) TryMove(-1, 0);
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame) TryMove(1, 0);
    }

    void TryMove(int dx, int dy)
    {
        int targetX = currentX + dx;
        int targetY = currentY + dy;

        // 1. Sicherheitscheck: Verlassen wir das 50x50 Raster?
        if (targetX < 0 || targetX >= gridManager.width || targetY < 0 || targetY >= gridManager.height)
            return;

        // 2. Logik-Check: Ist das Zielfeld ein gebauter Block?
        // Erinnerung: isWalkable == false bedeutet bei uns, dass ein Block gesetzt wurde
        if (gridManager.grid[targetX, targetY].IsWalkableForSlime)
        {
            currentX = targetX;
            currentY = targetY;
            UpdateWorldPosition();
        }
    }

    void UpdateWorldPosition()
    {
        // Rechnet die Grid-Koordinaten wieder in echte Unity-Weltkoordinaten um (Offset beachten!)
        float worldX = currentX * gridManager.offset;
        float worldY = currentY * gridManager.offset;
        
        // Z-Achse auf -1, als zusätzliche Sicherheit, damit er vor dem Grid rendert
        transform.position = new Vector3(worldX, worldY, -1f);
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossController : BossBase
{
    [Header("Bewegung & Raster")]
    [SerializeField] private Vector2Int currentGridPosition;
    [SerializeField] private float moveStepDuration = 0.3f;

    [Header("Angriffs-Einstellungen")]
    [SerializeField] private BossHazardType attackType = BossHazardType.DestroyRoom;
    [SerializeField] private int telegraphTurns = 1;

    private bool isInitialized = false;

    private void Start()
    {
        InitializePositionIfNeeded();
    }

    /// <summary>
    /// Liest die Startposition aus der aktuellen Transform-Position aus
    /// </summary>
    public void InitializePositionIfNeeded()
    {
        if (isInitialized) return;

        float offset = 1.1f;
        if (GridManager.Instance != null && GridManager.Instance.offset > 0.01f)
        {
            offset = GridManager.Instance.offset;
        }

        int startX = Mathf.RoundToInt(transform.localPosition.x / offset);
        int startY = Mathf.RoundToInt(transform.localPosition.y / offset);
        currentGridPosition = new Vector2Int(startX, startY);

        isInitialized = true;
    }

    public override IEnumerator ExecuteTurn()
    {
        InitializePositionIfNeeded();

        // 1. PHASE: Fällige Einschläge ausführen (rot aufblitzen, Raum zerstören/belegen)
        yield return StartCoroutine(ResolveTelegraphsRoutine());

        // 2. PHASE: Boss bewegt sich 1 Schritt in Richtung Slime
        yield return StartCoroutine(MoveTowardsSlimeRoutine());

        // 3. PHASE: Nächsten Angriff für die Folgerunde telegrafieren
        TelegraphNextAttack();

        // Kurze Pause zum Abschluss, damit das Geschehen auf der Karte gut lesbar bleibt
        yield return new WaitForSeconds(0.4f);
    }

    private IEnumerator MoveTowardsSlimeRoutine()
    {
        Vector2Int targetCoord = currentGridPosition;

        // Ziel ist der Raum, in dem sich der Slime gerade befindet
        if (MacroGridManager.Instance != null)
        {
            targetCoord = MacroGridManager.Instance.CurrentCoordinate;
        }

        Vector2Int delta = targetCoord - currentGridPosition;
        Vector2Int stepDirection = Vector2Int.zero;

        // Schrittweise Annäherung: Achse mit der größeren Distanz bevorzugen
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            if (delta.x != 0) stepDirection.x = delta.x > 0 ? 1 : -1;
            else if (delta.y != 0) stepDirection.y = delta.y > 0 ? 1 : -1;
        }
        else
        {
            if (delta.y != 0) stepDirection.y = delta.y > 0 ? 1 : -1;
            else if (delta.x != 0) stepDirection.x = delta.x > 0 ? 1 : -1;
        }

        if (stepDirection != Vector2Int.zero)
        {
            Vector2Int nextGridPos = currentGridPosition + stepDirection;
            float offset = GridManager.Instance != null ? GridManager.Instance.offset : 1.1f;

            Vector3 startWorld = transform.localPosition;
            Vector3 targetWorld = new Vector3(nextGridPos.x * offset, nextGridPos.y * offset, startWorld.z);

            float elapsed = 0f;
            while (elapsed < moveStepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / moveStepDuration);
                transform.localPosition = Vector3.Lerp(startWorld, targetWorld, t);
                yield return null;
            }

            transform.localPosition = targetWorld;
            currentGridPosition = nextGridPos;
        }

        yield return new WaitForSeconds(0.1f);
    }

    private void TelegraphNextAttack()
    {
        // Sucht sich als Angriffsziel das Feld direkt vor sich in Richtung Slime aus
        Vector2Int slimeCoord = currentGridPosition;
        if (MacroGridManager.Instance != null)
        {
            slimeCoord = MacroGridManager.Instance.CurrentCoordinate;
        }

        Vector2Int diff = slimeCoord - currentGridPosition;
        Vector2Int aimDir = Vector2Int.zero;

        if (Mathf.Abs(diff.x) >= Mathf.Abs(diff.y))
        {
            aimDir.x = diff.x >= 0 ? 1 : -1;
        }
        else
        {
            aimDir.y = diff.y >= 0 ? 1 : -1;
        }

        Vector2Int strikeTarget = currentGridPosition + aimDir;

        // Kündigt die Kachel für 1 Runde an
        Telegraph(new List<Vector2Int> { strikeTarget }, attackType, telegraphTurns);
    }
}
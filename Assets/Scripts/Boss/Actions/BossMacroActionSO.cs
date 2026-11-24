using UnityEngine;

/// <summary>
/// Datenpaket, das jeder Boss-Aktion beim Ausführen mitgegeben wird.
/// </summary>
public class BossActionContext
{
    public BossProfile Profile;
    public BossPhaseConfig CurrentPhase;
    public Vector2Int BossCoord;
    public Vector2Int TargetCoord;
    public Vector2Int SlimeCoord;
    public Vector2Int HubCoord;
    public int TotalActionsPerformed;
}

/// <summary>
/// Abstrakte Basis für alle modularen Boss-Aktionen auf dem Macro-Grid.
/// Neue verrückte Mechaniken einfach hiervon ableiten!
/// </summary>
public abstract class BossMacroActionSO : ScriptableObject
{
    [Header("Basis-Info")]
    public string actionName = "Neue Boss-Aktion";

    /// <summary>
    /// Optional: Bestimmt, welches Feld der Boss für diese Aktion anvisiert (Telegraphing).
    /// Gibt false zurück, wenn die Aktion kein bestimmtes Zielfeld braucht.
    /// </summary>
    public virtual bool TrySelectTarget(BossActionContext ctx, out Vector2Int chosenTarget)
    {
        chosenTarget = ctx.SlimeCoord;
        return false;
    }

    /// <summary>
    /// Wird ausgeführt, wenn der Tick-Countdown des Bosses auf 0 fällt.
    /// </summary>
    public abstract void Execute(BossActionContext ctx);
}
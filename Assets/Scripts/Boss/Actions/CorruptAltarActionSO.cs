using UnityEngine;

[CreateAssetMenu(fileName = "Action_CorruptAltar", menuName = "Slimily/Boss/Actions/Corrupt Altar")]
public class CorruptAltarActionSO : BossMacroActionSO
{
    [Tooltip("Fallback-Radius um den Boss, falls auf dem Zielfeld kein Altar steht.")]
    [Range(0, 5)] public int fallbackRadius = 1;

    public override bool TrySelectTarget(BossActionContext ctx, out Vector2Int chosenTarget)
    {
        chosenTarget = ctx.BossCoord;
        if (AltarManager.Instance == null) return false;

        float bestDist = float.MaxValue;
        bool found = false;

        foreach (var kvp in AltarManager.Instance.Altars)
        {
            if (kvp.Value == null || kvp.Value.status == AltarStatus.Corrupted) continue;
            float d = Vector2Int.Distance(ctx.BossCoord, kvp.Key);
            if (d < bestDist)
            {
                bestDist = d;
                chosenTarget = kvp.Key;
                found = true;
            }
        }

        return found;
    }

    public override void Execute(BossActionContext ctx)
    {
        if (AltarManager.Instance == null) return;

        bool corruptedDirect = AltarManager.Instance.TryCorruptAltar(ctx.TargetCoord);
        if (!corruptedDirect && fallbackRadius > 0)
        {
            AltarManager.Instance.CorruptAltarsInRadius(ctx.BossCoord, fallbackRadius);
        }
    }
}
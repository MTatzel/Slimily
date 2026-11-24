using UnityEngine;

[CreateAssetMenu(fileName = "Action_DestroyRoom", menuName = "Slimily/Boss/Actions/Destroy Room")]
public class DestroyRoomActionSO : BossMacroActionSO
{
    public override bool TrySelectTarget(BossActionContext ctx, out Vector2Int chosenTarget)
    {
        chosenTarget = ctx.BossCoord;
        if (GridManager.Instance == null || GridManager.Instance.grid == null || MacroGridManager.Instance == null)
            return false;

        float bestScore = float.MaxValue;
        bool foundRoom = false;

        for (int x = 0; x < GridManager.Instance.width; x++)
        {
            for (int y = 0; y < GridManager.Instance.height; y++)
            {
                Vector2Int c = new Vector2Int(x, y);
                if (c == ctx.HubCoord) continue;
                if (!MacroGridManager.Instance.HasRoom(c)) continue;

                float score = Vector2Int.Distance(ctx.BossCoord, c) * 1.2f + Vector2Int.Distance(ctx.SlimeCoord, c) * 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    chosenTarget = c;
                    foundRoom = true;
                }
            }
        }

        return foundRoom;
    }

    public override void Execute(BossActionContext ctx)
    {
        if (MacroGridManager.Instance != null && MacroGridManager.Instance.HasRoom(ctx.TargetCoord))
        {
            Debug.Log($"[BossAction] {ctx.Profile.displayName} zerstört Raum {ctx.TargetCoord}!");
            MacroGridManager.Instance.RemoveRoomAt(ctx.TargetCoord);
        }
    }
}
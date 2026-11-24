using UnityEngine;

[CreateAssetMenu(fileName = "Action_HuntPlayer", menuName = "Slimily/Boss/Actions/Hunt Player")]
public class HuntPlayerActionSO : BossMacroActionSO
{
    [Tooltip("Zusätzlicher Albtraum, wenn der Boss den Spieler auf dem Grid stellt.")]
    [Range(0f, 30f)] public float nightmareOnCatch = 10f;

    public override bool TrySelectTarget(BossActionContext ctx, out Vector2Int chosenTarget)
    {
        chosenTarget = ctx.SlimeCoord;
        return true;
    }

    public override void Execute(BossActionContext ctx)
    {
        if (ctx.BossCoord == ctx.SlimeCoord)
        {
            Debug.LogWarning($"[BossAction] {ctx.Profile.displayName} hat den Slime bei {ctx.BossCoord} gestellt!");
            if (nightmareOnCatch > 0f && GameManager.Instance != null)
            {
                GameManager.Instance.AddNightmare(nightmareOnCatch, $"Vom Boss '{ctx.Profile.displayName}' gestellt");
            }
        }
    }
}
using UnityEngine;

public class AltarBiomeBonusHandler : MonoBehaviour
{
    [Header("Biom-Boni beim Erwecken (Optional)")]
    [Tooltip("Wenn AUS, geben alle Altäre unabhängig vom Biom nur die Standard-Belohnung.")]
    public bool enableBiomeBonuses = true;

    [Header("Forest (Wald) - Extra Beruhigung")]
    [Tooltip("Zusätzliche Albtraum-Reduktion bei einem Wald-Altar.")]
    [Range(0f, 30f)] public float forestExtraNightmareHeal = 8f;

    [Header("Ruins (Ruinen) - Zeit-Verzögerung")]
    [Tooltip("Verzögert den nächsten Boss-Angriff um X Ticks.")]
    [Range(0, 6)] public int ruinsBossDelayTicks = 2;

    [Header("Ocean (Meer) - Extra Beute")]
    [Tooltip("Chance (0-100%), beim Erwecken einen zweiten Bonus-Würfel zu erhalten.")]
    [Range(0f, 100f)] public float oceanBonusCubeChance = 50f;

    [Header("Swamp (Sumpf) - Stabilisierung")]
    [Tooltip("Zusätzliche Albtraum-Reduktion + 1 Tick Boss-Verzögerung.")]
    [Range(0f, 20f)] public float swampExtraNightmareHeal = 4f;
    [Range(0, 4)] public int swampBossDelayTicks = 1;

    /// <summary>
    /// Wendet den Biom-spezifischen Zusatzbonus beim erstmaligen Erwecken an
    /// und gibt einen kurzen Text für das Feedback-Popup zurück.
    /// </summary>
    public string ApplyAwakenBonus(ZoneType biome, Vector2Int coord)
    {
        if (!enableBiomeBonuses) return string.Empty;

        switch (biome)
        {
            case ZoneType.Forest:
                if (forestExtraNightmareHeal > 0f && GameManager.Instance != null)
                {
                    GameManager.Instance.AddNightmare(-forestExtraNightmareHeal, $"Wald-Altar Bonus ({coord})");
                    return $"Wald-Bonus: -{forestExtraNightmareHeal:0}% Albtraum";
                }
                break;

            case ZoneType.Ruins:
                if (ruinsBossDelayTicks > 0 && GameManager.Instance != null)
                {
                    GameManager.Instance.DelayNextBossEvent(ruinsBossDelayTicks, $"Ruinen-Altar Bonus ({coord})");
                    return $"Ruinen-Bonus: Boss +{ruinsBossDelayTicks} Ticks verzögert";
                }
                break;

            case ZoneType.Ocean:
                if (oceanBonusCubeChance > 0f && Random.Range(0f, 100f) <= oceanBonusCubeChance)
                {
                    InventoryManager inv = GameManager.Instance != null
                        ? GameManager.Instance.inventoryManager
                        : FindAnyObjectByType<InventoryManager>();

                    if (inv != null && inv.CanAddCube() && inv.AddFoundCubeToInventory(isSecretBonus: true))
                    {
                        return "Meer-Bonus: +1 Extra-Würfel";
                    }
                }
                break;

            case ZoneType.Swamp:
                bool appliedSwamp = false;
                if (swampExtraNightmareHeal > 0f && GameManager.Instance != null)
                {
                    GameManager.Instance.AddNightmare(-swampExtraNightmareHeal, $"Sumpf-Altar Bonus ({coord})");
                    appliedSwamp = true;
                }
                if (swampBossDelayTicks > 0 && GameManager.Instance != null)
                {
                    GameManager.Instance.DelayNextBossEvent(swampBossDelayTicks, $"Sumpf-Altar Bonus ({coord})");
                    appliedSwamp = true;
                }
                if (appliedSwamp)
                {
                    return $"Sumpf-Bonus: -{swampExtraNightmareHeal:0}% & +{swampBossDelayTicks} Tick";
                }
                break;
        }

        return string.Empty;
    }
}
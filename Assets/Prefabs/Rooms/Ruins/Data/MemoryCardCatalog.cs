using System.Collections.Generic;
using UnityEngine;

public static class MemoryCardCatalog
{
    public static List<ShapeData> CreateStarterDeck()
    {
        return new List<ShapeData>
        {
            CreateMemory_CursedCorridor(),
            CreateMemory_CursedCorridor(),
            CreateMemory_GlacierBridge(),
            CreateMemory_GlacierBridge(),
            CreateMemory_SafeDetourShrine(),
            CreateMemory_SafeDetourShrine(),
            CreateMemory_WaveStandard(),
            CreateMemory_ShadowSpringboardZeroCost(),
            CreateMemory_ArchitectAxis(),
            CreateMemory_MentalFocus(),
            CreateMemory_TimeAnchor(),
            CreateMemory_PurifyThreats()
        };
    }

    public static ShapeData CreateMemory_CursedCorridor()
    {
        ShapeData card = new ShapeData(
            "Erinnerung: Glut-Pfad",
            cost: 1,
            desc: "-1 Zähler: Gerade Linie für schnellen Vorstoß, aber 2 Räume brennen (Feuer).",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.Fire));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2), BlockModifier.Fire));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 2)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
        return card;
    }

    public static ShapeData CreateMemory_GlacierBridge()
    {
        ShapeData card = new ShapeData(
            "Erinnerung: Frost-Brücke",
            cost: 1,
            desc: "-1 Zähler: Weites T-Netz mit 2 rutschigen Eis-Sektoren an der Spitze.",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2), BlockModifier.Ice));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 3), BlockModifier.Ice));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 3)));
        return card;
    }

    public static ShapeData CreateMemory_SafeDetourShrine()
    {
        ShapeData card = new ShapeData(
            "Erinnerung: Licht-Oase",
            cost: 1,
            desc: "-1 Zähler: Verwinkelte Z-Treppe ohne Gefahren + 1 Schrein (-8% Albtraum).",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.Shrine));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 3)));
        return card;
    }

    public static ShapeData CreateMemory_WaveStandard()
    {
        ShapeData card = new ShapeData(
            "Erinnerung: Ruhige Welle",
            cost: 1,
            desc: "-1 Zähler: Ausgewogenes 6er-Netz ohne gefährliche Modifikatoren.",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(2, 3)));
        return card;
    }

    public static ShapeData CreateMemory_ShadowSpringboardZeroCost()
    {
        ShapeData card = new ShapeData(
            "Flüchtige Sternschnuppe",
            cost: 0,
            desc: "0 ZÄHLER (GRATIS)! Bringt dich ohne Zeitverlust voran, hat aber 3 Gefahren-Räume.",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.Poison));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2), BlockModifier.Fire));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 3), BlockModifier.Poison));
        return card;
    }

    public static ShapeData CreateMemory_ArchitectAxis()
    {
        ShapeData card = new ShapeData(
            "Tiefe Erinnerung: Pfeiler",
            cost: 2,
            desc: "-2 Zähler: Komplett sichere Gerade + 1 Schrein (-8% Albtraum).",
            CardCategory.CubeShape
        );
        card.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 2), BlockModifier.Shrine));
        card.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
        card.cells.Add(new ShapeCell(new Vector2Int(-1, 1)));
        card.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
        return card;
    }

    public static ShapeData CreateMemory_MentalFocus()
    {
        ShapeData skill = new ShapeData(
            "IMPULS: Sternenschauer",
            cost: 0,
            desc: "0 Zähler: Lässt sofort +2 neue Erinnerungen von links einschweben.",
            CardCategory.TacticalSkill
        )
        {
            skillType = TacticalSkillType.DrawCards,
            skillPower = 2
        };
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 0), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(1, 0), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(1, 1), BlockModifier.SkillIcon));
        return skill;
    }

    public static ShapeData CreateMemory_TimeAnchor()
    {
        ShapeData skill = new ShapeData(
            "IMPULS: Zeit-Anker",
            cost: 1,
            desc: "-1 Zähler: Erhöht den Countdown ALLER Gefahren-Symbole oben rechts um +3!",
            CardCategory.TacticalSkill
        )
        {
            skillType = TacticalSkillType.DelayBossTimeline,
            skillPower = 3
        };
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 0), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(-1, 0), BlockModifier.Ice));
        skill.cells.Add(new ShapeCell(new Vector2Int(1, 0), BlockModifier.Ice));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, -1), BlockModifier.SkillIcon));
        return skill;
    }

    public static ShapeData CreateMemory_PurifyThreats()
    {
        ShapeData skill = new ShapeData(
            "IMPULS: Sternenlicht",
            cost: 0,
            desc: "0 Zähler: Löscht aktive Boss-Markierungen auf dem Grid & heilt -5% Albtraum.",
            CardCategory.TacticalSkill
        )
        {
            skillType = TacticalSkillType.PurifyBossThreats,
            skillPower = 5
        };
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 0), BlockModifier.Shrine));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, 1), BlockModifier.SkillIcon));
        skill.cells.Add(new ShapeCell(new Vector2Int(0, -1), BlockModifier.SkillIcon));
        return skill;
    }
}
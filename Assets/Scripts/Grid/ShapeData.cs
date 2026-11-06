using System;
using System.Collections.Generic;
using UnityEngine;

public enum BlockModifier
{
    None,
    Ice,        // Schneeflocke (Rutschig / Non-Stick)
    Fire,       // Feuer (Gefahr im Raum)
    Electric,   // Blitz
    Poison,     // Gift
    Shrine,     // Positiv: Senkt Albtraum beim Erschließen/Bauen
    SkillIcon   // Visueller Marker für Taktik-/Skill-Karten in der Vorschau
}

public enum CardCategory
{
    CubeShape,      // 6er-Faltwürfel zum Platzieren auf dem Grid
    TacticalSkill   // Sofort-Fähigkeit (Karten ziehen, Zeit-Anker, Läuterung, Heilung)
}

public enum TacticalSkillType
{
    None,
    DrawCards,          // Zieht sofort X Karten nach
    DelayBossTimeline,  // Verzögert den nächsten Boss-Puls um X Ticks
    PurifyBossThreats,  // Entfernt aktive Boss-Warnungen auf dem Grid
    ReduceNightmare     // Senkt die globale Albtraumleiste um X%
}

[System.Serializable]
public class ShapeCell
{
    public Vector2Int localPosition;
    public List<BlockModifier> modifiers = new List<BlockModifier>();

    public ShapeCell(Vector2Int pos)
    {
        localPosition = pos;
    }

    public ShapeCell(Vector2Int pos, BlockModifier initialModifier)
    {
        localPosition = pos;
        if (initialModifier != BlockModifier.None)
        {
            modifiers.Add(initialModifier);
        }
    }
}

[System.Serializable]
public class ShapeData
{
    public string shapeName;
    public CardCategory category = CardCategory.CubeShape;
    public int tickCost = 1;
    public string description = "";

    // Für Würfel-Karten (und als Mini-Symbol für Skills in der bestehenden UI)
    public List<ShapeCell> cells = new List<ShapeCell>();

    // Für Taktik-/Skill-Karten
    public TacticalSkillType skillType = TacticalSkillType.None;
    public int skillPower = 0;

    public ShapeData(string name, int cost = 1, string desc = "", CardCategory cardCategory = CardCategory.CubeShape)
    {
        shapeName = name;
        tickCost = cost;
        description = desc;
        category = cardCategory;
    }

    public ShapeData Clone()
    {
        ShapeData copy = new ShapeData(shapeName, tickCost, description, category)
        {
            skillType = this.skillType,
            skillPower = this.skillPower
        };

        foreach (ShapeCell cell in cells)
        {
            ShapeCell cellCopy = new ShapeCell(cell.localPosition);
            cellCopy.modifiers.AddRange(cell.modifiers);
            copy.cells.Add(cellCopy);
        }
        return copy;
    }

    public void RotateClockwise()
    {
        if (category != CardCategory.CubeShape) return;

        foreach (ShapeCell cell in cells)
        {
            int oldX = cell.localPosition.x;
            int oldY = cell.localPosition.y;
            cell.localPosition = new Vector2Int(oldY, -oldX);
        }
    }

    public int CountModifier(BlockModifier mod)
    {
        int count = 0;
        foreach (ShapeCell cell in cells)
        {
            if (cell.modifiers.Contains(mod)) count++;
        }
        return count;
    }

    public Vector2Int GetDimensions()
    {
        if (cells.Count == 0) return Vector2Int.zero;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (ShapeCell cell in cells)
        {
            minX = Mathf.Min(minX, cell.localPosition.x);
            maxX = Mathf.Max(maxX, cell.localPosition.x);
            minY = Mathf.Min(minY, cell.localPosition.y);
            maxY = Mathf.Max(maxY, cell.localPosition.y);
        }

        return new Vector2Int(maxX - minX + 1, maxY - minY + 1);
    }
}
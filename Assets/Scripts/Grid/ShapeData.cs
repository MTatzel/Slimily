using System;
using System.Collections.Generic;
using UnityEngine;

public enum BlockModifier
{
    None,
    Ice,
    Fire,
    Electric,
    Poison,
    Shrine,
    SkillIcon
}

public enum CardCategory
{
    CubeShape,
    TacticalSkill
}

public enum TacticalSkillType
{
    None,
    DrawCards,
    DelayBossTimeline,
    PurifyBossThreats,
    ReduceNightmare
}

[Serializable]
public class ShapeCell
{
    public Vector2Int localPosition;
    public ZoneType assignedZone = ZoneType.Forest;
    public bool hasAssignedZone = false;
    public List<BlockModifier> modifiers = new List<BlockModifier>();

    public ShapeCell() { }

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

[Serializable]
public class ShapeData
{
    public string shapeName = "6er-Würfel";
    public CardCategory category = CardCategory.CubeShape;
    [Range(0, 5)] public int tickCost = 1;
    [TextArea(1, 3)] public string description = "";

    public List<ShapeCell> cells = new List<ShapeCell>();

    public TacticalSkillType skillType = TacticalSkillType.None;
    public int skillPower = 0;

    public ShapeData() { }

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
            ShapeCell cellCopy = new ShapeCell(cell.localPosition)
            {
                assignedZone = cell.assignedZone,
                hasAssignedZone = cell.hasAssignedZone
            };
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

[CreateAssetMenu(fileName = "Card_New", menuName = "Slimily/Card Asset")]
public class CardAsset : ScriptableObject
{
    public ShapeData cardData = new ShapeData();

    [ContextMenu("Fülle mit Standard-Kreuz (6 Blöcke)")]
    private void FillDefaultCross()
    {
        cardData.cells.Clear();
        cardData.cells.Add(new ShapeCell(new Vector2Int(0, 0)));
        cardData.cells.Add(new ShapeCell(new Vector2Int(0, 1)));
        cardData.cells.Add(new ShapeCell(new Vector2Int(0, 2)));
        cardData.cells.Add(new ShapeCell(new Vector2Int(0, 3)));
        cardData.cells.Add(new ShapeCell(new Vector2Int(-1, 2)));
        cardData.cells.Add(new ShapeCell(new Vector2Int(1, 2)));
    }
}
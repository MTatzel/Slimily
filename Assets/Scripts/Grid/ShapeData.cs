using System;
using System.Collections.Generic;
using UnityEngine;

// 1. Alle verfügbaren Modifikatoren als Enum definieren
public enum BlockModifier
{
    None,
    Ice,        // Schneeflocke
    Fire,       // Feuer
    Electric,   // Blitz
    Poison      // Gift
}

// 2. Ein einzelner Block innerhalb einer Form
[System.Serializable]
public class ShapeCell
{
    public Vector2Int localPosition;
    
    // Eine Liste, damit später problemlos mehrere Mods pro Feld möglich sind
    public List<BlockModifier> modifiers = new List<BlockModifier>();

    // Konstruktor für eine Zelle ohne Modifikator
    public ShapeCell(Vector2Int pos)
    {
        localPosition = pos;
    }

    // Konstruktor für eine Zelle mit direktem Modifikator
    public ShapeCell(Vector2Int pos, BlockModifier initialModifier)
    {
        localPosition = pos;
        if (initialModifier != BlockModifier.None)
        {
            modifiers.Add(initialModifier);
        }
    }
}

// 3. Die Gesamtform, die im Inventar gespeichert wird
[System.Serializable]
public class ShapeData
{
    public string shapeName;
    public List<ShapeCell> cells = new List<ShapeCell>();

    public ShapeData(string name)
    {
        shapeName = name;
    }

    // Erstellt eine tiefe Kopie der Form (z. B. beim Platzieren oder Klonen)
    public ShapeData Clone()
    {
        ShapeData copy = new ShapeData(shapeName);
        foreach (ShapeCell cell in cells)
        {
            ShapeCell cellCopy = new ShapeCell(cell.localPosition);
            cellCopy.modifiers.AddRange(cell.modifiers);
            copy.cells.Add(cellCopy);
        }
        return copy;
    }

    // Rotiert alle Blöcke der Form um 90 Grad im Uhrzeigersinn
    public void RotateClockwise()
    {
        foreach (ShapeCell cell in cells)
        {
            int oldX = cell.localPosition.x;
            int oldY = cell.localPosition.y;
            cell.localPosition = new Vector2Int(oldY, -oldX);
        }
    }

    // Hilfsfunktion: Berechnet die Bounding Box (nützlich für die UI-Zentrierung)
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
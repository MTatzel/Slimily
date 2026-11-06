using UnityEngine;
using UnityEngine.UI;

public class ShapePreviewUI : MonoBehaviour
{
    public GameObject uiBlockPrefab;
    public RectTransform container;

    public void DisplayShape(ShapeData shape, float blockSize = 24f, float spacing = 2f)
    {
        Clear();

        if (shape == null || shape.cells.Count == 0 || container == null || uiBlockPrefab == null)
            return;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (ShapeCell cell in shape.cells)
        {
            minX = Mathf.Min(minX, cell.localPosition.x);
            maxX = Mathf.Max(maxX, cell.localPosition.x);
            minY = Mathf.Min(minY, cell.localPosition.y);
            maxY = Mathf.Max(maxY, cell.localPosition.y);
        }

        float totalWidth = (maxX - minX + 1) * (blockSize + spacing) - spacing;
        float totalHeight = (maxY - minY + 1) * (blockSize + spacing) - spacing;

        float startOffsetX = -totalWidth / 2f + (blockSize / 2f);
        float startOffsetY = -totalHeight / 2f + (blockSize / 2f);

        foreach (ShapeCell cell in shape.cells)
        {
            GameObject block = Instantiate(uiBlockPrefab, container);
            RectTransform rt = block.GetComponent<RectTransform>();

            rt.sizeDelta = new Vector2(blockSize, blockSize);

            float posX = startOffsetX + (cell.localPosition.x - minX) * (blockSize + spacing);
            float posY = startOffsetY + (cell.localPosition.y - minY) * (blockSize + spacing);
            rt.anchoredPosition = new Vector2(posX, posY);

            Image img = block.GetComponent<Image>();
            if (img != null)
            {
                if (cell.modifiers.Contains(BlockModifier.SkillIcon))
                {
                    img.color = new Color(1f, 0.82f, 0.22f); // Goldgelb für Taktik-/Skill-Karten
                }
                else if (cell.modifiers.Contains(BlockModifier.Shrine))
                {
                    img.color = new Color(0.2f, 0.95f, 0.55f); // Leuchtendes Heil-Grün für Schreine
                }
                else if (cell.modifiers.Contains(BlockModifier.Ice))
                {
                    img.color = new Color(0.4f, 0.8f, 1f); // Eisblau
                }
                else if (cell.modifiers.Contains(BlockModifier.Fire))
                {
                    img.color = new Color(1f, 0.3f, 0.2f); // Feuriges Rot
                }
                else if (cell.modifiers.Contains(BlockModifier.Poison))
                {
                    img.color = new Color(0.75f, 0.3f, 0.95f); // Gift-Violett
                }
                else
                {
                    img.color = Color.white;
                }
            }
        }
    }

    public void Clear()
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }
}
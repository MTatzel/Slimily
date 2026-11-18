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
                    img.color = new Color(1f, 0.82f, 0.22f);
                }
                else if (cell.hasAssignedZone && GridManager.Instance != null)
                {
                    img.color = GridManager.Instance.GetZoneColor(cell.assignedZone);
                }
                else
                {
                    img.color = Color.white;
                }
            }

            // Symbol/Attribut (Sanduhr, ?, Altar, Truhe) direkt auf dem UI-Block anzeigen
            if (cell.attribute != RoomAttributeType.None && RoomAttributeManager.Instance != null)
            {
                Sprite attrSprite = RoomAttributeManager.Instance.GetIcon(cell.attribute);
                if (attrSprite != null)
                {
                    GameObject iconObj = new GameObject("AttributeIcon", typeof(RectTransform), typeof(Image));
                    iconObj.transform.SetParent(rt, false);

                    RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                    iconRt.anchorMin = Vector2.zero;
                    iconRt.anchorMax = Vector2.one;
                    float pad = Mathf.Max(1f, blockSize * 0.1f);
                    iconRt.offsetMin = new Vector2(pad, pad);
                    iconRt.offsetMax = new Vector2(-pad, -pad);

                    Image iconImg = iconObj.GetComponent<Image>();
                    iconImg.sprite = attrSprite;
                    iconImg.color = RoomAttributeManager.Instance.GetIconColor(cell.attribute);
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
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
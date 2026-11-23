using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

internal sealed class InventoryCraftHoldTarget : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private InventoryCraftingPanel owner;
    private ItemData recipe;
    private RectTransform track;
    private RectTransform fill;

    internal void Bind(InventoryCraftingPanel panel, ItemData rowRecipe)
    {
        owner = panel;
        recipe = rowRecipe;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && owner != null)
        {
            owner.BeginPointerCraftHold(this, recipe, eventData);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && owner != null)
        {
            owner.EndPointerCraftHold(this, eventData.pointerId);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.EndPointerCraftHold(this, eventData.pointerId);
        }
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.CancelCraftHoldOn(this);
        }
    }

    internal void ShowProgress(float progress)
    {
        if (progress <= 0f)
        {
            if (track != null)
            {
                track.gameObject.SetActive(false);
            }

            return;
        }

        if (track == null)
        {
            track = CreateStrip("合成进度底线", transform, new Color(0.08f, 0.09f, 0.07f, 0.95f));
            track.anchorMin = Vector2.zero;
            track.anchorMax = Vector2.right;
            track.offsetMin = new Vector2(8f, 4f);
            track.offsetMax = new Vector2(-8f, 8f);

            fill = CreateStrip("合成进度", track, new Color(0.86f, 0.72f, 0.45f, 1f));
            fill.anchorMin = Vector2.zero;
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
        }

        track.gameObject.SetActive(true);
        fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateStrip(string objectName, Transform parent, Color color)
    {
        GameObject strip = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        strip.transform.SetParent(parent, false);
        Image image = strip.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }
}

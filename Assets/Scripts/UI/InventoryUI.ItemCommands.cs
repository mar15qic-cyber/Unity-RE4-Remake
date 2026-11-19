using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;

public partial class InventoryUI
{
    private const int ItemCommandCapacity = 7;
    private readonly Button[] itemCommandButtons = new Button[ItemCommandCapacity];
    private int itemCommandCount;
    private Texture2D itemCommandIcons;

    private void PresentItemCommands(GridItem item, Vector2 pointer)
    {
        var parent = contextMenu.parent as RectTransform;
        if (parent == null)
        {
            return;
        }
        if (itemCommandButtons[0] == null)
        {
            InitializeItemCommands();
        }

        itemCommandCount = 0;
        bool weapon = item.item.itemType == ItemType.Weapon && item.item.weaponType != WeaponType.Knife;
        if (weapon)
        {
            AddItemCommand("装备", 0, () =>
            {
                HideContextMenu();
                if (InventorySceneLoader.HasInstance)
                {
                    InventorySceneLoader.Instance.EquipAndClose(item.item.weaponType);
                }
            });
            AddItemCommand("零件", 1, null);
        }
        else if (item.item.itemType != ItemType.Weapon
            && InventoryManager.Instance != null && InventoryManager.Instance.CanUseItem(item.item))
        {
            AddItemCommand(ResolveUseText(item.item), 0, HandleContextUseButtonClicked);
        }

        if (TryGetComponent<InventoryCraftingPanel>(out var craftingPanel))
        {
            var recipe = craftingPanel.FindRecipeForIngredient(item.item);
            if (recipe != null)
            {
                AddItemCommand("合成", 1, () =>
                {
                    HideContextMenu();
                    craftingPanel.ShowIngredientRecipe(recipe);
                });
            }
        }

        if (ItemInspectionUI.HasInstance && item.item.GetInspectionModelPrefab() != null)
        {
            AddItemCommand("检视", 2, () =>
            {
                HideContextMenu();
                ItemInspectionUI.Instance.Show(item.item);
            });
        }

        if (StorageBoxManager.HasInstance)
        {
            var player = LeonController.Instance;
            bool canStore = StorageBoxManager.Instance.IsOpen
                || weapon && player != null && player.currentWeapon != item.item.weaponType;
            if (canStore)
            {
                AddItemCommand("移至寄存箱", 3, HandleContextStoreButtonClicked);
            }
            else if (weapon)
            {
                AddItemCommand("移至寄存箱", 3, null);
            }
        }

        if (weapon)
        {
            AddItemCommand("登记快捷栏", 4, () =>
            {
                HideContextMenu();
                RegisterWeaponToWheel(item);
            });
        }
        AddItemCommand("丢弃", 5, HandleContextDiscardButtonClicked);
        for (int index = itemCommandCount; index < ItemCommandCapacity; index++)
        {
            itemCommandButtons[index].gameObject.SetActive(false);
        }

        const float rowHeight = 37f;
        const float width = 210f;
        contextMenu.SetAsLastSibling();
        contextMenu.sizeDelta = new Vector2(width, rowHeight * itemCommandCount);
        Vector2 origin;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, pointer, null, out origin);
        if (gridView != null && gridView.GetItemWidget(item) is RectTransform widget)
        {
            var corners = new Vector3[4];
            widget.GetWorldCorners(corners);
            Vector3 upperRight = parent.InverseTransformPoint(corners[2]);
            origin = new Vector2(upperRight.x + 2f, upperRight.y);
            if (origin.x + width > parent.rect.xMax)
            {
                origin.x = parent.InverseTransformPoint(corners[1]).x - width - 2f;
            }
        }
        origin.x = Mathf.Clamp(origin.x, parent.rect.xMin, Mathf.Max(parent.rect.xMin, parent.rect.xMax - width));
        origin.y = Mathf.Clamp(origin.y, Mathf.Min(parent.rect.yMax, parent.rect.yMin + contextMenu.rect.height), parent.rect.yMax);
        contextMenu.localPosition = origin;
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(itemCommandButtons[0].gameObject);
        }
    }

    private void InitializeItemCommands()
    {
        foreach (Transform child in contextMenu)
        {
            child.gameObject.SetActive(false);
        }
        if (contextMenu.TryGetComponent<LayoutGroup>(out var layout))
        {
            layout.enabled = false;
        }
        if (contextMenu.TryGetComponent<Shadow>(out var shadow))
        {
            shadow.enabled = false;
        }
        if (contextMenu.TryGetComponent<Image>(out var panel))
        {
            panel.sprite = null;
            panel.color = new Color(.045f, .043f, .04f, .93f);
        }

        itemCommandIcons = Resources.Load<Texture2D>("UI/ItemCommandIcons");
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (ctxUseButtonText != null && ctxUseButtonText.font != null)
        {
            font = ctxUseButtonText.font;
        }
        for (int index = 0; index < ItemCommandCapacity; index++)
        {
            var row = new GameObject("物品菜单项", typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)row.transform;
            rect.SetParent(contextMenu, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = new Vector2(0f, 37f);
            rect.anchoredPosition = new Vector2(0f, -37f * index);

            var background = row.GetComponent<Image>();
            var button = row.GetComponent<Button>();
            background.color = Color.white;
            background.sprite = actionButtonHoverSprite;
            button.targetGraphic = background;
            button.colors = new ColorBlock
            {
                normalColor = new Color(0, 0, 0, .01f),
                highlightedColor = new Color(.70f, .68f, .61f, .81f),
                selectedColor = new Color(.70f, .68f, .61f, .81f),
                pressedColor = new Color(.55f, .54f, .48f, .8f),
                disabledColor = new Color(0, 0, 0, .01f),
                colorMultiplier = 1f,
                fadeDuration = 0f
            };
            background.canvasRenderer.SetColor(button.colors.normalColor);
            var trigger = row.AddComponent<EventTrigger>();
            var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ =>
            {
                if (button.interactable && EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                }
            });
            trigger.triggers.Add(hover);

            var icon = new GameObject("图标", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            icon.transform.SetParent(rect, false);
            icon.rectTransform.anchorMin = new Vector2(0f, .5f);
            icon.rectTransform.anchorMax = icon.rectTransform.anchorMin;
            icon.rectTransform.anchoredPosition = new Vector2(23f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(23f, 23f);
            icon.texture = itemCommandIcons;
            icon.enabled = itemCommandIcons != null;
            icon.raycastTarget = false;

            var label = new GameObject("命令文字", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(rect, false);
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.offsetMin = new Vector2(42f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);
            label.font = font;
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 18;
            label.raycastTarget = false;
            itemCommandButtons[index] = button;
        }
    }

    private void AddItemCommand(string caption, int iconIndex, UnityAction invoke)
    {
        Button button = itemCommandButtons[itemCommandCount++];
        button.gameObject.SetActive(true);
        button.interactable = invoke != null;
        button.onClick.RemoveAllListeners();
        var label = button.GetComponentInChildren<Text>();
        var icon = button.GetComponentInChildren<RawImage>();
        Color color = new Color(.92f, .91f, .87f);
        if (invoke == null)
        {
            color = new Color(.42f, .41f, .38f);
        }
        label.text = caption;
        label.color = color;
        icon.color = color;
        icon.uvRect = new Rect(iconIndex / 6f, 0f, 1f / 6f, 1f);
        if (invoke != null)
        {
            button.onClick.AddListener(invoke);
        }
    }
}

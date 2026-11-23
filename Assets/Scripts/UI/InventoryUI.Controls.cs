using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class InventoryUI
{
    private InputAction inventoryMoveAction;
    private InputAction inventoryConfirmAction;
    internal InputAction InventoryConfirmAction => inventoryConfirmAction;
    private RectTransform controlHints;
    private bool craftingBackdropVisible;
    private bool keyboardItemMove;
    private Vector2 keyboardMovePointer;

    private bool CanUseItemShortcuts => Interactive && !craftingBackdropVisible
        && (!GameUIManager.HasInstance || GameUIManager.Instance.currentState == GameUIManager.State.Inventory);

    private bool IsItemMenuOpen => contextMenu != null && contextMenu.gameObject.activeSelf;

    internal bool CanSortItems => CanUseItemShortcuts && draggingItem == null && !IsItemMenuOpen;

    private void UpdateItemShortcuts()
    {
        if (!CanUseItemShortcuts || Mouse.current == null)
        {
            return;
        }

        Vector2 pointer = Mouse.current.position.ReadValue();
        if (inventoryMoveAction != null && inventoryMoveAction.WasPressedThisFrame()
            && draggingItem == null && hoveredGridItem != null && !IsItemMenuOpen)
        {
            BeginItemMove(hoveredGridItem, pointer);
            keyboardItemMove = draggingItem != null;
            keyboardMovePointer = pointer;
        }

        if (keyboardItemMove && pointer != keyboardMovePointer && draggingItem != null)
        {
            PreviewItemMove(draggingItem, pointer);
            keyboardMovePointer = pointer;
        }

        if (inventoryConfirmAction == null || !inventoryConfirmAction.WasPressedThisFrame())
        {
            return;
        }

        if (keyboardItemMove)
        {
            if (draggingItem != null)
            {
                CommitItemMove(draggingItem, pointer);
            }

            keyboardItemMove = false;
            dragCompleted = false;
        }
        else if (draggingItem == null && !IsItemMenuOpen)
        {
            if (hoveredGridItem != null)
            {
                OnItemClicked(hoveredGridItem, PointerEventData.InputButton.Right, pointer);
            }
        }
        else if (IsItemMenuOpen && EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(contextMenu)
                && selected.TryGetComponent(out Button button) && button.IsActive() && button.IsInteractable())
            {
                button.onClick.Invoke();
            }
        }
    }

    private void BuildControlHints()
    {
        LeonInputBridge input = null;
        LeonController player = LeonController.Instance;
        if (player != null)
        {
            player.TryGetComponent(out input);
        }

        inventoryMoveAction = ReadInventoryAction(input, "InventoryMove");
        inventoryConfirmAction = ReadInventoryAction(input, "InventoryConfirm");

        if (hintText == null)
        {
            return;
        }

        if (controlHints == null)
        {
            controlHints = new GameObject("背包按键提示", typeof(RectTransform), typeof(HorizontalLayoutGroup))
                .GetComponent<RectTransform>();
            controlHints.SetParent(hintText.rectTransform, false);
            controlHints.anchorMax = Vector2.one;
            controlHints.anchorMin = Vector2.zero;
            controlHints.sizeDelta = Vector2.zero;
            controlHints.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = controlHints.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 28f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        for (int index = controlHints.childCount - 1; index >= 0; index--)
        {
            GameObject previousHint = controlHints.GetChild(index).gameObject;
            previousHint.SetActive(false);
            Destroy(previousHint);
        }

        hintText.enabled = false;
        controlHints.gameObject.SetActive(!craftingBackdropVisible);

        AddControlHint("移动物品", inventoryMoveAction);
        AddControlHint("自动整理", ReadInventoryAction(input, "Sort"));
        AddControlHint("确定", inventoryConfirmAction);
        AddControlHint("关闭", ReadInventoryAction(input, "Inventory"));
    }

    private static InputAction ReadInventoryAction(LeonInputBridge input, string actionName)
    {
        if (input != null)
        {
            return input.FindInventoryAction(actionName);
        }

        return null;
    }

    private void AddControlHint(string caption, InputAction action)
    {
        if (action == null || action.bindings.Count == 0)
        {
            return;
        }

        string path = action.bindings[0].effectivePath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        GameObject entry = new GameObject(caption, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        entry.transform.SetParent(controlHints, false);
        HorizontalLayoutGroup layout = entry.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (visualTheme != null && visualTheme.originalConfirmKey != null && TryGetKeyAtlasCell(path, out Vector2Int cell))
        {
            Texture2D atlas = visualTheme.originalConfirmKey.texture;
            GameObject icon = new GameObject("按键图标", typeof(RectTransform), typeof(RawImage), typeof(LayoutElement));
            icon.transform.SetParent(entry.transform, false);
            RawImage image = icon.GetComponent<RawImage>();
            image.texture = atlas;
            image.color = Color.white;
            image.raycastTarget = false;

            // 图集以十三列和十三行排列，沿用现有确定切片的像素边距。
            const float step = 1f / 13f;
            Rect confirmRect = visualTheme.originalConfirmKey.rect;
            image.uvRect = new Rect(confirmRect.x / atlas.width + (cell.x - 6) * step,
                confirmRect.y / atlas.height + (9 - cell.y) * step,
                confirmRect.width / atlas.width, confirmRect.height / atlas.height);

            LayoutElement size = icon.GetComponent<LayoutElement>();
            size.preferredWidth = 28f;
            size.preferredHeight = 28f;
        }
        else
        {
            AddControlLabel(entry.transform, action.GetBindingDisplayString(0, InputBinding.DisplayStringOptions.DontIncludeInteractions));
        }

        AddControlLabel(entry.transform, caption);
    }

    private void AddControlLabel(Transform parent, string caption)
    {
        GameObject label = new GameObject("提示文字", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(parent, false);
        Text text = label.GetComponent<Text>();
        text.font = hintText.font;
        text.fontSize = hintText.fontSize;
        text.color = hintText.color;
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.text = caption;
    }

    private static bool TryGetKeyAtlasCell(string bindingPath, out Vector2Int cell)
    {
        cell = default;
        if (bindingPath.StartsWith("<Mouse>/", System.StringComparison.OrdinalIgnoreCase))
        {
            string button = bindingPath.Substring("<Mouse>/".Length).ToLowerInvariant();
            switch (button)
            {
                case "leftbutton": cell = new Vector2Int(9, 3); return true;
                case "rightbutton": cell = new Vector2Int(10, 3); return true;
                case "middlebutton": cell = new Vector2Int(3, 8); return true;
            }

            return false;
        }

        const string keyboardPrefix = "<Keyboard>/";
        if (!bindingPath.StartsWith(keyboardPrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string key = bindingPath.Substring(keyboardPrefix.Length).ToLowerInvariant();
        if (key.Length == 1 && key[0] >= 'a' && key[0] <= 'z')
        {
            int index = key[0] - 'a';
            cell = new Vector2Int(6 + index % 7, 4 + index / 7);
            return true;
        }

        if (key.Length == 1 && key[0] >= '0' && key[0] <= '9')
        {
            cell = new Vector2Int(key[0] - '0', 11);
            return true;
        }

        switch (key)
        {
            case "leftshift": cell = new Vector2Int(3, 9); return true;
            case "rightshift": cell = new Vector2Int(3, 10); return true;
            case "leftctrl": cell = new Vector2Int(4, 9); return true;
            case "rightctrl": cell = new Vector2Int(4, 10); return true;
            case "leftalt": cell = new Vector2Int(5, 9); return true;
            case "rightalt": cell = new Vector2Int(5, 10); return true;
            case "space": cell = new Vector2Int(12, 9); return true;
            case "tab": cell = new Vector2Int(12, 8); return true;
            case "escape": cell = new Vector2Int(12, 7); return true;
            case "enter": cell = new Vector2Int(6, 9); return true;
            default: return false;
        }
    }
}

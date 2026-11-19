using UnityEngine;
using UnityEngine.UI;

// 复用现有菜单操作与许可，仅负责原版素材和排版表现。
public static class OriginalMenuPresentation
{
    public static void ApplySettings(Transform root, UIVisualTheme theme)
    {
        if (root == null || theme == null)
        {
            return;
        }
        foreach (var feedback in root.GetComponentsInChildren<MenuRowFeedback>(true))
        {
            if (feedback.background != null)
            {
                feedback.background.sprite = theme.originalSelectionBand;
                feedback.background.type = Image.Type.Simple;
                feedback.background.material = theme.originalSelectionMaterial;
                feedback.normalState = Color.clear;
                feedback.selected = new Color(0.78f, 0.78f, 0.75f, 0.52f);
            }
            if (feedback.marker != null)
            {
                feedback.marker.gameObject.SetActive(false);
            }
        }
        var layout = root.Find("现代选项布局");
        if (layout != null)
        {
            var background = layout.GetComponent<Image>();
            background.sprite = null;
            background.material = null;
            background.color = new Color(.002f, .002f, .002f, 1f);
            foreach (var label in layout.GetComponentsInChildren<Text>(true))
            {
                if (label.name == "标题")
                {
                    label.text = "选项";
                    label.fontSize = 24;
                }
                else if (label.name == "说明标题")
                {
                    label.fontSize = 20;
                }
                else if (label.name == "说明内容")
                {
                    label.fontSize = 18;
                    label.lineSpacing = 1.2f;
                }
            }
            foreach (var control in layout.GetComponentsInChildren<Selectable>(true))
            {
                if (control is Dropdown dropdown)
                {
                    dropdown.GetComponent<Image>().color = Color.clear;
                    dropdown.captionText.alignment = TextAnchor.MiddleRight;
                }
                else if (control is Toggle toggle && toggle.targetGraphic != null)
                {
                    toggle.targetGraphic.color = Color.clear;
                }
            }
            var topLine = layout.Find("内容区域/内容上沿");
            if (topLine != null)
            {
                topLine.gameObject.SetActive(false);
            }
            var back = layout.Find("返回按钮");
            if (back != null)
            {
                AddKey(back, "返回按键", theme.originalBackKey, new Vector2(0f, 0.2f), new Vector2(0.28f, 0.8f));
            }
        }
    }

    public static void ApplyConfirmation(Transform root, UIVisualTheme theme, Font font)
    {
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            if (theme != null)
            {
                button.image.sprite = theme.originalSelectionBand;
                button.image.material = theme.originalSelectionMaterial;
            }
            button.image.type = Image.Type.Simple;
            button.transition = Selectable.Transition.None;
        }
        var warning = AddText(root, "警告符号", "△", font, 42);
        Anchor(warning.rectTransform, new Vector2(0.47f, 0.65f), new Vector2(0.53f, 0.72f));
        var mark = AddText(root, "警告标记", "!", font, 20);
        Anchor(mark.rectTransform, new Vector2(0.47f, 0.651f), new Vector2(0.53f, 0.703f));
        var confirm = AddText(root, "底部确认提示", "确定", font, 17);
        Anchor(confirm.rectTransform, new Vector2(0.445f, 0.065f), new Vector2(0.485f, 0.105f));
        var back = AddText(root, "底部返回提示", "返回", font, 17);
        Anchor(back.rectTransform, new Vector2(0.535f, 0.065f), new Vector2(0.575f, 0.105f));
        if (theme != null)
        {
            AddKey(root, "确认按键", theme.originalConfirmKey, new Vector2(0.421f, 0.067f), new Vector2(0.444f, 0.103f));
            AddKey(root, "返回按键", theme.originalBackKey, new Vector2(0.511f, 0.067f), new Vector2(0.534f, 0.103f));
        }
    }

    private static void AddKey(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
    {
        if (sprite == null)
        {
            return;
        }
        var child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(parent, false);
        var image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        Anchor(image.rectTransform, min, max);
    }

    private static Text AddText(Transform parent, string name, string value, Font font, int size)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(Text));
        child.transform.SetParent(parent, false);
        var text = child.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = new Color(0.83f, 0.83f, 0.8f);
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class OriginalQuitConfirmation : MonoBehaviour
{
    private Action confirm;
    private Action cancel;
    private Button[] choices;
    private int selected = 1;
    private bool finished;

    public void Initialize(UIVisualTheme theme, Action accepted, Action dismissed)
    {
        confirm = accepted;
        cancel = dismissed;
        var rect = GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        gameObject.AddComponent<Image>().color = new Color(0.002f, 0.002f, 0.002f, 0.998f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (theme != null && theme.interfaceFont != null)
        {
            font = theme.interfaceFont;
        }
        AddLabel("确定要退出游戏吗？", font, new Vector2(.2f,.49f), new Vector2(.8f,.62f), transform);
        choices = new Button[2];
        for (int index = 0; index < choices.Length; index++)
        {
            int choice = index;
            string label = "是";
            if (index == 1)
            {
                label = "否";
            }
            var row = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(transform, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(.41f,.39f - index * .06f);
            rowRect.anchorMax = new Vector2(.59f,.44f - index * .06f);
            rowRect.offsetMin = rowRect.offsetMax = Vector2.zero;
            AddLabel(label, font, Vector2.zero, Vector2.one, row.transform);
            choices[index] = row.GetComponent<Button>();
            choices[index].navigation = new Navigation { mode = Navigation.Mode.None };
            choices[index].onClick.AddListener(() => Finish(choice == 0));
            var trigger = row.AddComponent<EventTrigger>();
            var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            hover.callback.AddListener(_ => Select(choice));
            trigger.triggers.Add(hover);
        }
        OriginalMenuPresentation.ApplyConfirmation(transform, theme, font);
        Select(1);
    }

    private void Update()
    {
        if (finished)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Finish(false);
            return;
        }
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)
            || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))
        {
            Select(1 - selected);
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            Finish(selected == 0);
        }
    }

    private void Select(int index)
    {
        selected = index;
        for (int i = 0; i < choices.Length; i++)
        {
            Color color = Color.clear;
            if (i == selected)
            {
                color = new Color(.8f,.8f,.78f,.75f);
            }
            choices[i].image.color = color;
        }
    }

    private void Finish(bool accepted)
    {
        if (finished)
        {
            return;
        }
        finished = true;
        if (accepted && confirm != null)
        {
            confirm();
        }
        else if (!accepted && cancel != null)
        {
            cancel();
        }
        Destroy(gameObject);
    }

    private static void AddLabel(string value, Font font, Vector2 min, Vector2 max, Transform parent)
    {
        var go = new GameObject("文字", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(.85f,.85f,.82f);
        text.raycastTarget = false;
        text.rectTransform.anchorMin = min;
        text.rectTransform.anchorMax = max;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
    }
}

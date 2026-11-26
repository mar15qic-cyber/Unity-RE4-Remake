using UnityEngine;
using UnityEngine.UI;

/// <summary>界面交互与显示模块 RefinedMenuStyle；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "精修菜单样式")]
public static class RefinedMenuStyle
{
    /// <summary>文字值。</summary>
    static Color textValue = new Color(.89f, .88f, .83f);
    /// <summary>细线线。</summary>
    static Color thinLine = new Color(.7f, .73f, .70f, .23f);
    /// <summary>ApplySet 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Color.Color(float, float, float)、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;MenuRowFeedback&gt;()、Component.GetComponentInParent&lt;Dropdown&gt;(bool)、Component.GetComponentsInChildren&lt;Button&gt;(bool)、Component.GetComponentsInChildren&lt;Dropdown&gt;(bool) 等共 25 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 OptionsTabManager.Initialize()；下游（含内部回调体） Color.Color(float, float, float, float)、Color.Color(float, float, float)、Component.GetComponent&lt;Image&gt;()、Component.GetComponent&lt;MenuRowFeedback&gt;()、Component.GetComponentInParent&lt;Dropdown&gt;(bool)、Component.GetComponentsInChildren&lt;Button&gt;(bool)、Component.GetComponentsInChildren&lt;Dropdown&gt;(bool) 等共 25 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根变换节点。</param>
    /// <param name="theme">主题。</param>
    public static void ApplySet(Transform root, UIVisualTheme theme)
    {
        if (root == null)
        {
            return;
        }

        // 布局。
var layout = root.Find("现代选项布局");
        if (layout != null)
        {
            root = layout;
        }

        if (theme != null)
        {
            textValue = theme.menuText;
            thinLine = theme.menuLine;
        }

        // image（图像）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 RefinedMenuStyle.AddRowLine(Transform) 的参数、作为 RefinedMenuStyle.ThinStrip(Transform, string, Vector2, Vector2, Vector2, Color) 的参数、RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)。
foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            // 名称。
string name = image.name;
            if (name == "顶部栏" || name == "内容区域" || name == "设置列表" || name == "说明区域" || name == "按键滚动视口")
            {
                image.color = Color.clear;
            }

            if (name.EndsWith("行") || name.StartsWith("按键_"))
            {
                image.sprite = null;
                image.color = Color.clear;
                AddRowLine(image.transform);
                // 空值判断前缓存的取得组件的返回结果。
global::MenuRowFeedback availableValue1 = image.GetComponent<MenuRowFeedback>();
                // 反馈。
global::MenuRowFeedback feedback;
                if (!global::System.Object.ReferenceEquals(availableValue1, null))
                {
                    feedback = availableValue1;
                }
                else
                {
                    feedback = image.gameObject.AddComponent<MenuRowFeedback>();
                }

                feedback.background = image;
                feedback.marker = ThinStrip(image.transform, "焦点标记", new Vector2(0, .22f), new Vector2(0, .78f), new Vector2(3, 0), textValue);
                feedback.marker.enabled = false;
            }

            if (name == "分隔线")
            {
                image.color = thinLine;
            }
        }

        // button（按钮）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 RefinedMenuStyle.AddRowLine(Transform) 的参数、RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)。
foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            // 背景。
var background = button.GetComponent<Image>();
            if (background != null)
            {
                button.targetGraphic = background;
                background.sprite = null;
                background.color = Color.clear;
                // 空值判断前缓存的取得组件的返回结果。
global::MenuRowFeedback availableValue3 = button.GetComponent<MenuRowFeedback>();
                // 反馈。
global::MenuRowFeedback feedback;
                if (!global::System.Object.ReferenceEquals(availableValue3, null))
                {
                    feedback = availableValue3;
                }
                else
                {
                    feedback = button.gameObject.AddComponent<MenuRowFeedback>();
                }

                feedback.background = background;
            }

            if (!button.name.StartsWith("按键_"))
            {
                AddRowLine(button.transform);
            }
        }

        // toggle（切换）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 RefinedMenuStyle.AddRowLine(Transform) 的参数、作为 Transform.SetParent(Transform, bool) 的参数、RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)。
foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
        {
            if (toggle.GetComponentInParent<Dropdown>(true) != null)
            {
                continue;
            }

            if (toggle.targetGraphic is Image /* 中文含义：背景。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ bg)
            {
                bg.sprite = null;
                bg.color = new Color(.6f, .64f, .61f, .10f);
            }

            if (toggle.graphic is Image /* 中文含义：标记。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ mark)
            {
                mark.enabled = false;
            }

            // 标签对象。
var labelObject = new GameObject("开关状态", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(toggle.transform, false);
            // 标签。
var label = labelObject.GetComponent<Text>();
            // 赋值前保存的目标标签。
global::UnityEngine.UI.Text assignmentReceiver5 = label;
            if (theme != null)
            {
                assignmentReceiver5.font = theme.interfaceFont;
            }
            else
            {
                assignmentReceiver5.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = textValue;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            // 赋值前保存的目标标签。
global::UnityEngine.UI.Text assignmentReceiver7 = label;
            if (toggle.isOn)
            {
                assignmentReceiver7.text = "开启";
            }
            else
            {
                assignmentReceiver7.text = "关闭";
            }

            toggle.onValueChanged.AddListener( /* 中文含义：是否值。 */value =>
            {
                // 赋值前保存的目标标签。
global::UnityEngine.UI.Text assignmentReceiver9 = label;
                if (value)
                {
                    assignmentReceiver9.text = "开启";
                }
                else
                {
                    assignmentReceiver9.text = "关闭";
                }
            });
            AddRowLine(toggle.transform);
        }

        // slider（滑块）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)。
foreach (var slider in root.GetComponentsInChildren<Slider>(true))
        {
            if (slider.handleRect != null)
            {
                // 区域。
var area = slider.handleRect.parent as RectTransform;
                area.anchorMin = new Vector2(0, .5f);
                area.anchorMax = new Vector2(1, .5f);
                area.sizeDelta = new Vector2(0, 22);
                slider.handleRect.sizeDelta = new Vector2(5, 0);
            }

            if (slider.fillRect != null)
            {
                slider.fillRect.GetComponent<Image>().color = textValue;
            }
        }

        // dropdown（下拉框）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 RefinedMenuStyle.AddRowLine(Transform) 的参数、RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)。
foreach (var dropdown in root.GetComponentsInChildren<Dropdown>(true))
        {
            // 界面图像组件。
var img = dropdown.GetComponent<Image>();
            img.sprite = null;
            img.color = new Color(.7f, .72f, .70f, .055f);
            AddRowLine(dropdown.transform);
            if (dropdown.captionText != null)
            {
                dropdown.captionText.fontSize = 20;
                dropdown.captionText.color = textValue;
            }

            // 箭头。
var arrow = dropdown.transform.Find("Arrow");
            if (arrow != null)
            {
                // 原有图像组件。
var old = arrow.GetComponent<Image>();
                if (old != null)
                {
                    old.enabled = false;
                }

                // 找到的。
var found = arrow.Find("展开箭头");
                // 子级。
global::UnityEngine.GameObject child;
                if (found != null)
                {
                    child = found.gameObject;
                }
                else
                {
                    child = new GameObject("展开箭头", typeof(RectTransform), typeof(Text));
                }

                child.transform.SetParent(arrow, false);
                // 文字。
var text = child.GetComponent<Text>();
                // 赋值前保存的目标文字。
global::UnityEngine.UI.Text assignmentReceiver12 = text;
                if (theme != null)
                {
                    assignmentReceiver12.font = theme.interfaceFont;
                }
                else
                {
                    assignmentReceiver12.font = dropdown.captionText.font;
                }

                text.text = "∨";
                text.fontSize = 22;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = textValue;
                text.raycastTarget = false;
                // 宽高比。
var ar = text.rectTransform;
                ar.anchorMin = Vector2.zero;
                ar.anchorMax = Vector2.one;
                ar.offsetMin = ar.offsetMax = Vector2.zero;
            }

            if (dropdown.template == null)
            {
                continue;
            }

            // 模板。
var template = dropdown.template;
            template.GetComponent<Image>().color = new Color(.045f, .053f, .055f, .99f);
            template.sizeDelta = new Vector2(0, 252);
            // t（当前文字组件）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)。
foreach (var t in template.GetComponentsInChildren<Text>(true))
            {
                t.color = textValue;
                t.fontSize = 20;
            }

            // 当前界面矩形变换。
var item = template.Find("Viewport/Content/Item") as RectTransform;
            if (item != null)
            {
                item.sizeDelta = new Vector2(0, 44);
                // 标记。
var mark = item.Find("Item Checkmark") as RectTransform;
                if (mark != null)
                {
                    mark.sizeDelta = new Vector2(3, 22);
                    mark.GetComponent<Image>().sprite = null;
                }
            }
        }

        // scroll（滚动）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)。
foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
        {
            scroll.scrollSensitivity = 42;
            scroll.decelerationRate = .08f;
            if (scroll.verticalScrollbar != null)
            {
                // 当前界面矩形变换。
var r = scroll.verticalScrollbar.transform as RectTransform;
                r.anchorMin = new Vector2(1, r.anchorMin.y);
                r.anchorMax = new Vector2(1, r.anchorMax.y);
                r.sizeDelta = new Vector2(4, r.sizeDelta.y);
                r.anchoredPosition = new Vector2(-2, r.anchoredPosition.y);
            }
        }

        // text（文字）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)。
foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            if (text.name == "标题")
            {
                text.text = "设置";
                text.fontSize = 42;
                text.fontStyle = FontStyle.Normal;
            }

            if (text.name == "分组标题")
            {
                text.fontSize = 18;
                text.color = new Color(.66f, .69f, .66f);
            }

            if (text.name == "说明标题")
            {
                text.fontSize = 27;
                text.fontStyle = FontStyle.Normal;
            }

            if (text.name == "说明内容")
            {
                text.fontSize = 21;
                text.lineSpacing = 1.45f;
            }
        }

        // 内容。
var content = root.Find("内容区域");
        if (content != null)
        {
            ThinStrip(content, "内容上沿", new Vector2(0, 1), Vector2.one, new Vector2(0, 1), thinLine);
        }
    }

    /// <summary>AddRowLine 的业务处理入口；本方法直接依赖 RefinedMenuStyle.ThinStrip(Transform, string, Vector2, Vector2, Vector2, Color)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)、StorageBoxUI.CreateStorageEntry(GridItem)；下游（含内部回调体） RefinedMenuStyle.ThinStrip(Transform, string, Vector2, Vector2, Vector2, Color)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="row">行。</param>
    public static void AddRowLine(Transform row)
    {
        ThinStrip(row, "行分隔", Vector2.zero, Vector2.right, new Vector2(0, 1), thinLine);
    }

    /// <summary>ThinStrip 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.Find(string)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 RefinedMenuStyle.AddRowLine(Transform)、RefinedMenuStyle.ApplySet(Transform, UIVisualTheme)、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.Find(string)、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">当前变换节点。</param>
    /// <param name="name">名称。</param>
    /// <param name="min">最小。</param>
    /// <param name="max">最大。</param>
    /// <param name="size">尺寸。</param>
    /// <param name="color">颜色。</param>
    public static Image ThinStrip(Transform p, string name, Vector2 min, Vector2 max, Vector2 size, Color color)
    {
        // 找到的。
var found = p.Find(name);
        // 当前游戏对象。
global::UnityEngine.GameObject go;
        if (found != null)
        {
            go = found.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Image));
        }

        go.transform.SetParent(p, false);
        // 图像。
var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        // 当前界面矩形变换。
var r = image.rectTransform;
        r.anchorMin = min;
        r.anchorMax = max;
        r.pivot = new Vector2(.5f, .5f);
        r.sizeDelta = size;
        r.anchoredPosition = Vector2.zero;
        return image;
    }
}

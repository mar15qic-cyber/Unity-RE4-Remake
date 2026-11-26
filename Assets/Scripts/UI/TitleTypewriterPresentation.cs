using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>界面交互与显示组件 TitleTypewriterPresentation，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DefaultExecutionOrder(100)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "标题逐字演出")]
public class TitleTypewriterPresentation : MonoBehaviour
{
    /// <summary>标题容器。</summary>
    [Header("标题资源")]
    [UnityEngine.Serialization.FormerlySerializedAs("标题容器")]
    [UnityEngine.InspectorName("标题容器")]
    public Text titleContainer;
    /// <summary>标题字体。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("标题字体")]
    [UnityEngine.InspectorName("标题字体")]
    public Font titleFont;
    /// <summary>字面材质。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("字面材质")]
    [UnityEngine.InspectorName("字面材质")]
    public Material textFaceMaterial;
    /// <summary>起始延迟。</summary>
    [Header("演出节奏")]
    [UnityEngine.Serialization.FormerlySerializedAs("起始延迟")]
    [UnityEngine.InspectorName("起始延迟")]
    public float startingDelay = 0.25f;
    /// <summary>逐字显示时两个字符之间的等待时间。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("字间延迟")]
    [UnityEngine.InspectorName("字间延迟")]
    public float characterSpacingDelay = 0.105f;
    /// <summary>逐字显示时每个字符的出现时长。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("单字时长")]
    [UnityEngine.InspectorName("单字时长")]
    public float singleCharacterDuration = 0.65f;
    /// <summary>提示延迟。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("提示延迟")]
    [UnityEngine.InspectorName("提示延迟")]
    public float promptDelay = 0.3f;
    /// <summary>是否完成。</summary>
    public bool Complete { get; private set; }

    /// <summary>字符。只读字段，初始化后不由普通写入口替换。</summary>
    readonly List<Text> character = new List<Text>();
    /// <summary>材质值。只读字段，初始化后不由普通写入口替换。</summary>
    readonly List<Material> materialValue = new List<Material>();
    /// <summary>字符边缘。只读字段，初始化后不由普通写入口替换。</summary>
    readonly List<Outline> characterEdge = new List<Outline>();
    /// <summary>字符宽度。只读字段，初始化后不由普通写入口替换。</summary>
    readonly List<float> characterWidth = new List<float>();
    /// <summary>计时器。</summary>
    float timer;
    /// <summary>上一次尺寸。</summary>
    Vector2 lastSize;
    /// <summary>文字排版。编译期常量，保持既有值与消费者映射。</summary>
    const string lettering = "RESIDENT EVIL 4";
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） char.ToString()、Color.Color(float, float, float, float)、GameObject.AddComponent&lt;Outline&gt;()、GameObject.AddComponent&lt;Text&gt;()、GameObject.GameObject(string, params Type[])、List&lt;float&gt;.Add(float)、List&lt;Material&gt;.Add(Material) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (titleContainer == null || titleFont == null)
        {
            Complete = true;
            return;
        }

        titleContainer.text = "ResidentEvil4";
        titleContainer.enabled = false;
        // 当前遍历索引。
for (int i = 0; i < lettering.Length; i++)
        {
            // 对象值。
var objectValue = new GameObject("标题字符" + i, typeof(RectTransform));
            objectValue.transform.SetParent(titleContainer.transform, false);
            // 文字值。
var textValue = objectValue.AddComponent<Text>();
            textValue.font = titleFont;
            textValue.fontSize = 180;
            textValue.fontStyle = FontStyle.Bold;
            textValue.text = lettering[i].ToString();
            textValue.alignment = TextAnchor.MiddleCenter;
            textValue.horizontalOverflow = HorizontalWrapMode.Overflow;
            textValue.verticalOverflow = VerticalWrapMode.Overflow;
            textValue.raycastTarget = false;
            textValue.color = new Color(1, 1, 1, 0);
            // 变换值。
var transformValue = textValue.rectTransform;
            transformValue.anchorMin = transformValue.anchorMax = new Vector2(0, .5f);
            transformValue.sizeDelta = new Vector2(240, 300);
            character.Add(textValue);
            // 添加粗线。
var addThick = objectValue.AddComponent<Outline>();
            addThick.effectDistance = new Vector2(2.5f, .3f);
            addThick.useGraphicAlpha = false;
            characterEdge.Add(addThick);
            // 调用前缓存的字符宽度。
global::System.Collections.Generic.List<float> callReceiver2 = characterWidth;
            // 分支选出的48。
float selectedValue1;
            if (lettering[i] == ' ')
            {
                selectedValue1 = 48;
            }
            else
            {
                selectedValue1 = textValue.preferredWidth + 4;
            }

            callReceiver2.Add(selectedValue1);
            if (textFaceMaterial != null)
            {
                // 实例。
var instance = new Material(textFaceMaterial);
                instance.SetFloat("_Seed", i * 3.17f);
                textValue.material = instance;
                materialValue.Add(instance);
            }
        }

        Typography();
    }

    /// <summary>Typography 的业务处理入口；本方法直接依赖 Mathf.Max(float, float)、Vector2.Vector2(float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 TitleTypewriterPresentation.Start()、TitleTypewriterPresentation.Update()；下游（含内部回调体） Mathf.Max(float, float)、Vector2.Vector2(float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Typography()
    {
        // 尺寸值。
var sizeValue = titleContainer.rectTransform.rect.size;
        lastSize = sizeValue;
        // 总计宽度。
float totalWidth = 0;
        // width（宽度）：每轮从角色宽度取得一个元素，仅代表当前这一项。使用位置：TitleTypewriterPresentation.Typography()。
foreach (var width in characterWidth)
        {
            totalWidth += width;
        }

        // 水平方向比例。
float horizontalDirectionRatio = sizeValue.x / Mathf.Max(1, totalWidth);
        // 竖直方向比例。
float verticalDirectionRatio = sizeValue.y / 136f;
        // 位置。
float position = 0;
        // 当前遍历索引。
for (int i = 0; i < character.Count; i++)
        {
            character[i].rectTransform.anchoredPosition = new Vector2((position + characterWidth[i] * .5f) * horizontalDirectionRatio, 0);
            character[i].rectTransform.localScale = new Vector3(horizontalDirectionRatio, verticalDirectionRatio, 1);
            position += characterWidth[i];
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float)、Material.SetFloat(string, float)、Mathf.Clamp01(float)、Mathf.Lerp(float, float, float)、Mathf.Max(float, float)、TitleTypewriterPresentation.Typography()、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (character.Count == 0)
        {
            return;
        }

        timer += Time.unscaledDeltaTime;
        if (titleContainer.rectTransform.rect.size != lastSize)
        {
            Typography();
        }

        // 总计宽度。
float totalWidth = 0;
        // width（宽度）：每轮从角色宽度取得一个元素，仅代表当前这一项。使用位置：TitleTypewriterPresentation.Update()。
foreach (var width in characterWidth)
        {
            totalWidth += width;
        }

        // 水平方向比例。
float horizontalDirectionRatio = lastSize.x / Mathf.Max(1, totalWidth);
        // 竖直方向比例。
float verticalDirectionRatio = lastSize.y / 136f;
        // 当前遍历索引。
for (int i = 0; i < character.Count; i++)
        {
            // 进度。
float progress = Mathf.Clamp01((timer - startingDelay - i * characterSpacingDelay) / singleCharacterDuration);
            // 缓动。
float ease = progress * progress * (3 - 2 * progress);
            // 颜色值。
Color colorValue;
            if (i == 0 || i == 9)
            {
                colorValue = new Color(.71f, .12f, .13f);
            }
            else
            {
                colorValue = new Color(.87f, .86f, .81f);
            }

            // 首个外观。
float firstAppearance = Mathf.Clamp01(timer / Mathf.Max(.01f, startingDelay));
            colorValue *= Mathf.Lerp(.27f, 1, ease);
            colorValue.a = Mathf.Lerp(.28f, 1, ease) * firstAppearance;
            character[i].color = colorValue;
            characterEdge[i].effectColor = colorValue;
            character[i].rectTransform.localScale = new Vector3(horizontalDirectionRatio * Mathf.Lerp(.91f, 1, ease), verticalDirectionRatio * Mathf.Lerp(.96f, 1, ease), 1);
            if (i < materialValue.Count)
            {
                materialValue[i].SetFloat("_Progress", ease);
            }
        }

        Complete = timer >= startingDelay + (character.Count - 1) * characterSpacingDelay + singleCharacterDuration + promptDelay;
    }

    /// <summary>Skip 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 TitleScreenManager.Update()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Skip()
    {
        timer = 100;
    }

    /// <summary>Unity 对象销毁回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDestroy()
    {
        // instance（实例）：每轮从材质值取得一个元素，仅代表当前这一项。使用位置：作为 Object.Destroy(Object) 的参数、TitleTypewriterPresentation.OnDestroy()。
foreach (var instance in materialValue)
        {
            if (instance != null)
            {
                Destroy(instance);
            }
        }
    }
}

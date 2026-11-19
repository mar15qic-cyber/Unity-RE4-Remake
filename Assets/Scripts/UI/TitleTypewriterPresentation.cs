using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>界面交互与显示组件 TitleTypewriterPresentation，由 Unity 生命周期、序列化绑定及外部方法调用共同驱动。</summary>
[DefaultExecutionOrder(100)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "标题逐字演出")]
public class TitleTypewriterPresentation : MonoBehaviour
{
    /// <summary>中文含义：标题容器。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、InterfaceReworkBuilder.ConfigurationTitle()、TitleTypewriterPresentation.Start() 等共 5 处。</summary>
    [Header("标题资源")]
    [UnityEngine.Serialization.FormerlySerializedAs("标题容器")]
    [UnityEngine.InspectorName("标题容器")]
    public Text titleContainer;
    /// <summary>中文含义：标题字体。使用位置：InterfaceReworkBuilder.ConfigurationTitle()、TitleTypewriterPresentation.Start()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("标题字体")]
    [UnityEngine.InspectorName("标题字体")]
    public Font titleFont;
    /// <summary>中文含义：字面材质。使用位置：作为 Material.Material(Material) 的参数、InterfaceReworkBuilder.ConfigurationTitle()、TitleTypewriterPresentation.Start()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("字面材质")]
    [UnityEngine.InspectorName("字面材质")]
    public Material textFaceMaterial;
    /// <summary>中文含义：起始延迟。使用位置：作为 Mathf.Clamp01(float) 的参数、作为 Mathf.Max(float, float) 的参数、TitleTypewriterPresentation.Update()。</summary>
    [Header("演出节奏")]
    [UnityEngine.Serialization.FormerlySerializedAs("起始延迟")]
    [UnityEngine.InspectorName("起始延迟")]
    public float startingDelay = 0.25f;
    /// <summary>中文含义：逐字显示时两个字符之间的等待时间。使用位置：作为 Mathf.Clamp01(float) 的参数、TitleTypewriterPresentation.Update()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("字间延迟")]
    [UnityEngine.InspectorName("字间延迟")]
    public float characterSpacingDelay = 0.105f;
    /// <summary>中文含义：逐字显示时每个字符的出现时长。使用位置：作为 Mathf.Clamp01(float) 的参数、TitleTypewriterPresentation.Update()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("单字时长")]
    [UnityEngine.InspectorName("单字时长")]
    public float singleCharacterDuration = 0.65f;
    /// <summary>中文含义：提示延迟。使用位置：TitleTypewriterPresentation.Update()。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("提示延迟")]
    [UnityEngine.InspectorName("提示延迟")]
    public float promptDelay = 0.3f;
    /// <summary>中文含义：是否完成。使用位置：TitleScreenManager.Update()、TitleTypewriterPresentation.Start()、TitleTypewriterPresentation.Update()。</summary>
    public bool Complete { get; private set; }

    /// <summary>中文含义：字符。只读字段，初始化后不由普通写入口替换。使用位置：TitleTypewriterPresentation.Start()、TitleTypewriterPresentation.Typography()、TitleTypewriterPresentation.Update()。</summary>
    readonly List<Text> character = new List<Text>();
    /// <summary>中文含义：材质值。只读字段，初始化后不由普通写入口替换。使用位置：TitleTypewriterPresentation.OnDestroy()、TitleTypewriterPresentation.Start()、TitleTypewriterPresentation.Update()。</summary>
    readonly List<Material> materialValue = new List<Material>();
    /// <summary>中文含义：字符边缘。只读字段，初始化后不由普通写入口替换。使用位置：TitleTypewriterPresentation.Start()、TitleTypewriterPresentation.Update()。</summary>
    readonly List<Outline> characterEdge = new List<Outline>();
    /// <summary>中文含义：字符宽度。只读字段，初始化后不由普通写入口替换。使用位置：作为 Vector2.Vector2(float, float) 的参数、TitleTypewriterPresentation.Start()、TitleTypewriterPresentation.Typography() 等共 4 处。</summary>
    readonly List<float> characterWidth = new List<float>();
    /// <summary>中文含义：计时器。使用位置：作为 Mathf.Clamp01(float) 的参数、TitleTypewriterPresentation.Skip()、TitleTypewriterPresentation.Update()。</summary>
    float timer;
    /// <summary>中文含义：上一次尺寸。使用位置：TitleTypewriterPresentation.Typography()、TitleTypewriterPresentation.Update()。</summary>
    Vector2 lastSize;
    /// <summary>中文含义：文字排版。编译期常量，保持既有值与消费者映射。使用位置：TitleTypewriterPresentation.Start()。</summary>
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
        // i（当前遍历索引）：使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数、作为 Material.SetFloat(string, float) 的参数、作为 string.this[int] 的参数 等共 4 处。
for (int i = 0; i < lettering.Length; i++)
        {
            // objectValue（对象值）：使用位置：TitleTypewriterPresentation.Start()。
var objectValue = new GameObject("标题字符" + i, typeof(RectTransform));
            objectValue.transform.SetParent(titleContainer.transform, false);
            // textValue（文字值）：保存 GameObject.AddComponent<Text>() 的返回结果，供当前步骤使用。使用位置：作为 List<Text>.Add(Text) 的参数、TitleTypewriterPresentation.Start()。
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
            // transformValue（变换值）：使用位置：TitleTypewriterPresentation.Start()。
var transformValue = textValue.rectTransform;
            transformValue.anchorMin = transformValue.anchorMax = new Vector2(0, .5f);
            transformValue.sizeDelta = new Vector2(240, 300);
            character.Add(textValue);
            // addThick（添加粗线）：保存 GameObject.AddComponent<Outline>() 的返回结果，供当前步骤使用。使用位置：作为 List<Outline>.Add(Outline) 的参数、TitleTypewriterPresentation.Start()。
var addThick = objectValue.AddComponent<Outline>();
            addThick.effectDistance = new Vector2(2.5f, .3f);
            addThick.useGraphicAlpha = false;
            characterEdge.Add(addThick);
            // callReceiver2（调用前缓存的字符宽度）：从角色宽度读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：TitleTypewriterPresentation.Start()。
global::System.Collections.Generic.List<float> callReceiver2 = characterWidth;
            // selectedValue1（分支选出的48）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 List<float>.Add(float) 的参数、TitleTypewriterPresentation.Start()。
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
                // instance（实例）：使用位置：作为 List<Material>.Add(Material) 的参数、TitleTypewriterPresentation.Start()。
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
        // sizeValue（尺寸值）：使用位置：TitleTypewriterPresentation.Typography()。
var sizeValue = titleContainer.rectTransform.rect.size;
        lastSize = sizeValue;
        // totalWidth（总计宽度）：使用位置：作为 Mathf.Max(float, float) 的参数、TitleTypewriterPresentation.Typography()。
float totalWidth = 0;
        // width（宽度）：每轮从角色宽度取得一个元素，仅代表当前这一项。使用位置：TitleTypewriterPresentation.Typography()。
foreach (var width in characterWidth)
        {
            totalWidth += width;
        }

        // horizontalDirectionRatio（水平方向比例）：使用位置：作为 Vector2.Vector2(float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
float horizontalDirectionRatio = sizeValue.x / Mathf.Max(1, totalWidth);
        // verticalDirectionRatio（竖直方向比例）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float verticalDirectionRatio = sizeValue.y / 136f;
        // position（位置）：使用位置：作为 Vector2.Vector2(float, float) 的参数、TitleTypewriterPresentation.Typography()。
float position = 0;
        // i（当前遍历索引）：使用位置：作为 List<float>.this[int] 的参数、作为 List<Text>.this[int] 的参数、TitleTypewriterPresentation.Typography()。
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

        // totalWidth（总计宽度）：使用位置：作为 Mathf.Max(float, float) 的参数、TitleTypewriterPresentation.Update()。
float totalWidth = 0;
        // width（宽度）：每轮从角色宽度取得一个元素，仅代表当前这一项。使用位置：TitleTypewriterPresentation.Update()。
foreach (var width in characterWidth)
        {
            totalWidth += width;
        }

        // horizontalDirectionRatio（水平方向比例）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float horizontalDirectionRatio = lastSize.x / Mathf.Max(1, totalWidth);
        // verticalDirectionRatio（竖直方向比例）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float verticalDirectionRatio = lastSize.y / 136f;
        // i（当前遍历索引）：使用位置：作为 List<Material>.this[int] 的参数、作为 List<Outline>.this[int] 的参数、作为 List<Text>.this[int] 的参数 等共 5 处。
for (int i = 0; i < character.Count; i++)
        {
            // progress（进度）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：TitleTypewriterPresentation.Update()。
float progress = Mathf.Clamp01((timer - startingDelay - i * characterSpacingDelay) / singleCharacterDuration);
            // ease（缓动）：使用位置：作为 Material.SetFloat(string, float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数。
float ease = progress * progress * (3 - 2 * progress);
            // colorValue（颜色值）：使用位置：TitleTypewriterPresentation.Update()。
Color colorValue;
            if (i == 0 || i == 9)
            {
                colorValue = new Color(.71f, .12f, .13f);
            }
            else
            {
                colorValue = new Color(.87f, .86f, .81f);
            }

            // firstAppearance（首个外观）：保存 Mathf.Clamp01(float) 的返回结果，供当前步骤使用。使用位置：TitleTypewriterPresentation.Update()。
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

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 全局光标管理器 — 统一管理界面光标的普通与悬停样式。
/// 普通状态使用浅色箭头，悬停可交互控件时使用深色箭头，确保在深色背包界面上清晰可见。
/// 通过 Cursor.SetCursor 设置光标，所有场景统一调用。
/// </summary>
public class UICursorManager : Singleton<UICursorManager>
{
    /// <summary>默认光标纹理。</summary>
    [Header("光标贴图")]
    public Texture2D defaultCursorTex;
    /// <summary>已按下光标纹理。</summary>
    public Texture2D pressedCursorTex;
    /// <summary>未在检视面板赋值时，是否使用项目内置素材作为后备。</summary>
    [Tooltip("未在检视面板赋值时，是否使用项目内置素材作为后备")]
    public bool loadFallbackFromResources = true;
    /// <summary>热点。</summary>
    [Header("光标热点")]
    public Vector2 hotspot = new Vector2(2f, 2f);
    /// <summary>是否力度软件光标。强制使用软件光标，确保自定义光标贴图和热点在不同平台保持一致。</summary>
    [Header("光标显示")]
    [Tooltip("强制使用软件光标，确保自定义光标贴图和热点在不同平台保持一致")]
    public bool forceSoftwareCursor = true;
    /// <summary>自定义光标相对于原始贴图的显示比例。</summary>
    [Range(0.25f, 1f)]
    [Tooltip("自定义光标相对于原始贴图的显示比例")]
    public float cursorScale = 0.32f;
    /// <summary>自定义光标是否生效。</summary>
    private bool customCursorActive;
    /// <summary>是否上方按钮。</summary>
    private bool isOverButton;
    /// <summary>生成的默认光标。</summary>
    private Texture2D generatedDefaultCursor;
    /// <summary>生成的已按下光标。</summary>
    private Texture2D generatedPressedCursor;
    /// <summary>缓存事件系统。</summary>
    private EventSystem cachedEventSystem;
    /// <summary>悬停指针。</summary>
    private PointerEventData hoverPointer;
    /// <summary>悬停结果集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly System.Collections.Generic.List<RaycastResult> hoverResults = new System.Collections.Generic.List<RaycastResult>(32);
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） UICursorManager.ApplyCursor()、UICursorManager.CacheHoverPointer()、UICursorManager.LoadCursorTextures()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        CacheHoverPointer();
        LoadCursorTextures();
        if (customCursorActive)
        {
            ApplyCursor();
        }
    }

    /// <summary>LoadCursorTextures 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、Resources.LoadAll&lt;Sprite&gt;(string)、UICursorManager.ExtractReadableTexture(Sprite)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 UICursorManager.ShowCustomCursor()、UICursorManager.Start()；下游（含内部回调体） Debug.LogWarning(object)、Resources.LoadAll&lt;Sprite&gt;(string)、UICursorManager.ExtractReadableTexture(Sprite)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LoadCursorTextures()
    {
        if (!loadFallbackFromResources)
        {
            return;
        }

        if (defaultCursorTex != null && pressedCursorTex != null)
        {
            return;
        }

        // 精灵图集合。
var sprites = Resources.LoadAll<Sprite>("Pictures/cs_ui0300_0_iam.tex");
        if (sprites == null || sprites.Length == 0)
        {
            Debug.LogWarning("[界面光标] 未找到后备光标素材，请在检视面板中赋值。");
            return;
        }

        // s（当前精灵图片）：每轮从精灵图集合取得一个元素，仅代表当前这一项。使用位置：作为 UICursorManager.ExtractReadableTexture(Sprite) 的参数、UICursorManager.LoadCursorTextures()。
foreach (var s in sprites)
        {
            if (s.name == "cs_ui0300_0_iam.tex_0" && defaultCursorTex == null)
            {
                generatedDefaultCursor = ExtractReadableTexture(s);
                defaultCursorTex = generatedDefaultCursor;
            }

            if (s.name == "cs_ui0300_0_iam.tex_1" && pressedCursorTex == null)
            {
                generatedPressedCursor = ExtractReadableTexture(s);
                pressedCursorTex = generatedPressedCursor;
            }
        }
    }

    /// <summary>
    /// 从 Sprite 的子区域提取独立 Texture2D（Cursor.SetCursor 需要独立 Texture2D）
    /// </summary>
    /// <remarks>静态调用链：上游 UICursorManager.LoadCursorTextures()；下游（含内部回调体） Mathf.Clamp(float, float, float)、Mathf.Max(int, int)、Mathf.RoundToInt(float)、Object.Destroy(Object)、Texture2D.Apply()、Texture2D.GetPixelBilinear(float, float)、Texture2D.GetPixels(int, int, int, int) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sprite">精灵图。</param>
    Texture2D ExtractReadableTexture(Sprite sprite)
    {
        // 来源纹理。
var srcTex = sprite.texture;
        // 矩形。
var rect = sprite.rect;
        // 横向。
int x = Mathf.RoundToInt(rect.x);
        // 纵向。
int y = Mathf.RoundToInt(rect.y);
        // 提取区域的像素宽度。
int w = Mathf.RoundToInt(rect.width);
        // 提取区域的像素高度。
int h = Mathf.RoundToInt(rect.height);
        // 直接从可读纹理中提取像素
        // 像素集合。
var pixels = srcTex.GetPixels(x, y, w, h);
        // 缩放后的宽度。
int scaledWidth = Mathf.Max(8, Mathf.RoundToInt(w * Mathf.Clamp(cursorScale, 0.25f, 1f)));
        // 缩放后的高度。
int scaledHeight = Mathf.Max(8, Mathf.RoundToInt(h * Mathf.Clamp(cursorScale, 0.25f, 1f)));
        // 来源二维纹理。
var source = new Texture2D(w, h, TextureFormat.RGBA32, false);
        source.filterMode = FilterMode.Bilinear;
        source.SetPixels(pixels);
        source.Apply();
        // 可读。
var readable = new Texture2D(scaledWidth, scaledHeight, TextureFormat.RGBA32, false);
        readable.filterMode = FilterMode.Bilinear;
        // 目标纵向。
for (int targetY = 0; targetY < scaledHeight; targetY++)
        {
            // 源纹理的纵向采样坐标。
float v = (targetY + 0.5f) / scaledHeight;
            // 目标横向。
for (int targetX = 0; targetX < scaledWidth; targetX++)
            {
                // 横向纹理坐标。
float u = (targetX + 0.5f) / scaledWidth;
                readable.SetPixel(targetX, targetY, source.GetPixelBilinear(u, v));
            }
        }

        readable.Apply();
        Destroy(source);
        return readable;
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） UICursorManager.UpdateHoverState()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        if (!customCursorActive)
        {
            return;
        }

        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        if (!Cursor.visible)
        {
            Cursor.visible = true;
        }

        UpdateHoverState();
    }

    /// <summary>UpdateHoverState 的业务处理入口；本方法直接依赖 AbstractEventData.Reset()、EventSystem.RaycastAll(PointerEventData, List&lt;RaycastResult&gt;)、GameObject.GetComponentInParent&lt;ScrollRect&gt;()、GameObject.GetComponentInParent&lt;Selectable&gt;()、InputControl&lt;Vector2&gt;.ReadValue()、List&lt;RaycastResult&gt;.Clear()、Selectable.IsInteractable() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 UICursorManager.Update()；下游（含内部回调体） AbstractEventData.Reset()、EventSystem.RaycastAll(PointerEventData, List&lt;RaycastResult&gt;)、GameObject.GetComponentInParent&lt;ScrollRect&gt;()、GameObject.GetComponentInParent&lt;Selectable&gt;()、InputControl&lt;Vector2&gt;.ReadValue()、List&lt;RaycastResult&gt;.Clear()、Selectable.IsInteractable() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void UpdateHoverState()
    {
        // 是否当前时刻上方按钮。
bool nowOverButton = false;
        CacheHoverPointer();
        if (EventSystem.current != null)
        {
            // 指针位置。
Vector2 pointerPosition;
            if (Mouse.current != null)
            {
                pointerPosition = Mouse.current.position.ReadValue();
            }
            else
            {
                pointerPosition = (Vector2)Input.mousePosition;
            }

            hoverPointer.Reset();
            hoverPointer.position = pointerPosition;
            hoverResults.Clear();
            EventSystem.current.RaycastAll(hoverPointer, hoverResults);
            // r（当前射线检测结果）：每轮从悬停结果集合取得一个元素，仅代表当前这一项。使用位置：UICursorManager.UpdateHoverState()。
foreach (var r in hoverResults)
            {
                // 可选择控件。
Selectable selectable = r.gameObject.GetComponentInParent<Selectable>();
                if ((selectable != null && selectable.IsInteractable()) || r.gameObject.GetComponentInParent<ScrollRect>() != null)
                {
                    nowOverButton = true;
                    break;
                }
            }
        }

        if (nowOverButton != isOverButton)
        {
            isOverButton = nowOverButton;
            ApplyCursor();
        }
    }

    /// <summary>ApplyCursor 的业务处理入口；本方法直接依赖 Cursor.SetCursor(Texture2D, Vector2, CursorMode)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 UICursorManager.ShowCustomCursor()、UICursorManager.Start()、UICursorManager.UpdateHoverState()；下游（含内部回调体） Cursor.SetCursor(Texture2D, Vector2, CursorMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void ApplyCursor()
    {
        // 纹理。
global::UnityEngine.Texture2D tex;
        if (isOverButton)
        {
            tex = pressedCursorTex;
        }
        else
        {
            tex = defaultCursorTex;
        }

        if (tex != null)
        {
            // 按原顺序缓存的实参纹理。
global::UnityEngine.Texture2D argumentValue4 = tex;
            // 按原顺序缓存的实参热点。
global::UnityEngine.Vector2 argumentValue5 = hotspot;
            // 分支选出的光标模式的力度软件。
global::UnityEngine.CursorMode selectedValue3;
            if (forceSoftwareCursor)
            {
                selectedValue3 = CursorMode.ForceSoftware;
            }
            else
            {
                selectedValue3 = CursorMode.Auto;
            }

            Cursor.SetCursor(argumentValue4, argumentValue5, selectedValue3);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    /// <summary>CacheHoverPointer 的业务处理入口；本方法直接依赖 List&lt;RaycastResult&gt;.Clear()、PointerEventData.PointerEventData(EventSystem)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 UICursorManager.Start()、UICursorManager.UpdateHoverState()；下游（含内部回调体） List&lt;RaycastResult&gt;.Clear()、PointerEventData.PointerEventData(EventSystem)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CacheHoverPointer()
    {
        // 短路判断得到的分支判断。
bool conditionResult7;
        if (cachedEventSystem == EventSystem.current)
        {
            // 分支选出的分支判断。
bool selectedValue6;
            if (EventSystem.current != null)
            {
                selectedValue6 = hoverPointer != null;
            }
            else
            {
                selectedValue6 = hoverPointer == null;
            }

            conditionResult7 = (selectedValue6);
        }
        else
        {
            conditionResult7 = false;
        }

        if (conditionResult7)
        {
            return;
        }

        cachedEventSystem = EventSystem.current;
        if (cachedEventSystem != null)
        {
            hoverPointer = new PointerEventData(cachedEventSystem);
        }
        else
        {
            hoverPointer = null;
        }

        hoverResults.Clear();
    }

    /// <summary>启用自定义光标（UI 场景调用）</summary>
    /// <remarks>静态调用链：上游 GameManager.OnPlayerDeath()、GameOverScreen.Show()、GameUIManager.ApplyModalState()、InventoryUI.OnSceneLoaded()、MissionClearedScreen.Show(MissionResult) 等共 6 处；下游（含内部回调体） UICursorManager.ApplyCursor()、UICursorManager.LoadCursorTextures()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ShowCustomCursor()
    {
        if (defaultCursorTex == null)
        {
            LoadCursorTextures();
        }

        customCursorActive = true;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        isOverButton = false;
        ApplyCursor();
    }

    /// <summary>隐藏光标并锁定（游戏场景调用）</summary>
    /// <remarks>静态调用链：上游 GameManager.BindingCurrentSession(AudioManager, EnemyAIManager)、GameUIManager.ApplyModalState()、InventoryUI.PlayExitAnimation(Action)、LeonInputBridge.Update()、RE4RThirdPersonCamera.Start() 等共 6 处；下游（含内部回调体） Cursor.SetCursor(Texture2D, Vector2, CursorMode)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void HideCustomCursor()
    {
        customCursorActive = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Object.Destroy(Object)、Singleton&lt;UICursorManager&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (generatedDefaultCursor != null)
        {
            Destroy(generatedDefaultCursor);
        }

        if (generatedPressedCursor != null)
        {
            Destroy(generatedPressedCursor);
        }
    }
}

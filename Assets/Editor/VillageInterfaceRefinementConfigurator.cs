using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>编辑器配置与校验模块 VillageInterfaceRefinementConfigurator；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "村庄界面精修配置器")]
public static class VillageInterfaceRefinementConfigurator
{
    /// <summary>中文含义：目录。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;UIVisualTheme&gt;(string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;VillageDaylightConfiguration&gt;(string) 的参数。</summary>
    const string directory = "Assets/UI/重制界面/";
    /// <summary>中文含义：主题。使用位置：作为 MenuThemeApplier.Apply(Transform, UIVisualTheme) 的参数、作为 RefinedMenuStyle.ThinStrip(Transform, string, Vector2, Vector2, Vector2, Color) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene() 等共 5 处。</summary>
    static UIVisualTheme theme;
    /// <summary>Locate 的业务处理入口；本方法直接依赖 Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)、VillageInterfaceRefinementConfigurator.TextValue(Transform, string, string, int, float, float, float, float)；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="r">中文含义：当前界面矩形变换。使用位置：VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float)。</param>
    /// <param name="x">中文含义：横向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="y">中文含义：纵向。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="x2">中文含义：横向2。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    /// <param name="y2">中文含义：纵向2。使用位置：作为 Vector2.Vector2(float, float) 的参数。</param>
    static void Locate(RectTransform r, float x, float y, float x2, float y2)
    {
        r.anchorMin = new Vector2(x, y);
        r.anchorMax = new Vector2(x2, y2);
        r.offsetMin = r.offsetMax = Vector2.zero;
        r.localScale = Vector3.one;
    }

    /// <summary>TextValue 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.Find(string)、Transform.SetParent(Transform, bool)、VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.Find(string)、Transform.SetParent(Transform, bool)、VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、VillageInterfaceRefinementConfigurator.TextValue(Transform, string, string, int, float, float, float, float)。</param>
    /// <param name="name">中文含义：名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数、作为 Transform.Find(string) 的参数。</param>
    /// <param name="value">中文含义：值。使用位置：VillageInterfaceRefinementConfigurator.TextValue(Transform, string, string, int, float, float, float, float)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：VillageInterfaceRefinementConfigurator.TextValue(Transform, string, string, int, float, float, float, float)。</param>
    /// <param name="x">中文含义：横向。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数。</param>
    /// <param name="y">中文含义：纵向。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数。</param>
    /// <param name="x2">中文含义：横向2。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数。</param>
    /// <param name="y2">中文含义：纵向2。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数。</param>
    static Text TextValue(Transform p, string name, string value, int size, float x, float y, float x2, float y2)
    {
        // t（当前变换节点）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：VillageInterfaceRefinementConfigurator.TextValue(Transform, string, string, int, float, float, float, float)。
var t = p.Find(name);
        // go（当前游戏对象）：使用位置：VillageInterfaceRefinementConfigurator.TextValue(Transform, string, string, int, float, float, float, float)。
global::UnityEngine.GameObject go;
        if (t != null)
        {
            go = t.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(Text));
        }

        go.transform.SetParent(p, false);
        // text（文字）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、VillageInterfaceRefinementConfigurator.TextValue(Transform, string, string, int, float, float, float, float)。
var text = go.GetComponent<Text>();
        text.text = value;
        text.font = theme.interfaceFont;
        text.fontSize = size;
        text.color = theme.menuText;
        text.alignment = TextAnchor.UpperLeft;
        text.raycastTarget = false;
        Locate(text.rectTransform, x, y, x2, y2);
        return text;
    }

    /// <summary>ApplyScene 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;UIVisualTheme&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;VillageDaylightConfiguration&gt;(string)、AssetDatabase.SaveAssets()、Color.Color(float, float, float)、Component.GetComponent&lt;MeshRenderer&gt;()、Component.GetComponent&lt;VillageDaylightApplier&gt;()、EditorSceneManager.MarkSceneDirty(Scene) 等共 29 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;UIVisualTheme&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;VillageDaylightConfiguration&gt;(string)、AssetDatabase.SaveAssets()、Color.Color(float, float, float)、Component.GetComponent&lt;MeshRenderer&gt;()、Component.GetComponent&lt;VillageDaylightApplier&gt;()、EditorSceneManager.MarkSceneDirty(Scene) 等共 29 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ApplyScene()
    {
        theme = AssetDatabase.LoadAssetAtPath<UIVisualTheme>(directory + "统一视觉主题.asset");
        // config（当前村庄日光配置）：保存 AssetDatabase.LoadAssetAtPath<VillageDaylightConfiguration>(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene()。
var config = AssetDatabase.LoadAssetAtPath<VillageDaylightConfiguration>(directory + "村庄日间配置.asset");
        config.sunlightColor = new Color(1, .95f, .84f);
        EditorUtility.SetDirty(config);
        // scene（场景）：保存 SceneManager.GetActiveScene() 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 EditorSceneManager.SaveScene(Scene) 的参数、作为 VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene) 的参数 等共 4 处。
var scene = SceneManager.GetActiveScene();
        // root（根游戏对象）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：VillageInterfaceRefinementConfigurator.ApplyScene()。
foreach (var root in scene.GetRootGameObjects())
        {
            // particle（粒子）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene()。
foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.name == "街道浮尘")
                {
                    Object.DestroyImmediate(particle.gameObject);
                }
            }

            // camera（摄像机）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：VillageInterfaceRefinementConfigurator.ApplyScene()。
foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                camera.cullingMask &= ~(1 << 29);
            }

            // light（光源）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 VillageDaylightConfiguration.Apply(Light) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene()。
foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                light.cullingMask &= ~(1 << 29);
                if (light.type == LightType.Directional)
                {
                    // availableValue2（空值判断前缓存的取得组件的返回结果）：从取得组件的返回结果读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene()。
global::VillageDaylightApplier availableValue2 = light.GetComponent<VillageDaylightApplier>();
                    // app（应用）：使用位置：VillageInterfaceRefinementConfigurator.ApplyScene()。
global::VillageDaylightApplier app;
                    if (!global::System.Object.ReferenceEquals(availableValue2, null))
                    {
                        app = availableValue2;
                    }
                    else
                    {
                        app = light.gameObject.AddComponent<VillageDaylightApplier>();
                    }

                    app.configuration = config;
                    app.sunlight = light;
                    config.Apply(light);
                }
            }

            // menu（菜单）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：VillageInterfaceRefinementConfigurator.ApplyScene()。
foreach (var menu in root.GetComponentsInChildren<PauseMenu>(true))
            {
                menu.visualTheme = theme;
                menu.backgroundDownsample = 2;
                menu.backgroundSofteningShader = Shader.Find("菜单/暂停柔化");
            }

            // options（选项集合）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：VillageInterfaceRefinementConfigurator.ApplyScene()。
foreach (var options in root.GetComponentsInChildren<OptionsTabManager>(true))
            {
                options.visualTheme = theme;
            }

            // p（当前物品拾取）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 PrefabUtility.InstantiatePrefab(Object, Transform) 的参数、作为 Quaternion.Euler(Vector3) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene()。
foreach (var p in root.GetComponentsInChildren<ItemPickup>(true))
            {
                if (p.item == null || p.item.worldModelPrefab == null)
                {
                    continue;
                }

                p.modelOverride = null;
                // old（原有变换节点）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene()。
var old = p.transform.Find("Model");
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                // child（子级）：每轮从投射的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数、VillageInterfaceRefinementConfigurator.ApplyScene()。
foreach (var child in p.transform.Cast<Transform>().ToArray())
                {
                    if (child.name.StartsWith("图集拾取标识"))
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                // mesh（网格）：保存 Component.GetComponent<MeshRenderer>() 的返回结果，供当前步骤使用。使用位置：VillageInterfaceRefinementConfigurator.ApplyScene()。
var mesh = p.GetComponent<MeshRenderer>();
                if (mesh != null)
                {
                    mesh.enabled = false;
                }

                // model（模型）：使用位置：VillageInterfaceRefinementConfigurator.ApplyScene()。
var model = (GameObject)PrefabUtility.InstantiatePrefab(p.item.worldModelPrefab, p.transform);
                model.name = "Model";
                model.transform.localPosition = p.item.modelOffset;
                model.transform.localRotation = Quaternion.Euler(p.item.modelRotation);
                model.transform.localScale = p.item.modelScale;
            }
        }

        if (scene.name == "StorageBoxScene")
        {
            ConfigurationStored(scene);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "已保存" + scene.name;
    }

    /// <summary>ConfigurationStored 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string)、Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasScaler&gt;()、Component.GetComponent&lt;Image&gt;()、Component.GetComponentInChildren&lt;ScrollRect&gt;()、Component.GetComponentInParent&lt;Canvas&gt;()、Component.GetComponentsInChildren&lt;Image&gt;(bool) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageInterfaceRefinementConfigurator.ApplyScene()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string)、Color.Color(float, float, float, float)、Component.GetComponent&lt;CanvasScaler&gt;()、Component.GetComponent&lt;Image&gt;()、Component.GetComponentInChildren&lt;ScrollRect&gt;()、Component.GetComponentInParent&lt;Canvas&gt;()、Component.GetComponentsInChildren&lt;Image&gt;(bool) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="scene">中文含义：场景。使用位置：VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。</param>
    static void ConfigurationStored(Scene scene)
    {
        // ui（界面）：保存 IEnumerable<StorageBoxUI>.First<StorageBoxUI>() 的返回结果，供当前步骤使用。使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
var ui = scene.GetRootGameObjects().SelectMany( /* 中文含义：当前游戏对象。 */g => g.GetComponentsInChildren<StorageBoxUI>(true)).First();
        ui.visualTheme = theme;
        // panel（面板）：使用位置：作为 MenuThemeApplier.Apply(Transform, UIVisualTheme) 的参数、作为 RefinedMenuStyle.ThinStrip(Transform, string, Vector2, Vector2, Vector2, Color) 的参数、作为 Transform.SetParent(Transform, bool) 的参数 等共 5 处。
var panel = ui.panel.transform;
        // canvas（画布）：保存 Component.GetComponentInParent<Canvas>() 的返回结果，供当前步骤使用。使用位置：VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
var canvas = panel.GetComponentInParent<Canvas>();
        // scaler（缩放器）：保存 Component.GetComponent<CanvasScaler>() 的返回结果，供当前步骤使用。使用位置：VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        // image（图像）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
foreach (var image in panel.GetComponentsInChildren<Image>(true))
        {
            image.sprite = null;
            image.color = new Color(.04f, .048f, .052f, .25f);
        }

        panel.GetComponent<Image>().color = new Color(.016f, .020f, .023f, .97f);
        Locate(ui.titleText.rectTransform, .055f, .87f, .93f, .95f);
        ui.titleText.text = "寄存箱";
        ui.titleText.fontSize = 42;
        ui.titleText.alignment = TextAnchor.MiddleLeft;
        // left（左侧）：使用位置：作为 Transform.SetParent(Transform, bool) 的参数、作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
var left = panel.Find("StoragePanel") as RectTransform;
        Locate(left, .055f, .15f, .40f, .79f);
        // right（右侧）：使用位置：作为 Transform.SetParent(Transform, bool) 的参数、作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数。
var right = panel.Find("InventoryPanel") as RectTransform;
        Locate(right, .46f, .43f, .945f, .79f);
        Locate(ui.storageTitleText.rectTransform, .055f, .80f, .40f, .85f);
        ui.storageTitleText.text = "寄存物品";
        ui.storageTitleText.fontSize = 23;
        Locate(ui.inventoryTitleText.rectTransform, .46f, .80f, .94f, .85f);
        ui.inventoryTitleText.text = "公文包";
        ui.inventoryTitleText.fontSize = 23;
        Locate(ui.gridView.gridArea, 0, 0, 1, 1);
        ui.gridView.visualTheme = theme;
        ui.gridView.cellBackgroundSprite = null;
        ui.gridView.cellFallbackColor = new Color(.25f, .28f, .28f, .32f);
        ui.gridView.selectedColor = new Color(.8f, .83f, .75f, .2f);
        ui.gridView.hoverColor = new Color(.8f, .83f, .75f, .10f);
        ui.dropZoneColor = ui.inventoryDropZoneColor = new Color(.74f, .80f, .71f, .14f);
        ui.storageDropArea = left;
        // scroll（滚动）：保存 Component.GetComponentInChildren<ScrollRect>() 的返回结果，供当前步骤使用。使用位置：VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
var scroll = left.GetComponentInChildren<ScrollRect>();
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 42;
        if (ui.dropZoneHighlight != null)
        {
            ui.dropZoneHighlight.transform.SetParent(left, false);
            Locate(ui.dropZoneHighlight.rectTransform, 0, 0, 1, 1);
            ui.dropZoneHighlight.raycastTarget = false;
        }

        if (ui.inventoryDropZoneHighlight != null)
        {
            ui.inventoryDropZoneHighlight.transform.SetParent(right, false);
            Locate(ui.inventoryDropZoneHighlight.rectTransform, 0, 0, 1, 1);
            ui.inventoryDropZoneHighlight.raycastTarget = false;
        }

        Locate(ui.hintText.rectTransform, .055f, .035f, .945f, .09f);
        ui.hintText.fontSize = 18;
        ui.hintText.alignment = TextAnchor.MiddleLeft;
        if (ui.failHintText != null)
        {
            Locate(ui.failHintText.rectTransform, .055f, .095f, .40f, .14f);
            ui.failHintText.fontSize = 20;
            ui.failHintText.alignment = TextAnchor.MiddleLeft;
        }

        // old（原有变换节点）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
var old = panel.Find("物品预览");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        // go（当前游戏对象）：使用位置：作为 VillageInterfaceRefinementConfigurator.Locate(RectTransform, float, float, float, float) 的参数、VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
var go = new GameObject("物品预览", typeof(RectTransform), typeof(RawImage), typeof(StoredItemPreview));
        go.transform.SetParent(panel, false);
        Locate(go.GetComponent<RectTransform>(), .46f, .12f, .69f, .39f);
        // preview（预览）：保存 GameObject.GetComponent<StoredItemPreview>() 的返回结果，供当前步骤使用。使用位置：VillageInterfaceRefinementConfigurator.ConfigurationStored(Scene)。
var preview = go.GetComponent<StoredItemPreview>();
        preview.configuration = AssetDatabase.LoadAssetAtPath<InventoryPresentationConfiguration>(directory + "背包展示配置.asset");
        preview.view = go.GetComponent<RawImage>();
        preview.view.color = Color.clear;
        preview.nameValue = TextValue(panel, "物品名称", "物品详情", 26, .72f, .31f, .945f, .39f);
        preview.countValue = TextValue(panel, "物品数量", "", 18, .72f, .26f, .945f, .31f);
        preview.descriptionValue = TextValue(panel, "物品说明", "选择物品查看详情。", 20, .72f, .12f, .945f, .25f);
        preview.descriptionValue.lineSpacing = 1.3f;
        preview.prompt = TextValue(panel, "旋转提示", "", 16, .46f, .09f, .69f, .12f);
        ui.itemPreview = preview;
        RefinedMenuStyle.ThinStrip(panel, "标题下线", new Vector2(.055f, .855f), new Vector2(.945f, .855f), new Vector2(0, 1), theme.menuLine);
        RefinedMenuStyle.ThinStrip(panel, "双栏分隔", new Vector2(.43f, .15f), new Vector2(.43f, .82f), new Vector2(1, 0), theme.menuLine);
        RefinedMenuStyle.ThinStrip(panel, "说明上沿", new Vector2(.46f, .41f), new Vector2(.945f, .41f), new Vector2(0, 1), theme.menuLine);
        MenuThemeApplier.Apply(panel, theme);
    }
}

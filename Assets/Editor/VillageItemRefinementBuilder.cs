using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>生成有实体厚度的展示资源；合并同材质网格，三种展示入口共用预制件。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "村庄物品精修构建器")]
public static class VillageItemRefinementBuilder
{
    /// <summary>中文含义：目录。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、作为 VillageItemRefinementBuilder.BuildDirectory(string) 的参数 等共 7 处。</summary>
    const string directory = "Assets/UI/重制界面/精修物品";
    /// <summary>中文含义：leaf 表示“叶片”；stem 表示“茎杆”；flower 表示“花瓣”；cloth 表示“布料”；steel 表示“钢材”；copper 表示“铜材质”；paper 表示“纸张”；glass 表示“玻璃”；cork 表示“软木”；powder 表示“粉末”。使用位置：作为 VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。；使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数、VillageItemRefinementBuilder.Build()。；使用位置：作为 VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。；使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。；使用位置：作为 VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool) 等共 4 处。；使用位置：VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)、VillageItemRefinementBuilder.Build()。；使用位置：作为 VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material) 的参数、VillageItemRefinementBuilder.Build()。；使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数、VillageItemRefinementBuilder.Build()。；使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数、VillageItemRefinementBuilder.Build()。；</summary>
    static Material leaf, stem, flower, cloth, steel, copper, paper, glass, cork, powder;
    /// <summary>中文含义：角落半径。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、VillageItemRefinementBuilder.Build()。</summary>
    static Mesh cornerRadius;
    /// <summary>中文含义：粒子。使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。</summary>
    static Mesh particle;
    /// <summary>中文含义：网格序号。使用位置：VillageItemRefinementBuilder.Build()、VillageItemRefinementBuilder.SaveMesh(Mesh, string)。</summary>
    static int meshSequence;
    /// <summary>中文含义：当前标识符。使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、VillageItemRefinementBuilder.Build()、VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool) 等共 5 处。</summary>
    static string currentIdentifier;
    /// <summary>Build 的业务处理入口；本方法直接依赖 Array.Empty&lt;string&gt;()、AssetDatabase.FindAssets(string)、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;ItemData&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Mesh&gt;(string) 等共 43 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Array.Empty&lt;string&gt;()、AssetDatabase.FindAssets(string)、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;ItemData&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Mesh&gt;(string) 等共 43 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string Build()
    {
        BuildDirectory(directory);
        BuildDirectory(directory + "/网格");
        BuildDirectory(directory + "/材质");
        cornerRadius = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/UI/重制界面/倒角网格.asset");
        stem = MaterialValue("草药茎", new Color(.26f, .31f, .12f), 0, .25f, 0);
        steel = MaterialValue("氧化钢", new Color(.28f, .30f, .30f), .72f, .42f, 1);
        copper = MaterialValue("旧黄铜", new Color(.51f, .40f, .23f), .75f, .48f, 1);
        paper = MaterialValue("折边纸板", new Color(.72f, .69f, .59f), 0, .16f, 1);
        cork = MaterialValue("软木", new Color(.39f, .29f, .18f), 0, .13f, 1);
        glass = MaterialValue("药管玻璃", new Color(.72f, .83f, .78f, .10f), 0, .88f, 0);
        glass.shader = Shader.Find("物品/薄壁玻璃");
        glass.shaderKeywords = Array.Empty<string>();
        glass.renderQueue = 3000;
        // configuration（配置）：保存 AssetDatabase.LoadAssetAtPath<InventoryPresentationConfiguration>(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、VillageItemRefinementBuilder.Build()。
var configuration = AssetDatabase.LoadAssetAtPath<InventoryPresentationConfiguration>("Assets/UI/重制界面/背包展示配置.asset");
        // record（当前文本构建器）：使用位置：VillageItemRefinementBuilder.Build()。
var record = new System.Text.StringBuilder();
        // guid（资源标识）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.GUIDToAssetPath(string) 的参数。
foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            // item（当前物品数据配置）：保存 AssetDatabase.LoadAssetAtPath<ItemData>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的参数、作为 EditorUtility.SetDirty(Object) 的参数、作为 GameObject.GameObject(string) 的参数 等共 14 处。
var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            currentIdentifier = item.itemId;
            meshSequence = 0;
            // entry（当前物品表现）：保存 InventoryPresentationConfiguration.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Euler(Vector3) 的参数、VillageItemRefinementBuilder.Build()。
var entry = configuration.Find(item.itemId);
            if (entry == null)
            {
                throw new InvalidOperationException("缺少物品展示映射：" + item.displayName);
            }

            // newBuild（是否新构建）：使用位置：VillageItemRefinementBuilder.Build()。
bool newBuild = item.itemId.Contains("herb") || item.itemId.Contains("resources") || item.itemId == "gunpowder" || item.itemId == "shotgun_ammo";
            // root（根游戏对象）：使用位置：作为 MethodBase.Invoke(object, object[]) 的参数、作为 Object.DestroyImmediate(Object) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数 等共 13 处。
GameObject root;
            if (newBuild)
            {
                root = new GameObject(item.displayName);
                if (item.itemId.StartsWith("mixed"))
                {
                    AuthoringMedicineTube(root.transform, item.itemId);
                }
                else if (item.itemId.Contains("herb"))
                {
                    AuthoringHerb(root.transform, item.itemId);
                }
                else if (item.itemId.Contains("resources"))
                {
                    AuthoringFabricBag(root.transform, item.itemId.Contains("large"));
                }
                else if (item.itemId == "gunpowder")
                {
                    AuthoringGunpowder(root.transform, item);
                }
                else
                {
                    AuthoringAmmo(root.transform, item);
                }

                entry.orientation = Vector3.zero;
            }
            else
            {
                // source（来源游戏对象）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.Build()。
var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/重制界面/模型/" + item.displayName.Replace("/", "_").Replace("M16 ", "") + ".prefab");
                // selectedValue1（分支选出的来源）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Object.Instantiate<GameObject>(GameObject) 的参数、VillageItemRefinementBuilder.Build()。
global::UnityEngine.GameObject selectedValue1;
                if (source != null)
                {
                    selectedValue1 = source;
                }
                else
                {
                    selectedValue1 = entry.model;
                }

                root = UnityEngine.Object.Instantiate(selectedValue1);
                root.name = item.displayName;
                entry.orientation = (Vector3)typeof(InterfaceReworkBuilder).GetMethod("BestOrientation", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { root, item.gridWidth, item.gridHeight });
                if (item.itemId == "broken_knife")
                {
                    root.transform.rotation = Quaternion.Euler(entry.orientation);
                    AuthoringBrokenBlade(root);
                }
            }

            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            // component（组件）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数、VillageItemRefinementBuilder.Build()。
foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component != null && !(component is Transform) && !(component is MeshFilter) && !(component is Renderer))
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }

            if (newBuild)
            {
                Merge(root);
            }

            // prefab（预制件）：保存 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.Build()。
var prefab = PrefabUtility.SaveAsPrefabAsset(root, directory + "/" + item.displayName.Replace("/", "_") + ".prefab");
            entry.model = prefab;
            entry.gridOccupancyRatio = .88f;
            item.worldModelPrefab = prefab;
            item.inspectionModelPrefab = prefab;
            item.inspectionModelRotation = entry.orientation + new Vector3(0, -12, 0);
            item.inspectionModelOffset = Vector3.zero;
            item.inspectionViewScale = 1f;
            root.transform.rotation = Quaternion.Euler(entry.orientation);
            // bounds（包围盒）：保存 VillageItemRefinementBuilder.Boundary(GameObject) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Max(params float[]) 的参数、VillageItemRefinementBuilder.Build()。
var bounds = Boundary(root);
            // targetLength（目标长度）：使用位置：VillageItemRefinementBuilder.Build()。
float targetLength;
            if (item.itemId == "rifle" || item.itemId == "shotgun")
            {
                targetLength = .85f;
            }
            else
            {
                if (item.itemId == "pistol")
                {
                    targetLength = .29f;
                }
                else
                {
                    if (item.itemId.Contains("knife"))
                    {
                        targetLength = .27f;
                    }
                    else
                    {
                        if (item.itemId.Contains("herb") && !item.itemId.StartsWith("mixed"))
                        {
                            targetLength = .32f;
                        }
                        else
                        {
                            targetLength = .22f;
                        }
                    }
                }
            }

            // scale（缩放）：使用位置：VillageItemRefinementBuilder.Build()。
float scale = targetLength / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            item.modelScale = Vector3.one * scale;
            item.modelRotation = entry.orientation;
            item.modelOffset = -bounds.center * scale;
            EditorUtility.SetDirty(item);
            record.AppendLine(item.displayName + "：" + root.GetComponentsInChildren<MeshFilter>().Sum( /* 中文含义：当前网格过滤器。 */m => m.sharedMesh.vertexCount) + "顶点");
            UnityEngine.Object.DestroyImmediate(root);
        }

        EditorUtility.SetDirty(configuration);
        AssetDatabase.SaveAssets();
        return record.ToString();
    }

    /// <summary>BuildDirectory 的业务处理入口；本方法直接依赖 AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)、string.LastIndexOf(char)、string.Substring(int, int)、string.Substring(int)、VillageItemRefinementBuilder.BuildDirectory(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Build()、VillageItemRefinementBuilder.BuildDirectory(string)；下游（含内部回调体） AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)、string.LastIndexOf(char)、string.Substring(int, int)、string.Substring(int)、VillageItemRefinementBuilder.BuildDirectory(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前处理的资源路径。使用位置：作为 AssetDatabase.CreateFolder(string, string) 的参数、作为 AssetDatabase.IsValidFolder(string) 的参数、作为 VillageItemRefinementBuilder.BuildDirectory(string) 的参数 等共 4 处。</param>
    static void BuildDirectory(string p)
    {
        if (AssetDatabase.IsValidFolder(p))
        {
            return;
        }

        // i（当前遍历索引）：保存 string.LastIndexOf(char) 的返回结果，供当前步骤使用。使用位置：作为 string.Substring(int, int) 的参数、作为 string.Substring(int) 的参数。
int i = p.LastIndexOf('/');
        BuildDirectory(p.Substring(0, i));
        AssetDatabase.CreateFolder(p.Substring(0, i), p.Substring(i + 1));
    }

    /// <summary>MaterialValue 的业务处理入口；本方法直接依赖 AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、Color.Color(float, float, float, float)、EditorUtility.SetDirty(Object)、Material.Material(Shader)、Material.SetFloat(string, float) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AtlasMaterial(ItemData)、VillageItemRefinementBuilder.AuthoringAmmo(Transform, ItemData)、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)、VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)、VillageItemRefinementBuilder.AuthoringHerb(Transform, string) 等共 8 处；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、Color.Color(float, float, float, float)、EditorUtility.SetDirty(Object)、Material.Material(Shader)、Material.SetFloat(string, float) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。</param>
    /// <param name="color">中文含义：颜色。使用位置：VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。</param>
    /// <param name="metal">中文含义：金属。使用位置：作为 Material.SetFloat(string, float) 的参数。</param>
    /// <param name="gloss">中文含义：光泽。使用位置：作为 Material.SetFloat(string, float) 的参数。</param>
    /// <param name="texture">中文含义：材质使用的纹理方案编号。使用位置：VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。</param>
    static Material MaterialValue(string name, Color color, float metal, float gloss, int texture)
    {
        // p（当前处理的资源路径）：使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath<Material>(string) 的参数。
string p = directory + "/材质/" + name + ".mat";
        // m（当前材质）：保存 AssetDatabase.LoadAssetAtPath<Material>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 EditorUtility.SetDirty(Object) 的参数、VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。
var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, p);
        }

        m.shader = Shader.Find("Standard");
        m.color = color;
        m.SetFloat("_Metallic", metal);
        m.SetFloat("_Glossiness", gloss);
        if (texture > 0)
        {
            // tp（纹理坐标）：使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath<Texture2D>(string) 的参数。
string tp = directory + "/材质/" + name + "织理.asset";
            // tex（纹理）：保存 AssetDatabase.LoadAssetAtPath<Texture2D>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。
var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            if (tex == null)
            {
                tex = new Texture2D(256, 256, TextureFormat.RGBA32, true)
                {
                    name = name + "织理",
                    wrapMode = TextureWrapMode.Repeat,
                    anisoLevel = 4
                };
                // y（纵向）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.PerlinNoise(float, float) 的参数、作为 Mathf.Sin(float) 的参数 等共 5 处。
for (int y = 0; y < 256; y++)
                {
                    // x（横向）：使用位置：作为 Mathf.PerlinNoise(float, float) 的参数、作为 Mathf.Sin(float) 的参数、作为 Texture2D.SetPixel(int, int, Color) 的参数 等共 4 处。
for (int x = 0; x < 256; x++)
                    {
                        // n（纹理颜色使用的亮度噪声系数）：使用位置：作为 Color.Color(float, float, float, float) 的参数、VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。
float n = .72f + Mathf.PerlinNoise(x * .11f, y * .11f) * .20f;
                        if (texture == 2)
                        {
                            n *= .83f + .17f * Mathf.Abs(Mathf.Sin(x * Mathf.PI * .5f) * Mathf.Cos(y * Mathf.PI * .5f));
                        }
                        else
                        {
                            n += .05f * Mathf.Sin(x * 3.7f + y * 5.1f);
                        }

                        tex.SetPixel(x, y, new Color(n, n, n, 1));
                    }
                }

                tex.Apply();
                AssetDatabase.CreateAsset(tex, tp);
            }

            m.mainTexture = tex;
            // assignmentReceiver6（赋值前保存的目标当前材质）：从矩阵读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。
global::UnityEngine.Material assignmentReceiver6 = m;
            // leftOperand8（计算前缓存的左侧向量2的第一）：从向量2的第一读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。
global::UnityEngine.Vector2 leftOperand8 = Vector2.one;
            // selectedValue7（分支选出的3）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。
int selectedValue7;
            if (texture == 2)
            {
                selectedValue7 = 3;
            }
            else
            {
                selectedValue7 = 1;
            }

            assignmentReceiver6.mainTextureScale = leftOperand8 * (selectedValue7);
        }

        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>MeshObject 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;MeshFilter&gt;()、GameObject.GetComponent&lt;MeshRenderer&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)、VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)、VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)、VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material) 等共 9 处；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;MeshFilter&gt;()、GameObject.GetComponent&lt;MeshRenderer&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="name">中文含义：名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="mesh">中文含义：网格。使用位置：VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material)。</param>
    /// <param name="mat">中文含义：材质。使用位置：VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material)。</param>
    static GameObject MeshObject(Transform p, string name, Mesh mesh, Material mat)
    {
        // go（当前游戏对象）：使用位置：VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material)。
var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(p, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>Block 的业务处理入口；本方法直接依赖 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringAmmo(Transform, ItemData)、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)；下游（含内部回调体） VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    /// <param name="name">中文含义：名称。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    /// <param name="pos">中文含义：位置。使用位置：VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material)。</param>
    /// <param name="mat">中文含义：材质。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    static GameObject Block(Transform p, string name, Vector3 pos, Vector3 size, Material mat)
    {
        // go（当前游戏对象）：保存 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material)。
var go = MeshObject(p, name, cornerRadius, mat);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        return go;
    }

    /// <summary>MeshValue 的业务处理入口；本方法直接依赖 Mesh.Mesh()、Mesh.RecalculateBounds()、Mesh.RecalculateNormals()、Mesh.RecalculateTangents()、Mesh.SetTriangles(List&lt;int&gt;, int)、Mesh.SetUVs(int, List&lt;Vector2&gt;)、Mesh.SetVertices(List&lt;Vector3&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)、VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)、VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)、VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool) 等共 7 处；下游（含内部回调体） Mesh.Mesh()、Mesh.RecalculateBounds()、Mesh.RecalculateNormals()、Mesh.RecalculateTangents()、Mesh.SetTriangles(List&lt;int&gt;, int)、Mesh.SetUVs(int, List&lt;Vector2&gt;)、Mesh.SetVertices(List&lt;Vector3&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="v">中文含义：当前三维向量集合。使用位置：作为 Mesh.SetVertices(List&lt;Vector3&gt;) 的参数。</param>
    /// <param name="t">中文含义：当前整数集合。使用位置：作为 Mesh.SetTriangles(List&lt;int&gt;, int) 的参数。</param>
    /// <param name="uv">中文含义：纹理坐标。使用位置：作为 Mesh.SetUVs(int, List&lt;Vector2&gt;) 的参数。</param>
    static Mesh MeshValue(List<Vector3> v, List<int> t, List<Vector2> uv)
    {
        // m（当前网格）：使用位置：VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>)。
var m = new Mesh();
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.SetUVs(0, uv);
        m.RecalculateNormals();
        m.RecalculateBounds();
        m.RecalculateTangents();
        return m;
    }

    /// <summary>Tube 的业务处理入口；本方法直接依赖 List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List()、Mathf.Cos(float) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)、VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)、VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)；下游（含内部回调体） List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List()、Mathf.Cos(float) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    /// <param name="name">中文含义：名称。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    /// <param name="a">中文含义：起点（三维向量）。使用位置：作为 List&lt;Vector3&gt;.Add(Vector3) 的参数、作为 Quaternion.FromToRotation(Vector3, Vector3) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。</param>
    /// <param name="b">中文含义：终点（三维向量）。使用位置：作为 List&lt;Vector3&gt;.Add(Vector3) 的参数、作为 Quaternion.FromToRotation(Vector3, Vector3) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    /// <param name="mat">中文含义：材质。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    /// <param name="sides">中文含义：侧面集合。使用位置：作为 List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;) 的参数、作为 Vector2.Vector2(float, float) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。</param>
    static void Tube(Transform p, string name, Vector3 a, Vector3 b, float radius, Material mat, int sides = 10)
    {
        // v（当前三维向量集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
var v = new List<Vector3>();
        // t（当前整数集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
var t = new List<int>();
        // uv（纹理坐标）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
var uv = new List<Vector2>();
        // q（当前处理的四元数旋转）：保存 Quaternion.FromToRotation(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：作为 List<Vector3>.Add(Vector3) 的参数。
var q = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        // j（内层遍历索引）：使用位置：作为 Vector2.Vector2(float, float) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
for (int j = 0; j < 2; j++)
        {
            // i（当前遍历索引）：使用位置：作为 Vector2.Vector2(float, float) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
for (int i = 0; i <= sides; i++)
            {
                // angle（角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float angle = i * Mathf.PI * 2 / sides;
                // callReceiver10（调用前缓存的当前三维向量集合）：从向量读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
global::System.Collections.Generic.List<global::UnityEngine.Vector3> callReceiver10 = v;
                // selectedValue9（分支选出的起点）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 List<Vector3>.Add(Vector3) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
global::UnityEngine.Vector3 selectedValue9;
                if (j == 0)
                {
                    selectedValue9 = a;
                }
                else
                {
                    selectedValue9 = b;
                }

                callReceiver10.Add((selectedValue9) + q * new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
                uv.Add(new Vector2(i / (float)sides, j));
            }
        }

        // i（当前遍历索引）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
for (int i = 0; i < sides; i++)
        {
            // k（候选遍历索引）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数。
int k = sides + 1;
            t.AddRange(new[] { i, i + k, i + 1, i + 1, i + k, i + k + 1 });
        }

        // bottom（底部）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数。
int bottom = v.Count;
        v.Add(a);
        uv.Add(new Vector2(.5f, .5f));
        // top（顶部）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数。
int top = v.Count;
        v.Add(b);
        uv.Add(new Vector2(.5f, .5f));
        // i（当前遍历索引）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数、VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int)。
for (int i = 0; i < sides; i++)
        {
            t.AddRange(new[] { bottom, i, i + 1, top, i + sides + 2, i + sides + 1 });
        }

        MeshObject(p, name, MeshValue(v, t, uv), mat);
    }

    /// <summary>Blade 的业务处理入口；本方法直接依赖 List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List()、Mathf.Max(float, float) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringHerb(Transform, string)；下游（含内部回调体） List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List()、Mathf.Max(float, float) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。</param>
    /// <param name="start">中文含义：起始。使用位置：作为 Quaternion.FromToRotation(Vector3, Vector3) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数 等共 4 处。</param>
    /// <param name="end">中文含义：结束。使用位置：作为 Quaternion.FromToRotation(Vector3, Vector3) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。</param>
    /// <param name="width">中文含义：宽度。使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。</param>
    /// <param name="mat">中文含义：材质。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    /// <param name="phase">中文含义：阶段。使用位置：作为 Mathf.Sin(float) 的参数。</param>
    static void Blade(Transform p, Vector3 start, Vector3 end, float width, Material mat, float phase)
    {
        // v（当前三维向量集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
var v = new List<Vector3>();
        // t（当前整数集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
var t = new List<int>();
        // uv（纹理坐标）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
var uv = new List<Vector2>();
        // q（当前处理的四元数旋转）：保存 Quaternion.FromToRotation(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
var q = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
        // len（长度）：保存 Vector3.Distance(Vector3, Vector3) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
float len = Vector3.Distance(start, end);
        // n（数量）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
const int n = 12;
        // face（面）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
for (int face = 0; face < 2; face++)
        {
            // j（内层遍历索引）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
for (int j = 0; j <= n; j++)
            {
                // x（横向）：使用位置：作为 Vector2.Vector2(float, float) 的参数、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
for (int x = 0; x < 3; x++)
                {
                    // f（系数）：使用位置：作为 Mathf.Sin(float) 的参数、作为 Vector2.Vector2(float, float) 的参数、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
float f = j / (float)n;
                    // leftOperand13（计算前缓存的左侧浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
float leftOperand13 = width * Mathf.Pow(Mathf.Max(0, Mathf.Sin(f * Mathf.PI)), .82f);
                    // leftOperand12（计算前缓存的左侧1）：从1读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
float leftOperand12 = 1;
                    // selectedValue11（分支选出的浮点计算）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
float selectedValue11;
                    if (j % 2 == 0)
                    {
                        selectedValue11 = .07f;
                    }
                    else
                    {
                        selectedValue11 = -.07f;
                    }

                    // w（宽度）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
float w = leftOperand13 * (leftOperand12 + (selectedValue11));
                    // ridge（隆起）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
float ridge;
                    if (x == 1)
                    {
                        ridge = -.024f * Mathf.Sin(f * Mathf.PI);
                    }
                    else
                    {
                        ridge = 0;
                    }

                    // callReceiver21（调用前缓存的当前三维向量集合）：从向量读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
global::System.Collections.Generic.List<global::UnityEngine.Vector3> callReceiver21 = v;
                    // leftOperand20（计算前缓存的左侧起始）：从起始读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 List<Vector3>.Add(Vector3) 的参数。
global::UnityEngine.Vector3 leftOperand20 = start;
                    // leftOperand19（计算前缓存的左侧当前处理的四元数旋转）：从旋转读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 List<Vector3>.Add(Vector3) 的参数。
global::UnityEngine.Quaternion leftOperand19 = q;
                    // argumentValue17（按原顺序缓存的实参浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float argumentValue17 = (x - 1) * w;
                    // argumentValue18（按原顺序缓存的实参浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float argumentValue18 = len * f;
                    // leftOperand16（计算前缓存的左侧正弦的返回结果）：从正弦的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float leftOperand16 = ridge + Mathf.Sin(f * 3 + phase) * .026f;
                    // selectedValue15（分支选出的浮点计算）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
float selectedValue15;
                    if (face == 0)
                    {
                        selectedValue15 = -.0015f;
                    }
                    else
                    {
                        selectedValue15 = .0015f;
                    }

                    callReceiver21.Add(leftOperand20 + leftOperand19 * new Vector3(argumentValue17, argumentValue18, leftOperand16 + (selectedValue15)));
                    uv.Add(new Vector2(x * .5f, f));
                }
            }
        }

        // span（跨度）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
int span = (n + 1) * 3;
        // face（面）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
for (int face = 0; face < 2; face++)
        {
            // j（内层遍历索引）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
for (int j = 0; j < n; j++)
            {
                // x（横向）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
for (int x = 0; x < 2; x++)
                {

                    // 中文含义：a 为“当前网格面片的第1个顶点索引”。；b 为“当前网格面片的第2个顶点索引”。；c 为“当前网格面片的第3个顶点索引”。；d 为“当前网格面片的第4个顶点索引”。
int a = face * span + j * 3 + x, b = a + 1, c = a + 3, d = c + 1;
                    if (face == 0)
                    {
                        t.AddRange(new[] { a, c, b, b, c, d });
                    }
                    else
                    {
                        t.AddRange(new[] { a, b, c, b, d, c });
                    }
                }
            }
        }

        // 叶缘封口使旋转检视时也具有厚度。
        // j（内层遍历索引）：使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
for (int j = 0; j < n; j++)
        {
            // x（横向）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float)。
foreach (int x in new[]
            {
                0,
                2
            }

            )
            {

                // 中文含义：a 为“当前网格面片的第1个顶点索引”。；b 为“当前网格面片的第2个顶点索引”。
int a = j * 3 + x, b = a + 3;
                t.AddRange(new[] { a, a + span, b, b, a + span, b + span });
            }
        }

        MeshObject(p, "弯曲叶片", MeshValue(v, t, uv), mat);
        Tube(p, "叶脉", start + new Vector3(0, 0, -.006f), end + new Vector3(0, 0, -.006f), .0023f, stem, 6);
    }

    /// <summary>AuthoringHerb 的业务处理入口；本方法直接依赖 Color.Color(float, float, float)、Mathf.Cos(float)、Mathf.Sin(float)、string.Contains(string)、Vector3.Lerp(Vector3, Vector3, float)、Vector3.Vector3(float, float, float)、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Build()；下游（含内部回调体） Color.Color(float, float, float)、Mathf.Cos(float)、Mathf.Sin(float)、string.Contains(string)、Vector3.Lerp(Vector3, Vector3, float)、Vector3.Vector3(float, float, float)、VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。</param>
    /// <param name="id">中文含义：编号。使用位置：VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。</param>
    static void AuthoringHerb(Transform p, string id)
    {

        // 中文含义：red 为“是否红色”。；yellow 为“是否黄色”。
bool red = id.Contains("red"), yellow = id.Contains("yellow");
        leaf = MaterialValue("细叶绿色", new Color(.23f, .39f, .13f), 0, .28f, 1);
        // argumentValue24（按原顺序缓存的实参文字“粉红花瓣”）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。
string argumentValue24;
        if (red)
        {
            argumentValue24 = "粉红花瓣";
        }
        else
        {
            argumentValue24 = "金黄花瓣";
        }

        // selectedValue23（分支选出的颜色）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。
global::UnityEngine.Color selectedValue23;
        if (red)
        {
            selectedValue23 = new Color(.72f, .30f, .47f);
        }
        else
        {
            selectedValue23 = new Color(.72f, .65f, .16f);
        }

        flower = MaterialValue(argumentValue24, selectedValue23, 0, .26f, 1);
        // branch（分支）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数 等共 5 处。
for (int branch = 0; branch < 5; branch++)
        {
            // spread（散布）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float spread = (branch - 2) * .19f;
            // basePos（基础位置）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
Vector3 basePos = new Vector3(-.05f, -.75f, .03f * (branch % 2));
            // tip（尖端）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
Vector3 tip = new Vector3(spread, .55f + .12f * Mathf.Cos(branch * 2), .06f * Mathf.Sin(branch * 2));
            Tube(p, "主茎", basePos, tip, .010f, stem);
            // j（内层遍历索引）：使用位置：作为 Mathf.Sin(float) 的参数、作为 VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。
for (int j = 1; j <= 7; j++)
            {
                // f（系数）：使用位置：作为 Vector3.Lerp(Vector3, Vector3, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
float f = j / 8f;
                // at（所在）：保存 Vector3.Lerp(Vector3, Vector3, float) 的返回结果，供当前步骤使用。使用位置：作为 VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。
Vector3 at = Vector3.Lerp(basePos, tip, f);
                // sign（符号）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
foreach (int sign in new[]
                {
                    -1,
                    1
                }

                )
                {
                    // end（结束）：使用位置：作为 VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。
var end = at + new Vector3(sign * (.16f - .08f * f), .13f, .05f * Mathf.Sin(branch + j));
                    Blade(p, at, end, .025f, leaf, branch + j);
                    if ((red || yellow) && j >= 4)
                    {
                        // center（中心）：使用位置：作为 VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 的参数。
Vector3 center = end + new Vector3(0, .025f, -.02f);
                        // petal（花瓣）：使用位置：作为 VillageItemRefinementBuilder.Blade(Transform, Vector3, Vector3, float, Material, float) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。
for (int petal = 0; petal < 5; petal++)
                        {
                            // angle（角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float angle = petal * Mathf.PI * 2 / 5;
                            Blade(p, center, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), -.12f) * .047f, .018f, flower, petal);
                        }
                    }
                }
            }
        }

        // i（当前遍历索引）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、VillageItemRefinementBuilder.AuthoringHerb(Transform, string)。
for (int i = 0; i < 3; i++)
        {
            Tube(p, "根部细枝", new Vector3(-.05f, -.74f, 0), new Vector3(-.05f + (i - 1) * .08f, -.88f, .02f * i), .006f, stem);
        }
    }

    /// <summary>AuthoringMedicineTube 的业务处理入口；本方法直接依赖 Color.Color(float, float, float)、GameObject.GameObject(string)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.List()、Mathf.Cos(float)、Mathf.Sin(float) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Build()；下游（含内部回调体） Color.Color(float, float, float)、GameObject.GameObject(string)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.List()、Mathf.Cos(float)、Mathf.Sin(float) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="id">中文含义：编号。使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。</param>
    static void AuthoringMedicineTube(Transform p, string id)
    {
        // g（当前游戏对象）：使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
var g = new GameObject("玻璃试管");
        g.transform.SetParent(p, false);
        g.transform.localRotation = Quaternion.Euler(0, 0, -22);
        // q（当前变换节点）：使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数、VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
var q = g.transform;
        // 闭合外壁与底部，内部药粒独立建模，避免把药粉画在外壁上。
        Tube(q, "玻璃外壁", new Vector3(0, -.6f, 0), new Vector3(0, .56f, 0), .125f, glass, 40);
        Tube(q, "加厚管口", new Vector3(0, .50f, 0), new Vector3(0, .57f, 0), .139f, glass, 40);
        Tube(q, "软木塞", new Vector3(0, .51f, 0), new Vector3(0, .67f, 0), .113f, cork, 32);
        // green（绿色）：保存 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
var green = MaterialValue("研碎绿草", new Color(.38f, .52f, .16f), 0, .12f, 1);
        // second（第二个材质）：使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
global::UnityEngine.Material second;
        if (id.EndsWith("gr"))
        {
            second = MaterialValue("研碎红草", new Color(.63f, .25f, .22f), 0, .12f, 1);
        }
        else
        {
            if (id.EndsWith("gy"))
            {
                second = MaterialValue("研碎黄草", new Color(.76f, .65f, .17f), 0, .12f, 1);
            }
            else
            {
                second = green;
            }
        }

        // count（数量）：使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
int count;
        if (id.EndsWith("ggg"))
        {
            count = 210;
        }
        else
        {
            count = 150;
        }

        // i（当前遍历索引）：使用位置：作为 Mathf.Sqrt(float) 的参数、作为 Quaternion.Euler(float, float, float) 的参数、VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
for (int i = 0; i < count; i++)
        {
            // angle（角度）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float angle = i * 2.399963f;
            // radius（半径）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float radius = .088f * Mathf.Sqrt(((i * 47) % 101) / 101f);
            // leftOperand30（计算前缓存的左侧浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
float leftOperand30 = -.55f;
            // leftOperand29（计算前缓存的左侧浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
float leftOperand29 = (i / (float)count);
            // selectedValue28（分支选出的浮点计算）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
float selectedValue28;
            if (id.EndsWith("ggg"))
            {
                selectedValue28 = .91f;
            }
            else
            {
                selectedValue28 = .72f;
            }

            // height（高度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float height = leftOperand30 + leftOperand29 * (selectedValue28);
            if (particle == null)
            {
                particle = MeshValue(new List<Vector3> { Vector3.up * .5f, Vector3.down * .5f, Vector3.left * .5f, Vector3.right * .5f, Vector3.forward * .5f, Vector3.back * .5f }, new List<int> { 0, 4, 3, 0, 2, 4, 0, 5, 2, 0, 3, 5, 1, 3, 4, 1, 4, 2, 1, 2, 5, 1, 5, 3 }, new List<Vector2> { Vector2.up, Vector2.zero, Vector2.left, Vector2.right, Vector2.one, Vector2.zero });
            }

            // argumentValue32（按原顺序缓存的实参当前变换节点）：从旋转读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。
global::UnityEngine.Transform argumentValue32 = q;
            // argumentValue33（按原顺序缓存的实参文字“草药颗粒”）：从文字“草药颗粒”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。
string argumentValue33 = "草药颗粒";
            // argumentValue34（按原顺序缓存的实参粒子）：从粒子读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。
global::UnityEngine.Mesh argumentValue34 = particle;
            // selectedValue31（分支选出的第二）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
global::UnityEngine.Material selectedValue31;
            if (i > count / 2)
            {
                selectedValue31 = second;
            }
            else
            {
                selectedValue31 = green;
            }

            // bit（位）：保存 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.AuthoringMedicineTube(Transform, string)。
var bit = MeshObject(argumentValue32, argumentValue33, argumentValue34, selectedValue31);
            bit.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
            bit.transform.localScale = new Vector3(.052f, .046f, .05f);
            bit.transform.localRotation = Quaternion.Euler(i * 31, i * 17, i * 71);
        }
    }

    /// <summary>AuthoringFabricBag 的业务处理入口；本方法直接依赖 Color.Color(float, float, float)、List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List() 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Build()；下游（含内部回调体） Color.Color(float, float, float)、List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List() 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material) 的参数、作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数 等共 4 处。</param>
    /// <param name="large">中文含义：是否较大。使用位置：VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。</param>
    static void AuthoringFabricBag(Transform p, bool large)
    {
        // argumentValue37（按原顺序缓存的实参文字“旧帆布”）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
string argumentValue37;
        if (large)
        {
            argumentValue37 = "旧帆布";
        }
        else
        {
            argumentValue37 = "蓝灰帆布";
        }

        // selectedValue36（分支选出的颜色）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
global::UnityEngine.Color selectedValue36;
        if (large)
        {
            selectedValue36 = new Color(.51f, .43f, .31f);
        }
        else
        {
            selectedValue36 = new Color(.24f, .31f, .41f);
        }

        cloth = MaterialValue(argumentValue37, selectedValue36, 0, .14f, 2);
        // v（当前三维向量集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
var v = new List<Vector3>();
        // t（当前整数集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
var t = new List<int>();
        // uv（纹理坐标）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
var uv = new List<Vector2>();

        // 中文含义：rings 为“环集合”。；sides 为“侧面集合”。
const int rings = 28, sides = 48;
        // length（长度）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
float length;
        if (large)
        {
            length = 1.5f;
        }
        else
        {
            length = 1.2f;
        }

        // j（内层遍历索引）：使用位置：VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int j = 0; j <= rings; j++)
        {
            // i（当前遍历索引）：使用位置：作为 Vector2.Vector2(float, float) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int i = 0; i <= sides; i++)
            {

                // 中文含义：u 为“横向纹理坐标”。；a 为“沿圆周布置部件的角度（弧度）”。
float u = j / (float)rings, a = i * Mathf.PI * 2 / sides;
                // profile（配置档）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float profile = (.74f + .26f * Mathf.Sin(u * Mathf.PI)) * (1 - .55f * Mathf.Exp(-Mathf.Pow((u - .84f) * 16, 2)));
                // wrinkle（皱褶）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float wrinkle = .018f * Mathf.Sin(a * 9 + u * 19) + .012f * Mathf.Sin(a * 17 - u * 31);
                v.Add(new Vector3((u - .5f) * length, Mathf.Cos(a) * (.25f * profile + wrinkle), Mathf.Sin(a) * (.19f * profile + wrinkle)));
                uv.Add(new Vector2(u * 2, i / (float)sides));
            }
        }

        // j（内层遍历索引）：使用位置：VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int j = 0; j < rings; j++)
        {
            // i（当前遍历索引）：使用位置：VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int i = 0; i < sides; i++)
            {

                // 中文含义：a 为“当前网格面片的第1个顶点索引”。；b 为“当前网格面片的第2个顶点索引”。；c 为“当前网格面片的第3个顶点索引”。；d 为“当前网格面片的第4个顶点索引”。
int a = j * (sides + 1) + i, b = a + 1, c = a + sides + 1, d = c + 1;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
        }

        // cap（盖顶）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数。
int cap = v.Count;
        v.Add(new Vector3(length * .5f, 0, 0));
        uv.Add(Vector2.zero);
        // i（当前遍历索引）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int i = 0; i < sides; i++)
        {
            t.AddRange(new[] { cap, rings * (sides + 1) + i, rings * (sides + 1) + i + 1 });
        }

        MeshObject(p, "有褶皱的布套", MeshValue(v, t, uv), cloth);
        // stitching（缝线）：保存 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的返回结果，供当前步骤使用。使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
var stitching = MaterialValue("布包缝线", new Color(.61f, .58f, .49f), 0, .15f, 0);
        // i（当前遍历索引）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int i = 0; i < 42; i++)
        {
            Tube(p, "针脚", new Vector3(-length * .48f + i * length * .022f, .075f, -.193f), new Vector3(-length * .48f + i * length * .022f + .009f, .09f, -.195f), .0035f, stitching, 5);
        }

        // i（当前遍历索引）：使用位置：VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int i = 0; i < 3; i++)
        {
            // x（横向）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
float x = length * .34f + (i - 1) * .014f;
            // previous（前一次）：使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
var previous = new Vector3(x, .12f, 0);
            // j（内层遍历索引）：使用位置：VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int j = 1; j <= 40; j++)
            {
                // a（沿圆周布置部件的角度（弧度））：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float a = j * Mathf.PI * 2 / 40;
                // next（下一次）：使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
var next = new Vector3(x, .12f * Mathf.Cos(a), .105f * Mathf.Sin(a));
                Tube(p, "束口绳", previous, next, .009f, cork, 6);
                previous = next;
            }
        }

        // i（当前遍历索引）：使用位置：VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int i = 0; i < 7; i++)
        {
            // a（起点）：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float a = i * 2.4f;
            // c（候选值（三维向量））：使用位置：作为 VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
var c = new Vector3(-length * .49f, .10f * Mathf.Cos(a), .10f * Mathf.Sin(a));
            // argumentValue40（按原顺序缓存的实参当前变换节点）：从位置读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
global::UnityEngine.Transform argumentValue40 = p;
            // argumentValue41（按原顺序缓存的实参文字“露出金属零件”）：从文字“露出金属零件”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
string argumentValue41 = "露出金属零件";
            // argumentValue42（按原顺序缓存的实参三维向量）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
global::UnityEngine.Vector3 argumentValue42 = c - new Vector3(.08f, 0, 0);
            // argumentValue43（按原顺序缓存的实参三维向量）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
global::UnityEngine.Vector3 argumentValue43 = c + new Vector3(.19f, 0, 0);
            // argumentValue44（按原顺序缓存的实参浮点计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
float argumentValue44 = .038f;
            // selectedValue39（分支选出的钢材）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
global::UnityEngine.Material selectedValue39;
            if (i % 2 == 0)
            {
                selectedValue39 = steel;
            }
            else
            {
                selectedValue39 = copper;
            }

            Tube(argumentValue40, argumentValue41, argumentValue42, argumentValue43, argumentValue44, selectedValue39, 12);
            // j（内层遍历索引）：使用位置：作为 Vector3.Vector3(float, float, float) 的参数、VillageItemRefinementBuilder.AuthoringFabricBag(Transform, bool)。
for (int j = 0; j < 4; j++)
            {
                Block(p, "螺纹", c + new Vector3(j * .025f, 0, 0), new Vector3(.008f, .085f, .085f), steel);
            }
        }

        p.localRotation = Quaternion.Euler(8, 0, -8);
    }

    /// <summary>AuthoringGunpowder 的业务处理入口；本方法直接依赖 Color.Color(float, float, float)、List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List() 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Build()；下游（含内部回调体） Color.Color(float, float, float)、List&lt;int&gt;.AddRange(IEnumerable&lt;int&gt;)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List() 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。</param>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2) 的参数、作为 VillageItemRefinementBuilder.AtlasMaterial(ItemData) 的参数。</param>
    static void AuthoringGunpowder(Transform p, ItemData item)
    {
        // darkColor（深色颜色）：保存 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的返回结果，供当前步骤使用。使用位置：作为 VillageItemRefinementBuilder.Tube(Transform, string, Vector3, Vector3, float, Material, int) 的参数。
var darkColor = MaterialValue("火药罐黑漆", new Color(.085f, .095f, .09f), .45f, .35f, 1);
        Tube(p, "圆形铁罐", new Vector3(0, 0, .12f), new Vector3(0, 0, -.10f), .47f, darkColor, 64);
        Tube(p, "罐盖卷边", new Vector3(0, 0, -.10f), new Vector3(0, 0, -.13f), .48f, steel, 64);
        Tube(p, "罐底卷边", new Vector3(0, 0, .10f), new Vector3(0, 0, .13f), .48f, steel, 64);
        // mat（材质）：保存 VillageItemRefinementBuilder.AtlasMaterial(ItemData) 的返回结果，供当前步骤使用。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。
var mat = AtlasMaterial(item);
        // v（当前三维向量集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)。
var v = new List<Vector3>
        {
            new Vector3(0, 0, -.135f)
        };
        // uv（纹理坐标）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)。
var uv = new List<Vector2>
        {
            AtlasCoordinate(item, new Vector2(.51f, .57f))
        };
        // t（当前整数集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数、VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)。
var t = new List<int>();
        // i（当前遍历索引）：使用位置：VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)。
for (int i = 0; i <= 64; i++)
        {
            // a（沿圆周布置部件的角度（弧度））：使用位置：作为 Mathf.Cos(float) 的参数、作为 Mathf.Sin(float) 的参数。
float a = i * Mathf.PI * 2 / 64;
            v.Add(new Vector3(Mathf.Cos(a) * .44f, Mathf.Sin(a) * .44f, -.135f));
            uv.Add(AtlasCoordinate(item, new Vector2(.51f + Mathf.Cos(a) * .43f, .57f + Mathf.Sin(a) * .39f)));
        }

        // i（当前遍历索引）：使用位置：作为 List<int>.AddRange(IEnumerable<int>) 的参数、VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)。
for (int i = 0; i < 64; i++)
        {
            t.AddRange(new[] { 0, i + 2, i + 1 });
        }

        MeshObject(p, "原作罐盖图案", MeshValue(v, t, uv), mat);
    }

    /// <summary>AuthoringAmmo 的业务处理入口；本方法直接依赖 Color.Color(float, float, float)、Vector3.Vector3(float, float, float)、VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material)、VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)、VillageItemRefinementBuilder.OriginalImageBoxSurface(Transform, ItemData, Vector3, Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, Vector2)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Build()；下游（含内部回调体） Color.Color(float, float, float)、Vector3.Vector3(float, float, float)、VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material)、VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)、VillageItemRefinementBuilder.OriginalImageBoxSurface(Transform, ItemData, Vector3, Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, Vector2)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material) 的参数、作为 VillageItemRefinementBuilder.OriginalImageBoxSurface(Transform, ItemData, Vector3, Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, Vector2) 的参数。</param>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、作为 VillageItemRefinementBuilder.OriginalImageBoxSurface(Transform, ItemData, Vector3, Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, Vector2) 的参数、VillageItemRefinementBuilder.AuthoringAmmo(Transform, ItemData)。</param>
    static void AuthoringAmmo(Transform p, ItemData item)
    {

        // 中文含义：shotgun 为“是否霰弹枪”。；rifle 为“是否步枪”。
bool shotgun = item.itemId == "shotgun_ammo", rifle = item.itemId == "rifle_ammo";
        // color（颜色）：使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、VillageItemRefinementBuilder.AuthoringAmmo(Transform, ItemData)。
global::UnityEngine.Color color;
        if (shotgun)
        {
            color = new Color(.45f, .51f, .22f);
        }
        else
        {
            if (rifle)
            {
                color = new Color(.70f, .67f, .56f);
            }
            else
            {
                color = new Color(.56f, .13f, .10f);
            }
        }

        // card（卡片）：保存 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的返回结果，供当前步骤使用。使用位置：作为 VillageItemRefinementBuilder.Block(Transform, string, Vector3, Vector3, Material) 的参数。
var card = MaterialValue(item.displayName + "纸盒", color, 0, .2f, 1);
        Block(p, "纸盒实体", Vector3.zero, new Vector3(1.15f, .64f, .42f), card);
        // side（侧边）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
foreach (int side in new[]
        {
            -1,
            1
        }

        )
        {
            Block(p, "折叠封边", new Vector3(side * .565f, 0, 0), new Vector3(.015f, .59f, .39f), paper);
            Block(p, "封边折痕", new Vector3(side * .572f, 0, -.11f), new Vector3(.012f, .60f, .008f), card);
        }

        OriginalImageBoxSurface(p, item, new Vector3(-.57f, -.31f, -.214f), new Vector3(-.57f, .31f, -.214f), new Vector3(.57f, .31f, -.214f), new Vector3(.57f, -.31f, -.214f), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
    }

    /// <summary>AtlasMaterial 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)、VillageItemRefinementBuilder.OriginalImageBoxSurface(Transform, ItemData, Vector3, Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, Vector2)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、VillageItemRefinementBuilder.AtlasMaterial(ItemData)。</param>
    static Material AtlasMaterial(ItemData item)
    {
        // mat（材质）：保存 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.AtlasMaterial(ItemData)。
var mat = MaterialValue(item.itemId + "原图包装", Color.white, 0, .2f, 0);
        // heightClear（高度清除）：保存 AssetDatabase.LoadAssetAtPath<Texture2D>(string) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.AtlasMaterial(ItemData)。
var heightClear = AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/材质/霰弹包装高清.png");
        // assignmentReceiver47（赋值前保存的目标材质）：从材质读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.AtlasMaterial(ItemData)。
global::UnityEngine.Material assignmentReceiver47 = mat;
        // selectedValue48（分支选出的高度清除）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：VillageItemRefinementBuilder.AtlasMaterial(ItemData)。
global::UnityEngine.Texture2D selectedValue48;
        if (item.itemId == "shotgun_ammo" && heightClear != null)
        {
            selectedValue48 = heightClear;
        }
        else
        {
            selectedValue48 = item.icon.texture;
        }

        assignmentReceiver47.mainTexture = selectedValue48;
        return mat;
    }

    /// <summary>AtlasCoordinate 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringGunpowder(Transform, ItemData)、VillageItemRefinementBuilder.OriginalImageBoxSurface(Transform, ItemData, Vector3, Vector3, Vector3, Vector3, Vector2, Vector2, Vector2, Vector2)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 Vector2.Vector2(float, float) 的参数、VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2)。</param>
    /// <param name="p">中文含义：位置（二维向量）。使用位置：作为 Vector2.Vector2(float, float) 的参数、VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2)。</param>
    static Vector2 AtlasCoordinate(ItemData item, Vector2 p)
    {
        if (item.itemId == "shotgun_ammo" && AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/材质/霰弹包装高清.png") != null)
        {
            return p;
        }

        // r（当前矩形）：使用位置：作为 Vector2.Vector2(float, float) 的参数。
var r = item.icon.rect;
        return new Vector2((r.x + p.x * r.width) / item.icon.texture.width, (r.y + p.y * r.height) / item.icon.texture.height);
    }

    /// <summary>OriginalImageBoxSurface 的业务处理入口；本方法直接依赖 List&lt;int&gt;.List()、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.List()、VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2)、VillageItemRefinementBuilder.AtlasMaterial(ItemData)、VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material)、VillageItemRefinementBuilder.MeshValue(List&lt;Vector3&gt;, List&lt;int&gt;, List&lt;Vector2&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringAmmo(Transform, ItemData)；下游（含内部回调体） List&lt;int&gt;.List()、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.List()、VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2)、VillageItemRefinementBuilder.AtlasMaterial(ItemData)、VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material)、VillageItemRefinementBuilder.MeshValue(List&lt;Vector3&gt;, List&lt;int&gt;, List&lt;Vector2&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    /// <param name="item">中文含义：当前物品数据配置。使用位置：作为 VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2) 的参数、作为 VillageItemRefinementBuilder.AtlasMaterial(ItemData) 的参数。</param>
    /// <param name="a">中文含义：起点（三维向量）。使用位置：作为 VillageItemRefinementBuilder.MeshValue(List&lt;Vector3&gt;, List&lt;int&gt;, List&lt;Vector2&gt;) 的参数。</param>
    /// <param name="b">中文含义：终点（三维向量）。使用位置：作为 VillageItemRefinementBuilder.MeshValue(List&lt;Vector3&gt;, List&lt;int&gt;, List&lt;Vector2&gt;) 的参数。</param>
    /// <param name="c">中文含义：候选值（三维向量）。使用位置：作为 VillageItemRefinementBuilder.MeshValue(List&lt;Vector3&gt;, List&lt;int&gt;, List&lt;Vector2&gt;) 的参数。</param>
    /// <param name="d">中文含义：距离（三维向量）。使用位置：作为 VillageItemRefinementBuilder.MeshValue(List&lt;Vector3&gt;, List&lt;int&gt;, List&lt;Vector2&gt;) 的参数。</param>
    /// <param name="ua">中文含义：第一纹理坐标。使用位置：作为 VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2) 的参数。</param>
    /// <param name="ub">中文含义：第二纹理坐标。使用位置：作为 VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2) 的参数。</param>
    /// <param name="uc">中文含义：第三纹理坐标。使用位置：作为 VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2) 的参数。</param>
    /// <param name="ud">中文含义：第四纹理坐标。使用位置：作为 VillageItemRefinementBuilder.AtlasCoordinate(ItemData, Vector2) 的参数。</param>
    static void OriginalImageBoxSurface(Transform p, ItemData item, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
    {
        MeshObject(p, "原作包装正面", MeshValue(new List<Vector3> { a, b, c, d }, new List<int> { 0, 1, 2, 0, 2, 3 }, new List<Vector2> { AtlasCoordinate(item, ua), AtlasCoordinate(item, ub), AtlasCoordinate(item, uc), AtlasCoordinate(item, ud) }), AtlasMaterial(item));
    }

    /// <summary>Label 的业务处理入口；本方法直接依赖 AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、Color.Color(float, float, float)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.List()、Mathf.PerlinNoise(float, float) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、Color.Color(float, float, float)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.List()、Mathf.PerlinNoise(float, float) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="p">中文含义：当前变换节点。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数。</param>
    /// <param name="name">中文含义：名称。使用位置：作为 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的参数、作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。</param>
    /// <param name="pos">中文含义：位置。使用位置：VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。</param>
    /// <param name="text">中文含义：文字。使用位置：作为 VillageItemRefinementBuilder.WriteCharacter(Texture2D, string) 的参数。</param>
    /// <param name="color">中文含义：颜色。使用位置：VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。</param>
    /// <param name="back">中文含义：是否后方。使用位置：VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。</param>
    static void Label(Transform p, string name, Vector3 pos, Vector2 size, string text, Color color, bool back = false)
    {
        // path（路径）：使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath<Texture2D>(string) 的参数。
string path = directory + "/材质/" + currentIdentifier + name + ".asset";
        // tex（纹理）：保存 AssetDatabase.LoadAssetAtPath<Texture2D>(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 VillageItemRefinementBuilder.WriteCharacter(Texture2D, string) 的参数、VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
        {
            tex = new Texture2D(512, 256, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            // y（纵向）：使用位置：作为 Mathf.PerlinNoise(float, float) 的参数、VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
for (int y = 0; y < 256; y++)
            {
                // x（横向）：使用位置：作为 Mathf.PerlinNoise(float, float) 的参数、VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
for (int x = 0; x < 512; x++)
                {
                    // n（纹理颜色使用的亮度噪声系数）：使用位置：VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
float n = .85f + .15f * Mathf.PerlinNoise(x * .18f, y * .18f);
                    // border（是否边框）：使用位置：VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
bool border = x < 9 || x > 502 || y < 9 || y > 246;
                    // callReceiver52（调用前缓存的纹理）：从纹理读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
global::UnityEngine.Texture2D callReceiver52 = tex;
                    // argumentValue50（按原顺序缓存的实参横向）：从横向读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Texture2D.SetPixel(int, int, Color) 的参数。
int argumentValue50 = x;
                    // argumentValue51（按原顺序缓存的实参纵向）：从纵向读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Texture2D.SetPixel(int, int, Color) 的参数。
int argumentValue51 = y;
                    // selectedValue49（分支选出的颜色）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Texture2D.SetPixel(int, int, Color) 的参数、VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
global::UnityEngine.Color selectedValue49;
                    if (border)
                    {
                        selectedValue49 = new Color(.77f, .74f, .64f);
                    }
                    else
                    {
                        selectedValue49 = color * n;
                    }

                    callReceiver52.SetPixel(argumentValue50, argumentValue51, selectedValue49);
                }
            }

            WriteCharacter(tex, text);
            tex.Apply();
            AssetDatabase.CreateAsset(tex, path);
        }

        // mat（材质）：保存 VillageItemRefinementBuilder.MaterialValue(string, Color, float, float, int) 的返回结果，供当前步骤使用。使用位置：作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
var mat = MaterialValue(currentIdentifier + name, Color.white, 0, .19f, 0);
        mat.mainTexture = tex;
        // v（当前三维向量集合）：使用位置：作为 VillageItemRefinementBuilder.MeshValue(List<Vector3>, List<int>, List<Vector2>) 的参数。
var v = new List<Vector3>
        {
            new Vector3(-size.x / 2, -size.y / 2, 0),
            new Vector3(-size.x / 2, size.y / 2, 0),
            new Vector3(size.x / 2, size.y / 2, 0),
            new Vector3(size.x / 2, -size.y / 2, 0)
        };
        // go（当前游戏对象）：保存 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)。
var go = MeshObject(p, name, MeshValue(v, new List<int> { 0, 1, 2, 0, 2, 3 }, new List<Vector2> { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }), mat);
        go.transform.localPosition = pos;
        if (back)
        {
            go.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }
    }

    /// <summary>WriteCharacter 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;Font&gt;(string)、Color.Color(float, float, float)、Color.Lerp(Color, Color, float)、Font.GetCharacterInfo(char, out CharacterInfo, int)、Font.RequestCharactersInTexture(string, int, FontStyle)、Graphics.Blit(Texture, RenderTexture)、Mathf.Max(int, int) 等共 19 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Label(Transform, string, Vector3, Vector2, string, Color, bool)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;Font&gt;(string)、Color.Color(float, float, float)、Color.Lerp(Color, Color, float)、Font.GetCharacterInfo(char, out CharacterInfo, int)、Font.RequestCharactersInTexture(string, int, FontStyle)、Graphics.Blit(Texture, RenderTexture)、Mathf.Max(int, int) 等共 19 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">中文含义：目标二维纹理。使用位置：作为 Color.Lerp(Color, Color, float) 的参数、VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。</param>
    /// <param name="text">中文含义：文字。使用位置：作为 Font.RequestCharactersInTexture(string, int, FontStyle) 的参数、VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。</param>
    static void WriteCharacter(Texture2D target, string text)
    {
        // font（字体）：保存 AssetDatabase.LoadAssetAtPath<Font>(string) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/重制界面/中文正文字体.otf");
        if (font == null)
        {
            return;
        }

        // size（尺寸）：使用位置：作为 Font.GetCharacterInfo(char, out CharacterInfo, int) 的参数、作为 Font.RequestCharactersInTexture(string, int, FontStyle) 的参数。
const int size = 36;
        font.RequestCharactersInTexture(text, size, FontStyle.Normal);
        // source（来源纹理）：使用位置：作为 Graphics.Blit(Texture, RenderTexture) 的参数、作为 Rect.Rect(float, float, float, float) 的参数、作为 RenderTexture.GetTemporary(int, int, int) 的参数 等共 4 处。
var source = font.material.mainTexture;
        // rt（当前渲染纹理）：保存 RenderTexture.GetTemporary(int, int, int) 的返回结果，供当前步骤使用。使用位置：作为 Graphics.Blit(Texture, RenderTexture) 的参数、作为 RenderTexture.ReleaseTemporary(RenderTexture) 的参数、VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
var rt = RenderTexture.GetTemporary(source.width, source.height, 0);
        Graphics.Blit(source, rt);
        // previous（前一次）：使用位置：VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
var previous = RenderTexture.active;
        RenderTexture.active = rt;
        // copy（副本）：使用位置：作为 Object.DestroyImmediate(Object) 的参数、VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        // lines（线集合）：保存 string.Split(char, StringSplitOptions) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
string[] lines = text.Split('\n');
        // line（线）：使用位置：VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
for (int line = 0; line < lines.Length; line++)
        {
            // total（总计）：使用位置：VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
int total = 0;
            // ch（通道）：每轮从线集合取得一个元素，仅代表当前这一项。使用位置：作为 Font.GetCharacterInfo(char, out CharacterInfo, int) 的参数。
foreach (char ch in lines[line])
            {
                font.GetCharacterInfo(ch, out var /* 中文含义：当前字符信息。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ info, size);
                total += info.advance;
            }


            // 中文含义：pen 为“笔画”。；baseline 为“基准”。
int pen = (target.width - total) / 2, baseline = 188 - line * 60;
            // ch（通道）：每轮从线集合取得一个元素，仅代表当前这一项。使用位置：作为 Font.GetCharacterInfo(char, out CharacterInfo, int) 的参数。
foreach (char ch in lines[line])
            {
                if (!font.GetCharacterInfo(ch, out var /* 中文含义：当前字符信息。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ c, size))
                {
                    continue;
                }

                // y（纵向）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
for (int y = 0; y < c.glyphHeight; y++)
                {
                    // x（横向）：使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数、VillageItemRefinementBuilder.WriteCharacter(Texture2D, string)。
for (int x = 0; x < c.glyphWidth; x++)
                    {
                        // low（低）：保存 Vector2.Lerp(Vector2, Vector2, float) 的返回结果，供当前步骤使用。使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数。
var low = Vector2.Lerp(c.uvBottomLeft, c.uvBottomRight, x / (float)Mathf.Max(1, c.glyphWidth - 1));
                        // high（高）：保存 Vector2.Lerp(Vector2, Vector2, float) 的返回结果，供当前步骤使用。使用位置：作为 Vector2.Lerp(Vector2, Vector2, float) 的参数。
var high = Vector2.Lerp(c.uvTopLeft, c.uvTopRight, x / (float)Mathf.Max(1, c.glyphWidth - 1));
                        // uv（纹理坐标）：保存 Vector2.Lerp(Vector2, Vector2, float) 的返回结果，供当前步骤使用。使用位置：作为 Texture2D.GetPixelBilinear(float, float) 的参数。
var uv = Vector2.Lerp(low, high, y / (float)Mathf.Max(1, c.glyphHeight - 1));
                        // a（源图片采样像素的透明度）：使用位置：作为 Color.Lerp(Color, Color, float) 的参数。
float a = copy.GetPixelBilinear(uv.x, uv.y).a;

                        // 中文含义：px 为“像素横坐标”。；py 为“像素纵坐标”。
int px = pen + c.minX + x, py = baseline + c.minY + y;
                        if (px >= 0 && px < target.width && py >= 0 && py < target.height)
                        {
                            target.SetPixel(px, py, Color.Lerp(target.GetPixel(px, py), new Color(.92f, .89f, .77f), a));
                        }
                    }
                }

                pen += c.advance;
            }
        }

        UnityEngine.Object.DestroyImmediate(copy);
    }

    /// <summary>Boundary 的业务处理入口；本方法直接依赖 Bounds.Encapsulate(Bounds)、GameObject.GetComponentsInChildren&lt;Renderer&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)、VillageItemRefinementBuilder.Build()；下游（含内部回调体） Bounds.Encapsulate(Bounds)、GameObject.GetComponentsInChildren&lt;Renderer&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根游戏对象。使用位置：VillageItemRefinementBuilder.Boundary(GameObject)。</param>
    static Bounds Boundary(GameObject root)
    {
        // rr（渲染器集合）：保存 GameObject.GetComponentsInChildren<Renderer>() 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.Boundary(GameObject)。
var rr = root.GetComponentsInChildren<Renderer>();
        // b（当前包围盒）：使用位置：VillageItemRefinementBuilder.Boundary(GameObject)。
var b = rr[0].bounds;
        // r（当前渲染器）：每轮从渲染器集合取得一个元素，仅代表当前这一项。使用位置：作为 Bounds.Encapsulate(Bounds) 的参数。
foreach (var r in rr)
        {
            b.Encapsulate(r.bounds);
        }

        return b;
    }

    /// <summary>AuthoringBrokenBlade 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;MeshFilter&gt;()、Mathf.Sin(float)、Mesh.RecalculateBounds()、Mesh.RecalculateNormals()、Object.Instantiate&lt;Mesh&gt;(Mesh)、Transform.InverseTransformPoint(Vector3)、Transform.TransformPoint(Vector3) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Build()；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;MeshFilter&gt;()、Mathf.Sin(float)、Mesh.RecalculateBounds()、Mesh.RecalculateNormals()、Object.Instantiate&lt;Mesh&gt;(Mesh)、Transform.InverseTransformPoint(Vector3)、Transform.TransformPoint(Vector3) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根游戏对象。使用位置：作为 VillageItemRefinementBuilder.Boundary(GameObject) 的参数、VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)。</param>
    static void AuthoringBrokenBlade(GameObject root)
    {
        // 仅修改展示副本的刀尖轮廓，原持刀模型与战斗绑定保持原状。
        // bounds（包围盒）：保存 VillageItemRefinementBuilder.Boundary(GameObject) 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)。
var bounds = Boundary(root);
        // cut（切口）：使用位置：VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)。
float cut = bounds.min.y + bounds.size.y * .72f;
        // f（当前网格过滤器）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.Instantiate<Mesh>(Mesh) 的参数、VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)。
foreach (var f in root.GetComponentsInChildren<MeshFilter>())
        {
            if (f.sharedMesh == null || !f.sharedMesh.isReadable)
            {
                continue;
            }

            // m（当前网格）：保存 Object.Instantiate<Mesh>(Mesh) 的返回结果，供当前步骤使用。使用位置：作为 VillageItemRefinementBuilder.SaveMesh(Mesh, string) 的参数、VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)。
var m = UnityEngine.Object.Instantiate(f.sharedMesh);
            // vertices（顶点集合）：使用位置：作为 Transform.TransformPoint(Vector3) 的参数、VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)。
var vertices = m.vertices;
            // i（当前遍历索引）：使用位置：VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)。
for (int i = 0; i < vertices.Length; i++)
            {
                // world（世界空间）：保存 Transform.TransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Sin(float) 的参数、作为 Transform.InverseTransformPoint(Vector3) 的参数、VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)。
var world = f.transform.TransformPoint(vertices[i]);
                if (world.y <= cut)
                {
                    continue;
                }

                world.y = cut + Mathf.Sin(world.x * 137) * bounds.size.y * .008f;
                vertices[i] = f.transform.InverseTransformPoint(world);
            }

            m.vertices = vertices;
            m.RecalculateNormals();
            m.RecalculateBounds();
            f.sharedMesh = SaveMesh(m, "断刃");
        }
    }

    /// <summary>SaveMesh 的业务处理入口；本方法直接依赖 AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Mesh&gt;(string)、EditorUtility.SetDirty(Object)、Mesh.Clear(bool)、Mesh.GetTriangles(int)、Mesh.SetTriangles(int[], int)、Mesh.UploadMeshData(bool) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.AuthoringBrokenBlade(GameObject)；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Mesh&gt;(string)、EditorUtility.SetDirty(Object)、Mesh.Clear(bool)、Mesh.GetTriangles(int)、Mesh.SetTriangles(int[], int)、Mesh.UploadMeshData(bool) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="m">中文含义：当前网格。使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 Mesh.SetTriangles(int[], int) 的参数、作为 Object.DestroyImmediate(Object) 的参数 等共 4 处。</param>
    /// <param name="name">中文含义：名称。使用位置：VillageItemRefinementBuilder.SaveMesh(Mesh, string)。</param>
    static Mesh SaveMesh(Mesh m, string name)
    {
        // path（路径）：使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath<Mesh>(string) 的参数。
string path = directory + "/网格/" + currentIdentifier + "_" + name + "_" + (meshSequence++) + ".asset";
        // old（原有网格）：保存 AssetDatabase.LoadAssetAtPath<Mesh>(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、VillageItemRefinementBuilder.SaveMesh(Mesh, string)。
var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null)
        {
            // 显式更新网格缓冲，避免资源数据已更新而显卡仍使用旧顶点和纹理坐标。
            old.Clear(false);
            old.indexFormat = m.indexFormat;
            old.vertices = m.vertices;
            old.normals = m.normals;
            old.tangents = m.tangents;
            old.uv = m.uv;
            old.subMeshCount = m.subMeshCount;
            // i（当前遍历索引）：使用位置：作为 Mesh.GetTriangles(int) 的参数、作为 Mesh.SetTriangles(int[], int) 的参数、VillageItemRefinementBuilder.SaveMesh(Mesh, string)。
for (int i = 0; i < m.subMeshCount; i++)
            {
                old.SetTriangles(m.GetTriangles(i), i);
            }

            old.bounds = m.bounds;
            old.UploadMeshData(false);
            EditorUtility.SetDirty(old);
            UnityEngine.Object.DestroyImmediate(m);
            return old;
        }

        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    /// <summary>Merge 的业务处理入口；本方法直接依赖 AssetDatabase.Contains(Object)、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Mesh&gt;(string)、CombineInstance.CombineInstance()、Component.GetComponent&lt;MeshRenderer&gt;()、Dictionary&lt;Material, List&lt;CombineInstance&gt; &gt;.ContainsKey(Material)、Dictionary&lt;Material, List&lt;CombineInstance&gt; &gt;.Dictionary() 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillageItemRefinementBuilder.Build()；下游（含内部回调体） AssetDatabase.Contains(Object)、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Mesh&gt;(string)、CombineInstance.CombineInstance()、Component.GetComponent&lt;MeshRenderer&gt;()、Dictionary&lt;Material, List&lt;CombineInstance&gt; &gt;.ContainsKey(Material)、Dictionary&lt;Material, List&lt;CombineInstance&gt; &gt;.Dictionary() 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根游戏对象。使用位置：作为 List&lt;CombineInstance&gt;.Add(CombineInstance) 的参数、作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、VillageItemRefinementBuilder.Merge(GameObject)。</param>
    static void Merge(GameObject root)
    {
        // groups（分组集合）：使用位置：VillageItemRefinementBuilder.Merge(GameObject)。
var groups = new Dictionary<Material, List<CombineInstance>>();
        // oldMeshes（旧网格集合）：使用位置：VillageItemRefinementBuilder.Merge(GameObject)。
var oldMeshes = new HashSet<Mesh>();
        // f（当前网格过滤器）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.Contains(Object) 的参数、作为 HashSet<Mesh>.Add(Mesh) 的参数、作为 List<CombineInstance>.Add(CombineInstance) 的参数 等共 4 处。
foreach (var f in root.GetComponentsInChildren<MeshFilter>())
        {
            // r（当前网格渲染器）：保存 Component.GetComponent<MeshRenderer>() 的返回结果，供当前步骤使用。使用位置：VillageItemRefinementBuilder.Merge(GameObject)。
var r = f.GetComponent<MeshRenderer>();
            if (r == null || f.sharedMesh == null)
            {
                continue;
            }

            // mat（材质）：使用位置：作为 Dictionary<Material, List<CombineInstance>>.ContainsKey(Material) 的参数、作为 Dictionary<Material, List<CombineInstance>>.this[Material] 的参数。
var mat = r.sharedMaterial;
            if (!groups.ContainsKey(mat))
            {
                groups[mat] = new List<CombineInstance>();
            }

            groups[mat].Add(new CombineInstance { mesh = f.sharedMesh, transform = root.transform.worldToLocalMatrix * f.transform.localToWorldMatrix });
            if (!AssetDatabase.Contains(f.sharedMesh))
            {
                oldMeshes.Add(f.sharedMesh);
            }
        }

        // c（当前变换节点）：每轮从投射的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数。
foreach (Transform c in root.transform.Cast<Transform>().ToArray())
        {
            UnityEngine.Object.DestroyImmediate(c.gameObject);
        }

        // kv（键值对）：每轮从分组集合取得一个元素，仅代表当前这一项。使用位置：作为 Mesh.CombineMeshes(CombineInstance[], bool, bool) 的参数、作为 VillageItemRefinementBuilder.MeshObject(Transform, string, Mesh, Material) 的参数、VillageItemRefinementBuilder.Merge(GameObject)。
foreach (var kv in groups)
        {
            // mesh（网格）：使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 EditorUtility.CopySerialized(Object, Object) 的参数、作为 Object.DestroyImmediate(Object) 的参数 等共 5 处。
var mesh = new Mesh
            {
                name = currentIdentifier + kv.Key.name,
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true);
            // path（路径）：使用位置：作为 AssetDatabase.CreateAsset(Object, string) 的参数、作为 AssetDatabase.LoadAssetAtPath<Mesh>(string) 的参数。
string path = directory + "/网格/" + currentIdentifier + "_" + kv.Key.name + ".asset";
            // old（原有网格）：保存 AssetDatabase.LoadAssetAtPath<Mesh>(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.CopySerialized(Object, Object) 的参数、VillageItemRefinementBuilder.Merge(GameObject)。
var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null)
            {
                EditorUtility.CopySerialized(mesh, old);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = old;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }

            MeshObject(root.transform, kv.Key.name, mesh, kv.Key);
        }

        // m（当前网格）：每轮从旧网格集合取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数。
foreach (var m in oldMeshes)
        {
            UnityEngine.Object.DestroyImmediate(m);
        }
    }
}

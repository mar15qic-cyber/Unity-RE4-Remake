using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>制作菜单与公文包资源，并将引用显式保存到项目场景。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "界面改造构建器")]
public static class InterfaceReworkBuilder
{
    /// <summary>目录。编译期常量，保持既有值与消费者映射。</summary>
    const string directory = "Assets/UI/重制界面";
    /// <summary>倒角网格。</summary>
    static Mesh bevelMesh;
    /// <summary>leather 表示“皮革”；lining 表示“内衬”；metal 表示“金属”；darkMetal 表示“深色金属”；paper 表示“纸张”；greenLeaf 表示“绿色叶片”；redLeaf 表示“红色叶片”；yellowLeaf 表示“黄色叶片”。</summary>
    static Material leather, lining, metal, darkMetal, paper, greenLeaf, redLeaf, yellowLeaf;
    /// <summary>主题。</summary>
    static UIVisualTheme theme;
    /// <summary>配置。</summary>
    static InventoryPresentationConfiguration configuration;
    /// <summary>GenerateAsset 的业务处理入口；本方法直接依赖 AssetDatabase.CopyAsset(string, string)、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.FindAssets(string)、AssetDatabase.GetAssetPath(Object)、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;Font&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string) 等共 33 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.CopyAsset(string, string)、AssetDatabase.CreateAsset(Object, string)、AssetDatabase.FindAssets(string)、AssetDatabase.GetAssetPath(Object)、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;Font&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string) 等共 33 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string GenerateAsset()
    {
        BuildDirectory(directory);
        BuildDirectory(directory + "/模型");
        BuildDirectory(directory + "/材质");
        bevelMesh = AssetDatabase.LoadAssetAtPath<Mesh>(directory + "/倒角网格.asset");
        if (bevelMesh == null)
        {
            bevelMesh = CreateBevelMesh();
            AssetDatabase.CreateAsset(bevelMesh, directory + "/倒角网格.asset");
        }

        leather = MaterialValue("旧皮革", new Color(0.095f, 0.073f, 0.049f), 0.05f, 0.26f, true);
        lining = MaterialValue("深灰内衬", new Color(0.13f, 0.145f, 0.13f), 0f, 0.12f, true);
        metal = MaterialValue("磨损黄铜", new Color(0.44f, 0.4f, 0.29f), 0.72f, 0.48f, false);
        darkMetal = MaterialValue("枪灰金属", new Color(0.17f, 0.19f, 0.18f), 0.7f, 0.35f, false);
        paper = MaterialValue("旧纸包装", new Color(0.6f, 0.56f, 0.43f), 0f, 0.2f, true);
        greenLeaf = MaterialValue("草药绿色", new Color(0.14f, 0.34f, 0.09f), 0f, 0.24f, false);
        redLeaf = MaterialValue("草药红色", new Color(0.42f, 0.08f, 0.055f), 0f, 0.24f, false);
        yellowLeaf = MaterialValue("草药黄色", new Color(0.63f, 0.48f, 0.07f), 0f, 0.24f, false);
        theme = AssetDatabase.LoadAssetAtPath<UIVisualTheme>(directory + "/统一视觉主题.asset");
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<UIVisualTheme>();
            AssetDatabase.CreateAsset(theme, directory + "/统一视觉主题.asset");
        }

        // 字体路径。
string fontPath = directory + "/中文正文字体.otf";
        if (AssetDatabase.LoadAssetAtPath<Font>(fontPath) == null)
        {
            AssetDatabase.CopyAsset("Packages/cn.tuanjie.ai.generators/Editor/Fonts/SourceHanSansSC-Regular.otf", fontPath);
        }

        theme.interfaceFont = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        theme.primaryColor = theme.menuText;
        theme.accentColor = theme.menuLine;
        EditorUtility.SetDirty(theme);
        configuration = AssetDatabase.LoadAssetAtPath<InventoryPresentationConfiguration>(directory + "/背包展示配置.asset");
        if (configuration == null)
        {
            configuration = ScriptableObject.CreateInstance<InventoryPresentationConfiguration>();
            AssetDatabase.CreateAsset(configuration, directory + "/背包展示配置.asset");
        }

        configuration.attacheCaseModel = GenerateAttacheCase();
        // 表现。
var presentation = new List<InventoryPresentationConfiguration.ItemPresentation>();
        // guid（资源标识）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.GUIDToAssetPath(string) 的参数。
foreach (string guid in AssetDatabase.FindAssets("t:ItemData"))
        {
            // 当前物品数据配置。
var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null)
            {
                continue;
            }

            // 精修。
var refinement = configuration.Find(item.itemId);
            if (refinement != null && refinement.model != null && AssetDatabase.GetAssetPath(refinement.model).StartsWith(directory + "/精修物品/"))
            {
                presentation.Add(refinement);
                continue;
            }

            // 来源游戏对象。
GameObject source = item.GetInspectionModelPrefab();
            // 根游戏对象。
GameObject root;
            // 是否具有。
bool hasHas = source != null;
            if (hasHas)
            {
                root = UnityEngine.Object.Instantiate(source);
                root.name = item.displayName + "展示模型";
                CleanupPresentationComponent(root);
            }
            else
            {
                root = AuthoringProp(item);
            }

            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            // 旋转值。
Vector3 rotationValue;
            if (hasHas)
            {
                rotationValue = BestOrientation(root, item.gridWidth, item.gridHeight);
            }
            else
            {
                rotationValue = Vector3.zero;
            }

            // 名称。
string name = item.displayName.Replace("/", "_").Replace("M16 ", "");
            // 预制件。
var prefab = PrefabUtility.SaveAsPrefabAsset(root, directory + "/模型/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(root);
            presentation.Add(new InventoryPresentationConfiguration.ItemPresentation { itemIdentifier = item.itemId, model = prefab, orientation = rotationValue, gridOccupancyRatio = 0.88f });
        }

        configuration.item = presentation.ToArray();
        EditorUtility.SetDirty(configuration);
        AssetDatabase.SaveAssets();
        return "已生成公文包与" + presentation.Count + "种物品的展示配置";
    }

    /// <summary>BuildDirectory 的业务处理入口；本方法直接依赖 AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)、InterfaceReworkBuilder.BuildDirectory(string)、string.LastIndexOf(char)、string.Substring(int, int)、string.Substring(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.BuildDirectory(string)、InterfaceReworkBuilder.GenerateAsset()；下游（含内部回调体） AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)、InterfaceReworkBuilder.BuildDirectory(string)、string.LastIndexOf(char)、string.Substring(int, int)、string.Substring(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    static void BuildDirectory(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        // 当前遍历索引。
int i = path.LastIndexOf('/');
        BuildDirectory(path.Substring(0, i));
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    /// <summary>MaterialValue 的业务处理入口；本方法直接依赖 AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、Color.Color(float, float, float)、EditorUtility.SetDirty(Object)、Material.Material(Shader)、Material.SetFloat(string, float) 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.GenerateAsset()；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)、Color.Color(float, float, float)、EditorUtility.SetDirty(Object)、Material.Material(Shader)、Material.SetFloat(string, float) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="nameValue">名称值。</param>
    /// <param name="colorValue">颜色值。</param>
    /// <param name="metallic">金属度。</param>
    /// <param name="smoothness">光滑度。</param>
    /// <param name="texture">是否为材质生成并绑定纹理。</param>
    static Material MaterialValue(string nameValue, Color colorValue, float metallic, float smoothness, bool texture)
    {
        // 当前处理的资源路径。
string p = directory + "/材质/" + nameValue + ".mat";
        // 材质。
var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, p);
        }

        mat.color = colorValue;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness);
        if (texture)
        {
            // 纹理坐标。
string tp = directory + "/材质/" + nameValue + "纹理.asset";
            // 纹理。
var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            if (tex == null)
            {
                tex = new Texture2D(128, 128, TextureFormat.RGB24, true);
                tex.name = nameValue + "纹理";
                // 纵向。
for (int y = 0; y < 128; y++)
                {
                    // 横向。
for (int x = 0; x < 128; x++)
                    {
                        // 纹理颜色使用的亮度噪声系数。
float n = 0.62f + Mathf.PerlinNoise(x * 0.32f, y * 0.32f) * 0.28f + Mathf.PerlinNoise(x * 1.7f, y * 1.7f) * 0.1f;
                        tex.SetPixel(x, y, new Color(n, n, n));
                    }
                }

                tex.Apply();
                AssetDatabase.CreateAsset(tex, tp);
            }

            mat.mainTexture = tex;
            mat.mainTextureScale = new Vector2(4, 4);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>CreateBevelMesh 的业务处理入口；本方法直接依赖 List&lt;int&gt;.Add(int)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List()、Mathf.Abs(float) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.GenerateAsset()；下游（含内部回调体） List&lt;int&gt;.Add(int)、List&lt;int&gt;.List()、List&lt;Vector2&gt;.Add(Vector2)、List&lt;Vector2&gt;.List()、List&lt;Vector3&gt;.Add(Vector3)、List&lt;Vector3&gt;.List()、Mathf.Abs(float) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static Mesh CreateBevelMesh()
    {
        // 当前三维向量集合。
var v = new List<Vector3>();
        // 当前三维向量集合。
var n = new List<Vector3>();
        // 纹理坐标。
var uv = new List<Vector2>();
        // 三角形索引集合。
var tris = new List<int>();
        // 法线集合。
Vector3[] normals =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };
        // 步数。
const int steps = 8;
        // normal（表面法线）：每轮从法线集合取得一个元素，仅代表当前这一项。使用位置：作为 Mathf.Abs(float) 的参数、作为 Vector3.Cross(Vector3, Vector3) 的参数、InterfaceReworkBuilder.CreateBevelMesh()。
foreach (var normal in normals)
        {
            // 横向纹理坐标。
Vector3 u;
            if (Mathf.Abs(normal.y) > 0.9f)
            {
                u = Vector3.right;
            }
            else
            {
                u = Vector3.up;
            }

            // 跨越的返回结果。
Vector3 w = Vector3.Cross(normal, u);
            // 起始。
int start = v.Count;
            // 纵向。
for (int y = 0; y <= steps; y++)
            {
                // 横向。
for (int x = 0; x <= steps; x++)
                {
                    // p（位置（三维向量））：使用位置：作为 Mathf.Clamp(float, float, float) 的参数、InterfaceReworkBuilder.CreateBevelMesh()。
var p = normal * 0.5f + u * (x / (float)steps - 0.5f) + w * (y / (float)steps - 0.5f);
                    // 限制的返回结果。
var c = new Vector3(Mathf.Clamp(p.x, -0.43f, 0.43f), Mathf.Clamp(p.y, -0.43f, 0.43f), Mathf.Clamp(p.z, -0.43f, 0.43f));
                    // d（距离（三维向量））：使用位置：作为 List<Vector3>.Add(Vector3) 的参数。
var d = (p - c).normalized;
                    v.Add(c + d * 0.07f);
                    n.Add(d);
                    uv.Add(new Vector2(x / (float)steps, y / (float)steps));
                }
            }

            // 纵向。
for (int y = 0; y < steps; y++)
            {
                // 横向。
for (int x = 0; x < steps; x++)
                {

                    // 中文含义：a 为“当前网格面片的第1个顶点索引”。；b 为“当前网格面片的第2个顶点索引”。；c 为“当前网格面片的第3个顶点索引”。；d 为“当前网格面片的第4个顶点索引”。
int a = start + y * (steps + 1) + x, b = a + 1, c = a + steps + 1, d = c + 1;
                    tris.Add(a);
                    tris.Add(b);
                    tris.Add(c);
                    tris.Add(b);
                    tris.Add(d);
                    tris.Add(c);
                }
            }
        }

        // 网格。
var mesh = new Mesh
        {
            name = "圆角实体网格"
        };
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Block 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;MeshFilter&gt;()、GameObject.GetComponent&lt;MeshRenderer&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.AuthoringProp(ItemData)、InterfaceReworkBuilder.GenerateAttacheCase()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;MeshFilter&gt;()、GameObject.GetComponent&lt;MeshRenderer&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="name">名称。</param>
    /// <param name="position">位置。</param>
    /// <param name="largeSmall">较大较小。</param>
    /// <param name="material">材质。</param>
    static GameObject Block(Transform parent, string name, Vector3 position, Vector3 largeSmall, Material material)
    {
        // 当前游戏对象。
var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = largeSmall;
        go.GetComponent<MeshFilter>().sharedMesh = bevelMesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    /// <summary>SphereValue 的业务处理入口；本方法直接依赖 GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Collider&gt;()、GameObject.GetComponent&lt;Renderer&gt;()、Object.DestroyImmediate(Object)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.AuthoringProp(ItemData)；下游（含内部回调体） GameObject.CreatePrimitive(PrimitiveType)、GameObject.GetComponent&lt;Collider&gt;()、GameObject.GetComponent&lt;Renderer&gt;()、Object.DestroyImmediate(Object)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="nameValue">名称值。</param>
    /// <param name="position">位置。</param>
    /// <param name="largeSmall">较大较小。</param>
    /// <param name="material">材质。</param>
    static GameObject SphereValue(Transform parent, string nameValue, Vector3 position, Vector3 largeSmall, Material material)
    {
        // 当前游戏对象。
var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = nameValue;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = largeSmall;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    /// <summary>GenerateAttacheCase 的业务处理入口；本方法直接依赖 GameObject.GameObject(string)、InterfaceReworkBuilder.Block(Transform, string, Vector3, Vector3, Material)、Object.DestroyImmediate(Object)、PrefabUtility.SaveAsPrefabAsset(GameObject, string)、Transform.SetParent(Transform, bool)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.GenerateAsset()；下游（含内部回调体） GameObject.GameObject(string)、InterfaceReworkBuilder.Block(Transform, string, Vector3, Vector3, Material)、Object.DestroyImmediate(Object)、PrefabUtility.SaveAsPrefabAsset(GameObject, string)、Transform.SetParent(Transform, bool)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static GameObject GenerateAttacheCase()
    {
        // 根游戏对象。
var root = new GameObject("皮革公文包");
        Block(root.transform, "箱底", new Vector3(0, 0, 0.35f), new Vector3(10.8f, 6.8f, 0.55f), leather);
        Block(root.transform, "底部内衬", new Vector3(0, 0, 0.06f), new Vector3(10.05f, 6.05f, 0.16f), lining);
        // side（侧边）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
foreach (int side in new[]
        {
            -1,
            1
        }

        )
        {
            Block(root.transform, "侧边金属框", new Vector3(side * 5.25f, 0, -0.08f), new Vector3(0.18f, 6.5f, 0.32f), metal);
            Block(root.transform, "横向金属框", new Vector3(0, side * 3.25f, -0.08f), new Vector3(10.6f, 0.18f, 0.32f), metal);
            Block(root.transform, "锁扣", new Vector3(side * 3.2f, -3.49f, -0.12f), new Vector3(0.7f, 0.44f, 0.24f), metal);
            Block(root.transform, "锁芯", new Vector3(side * 3.2f, -3.51f, -0.26f), new Vector3(0.15f, 0.13f, 0.06f), darkMetal);
            Block(root.transform, "提手支座", new Vector3(side * 0.9f, -3.63f, 0.08f), new Vector3(0.2f, 0.7f, 0.25f), metal);
        }

        Block(root.transform, "皮革提手", new Vector3(0, -3.95f, 0.08f), new Vector3(1.9f, 0.25f, 0.3f), leather);
        // 当前遍历索引。
for (int i = 0; i < 3; i++)
        {
            Block(root.transform, "箱盖铰链金属", new Vector3((i - 1) * 3.9f, 3.3f, -0.15f), new Vector3(0.65f, 0.22f, 0.25f), metal);
        }

        // 铰链。
var hinge = new GameObject("箱盖铰链").transform;
        hinge.SetParent(root.transform, false);
        hinge.localPosition = new Vector3(0, 3.35f, -0.2f);
        Block(hinge, "箱盖皮革", new Vector3(0, -3.35f, -0.35f), new Vector3(10.8f, 6.8f, 0.36f), leather);
        Block(hinge, "箱盖内衬", new Vector3(0, -3.35f, -0.12f), new Vector3(10.3f, 6.3f, 0.1f), lining);
        // side（侧边）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Vector3.Vector3(float, float, float) 的参数。
foreach (int side in new[]
        {
            -1,
            1
        }

        )
        {
            Block(hinge, "箱盖金属边", new Vector3(side * 5.25f, -3.35f, -0.1f), new Vector3(0.13f, 6.6f, 0.2f), metal);
            Block(hinge, "箱盖横边", new Vector3(0, -3.35f + side * 3.25f, -0.1f), new Vector3(10.5f, 0.13f, 0.2f), metal);
            Block(hinge, "内衬压带", new Vector3(side * 2.5f, -3.35f, -0.04f), new Vector3(0.24f, 5.9f, 0.06f), leather);
        }

        // 预制件。
var prefab = PrefabUtility.SaveAsPrefabAsset(root, directory + "/模型/皮革公文包.prefab");
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>AuthoringProp 的业务处理入口；本方法直接依赖 GameObject.GameObject(string)、InterfaceReworkBuilder.Block(Transform, string, Vector3, Vector3, Material)、InterfaceReworkBuilder.SphereValue(Transform, string, Vector3, Vector3, Material)、Mathf.Cos(float)、Mathf.Sin(float)、Quaternion.Euler(float, float, float)、string.Contains(string) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.GenerateAsset()；下游（含内部回调体） GameObject.GameObject(string)、InterfaceReworkBuilder.Block(Transform, string, Vector3, Vector3, Material)、InterfaceReworkBuilder.SphereValue(Transform, string, Vector3, Vector3, Material)、Mathf.Cos(float)、Mathf.Sin(float)、Quaternion.Euler(float, float, float)、string.Contains(string) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="item">当前物品数据配置。</param>
    static GameObject AuthoringProp(ItemData item)
    {
        // 根游戏对象。
var root = new GameObject(item.displayName + "展示模型");
        // 编号。
string id = item.itemId.ToLowerInvariant();
        if (id.Contains("herb") && !id.Contains("mixed"))
        {
            // 叶片。
global::UnityEngine.Material leaf;
            if (id.Contains("red"))
            {
                leaf = redLeaf;
            }
            else
            {
                if (id.Contains("yellow"))
                {
                    leaf = yellowLeaf;
                }
                else
                {
                    leaf = greenLeaf;
                }
            }

            Block(root.transform, "根部包纸", new Vector3(0, -0.55f, 0), new Vector3(0.65f, 0.45f, 0.25f), paper);
            Block(root.transform, "草药茎", new Vector3(0, 0, 0), new Vector3(0.07f, 1.3f, 0.06f), greenLeaf);
            // 当前遍历索引。
for (int i = 0; i < 8; i++)
            {
                // 按原顺序缓存的实参根节点的变换。
global::UnityEngine.Transform argumentValue6 = root.transform;
                // 按原顺序缓存的实参文字“草药叶”。
string argumentValue7 = "草药叶";
                // 分支选出的1。
int selectedValue5;
                if (i % 2 == 0)
                {
                    selectedValue5 = -1;
                }
                else
                {
                    selectedValue5 = 1;
                }

                // 当前游戏对象。
var go = SphereValue(argumentValue6, argumentValue7, new Vector3((selectedValue5) * 0.22f, -0.2f + i * 0.13f, -0.04f), new Vector3(0.48f, 0.19f, 0.07f), leaf);
                // 赋值前保存的目标游戏对象的变换。
global::UnityEngine.Transform assignmentReceiver8 = go.transform;
                // 按原顺序缓存的实参0。
float argumentValue10 = 0;
                // 按原顺序缓存的实参0。
float argumentValue11 = 0;
                // 分支选出的1。
int selectedValue9;
                if (i % 2 == 0)
                {
                    selectedValue9 = -1;
                }
                else
                {
                    selectedValue9 = 1;
                }

                assignmentReceiver8.localRotation = Quaternion.Euler(argumentValue10, argumentValue11, (selectedValue9) * 28f);
            }
        }
        else if (id.Contains("mixed"))
        {
            Block(root.transform, "药粉纸包", Vector3.zero, new Vector3(1.1f, 0.65f, 0.15f), paper);
            // 当前遍历索引。
for (int i = 0; i < 14; i++)
            {
                // 按原顺序缓存的实参根节点的变换。
global::UnityEngine.Transform argumentValue14 = root.transform;
                // 按原顺序缓存的实参文字“药粉”。
string argumentValue15 = "药粉";
                // 按原顺序缓存的实参正弦的返回结果。
global::UnityEngine.Vector3 argumentValue16 = new Vector3(Mathf.Sin(i * 2.4f) * 0.34f, Mathf.Cos(i * 1.7f) * 0.15f, -0.12f);
                // 按原顺序缓存的实参三维向量。
global::UnityEngine.Vector3 argumentValue17 = new Vector3(0.16f, 0.12f, 0.07f);
                // 分支选出的红色叶片。
global::UnityEngine.Material selectedValue13;
                if (i % 3 == 0 && id.Contains("r"))
                {
                    selectedValue13 = redLeaf;
                }
                else
                {
                    if (i % 3 == 0 && id.Contains("y"))
                    {
                        selectedValue13 = yellowLeaf;
                    }
                    else
                    {
                        selectedValue13 = greenLeaf;
                    }
                }

                SphereValue(argumentValue14, argumentValue15, argumentValue16, argumentValue17, selectedValue13);
            }
        }
        else if (id.Contains("powder"))
        {
            Block(root.transform, "火药罐", Vector3.zero, new Vector3(0.7f, 0.95f, 0.4f), darkMetal);
            Block(root.transform, "罐盖", new Vector3(0, 0.5f, 0), new Vector3(0.65f, 0.12f, 0.38f), metal);
            Block(root.transform, "纸质标签", new Vector3(0, 0, -0.215f), new Vector3(0.5f, 0.5f, 0.025f), paper);
            Block(root.transform, "警示标记", new Vector3(0, 0, -0.24f), new Vector3(0.2f, 0.2f, 0.02f), redLeaf).transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
        else
        {
            Block(root.transform, "零件布包", Vector3.zero, new Vector3(1.25f, 0.7f, 0.18f), lining);
            // 当前遍历索引。
for (int i = 0; i < 5; i++)
            {
                // 按原顺序缓存的实参根节点的变换。
global::UnityEngine.Transform argumentValue19 = root.transform;
                // 按原顺序缓存的实参文字“金属零件”。
string argumentValue20 = "金属零件";
                // 按原顺序缓存的实参正弦的返回结果。
global::UnityEngine.Vector3 argumentValue21 = new Vector3((i - 2) * 0.22f, Mathf.Sin(i) * 0.12f, -0.2f);
                // 按原顺序缓存的实参三维向量。
global::UnityEngine.Vector3 argumentValue22 = new Vector3(0.14f, 0.45f, 0.12f);
                // 分支选出的金属。
global::UnityEngine.Material selectedValue18;
                if (i % 2 == 0)
                {
                    selectedValue18 = metal;
                }
                else
                {
                    selectedValue18 = darkMetal;
                }

                // 部件。
var part = Block(argumentValue19, argumentValue20, argumentValue21, argumentValue22, selectedValue18);
                part.transform.localRotation = Quaternion.Euler(0, 0, i * 19f);
            }
        }

        return root;
    }

    /// <summary>CleanupPresentationComponent 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;Component&gt;(bool)、Object.DestroyImmediate(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.GenerateAsset()；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;Component&gt;(bool)、Object.DestroyImmediate(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根游戏对象。</param>
    static void CleanupPresentationComponent(GameObject root)
    {
        // c（当前组件）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数、InterfaceReworkBuilder.CleanupPresentationComponent(GameObject)。
foreach (var c in root.GetComponentsInChildren<Component>(true))
        {
            if (c != null && !(c is Transform) && !(c is Renderer) && !(c is MeshFilter))
            {
                UnityEngine.Object.DestroyImmediate(c);
            }
        }
    }

    /// <summary>BestOrientation 的业务处理入口；本方法直接依赖 Bounds.Encapsulate(Bounds)、GameObject.GetComponentsInChildren&lt;Renderer&gt;(bool)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Quaternion.Euler(float, float, float)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.GenerateAsset()；下游（含内部回调体） Bounds.Encapsulate(Bounds)、GameObject.GetComponentsInChildren&lt;Renderer&gt;(bool)、Mathf.Abs(float)、Mathf.Max(float, float)、Mathf.Min(float, float)、Quaternion.Euler(float, float, float)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根游戏对象。</param>
    /// <param name="w">宽度。</param>
    /// <param name="h">高度。</param>
    static Vector3 BestOrientation(GameObject root, int w, int h)
    {
        // 渲染器集合。
var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return Vector3.zero;
        }

        // 当前包围盒。
Bounds b = renderers[0].bounds;
        // r（当前渲染器）：每轮从渲染器集合取得一个元素，仅代表当前这一项。使用位置：作为 Bounds.Encapsulate(Bounds) 的参数。
foreach (var r in renderers)
        {
            b.Encapsulate(r.bounds);
        }

        // 尺寸。
Vector3 size = b.size;
        // 最佳。
float best = -1f;
        // 结果。
Vector3 result = Vector3.zero;
        // x（横向）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Quaternion.Euler(float, float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
foreach (int x in new[]
        {
            0,
            90,
            180,
            270
        }

        )
        {
            // y（纵向）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Quaternion.Euler(float, float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
foreach (int y in new[]
            {
                0,
                90,
                180,
                270
            }

            )
            {
                // z（深度）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Quaternion.Euler(float, float, float) 的参数、作为 Vector3.Vector3(float, float, float) 的参数。
foreach (int z in new[]
                {
                    0,
                    90,
                    180,
                    270
                }

                )
                {
                    // 当前处理的四元数旋转。
var q = Quaternion.Euler(x, y, z);
                    // a（起点（三维向量））：使用位置：作为 Mathf.Abs(float) 的参数。
var a = q * new Vector3(size.x, 0, 0);
                    // c（候选值（三维向量））：使用位置：作为 Mathf.Abs(float) 的参数。
var c = q * new Vector3(0, size.y, 0);
                    // d（距离（三维向量））：使用位置：作为 Mathf.Abs(float) 的参数。
var d = q * new Vector3(0, 0, size.z);
                    // 绝对值的返回结果。
Vector3 s = new Vector3(Mathf.Abs(a.x) + Mathf.Abs(c.x) + Mathf.Abs(d.x), Mathf.Abs(a.y) + Mathf.Abs(c.y) + Mathf.Abs(d.y), Mathf.Abs(a.z) + Mathf.Abs(c.z) + Mathf.Abs(d.z));
                    // 缩放。
float scale = Mathf.Min(w / Mathf.Max(s.x, 0.001f), h / Mathf.Max(s.y, 0.001f));
                    // 分值。
float score = s.x * s.y * scale * scale / (1f + s.z * scale * 0.5f);
                    if (score > best + 0.001f)
                    {
                        best = score;
                        result = new Vector3(x, y, z);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>ConfigurationTitle 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;Font&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;VillageDaylightConfiguration&gt;(string)、Color.Color(float, float, float)、Component.GetComponent&lt;Canvas&gt;()、Component.GetComponent&lt;CanvasScaler&gt;()、Component.GetComponent&lt;Image&gt;() 等共 60 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;Font&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;VillageDaylightConfiguration&gt;(string)、Color.Color(float, float, float)、Component.GetComponent&lt;Canvas&gt;()、Component.GetComponent&lt;CanvasScaler&gt;()、Component.GetComponent&lt;Image&gt;() 等共 60 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ConfigurationTitle()
    {
        ReadAsset();
        // 标题。
var title = SceneManager.GetSceneByPath("Assets/Scenes/TitleScreen.unity");
        // 游戏。
var game = SceneManager.GetSceneByPath("Assets/GameScene.unity");
        SceneManager.SetActiveScene(title);
        // 原有游戏对象。
var old = title.GetRootGameObjects().FirstOrDefault( /* 中文含义：当前游戏对象。 */g => g.name == "VillageBackground");
        if (old != null)
        {
            old.SetActive(false);
        }

        // 前一次。
var previous = title.GetRootGameObjects().FirstOrDefault( /* 中文含义：当前游戏对象。 */g => g.name == "菜单村庄实景");
        if (previous != null)
        {
            UnityEngine.Object.DestroyImmediate(previous);
        }

        // 来源游戏对象。
var source = game.GetRootGameObjects().First( /* 中文含义：当前游戏对象。 */g => g.name == "VillageDemo");
        // 村庄。
var village = new GameObject("菜单村庄实景");
        SceneManager.MoveGameObjectToScene(village, title);
        // r（当前网格渲染器）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数、作为 Transform.SetPositionAndRotation(Vector3, Quaternion) 的参数、InterfaceReworkBuilder.ConfigurationTitle()。
foreach (var r in source.GetComponentsInChildren<MeshRenderer>())
        {
            if (!r.enabled || r.GetComponentInParent<VillagerController>() != null || r.GetComponentInParent<ItemPickup>() != null)
            {
                continue;
            }

            // 网格过滤器。
var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                continue;
            }

            // 当前游戏对象。
var go = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(village.transform, false);
            go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            go.transform.localScale = r.transform.lossyScale;
            go.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            // 渲染器。
var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = r.sharedMaterials;
            renderer.shadowCastingMode = r.shadowCastingMode;
        }

        // t（当前变换节点）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationTitle()。
foreach (var t in village.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = 0;
        }

        // 氛围。
var atmosphere = village.AddComponent<VillageMenuAtmosphere>();
        // 火堆集合。
var fires = new List<Light>();
        // pos（位置）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationTitle()。
foreach (var pos in new[]
        {
            new Vector3(7f, 1.8f, -9f),
            new Vector3(-8f, 1.5f, -12f)
        }

        )
        {
            // 当前游戏对象。
var go = new GameObject("村庄暖火", typeof(Light));
            SceneManager.MoveGameObjectToScene(go, title);
            go.transform.SetParent(village.transform, true);
            go.transform.position = pos;
            // 当前光源。
var l = go.GetComponent<Light>();
            l.type = LightType.Point;
            l.range = 9f;
            l.color = new Color(1f, 0.47f, 0.17f);
            l.intensity = 2f;
            fires.Add(l);
        }

        atmosphere.firelight = fires.ToArray();
        // 当前标题屏幕管理器。
var manager = title.GetRootGameObjects().SelectMany( /* 中文含义：当前游戏对象。 */g => g.GetComponentsInChildren<TitleScreenManager>(true)).First();
        manager.visualTheme = theme;
        manager.particleCount = 0;
        if (manager.particlesContainer != null)
        {
            manager.particlesContainer.gameObject.SetActive(false);
        }

        manager.particlesContainer = null;
        // canvas（画布）：保存 Component.GetComponent<Canvas>() 的返回结果，供当前步骤使用。
var canvas = manager.GetComponent<Canvas>();
        // 缩放器。
var scaler = manager.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        // 背景。
var bg = manager.transform.Find("BackgroundPanel");
        if (bg != null)
        {
            bg.gameObject.SetActive(false);
        }

        // tr（变换）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationTitle()。
foreach (var tr in manager.GetComponentsInChildren<Transform>(true))
        {
            if (tr.name == "左侧渐暗")
            {
                tr.gameObject.SetActive(false);
            }
        }

        Locate(manager.menuPanel.GetComponent<RectTransform>(), new Vector2(0.07f, 0.2f), new Vector2(0.36f, 0.64f));
        // 当前遍历索引。
for (int i = 0; i < manager.menuItems.Length; i++)
        {
            // 当前文字组件。
var t = manager.menuItems[i];
            // 界面矩形变换。
var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(22, -32 - i * 62);
            rt.sizeDelta = new Vector2(360, 52);
            rt.localScale = Vector3.one;
            t.font = theme.interfaceFont;
            t.fontSize = 25;
            t.alignment = TextAnchor.MiddleLeft;
            t.text = new[]
            {
                "继续游戏",
                "开始游戏",
                "读取存档",
                "设置",
                "退出游戏"
            }[i];
        }

        // 标记。
var marker = manager.selectionIndicator;
        marker.anchorMin = marker.anchorMax = new Vector2(0, 1);
        marker.pivot = new Vector2(0, 0.5f);
        marker.sizeDelta = new Vector2(3, 30);
        marker.anchoredPosition = new Vector2(0, -94);
        marker.GetComponent<Image>().color = theme.menuText;
        // hover（悬停）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationTitle()。
foreach (var hover in manager.GetComponentsInChildren<MenuHoverScale>(true))
        {
            hover.enabled = false;
        }

        manager.menuHoverScale = null;
        manager.titlePanel.SetActive(true);
        manager.menuPanel.SetActive(false);
        if (manager.logoText != null)
        {
            manager.logoText.text = "ResidentEvil4";
            Locate(manager.logoText.rectTransform, new Vector2(.09f, .37f), new Vector2(.91f, .69f));
            // effect（效果）：每轮从取得组件集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数。
foreach (var effect in manager.logoText.GetComponents<Shadow>())
            {
                UnityEngine.Object.DestroyImmediate(effect);
            }

            // 空值判断前缓存的取得组件的返回结果。
global::TitleTypewriterPresentation availableValue23 = manager.GetComponent<TitleTypewriterPresentation>();
            // 介绍。
global::TitleTypewriterPresentation intro;
            if (!global::System.Object.ReferenceEquals(availableValue23, null))
            {
                intro = availableValue23;
            }
            else
            {
                intro = manager.gameObject.AddComponent<TitleTypewriterPresentation>();
            }

            intro.titleContainer = manager.logoText;
            intro.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Oswald-Medium.ttf");
            intro.textFaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(directory + "/标题字面.mat");
            manager.titlePresentation = intro;
        }

        if (manager.pressAnyButtonText != null)
        {
            manager.pressAnyButtonText.text = "开始游戏";
            manager.pressAnyButtonText.font = theme.interfaceFont;
            manager.pressAnyButtonText.fontSize = 27;
            Locate(manager.pressAnyButtonText.rectTransform, new Vector2(.25f, .17f), new Vector2(.75f, .24f));
            manager.pressAnyButtonText.alignment = TextAnchor.MiddleCenter;
        }

        // 标题。
var heading = NewText(manager.menuPanel.transform, "菜单标题", "ResidentEvil4", AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Cinzel-Black.ttf"), 40);
        // 标题横线。
var hr = heading.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0, 1);
        hr.pivot = new Vector2(0, 0);
        hr.anchoredPosition = new Vector2(16, 50);
        hr.sizeDelta = new Vector2(720, 90);
        // 线。
var line = NewImage(manager.menuPanel.transform, "标题细线", theme.menuLine);
        // 左侧分隔线。
var lr = line.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0, 1);
        lr.pivot = Vector2.zero;
        lr.anchoredPosition = new Vector2(20, 25);
        lr.sizeDelta = new Vector2(320, 1);
        if (manager.optionsMenu != null)
        {
            manager.optionsMenu.visualTheme = theme;
            manager.optionsMenu.gameObject.SetActive(false);
        }

        // 摄像机。
var camera = title.GetRootGameObjects().SelectMany( /* 中文含义：当前游戏对象。 */g => g.GetComponentsInChildren<Camera>(true)).First();
        camera.transform.position = new Vector3(3.5f, 2.3f, -3f);
        camera.transform.rotation = Quaternion.Euler(357, 244, 0);
        camera.fieldOfView = 58;
        atmosphere.presentationCamera = camera;
        camera.farClipPlane = 500;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.3f, 0.32f, 0.29f);
        // 光源。
var light = title.GetRootGameObjects().SelectMany( /* 中文含义：当前游戏对象。 */g => g.GetComponentsInChildren<Light>(true)).First( /* 中文含义：当前光源。 */l => l.type == LightType.Directional);
        // 日光。
var daylight = AssetDatabase.LoadAssetAtPath<VillageDaylightConfiguration>(directory + "/村庄日间配置.asset");
        if (daylight != null)
        {
            // 空值判断前缓存的取得组件的返回结果。
global::VillageDaylightApplier availableValue25 = light.GetComponent<VillageDaylightApplier>();
            // 应用。
global::VillageDaylightApplier apply;
            if (!global::System.Object.ReferenceEquals(availableValue25, null))
            {
                apply = availableValue25;
            }
            else
            {
                apply = light.gameObject.AddComponent<VillageDaylightApplier>();
            }

            apply.configuration = daylight;
            apply.sunlight = light;
            daylight.Apply(light);
        }

        EditorSceneManager.MarkSceneDirty(title);
        EditorSceneManager.SaveScene(title);
        return "已保存村庄实景标题";
    }

    /// <summary>ReadAsset 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;UIVisualTheme&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.ConfigurationInventory()、InterfaceReworkBuilder.ConfigurationOtherInterface()、InterfaceReworkBuilder.ConfigurationTitle()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;InventoryPresentationConfiguration&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;UIVisualTheme&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static void ReadAsset()
    {
        theme = AssetDatabase.LoadAssetAtPath<UIVisualTheme>(directory + "/统一视觉主题.asset");
        configuration = AssetDatabase.LoadAssetAtPath<InventoryPresentationConfiguration>(directory + "/背包展示配置.asset");
    }

    /// <summary>ConfigurationInventory 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、Color.Color(float, float, float)、Component.GetComponent&lt;AttacheCasePresentationController&gt;()、Component.GetComponent&lt;InventoryGridView&gt;()、Component.GetComponents&lt;Shadow&gt;()、EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.MarkSceneDirty(Scene) 等共 26 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、Color.Color(float, float, float)、Component.GetComponent&lt;AttacheCasePresentationController&gt;()、Component.GetComponent&lt;InventoryGridView&gt;()、Component.GetComponents&lt;Shadow&gt;()、EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.MarkSceneDirty(Scene) 等共 26 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ConfigurationInventory()
    {
        ReadAsset();
        // 场景。
var scene = EditorSceneManager.OpenScene("Assets/Scenes/InventoryScene.unity", OpenSceneMode.Additive);
        // 界面。
var ui = scene.GetRootGameObjects().SelectMany( /* 中文含义：当前游戏对象。 */g => g.GetComponentsInChildren<InventoryUI>(true)).First();
        // 网格。
global::InventoryGridView grid;
        if (ui.gridView != null)
        {
            grid = ui.gridView;
        }
        else
        {
            grid = ui.GetComponent<InventoryGridView>();
        }

        // 显示器。
var presenter = ui.GetComponent<AttacheCasePresentationController>();
        if (presenter == null)
        {
            presenter = ui.gameObject.AddComponent<AttacheCasePresentationController>();
        }

        presenter.mesh = grid;
        presenter.configuration = configuration;
        ui.threeDimensionalPresentation = presenter;
        ui.visualTheme = theme;
        grid.threeDimensionalPresentation = presenter;
        grid.visualTheme = theme;
        grid.cellBackgroundSprite = null;
        grid.cellFallbackColor = new Color(0.6f, 0.64f, 0.56f, 0.035f);
        grid.selectedColor = new Color(0.72f, 0.74f, 0.64f, 0.12f);
        grid.hoverColor = new Color(0.8f, 0.79f, 0.7f, 0.07f);
        ui.highlightColor = grid.selectedColor;
        ui.actionButtonNormalSprite = null;
        ui.actionButtonHoverSprite = null;
        ui.actionNormalColor = theme.menuSelection;
        ui.actionHighlightedColor = new Color(0.4f, 0.43f, 0.37f, 0.7f);
        // img（界面图像组件）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationInventory()。
foreach (var img in new[]
        {
            ui.skinShellImage,
            ui.foamGridImage,
            ui.metalFrameImage
        }

        )
        {
            if (img != null)
            {
                img.gameObject.SetActive(false);
            }
        }

        // t（当前界面矩形变换）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 InterfaceReworkBuilder.Locate(RectTransform, Vector2, Vector2) 的参数、InterfaceReworkBuilder.ConfigurationInventory()。
foreach (var t in scene.GetRootGameObjects().SelectMany( /* 中文含义：当前游戏对象。 */g => g.GetComponentsInChildren<RectTransform>(true)))
        {
            if (t.name == "GridArea")
            {
                Locate(t, new Vector2(.04f, .10f), new Vector2(.71f, .73f));
            }

            if (t.name == "描述面板" || t.name == "说明面板")
            {
                Locate(t, new Vector2(0.76f, 0.23f), new Vector2(0.96f, 0.71f));
            }
        }

        if (ui.descriptionPanel != null)
        {
            Locate(ui.descriptionPanel, new Vector2(0.76f, 0.23f), new Vector2(0.96f, 0.72f));
        }

        // c（当前摄像机）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationInventory()。
foreach (var c in scene.GetRootGameObjects().SelectMany( /* 中文含义：当前游戏对象。 */g => g.GetComponentsInChildren<Camera>(true)))
        {
            c.cullingMask = 0;
            c.backgroundColor = new Color(0.018f, 0.023f, 0.021f);
            c.clearFlags = CameraClearFlags.SolidColor;
        }

        // img（界面图像组件）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationInventory()。
foreach (var img in scene.GetRootGameObjects().SelectMany( /* 中文含义：当前游戏对象。 */g => g.GetComponentsInChildren<Image>(true)))
        {
            if (img.name == "公文包外框" || img.name == "公文包内衬" || img.name == "GridArea")
            {
                img.enabled = false;
                // effect（效果）：每轮从取得组件集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationInventory()。
foreach (var effect in img.GetComponents<Shadow>())
                {
                    effect.enabled = false;
                }
            }
            else if (img.name.Contains("背景"))
            {
                img.sprite = null;
                img.color = new Color(0.018f, 0.023f, 0.021f, 0.94f);
            }
        }

        // root（根游戏对象）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 MenuThemeApplier.Apply(Transform, UIVisualTheme) 的参数。
foreach (var root in scene.GetRootGameObjects())
        {
            MenuThemeApplier.Apply(root.transform, theme);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.CloseScene(scene, true);
        return "已接入三维背包";
    }

    /// <summary>ConfigurationOtherInterface 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.OpenScene(string, OpenSceneMode)、EditorSceneManager.SaveScene(Scene)、GameObject.GetComponentsInChildren&lt;InventoryGridView&gt;(bool)、GameObject.GetComponentsInChildren&lt;OptionsMenu&gt;(bool) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Color.Color(float, float, float, float)、EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.OpenScene(string, OpenSceneMode)、EditorSceneManager.SaveScene(Scene)、GameObject.GetComponentsInChildren&lt;InventoryGridView&gt;(bool)、GameObject.GetComponentsInChildren&lt;OptionsMenu&gt;(bool) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ConfigurationOtherInterface()
    {
        ReadAsset();
        // 游戏。
var game = SceneManager.GetSceneByPath("Assets/GameScene.unity");
        // root（根游戏对象）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationOtherInterface()。
foreach (var root in game.GetRootGameObjects())
        {
            // p（当前暂停菜单）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationOtherInterface()。
foreach (var p in root.GetComponentsInChildren<PauseMenu>(true))
            {
                p.visualTheme = theme;
            }

            // p（当前选项集合分页管理器）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationOtherInterface()。
foreach (var p in root.GetComponentsInChildren<OptionsTabManager>(true))
            {
                p.visualTheme = theme;
            }

            // p（当前选项集合菜单）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationOtherInterface()。
foreach (var p in root.GetComponentsInChildren<OptionsMenu>(true))
            {
                p.visualTheme = theme;
            }
        }

        EditorSceneManager.MarkSceneDirty(game);
        EditorSceneManager.SaveScene(game);
        // 仓库。
var storage = EditorSceneManager.OpenScene("Assets/Scenes/StorageBoxScene.unity", OpenSceneMode.Additive);
        // root（根游戏对象）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 MenuThemeApplier.Apply(Transform, UIVisualTheme) 的参数、InterfaceReworkBuilder.ConfigurationOtherInterface()。
foreach (var root in storage.GetRootGameObjects())
        {
            // p（当前仓库箱体界面）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationOtherInterface()。
foreach (var p in root.GetComponentsInChildren<StorageBoxUI>(true))
            {
                p.visualTheme = theme;
                p.dropZoneColor = new Color(0.64f, 0.7f, 0.57f, 0.2f);
                p.inventoryDropZoneColor = p.dropZoneColor;
            }

            // p（当前背包网格视图）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：InterfaceReworkBuilder.ConfigurationOtherInterface()。
foreach (var p in root.GetComponentsInChildren<InventoryGridView>(true))
            {
                p.visualTheme = theme;
                p.cellFallbackColor = new Color(0.12f, 0.14f, 0.12f, 0.6f);
                p.hoverColor = new Color(0.7f, 0.72f, 0.64f, 0.13f);
                p.selectedColor = p.hoverColor;
            }

            MenuThemeApplier.Apply(root.transform, theme);
        }

        EditorSceneManager.MarkSceneDirty(storage);
        EditorSceneManager.SaveScene(storage);
        EditorSceneManager.CloseScene(storage, true);
        return "暂停、设置和寄存箱主题已配置";
    }

    /// <summary>NewImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.ConfigurationTitle()；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="name">名称。</param>
    /// <param name="color">颜色。</param>
    static Image NewImage(Transform parent, string name, Color color)
    {
        // 当前游戏对象。
var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        // 界面图像组件。
var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>NewText 的业务处理入口；本方法直接依赖 Color.Color(float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.ConfigurationTitle()；下游（含内部回调体） Color.Color(float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">父级。</param>
    /// <param name="name">名称。</param>
    /// <param name="text">文字。</param>
    /// <param name="font">字体。</param>
    /// <param name="size">尺寸。</param>
    static Text NewText(Transform parent, string name, string text, Font font, int size)
    {
        // 当前游戏对象。
var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        // 当前文字组件。
var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = new Color(0.88f, 0.86f, 0.8f);
        t.alignment = TextAnchor.MiddleLeft;
        return t;
    }

    /// <summary>Locate 的业务处理入口；本方法直接依赖 Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InterfaceReworkBuilder.ConfigurationInventory()、InterfaceReworkBuilder.ConfigurationTitle()；下游（含内部回调体） Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="r">当前界面矩形变换。</param>
    /// <param name="min">最小。</param>
    /// <param name="max">最大。</param>
    static void Locate(RectTransform r, Vector2 min, Vector2 max)
    {
        r.anchorMin = min;
        r.anchorMax = max;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.localScale = Vector3.one;
    }
}

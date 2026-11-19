using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>首次获得物品检视界面的资源安装与配置工具。</summary>
public static class ItemInspectionSetupUtility
{
    /// <summary>中文含义：游戏场景路径。编译期常量，保持既有值与消费者映射。使用位置：作为 EditorSceneManager.OpenScene(string, OpenSceneMode) 的参数、作为 SceneManager.GetSceneByPath(string) 的参数。</summary>
    const string GameScenePath = SceneManagerEx.MainScenePath;
    /// <summary>中文含义：预制件文件夹。编译期常量，保持既有值与消费者映射。使用位置：ItemInspectionSetupUtility.PrefabPath。</summary>
    const string PrefabFolder = "Assets/Prefab/UI";
    /// <summary>中文含义：预制件路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数。</summary>
    const string PrefabPath = PrefabFolder + "/首次获得物品检视界面.prefab";
    /// <summary>中文含义：已知模型路径集合。只读字段，初始化后不由普通写入口替换。使用位置：ItemInspectionSetupUtility.ConfigureKnownModels()。</summary>
    static readonly Dictionary<string, string> KnownModelPaths = new Dictionary<string, string>
    {
        {
            "first_aid_spray",
            "Assets/Models/道具/急救喷雾/AidObject_Configured.prefab"
        },
        {
            "knife",
            "Assets/Models/武器/小刀/combat_knife/scene.prefab"
        },
        {
            "broken_knife",
            "Assets/Models/武器/小刀/combat_knife/scene.prefab"
        },
        {
            "pistol",
            "Assets/素材库/武器/Nokobot/Modern Guns v4/_Prefabs/M1911 Handgun/Handgun Black/M1911 Handgun_Black.prefab"
        },
        {
            "pistol_ammo",
            "Assets/Models/道具/弹药/handgun_ammo_box_remake/scene.prefab"
        },
        {
            "rifle",
            "Assets/素材库/武器/Nokobot/Modern Guns v4/_Prefabs/Assault Rifles/M16 Assault Rifle/M16 Assault Rifle.prefab"
        },
        {
            "rifle_ammo",
            "Assets/Models/道具/弹药/ammo/Assault Rifle Ammo Box.fbx"
        },
        {
            "shotgun_ammo",
            "Assets/素材库/武器/Nokobot/Modern Guns v4/_Prefabs/Mossberg 590 Shotgun/12 Gauge Shell.prefab"
        }
    };
    /// <summary>编辑器菜单调用入口 InstallAndConfigure；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.Refresh()、AssetDatabase.SaveAssets()、Debug.Log(object)、ItemInspectionSetupUtility.ConfigureKnownModels()、ItemInspectionSetupUtility.EnsurePrefab()、ItemInspectionSetupUtility.EnsureSceneInstance(GameObject)、ItemInspectionSetupUtility.ValidateAllModels(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/物品检视/安装界面并配置现有模型")]
    public static void InstallAndConfigure()
    {
        ConfigureKnownModels();
        // prefab（预制件）：保存 ItemInspectionSetupUtility.EnsurePrefab() 的返回结果，供当前步骤使用。使用位置：作为 ItemInspectionSetupUtility.EnsureSceneInstance(GameObject) 的参数。
GameObject prefab = EnsurePrefab();
        EnsureSceneInstance(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        // missingCount（缺失的数量）：保存 ItemInspectionSetupUtility.ValidateAllModels(bool) 的返回结果，供当前步骤使用。使用位置：作为 Debug.Log(object) 的参数。
int missingCount = ValidateAllModels(true);
        Debug.Log("[物品检视] 安装完成；仍缺少成品三维模型的物品数量：" + missingCount);
    }

    /// <summary>编辑器菜单调用入口 ValidateFromMenu；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、ItemInspectionSetupUtility.ValidateAllModels(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/物品检视/校验全部物品模型")]
    public static void ValidateFromMenu()
    {
        // missingCount（缺失的数量）：保存 ItemInspectionSetupUtility.ValidateAllModels(bool) 的返回结果，供当前步骤使用。使用位置：ItemInspectionSetupUtility.ValidateFromMenu()。
int missingCount = ValidateAllModels(true);
        if (missingCount == 0)
        {
            Debug.Log("[物品检视] 全部物品均已配置三维检视模型。");
        }
    }

    /// <summary>ValidateAllModels 的业务处理入口；本方法直接依赖 Debug.LogError(object, Object)、ItemData.GetInspectionModelPrefab()、ItemInspectionSetupUtility.LoadAllItems()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionSetupUtility.InstallAndConfigure()、ItemInspectionSetupUtility.ValidateFromMenu()；下游（含内部回调体） Debug.LogError(object, Object)、ItemData.GetInspectionModelPrefab()、ItemInspectionSetupUtility.LoadAllItems()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="logErrors">中文含义：是否日志错误集合。使用位置：ItemInspectionSetupUtility.ValidateAllModels(bool)。</param>
    public static int ValidateAllModels(bool logErrors)
    {
        // items（物品列表）：保存 ItemInspectionSetupUtility.LoadAllItems() 的返回结果，供当前步骤使用。使用位置：ItemInspectionSetupUtility.ValidateAllModels(bool)。
List<ItemData> items = LoadAllItems();
        // missingCount（缺失的数量）：使用位置：ItemInspectionSetupUtility.ValidateAllModels(bool)。
int missingCount = 0;
        // i（当前遍历索引）：使用位置：作为 List<ItemData>.this[int] 的参数、ItemInspectionSetupUtility.ValidateAllModels(bool)。
for (int i = 0; i < items.Count; i++)
        {
            // item（当前物品数据配置）：使用位置：作为 Debug.LogError(object, Object) 的参数、ItemInspectionSetupUtility.ValidateAllModels(bool)。
ItemData item = items[i];
            if (item == null || item.GetInspectionModelPrefab() != null)
            {
                continue;
            }

            missingCount++;
            if (logErrors)
            {
                Debug.LogError("[物品检视校验] 缺少成品三维模型：" + item.displayName + "（" + item.itemId + "）", item);
            }
        }

        return missingCount;
    }

    /// <summary>ConfigureKnownModels 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.LogWarning(object, Object)、Dictionary&lt;string, string&gt;.TryGetValue(string, out string)、EditorUtility.SetDirty(Object)、ItemInspectionSetupUtility.LoadAllItems()、Undo.RecordObject(Object, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionSetupUtility.InstallAndConfigure()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.LogWarning(object, Object)、Dictionary&lt;string, string&gt;.TryGetValue(string, out string)、EditorUtility.SetDirty(Object)、ItemInspectionSetupUtility.LoadAllItems()、Undo.RecordObject(Object, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static void ConfigureKnownModels()
    {
        // items（物品列表）：保存 ItemInspectionSetupUtility.LoadAllItems() 的返回结果，供当前步骤使用。使用位置：ItemInspectionSetupUtility.ConfigureKnownModels()。
List<ItemData> items = LoadAllItems();
        // i（当前遍历索引）：使用位置：作为 List<ItemData>.this[int] 的参数、ItemInspectionSetupUtility.ConfigureKnownModels()。
for (int i = 0; i < items.Count; i++)
        {
            // item（当前物品数据配置）：使用位置：作为 Debug.LogWarning(object, Object) 的参数、作为 Dictionary<string, string>.TryGetValue(string, out string) 的参数、作为 EditorUtility.SetDirty(Object) 的参数 等共 5 处。
ItemData item = items[i];
            if (item == null || item.inspectionModelPrefab != null)
            {
                continue;
            }

            if (!KnownModelPaths.TryGetValue(item.itemId, out string /* 中文含义：模型路径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ modelPath))
            {
                continue;
            }

            // model（模型）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：ItemInspectionSetupUtility.ConfigureKnownModels()。
GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning("[物品检视] 找不到候选模型资源：" + modelPath, item);
                continue;
            }

            Undo.RecordObject(item, "配置物品检视模型");
            item.inspectionModelPrefab = model;
            EditorUtility.SetDirty(item);
        }
    }

    /// <summary>EnsurePrefab 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、GameObject.AddComponent&lt;ItemInspectionUI&gt;()、GameObject.GameObject(string)、ItemInspectionSetupUtility.EnsureFolder(string, string)、Object.DestroyImmediate(Object)、PrefabUtility.SaveAsPrefabAsset(GameObject, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionSetupUtility.InstallAndConfigure()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、GameObject.AddComponent&lt;ItemInspectionUI&gt;()、GameObject.GameObject(string)、ItemInspectionSetupUtility.EnsureFolder(string, string)、Object.DestroyImmediate(Object)、PrefabUtility.SaveAsPrefabAsset(GameObject, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static GameObject EnsurePrefab()
    {
        EnsureFolder("Assets/Prefab", "UI");
        // prefab（预制件）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：ItemInspectionSetupUtility.EnsurePrefab()。
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null)
        {
            return prefab;
        }

        // root（根游戏对象）：使用位置：作为 Object.DestroyImmediate(Object) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、ItemInspectionSetupUtility.EnsurePrefab()。
GameObject root = new GameObject("首次获得物品检视界面");
        root.AddComponent<ItemInspectionUI>();
        prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>EnsureSceneInstance 的业务处理入口；本方法直接依赖 EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.OpenScene(string, OpenSceneMode)、EditorSceneManager.SaveScene(Scene)、PrefabUtility.InstantiatePrefab(Object, Scene)、Resources.FindObjectsOfTypeAll&lt;ItemInspectionUI&gt;()、Scene.IsValid() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionSetupUtility.InstallAndConfigure()；下游（含内部回调体） EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.OpenScene(string, OpenSceneMode)、EditorSceneManager.SaveScene(Scene)、PrefabUtility.InstantiatePrefab(Object, Scene)、Resources.FindObjectsOfTypeAll&lt;ItemInspectionUI&gt;()、Scene.IsValid() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="prefab">中文含义：预制件。使用位置：作为 PrefabUtility.InstantiatePrefab(Object, Scene) 的参数、ItemInspectionSetupUtility.EnsureSceneInstance(GameObject)。</param>
    static void EnsureSceneInstance(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        // scene（场景）：保存 SceneManager.GetSceneByPath(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.CloseScene(Scene, bool) 的参数、作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 EditorSceneManager.SaveScene(Scene) 的参数 等共 5 处。
Scene scene = SceneManager.GetSceneByPath(GameScenePath);
        // openedByTool（是否已打开依据工具）：使用位置：ItemInspectionSetupUtility.EnsureSceneInstance(GameObject)。
bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
        {
            scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        }

        // existing（已有）：保存 Resources.FindObjectsOfTypeAll<ItemInspectionUI>() 的返回结果，供当前步骤使用。使用位置：ItemInspectionSetupUtility.EnsureSceneInstance(GameObject)。
ItemInspectionUI[] existing = Resources.FindObjectsOfTypeAll<ItemInspectionUI>();
        // i（当前遍历索引）：使用位置：ItemInspectionSetupUtility.EnsureSceneInstance(GameObject)。
for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null && existing[i].gameObject.scene == scene)
            {
                if (openedByTool)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                return;
            }
        }

        // instance（实例）：使用位置：ItemInspectionSetupUtility.EnsureSceneInstance(GameObject)。
GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance != null)
        {
            instance.name = "首次获得物品检视界面";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedByTool)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    /// <summary>LoadAllItems 的业务处理入口；本方法直接依赖 AssetDatabase.FindAssets(string, string[])、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;ItemData&gt;(string)、List&lt;ItemData&gt;.Add(ItemData)、List&lt;ItemData&gt;.List(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionSetupUtility.ConfigureKnownModels()、ItemInspectionSetupUtility.ValidateAllModels(bool)；下游（含内部回调体） AssetDatabase.FindAssets(string, string[])、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;ItemData&gt;(string)、List&lt;ItemData&gt;.Add(ItemData)、List&lt;ItemData&gt;.List(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    static List<ItemData> LoadAllItems()
    {
        // guids（资源标识集合）：保存 AssetDatabase.FindAssets(string, string[]) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.GUIDToAssetPath(string) 的参数、作为 List<ItemData>.List(int) 的参数、ItemInspectionSetupUtility.LoadAllItems()。
string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Resources/Items", "Assets/Scripts/Inventory/Resources/Items" });
        // items（物品列表）：使用位置：ItemInspectionSetupUtility.LoadAllItems()。
var items = new List<ItemData>(guids.Length);
        // i（当前遍历索引）：使用位置：ItemInspectionSetupUtility.LoadAllItems()。
for (int i = 0; i < guids.Length; i++)
        {
            // path（路径）：保存 AssetDatabase.GUIDToAssetPath(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.LoadAssetAtPath<ItemData>(string) 的参数。
string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            // item（当前物品数据配置）：保存 AssetDatabase.LoadAssetAtPath<ItemData>(string) 的返回结果，供当前步骤使用。使用位置：作为 List<ItemData>.Add(ItemData) 的参数、ItemInspectionSetupUtility.LoadAllItems()。
ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>EnsureFolder 的业务处理入口；本方法直接依赖 AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ItemInspectionSetupUtility.EnsurePrefab()；下游（含内部回调体） AssetDatabase.CreateFolder(string, string)、AssetDatabase.IsValidFolder(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">中文含义：父级。使用位置：作为 AssetDatabase.CreateFolder(string, string) 的参数、ItemInspectionSetupUtility.EnsureFolder(string, string)。</param>
    /// <param name="folderName">中文含义：文件夹名称。使用位置：作为 AssetDatabase.CreateFolder(string, string) 的参数、ItemInspectionSetupUtility.EnsureFolder(string, string)。</param>
    static void EnsureFolder(string parent, string folderName)
    {
        // path（路径）：使用位置：作为 AssetDatabase.IsValidFolder(string) 的参数。
string path = parent + "/" + folderName;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

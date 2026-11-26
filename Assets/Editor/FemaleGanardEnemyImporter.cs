using System;
using System.Collections.Generic;
using System.IO;
using BehaviorDesigner.Runtime;
using Pathfinding;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using FilePath = System.IO.Path;

/// <summary>
/// 导入两套女性村民模型，并按照现有村民敌人预制件配置材质、动画与运行组件。
/// </summary>
public static class FemaleGanardEnemyImporter
{
    /// <summary>序数第一集合模型路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string ordinalOneSetModelPath = "Assets/Models/角色/敌人/FemaleGanard.fbx";
    /// <summary>序数第二集合模型路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string ordinalTwoSetModelPath = "Assets/Models/角色/敌人/FemaleGanard2.fbx";
    /// <summary>序数第一集合脚掌贴地图像目录。编译期常量，保持既有值与消费者映射。</summary>
    private const string ordinalOneSetPlantImageDirectory = "Assets/Textures/角色/Villager/FemaleGanard";
    /// <summary>序数第二集合脚掌贴地图像目录。编译期常量，保持既有值与消费者映射。</summary>
    private const string ordinalTwoSetPlantImageDirectory = "Assets/Textures/角色/Villager/FemaleGanard2";
    /// <summary>敌人动画控制器路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string enemyAnimationControllerPath = "Assets/Animations/Enemy/Enemy.controller";
    /// <summary>行为树路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string behaviorTreePath = "Assets/AI/敌人主行为树.asset";
    /// <summary>模板预制件路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string templatePrefabPath = "Assets/Prefab/Villager.prefab";
    /// <summary>预制件目录。编译期常量，保持既有值与消费者映射。</summary>
    private const string prefabDirectory = "Assets/Prefab/Enemies";
    /// <summary>编辑器菜单调用入口 Import；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.Refresh()、AssetDatabase.SaveAssets()、Debug.Log(object, Object)、FemaleGanardEnemyImporter.EnsureFolder(string)、FemaleGanardEnemyImporter.ImportOne(string, string, string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/导入女性村民敌人")]
    public static void Import()
    {
        EnsureFolder(prefabDirectory);
        ImportOne(ordinalOneSetModelPath, ordinalOneSetPlantImageDirectory, "FemaleGanardEnemy", "FemaleGanardEnemy.prefab");
        ImportOne(ordinalTwoSetModelPath, ordinalTwoSetPlantImageDirectory, "FemaleGanard2Enemy", "FemaleGanard2Enemy.prefab");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("女性村民敌人导入完成。", null);
    }

    /// <summary>ImportOne 的业务处理入口；本方法直接依赖 AssetDatabase.ImportAsset(string, ImportAssetOptions)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.Refresh(ImportAssetOptions)、AssetDatabase.SaveAssets()、Debug.Log(object, Object)、FemaleGanardEnemyImporter.ConfigureEnemyComponents(GameObject, GameObject)、FemaleGanardEnemyImporter.ConfigureMaterials(string) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.Import()；下游（含内部回调体） AssetDatabase.ImportAsset(string, ImportAssetOptions)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.Refresh(ImportAssetOptions)、AssetDatabase.SaveAssets()、Debug.Log(object, Object)、FemaleGanardEnemyImporter.ConfigureEnemyComponents(GameObject, GameObject)、FemaleGanardEnemyImporter.ConfigureMaterials(string) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="modelPath">模型路径。</param>
    /// <param name="textureDirectory">纹理目录。</param>
    /// <param name="rootName">根节点名称。</param>
    /// <param name="prefabFileName">预制件文件名称。</param>
    private static void ImportOne(string modelPath, string textureDirectory, string rootName, string prefabFileName)
    {
        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        ConfigureModel(modelPath);
        AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
        GeneratePackedTextures(textureDirectory);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporters(textureDirectory);
        ConfigureMaterials(textureDirectory);
        AssetDatabase.SaveAssets();
        // 模型资源。
GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset == null)
        {
            throw new InvalidOperationException("无法加载模型资源：" + modelPath);
        }

        // 模板根节点。
GameObject templateRoot = PrefabUtility.LoadPrefabContents(templatePrefabPath);
        // 实例。
GameObject instance = null;
        try
        {
            instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("无法实例化模型资源：" + modelPath);
            }

            instance.name = rootName;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            SetLayers(instance, LayerMask.NameToLayer("Enemy"), LayerMask.NameToLayer("EnemyHitbox"));
            ConfigureMaterialsOnRenderers(instance, textureDirectory);
            ConfigureEnemyComponents(instance, templateRoot);
            // 预制件路径。
string prefabPath = prefabDirectory + "/" + prefabFileName;
            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Debug.Log("已生成敌人预制件：" + prefabPath, null);
        }
        finally
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            if (templateRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(templateRoot);
            }
        }
    }

    /// <summary>ConfigureModel 的业务处理入口；本方法直接依赖 AssetImporter.GetAtPath(string)、AssetImporter.SaveAndReimport()、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ImportOne(string, string, string, string)；下游（含内部回调体） AssetImporter.GetAtPath(string)、AssetImporter.SaveAndReimport()、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="modelPath">模型路径。</param>
    private static void ConfigureModel(string modelPath)
    {
        // 导入器。
ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null)
        {
            throw new InvalidOperationException("模型导入器不存在：" + modelPath);
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.optimizeGameObjects = false;
        importer.importBlendShapes = true;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.SaveAndReimport();
    }

    /// <summary>GeneratePackedTextures 的业务处理入口；本方法直接依赖 Color32.Color32(byte, byte, byte, byte)、Directory.Exists(string)、Directory.GetFiles(string, string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)、File.Exists(string)、File.ReadAllBytes(string)、File.WriteAllBytes(string, byte[]) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ImportOne(string, string, string, string)；下游（含内部回调体） Color32.Color32(byte, byte, byte, byte)、Directory.Exists(string)、Directory.GetFiles(string, string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)、File.Exists(string)、File.ReadAllBytes(string)、File.WriteAllBytes(string, byte[]) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="textureDirectory">纹理目录。</param>
    private static void GeneratePackedTextures(string textureDirectory)
    {
        // 物理目录。
string physicalDirectory = ToPhysicalPath(textureDirectory);
        if (!Directory.Exists(physicalDirectory))
        {
            return;
        }

        // sourcePath（来源路径）：每轮从取得文件集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 File.ReadAllBytes(string) 的参数、作为 Path.GetFileNameWithoutExtension(string) 的参数。
foreach (string sourcePath in Directory.GetFiles(physicalDirectory, "*_NRMR.jpg"))
        {
            // 文件名称。
string fileName = FilePath.GetFileNameWithoutExtension(sourcePath);
            // 常规路径。
string normalPath = FilePath.Combine(physicalDirectory, fileName + "_Normal.png");
            // 遮罩路径。
string maskPath = FilePath.Combine(physicalDirectory, fileName + "_MetallicSmoothness.png");
            if (File.Exists(normalPath) && File.Exists(maskPath))
            {
                continue;
            }

            // 来源二维纹理。
Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            // 普通状态值。
Texture2D normal = null;
            // 遮罩。
Texture2D mask = null;
            try
            {
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath), false))
                {
                    continue;
                }

                // 来源像素集合。
Color32[] sourcePixels = source.GetPixels32();
                // 常规像素集合。
Color32[] normalPixels = new Color32[sourcePixels.Length];
                // 遮罩像素集合。
Color32[] maskPixels = new Color32[sourcePixels.Length];
                // 当前遍历索引。
for (int i = 0; i < sourcePixels.Length; i++)
                {
                    // 横向。
float x = sourcePixels[i].r / 255f * 2f - 1f;
                    // 纵向。
float y = -(sourcePixels[i].g / 255f * 2f - 1f);
                    // 深度。
float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
                    normalPixels[i] = new Color32((byte)Mathf.Clamp(Mathf.Round((x * 0.5f + 0.5f) * 255f), 0f, 255f), (byte)Mathf.Clamp(Mathf.Round((y * 0.5f + 0.5f) * 255f), 0f, 255f), (byte)Mathf.Clamp(Mathf.Round((z * 0.5f + 0.5f) * 255f), 0f, 255f), 255);
                    // 光滑度。
byte smoothness = (byte)(255 - sourcePixels[i].b);
                    maskPixels[i] = new Color32(0, 0, 0, smoothness);
                }

                normal = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, true);
                normal.SetPixels32(normalPixels);
                normal.Apply(true, false);
                mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, true);
                mask.SetPixels32(maskPixels);
                mask.Apply(true, false);
                if (!File.Exists(normalPath))
                {
                    File.WriteAllBytes(normalPath, ImageConversion.EncodeToPNG(normal));
                }

                if (!File.Exists(maskPath))
                {
                    File.WriteAllBytes(maskPath, ImageConversion.EncodeToPNG(mask));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (normal != null)
                {
                    UnityEngine.Object.DestroyImmediate(normal);
                }

                if (mask != null)
                {
                    UnityEngine.Object.DestroyImmediate(mask);
                }
            }
        }
    }

    /// <summary>ConfigureTextureImporters 的业务处理入口；本方法直接依赖 AssetImporter.GetAtPath(string)、AssetImporter.SaveAndReimport()、Directory.Exists(string)、Directory.GetFiles(string)、FemaleGanardEnemyImporter.ToAssetPath(string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)、Path.GetExtension(string) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ImportOne(string, string, string, string)；下游（含内部回调体） AssetImporter.GetAtPath(string)、AssetImporter.SaveAndReimport()、Directory.Exists(string)、Directory.GetFiles(string)、FemaleGanardEnemyImporter.ToAssetPath(string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)、Path.GetExtension(string) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="textureDirectory">纹理目录。</param>
    private static void ConfigureTextureImporters(string textureDirectory)
    {
        // 物理目录。
string physicalDirectory = ToPhysicalPath(textureDirectory);
        if (!Directory.Exists(physicalDirectory))
        {
            return;
        }

        // physicalPath（物理路径）：每轮从取得文件集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 FemaleGanardEnemyImporter.ToAssetPath(string) 的参数、作为 Path.GetExtension(string) 的参数、作为 Path.GetFileNameWithoutExtension(string) 的参数。
foreach (string physicalPath in Directory.GetFiles(physicalDirectory))
        {
            // 扩展名。
string extension = FilePath.GetExtension(physicalPath).ToLowerInvariant();
            if (extension != ".jpg" && extension != ".tga" && extension != ".png")
            {
                continue;
            }

            // 资源路径。
string assetPath = ToAssetPath(physicalPath);
            // 导入器。
TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            // 是否打包后的常规。
bool isPackedNormal = FilePath.GetFileNameWithoutExtension(physicalPath).EndsWith("_Normal", StringComparison.OrdinalIgnoreCase);
            // 是否打包后的遮罩。
bool isPackedMask = FilePath.GetFileNameWithoutExtension(physicalPath).EndsWith("_MetallicSmoothness", StringComparison.OrdinalIgnoreCase);
            // 是否打包后的来源。
bool isPackedSource = FilePath.GetFileNameWithoutExtension(physicalPath).EndsWith("_NRMR", StringComparison.OrdinalIgnoreCase);
            if (FilePath.GetFileNameWithoutExtension(physicalPath).Equals("hair_ALBD", StringComparison.OrdinalIgnoreCase))
            {
                importer.filterMode = FilterMode.Trilinear;
            }

            importer.sRGBTexture = !isPackedMask && !isPackedNormal && !isPackedSource;
            if (isPackedNormal)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.convertToNormalmap = false;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
            }

            importer.SaveAndReimport();
        }
    }

    /// <summary>ConfigureMaterials 的业务处理入口；本方法直接依赖 AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、Directory.Exists(string)、EditorUtility.SetDirty(Object)、FemaleGanardEnemyImporter.ApplyMaterialTextures(Material, string, string)、FemaleGanardEnemyImporter.GetMaterialNamesForSource(string, string)、FemaleGanardEnemyImporter.ToPhysicalPath(string) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ImportOne(string, string, string, string)；下游（含内部回调体） AssetDatabase.CreateAsset(Object, string)、AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、Directory.Exists(string)、EditorUtility.SetDirty(Object)、FemaleGanardEnemyImporter.ApplyMaterialTextures(Material, string, string)、FemaleGanardEnemyImporter.GetMaterialNamesForSource(string, string)、FemaleGanardEnemyImporter.ToPhysicalPath(string) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="textureDirectory">纹理目录。</param>
    private static void ConfigureMaterials(string textureDirectory)
    {
        // 物理目录。
string physicalDirectory = ToPhysicalPath(textureDirectory);
        if (!Directory.Exists(physicalDirectory))
        {
            return;
        }

        // 标准着色器。
Shader standardShader = Shader.Find("Standard");
        if (standardShader == null)
        {
            throw new InvalidOperationException("找不到内置标准材质着色器。");
        }

        // 来源名称列表。
string[] sourceNames =
        {
            "Apron",
            "Body",
            "EyeL",
            "EyeR",
            "Face",
            "hair",
            "Jacket",
            "Shirts",
            "Skirt",
            "Mouth",
            "Props"
        };
        // sourceName（来源名称）：每轮从来源名称列表取得一个元素，仅代表当前这一项。使用位置：作为 FemaleGanardEnemyImporter.ApplyMaterialTextures(Material, string, string) 的参数、作为 FemaleGanardEnemyImporter.GetMaterialNamesForSource(string, string) 的参数。
foreach (string sourceName in sourceNames)
        {
            // 材质名称列表。
string[] materialNames = GetMaterialNamesForSource(sourceName, textureDirectory);
            // materialName（材质名称）：每轮从材质名称列表取得一个元素，仅代表当前这一项。使用位置：FemaleGanardEnemyImporter.ConfigureMaterials(string)。
foreach (string materialName in materialNames)
            {
                // 材质路径。
string materialPath = textureDirectory + "/" + materialName + ".mat";
                // 材质。
Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(standardShader)
                    {
                        name = materialName
                    };
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                ApplyMaterialTextures(material, textureDirectory, sourceName);
                EditorUtility.SetDirty(material);
            }
        }
    }

    /// <summary>GetMaterialNamesForSource 的业务处理入口；本方法直接依赖 Directory.GetFiles(string, string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)、List&lt;string&gt;.Add(string)、List&lt;string&gt;.List()、List&lt;string&gt;.ToArray()、Path.GetFileNameWithoutExtension(string)、string.Contains(string) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureMaterials(string)、FemaleGanardEnemyImporter.FindMaterialPath(string, string, string)；下游（含内部回调体） Directory.GetFiles(string, string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)、List&lt;string&gt;.Add(string)、List&lt;string&gt;.List()、List&lt;string&gt;.ToArray()、Path.GetFileNameWithoutExtension(string)、string.Contains(string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="sourceName">来源名称。</param>
    /// <param name="textureDirectory">纹理目录。</param>
    private static string[] GetMaterialNamesForSource(string sourceName, string textureDirectory)
    {
        // 名称列表。
List<string> names = new List<string>();
        // 物理目录。
string physicalDirectory = ToPhysicalPath(textureDirectory);
        // materialPath（材质路径）：每轮从取得文件集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Path.GetFileNameWithoutExtension(string) 的参数。
foreach (string materialPath in Directory.GetFiles(physicalDirectory, "*.mat"))
        {
            // 名称。
string name = FilePath.GetFileNameWithoutExtension(materialPath);
            // 下部。
string lower = name.ToLowerInvariant();
            if ((sourceName == "Shirts" && lower.Contains("shirt")) || (sourceName == "Mouth" && (lower.Contains("mouth") || lower.Contains("teeth"))) || (sourceName == "Props" && (lower.Contains("prop") || lower.Contains("shoe"))) || lower.Contains(sourceName.ToLowerInvariant()))
            {
                names.Add(name);
            }
        }

        if (names.Count == 0)
        {
            // 后备。
string fallback = sourceName + "_Mat";
            if (sourceName == "hair")
            {
                fallback = "hair_MAT";
            }

            names.Add(fallback);
        }

        return names.ToArray();
    }

    /// <summary>ApplyMaterialTextures 的业务处理入口；本方法直接依赖 FemaleGanardEnemyImporter.FindTexture(string, string)、Material.DisableKeyword(string)、Material.EnableKeyword(string)、Material.SetFloat(string, float)、Material.SetInt(string, int)、Material.SetOverrideTag(string, string)、Material.SetTexture(string, Texture)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureMaterials(string)；下游（含内部回调体） FemaleGanardEnemyImporter.FindTexture(string, string)、Material.DisableKeyword(string)、Material.EnableKeyword(string)、Material.SetFloat(string, float)、Material.SetInt(string, int)、Material.SetOverrideTag(string, string)、Material.SetTexture(string, Texture)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="material">材质。</param>
    /// <param name="textureDirectory">纹理目录。</param>
    /// <param name="sourceName">来源名称。</param>
    private static void ApplyMaterialTextures(Material material, string textureDirectory, string sourceName)
    {
        // 基础色名称。
string albedoName = sourceName + "_ALBD";
        // 常规名称。
string normalName = sourceName + "_NRMR_Normal";
        // 遮罩名称。
string maskName = sourceName + "_NRMR_MetallicSmoothness";
        if (sourceName == "Shirts")
        {
            albedoName = "Shirts_ALBD";
            normalName = "Shirts_NRMR_Normal";
            maskName = "Shirts_NRMR_MetallicSmoothness";
        }

        // 基础色。
Texture2D albedo = FindTexture(textureDirectory, albedoName);
        // 普通状态值。
Texture2D normal = FindTexture(textureDirectory, normalName);
        // 是否固定光滑度。
bool fixedGlossiness = sourceName is "hair" or "Face" or "Body" or "Mouth";
        // 遮罩。
Texture2D mask;
        if (fixedGlossiness)
        {
            mask = null;
        }
        else
        {
            mask = FindTexture(textureDirectory, maskName);
        }

        material.SetTexture("_MainTex", albedo);
        material.SetTexture("_BumpMap", normal);
        material.SetTexture("_MetallicGlossMap", mask);
        material.SetFloat("_Metallic", 0f);
        // 是否皮肤。
bool skin = sourceName == "Face" || sourceName == "Body";
        // 光滑度比例。
float glossinessRatio = sourceName switch
        {
            "Face" or "Body" => 0.68f,
            "Mouth" => 0.8f,
            "Props" => 0.64f,
            "hair" => 0.43f,
            "Jacket" => 0.54f,
            _ => 0.40f
        };
        material.SetFloat("_Glossiness", sourceName switch
        {
            "EyeL" or "EyeR" => 0.58f,
            "Mouth" => 0.38f,
            "Face" or "Body" => 0.27f,
            "hair" => 0.24f,
            _ => 0.18f
        });
        material.SetFloat("_GlossMapScale", glossinessRatio);
        // 调用前缓存的材质。
global::UnityEngine.Material callReceiver5 = material;
        // 按原顺序缓存的实参文字“_BumpScale”。
string argumentValue4 = "_BumpScale";
        // 分支选出的0.58f。
float selectedValue3;
        if (skin)
        {
            selectedValue3 = 0.58f;
        }
        else
        {
            if (sourceName == "hair")
            {
                selectedValue3 = 0.38f;
            }
            else
            {
                selectedValue3 = 0.85f;
            }
        }

        callReceiver5.SetFloat(argumentValue4, selectedValue3);
        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        if (normal != null)
        {
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            material.DisableKeyword("_NORMALMAP");
        }

        if (mask != null)
        {
            material.EnableKeyword("_METALLICGLOSSMAP");
        }
        else
        {
            material.DisableKeyword("_METALLICGLOSSMAP");
        }

        // 发型由实体网格塑形，原始发色贴图的透明通道全为实心。
        material.SetFloat("_Mode", 0f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.SetInt("_SrcBlend", (int)BlendMode.One);
        material.SetInt("_DstBlend", (int)BlendMode.Zero);
        material.SetInt("_ZWrite", 1);
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = -1;
    }

    /// <summary>ConfigureMaterialsOnRenderers 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、EditorUtility.SetDirty(Object)、FemaleGanardEnemyImporter.FindMaterialPath(string, string, string)、FemaleGanardEnemyImporter.ResolveSourceName(string)、GameObject.GetComponentsInChildren&lt;Renderer&gt;(bool)、string.IsNullOrEmpty(string)、string.LastIndexOf(string, StringComparison) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ImportOne(string, string, string, string)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;Material&gt;(string)、EditorUtility.SetDirty(Object)、FemaleGanardEnemyImporter.FindMaterialPath(string, string, string)、FemaleGanardEnemyImporter.ResolveSourceName(string)、GameObject.GetComponentsInChildren&lt;Renderer&gt;(bool)、string.IsNullOrEmpty(string)、string.LastIndexOf(string, StringComparison) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="instance">实例。</param>
    /// <param name="textureDirectory">纹理目录。</param>
    private static void ConfigureMaterialsOnRenderers(GameObject instance, string textureDirectory)
    {
        // renderer（渲染器）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、FemaleGanardEnemyImporter.ConfigureMaterialsOnRenderers(GameObject, string)。
foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            // 材质集合。
Material[] materials = renderer.sharedMaterials;
            // 当前遍历索引。
for (int i = 0; i < materials.Length; i++)
            {
                // 材质名称。
string materialName;
                if (materials[i] != null)
                {
                    materialName = materials[i].name;
                }
                else
                {
                    materialName = string.Empty;
                }

                // 渲染器材质名称。
string rendererMaterialName = renderer.name;
                // 名称分隔器。
int nameSeparator = rendererMaterialName.LastIndexOf("__", StringComparison.Ordinal);
                if (nameSeparator >= 0 && nameSeparator + 2 < rendererMaterialName.Length)
                {
                    rendererMaterialName = rendererMaterialName.Substring(nameSeparator + 2);
                }

                // 提示指定的名称。
string hintedName;
                if (string.IsNullOrEmpty(rendererMaterialName))
                {
                    hintedName = materialName;
                }
                else
                {
                    hintedName = rendererMaterialName;
                }

                // 来源名称。
string sourceName = ResolveSourceName(hintedName);
                // 材质路径。
string materialPath = FindMaterialPath(textureDirectory, hintedName, sourceName);
                // 已配置。
Material configured = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (configured != null)
                {
                    materials[i] = configured;
                }
            }

            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }
    }

    /// <summary>FindMaterialPath 的业务处理入口；本方法直接依赖 FemaleGanardEnemyImporter.GetMaterialNamesForSource(string, string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)、File.Exists(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureMaterialsOnRenderers(GameObject, string)；下游（含内部回调体） FemaleGanardEnemyImporter.GetMaterialNamesForSource(string, string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)、File.Exists(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="textureDirectory">纹理目录。</param>
    /// <param name="materialName">材质名称。</param>
    /// <param name="sourceName">来源名称。</param>
    private static string FindMaterialPath(string textureDirectory, string materialName, string sourceName)
    {
        // 精确。
string exact = textureDirectory + "/" + materialName + ".mat";
        if (File.Exists(ToPhysicalPath(exact)))
        {
            return exact;
        }

        // 候选集合。
string[] candidates = GetMaterialNamesForSource(sourceName, textureDirectory);
        return textureDirectory + "/" + candidates[0] + ".mat";
    }

    /// <summary>FindTexture 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ApplyMaterialTextures(Material, string, string)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;Texture2D&gt;(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="textureDirectory">纹理目录。</param>
    /// <param name="textureName">纹理名称。</param>
    private static Texture2D FindTexture(string textureDirectory, string textureName)
    {
        // 扩展名集合。
string[] extensions =
        {
            ".jpg",
            ".tga",
            ".png"
        };
        // extension（扩展名）：每轮从扩展名集合取得一个元素，仅代表当前这一项。使用位置：FemaleGanardEnemyImporter.FindTexture(string, string)。
foreach (string extension in extensions)
        {
            // 路径。
string path = textureDirectory + "/" + textureName + extension;
            // 当前处理的纹理资源。
Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    /// <summary>ResolveSourceName 的业务处理入口；本方法直接依赖 string.Contains(string)、string.ToLowerInvariant()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureMaterialsOnRenderers(GameObject, string)；下游（含内部回调体） string.Contains(string)、string.ToLowerInvariant()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="materialName">材质名称。</param>
    private static string ResolveSourceName(string materialName)
    {
        // 下部。
string lower = materialName.ToLowerInvariant();
        if (lower.Contains("apron"))
        {
            return "Apron";
        }

        if (lower.Contains("body"))
        {
            return "Body";
        }

        if (lower.Contains("eyel"))
        {
            return "EyeL";
        }

        if (lower.Contains("eyer"))
        {
            return "EyeR";
        }

        if (lower.Contains("face"))
        {
            return "Face";
        }

        if (lower.Contains("hair"))
        {
            return "hair";
        }

        if (lower.Contains("jacket"))
        {
            return "Jacket";
        }

        if (lower.Contains("shirt"))
        {
            return "Shirts";
        }

        if (lower.Contains("skirt"))
        {
            return "Skirt";
        }

        if (lower.Contains("mouth") || lower.Contains("teeth"))
        {
            return "Mouth";
        }

        if (lower.Contains("shoe") || lower.Contains("prop") || lower.Contains("scarf"))
        {
            return "Props";
        }

        return "Body";
    }

    /// <summary>ConfigureEnemyComponents 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;ExternalBehaviorTree&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;RuntimeAnimatorController&gt;(string)、EditorUtility.CopySerialized(Object, Object)、EditorUtility.SetDirty(Object)、FemaleGanardEnemyImporter.ConfigureBakedHitboxes(Animator, CapsuleCollider, BoxCollider)、FemaleGanardEnemyImporter.GetOrAdd&lt;BehaviorTree&gt;(GameObject)、FemaleGanardEnemyImporter.GetOrAdd&lt;BoxCollider&gt;(GameObject) 等共 31 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ImportOne(string, string, string, string)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;ExternalBehaviorTree&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;RuntimeAnimatorController&gt;(string)、EditorUtility.CopySerialized(Object, Object)、EditorUtility.SetDirty(Object)、FemaleGanardEnemyImporter.ConfigureBakedHitboxes(Animator, CapsuleCollider, BoxCollider)、FemaleGanardEnemyImporter.GetOrAdd&lt;BehaviorTree&gt;(GameObject)、FemaleGanardEnemyImporter.GetOrAdd&lt;BoxCollider&gt;(GameObject) 等共 31 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="instance">实例。</param>
    /// <param name="templateRoot">模板根节点。</param>
    private static void ConfigureEnemyComponents(GameObject instance, GameObject templateRoot)
    {
        // 模板控制器。
VillagerController templateController = templateRoot.GetComponent<VillagerController>();
        // 模板寻路代理。
PathAgent templatePathAgent = templateRoot.GetComponent<PathAgent>();
        // 模板行为树。
BehaviorTree templateBehaviorTree = templateRoot.GetComponent<BehaviorTree>();
        // 模板参与者。
VillagerQTEParticipant templateParticipant = templateRoot.GetComponent<VillagerQTEParticipant>();
        // 模板转发器。
EnemyQTEAnimationEventRelay templateRelay = templateRoot.GetComponent<EnemyQTEAnimationEventRelay>();
        // 村民敌人控制器。
VillagerController controller = GetOrAdd<VillagerController>(instance);
        // 寻路代理。
PathAgent pathAgent = GetOrAdd<PathAgent>(instance);
        // 行为树。
BehaviorTree behaviorTree = GetOrAdd<BehaviorTree>(instance);
        // 参与者。
VillagerQTEParticipant participant = GetOrAdd<VillagerQTEParticipant>(instance);
        // 转发器。
EnemyQTEAnimationEventRelay relay = GetOrAdd<EnemyQTEAnimationEventRelay>(instance);
        GetOrAdd<EnemyColliderController>(instance);
        GetOrAdd<Rigidbody>(instance);
        GetOrAdd<CapsuleCollider>(instance);
        GetOrAdd<BoxCollider>(instance);
        if (templateController != null)
        {
            EditorUtility.CopySerialized(templateController, controller);
        }

        if (templatePathAgent != null)
        {
            EditorUtility.CopySerialized(templatePathAgent, pathAgent);
        }

        if (templateBehaviorTree != null)
        {
            EditorUtility.CopySerialized(templateBehaviorTree, behaviorTree);
        }

        if (templateParticipant != null)
        {
            EditorUtility.CopySerialized(templateParticipant, participant);
        }

        if (templateRelay != null)
        {
            EditorUtility.CopySerialized(templateRelay, relay);
        }

        // 动画控制器。
Animator animator = instance.GetComponent<Animator>();
        // 动画控制器控制器。
RuntimeAnimatorController animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(enemyAnimationControllerPath);
        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        controller.healthModule = GetOrAdd<CharacterHealthModule>(instance);
        controller.combatModule = GetOrAdd<EnemyCombatModule>(instance);
        controller.perceptionModule = GetOrAdd<EnemyPerceptionModule>(instance);
        controller.actionCoordinator = GetOrAdd<EnemyActionCoordinator>(instance);
        controller.animationPresenter = GetOrAdd<EnemyAnimationAdapter>(instance);
        controller.motionExecutor = GetOrAdd<CharacterMotionExecutor>(instance);
        controller.motionExecutor.host = controller;
        controller.combatModule.host = controller;
        controller.perceptionModule.host = controller;
        controller.actionCoordinator.host = controller;
        controller.animationPresenter.host = controller;
        // 电锯表现。
var chainsawPresentation = instance.GetComponent<ChainsawManAnimationBridge>();
        if (chainsawPresentation != null)
        {
            chainsawPresentation.animationPresenter = controller.animationPresenter;
        }

        controller.enableMovementDiagnostic = false;
        participant.controller = controller;
        participant.animator = animator;
        participant.pathAgent = pathAgent;
        participant.behaviorTree = behaviorTree;
        participant.body = instance.GetComponent<Rigidbody>();
        participant.participantColliders = ConfigureBakedHitboxes(animator, instance.GetComponent<CapsuleCollider>(), instance.GetComponent<BoxCollider>());
        participant.keepAIDisabledForIsolatedTest = false;
        relay.participant = participant;
        behaviorTree.ExternalBehavior = AssetDatabase.LoadAssetAtPath<ExternalBehaviorTree>(behaviorTreePath);
        // 身体。
Rigidbody body = instance.GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        // 胶囊。
CapsuleCollider capsule = instance.GetComponent<CapsuleCollider>();
        capsule.isTrigger = false;
        capsule.radius = 0.35f;
        capsule.height = 1.8f;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        // 箱体。
BoxCollider box = instance.GetComponent<BoxCollider>();
        box.enabled = false;
        box.size = new Vector3(0.7f, 0.4f, 1.6f);
        box.center = new Vector3(0f, 0.2f, 0f);
        EditorUtility.SetDirty(instance);
    }

    /// <summary>ConfigureBakedHitboxes 的业务处理入口；本方法直接依赖 FemaleGanardEnemyImporter.AddArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.AddCapsuleHitbox(Animator, HumanBodyBones, HitboxType, float, float, Vector3, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.AddLimbHitbox(Animator, HumanBodyBones, HitboxType, float, float, float, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.AddSphereHitbox(Animator, HumanBodyBones, HitboxType, float, int, List&lt;Collider&gt;)、LayerMask.NameToLayer(string)、List&lt;Collider&gt;.Add(Collider)、List&lt;Collider&gt;.List() 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureEnemyComponents(GameObject, GameObject)；下游（含内部回调体） FemaleGanardEnemyImporter.AddArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.AddCapsuleHitbox(Animator, HumanBodyBones, HitboxType, float, float, Vector3, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.AddLimbHitbox(Animator, HumanBodyBones, HitboxType, float, float, float, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.AddSphereHitbox(Animator, HumanBodyBones, HitboxType, float, int, List&lt;Collider&gt;)、LayerMask.NameToLayer(string)、List&lt;Collider&gt;.Add(Collider)、List&lt;Collider&gt;.List() 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="bodyCollider">身体碰撞体。</param>
    /// <param name="crawlCollider">爬行碰撞体。</param>
    private static Collider[] ConfigureBakedHitboxes(Animator animator, CapsuleCollider bodyCollider, BoxCollider crawlCollider)
    {
        // 碰撞体集合。
List<Collider> colliders = new List<Collider>();
        if (bodyCollider != null)
        {
            colliders.Add(bodyCollider);
        }

        if (crawlCollider != null)
        {
            colliders.Add(crawlCollider);
        }

        // 图层。
int layer = LayerMask.NameToLayer("EnemyHitbox");
        if (animator == null || !animator.isHuman || layer < 0)
        {
            return colliders.ToArray();
        }

        AddSphereHitbox(animator, HumanBodyBones.Head, HitboxType.Head, 0.12f, layer, colliders);
        AddCapsuleHitbox(animator, HumanBodyBones.Chest, HitboxType.Chest, 0.18f, 0.35f, Vector3.up * 0.1f, layer, colliders);
        AddCapsuleHitbox(animator, HumanBodyBones.Hips, HitboxType.Pelvis, 0.16f, 0.25f, Vector3.zero, layer, colliders);
        AddArmHitbox(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HitboxType.LeftArm, layer, colliders);
        AddArmHitbox(animator, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HitboxType.LeftArm, layer, colliders);
        AddArmHitbox(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HitboxType.RightArm, layer, colliders);
        AddArmHitbox(animator, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HitboxType.RightArm, layer, colliders);
        AddSphereHitbox(animator, HumanBodyBones.LeftHand, HitboxType.LeftArm, 0.09f, layer, colliders);
        AddSphereHitbox(animator, HumanBodyBones.RightHand, HitboxType.RightArm, 0.09f, layer, colliders);
        AddLimbHitbox(animator, HumanBodyBones.LeftUpperLeg, HitboxType.LeftLeg, 0.1f, 0.38f, -0.1f, layer, colliders);
        AddLimbHitbox(animator, HumanBodyBones.LeftLowerLeg, HitboxType.LeftLeg, 0.095f, 0.4f, -0.12f, layer, colliders);
        AddLimbHitbox(animator, HumanBodyBones.RightUpperLeg, HitboxType.RightLeg, 0.1f, 0.38f, -0.1f, layer, colliders);
        AddLimbHitbox(animator, HumanBodyBones.RightLowerLeg, HitboxType.RightLeg, 0.095f, 0.4f, -0.12f, layer, colliders);
        return colliders.ToArray();
    }

    /// <summary>AddSphereHitbox 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;SphereCollider&gt;()、FemaleGanardEnemyImporter.ConfigureHitboxComponent(Transform, HitboxType)、GameObject.AddComponent&lt;SphereCollider&gt;()、List&lt;Collider&gt;.Add(Collider)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureBakedHitboxes(Animator, CapsuleCollider, BoxCollider)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;SphereCollider&gt;()、FemaleGanardEnemyImporter.ConfigureHitboxComponent(Transform, HitboxType)、GameObject.AddComponent&lt;SphereCollider&gt;()、List&lt;Collider&gt;.Add(Collider)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="bone">骨骼。</param>
    /// <param name="type">当前命中部位类别。</param>
    /// <param name="radius">半径。</param>
    /// <param name="layer">图层。</param>
    /// <param name="colliders">碰撞体集合。</param>
    private static void AddSphereHitbox(Animator animator, HumanBodyBones bone, HitboxType type, float radius, int layer, List<Collider> colliders)
    {
        // 目标变换节点。
Transform target = animator.GetBoneTransform(bone);
        if (target == null)
        {
            return;
        }

        target.gameObject.layer = layer;
        // 碰撞体。
SphereCollider collider = target.GetComponent<SphereCollider>();
        if (collider == null)
        {
            collider = target.gameObject.AddComponent<SphereCollider>();
        }

        collider.radius = radius;
        collider.center = Vector3.zero;
        collider.isTrigger = true;
        ConfigureHitboxComponent(target, type);
        colliders.Add(collider);
    }

    /// <summary>AddCapsuleHitbox 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、FemaleGanardEnemyImporter.ConfigureHitboxComponent(Transform, HitboxType)、GameObject.AddComponent&lt;CapsuleCollider&gt;()、List&lt;Collider&gt;.Add(Collider)、Mathf.Max(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.AddLimbHitbox(Animator, HumanBodyBones, HitboxType, float, float, float, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.ConfigureBakedHitboxes(Animator, CapsuleCollider, BoxCollider)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、FemaleGanardEnemyImporter.ConfigureHitboxComponent(Transform, HitboxType)、GameObject.AddComponent&lt;CapsuleCollider&gt;()、List&lt;Collider&gt;.Add(Collider)、Mathf.Max(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="bone">骨骼。</param>
    /// <param name="type">当前命中部位类别。</param>
    /// <param name="radius">半径。</param>
    /// <param name="height">高度。</param>
    /// <param name="center">中心。</param>
    /// <param name="layer">图层。</param>
    /// <param name="colliders">碰撞体集合。</param>
    private static void AddCapsuleHitbox(Animator animator, HumanBodyBones bone, HitboxType type, float radius, float height, Vector3 center, int layer, List<Collider> colliders)
    {
        // 目标变换节点。
Transform target = animator.GetBoneTransform(bone);
        if (target == null)
        {
            return;
        }

        target.gameObject.layer = layer;
        // 碰撞体。
CapsuleCollider collider = target.GetComponent<CapsuleCollider>();
        if (collider == null)
        {
            collider = target.gameObject.AddComponent<CapsuleCollider>();
        }

        collider.radius = radius;
        collider.height = Mathf.Max(height, radius * 2f);
        collider.direction = 1;
        collider.center = center;
        collider.isTrigger = true;
        ConfigureHitboxComponent(target, type);
        colliders.Add(collider);
    }

    /// <summary>AddLimbHitbox 的业务处理入口；本方法直接依赖 FemaleGanardEnemyImporter.AddCapsuleHitbox(Animator, HumanBodyBones, HitboxType, float, float, Vector3, int, List&lt;Collider&gt;)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureBakedHitboxes(Animator, CapsuleCollider, BoxCollider)；下游（含内部回调体） FemaleGanardEnemyImporter.AddCapsuleHitbox(Animator, HumanBodyBones, HitboxType, float, float, Vector3, int, List&lt;Collider&gt;)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="bone">骨骼。</param>
    /// <param name="type">当前命中部位类别。</param>
    /// <param name="radius">半径。</param>
    /// <param name="height">高度。</param>
    /// <param name="centerY">中心纵向。</param>
    /// <param name="layer">图层。</param>
    /// <param name="colliders">碰撞体集合。</param>
    private static void AddLimbHitbox(Animator animator, HumanBodyBones bone, HitboxType type, float radius, float height, float centerY, int layer, List<Collider> colliders)
    {
        AddCapsuleHitbox(animator, bone, type, radius, height, new Vector3(0f, centerY, 0f), layer, colliders);
    }

    /// <summary>AddArmHitbox 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、FemaleGanardEnemyImporter.ConfigureHitboxComponent(Transform, HitboxType)、GameObject.AddComponent&lt;CapsuleCollider&gt;()、List&lt;Collider&gt;.Add(Collider)、Mathf.Abs(float)、Mathf.Max(float, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureBakedHitboxes(Animator, CapsuleCollider, BoxCollider)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、FemaleGanardEnemyImporter.ConfigureHitboxComponent(Transform, HitboxType)、GameObject.AddComponent&lt;CapsuleCollider&gt;()、List&lt;Collider&gt;.Add(Collider)、Mathf.Abs(float)、Mathf.Max(float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="animator">动画控制器。</param>
    /// <param name="bone">骨骼。</param>
    /// <param name="nextBone">下一次骨骼。</param>
    /// <param name="type">当前命中部位类别。</param>
    /// <param name="layer">图层。</param>
    /// <param name="colliders">碰撞体集合。</param>
    private static void AddArmHitbox(Animator animator, HumanBodyBones bone, HumanBodyBones nextBone, HitboxType type, int layer, List<Collider> colliders)
    {
        // 当前变换节点。
Transform current = animator.GetBoneTransform(bone);
        // 下一个变换节点。
Transform next = animator.GetBoneTransform(nextBone);
        if (current == null || next == null)
        {
            return;
        }

        // 局部偏移量。
Vector3 localOffset = current.InverseTransformPoint(next.position);
        // 长度。
float length = localOffset.magnitude;
        if (length < 0.001f)
        {
            return;
        }

        // 绝对值横向。
float absX = Mathf.Abs(localOffset.x);
        // 绝对值纵向。
float absY = Mathf.Abs(localOffset.y);
        // 绝对值深度。
float absZ = Mathf.Abs(localOffset.z);
        // 方向。
int direction;
        if (absX >= absY && absX >= absZ)
        {
            direction = 0;
        }
        else
        {
            if (absY >= absZ)
            {
                direction = 1;
            }
            else
            {
                direction = 2;
            }
        }

        current.gameObject.layer = layer;
        // 碰撞体。
CapsuleCollider collider = current.GetComponent<CapsuleCollider>();
        if (collider == null)
        {
            collider = current.gameObject.AddComponent<CapsuleCollider>();
        }

        collider.radius = 0.1f;
        collider.height = Mathf.Max(length, 0.2f);
        collider.direction = direction;
        collider.center = localOffset * 0.5f;
        collider.isTrigger = true;
        ConfigureHitboxComponent(current, type);
        colliders.Add(collider);
    }

    /// <summary>ConfigureHitboxComponent 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;EnemyHitbox&gt;()、GameObject.AddComponent&lt;EnemyHitbox&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.AddArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.AddCapsuleHitbox(Animator, HumanBodyBones, HitboxType, float, float, Vector3, int, List&lt;Collider&gt;)、FemaleGanardEnemyImporter.AddSphereHitbox(Animator, HumanBodyBones, HitboxType, float, int, List&lt;Collider&gt;)；下游（含内部回调体） Component.GetComponent&lt;EnemyHitbox&gt;()、GameObject.AddComponent&lt;EnemyHitbox&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标变换节点。</param>
    /// <param name="type">当前命中部位类别。</param>
    private static void ConfigureHitboxComponent(Transform target, HitboxType type)
    {
        // 命中盒。
EnemyHitbox hitbox = target.GetComponent<EnemyHitbox>();
        if (hitbox == null)
        {
            hitbox = target.gameObject.AddComponent<EnemyHitbox>();
        }

        hitbox.hitboxType = type;
    }

    /// <summary>GetOrAdd 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;T&gt;()、GameObject.GetComponent&lt;T&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureEnemyComponents(GameObject, GameObject)；下游（含内部回调体） GameObject.AddComponent&lt;T&gt;()、GameObject.GetComponent&lt;T&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="target">目标游戏对象。</param>
    private static T GetOrAdd</* 中文含义：由调用方指定的通用类型参数。 */ T>(GameObject target)
        where T : Component
    {
        // 组件。
T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }
        else
        {
            return target.AddComponent<T>();
        }
    }

    /// <summary>SetLayers 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ImportOne(string, string, string, string)；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根游戏对象。</param>
    /// <param name="rootLayer">根节点图层。</param>
    /// <param name="childLayer">子级图层。</param>
    private static void SetLayers(GameObject root, int rootLayer, int childLayer)
    {
        if (rootLayer >= 0)
        {
            root.layer = rootLayer;
        }

        if (childLayer < 0)
        {
            return;
        }

        // child（子级）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：FemaleGanardEnemyImporter.SetLayers(GameObject, int, int)。
foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root.transform)
            {
                child.gameObject.layer = childLayer;
            }
        }
    }

    /// <summary>EnsureFolder 的业务处理入口；本方法直接依赖 Directory.CreateDirectory(string)、Directory.Exists(string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.Import()；下游（含内部回调体） Directory.CreateDirectory(string)、Directory.Exists(string)、FemaleGanardEnemyImporter.ToPhysicalPath(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="assetPath">资源路径。</param>
    private static void EnsureFolder(string assetPath)
    {
        // 物理路径。
string physicalPath = ToPhysicalPath(assetPath);
        if (!Directory.Exists(physicalPath))
        {
            Directory.CreateDirectory(physicalPath);
        }
    }

    /// <summary>ToPhysicalPath 的业务处理入口；本方法直接依赖 Directory.GetParent(string)、string.Replace(char, char)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureMaterials(string)、FemaleGanardEnemyImporter.ConfigureTextureImporters(string)、FemaleGanardEnemyImporter.EnsureFolder(string)、FemaleGanardEnemyImporter.FindMaterialPath(string, string, string)、FemaleGanardEnemyImporter.GeneratePackedTextures(string) 等共 6 处；下游（含内部回调体） Directory.GetParent(string)、string.Replace(char, char)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="assetPath">资源路径。</param>
    private static string ToPhysicalPath(string assetPath)
    {
        // 工程根节点。
string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        return projectRoot + "/" + assetPath.Replace('\\', '/');
    }

    /// <summary>ToAssetPath 的业务处理入口；本方法直接依赖 Directory.GetParent(string)、string.Replace(char, char)、string.StartsWith(string, StringComparison)、string.Substring(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 FemaleGanardEnemyImporter.ConfigureTextureImporters(string)；下游（含内部回调体） Directory.GetParent(string)、string.Replace(char, char)、string.StartsWith(string, StringComparison)、string.Substring(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="physicalPath">物理路径。</param>
    private static string ToAssetPath(string physicalPath)
    {
        // 工程根节点。
string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/');
        // 归一化。
string normalized = physicalPath.Replace('\\', '/');
        if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized.Substring(projectRoot.Length + 1);
        }
        else
        {
            return normalized;
        }
    }
}

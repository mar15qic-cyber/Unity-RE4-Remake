using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>显式登记单个资源的迁移。预览不写资源；应用和回退都核对整份资源摘要。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构资源迁移工具")]
public static class ArchitectureAssetMigrationTool
{
    /// <summary>编辑器配置与校验模块 MigrationPlan；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构资源迁移工具+迁移方案")]
    public sealed class MigrationPlan
    {
        /// <summary>版本。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("版本")]
        [UnityEngine.InspectorName("版本")]
        [Newtonsoft.Json.JsonProperty("版本")]
        public int version = 1;
        /// <summary>资源路径。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("资源路径")]
        [UnityEngine.InspectorName("资源路径")]
        [Newtonsoft.Json.JsonProperty("资源路径")]
        public string assetPath;
        /// <summary>宿主类型。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("宿主类型")]
        [UnityEngine.InspectorName("宿主类型")]
        [Newtonsoft.Json.JsonProperty("宿主类型")]
        public string hostType;
        /// <summary>新组件类型。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新组件类型")]
        [UnityEngine.InspectorName("新组件类型")]
        [Newtonsoft.Json.JsonProperty("新组件类型")]
        public string newComponentType;
        /// <summary>引用字段。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("引用字段")]
        [UnityEngine.InspectorName("引用字段")]
        [Newtonsoft.Json.JsonProperty("引用字段")]
        public string referenceField;
        /// <summary>新组件配置。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新组件配置")]
        [UnityEngine.InspectorName("新组件配置")]
        [Newtonsoft.Json.JsonProperty("新组件配置")]
        public string newComponentConfiguration = "{}";
    }

    /// <summary>编辑器配置与校验模块 ObjectChange；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构资源迁移工具+对象变更")]
    public sealed class ObjectChange
    {
        /// <summary>层级索引。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("层级索引")]
        [UnityEngine.InspectorName("层级索引")]
        [Newtonsoft.Json.JsonProperty("层级索引")]
        public string hierarchyIndex;
        /// <summary>对象名称。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("对象名称")]
        [UnityEngine.InspectorName("对象名称")]
        [Newtonsoft.Json.JsonProperty("对象名称")]
        public string objectName;
        /// <summary>原宿主数据。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("原宿主数据")]
        [UnityEngine.InspectorName("原宿主数据")]
        [Newtonsoft.Json.JsonProperty("原宿主数据")]
        public string originalHostData;
        /// <summary>新宿主数据。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新宿主数据")]
        [UnityEngine.InspectorName("新宿主数据")]
        [Newtonsoft.Json.JsonProperty("新宿主数据")]
        public string newHostData;
        /// <summary>新组件数据。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新组件数据")]
        [UnityEngine.InspectorName("新组件数据")]
        [Newtonsoft.Json.JsonProperty("新组件数据")]
        public string newComponentData;
        /// <summary>是否新增组件。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新增组件")]
        [UnityEngine.InspectorName("新增组件")]
        [Newtonsoft.Json.JsonProperty("新增组件")]
        public bool newlyAddedComponent;
        /// <summary>是否新增绑定。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新增绑定")]
        [UnityEngine.InspectorName("新增绑定")]
        [Newtonsoft.Json.JsonProperty("新增绑定")]
        public bool newlyAddedBinding;
    }

    /// <summary>编辑器配置与校验模块 MigrationRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构资源迁移工具+迁移记录")]
    public sealed class MigrationRecord
    {
        /// <summary>方案。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("方案")]
        [UnityEngine.InspectorName("方案")]
        [Newtonsoft.Json.JsonProperty("方案")]
        public MigrationPlan plan;
        /// <summary>资源身份。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("资源身份")]
        [UnityEngine.InspectorName("资源身份")]
        [Newtonsoft.Json.JsonProperty("资源身份")]
        public string assetIdentity;
        /// <summary>原摘要。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("原摘要")]
        [UnityEngine.InspectorName("原摘要")]
        [Newtonsoft.Json.JsonProperty("原摘要")]
        public string originalSummary;
        /// <summary>新摘要。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新摘要")]
        [UnityEngine.InspectorName("新摘要")]
        [Newtonsoft.Json.JsonProperty("新摘要")]
        public string newSummary;
        /// <summary>状态。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("状态")]
        [UnityEngine.InspectorName("状态")]
        [Newtonsoft.Json.JsonProperty("状态")]
        public string state;
        /// <summary>对象。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("对象")]
        [UnityEngine.InspectorName("对象")]
        [Newtonsoft.Json.JsonProperty("对象")]
        public List<ObjectChange> objectValue = new List<ObjectChange>();
    }

    /// <summary>编辑器配置与校验模块 AssetContext；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构资源迁移工具+资源上下文")]
    private sealed class AssetContext : IDisposable
    {
        /// <summary>根游戏对象。只读字段，初始化后不由普通写入口替换。</summary>
        public readonly GameObject[] rootObject;
        /// <summary>路径。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly string path;
        /// <summary>预制件。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly GameObject prefab;
        /// <summary>场景。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly Scene scene;
        /// <summary>当前是否打开。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly bool currentOpen;
        /// <summary>原始生效场景。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly Scene originalActiveScene;
        /// <summary>构造并初始化 AssetContext 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)；下游（含内部回调体） EditorSceneManager.OpenScene(string, OpenSceneMode)、InvalidOperationException.InvalidOperationException(string)、PrefabUtility.LoadPrefabContents(string)、Scene.GetRootGameObjects()、Scene.IsValid()、SceneManager.GetActiveScene()、SceneManager.GetSceneByPath(string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="path">路径。</param>
        public AssetContext(string path)
        {
            this.path = path;
            originalActiveScene = SceneManager.GetActiveScene();
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                prefab = PrefabUtility.LoadPrefabContents(path);
                rootObject = new[]
                {
                    prefab
                };
            }
            else
            {
                scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    currentOpen = true;
                }

                if (scene.isDirty)
                {
                    throw new InvalidOperationException("场景存在未保存修改，迁移不会替用户保存：" + path);
                }

                rootObject = scene.GetRootGameObjects();
            }
        }

        /// <summary>Save 的业务处理入口；本方法直接依赖 EditorSceneManager.SaveScene(Scene, string)、IOException.IOException(string)、PrefabUtility.SaveAsPrefabAsset(GameObject, string, out bool)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） EditorSceneManager.SaveScene(Scene, string)、IOException.IOException(string)、PrefabUtility.SaveAsPrefabAsset(GameObject, string, out bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public void Save()
        {
            if (prefab != null)
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, path, out bool /* 中文含义：是否成功。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ success);
                if (!success)
                {
                    throw new IOException("预制件保存失败：" + path);
                }
            }
            else if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException("场景保存失败：" + path);
            }
        }

        /// <summary>Dispose 的业务处理入口；本方法直接依赖 EditorSceneManager.CloseScene(Scene, bool)、PrefabUtility.UnloadPrefabContents(GameObject)、Scene.IsValid()、SceneManager.SetActiveScene(Scene)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EditorSceneManager.CloseScene(Scene, bool)、PrefabUtility.UnloadPrefabContents(GameObject)、Scene.IsValid()、SceneManager.SetActiveScene(Scene)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public void Dispose()
        {
            if (prefab != null)
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            if (currentOpen && scene.IsValid())
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(originalActiveScene);
            }
        }
    }

    /// <summary>Preview 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.AssetContext.AssetContext(string)、ArchitectureAssetMigrationTool.CalculateSummary(string)、ArchitectureAssetMigrationTool.FindType(string)、ArchitectureAssetMigrationTool.GetIndex(GameObject[], Transform)、ArchitectureAssetMigrationTool.MigrationRecord.MigrationRecord()、ArchitectureAssetMigrationTool.ObjectChange.ObjectChange()、ArchitectureAssetMigrationTool.ReadReference(Component, string) 等共 21 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Validate(ArchitectureAssetMigrationTool.MigrationRecord)；下游（含内部回调体） ArchitectureAssetMigrationTool.AssetContext.AssetContext(string)、ArchitectureAssetMigrationTool.CalculateSummary(string)、ArchitectureAssetMigrationTool.FindType(string)、ArchitectureAssetMigrationTool.GetIndex(GameObject[], Transform)、ArchitectureAssetMigrationTool.MigrationRecord.MigrationRecord()、ArchitectureAssetMigrationTool.ObjectChange.ObjectChange()、ArchitectureAssetMigrationTool.ReadReference(Component, string) 等共 21 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="plan">方案。</param>
    public static MigrationRecord Preview(MigrationPlan plan)
    {
        ValidatePlan(plan);
        // 当前迁移记录。
var record = new MigrationRecord
        {
            plan = JsonUtility.FromJson<MigrationPlan>(JsonUtility.ToJson(plan)),
            assetIdentity = AssetDatabase.AssetPathToGUID(plan.assetPath),
            originalSummary = CalculateSummary(plan.assetPath),
            state = "已预览"
        };
        // asset（资源）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var asset = new AssetContext(plan.assetPath))
        {
            // 宿主类型。
var hostType = FindType(plan.hostType);
            // 组件类型。
var componentType = FindType(plan.newComponentType);
            // root（根游戏对象）：每轮从资源的根游戏对象取得一个元素，仅代表当前这一项。使用位置：ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)。
foreach (var root in asset.rootObject)
            {
                // host（所属组件）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 ArchitectureAssetMigrationTool.GetIndex(GameObject[], Transform) 的参数、作为 ArchitectureAssetMigrationTool.ReadReference(Component, string) 的参数、作为 EditorJsonUtility.ToJson(object) 的参数 等共 6 处。
foreach (Component host in root.GetComponentsInChildren(hostType, true))
                {
                    if (host.GetComponents(hostType).Length != 1 || host.GetComponents(componentType).Length > 1)
                    {
                        throw new InvalidOperationException("目标上存在多个同类组件，无法唯一定位：" + host.name);
                    }

                    // 是否具有组件。
Component hasHasComponent = host.GetComponent(componentType);
                    if (hasHasComponent == null && componentType.GetCustomAttributes(typeof(RequireComponent), true).Length > 0)
                    {
                        throw new InvalidOperationException("含隐式依赖组件的迁移必须先登记依赖，当前工具拒绝自动添加：" + componentType.Name);
                    }

                    // 是否绑定。
bool binding = false;
                    if (!string.IsNullOrEmpty(plan.referenceField))
                    {
                        // 字段信息。
var fieldInformation = hostType.GetField(plan.referenceField);
                        if (fieldInformation == null || !fieldInformation.FieldType.IsAssignableFrom(componentType))
                        {
                            throw new InvalidOperationException("引用必须是类型兼容的公开组件字段：" + plan.referenceField);
                        }

                        // 引用。
var reference = ReadReference(host, plan.referenceField);
                        if (reference.objectReferenceValue != null && reference.objectReferenceValue != hasHasComponent)
                        {
                            throw new InvalidOperationException("引用已绑定其他对象，拒绝覆盖：" + host.name + "." + plan.referenceField);
                        }

                        binding = reference.objectReferenceValue == null;
                    }

                    if (hasHasComponent != null && !binding)
                    {
                        continue;
                    }

                    record.objectValue.Add(new ObjectChange { hierarchyIndex = GetIndex(asset.rootObject, host.transform), objectName = host.name, originalHostData = EditorJsonUtility.ToJson(host), newlyAddedComponent = hasHasComponent == null, newlyAddedBinding = binding });
                }
            }
        }

        return record;
    }

    /// <summary>Apply 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.AssetContext.AssetContext(string)、ArchitectureAssetMigrationTool.AssetContext.Save()、ArchitectureAssetMigrationTool.CalculateSummary(string)、ArchitectureAssetMigrationTool.FindType(string)、ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.SetReference(Component, string, Component) 等共 25 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ArchitectureAssetMigrationTool.AssetContext.AssetContext(string)、ArchitectureAssetMigrationTool.AssetContext.Save()、ArchitectureAssetMigrationTool.CalculateSummary(string)、ArchitectureAssetMigrationTool.FindType(string)、ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.SetReference(Component, string, Component) 等共 25 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="record">当前迁移记录。</param>
    /// <param name="recordPath">记录路径。</param>
    public static void Apply(MigrationRecord record, string recordPath)
    {
        ValidatePlan(record.plan);
        ValidateRecordPath(recordPath);
        if (record.state == "已应用")
        {
            ValidateAsset(record, record.newSummary);
            return;
        }

        if (record.state != "已预览")
        {
            throw new InvalidOperationException("只能应用已预览记录");
        }

        ValidateAsset(record, record.originalSummary);
        // 不信任调用者对对象列表的手工改动，重新核对预览的资源范围。
        // 当前预览。
var currentPreview = Preview(record.plan);
        if (currentPreview.objectValue.Count != record.objectValue.Count || currentPreview.objectValue.Where(( /* 中文含义：当前对象变化。 */item, /* 中文含义：索引。 */ index) => item.hierarchyIndex != record.objectValue[index].hierarchyIndex || item.objectName != record.objectValue[index].objectName || item.newlyAddedComponent != record.objectValue[index].newlyAddedComponent || item.newlyAddedBinding != record.objectValue[index].newlyAddedBinding).Any())
        {
            throw new InvalidOperationException("预览对象列表已变化，请重新预览");
        }

        if (File.Exists(recordPath))
        {
            throw new IOException("迁移记录已存在，不覆盖已有审计记录");
        }

        record.state = "应用中";
        WriteRecord(recordPath, record);
        // asset（资源）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var asset = new AssetContext(record.plan.assetPath))
        {
            // 是否具有句柄。
var hasHandle = new List<Tuple<Component, Component, ObjectChange>>();
            try
            {
                // item（当前对象变化）：每轮从记录的对象值取得一个元素，仅代表当前这一项。使用位置：作为 ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 Tuple.Create<Component, Component, ArchitectureAssetMigrationTool.ObjectChange>(Component, Component, ArchitectureAssetMigrationTool.ObjectChange) 的参数 等共 4 处。
foreach (var item in record.objectValue)
                {
                    // 对象值。
var objectValue = PressIndexFind(asset.rootObject, item.hierarchyIndex);
                    // 所属组件。
var host = objectValue.GetComponent(FindType(record.plan.hostType));
                    // 组件类型。
var componentType = FindType(record.plan.newComponentType);
                    // 组件。
global::UnityEngine.Component component;
                    if (item.newlyAddedComponent)
                    {
                        component = objectValue.AddComponent(componentType);
                    }
                    else
                    {
                        component = objectValue.GetComponent(componentType);
                    }

                    if (component == null)
                    {
                        throw new InvalidOperationException("新增组件失败：" + item.objectName);
                    }

                    hasHandle.Add(Tuple.Create(host, component, item));
                    if (item.newlyAddedComponent)
                    {
                        JsonUtility.FromJsonOverwrite(record.plan.newComponentConfiguration, component);
                    }

                    if (item.newlyAddedBinding)
                    {
                        SetReference(host, record.plan.referenceField, component);
                    }

                    item.newHostData = EditorJsonUtility.ToJson(host);
                    item.newComponentData = EditorJsonUtility.ToJson(component);
                    EditorUtility.SetDirty(host);
                    EditorUtility.SetDirty(component);
                }

                if (record.objectValue.Count > 0)
                {
                    asset.Save();
                }
            }
            catch
            {
                // 当前元素索引。
for (int index = hasHandle.Count - 1; index >= 0; index--)
                {
                    // 当前元组。
var item = hasHandle[index];
                    if (item.Item3.newlyAddedBinding)
                    {
                        SetReference(item.Item1, record.plan.referenceField, null);
                    }

                    if (item.Item3.newlyAddedComponent && item.Item2 != null)
                    {
                        UnityEngine.Object.DestroyImmediate(item.Item2);
                    }
                }

                record.state = "应用失败待核对";
                WriteRecord(recordPath, record);
                throw;
            }
        }

        record.newSummary = CalculateSummary(record.plan.assetPath);
        record.state = "已应用";
        WriteRecord(recordPath, record);
    }

    /// <summary>Validate 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="record">当前迁移记录。</param>
    public static void Validate(MigrationRecord record)
    {
        ValidatePlan(record.plan);
        if (record.state != "已应用")
        {
            throw new InvalidOperationException("记录尚未应用");
        }

        ValidateAsset(record, record.newSummary);
        if (Preview(record.plan).objectValue.Count != 0)
        {
            throw new InvalidOperationException("迁移后仍有未绑定对象");
        }
    }

    /// <summary>Fallback 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.AssetContext.AssetContext(string)、ArchitectureAssetMigrationTool.AssetContext.Save()、ArchitectureAssetMigrationTool.CalculateSummary(string)、ArchitectureAssetMigrationTool.FindType(string)、ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)、ArchitectureAssetMigrationTool.SetReference(Component, string, Component)、ArchitectureAssetMigrationTool.Validate(ArchitectureAssetMigrationTool.MigrationRecord) 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ArchitectureAssetMigrationTool.AssetContext.AssetContext(string)、ArchitectureAssetMigrationTool.AssetContext.Save()、ArchitectureAssetMigrationTool.CalculateSummary(string)、ArchitectureAssetMigrationTool.FindType(string)、ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)、ArchitectureAssetMigrationTool.SetReference(Component, string, Component)、ArchitectureAssetMigrationTool.Validate(ArchitectureAssetMigrationTool.MigrationRecord) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="record">当前迁移记录。</param>
    /// <param name="recordPath">记录路径。</param>
    public static void Fallback(MigrationRecord record, string recordPath)
    {
        ValidateRecordPath(recordPath);
        if (record.state == "已回退")
        {
            ValidateAsset(record, record.newSummary);
            return;
        }

        Validate(record);
        // asset（资源）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var asset = new AssetContext(record.plan.assetPath))
        {
            // 先确认全部目标仍可定位，再执行任何删除。
            // 目标游戏对象数组。
var target = record.objectValue.Select( /* 中文含义：当前对象变化。 */item => PressIndexFind(asset.rootObject, item.hierarchyIndex)).ToArray();
            // 当前元素索引。
for (int index = record.objectValue.Count - 1; index >= 0; index--)
            {
                // 当前对象变化。
var item = record.objectValue[index];
                // 所属组件。
var host = target[index].GetComponent(FindType(record.plan.hostType));
                if (item.newlyAddedBinding)
                {
                    SetReference(host, record.plan.referenceField, null);
                }

                if (item.newlyAddedComponent)
                {
                    UnityEngine.Object.DestroyImmediate(target[index].GetComponent(FindType(record.plan.newComponentType)));
                }
            }

            if (record.objectValue.Count > 0)
            {
                asset.Save();
            }
        }

        // 编辑器保存可能重排序列化文本；回退按编辑器状态验收，不要求字节回到旧排列。
        record.newSummary = CalculateSummary(record.plan.assetPath);
        record.state = "已回退";
        WriteRecord(recordPath, record);
    }

    /// <summary>ValidatePlan 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.FindType(string)、ArgumentException.ArgumentException(string)、AssetDatabase.AssetPathToGUID(string)、File.Exists(string)、InvalidOperationException.InvalidOperationException(string)、JObject.Parse(string)、JObject.Properties() 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.Validate(ArchitectureAssetMigrationTool.MigrationRecord)；下游（含内部回调体） ArchitectureAssetMigrationTool.FindType(string)、ArgumentException.ArgumentException(string)、AssetDatabase.AssetPathToGUID(string)、File.Exists(string)、InvalidOperationException.InvalidOperationException(string)、JObject.Parse(string)、JObject.Properties() 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="plan">方案。</param>
    private static void ValidatePlan(MigrationPlan plan)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("运行或编译期间禁止资源迁移");
        }

        if (plan == null || plan.version < 1)
        {
            throw new ArgumentException("迁移方案版本无效");
        }

        // 判空前缓存的方案的资源路径。
string receiverValue2 = plan.assetPath;
        // 路径。
string path;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            path = receiverValue2.Replace('\\', '/');
        }
        else
        {
            path = default;
        }

        if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("..") || !(path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) || !File.Exists(path) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
        {
            throw new ArgumentException("必须明确指定已导入的项目场景或预制件");
        }

        // name（名称）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 ArchitectureAssetMigrationTool.FindType(string) 的参数、作为 ArgumentException.ArgumentException(string) 的参数。
foreach (string name in new[]
        {
            plan.hostType,
            plan.newComponentType
        }

        )
        {
            // 反射操作使用的类型信息。
Type type = FindType(name);
            if (!typeof(MonoBehaviour).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
            {
                throw new ArgumentException("迁移类型必须是具体脚本组件：" + name);
            }
        }

        if (plan.hostType == plan.newComponentType)
        {
            throw new ArgumentException("宿主与新组件类型不能相同");
        }

        if (string.IsNullOrWhiteSpace(plan.newComponentConfiguration))
        {
            throw new ArgumentException("新增组件配置不能为空");
        }

        // 配置。
var configuration = Newtonsoft.Json.Linq.JObject.Parse(plan.newComponentConfiguration);
        // 新类型。
var newType = FindType(plan.newComponentType);
        // property（属性）：每轮从属性集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 ArgumentException.ArgumentException(string) 的参数、作为 Type.GetField(string) 的参数。
foreach (var property in configuration.Properties())
        {
            // 字段。
var field = newType.GetField(property.Name);
            if (field == null || field.IsStatic || field.IsInitOnly || field.IsNotSerialized || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                throw new ArgumentException("配置只能写新增组件的公开数据字段；对象引用必须显式绑定：" + property.Name);
            }
        }
    }

    /// <summary>FindType 的业务处理入口；本方法直接依赖 AppDomain.GetAssemblies()、ArgumentException.ArgumentException(string)、Assembly.GetType(string, bool)、IEnumerable&lt;Assembly&gt;.Select&lt;Assembly, Type&gt;(Func&lt;Assembly, Type&gt;)、IEnumerable&lt;Type&gt;.FirstOrDefault&lt;Type&gt;(Func&lt;Type, bool&gt;)、object.ReferenceEquals(object, object)、Type.GetType(string, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)；下游（含内部回调体） AppDomain.GetAssemblies()、ArgumentException.ArgumentException(string)、Assembly.GetType(string, bool)、IEnumerable&lt;Assembly&gt;.Select&lt;Assembly, Type&gt;(Func&lt;Assembly, Type&gt;)、IEnumerable&lt;Type&gt;.FirstOrDefault&lt;Type&gt;(Func&lt;Type, bool&gt;)、object.ReferenceEquals(object, object)、Type.GetType(string, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    private static Type FindType(string name)
    {
        // 空值判断前缓存的取得类型的返回结果。
global::System.Type availableValue4 = Type.GetType(name, false);
        if (!global::System.Object.ReferenceEquals(availableValue4, null))
        {
            return availableValue4;
        }
        else
        {
            // 空值判断前缓存的反射类型。
global::System.Type availableValue8 = AppDomain.CurrentDomain.GetAssemblies().Select( /* 中文含义：程序集。 */assembly =>
            {
                // 调用前缓存的程序集。
global::System.Reflection.Assembly callReceiver7 = assembly;
                // 空值判断前缓存的名称。
string availableValue5 = name;
                // 分支选出的可用值5。
string selectedValue6;
                if (!global::System.Object.ReferenceEquals(availableValue5, null))
                {
                    selectedValue6 = availableValue5;
                }
                else
                {
                    selectedValue6 = "";
                }

                return callReceiver7.GetType(selectedValue6, false);
            }).FirstOrDefault( /* 中文含义：反射操作使用的类型信息。 */type => type != null);
            if (!global::System.Object.ReferenceEquals(availableValue8, null))
            {
                return availableValue8;
            }
            else
            {
                throw new ArgumentException("找不到组件类型：" + name);
            }
        }
    }

    /// <summary>ReadReference 的业务处理入口；本方法直接依赖 InvalidOperationException.InvalidOperationException(string)、SerializedObject.FindProperty(string)、SerializedObject.SerializedObject(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.SetReference(Component, string, Component)；下游（含内部回调体） InvalidOperationException.InvalidOperationException(string)、SerializedObject.FindProperty(string)、SerializedObject.SerializedObject(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="host">所属组件。</param>
    /// <param name="field">字段。</param>
    private static SerializedProperty ReadReference(Component host, string field)
    {
        // 当前序列化的对象。
var data = new SerializedObject(host);
        // 引用。
var reference = data.FindProperty(field);
        if (reference == null || reference.propertyType != SerializedPropertyType.ObjectReference)
        {
            throw new InvalidOperationException("组件缺少可序列化引用字段：" + host.name + "." + field);
        }

        return reference;
    }

    /// <summary>SetReference 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.ReadReference(Component, string)、InvalidOperationException.InvalidOperationException(string)、SerializedObject.ApplyModifiedPropertiesWithoutUndo()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） ArchitectureAssetMigrationTool.ReadReference(Component, string)、InvalidOperationException.InvalidOperationException(string)、SerializedObject.ApplyModifiedPropertiesWithoutUndo()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="host">所属组件。</param>
    /// <param name="field">字段。</param>
    /// <param name="value">当前组件。</param>
    private static void SetReference(Component host, string field, Component value)
    {
        // 引用。
var reference = ReadReference(host, field);
        reference.objectReferenceValue = value;
        reference.serializedObject.ApplyModifiedPropertiesWithoutUndo();
        if (reference.objectReferenceValue != value)
        {
            throw new InvalidOperationException("引用类型不兼容：" + field);
        }
    }

    /// <summary>GetIndex 的业务处理入口；本方法直接依赖 Array.IndexOf&lt;GameObject&gt;(GameObject[], GameObject)、List&lt;int&gt;.Add(int)、List&lt;int&gt;.List()、List&lt;int&gt;.Reverse()、string.Join&lt;int&gt;(string, IEnumerable&lt;int&gt;)、Transform.GetSiblingIndex()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)；下游（含内部回调体） Array.IndexOf&lt;GameObject&gt;(GameObject[], GameObject)、List&lt;int&gt;.Add(int)、List&lt;int&gt;.List()、List&lt;int&gt;.Reverse()、string.Join&lt;int&gt;(string, IEnumerable&lt;int&gt;)、Transform.GetSiblingIndex()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rootObject">根游戏对象。</param>
    /// <param name="objectValue">对象值。</param>
    private static string GetIndex(GameObject[] rootObject, Transform objectValue)
    {
        // 当前元素索引。
var index = new List<int>();
        while (objectValue.parent != null)
        {
            index.Add(objectValue.GetSiblingIndex());
            objectValue = objectValue.parent;
        }

        index.Add(Array.IndexOf(rootObject, objectValue.gameObject));
        index.Reverse();
        return string.Join("/", index);
    }

    /// <summary>PressIndexFind 的业务处理入口；本方法直接依赖 IEnumerable&lt;int&gt;.ToArray&lt;int&gt;()、IEnumerable&lt;string&gt;.Select&lt;string, int&gt;(Func&lt;string, int&gt;)、string.Split(char, StringSplitOptions)、Transform.GetChild(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） IEnumerable&lt;int&gt;.ToArray&lt;int&gt;()、IEnumerable&lt;string&gt;.Select&lt;string, int&gt;(Func&lt;string, int&gt;)、string.Split(char, StringSplitOptions)、Transform.GetChild(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="rootObject">根游戏对象。</param>
    /// <param name="indexText">索引文字。</param>
    private static GameObject PressIndexFind(GameObject[] rootObject, string indexText)
    {
        // 当前元素索引。
int[] index = indexText.Split('/').Select(int.Parse).ToArray();
        // 对象值。
Transform objectValue = rootObject[index[0]].transform;
        // 层级。
for (int hierarchy = 1; hierarchy < index.Length; hierarchy++)
        {
            objectValue = objectValue.GetChild(index[hierarchy]);
        }

        return objectValue.gameObject;
    }

    /// <summary>ValidateAsset 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.CalculateSummary(string)、AssetDatabase.AssetPathToGUID(string)、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Validate(ArchitectureAssetMigrationTool.MigrationRecord)；下游（含内部回调体） ArchitectureAssetMigrationTool.CalculateSummary(string)、AssetDatabase.AssetPathToGUID(string)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="record">当前迁移记录。</param>
    /// <param name="expectedSummary">预期摘要。</param>
    private static void ValidateAsset(MigrationRecord record, string expectedSummary)
    {
        if (AssetDatabase.AssetPathToGUID(record.plan.assetPath) != record.assetIdentity || CalculateSummary(record.plan.assetPath) != expectedSummary)
        {
            throw new InvalidOperationException("资源在预览或迁移后发生变化，拒绝覆盖用户修改");
        }
    }

    /// <summary>CalculateSummary 的业务处理入口；本方法直接依赖 BitConverter.ToString(byte[])、File.ReadAllBytes(string)、HashAlgorithm.ComputeHash(byte[])、SHA256.Create()、string.Replace(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） BitConverter.ToString(byte[])、File.ReadAllBytes(string)、HashAlgorithm.ComputeHash(byte[])、SHA256.Create()、string.Replace(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    public static string CalculateSummary(string path)
    {
        // calculateNormal（计算常规）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var calculateNormal = SHA256.Create())
            return BitConverter.ToString(calculateNormal.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    /// <summary>ValidateRecordPath 的业务处理入口；本方法直接依赖 ArgumentException.ArgumentException(string)、Path.GetExtension(string)、Path.GetFullPath(string)、string.StartsWith(string, StringComparison)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） ArgumentException.ArgumentException(string)、Path.GetExtension(string)、Path.GetFullPath(string)、string.StartsWith(string, StringComparison)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    private static void ValidateRecordPath(string path)
    {
        // 根节点目录。
string rootDirectory = Path.GetFullPath("Docs/架构改造/迁移记录") + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path) != ".json")
        {
            throw new ArgumentException("记录必须写入架构改造的迁移记录目录");
        }
    }

    /// <summary>WriteRecord 的业务处理入口；本方法直接依赖 Directory.CreateDirectory(string)、File.WriteAllText(string, string)、JsonUtility.ToJson(object, bool)、Path.GetDirectoryName(string)、Path.GetFullPath(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） Directory.CreateDirectory(string)、File.WriteAllText(string, string)、JsonUtility.ToJson(object, bool)、Path.GetDirectoryName(string)、Path.GetFullPath(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    /// <param name="record">当前迁移记录。</param>
    private static void WriteRecord(string path, MigrationRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, JsonUtility.ToJson(record, true));
    }
}

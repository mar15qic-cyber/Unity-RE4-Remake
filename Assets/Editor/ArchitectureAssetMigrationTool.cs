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
        /// <summary>中文含义：版本。使用位置：ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("版本")]
        [UnityEngine.InspectorName("版本")]
        [Newtonsoft.Json.JsonProperty("版本")]
        public int version = 1;
        /// <summary>中文含义：资源路径。使用位置：作为 ArchitectureAssetMigrationTool.AssetContext.AssetContext(string) 的参数、作为 ArchitectureAssetMigrationTool.CalculateSummary(string) 的参数、作为 AssetDatabase.AssetPathToGUID(string) 的参数 等共 4 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("资源路径")]
        [UnityEngine.InspectorName("资源路径")]
        [Newtonsoft.Json.JsonProperty("资源路径")]
        public string assetPath;
        /// <summary>中文含义：宿主类型。使用位置：作为 ArchitectureAssetMigrationTool.FindType(string) 的参数、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("宿主类型")]
        [UnityEngine.InspectorName("宿主类型")]
        [Newtonsoft.Json.JsonProperty("宿主类型")]
        public string hostType;
        /// <summary>中文含义：新组件类型。使用位置：作为 ArchitectureAssetMigrationTool.FindType(string) 的参数、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新组件类型")]
        [UnityEngine.InspectorName("新组件类型")]
        [Newtonsoft.Json.JsonProperty("新组件类型")]
        public string newComponentType;
        /// <summary>中文含义：引用字段。使用位置：作为 ArchitectureAssetMigrationTool.ReadReference(Component, string) 的参数、作为 ArchitectureAssetMigrationTool.SetReference(Component, string, Component) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数 等共 5 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("引用字段")]
        [UnityEngine.InspectorName("引用字段")]
        [Newtonsoft.Json.JsonProperty("引用字段")]
        public string referenceField;
        /// <summary>中文含义：新组件配置。使用位置：作为 JObject.Parse(string) 的参数、作为 JsonUtility.FromJsonOverwrite(string, object) 的参数、作为 string.IsNullOrWhiteSpace(string) 的参数。</summary>
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
        /// <summary>中文含义：层级索引。使用位置：作为 ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string) 的参数、作为 IEnumerable&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Where&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;(Func&lt;ArchitectureAssetMigrationTool.ObjectChange, int, bool&gt;) 的参数、作为 List&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Add(ArchitectureAssetMigrationTool.ObjectChange) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("层级索引")]
        [UnityEngine.InspectorName("层级索引")]
        [Newtonsoft.Json.JsonProperty("层级索引")]
        public string hierarchyIndex;
        /// <summary>中文含义：对象名称。使用位置：作为 IEnumerable&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Where&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;(Func&lt;ArchitectureAssetMigrationTool.ObjectChange, int, bool&gt;) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 List&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Add(ArchitectureAssetMigrationTool.ObjectChange) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("对象名称")]
        [UnityEngine.InspectorName("对象名称")]
        [Newtonsoft.Json.JsonProperty("对象名称")]
        public string objectName;
        /// <summary>中文含义：原宿主数据。使用位置：作为 List&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Add(ArchitectureAssetMigrationTool.ObjectChange) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("原宿主数据")]
        [UnityEngine.InspectorName("原宿主数据")]
        [Newtonsoft.Json.JsonProperty("原宿主数据")]
        public string originalHostData;
        /// <summary>中文含义：新宿主数据。使用位置：ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新宿主数据")]
        [UnityEngine.InspectorName("新宿主数据")]
        [Newtonsoft.Json.JsonProperty("新宿主数据")]
        public string newHostData;
        /// <summary>中文含义：新组件数据。使用位置：ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新组件数据")]
        [UnityEngine.InspectorName("新组件数据")]
        [Newtonsoft.Json.JsonProperty("新组件数据")]
        public string newComponentData;
        /// <summary>中文含义：是否新增组件。使用位置：作为 IEnumerable&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Where&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;(Func&lt;ArchitectureAssetMigrationTool.ObjectChange, int, bool&gt;) 的参数、作为 List&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Add(ArchitectureAssetMigrationTool.ObjectChange) 的参数、ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string) 等共 4 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新增组件")]
        [UnityEngine.InspectorName("新增组件")]
        [Newtonsoft.Json.JsonProperty("新增组件")]
        public bool newlyAddedComponent;
        /// <summary>中文含义：是否新增绑定。使用位置：作为 IEnumerable&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Where&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;(Func&lt;ArchitectureAssetMigrationTool.ObjectChange, int, bool&gt;) 的参数、作为 List&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Add(ArchitectureAssetMigrationTool.ObjectChange) 的参数、ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string) 等共 4 处。</summary>
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
        /// <summary>中文含义：方案。使用位置：作为 ArchitectureAssetMigrationTool.AssetContext.AssetContext(string) 的参数、作为 ArchitectureAssetMigrationTool.CalculateSummary(string) 的参数、作为 ArchitectureAssetMigrationTool.FindType(string) 的参数 等共 9 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("方案")]
        [UnityEngine.InspectorName("方案")]
        [Newtonsoft.Json.JsonProperty("方案")]
        public MigrationPlan plan;
        /// <summary>中文含义：资源身份。使用位置：ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("资源身份")]
        [UnityEngine.InspectorName("资源身份")]
        [Newtonsoft.Json.JsonProperty("资源身份")]
        public string assetIdentity;
        /// <summary>中文含义：原摘要。使用位置：作为 ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string) 的参数、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("原摘要")]
        [UnityEngine.InspectorName("原摘要")]
        [Newtonsoft.Json.JsonProperty("原摘要")]
        public string originalSummary;
        /// <summary>中文含义：新摘要。使用位置：作为 ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string) 的参数、ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("新摘要")]
        [UnityEngine.InspectorName("新摘要")]
        [Newtonsoft.Json.JsonProperty("新摘要")]
        public string newSummary;
        /// <summary>中文含义：状态。使用位置：ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan) 等共 4 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("状态")]
        [UnityEngine.InspectorName("状态")]
        [Newtonsoft.Json.JsonProperty("状态")]
        public string state;
        /// <summary>中文含义：对象。使用位置：作为 IEnumerable&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;.Where&lt;ArchitectureAssetMigrationTool.ObjectChange&gt;(Func&lt;ArchitectureAssetMigrationTool.ObjectChange, int, bool&gt;) 的参数、ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string) 等共 5 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("对象")]
        [UnityEngine.InspectorName("对象")]
        [Newtonsoft.Json.JsonProperty("对象")]
        public List<ObjectChange> objectValue = new List<ObjectChange>();
    }

    /// <summary>编辑器配置与校验模块 AssetContext；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构资源迁移工具+资源上下文")]
    private sealed class AssetContext : IDisposable
    {
        /// <summary>中文含义：根游戏对象。只读字段，初始化后不由普通写入口替换。使用位置：作为 ArchitectureAssetMigrationTool.GetIndex(GameObject[], Transform) 的参数、作为 ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string) 的参数、ArchitectureAssetMigrationTool.AssetContext.AssetContext(string) 等共 4 处。</summary>
        public readonly GameObject[] rootObject;
        /// <summary>中文含义：路径。只读字段，初始化后不由普通写入口替换。使用位置：作为 EditorSceneManager.SaveScene(Scene, string) 的参数、作为 IOException.IOException(string) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string, out bool) 的参数 等共 4 处。</summary>
        private readonly string path;
        /// <summary>中文含义：预制件。只读字段，初始化后不由普通写入口替换。使用位置：作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string, out bool) 的参数、作为 PrefabUtility.UnloadPrefabContents(GameObject) 的参数、ArchitectureAssetMigrationTool.AssetContext.AssetContext(string) 等共 5 处。</summary>
        private readonly GameObject prefab;
        /// <summary>中文含义：场景。只读字段，初始化后不由普通写入口替换。使用位置：作为 EditorSceneManager.CloseScene(Scene, bool) 的参数、作为 EditorSceneManager.SaveScene(Scene, string) 的参数、ArchitectureAssetMigrationTool.AssetContext.AssetContext(string) 等共 4 处。</summary>
        private readonly Scene scene;
        /// <summary>中文含义：当前是否打开。只读字段，初始化后不由普通写入口替换。使用位置：ArchitectureAssetMigrationTool.AssetContext.AssetContext(string)、ArchitectureAssetMigrationTool.AssetContext.Dispose()。</summary>
        private readonly bool currentOpen;
        /// <summary>中文含义：原始生效场景。只读字段，初始化后不由普通写入口替换。使用位置：作为 SceneManager.SetActiveScene(Scene) 的参数、ArchitectureAssetMigrationTool.AssetContext.AssetContext(string)、ArchitectureAssetMigrationTool.AssetContext.Dispose()。</summary>
        private readonly Scene originalActiveScene;
        /// <summary>构造并初始化 AssetContext 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)；下游（含内部回调体） EditorSceneManager.OpenScene(string, OpenSceneMode)、InvalidOperationException.InvalidOperationException(string)、PrefabUtility.LoadPrefabContents(string)、Scene.GetRootGameObjects()、Scene.IsValid()、SceneManager.GetActiveScene()、SceneManager.GetSceneByPath(string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="path">中文含义：路径。使用位置：作为 EditorSceneManager.OpenScene(string, OpenSceneMode) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 PrefabUtility.LoadPrefabContents(string) 的参数 等共 5 处。</param>
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
    /// <param name="plan">中文含义：方案。使用位置：作为 ArchitectureAssetMigrationTool.AssetContext.AssetContext(string) 的参数、作为 ArchitectureAssetMigrationTool.CalculateSummary(string) 的参数、作为 ArchitectureAssetMigrationTool.FindType(string) 的参数 等共 10 处。</param>
    public static MigrationRecord Preview(MigrationPlan plan)
    {
        ValidatePlan(plan);
        // record（当前迁移记录）：使用位置：ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)。
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
            // hostType（宿主类型）：保存 ArchitectureAssetMigrationTool.FindType(string) 的返回结果，供当前步骤使用。使用位置：作为 Component.GetComponents(Type) 的参数、作为 GameObject.GetComponentsInChildren(Type, bool) 的参数、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)。
var hostType = FindType(plan.hostType);
            // componentType（组件类型）：保存 ArchitectureAssetMigrationTool.FindType(string) 的返回结果，供当前步骤使用。使用位置：作为 Component.GetComponent(Type) 的参数、作为 Component.GetComponents(Type) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数 等共 5 处。
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

                    // hasHasComponent（是否具有组件）：保存 Component.GetComponent(Type) 的返回结果，供当前步骤使用。使用位置：作为 List<ArchitectureAssetMigrationTool.ObjectChange>.Add(ArchitectureAssetMigrationTool.ObjectChange) 的参数、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)。
Component hasHasComponent = host.GetComponent(componentType);
                    if (hasHasComponent == null && componentType.GetCustomAttributes(typeof(RequireComponent), true).Length > 0)
                    {
                        throw new InvalidOperationException("含隐式依赖组件的迁移必须先登记依赖，当前工具拒绝自动添加：" + componentType.Name);
                    }

                    // binding（是否绑定）：使用位置：作为 List<ArchitectureAssetMigrationTool.ObjectChange>.Add(ArchitectureAssetMigrationTool.ObjectChange) 的参数、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)。
bool binding = false;
                    if (!string.IsNullOrEmpty(plan.referenceField))
                    {
                        // fieldInformation（字段信息）：保存 Type.GetField(string) 的返回结果，供当前步骤使用。使用位置：ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)。
var fieldInformation = hostType.GetField(plan.referenceField);
                        if (fieldInformation == null || !fieldInformation.FieldType.IsAssignableFrom(componentType))
                        {
                            throw new InvalidOperationException("引用必须是类型兼容的公开组件字段：" + plan.referenceField);
                        }

                        // reference（引用）：保存 ArchitectureAssetMigrationTool.ReadReference(Component, string) 的返回结果，供当前步骤使用。使用位置：ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)。
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
    /// <param name="record">中文含义：当前迁移记录。使用位置：作为 ArchitectureAssetMigrationTool.AssetContext.AssetContext(string) 的参数、作为 ArchitectureAssetMigrationTool.CalculateSummary(string) 的参数、作为 ArchitectureAssetMigrationTool.FindType(string) 的参数 等共 11 处。</param>
    /// <param name="recordPath">中文含义：记录路径。使用位置：作为 ArchitectureAssetMigrationTool.ValidateRecordPath(string) 的参数、作为 ArchitectureAssetMigrationTool.WriteRecord(string, ArchitectureAssetMigrationTool.MigrationRecord) 的参数、作为 File.Exists(string) 的参数。</param>
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
        // currentPreview（当前预览）：保存 ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan) 的返回结果，供当前步骤使用。使用位置：ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)。
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
            // hasHandle（是否具有句柄）：使用位置：ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)。
var hasHandle = new List<Tuple<Component, Component, ObjectChange>>();
            try
            {
                // item（当前对象变化）：每轮从记录的对象值取得一个元素，仅代表当前这一项。使用位置：作为 ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 Tuple.Create<Component, Component, ArchitectureAssetMigrationTool.ObjectChange>(Component, Component, ArchitectureAssetMigrationTool.ObjectChange) 的参数 等共 4 处。
foreach (var item in record.objectValue)
                {
                    // objectValue（对象值）：保存 ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string) 的返回结果，供当前步骤使用。使用位置：ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)。
var objectValue = PressIndexFind(asset.rootObject, item.hierarchyIndex);
                    // host（所属组件）：保存 GameObject.GetComponent(Type) 的返回结果，供当前步骤使用。使用位置：作为 ArchitectureAssetMigrationTool.SetReference(Component, string, Component) 的参数、作为 EditorJsonUtility.ToJson(object) 的参数、作为 EditorUtility.SetDirty(Object) 的参数 等共 4 处。
var host = objectValue.GetComponent(FindType(record.plan.hostType));
                    // componentType（组件类型）：保存 ArchitectureAssetMigrationTool.FindType(string) 的返回结果，供当前步骤使用。使用位置：作为 GameObject.AddComponent(Type) 的参数、作为 GameObject.GetComponent(Type) 的参数。
var componentType = FindType(record.plan.newComponentType);
                    // component（组件）：使用位置：作为 ArchitectureAssetMigrationTool.SetReference(Component, string, Component) 的参数、作为 EditorJsonUtility.ToJson(object) 的参数、作为 EditorUtility.SetDirty(Object) 的参数 等共 6 处。
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
                // index（当前元素索引）：使用位置：作为 List<Tuple<Component, Component, ArchitectureAssetMigrationTool.ObjectChange>>.this[int] 的参数、ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)。
for (int index = hasHandle.Count - 1; index >= 0; index--)
                {
                    // item（当前元组）：使用位置：作为 ArchitectureAssetMigrationTool.SetReference(Component, string, Component) 的参数、作为 Object.DestroyImmediate(Object) 的参数、ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)。
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
    /// <param name="record">中文含义：当前迁移记录。使用位置：作为 ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan) 的参数、作为 ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string) 的参数、作为 ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan) 的参数 等共 4 处。</param>
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
    /// <param name="record">中文含义：当前迁移记录。使用位置：作为 ArchitectureAssetMigrationTool.AssetContext.AssetContext(string) 的参数、作为 ArchitectureAssetMigrationTool.CalculateSummary(string) 的参数、作为 ArchitectureAssetMigrationTool.FindType(string) 的参数 等共 8 处。</param>
    /// <param name="recordPath">中文含义：记录路径。使用位置：作为 ArchitectureAssetMigrationTool.ValidateRecordPath(string) 的参数、作为 ArchitectureAssetMigrationTool.WriteRecord(string, ArchitectureAssetMigrationTool.MigrationRecord) 的参数。</param>
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
            // target（目标游戏对象数组）：保存 IEnumerable<GameObject>.ToArray<GameObject>() 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)。
var target = record.objectValue.Select( /* 中文含义：当前对象变化。 */item => PressIndexFind(asset.rootObject, item.hierarchyIndex)).ToArray();
            // index（当前元素索引）：使用位置：作为 List<ArchitectureAssetMigrationTool.ObjectChange>.this[int] 的参数、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)。
for (int index = record.objectValue.Count - 1; index >= 0; index--)
            {
                // item（当前对象变化）：使用位置：ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)。
var item = record.objectValue[index];
                // host（所属组件）：保存 GameObject.GetComponent(Type) 的返回结果，供当前步骤使用。使用位置：作为 ArchitectureAssetMigrationTool.SetReference(Component, string, Component) 的参数。
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
    /// <param name="plan">中文含义：方案。使用位置：作为 ArchitectureAssetMigrationTool.FindType(string) 的参数、作为 JObject.Parse(string) 的参数、作为 string.IsNullOrWhiteSpace(string) 的参数 等共 4 处。</param>
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

        // receiverValue2（判空前缓存的方案的资源路径）：从方案的资源路径读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)。
string receiverValue2 = plan.assetPath;
        // path（路径）：使用位置：作为 AssetDatabase.AssetPathToGUID(string) 的参数、作为 File.Exists(string) 的参数、作为 string.IsNullOrEmpty(string) 的参数 等共 4 处。
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
            // type（反射操作使用的类型信息）：保存 ArchitectureAssetMigrationTool.FindType(string) 的返回结果，供当前步骤使用。使用位置：作为 Type.IsAssignableFrom(Type) 的参数、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)。
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

        // configuration（配置）：保存 JObject.Parse(string) 的返回结果，供当前步骤使用。使用位置：ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)。
var configuration = Newtonsoft.Json.Linq.JObject.Parse(plan.newComponentConfiguration);
        // newType（新类型）：保存 ArchitectureAssetMigrationTool.FindType(string) 的返回结果，供当前步骤使用。使用位置：ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)。
var newType = FindType(plan.newComponentType);
        // property（属性）：每轮从属性集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 ArgumentException.ArgumentException(string) 的参数、作为 Type.GetField(string) 的参数。
foreach (var property in configuration.Properties())
        {
            // field（字段）：保存 Type.GetField(string) 的返回结果，供当前步骤使用。使用位置：作为 Type.IsAssignableFrom(Type) 的参数、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)。
var field = newType.GetField(property.Name);
            if (field == null || field.IsStatic || field.IsInitOnly || field.IsNotSerialized || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
            {
                throw new ArgumentException("配置只能写新增组件的公开数据字段；对象引用必须显式绑定：" + property.Name);
            }
        }
    }

    /// <summary>FindType 的业务处理入口；本方法直接依赖 AppDomain.GetAssemblies()、ArgumentException.ArgumentException(string)、Assembly.GetType(string, bool)、IEnumerable&lt;Assembly&gt;.Select&lt;Assembly, Type&gt;(Func&lt;Assembly, Type&gt;)、IEnumerable&lt;Type&gt;.FirstOrDefault&lt;Type&gt;(Func&lt;Type, bool&gt;)、object.ReferenceEquals(object, object)、Type.GetType(string, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.ValidatePlan(ArchitectureAssetMigrationTool.MigrationPlan)；下游（含内部回调体） AppDomain.GetAssemblies()、ArgumentException.ArgumentException(string)、Assembly.GetType(string, bool)、IEnumerable&lt;Assembly&gt;.Select&lt;Assembly, Type&gt;(Func&lt;Assembly, Type&gt;)、IEnumerable&lt;Type&gt;.FirstOrDefault&lt;Type&gt;(Func&lt;Type, bool&gt;)、object.ReferenceEquals(object, object)、Type.GetType(string, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：作为 ArgumentException.ArgumentException(string) 的参数、作为 IEnumerable&lt;Assembly&gt;.Select&lt;Assembly, Type&gt;(Func&lt;Assembly, Type&gt;) 的参数、作为 Type.GetType(string, bool) 的参数。</param>
    private static Type FindType(string name)
    {
        // availableValue4（空值判断前缓存的取得类型的返回结果）：从取得类型的返回结果读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ArchitectureAssetMigrationTool.FindType(string)。
global::System.Type availableValue4 = Type.GetType(name, false);
        if (!global::System.Object.ReferenceEquals(availableValue4, null))
        {
            return availableValue4;
        }
        else
        {
            // availableValue8（空值判断前缓存的反射类型）：保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ArchitectureAssetMigrationTool.FindType(string)。
global::System.Type availableValue8 = AppDomain.CurrentDomain.GetAssemblies().Select( /* 中文含义：程序集。 */assembly =>
            {
                // callReceiver7（调用前缓存的程序集）：从程序集读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 IEnumerable<Assembly>.Select<Assembly, Type>(Func<Assembly, Type>) 的参数。
global::System.Reflection.Assembly callReceiver7 = assembly;
                // availableValue5（空值判断前缓存的名称）：从名称读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 IEnumerable<Assembly>.Select<Assembly, Type>(Func<Assembly, Type>) 的参数、作为 object.ReferenceEquals(object, object) 的参数。
string availableValue5 = name;
                // selectedValue6（分支选出的可用值5）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Assembly.GetType(string, bool) 的参数、作为 IEnumerable<Assembly>.Select<Assembly, Type>(Func<Assembly, Type>) 的参数。
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
    /// <param name="host">中文含义：所属组件。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 SerializedObject.SerializedObject(Object) 的参数。</param>
    /// <param name="field">中文含义：字段。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 SerializedObject.FindProperty(string) 的参数。</param>
    private static SerializedProperty ReadReference(Component host, string field)
    {
        // data（当前序列化的对象）：使用位置：ArchitectureAssetMigrationTool.ReadReference(Component, string)。
var data = new SerializedObject(host);
        // reference（引用）：保存 SerializedObject.FindProperty(string) 的返回结果，供当前步骤使用。使用位置：ArchitectureAssetMigrationTool.ReadReference(Component, string)。
var reference = data.FindProperty(field);
        if (reference == null || reference.propertyType != SerializedPropertyType.ObjectReference)
        {
            throw new InvalidOperationException("组件缺少可序列化引用字段：" + host.name + "." + field);
        }

        return reference;
    }

    /// <summary>SetReference 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.ReadReference(Component, string)、InvalidOperationException.InvalidOperationException(string)、SerializedObject.ApplyModifiedPropertiesWithoutUndo()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） ArchitectureAssetMigrationTool.ReadReference(Component, string)、InvalidOperationException.InvalidOperationException(string)、SerializedObject.ApplyModifiedPropertiesWithoutUndo()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="host">中文含义：所属组件。使用位置：作为 ArchitectureAssetMigrationTool.ReadReference(Component, string) 的参数。</param>
    /// <param name="field">中文含义：字段。使用位置：作为 ArchitectureAssetMigrationTool.ReadReference(Component, string) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。</param>
    /// <param name="value">中文含义：当前组件。使用位置：ArchitectureAssetMigrationTool.SetReference(Component, string, Component)。</param>
    private static void SetReference(Component host, string field, Component value)
    {
        // reference（引用）：保存 ArchitectureAssetMigrationTool.ReadReference(Component, string) 的返回结果，供当前步骤使用。使用位置：ArchitectureAssetMigrationTool.SetReference(Component, string, Component)。
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
    /// <param name="rootObject">中文含义：根游戏对象。使用位置：作为 Array.IndexOf&lt;GameObject&gt;(GameObject[], GameObject) 的参数。</param>
    /// <param name="objectValue">中文含义：对象值。使用位置：作为 Array.IndexOf&lt;GameObject&gt;(GameObject[], GameObject) 的参数、作为 List&lt;int&gt;.Add(int) 的参数、ArchitectureAssetMigrationTool.GetIndex(GameObject[], Transform)。</param>
    private static string GetIndex(GameObject[] rootObject, Transform objectValue)
    {
        // index（当前元素索引）：使用位置：作为 string.Join<int>(string, IEnumerable<int>) 的参数、ArchitectureAssetMigrationTool.GetIndex(GameObject[], Transform)。
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
    /// <param name="rootObject">中文含义：根游戏对象。使用位置：ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)。</param>
    /// <param name="indexText">中文含义：索引文字。使用位置：ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)。</param>
    private static GameObject PressIndexFind(GameObject[] rootObject, string indexText)
    {
        // index（当前元素索引）：保存 IEnumerable<int>.ToArray<int>() 的返回结果，供当前步骤使用。使用位置：作为 Transform.GetChild(int) 的参数、ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)。
int[] index = indexText.Split('/').Select(int.Parse).ToArray();
        // objectValue（对象值）：使用位置：ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)。
Transform objectValue = rootObject[index[0]].transform;
        // hierarchy（层级）：使用位置：ArchitectureAssetMigrationTool.PressIndexFind(GameObject[], string)。
for (int hierarchy = 1; hierarchy < index.Length; hierarchy++)
        {
            objectValue = objectValue.GetChild(index[hierarchy]);
        }

        return objectValue.gameObject;
    }

    /// <summary>ValidateAsset 的业务处理入口；本方法直接依赖 ArchitectureAssetMigrationTool.CalculateSummary(string)、AssetDatabase.AssetPathToGUID(string)、InvalidOperationException.InvalidOperationException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Validate(ArchitectureAssetMigrationTool.MigrationRecord)；下游（含内部回调体） ArchitectureAssetMigrationTool.CalculateSummary(string)、AssetDatabase.AssetPathToGUID(string)、InvalidOperationException.InvalidOperationException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="record">中文含义：当前迁移记录。使用位置：作为 ArchitectureAssetMigrationTool.CalculateSummary(string) 的参数、作为 AssetDatabase.AssetPathToGUID(string) 的参数、ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string)。</param>
    /// <param name="expectedSummary">中文含义：预期摘要。使用位置：ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string)。</param>
    private static void ValidateAsset(MigrationRecord record, string expectedSummary)
    {
        if (AssetDatabase.AssetPathToGUID(record.plan.assetPath) != record.assetIdentity || CalculateSummary(record.plan.assetPath) != expectedSummary)
        {
            throw new InvalidOperationException("资源在预览或迁移后发生变化，拒绝覆盖用户修改");
        }
    }

    /// <summary>CalculateSummary 的业务处理入口；本方法直接依赖 BitConverter.ToString(byte[])、File.ReadAllBytes(string)、HashAlgorithm.ComputeHash(byte[])、SHA256.Create()、string.Replace(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Preview(ArchitectureAssetMigrationTool.MigrationPlan)、ArchitectureAssetMigrationTool.ValidateAsset(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） BitConverter.ToString(byte[])、File.ReadAllBytes(string)、HashAlgorithm.ComputeHash(byte[])、SHA256.Create()、string.Replace(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">中文含义：路径。使用位置：作为 File.ReadAllBytes(string) 的参数。</param>
    public static string CalculateSummary(string path)
    {
        // calculateNormal（计算常规）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var calculateNormal = SHA256.Create())
            return BitConverter.ToString(calculateNormal.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    /// <summary>ValidateRecordPath 的业务处理入口；本方法直接依赖 ArgumentException.ArgumentException(string)、Path.GetExtension(string)、Path.GetFullPath(string)、string.StartsWith(string, StringComparison)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） ArgumentException.ArgumentException(string)、Path.GetExtension(string)、Path.GetFullPath(string)、string.StartsWith(string, StringComparison)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">中文含义：路径。使用位置：作为 Path.GetExtension(string) 的参数、作为 Path.GetFullPath(string) 的参数。</param>
    private static void ValidateRecordPath(string path)
    {
        // rootDirectory（根节点目录）：使用位置：作为 string.StartsWith(string, StringComparison) 的参数。
string rootDirectory = Path.GetFullPath("Docs/架构改造/迁移记录") + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(path).StartsWith(rootDirectory, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path) != ".json")
        {
            throw new ArgumentException("记录必须写入架构改造的迁移记录目录");
        }
    }

    /// <summary>WriteRecord 的业务处理入口；本方法直接依赖 Directory.CreateDirectory(string)、File.WriteAllText(string, string)、JsonUtility.ToJson(object, bool)、Path.GetDirectoryName(string)、Path.GetFullPath(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureAssetMigrationTool.Apply(ArchitectureAssetMigrationTool.MigrationRecord, string)、ArchitectureAssetMigrationTool.Fallback(ArchitectureAssetMigrationTool.MigrationRecord, string)；下游（含内部回调体） Directory.CreateDirectory(string)、File.WriteAllText(string, string)、JsonUtility.ToJson(object, bool)、Path.GetDirectoryName(string)、Path.GetFullPath(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">中文含义：路径。使用位置：作为 File.WriteAllText(string, string) 的参数、作为 Path.GetFullPath(string) 的参数。</param>
    /// <param name="record">中文含义：当前迁移记录。使用位置：作为 JsonUtility.ToJson(object, bool) 的参数。</param>
    private static void WriteRecord(string path, MigrationRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, JsonUtility.ToJson(record, true));
    }
}

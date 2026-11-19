using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>保存命名迁移前的序列化字段，通过编辑器接口迁移并核对引用。</summary>
public static class CodeIdentifierAssetMigration
{
    /// <summary>中文含义：目录路径。编译期常量，保持既有值与消费者映射。使用位置：作为 File.Copy(string, string, bool) 的参数、作为 File.ReadAllText(string) 的参数、作为 File.WriteAllText(string, string) 的参数。</summary>
    private const string DirectoryPath = "Library/CodeIdentifierMigration";
    /// <summary>中文含义：字段标志集合。编译期常量，保持既有值与消费者映射。使用位置：作为 Type.GetField(string, BindingFlags) 的参数。</summary>
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>中文含义：生效字段映射集合。使用位置：作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、CodeIdentifierAssetMigration.RestoreAndVerify()。</summary>
    private static JObject activeFieldMaps;
    /// <summary>编辑器配置与校验模块 PropertyRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    private sealed class PropertyRecord
    {
        /// <summary>中文含义：路径。使用位置：作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、作为 IEnumerable&lt;CodeIdentifierAssetMigration.PropertyRecord&gt;.Select&lt;CodeIdentifierAssetMigration.PropertyRecord, string&gt;(Func&lt;CodeIdentifierAssetMigration.PropertyRecord, string&gt;) 的参数、作为 IEnumerable&lt;string&gt;.Any&lt;string&gt;(Func&lt;string, bool&gt;) 的参数 等共 5 处。</summary>
        public string path;
        /// <summary>中文含义：类型。使用位置：作为 IEnumerable&lt;CodeIdentifierAssetMigration.PropertyRecord&gt;.Where&lt;CodeIdentifierAssetMigration.PropertyRecord&gt;(Func&lt;CodeIdentifierAssetMigration.PropertyRecord, bool&gt;) 的参数、作为 List&lt;CodeIdentifierAssetMigration.PropertyRecord&gt;.Add(CodeIdentifierAssetMigration.PropertyRecord) 的参数。</summary>
        public int type;
        /// <summary>中文含义：当前次级索引凭证。使用位置：作为 CodeIdentifierAssetMigration.Equivalent(JToken, JToken) 的参数、作为 CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken) 的参数、作为 List&lt;CodeIdentifierAssetMigration.PropertyRecord&gt;.Add(CodeIdentifierAssetMigration.PropertyRecord) 的参数。</summary>
        public JToken value;
    }

    /// <summary>编辑器配置与校验模块 ObjectRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    private sealed class ObjectRecord
    {
        /// <summary>中文含义：身份标识。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 Dictionary&lt;string, Object&gt;.TryGetValue(string, out Object) 的参数、作为 List&lt;string&gt;.Add(string) 的参数。</summary>
        public string identity;
        /// <summary>中文含义：实例编号。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数。</summary>
        public int instanceId;
        /// <summary>中文含义：类型名称。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 List&lt;string&gt;.Add(string) 的参数。</summary>
        public string typeName;
        /// <summary>中文含义：属性集合。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 IEnumerable&lt;CodeIdentifierAssetMigration.ObjectRecord&gt;.Sum&lt;CodeIdentifierAssetMigration.ObjectRecord&gt;(Func&lt;CodeIdentifierAssetMigration.ObjectRecord, int&gt;) 的参数。</summary>
        public List<PropertyRecord> properties = new List<PropertyRecord>();
    }

    /// <summary>编辑器配置与校验模块 AssetRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    private sealed class AssetRecord
    {
        /// <summary>中文含义：路径。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 List&lt;string&gt;.Add(string) 的参数、CodeIdentifierAssetMigration.Capture()。</summary>
        public string path;
        /// <summary>中文含义：之前是否未保存改动。使用位置：CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord)。</summary>
        public bool wasDirty;
        /// <summary>中文含义：对象集合。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 IEnumerable&lt;CodeIdentifierAssetMigration.AssetRecord&gt;.Sum&lt;CodeIdentifierAssetMigration.AssetRecord&gt;(Func&lt;CodeIdentifierAssetMigration.AssetRecord, int&gt;) 的参数、CodeIdentifierAssetMigration.Capture()。</summary>
        public List<ObjectRecord> objects = new List<ObjectRecord>();
    }

    /// <summary>Capture 的业务处理入口；本方法直接依赖 AssetDatabase.FindAssets(string, string[])、AssetDatabase.GetDependencies(string, bool)、CodeIdentifierAssetMigration.AssetRecord.AssetRecord()、CodeIdentifierAssetMigration.ObjectRecord.ObjectRecord()、CodeIdentifierAssetMigration.PropertyRecord.PropertyRecord()、CodeIdentifierAssetMigration.ReadValue(SerializedProperty)、CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord) 等共 33 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.FindAssets(string, string[])、AssetDatabase.GetDependencies(string, bool)、CodeIdentifierAssetMigration.AssetRecord.AssetRecord()、CodeIdentifierAssetMigration.ObjectRecord.ObjectRecord()、CodeIdentifierAssetMigration.PropertyRecord.PropertyRecord()、CodeIdentifierAssetMigration.ReadValue(SerializedProperty)、CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord) 等共 33 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请先停止播放再保存迁移基线。");
        }

        // scriptPaths（脚本路径集合）：使用位置：作为 IEnumerable<string>.Any<string>(Func<string, bool>) 的参数。
HashSet<string> scriptPaths = new HashSet<string>(AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts", "Assets/Editor" }).Select(AssetDatabase.GUIDToAssetPath));
        // records（记录集合）：使用位置：作为 JsonConvert.SerializeObject(object?, Formatting) 的参数、CodeIdentifierAssetMigration.Capture()。
List<AssetRecord> records = new List<AssetRecord>();
        // extension（扩展名）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Directory.EnumerateFiles(string, string, SearchOption) 的参数。
foreach (string extension in new[]
        {
            "*.prefab",
            "*.asset",
            "*.unity"
        }

        )
        {
            // filename（文件名）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：CodeIdentifierAssetMigration.Capture()。
foreach (string filename in System.IO.Directory.EnumerateFiles("Assets", extension, SearchOption.AllDirectories))
            {
                // path（路径）：保存 string.Replace(char, char) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.GetDependencies(string, bool) 的参数、作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 File.Copy(string, string, bool) 的参数 等共 5 处。
string path = filename.Replace('\\', '/');
                // 正式场景使用二进制序列化，必须依据资源依赖图筛选。
                // referencesOwnedScript（是否引用集合持有的脚本）：保存 IEnumerable<string>.Any<string>(Func<string, bool>) 的返回结果，供当前步骤使用。使用位置：CodeIdentifierAssetMigration.Capture()。
bool referencesOwnedScript = AssetDatabase.GetDependencies(path, true).Any(scriptPaths.Contains);
                if (!referencesOwnedScript)
                {
                    continue;
                }

                // record（当前资源记录）：使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 List<CodeIdentifierAssetMigration.AssetRecord>.Add(CodeIdentifierAssetMigration.AssetRecord) 的参数、CodeIdentifierAssetMigration.Capture()。
AssetRecord record = new AssetRecord
                {
                    path = path
                };
                VisitAsset(path, /* 中文含义：对象集合。 */ objects =>
                {
                    // target（目标对象）：每轮从对象集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 GlobalObjectId.GetGlobalObjectIdSlow(Object) 的参数、作为 SerializedObject.SerializedObject(Object) 的参数。
foreach (Object target in objects)
                    {
                        // serialized（序列化的）：使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数。
SerializedObject serialized = new SerializedObject(target);
                        // property（属性）：保存 SerializedObject.GetIterator() 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.ReadValue(SerializedProperty) 的参数、作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 List<CodeIdentifierAssetMigration.PropertyRecord>.Add(CodeIdentifierAssetMigration.PropertyRecord) 的参数。
SerializedProperty property = serialized.GetIterator();
                        // item（当前对象记录）：使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 List<CodeIdentifierAssetMigration.ObjectRecord>.Add(CodeIdentifierAssetMigration.ObjectRecord) 的参数。
ObjectRecord item = new ObjectRecord
                        {
                            identity = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString(),
                            instanceId = target.GetInstanceID(),
                            typeName = target.GetType().FullName
                        };
                        // enterChildren（是否进入子对象集合）：使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 SerializedProperty.Next(bool) 的参数。
bool enterChildren = true;
                        while (property.Next(enterChildren))
                        {
                            enterChildren = property.propertyType != SerializedPropertyType.ObjectReference;
                            if (!property.propertyPath.Any( /* 中文含义：字符。 */character => character > 127))
                            {
                                continue;
                            }

                            // value（当前次级索引凭证）：保存 CodeIdentifierAssetMigration.ReadValue(SerializedProperty) 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 List<CodeIdentifierAssetMigration.PropertyRecord>.Add(CodeIdentifierAssetMigration.PropertyRecord) 的参数。
JToken value = ReadValue(property);
                            if (value != null)
                            {
                                item.properties.Add(new PropertyRecord { path = property.propertyPath, type = (int)property.propertyType, value = value });
                            }
                        }

                        if (item.properties.Count > 0)
                        {
                            record.objects.Add(item);
                        }
                    }
                }, false, record);
                if (record.objects.Count > 0)
                {
                    records.Add(record);
                    // backup（备份）：使用位置：作为 File.Copy(string, string, bool) 的参数、作为 Path.GetDirectoryName(string) 的参数。
string backup = "Library/IdentifierMigrationBaseline/" + path;
                    System.IO.Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    File.Copy(path, backup, true);
                    if (File.Exists(path + ".meta"))
                    {
                        File.Copy(path + ".meta", backup + ".meta", true);
                    }
                }
            }
        }

        File.WriteAllText(DirectoryPath + "/asset_snapshot.json", JsonConvert.SerializeObject(records, Formatting.Indented));
        return "已保存资源基线：" + records.Count + "项；对象：" + records.Sum( /* 中文含义：当前资源记录。 */item => item.objects.Count) + "；字段：" + records.Sum( /* 中文含义：当前资源记录。 */item => item.objects.Sum( /* 中文含义：目标对象记录。 */target => target.properties.Count));
    }

    /// <summary>VisitAsset 的业务处理入口；本方法直接依赖 Action&lt;List&lt;Object&gt; &gt;.Invoke(List&lt;Object&gt;)、AssetDatabase.LoadAllAssetsAtPath(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、CodeIdentifierAssetMigration.MigrateOverrides(GameObject)、EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.OpenScene(string, OpenSceneMode) 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CodeIdentifierAssetMigration.Capture()、CodeIdentifierAssetMigration.RestoreAndVerify()；下游（含内部回调体） Action&lt;List&lt;Object&gt; &gt;.Invoke(List&lt;Object&gt;)、AssetDatabase.LoadAllAssetsAtPath(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、CodeIdentifierAssetMigration.MigrateOverrides(GameObject)、EditorSceneManager.CloseScene(Scene, bool)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.OpenScene(string, OpenSceneMode) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">中文含义：路径。使用位置：作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数、作为 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string) 的参数、作为 EditorSceneManager.OpenScene(string, OpenSceneMode) 的参数 等共 5 处。</param>
    /// <param name="action">中文含义：动作。使用位置：CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord)。</param>
    /// <param name="save">中文含义：是否保存。使用位置：CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord)。</param>
    /// <param name="record">中文含义：当前资源记录。使用位置：CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord)。</param>
    private static void VisitAsset(string path, Action<List<Object>> action, bool save, AssetRecord record)
    {
        // objects（对象集合）：使用位置：作为 Action<List<Object>>.Invoke(List<Object>) 的参数、CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord)。
List<Object> objects = new List<Object>();
        if (path.EndsWith(".unity", StringComparison.Ordinal))
        {
            // scene（场景）：保存 SceneManager.GetSceneByPath(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.CloseScene(Scene, bool) 的参数、作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 EditorSceneManager.SaveScene(Scene) 的参数 等共 4 处。
Scene scene = SceneManager.GetSceneByPath(path);
            // alreadyLoaded（是否已经已加载）：使用位置：CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord)。
bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            // wasDirty（之前是否未保存改动）：使用位置：CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord)。
bool wasDirty = scene.isDirty;
            if (!save)
            {
                record.wasDirty = wasDirty;
            }

            try
            {
                // root（根游戏对象）：每轮从取得根节点游戏对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.MigrateOverrides(GameObject) 的参数、作为 List<Object>.AddRange(IEnumerable<Object>) 的参数。
foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (save)
                    {
                        MigrateOverrides(root);
                    }

                    objects.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true).Where( /* 中文含义：当前运行组件。 */item => item != null));
                }

                action(objects);
                if (save && !record.wasDirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                if (!alreadyLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            return;
        }

        // prefab（预制件）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.MigrateOverrides(GameObject) 的参数、作为 List<Object>.AddRange(IEnumerable<Object>) 的参数、CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord)。
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            if (save)
            {
                MigrateOverrides(prefab);
            }

            objects.AddRange(prefab.GetComponentsInChildren<MonoBehaviour>(true).Where( /* 中文含义：当前运行组件。 */item => item != null));
        }
        else
        {
            objects.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).Where( /* 中文含义：当前对象。 */item => item is ScriptableObject));
        }

        action(objects);
    }

    /// <summary>ReadValue 的业务处理入口；本方法直接依赖 GlobalObjectId.GetGlobalObjectIdSlow(Object)、GlobalObjectId.ToString()、IEnumerable&lt;Keyframe&gt;.Select&lt;Keyframe, JArray&gt;(Func&lt;Keyframe, JArray&gt;)、InvalidOperationException.InvalidOperationException(string)、JArray.JArray(object)、JArray.JArray(params object[])、JObject.JObject() 等共 13 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CodeIdentifierAssetMigration.Capture()、CodeIdentifierAssetMigration.RestoreAndVerify()；下游（含内部回调体） GlobalObjectId.GetGlobalObjectIdSlow(Object)、GlobalObjectId.ToString()、IEnumerable&lt;Keyframe&gt;.Select&lt;Keyframe, JArray&gt;(Func&lt;Keyframe, JArray&gt;)、InvalidOperationException.InvalidOperationException(string)、JArray.JArray(object)、JArray.JArray(params object[])、JObject.JObject() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="property">中文含义：属性。使用位置：作为 GlobalObjectId.GetGlobalObjectIdSlow(Object) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 JValue.JValue(bool) 的参数 等共 7 处。</param>
    private static JToken ReadValue(SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.Character:
                return new JValue(property.longValue);
            case SerializedPropertyType.Boolean:
                return new JValue(property.boolValue);
            case SerializedPropertyType.Float:
                return new JValue(property.doubleValue);
            case SerializedPropertyType.String:
                return new JValue(property.stringValue);
            case SerializedPropertyType.ObjectReference:
                if (property.objectReferenceValue == null)
                {
                    return JValue.CreateNull();
                }

                return new JObject
                {
                    ["identity"] = GlobalObjectId.GetGlobalObjectIdSlow(property.objectReferenceValue).ToString(),
                    ["instanceId"] = property.objectReferenceValue.GetInstanceID()
                };
            case SerializedPropertyType.Color:
                // color（颜色）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Color color = property.colorValue;
                return new JArray(color.r, color.g, color.b, color.a);
            case SerializedPropertyType.Vector2:
                // vector2（向量2）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Vector2 vector2 = property.vector2Value;
                return new JArray(vector2.x, vector2.y);
            case SerializedPropertyType.Vector3:
                // vector3（向量3）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Vector3 vector3 = property.vector3Value;
                return new JArray(vector3.x, vector3.y, vector3.z);
            case SerializedPropertyType.Vector4:
                // vector4（向量4）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Vector4 vector4 = property.vector4Value;
                return new JArray(vector4.x, vector4.y, vector4.z, vector4.w);
            case SerializedPropertyType.Quaternion:
                // rotation（旋转）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Quaternion rotation = property.quaternionValue;
                return new JArray(rotation.x, rotation.y, rotation.z, rotation.w);
            case SerializedPropertyType.Rect:
                // rect（矩形）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Rect rect = property.rectValue;
                return new JArray(rect.x, rect.y, rect.width, rect.height);
            case SerializedPropertyType.Bounds:
                // bounds（包围盒）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Bounds bounds = property.boundsValue;
                return new JArray(bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, bounds.size.y, bounds.size.z);
            case SerializedPropertyType.Vector2Int:
                // integer2（整数2）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Vector2Int integer2 = property.vector2IntValue;
                return new JArray(integer2.x, integer2.y);
            case SerializedPropertyType.Vector3Int:
                // integer3（整数3）：使用位置：作为 JArray.JArray(params object[]) 的参数。
Vector3Int integer3 = property.vector3IntValue;
                return new JArray(integer3.x, integer3.y, integer3.z);
            case SerializedPropertyType.AnimationCurve:
                // curve（曲线）：使用位置：作为 JArray.JArray(object) 的参数、CodeIdentifierAssetMigration.ReadValue(SerializedProperty)。
AnimationCurve curve = property.animationCurveValue;
                return new JObject
                {
                    ["pre"] = (int)curve.preWrapMode,
                    ["post"] = (int)curve.postWrapMode,
                    ["keys"] = new JArray(curve.keys.Select( /* 中文含义：键。 */key => new JArray(key.time, key.value, key.inTangent, key.outTangent, key.inWeight, key.outWeight, (int)key.weightedMode)))
                };
            case SerializedPropertyType.ManagedReference:
                throw new InvalidOperationException("发现需要专项迁移的多态序列化字段：" + property.propertyPath);
            default:
                return null;
        }
    }

    /// <summary>MapPath 的业务处理入口；本方法直接依赖 string.Join(string, params string[])、string.Split(char, StringSplitOptions)、Type.GetElementType()、Type.GetField(string, BindingFlags)、Type.GetGenericArguments()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CodeIdentifierAssetMigration.MigrateOverrides(GameObject)、CodeIdentifierAssetMigration.RestoreAndVerify()；下游（含内部回调体） string.Join(string, params string[])、string.Split(char, StringSplitOptions)、Type.GetElementType()、Type.GetField(string, BindingFlags)、Type.GetGenericArguments()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetType">中文含义：目标类型。使用位置：CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。</param>
    /// <param name="original">中文含义：原始。使用位置：CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。</param>
    /// <param name="fieldMaps">中文含义：字段映射集合。使用位置：CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。</param>
    private static string MapPath(Type targetType, string original, JObject fieldMaps)
    {
        // parts（部件集合）：保存 string.Split(char, StringSplitOptions) 的返回结果，供当前步骤使用。使用位置：作为 string.Join(string, params string[]) 的参数、CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。
string[] parts = original.Split('.');
        // current（当前反射类型）：使用位置：CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。
Type current = targetType;
        // index（当前元素索引）：使用位置：CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。
for (int index = 0; index < parts.Length; index++)
        {
            if (parts[index] == "Array")
            {
                if (current.IsArray)
                {
                    current = current.GetElementType();
                }
                else if (current.IsGenericType)
                {
                    current = current.GetGenericArguments()[0];
                }

                index++;
                continue;
            }

            // declaring（声明的）：使用位置：作为 JObject.this[string] 的参数、CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。
Type declaring = current;
            // field（字段）：使用位置：CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。
FieldInfo field = null;
            while (declaring != null)
            {
                // map（映射）：使用位置：CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。
JObject map = fieldMaps[declaring.FullName] as JObject;
                // name（名称）：使用位置：作为 JObject.this[string] 的参数、作为 Type.GetField(string, BindingFlags) 的参数、CodeIdentifierAssetMigration.MapPath(Type, string, JObject)。
string name = parts[index];
                if (map != null && map[name] != null)
                {
                    name = (string)map[name];
                }

                field = declaring.GetField(name, FieldFlags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    parts[index] = name;
                    current = field.FieldType;
                    break;
                }

                declaring = declaring.BaseType;
            }
        }

        return string.Join(".", parts);
    }

    /// <summary>RestoreAndVerify 的业务处理入口；本方法直接依赖 AssetDatabase.SaveAssets()、CodeIdentifierAssetMigration.Equivalent(JToken, JToken)、CodeIdentifierAssetMigration.MapPath(Type, string, JObject)、CodeIdentifierAssetMigration.ReadValue(SerializedProperty)、CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord)、CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)、Dictionary&lt;string, Object&gt;.TryGetValue(string, out Object) 等共 30 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.SaveAssets()、CodeIdentifierAssetMigration.Equivalent(JToken, JToken)、CodeIdentifierAssetMigration.MapPath(Type, string, JObject)、CodeIdentifierAssetMigration.ReadValue(SerializedProperty)、CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord)、CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)、Dictionary&lt;string, Object&gt;.TryGetValue(string, out Object) 等共 30 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string RestoreAndVerify()
    {
        // records（记录集合）：保存 JsonConvert.DeserializeObject<List<CodeIdentifierAssetMigration.AssetRecord>>(string) 的返回结果，供当前步骤使用。使用位置：CodeIdentifierAssetMigration.RestoreAndVerify()。
List<AssetRecord> records = JsonConvert.DeserializeObject<List<AssetRecord>>(File.ReadAllText(DirectoryPath + "/asset_snapshot.json"));
        // fieldMaps（字段映射集合）：保存 JObject.Parse(string) 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、CodeIdentifierAssetMigration.RestoreAndVerify()。
JObject fieldMaps = JObject.Parse(File.ReadAllText(DirectoryPath + "/field_maps.json"));
        activeFieldMaps = fieldMaps;
        // failures（失败记录）：使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 JsonConvert.SerializeObject(object?, Formatting) 的参数 等共 4 处。
List<string> failures = new List<string>();
        // repaired（修复后的）：使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 JsonConvert.SerializeObject(object?, Formatting) 的参数、CodeIdentifierAssetMigration.RestoreAndVerify()。
int repaired = 0;
        // verified（验证过的）：使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 JsonConvert.SerializeObject(object?, Formatting) 的参数、CodeIdentifierAssetMigration.RestoreAndVerify()。
int verified = 0;
        // asset（资源）：每轮从记录集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 List<string>.Add(string) 的参数。
foreach (AssetRecord asset in records)
        {
            VisitAsset(asset.path, /* 中文含义：对象集合。 */ objects =>
            {
                // byIdentity（依据身份标识）：保存 IEnumerable<Object>.ToDictionary<Object, string>(Func<Object, string>) 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数。
Dictionary<string, Object> byIdentity = objects.ToDictionary( /* 中文含义：当前对象。 */item => GlobalObjectId.GetGlobalObjectIdSlow(item).ToString());
                // item（当前对象记录）：每轮从资源的对象集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 Dictionary<string, Object>.TryGetValue(string, out Object) 的参数、作为 List<string>.Add(string) 的参数。
foreach (ObjectRecord item in asset.objects)
                {
                    // 对象引用的内部编号只在当前编辑器会话有效，不能作为字段写回。
                    // referencePaths（引用路径集合）：保存 IEnumerable<string>.ToArray<string>() 的返回结果，供当前步骤使用。使用位置：作为 IEnumerable<CodeIdentifierAssetMigration.PropertyRecord>.Where<CodeIdentifierAssetMigration.PropertyRecord>(Func<CodeIdentifierAssetMigration.PropertyRecord, bool>) 的参数。
string[] referencePaths = item.properties.Where( /* 中文含义：当前属性记录。 */value => value.type == (int)SerializedPropertyType.ObjectReference).Select( /* 中文含义：当前属性记录。 */value => value.path + ".").ToArray();
                    // properties（属性集合）：保存 IEnumerable<CodeIdentifierAssetMigration.PropertyRecord>.ToList<CodeIdentifierAssetMigration.PropertyRecord>() 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数。
List<PropertyRecord> properties = item.properties.Where( /* 中文含义：当前属性记录。 */value => !referencePaths.Any( /* 中文含义：前缀。 */prefix => value.path.StartsWith(prefix, StringComparison.Ordinal))).ToList();
                    // target（目标对象）：使用位置：作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、作为 Dictionary<string, Object>.TryGetValue(string, out Object) 的参数、作为 EditorUtility.SetDirty(Object) 的参数 等共 4 处。
Object target;
                    if (!byIdentity.TryGetValue(item.identity, out target))
                    {
                        failures.Add(asset.path + "：对象身份丢失：" + item.identity);
                        continue;
                    }

                    // serialized（序列化的）：使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数。
SerializedObject serialized = new SerializedObject(target);
                    // old（原有属性记录）：每轮从属性集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.Equivalent(JToken, JToken) 的参数、作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、作为 CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken) 的参数 等共 4 处。
foreach (PropertyRecord old in properties)
                    {
                        // path（路径）：保存 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的返回结果，供当前步骤使用。使用位置：作为 List<string>.Add(string) 的参数、作为 SerializedObject.FindProperty(string) 的参数。
string path = MapPath(target.GetType(), old.path, fieldMaps);
                        // property（属性）：保存 SerializedObject.FindProperty(string) 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.ReadValue(SerializedProperty) 的参数、作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken) 的参数。
SerializedProperty property = serialized.FindProperty(path);
                        if (property == null)
                        {
                            failures.Add(asset.path + "：字段无法映射：" + item.typeName + "." + old.path + " → " + path);
                            continue;
                        }

                        // current（当前次级索引凭证）：保存 CodeIdentifierAssetMigration.ReadValue(SerializedProperty) 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.Equivalent(JToken, JToken) 的参数。
JToken current = ReadValue(property);
                        if (!Equivalent(current, old.value))
                        {
                            WriteValue(property, old.value);
                            repaired++;
                        }

                        verified++;
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(target);
                    serialized.Update();
                    // old（原有属性记录）：每轮从属性集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.Equivalent(JToken, JToken) 的参数、作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、作为 List<string>.Add(string) 的参数。
foreach (PropertyRecord old in properties)
                    {
                        // property（属性）：保存 SerializedObject.FindProperty(string) 的返回结果，供当前步骤使用。使用位置：作为 CodeIdentifierAssetMigration.ReadValue(SerializedProperty) 的参数、作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数。
SerializedProperty property = serialized.FindProperty(MapPath(target.GetType(), old.path, fieldMaps));
                        if (property != null && !Equivalent(ReadValue(property), old.value))
                        {
                            failures.Add(asset.path + "：字段数值未保留：" + old.path);
                        }
                    }
                }
            }, true, asset);
        }

        AssetDatabase.SaveAssets();
        File.WriteAllText(DirectoryPath + "/asset_verification.json", JsonConvert.SerializeObject(new { verified, repaired, failures }, Formatting.Indented));
        if (failures.Count > 0)
        {
            throw new InvalidOperationException("资源迁移核验存在差异：" + failures.Count + "项，详见迁移报告。");
        }

        return "资源字段核验通过：" + verified + "项；补回无法自动迁移的字段：" + repaired + "项。";
    }

    /// <summary>Equivalent 的业务处理入口；本方法直接依赖 CodeIdentifierAssetMigration.Equivalent(JToken, JToken)、double.Equals(double)、IEnumerable&lt;JToken&gt;.Count&lt;JToken&gt;()、JToken.DeepEquals(JToken?, JToken?)、Math.Abs(double)、Math.Max(double, double)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CodeIdentifierAssetMigration.Equivalent(JToken, JToken)、CodeIdentifierAssetMigration.RestoreAndVerify()；下游（含内部回调体） CodeIdentifierAssetMigration.Equivalent(JToken, JToken)、double.Equals(double)、IEnumerable&lt;JToken&gt;.Count&lt;JToken&gt;()、JToken.DeepEquals(JToken?, JToken?)、Math.Abs(double)、Math.Max(double, double)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="current">中文含义：当前次级索引凭证。使用位置：作为 CodeIdentifierAssetMigration.Equivalent(JToken, JToken) 的参数、作为 JToken.DeepEquals(JToken?, JToken?) 的参数、CodeIdentifierAssetMigration.Equivalent(JToken, JToken)。</param>
    /// <param name="expected">中文含义：预期。使用位置：作为 CodeIdentifierAssetMigration.Equivalent(JToken, JToken) 的参数、作为 JToken.DeepEquals(JToken?, JToken?) 的参数、CodeIdentifierAssetMigration.Equivalent(JToken, JToken)。</param>
    private static bool Equivalent(JToken current, JToken expected)
    {
        if (current is JArray && expected is JArray)
        {
            if (current.Count() != expected.Count())
            {
                return false;
            }

            // index（当前元素索引）：使用位置：作为 JToken.this[object] 的参数、CodeIdentifierAssetMigration.Equivalent(JToken, JToken)。
for (int index = 0; index < current.Count(); index++)
            {
                if (!Equivalent(current[index], expected[index]))
                {
                    return false;
                }
            }

            return true;
        }

        if (current != null && expected != null && current.Type == JTokenType.Float && expected.Type == JTokenType.Float)
        {
            // actual（实际）：使用位置：作为 Math.Abs(double) 的参数、CodeIdentifierAssetMigration.Equivalent(JToken, JToken)。
double actual = (double)current;
            // wanted（需要的）：使用位置：作为 double.Equals(double) 的参数、作为 Math.Abs(double) 的参数。
double wanted = (double)expected;
            if (actual.Equals(wanted))
            {
                return true;
            }

            return Math.Abs(actual - wanted) <= Math.Max(.000001, Math.Abs(wanted) * .000001);
        }

        if (current is JObject && expected is JObject && current["identity"] != null && expected["identity"] != null)
        {
            return (string)current["identity"] == (string)expected["identity"];
        }

        return JToken.DeepEquals(current, expected);
    }

    /// <summary>MigrateOverrides 的业务处理入口；本方法直接依赖 CodeIdentifierAssetMigration.MapPath(Type, string, JObject)、GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)、object.GetType()、PrefabUtility.GetPropertyModifications(Object)、PrefabUtility.IsAnyPrefabInstanceRoot(GameObject)、PrefabUtility.SetPropertyModifications(Object, PropertyModification[])、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CodeIdentifierAssetMigration.VisitAsset(string, Action&lt;List&lt;Object&gt; &gt;, bool, CodeIdentifierAssetMigration.AssetRecord)；下游（含内部回调体） CodeIdentifierAssetMigration.MapPath(Type, string, JObject)、GameObject.GetComponentsInChildren&lt;Transform&gt;(bool)、object.GetType()、PrefabUtility.GetPropertyModifications(Object)、PrefabUtility.IsAnyPrefabInstanceRoot(GameObject)、PrefabUtility.SetPropertyModifications(Object, PropertyModification[])、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根游戏对象。使用位置：CodeIdentifierAssetMigration.MigrateOverrides(GameObject)。</param>
    private static void MigrateOverrides(GameObject root)
    {
        // child（子级）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 PrefabUtility.GetPropertyModifications(Object) 的参数、作为 PrefabUtility.IsAnyPrefabInstanceRoot(GameObject) 的参数、作为 PrefabUtility.SetPropertyModifications(Object, PropertyModification[]) 的参数。
foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
            {
                continue;
            }

            // modifications（修改记录）：保存 PrefabUtility.GetPropertyModifications(Object) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.SetPropertyModifications(Object, PropertyModification[]) 的参数、CodeIdentifierAssetMigration.MigrateOverrides(GameObject)。
PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(child.gameObject);
            if (modifications == null)
            {
                continue;
            }

            // changed（是否发生变化）：使用位置：CodeIdentifierAssetMigration.MigrateOverrides(GameObject)。
bool changed = false;
            // modification（修改）：每轮从修改记录取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、作为 string.IsNullOrEmpty(string) 的参数、CodeIdentifierAssetMigration.MigrateOverrides(GameObject)。
foreach (PropertyModification modification in modifications)
            {
                if (modification.target == null || string.IsNullOrEmpty(modification.propertyPath))
                {
                    continue;
                }

                // mapped（映射后的）：保存 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的返回结果，供当前步骤使用。使用位置：CodeIdentifierAssetMigration.MigrateOverrides(GameObject)。
string mapped = MapPath(modification.target.GetType(), modification.propertyPath, activeFieldMaps);
                if (mapped != modification.propertyPath)
                {
                    modification.propertyPath = mapped;
                    changed = true;
                }
            }

            if (changed)
            {
                PrefabUtility.SetPropertyModifications(child.gameObject, modifications);
            }
        }
    }

    /// <summary>WriteValue 的业务处理入口；本方法直接依赖 AnimationCurve.AnimationCurve(params Keyframe[])、Bounds.Bounds(Vector3, Vector3)、Color.Color(float, float, float, float)、EditorUtility.InstanceIDToObject(int)、GlobalObjectId.GlobalObjectIdentifierToObjectSlow(GlobalObjectId)、GlobalObjectId.TryParse(string, out GlobalObjectId)、InvalidOperationException.InvalidOperationException(string) 等共 18 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CodeIdentifierAssetMigration.RestoreAndVerify()；下游（含内部回调体） AnimationCurve.AnimationCurve(params Keyframe[])、Bounds.Bounds(Vector3, Vector3)、Color.Color(float, float, float, float)、EditorUtility.InstanceIDToObject(int)、GlobalObjectId.GlobalObjectIdentifierToObjectSlow(GlobalObjectId)、GlobalObjectId.TryParse(string, out GlobalObjectId)、InvalidOperationException.InvalidOperationException(string) 等共 18 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="property">中文含义：属性。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)。</param>
    /// <param name="value">中文含义：当前次级索引凭证。使用位置：作为 Color.Color(float, float, float, float) 的参数、作为 EditorUtility.InstanceIDToObject(int) 的参数、作为 GlobalObjectId.TryParse(string, out GlobalObjectId) 的参数 等共 11 处。</param>
    private static void WriteValue(SerializedProperty property, JToken value)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.Character:
                property.longValue = (long)value;
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = (bool)value;
                break;
            case SerializedPropertyType.Float:
                property.doubleValue = (double)value;
                break;
            case SerializedPropertyType.String:
                property.stringValue = (string)value;
                break;
            case SerializedPropertyType.ObjectReference:
                // reference（引用）：使用位置：CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)。
Object reference = null;
                if (value.Type != JTokenType.Null)
                {
                    // identity（身份标识）：使用位置：作为 GlobalObjectId.GlobalObjectIdentifierToObjectSlow(GlobalObjectId) 的参数、作为 GlobalObjectId.TryParse(string, out GlobalObjectId) 的参数。
GlobalObjectId identity;
                    if (GlobalObjectId.TryParse((string)value["identity"], out identity))
                    {
                        reference = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identity);
                    }

                    if (reference == null)
                    {
                        reference = EditorUtility.InstanceIDToObject((int)value["instanceId"]);
                    }

                    if (reference == null)
                    {
                        throw new InvalidOperationException("引用无法恢复：" + property.propertyPath);
                    }
                }

                property.objectReferenceValue = reference;
                break;
            case SerializedPropertyType.Color:
                property.colorValue = new Color((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
                break;
            case SerializedPropertyType.Vector2:
                property.vector2Value = new Vector2((float)value[0], (float)value[1]);
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = new Vector3((float)value[0], (float)value[1], (float)value[2]);
                break;
            case SerializedPropertyType.Vector4:
                property.vector4Value = new Vector4((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
                break;
            case SerializedPropertyType.Quaternion:
                property.quaternionValue = new Quaternion((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
                break;
            case SerializedPropertyType.Rect:
                property.rectValue = new Rect((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
                break;
            case SerializedPropertyType.Bounds:
                property.boundsValue = new Bounds(new Vector3((float)value[0], (float)value[1], (float)value[2]), new Vector3((float)value[3], (float)value[4], (float)value[5]));
                break;
            case SerializedPropertyType.Vector2Int:
                property.vector2IntValue = new Vector2Int((int)value[0], (int)value[1]);
                break;
            case SerializedPropertyType.Vector3Int:
                property.vector3IntValue = new Vector3Int((int)value[0], (int)value[1], (int)value[2]);
                break;
            case SerializedPropertyType.AnimationCurve:
                // keys（键集合）：使用位置：作为 AnimationCurve.AnimationCurve(params Keyframe[]) 的参数、CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)。
List<Keyframe> keys = new List<Keyframe>();
                // key（键）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Keyframe.Keyframe(float, float, float, float, float, float) 的参数、CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)。
foreach (JArray key in value["keys"])
                {
                    // frame（帧）：使用位置：作为 List<Keyframe>.Add(Keyframe) 的参数、CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)。
Keyframe frame = new Keyframe((float)key[0], (float)key[1], (float)key[2], (float)key[3], (float)key[4], (float)key[5]);
                    frame.weightedMode = (WeightedMode)(int)key[6];
                    keys.Add(frame);
                }

                // curve（曲线）：使用位置：CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)。
AnimationCurve curve = new AnimationCurve(keys.ToArray());
                curve.preWrapMode = (WrapMode)(int)value["pre"];
                curve.postWrapMode = (WrapMode)(int)value["post"];
                property.animationCurveValue = curve;
                break;
            default:
                throw new InvalidOperationException("不支持迁移的字段类型：" + property.propertyType);
        }
    }

    /// <summary>ApplyPreparedSources 的业务处理入口；本方法直接依赖 AssetDatabase.AllowAutoRefresh()、AssetDatabase.DisallowAutoRefresh()、AssetDatabase.Refresh()、BitConverter.ToString(byte[])、CodeIdentifierAssetMigration.MoveScripts()、Directory.CreateDirectory(string)、EditorApplication.LockReloadAssemblies() 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.AllowAutoRefresh()、AssetDatabase.DisallowAutoRefresh()、AssetDatabase.Refresh()、BitConverter.ToString(byte[])、CodeIdentifierAssetMigration.MoveScripts()、Directory.CreateDirectory(string)、EditorApplication.LockReloadAssemblies() 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ApplyPreparedSources()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("迁移前必须停止播放。");
        }

        // report（报告）：保存 JObject.Parse(string) 的返回结果，供当前步骤使用。使用位置：CodeIdentifierAssetMigration.ApplyPreparedSources()。
JObject report = JObject.Parse(File.ReadAllText(DirectoryPath + "/preflight_report.json"));
        if (report["errors"].Any() || report["summaries"].Count() != 7)
        {
            throw new InvalidOperationException("暂存源码尚未通过全部程序集预检查。");
        }

        // changes（变化集合）：保存 JArray.Parse(string) 的返回结果，供当前步骤使用。使用位置：CodeIdentifierAssetMigration.ApplyPreparedSources()。
JArray changes = JArray.Parse(File.ReadAllText(DirectoryPath + "/changed_files.json"));
        // hash（哈希编号）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
        {
            // item（当前次级索引对象）：每轮从变化集合取得一个元素，仅代表当前这一项。使用位置：CodeIdentifierAssetMigration.ApplyPreparedSources()。
foreach (JObject item in changes)
            {
                // path（路径）：使用位置：作为 File.ReadAllBytes(string) 的参数、作为 InvalidOperationException.InvalidOperationException(string) 的参数。
string path = (string)item["path"];
                // current（当前）：保存 string.Replace(string, string) 的返回结果，供当前步骤使用。使用位置：CodeIdentifierAssetMigration.ApplyPreparedSources()。
string current = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
                if (current != (string)item["sourceHash"])
                {
                    throw new InvalidOperationException("预检查后源码已变化，请重新生成迁移：" + path);
                }
            }
        }

        AssetDatabase.DisallowAutoRefresh();
        EditorApplication.LockReloadAssemblies();
        try
        {
            // item（当前次级索引对象）：每轮从变化集合取得一个元素，仅代表当前这一项。使用位置：CodeIdentifierAssetMigration.ApplyPreparedSources()。
foreach (JObject item in changes)
            {
                // path（路径）：使用位置：作为 File.Copy(string, string, bool) 的参数、CodeIdentifierAssetMigration.ApplyPreparedSources()。
string path = (string)item["path"];
                // backup（备份）：使用位置：作为 File.Copy(string, string, bool) 的参数、作为 Path.GetDirectoryName(string) 的参数。
string backup = "Library/IdentifierMigrationCommitBaseline/" + path;
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(path, backup, true);
                File.Copy(path + ".meta", backup + ".meta", true);
                File.Copy(DirectoryPath + "/staged/" + path, path, true);
            }

            return "已应用源码：" + changes.Count + "项；" + MoveScripts();
        }
        finally
        {
            AssetDatabase.AllowAutoRefresh();
            EditorApplication.UnlockReloadAssemblies();
            AssetDatabase.Refresh();
        }
    }

    /// <summary>MoveScripts 的业务处理入口；本方法直接依赖 AssetDatabase.MoveAsset(string, string)、AssetDatabase.StartAssetEditing()、AssetDatabase.StopAssetEditing()、File.ReadAllText(string)、InvalidOperationException.InvalidOperationException(string)、JsonConvert.DeserializeObject&lt;Dictionary&lt;string, string&gt; &gt;(string)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CodeIdentifierAssetMigration.ApplyPreparedSources()；下游（含内部回调体） AssetDatabase.MoveAsset(string, string)、AssetDatabase.StartAssetEditing()、AssetDatabase.StopAssetEditing()、File.ReadAllText(string)、InvalidOperationException.InvalidOperationException(string)、JsonConvert.DeserializeObject&lt;Dictionary&lt;string, string&gt; &gt;(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string MoveScripts()
    {
        // paths（路径集合）：保存 JsonConvert.DeserializeObject<Dictionary<string, string>>(string) 的返回结果，供当前步骤使用。使用位置：CodeIdentifierAssetMigration.MoveScripts()。
Dictionary<string, string> paths = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(DirectoryPath + "/file_moves.json"));
        AssetDatabase.StartAssetEditing();
        try
        {
            // path（路径）：每轮从路径集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.MoveAsset(string, string) 的参数。
foreach (KeyValuePair<string, string> path in paths)
            {
                // error（错误）：保存 AssetDatabase.MoveAsset(string, string) 的返回结果，供当前步骤使用。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、作为 string.IsNullOrEmpty(string) 的参数。
string error = AssetDatabase.MoveAsset(path.Key, path.Value);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException(error);
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        return "已保留资源身份并迁移脚本文件：" + paths.Count + "项。";
    }
}

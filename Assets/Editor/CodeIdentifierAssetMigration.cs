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
    /// <summary>目录路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string DirectoryPath = "Library/CodeIdentifierMigration";
    /// <summary>字段标志集合。编译期常量，保持既有值与消费者映射。</summary>
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    /// <summary>生效字段映射集合。</summary>
    private static JObject activeFieldMaps;
    /// <summary>编辑器配置与校验模块 PropertyRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    private sealed class PropertyRecord
    {
        /// <summary>路径。</summary>
        public string path;
        /// <summary>类型。</summary>
        public int type;
        /// <summary>当前次级索引凭证。</summary>
        public JToken value;
    }

    /// <summary>编辑器配置与校验模块 ObjectRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    private sealed class ObjectRecord
    {
        /// <summary>身份标识。</summary>
        public string identity;
        /// <summary>实例编号。</summary>
        public int instanceId;
        /// <summary>类型名称。</summary>
        public string typeName;
        /// <summary>属性集合。</summary>
        public List<PropertyRecord> properties = new List<PropertyRecord>();
    }

    /// <summary>编辑器配置与校验模块 AssetRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    private sealed class AssetRecord
    {
        /// <summary>路径。</summary>
        public string path;
        /// <summary>之前是否未保存改动。</summary>
        public bool wasDirty;
        /// <summary>对象集合。</summary>
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

        // 脚本路径集合。
HashSet<string> scriptPaths = new HashSet<string>(AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts", "Assets/Editor" }).Select(AssetDatabase.GUIDToAssetPath));
        // 记录集合。
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
                // 路径。
string path = filename.Replace('\\', '/');
                // 正式场景使用二进制序列化，必须依据资源依赖图筛选。
                // 是否引用集合持有的脚本。
bool referencesOwnedScript = AssetDatabase.GetDependencies(path, true).Any(scriptPaths.Contains);
                if (!referencesOwnedScript)
                {
                    continue;
                }

                // 当前资源记录。
AssetRecord record = new AssetRecord
                {
                    path = path
                };
                VisitAsset(path, /* 中文含义：对象集合。 */ objects =>
                {
                    // target（目标对象）：每轮从对象集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 GlobalObjectId.GetGlobalObjectIdSlow(Object) 的参数、作为 SerializedObject.SerializedObject(Object) 的参数。
foreach (Object target in objects)
                    {
                        // 序列化的。
SerializedObject serialized = new SerializedObject(target);
                        // 属性。
SerializedProperty property = serialized.GetIterator();
                        // 当前对象记录。
ObjectRecord item = new ObjectRecord
                        {
                            identity = GlobalObjectId.GetGlobalObjectIdSlow(target).ToString(),
                            instanceId = target.GetInstanceID(),
                            typeName = target.GetType().FullName
                        };
                        // 是否进入子对象集合。
bool enterChildren = true;
                        while (property.Next(enterChildren))
                        {
                            enterChildren = property.propertyType != SerializedPropertyType.ObjectReference;
                            if (!property.propertyPath.Any( /* 中文含义：字符。 */character => character > 127))
                            {
                                continue;
                            }

                            // 当前次级索引凭证。
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
                    // 备份。
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
    /// <param name="path">路径。</param>
    /// <param name="action">动作。</param>
    /// <param name="save">是否保存。</param>
    /// <param name="record">当前资源记录。</param>
    private static void VisitAsset(string path, Action<List<Object>> action, bool save, AssetRecord record)
    {
        // 对象集合。
List<Object> objects = new List<Object>();
        if (path.EndsWith(".unity", StringComparison.Ordinal))
        {
            // 场景。
Scene scene = SceneManager.GetSceneByPath(path);
            // 是否已经已加载。
bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (!alreadyLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            // 之前是否未保存改动。
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

        // 预制件。
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
    /// <param name="property">属性。</param>
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
                // 颜色。
Color color = property.colorValue;
                return new JArray(color.r, color.g, color.b, color.a);
            case SerializedPropertyType.Vector2:
                // 向量2。
Vector2 vector2 = property.vector2Value;
                return new JArray(vector2.x, vector2.y);
            case SerializedPropertyType.Vector3:
                // 向量3。
Vector3 vector3 = property.vector3Value;
                return new JArray(vector3.x, vector3.y, vector3.z);
            case SerializedPropertyType.Vector4:
                // 向量4。
Vector4 vector4 = property.vector4Value;
                return new JArray(vector4.x, vector4.y, vector4.z, vector4.w);
            case SerializedPropertyType.Quaternion:
                // 旋转。
Quaternion rotation = property.quaternionValue;
                return new JArray(rotation.x, rotation.y, rotation.z, rotation.w);
            case SerializedPropertyType.Rect:
                // 矩形。
Rect rect = property.rectValue;
                return new JArray(rect.x, rect.y, rect.width, rect.height);
            case SerializedPropertyType.Bounds:
                // 包围盒。
Bounds bounds = property.boundsValue;
                return new JArray(bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, bounds.size.y, bounds.size.z);
            case SerializedPropertyType.Vector2Int:
                // 整数2。
Vector2Int integer2 = property.vector2IntValue;
                return new JArray(integer2.x, integer2.y);
            case SerializedPropertyType.Vector3Int:
                // 整数3。
Vector3Int integer3 = property.vector3IntValue;
                return new JArray(integer3.x, integer3.y, integer3.z);
            case SerializedPropertyType.AnimationCurve:
                // 曲线。
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
    /// <param name="targetType">目标类型。</param>
    /// <param name="original">原始。</param>
    /// <param name="fieldMaps">字段映射集合。</param>
    private static string MapPath(Type targetType, string original, JObject fieldMaps)
    {
        // 部件集合。
string[] parts = original.Split('.');
        // 当前反射类型。
Type current = targetType;
        // 当前元素索引。
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

            // 声明的。
Type declaring = current;
            // 字段。
FieldInfo field = null;
            while (declaring != null)
            {
                // 映射。
JObject map = fieldMaps[declaring.FullName] as JObject;
                // 名称。
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
        // 记录集合。
List<AssetRecord> records = JsonConvert.DeserializeObject<List<AssetRecord>>(File.ReadAllText(DirectoryPath + "/asset_snapshot.json"));
        // 字段映射集合。
JObject fieldMaps = JObject.Parse(File.ReadAllText(DirectoryPath + "/field_maps.json"));
        activeFieldMaps = fieldMaps;
        // 失败记录。
List<string> failures = new List<string>();
        // 修复后的。
int repaired = 0;
        // 验证过的。
int verified = 0;
        // asset（资源）：每轮从记录集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 List<string>.Add(string) 的参数。
foreach (AssetRecord asset in records)
        {
            VisitAsset(asset.path, /* 中文含义：对象集合。 */ objects =>
            {
                // 依据身份标识。
Dictionary<string, Object> byIdentity = objects.ToDictionary( /* 中文含义：当前对象。 */item => GlobalObjectId.GetGlobalObjectIdSlow(item).ToString());
                // item（当前对象记录）：每轮从资源的对象集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.VisitAsset(string, Action<List<Object>>, bool, CodeIdentifierAssetMigration.AssetRecord) 的参数、作为 Dictionary<string, Object>.TryGetValue(string, out Object) 的参数、作为 List<string>.Add(string) 的参数。
foreach (ObjectRecord item in asset.objects)
                {
                    // 对象引用的内部编号只在当前编辑器会话有效，不能作为字段写回。
                    // 引用路径集合。
string[] referencePaths = item.properties.Where( /* 中文含义：当前属性记录。 */value => value.type == (int)SerializedPropertyType.ObjectReference).Select( /* 中文含义：当前属性记录。 */value => value.path + ".").ToArray();
                    // 属性集合。
List<PropertyRecord> properties = item.properties.Where( /* 中文含义：当前属性记录。 */value => !referencePaths.Any( /* 中文含义：前缀。 */prefix => value.path.StartsWith(prefix, StringComparison.Ordinal))).ToList();
                    // 目标对象。
Object target;
                    if (!byIdentity.TryGetValue(item.identity, out target))
                    {
                        failures.Add(asset.path + "：对象身份丢失：" + item.identity);
                        continue;
                    }

                    // 序列化的。
SerializedObject serialized = new SerializedObject(target);
                    // old（原有属性记录）：每轮从属性集合取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.Equivalent(JToken, JToken) 的参数、作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、作为 CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken) 的参数 等共 4 处。
foreach (PropertyRecord old in properties)
                    {
                        // 路径。
string path = MapPath(target.GetType(), old.path, fieldMaps);
                        // 属性。
SerializedProperty property = serialized.FindProperty(path);
                        if (property == null)
                        {
                            failures.Add(asset.path + "：字段无法映射：" + item.typeName + "." + old.path + " → " + path);
                            continue;
                        }

                        // 当前次级索引凭证。
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
                        // 属性。
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
    /// <param name="current">当前次级索引凭证。</param>
    /// <param name="expected">预期。</param>
    private static bool Equivalent(JToken current, JToken expected)
    {
        if (current is JArray && expected is JArray)
        {
            if (current.Count() != expected.Count())
            {
                return false;
            }

            // 当前元素索引。
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
            // 实际。
double actual = (double)current;
            // 需要的。
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
    /// <param name="root">根游戏对象。</param>
    private static void MigrateOverrides(GameObject root)
    {
        // child（子级）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 PrefabUtility.GetPropertyModifications(Object) 的参数、作为 PrefabUtility.IsAnyPrefabInstanceRoot(GameObject) 的参数、作为 PrefabUtility.SetPropertyModifications(Object, PropertyModification[]) 的参数。
foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
            {
                continue;
            }

            // 修改记录。
PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(child.gameObject);
            if (modifications == null)
            {
                continue;
            }

            // 是否发生变化。
bool changed = false;
            // modification（修改）：每轮从修改记录取得一个元素，仅代表当前这一项。使用位置：作为 CodeIdentifierAssetMigration.MapPath(Type, string, JObject) 的参数、作为 string.IsNullOrEmpty(string) 的参数、CodeIdentifierAssetMigration.MigrateOverrides(GameObject)。
foreach (PropertyModification modification in modifications)
            {
                if (modification.target == null || string.IsNullOrEmpty(modification.propertyPath))
                {
                    continue;
                }

                // 映射后的。
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
    /// <param name="property">属性。</param>
    /// <param name="value">当前次级索引凭证。</param>
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
                // 引用。
Object reference = null;
                if (value.Type != JTokenType.Null)
                {
                    // 身份标识。
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
                // 键集合。
List<Keyframe> keys = new List<Keyframe>();
                // key（键）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 Keyframe.Keyframe(float, float, float, float, float, float) 的参数、CodeIdentifierAssetMigration.WriteValue(SerializedProperty, JToken)。
foreach (JArray key in value["keys"])
                {
                    // 帧。
Keyframe frame = new Keyframe((float)key[0], (float)key[1], (float)key[2], (float)key[3], (float)key[4], (float)key[5]);
                    frame.weightedMode = (WeightedMode)(int)key[6];
                    keys.Add(frame);
                }

                // 曲线。
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

        // 报告。
JObject report = JObject.Parse(File.ReadAllText(DirectoryPath + "/preflight_report.json"));
        if (report["errors"].Any() || report["summaries"].Count() != 7)
        {
            throw new InvalidOperationException("暂存源码尚未通过全部程序集预检查。");
        }

        // 变化集合。
JArray changes = JArray.Parse(File.ReadAllText(DirectoryPath + "/changed_files.json"));
        // hash（哈希编号）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
        {
            // item（当前次级索引对象）：每轮从变化集合取得一个元素，仅代表当前这一项。使用位置：CodeIdentifierAssetMigration.ApplyPreparedSources()。
foreach (JObject item in changes)
            {
                // 路径。
string path = (string)item["path"];
                // 当前。
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
                // 路径。
string path = (string)item["path"];
                // 备份。
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
        // 路径集合。
Dictionary<string, string> paths = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(DirectoryPath + "/file_moves.json"));
        AssetDatabase.StartAssetEditing();
        try
        {
            // path（路径）：每轮从路径集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.MoveAsset(string, string) 的参数。
foreach (KeyValuePair<string, string> path in paths)
            {
                // 错误。
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEditor;
using UnityEngine;

/// <summary>从实际编译结果列出直接字段写入、动画调用和事件订阅，避免把同名局部变量当作业务字段。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构边界校验器")]
public static class ArchitectureBoundaryValidator
{
    /// <summary>编辑器配置与校验模块 BoundaryRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构边界校验器+边界记录")]
    public sealed class BoundaryRecord
    {
        /// <summary>中文含义：类别。使用位置：作为 IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;.Where&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;(Func&lt;ArchitectureBoundaryValidator.BoundaryRecord, bool&gt;) 的参数、ArchitectureBoundaryValidator.Scan()、ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;) 等共 5 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("类别")]
        [UnityEngine.InspectorName("类别")]
        [Newtonsoft.Json.JsonProperty("类别")]
        public string category;
        /// <summary>中文含义：调用类型。使用位置：作为 IEnumerable&lt;Type&gt;.Any&lt;Type&gt;(Func&lt;Type, bool&gt;) 的参数、作为 List&lt;string&gt;.Add(string) 的参数、ArchitectureBoundaryValidator.Scan() 等共 5 处。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("调用类型")]
        [UnityEngine.InspectorName("调用类型")]
        [Newtonsoft.Json.JsonProperty("调用类型")]
        public string invokeType;
        /// <summary>中文含义：调用方法。使用位置：作为 List&lt;string&gt;.Add(string) 的参数、ArchitectureBoundaryValidator.Scan()、PlayerModuleBoundaryValidator.Validate(List&lt;string&gt;)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("调用方法")]
        [UnityEngine.InspectorName("调用方法")]
        [Newtonsoft.Json.JsonProperty("调用方法")]
        public string invokeMethod;
        /// <summary>中文含义：目标类型。使用位置：作为 List&lt;string&gt;.Add(string) 的参数、ArchitectureBoundaryValidator.Scan()、ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("目标类型")]
        [UnityEngine.InspectorName("目标类型")]
        [Newtonsoft.Json.JsonProperty("目标类型")]
        public string targetType;
        /// <summary>中文含义：目标成员。使用位置：作为 HashSet&lt;string&gt;.Contains(string) 的参数、作为 List&lt;string&gt;.Add(string) 的参数、ArchitectureBoundaryValidator.Scan()。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("目标成员")]
        [UnityEngine.InspectorName("目标成员")]
        [Newtonsoft.Json.JsonProperty("目标成员")]
        public string targetMember;
        /// <summary>中文含义：源文件。使用位置：作为 List&lt;string&gt;.Add(string) 的参数、ArchitectureBoundaryValidator.Scan()。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("源文件")]
        [UnityEngine.InspectorName("源文件")]
        [Newtonsoft.Json.JsonProperty("源文件")]
        public string sourceFile;
        /// <summary>中文含义：指令偏移。使用位置：作为 List&lt;string&gt;.Add(string) 的参数、ArchitectureBoundaryValidator.Scan()。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("指令偏移")]
        [UnityEngine.InspectorName("指令偏移")]
        [Newtonsoft.Json.JsonProperty("指令偏移")]
        public int instructionOffset;
        /// <summary>中文含义：是否跨类型。使用位置：ArchitectureBoundaryValidator.Scan()、ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("跨类型")]
        [UnityEngine.InspectorName("跨类型")]
        [Newtonsoft.Json.JsonProperty("跨类型")]
        public bool crossType;
    }

    /// <summary>编辑器配置与校验模块 BoundaryReport；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "架构边界校验器+边界报告")]
    private sealed class BoundaryReport
    {
        /// <summary>中文含义：说明。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("说明")]
        [UnityEngine.InspectorName("说明")]
        [Newtonsoft.Json.JsonProperty("说明")]
        public string description = "实际编译结果中的直接访问；字段地址记录表示可能的间接写入。集合内部变化、反射调用和外部插件内部访问仍需结合词法清单与人工核对。未迁移的跨类型访问仅登记，不自动视为错误。";
        /// <summary>中文含义：记录。使用位置：作为 JsonUtility.ToJson(object, bool) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("记录")]
        [UnityEngine.InspectorName("记录")]
        [Newtonsoft.Json.JsonProperty("记录")]
        public List<BoundaryRecord> record;
    }

    /// <summary>中文含义：指令。只读字段，初始化后不由普通写入口替换。使用位置：ArchitectureBoundaryValidator.Scan()。</summary>
    private static readonly Dictionary<short, OpCode> instruction = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Where( /* 中文含义：字段。 */field => field.FieldType == typeof(OpCode)).Select( /* 中文含义：字段。 */field => (OpCode)field.GetValue(null)).GroupBy( /* 中文含义：代码。 */code => code.Value).ToDictionary( /* 中文含义：分组。 */group => group.Key, /* 中文含义：分组。 */ group => group.First());
    /// <summary>中文含义：状态类型。只读字段，初始化后不由普通写入口替换。使用位置：ArchitectureBoundaryValidator.Scan()。</summary>
    private static readonly HashSet<string> stateType = new HashSet<string>
    {
        "VillagerController",
        "EnemyContext",
        "PathAgent",
        "EnemyAIManager",
        "EnemyWindowTraversal",
        "CharacterMotionExecutor",
        "DoorwayTraversalOccupancy",
        "TraversalTokenRegistry",
        "EnemyWindowTraversalRoute",
        "EnemyPlatformDropRoute",
        "LeonController",
        "HealthComponent",
        "LeonAnimationActionCoordinator",
        "AnimationActionTransaction",
        "LeonWeaponHandler",
        "WeaponBase",
        "LeonQTECoordinator",
        "VillagerQTEParticipant",
        "InventoryManager",
        "StorageBoxManager",
        "GridItem",
        "ChapterOneMissionManager",
        "GameUIManager",
        "CharacterHealthModule",
        "EnemyCombatModule",
        "EnemyPerceptionModule",
        "EnemyActionCoordinator",
        "EnemyAnimationAdapter"
    };
    /// <summary>Scan 的业务处理入口；本方法直接依赖 ArchitectureBoundaryValidator.BoundaryRecord.BoundaryRecord()、ArchitectureBoundaryValidator.OperandByteCount(OperandType, byte[], int)、Assembly.GetTypes()、AssetDatabase.FindAssets(string, string[])、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;MonoScript&gt;(string)、BitConverter.ToInt32(byte[], int) 等共 25 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureBoundaryValidator.Export()、EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List&lt;string&gt;)、EnemyModuleBoundaryValidator.ValidateCode(List&lt;string&gt;)、PhaseSevenCompilationBoundary.Validate(List&lt;string&gt;)、PlayerModuleBoundaryValidator.Validate(List&lt;string&gt;)；下游（含内部回调体） ArchitectureBoundaryValidator.BoundaryRecord.BoundaryRecord()、ArchitectureBoundaryValidator.OperandByteCount(OperandType, byte[], int)、Assembly.GetTypes()、AssetDatabase.FindAssets(string, string[])、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;MonoScript&gt;(string)、BitConverter.ToInt32(byte[], int) 等共 25 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static List<BoundaryRecord> Scan()
    {
        // sourceFile（来源文件）：使用位置：ArchitectureBoundaryValidator.Scan()。
var sourceFile = new Dictionary<Type, string>();
        // id（编号）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.GUIDToAssetPath(string) 的参数。
foreach (string id in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Scripts", "Assets/Editor" }))
        {
            // path（路径）：保存 AssetDatabase.GUIDToAssetPath(string) 的返回结果，供当前步骤使用。使用位置：作为 AssetDatabase.LoadAssetAtPath<MonoScript>(string) 的参数、ArchitectureBoundaryValidator.Scan()。
string path = AssetDatabase.GUIDToAssetPath(id);
            // receiverValue1（判空前缓存的加载资源所在路径的返回结果）：从加载资源所在路径的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ArchitectureBoundaryValidator.Scan()。
global::UnityEditor.MonoScript receiverValue1 = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            // type（反射操作使用的类型信息）：使用位置：作为 Dictionary<Type, string>.this[Type] 的参数、ArchitectureBoundaryValidator.Scan()。
Type type;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                type = receiverValue1.GetClass();
            }
            else
            {
                type = default;
            }

            if (type != null)
            {
                sourceFile[type] = path;
            }
        }

        // result（当前边界记录集合）：使用位置：ArchitectureBoundaryValidator.Scan()。
var result = new List<BoundaryRecord>();
        // assemblyList（程序集列表）：使用位置：ArchitectureBoundaryValidator.Scan()。
var assemblyList = new[]
        {
            typeof(VillagerController).Assembly,
            typeof(ArchitectureBoundaryValidator).Assembly,
            typeof(GameArchitecture.ActionHandle).Assembly,
            typeof(WeaponRuntimeModule).Assembly,
            typeof(InventoryRulesModel).Assembly,
            typeof(MissionRulesModel).Assembly
        };
        // assembly（程序集）：每轮从程序集列表取得一个元素，仅代表当前这一项。使用位置：ArchitectureBoundaryValidator.Scan()。
foreach (var assembly in assemblyList)
        {
            // type（反射操作使用的类型信息）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 IEnumerable<MethodBase>.Concat<MethodBase>(IEnumerable<MethodBase>) 的参数、作为 PhaseSevenCompilationBoundary.OutOfBoundsInvoke(Type, MemberInfo) 的参数、ArchitectureBoundaryValidator.Scan()。
foreach (Type type in assembly.GetTypes())
            {
                // rootType（根节点类型）：使用位置：作为 Dictionary<Type, string>.TryGetValue(Type, out string) 的参数、ArchitectureBoundaryValidator.Scan()。
var rootType = type;
                while (rootType.DeclaringType != null)
                {
                    rootType = rootType.DeclaringType;
                }

                if (rootType == typeof(ArchitectureBoundaryValidator))
                {
                    continue;
                }

                // methodList（方法列表）：保存 IEnumerable<MethodBase>.Concat<MethodBase>(IEnumerable<MethodBase>) 的返回结果，供当前步骤使用。使用位置：ArchitectureBoundaryValidator.Scan()。
var methodList = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Cast<MethodBase>().Concat(type.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
                // method（方法）：每轮从方法列表取得一个元素，仅代表当前这一项。使用位置：ArchitectureBoundaryValidator.Scan()。
foreach (MethodBase method in methodList)
                {
                    // receiverValue3（判空前缓存的取得方法身体的返回结果）：从取得方法身体的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、ArchitectureBoundaryValidator.Scan()。
global::System.Reflection.MethodBody receiverValue3 = method.GetMethodBody();
                    // content（内容）：使用位置：作为 ArchitectureBoundaryValidator.OperandByteCount(OperandType, byte[], int) 的参数、作为 BitConverter.ToInt32(byte[], int) 的参数、ArchitectureBoundaryValidator.Scan()。
byte[] content;
                    if (!global::System.Object.ReferenceEquals(receiverValue3, null))
                    {
                        content = receiverValue3.GetILAsByteArray();
                    }
                    else
                    {
                        content = default;
                    }

                    if (content == null)
                    {
                        continue;
                    }

                    // position（位置）：使用位置：作为 ArchitectureBoundaryValidator.OperandByteCount(OperandType, byte[], int) 的参数、作为 BitConverter.ToInt32(byte[], int) 的参数、ArchitectureBoundaryValidator.Scan()。
int position = 0;
                    while (position < content.Length)
                    {
                        // startPoint（起始点）：使用位置：ArchitectureBoundaryValidator.Scan()。
int startPoint = position;
                        // id（编号）：使用位置：作为 Dictionary<short, OpCode>.this[short] 的参数、ArchitectureBoundaryValidator.Scan()。
short id = content[position++];
                        if (id == 0xfe)
                        {
                            id = unchecked((short)(0xfe00 | content[position++]));
                        }

                        // code（代码）：使用位置：作为 ArchitectureBoundaryValidator.OperandByteCount(OperandType, byte[], int) 的参数、ArchitectureBoundaryValidator.Scan()。
OpCode code = instruction[id];
                        if (code.OperandType == OperandType.InlineField || code.OperandType == OperandType.InlineMethod)
                        {
                            // callReceiver9（调用前缓存的方法的模块）：从方法的模块读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：ArchitectureBoundaryValidator.Scan()。
global::System.Reflection.Module callReceiver9 = method.Module;
                            // argumentValue7（按原顺序缓存的实参目标整数32的返回结果）：从目标整数32的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Module.ResolveMember(int, Type[], Type[]) 的参数。
int argumentValue7 = BitConverter.ToInt32(content, position);
                            // argumentValue8（按原顺序缓存的实参反射类型数组）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Module.ResolveMember(int, Type[], Type[]) 的参数、ArchitectureBoundaryValidator.Scan()。
global::System.Type[] argumentValue8;
                            if (type.IsGenericType)
                            {
                                argumentValue8 = type.GetGenericArguments();
                            }
                            else
                            {
                                argumentValue8 = null;
                            }

                            // selectedValue6（分支选出的反射类型数组）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Module.ResolveMember(int, Type[], Type[]) 的参数、ArchitectureBoundaryValidator.Scan()。
global::System.Type[] selectedValue6;
                            if (method.IsGenericMethod)
                            {
                                selectedValue6 = method.GetGenericArguments();
                            }
                            else
                            {
                                selectedValue6 = null;
                            }

                            // member（成员）：保存 Module.ResolveMember(int, Type[], Type[]) 的返回结果，供当前步骤使用。使用位置：作为 PhaseSevenCompilationBoundary.OutOfBoundsInvoke(Type, MemberInfo) 的参数、ArchitectureBoundaryValidator.Scan()。
MemberInfo member = callReceiver9.ResolveMember(argumentValue7, argumentValue8, selectedValue6);
                            // category（类别）：使用位置：ArchitectureBoundaryValidator.Scan()。
string category = null;
                            if (PhaseSevenCompilationBoundary.OutOfBoundsInvoke(type, member))
                            {
                                category = "规则越界调用";
                            }
                            else if (member is FieldInfo /* 中文含义：字段。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ field && stateType.Contains(field.DeclaringType.Name))
                            {
                                if (code == OpCodes.Stfld || code == OpCodes.Stsfld)
                                {
                                    category = "状态直接写入";
                                }
                                else if (code == OpCodes.Ldflda || code == OpCodes.Ldsflda)
                                {
                                    category = "状态地址访问";
                                }
                            }
                            else if (member.DeclaringType == typeof(Animator) && (member.Name.StartsWith("Set", StringComparison.Ordinal) || member.Name.StartsWith("Reset", StringComparison.Ordinal) || member.Name.StartsWith("CrossFade", StringComparison.Ordinal) || member.Name == "Play" || member.Name.StartsWith("set_", StringComparison.Ordinal)))
                            {
                                category = "动画写入";
                            }
                            else if ((member.DeclaringType == typeof(Transform) || member.DeclaringType == typeof(Rigidbody)) && (member.Name == "set_position" || member.Name == "set_rotation" || member.Name == "SetPositionAndRotation" || member.Name == "MovePosition" || member.Name == "MoveRotation" || member.Name == "set_linearVelocity" || member.Name == "set_angularVelocity"))
                            {
                                category = "运动写入";
                            }
                            else if (member.DeclaringType == typeof(VillagerController) && (member.Name == "BeginChase" || member.Name == "BeginAttack" || member.Name == "SetMovementMode" || member.Name == "Die"))
                            {
                                category = "角色动作调用";
                            }
                            else if (member.Name.StartsWith("add_", StringComparison.Ordinal) || member.Name.StartsWith("remove_", StringComparison.Ordinal))
                            {
                                category = "事件订阅边界";
                            }
                            else if (member.Name == "set_timeScale" && member.DeclaringType == typeof(Time))
                            {
                                category = "时间倍率写入";
                            }

                            if (category != null)
                            {
                                // callReceiver12（调用前缓存的当前边界记录集合）：从结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：ArchitectureBoundaryValidator.Scan()。
global::System.Collections.Generic.List<global::ArchitectureBoundaryValidator.BoundaryRecord> callReceiver12 = result;
                                // createdValue10（本次新建的边界记录）：保存本次创建实例，按原先顺序完成逐项初始化后交给后续使用方。使用位置：作为 List<ArchitectureBoundaryValidator.BoundaryRecord>.Add(ArchitectureBoundaryValidator.BoundaryRecord) 的参数、ArchitectureBoundaryValidator.Scan()。
global::ArchitectureBoundaryValidator.BoundaryRecord createdValue10 = new BoundaryRecord();
                                createdValue10.category = category;
                                createdValue10.invokeType = type.FullName;
                                createdValue10.invokeMethod = method.Name;
                                createdValue10.targetType = member.DeclaringType.FullName;
                                createdValue10.targetMember = member.Name;
                                if (sourceFile.TryGetValue(rootType, out string /* 中文含义：路径。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ path))
                                {
                                    createdValue10.sourceFile = path;
                                }
                                else
                                {
                                    createdValue10.sourceFile = "同程序集辅助类型，按类型与方法定位";
                                }

                                createdValue10.instructionOffset = startPoint;
                                createdValue10.crossType = rootType != member.DeclaringType;
                                callReceiver12.Add(createdValue10);
                            }
                        }

                        position += OperandByteCount(code.OperandType, content, position);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>OperandByteCount 的业务处理入口；本方法直接依赖 BitConverter.ToInt32(byte[], int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureBoundaryValidator.Scan()；下游（含内部回调体） BitConverter.ToInt32(byte[], int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">中文含义：当前运算项类型。使用位置：ArchitectureBoundaryValidator.OperandByteCount(OperandType, byte[], int)。</param>
    /// <param name="content">中文含义：内容。使用位置：作为 BitConverter.ToInt32(byte[], int) 的参数。</param>
    /// <param name="position">中文含义：位置。使用位置：作为 BitConverter.ToInt32(byte[], int) 的参数。</param>
    private static int OperandByteCount(OperandType type, byte[] content, int position)
    {
        switch (type)
        {
            case OperandType.InlineNone:
                return 0;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                return 1;
            case OperandType.InlineVar:
                return 2;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                return 8;
            case OperandType.InlineSwitch:
                return 4 + 4 * BitConverter.ToInt32(content, position);
            default:
                return 4;
        }
    }

    /// <summary>ValidateNewContract 的业务处理入口；本方法直接依赖 List&lt;string&gt;.Add(string)、PropertyInfo.GetSetMethod(bool)、Type.GetFields(BindingFlags)、Type.GetProperties()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） List&lt;string&gt;.Add(string)、PropertyInfo.GetSetMethod(bool)、Type.GetFields(BindingFlags)、Type.GetProperties()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failure">中文含义：失败。使用位置：ArchitectureBoundaryValidator.ValidateNewContract(List&lt;string&gt;)。</param>
    public static void ValidateNewContract(List<string> failure)
    {
        // snapshotType（快照类型）：使用位置：ArchitectureBoundaryValidator.ValidateNewContract(List<string>)。
Type[] snapshotType =
        {
            typeof(GameArchitecture.ObjectIdentity),
            typeof(GameArchitecture.ActionHandle),
            typeof(GameArchitecture.RequestContext),
            typeof(GameArchitecture.ActionRequest),
            typeof(GameArchitecture.ActionResult),
            typeof(GameArchitecture.MotionControlToken),
            typeof(GameArchitecture.TraversalToken),
            typeof(GameArchitecture.TraversalRequestResult),
            typeof(GameArchitecture.GameplayPermissionHandle),
            typeof(GameArchitecture.DamageRequest),
            typeof(GameArchitecture.DamageResult),
            typeof(GameArchitecture.CharacterStateSnapshot),
            typeof(GameArchitecture.ArchitectureDiagnosticEntry),
            typeof(EnemyStateSnapshot)
        };
        // type（反射操作使用的类型信息）：每轮从快照类型取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数、ArchitectureBoundaryValidator.ValidateNewContract(List<string>)。
foreach (Type type in snapshotType)
        {
            // field（字段）：每轮从取得字段集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数、ArchitectureBoundaryValidator.ValidateNewContract(List<string>)。
foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.IsInitOnly)
                {
                    failure.Add("架构契约存在可写存储：" + type.FullName + "." + field.Name);
                }
            }

            // property（属性）：每轮从取得属性集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数、ArchitectureBoundaryValidator.ValidateNewContract(List<string>)。
foreach (var property in type.GetProperties())
            {
                if (property.GetSetMethod(true) != null)
                {
                    failure.Add("架构契约存在设置器：" + type.FullName + "." + property.Name);
                }
            }
        }
    }

    /// <summary>迁移某个拥有者后登记其字段名单；历史未迁移字段不会被一并误判为新回归。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List&lt;string&gt;)、EnemyModuleBoundaryValidator.ValidateCode(List&lt;string&gt;)；下游（含内部回调体） HashSet&lt;string&gt;.Contains(string)、HashSet&lt;string&gt;.HashSet(IEnumerable&lt;string&gt;)、List&lt;string&gt;.Add(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="record">中文含义：当前边界记录集合。使用位置：ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)。</param>
    /// <param name="owner">中文含义：拥有者。使用位置：ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)。</param>
    /// <param name="fieldName">中文含义：字段名称。使用位置：作为 HashSet&lt;string&gt;.HashSet(IEnumerable&lt;string&gt;) 的参数。</param>
    /// <param name="failure">中文含义：失败。使用位置：ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)。</param>
    public static void ValidateStateExclusiveWrite(IEnumerable<BoundaryRecord> record, Type owner, IEnumerable<string> fieldName, List<string> failure)
    {
        // nameSingle（名称单一）：使用位置：ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable<ArchitectureBoundaryValidator.BoundaryRecord>, Type, IEnumerable<string>, List<string>)。
var nameSingle = new HashSet<string>(fieldName);
        // item（当前边界记录）：每轮从记录取得一个元素，仅代表当前这一项。使用位置：作为 HashSet<string>.Contains(string) 的参数、作为 List<string>.Add(string) 的参数、ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable<ArchitectureBoundaryValidator.BoundaryRecord>, Type, IEnumerable<string>, List<string>)。
foreach (var item in record)
        {
            if (item.targetType != owner.FullName || !nameSingle.Contains(item.targetMember) || !item.crossType || (item.category != "状态直接写入" && item.category != "状态地址访问"))
            {
                continue;
            }

            failure.Add("状态越界：" + item.sourceFile + "，" + item.invokeType + "." + item.invokeMethod + "，指令偏移=" + item.instructionOffset + "，目标=" + item.targetType + "." + item.targetMember);
        }
    }

    /// <summary>编辑器菜单调用入口 Export；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ArchitectureBoundaryValidator.BoundaryReport.BoundaryReport()、ArchitectureBoundaryValidator.Scan()、Debug.Log(object)、Directory.CreateDirectory(string)、File.WriteAllText(string, string)、InvalidOperationException.InvalidOperationException(string)、JsonUtility.ToJson(object, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/架构改造/导出编译后的边界清单")]
    public static void Export()
    {
        if (EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("编译尚未完成");
        }

        Directory.CreateDirectory("Docs/架构改造/检查结果");
        // record（当前边界记录集合）：保存 ArchitectureBoundaryValidator.Scan() 的返回结果，供当前步骤使用。使用位置：作为 Debug.Log(object) 的参数、作为 JsonUtility.ToJson(object, bool) 的参数。
var record = Scan();
        File.WriteAllText("Docs/架构改造/检查结果/编译后边界清单.json", JsonUtility.ToJson(new BoundaryReport { record = record }, true));
        Debug.Log("已导出编译后的边界清单，记录数：" + record.Count);
    }
}

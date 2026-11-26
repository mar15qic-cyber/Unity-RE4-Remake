using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>编辑器配置与校验模块 PhaseSevenCompilationBoundary；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "阶段七编译边界")]
// 运行时规则一旦依赖场景代码，应在集成校验和正式构建前同时报告。
public sealed class PhaseSevenCompilationBoundary : IPreprocessBuildWithReport
{
    /// <summary>中文含义：回调顺序。</summary>
    public int callbackOrder => 0;

    /// <summary>OnPreprocessBuild 的业务处理入口；本方法直接依赖 BuildFailedException.BuildFailedException(string)、List&lt;string&gt;.List()、PhaseSevenCompilationBoundary.Validate(List&lt;string&gt;)、string.Join(string, IEnumerable&lt;string&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） BuildFailedException.BuildFailedException(string)、List&lt;string&gt;.List()、PhaseSevenCompilationBoundary.Validate(List&lt;string&gt;)、string.Join(string, IEnumerable&lt;string&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="report">中文含义：报告。</param>
    public void OnPreprocessBuild(BuildReport report)
    {
        // 失败。
var failure = new List<string>();
        Validate(failure);
        if (failure.Count > 0)
        {
            throw new BuildFailedException(string.Join("\n", failure));
        }
    }

    /// <summary>编辑器配置与校验模块 AssemblyDefinition；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "阶段七编译边界+定义")]
    private sealed class AssemblyDefinition
    {
        /// <summary>名称。</summary>
        public string name;
        /// <summary>references 表示“引用集合”；includePlatforms 表示“包含平台集合”；precompiledReferences 表示“预编译引用集合”；optionalUnityReferences 表示“可空引擎引用集合”。</summary>
        public string[] references, includePlatforms, precompiledReferences, optionalUnityReferences;
        /// <summary>overrideReferences 表示“是否覆盖引用集合”；noEngineReferences 表示“是否无引擎引用集合”；autoReferenced 表示“是否自动被引用的”。</summary>
        public bool overrideReferences, noEngineReferences, autoReferenced;
    }

    /// <summary>ValidateDependency 的业务处理入口；本方法直接依赖 IEnumerable&lt;string&gt;.Except&lt;string&gt;(IEnumerable&lt;string&gt;)、List&lt;string&gt;.Add(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 Check(string, string, Type, bool, bool, params string[])；下游（含内部回调体） IEnumerable&lt;string&gt;.Except&lt;string&gt;(IEnumerable&lt;string&gt;)、List&lt;string&gt;.Add(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    /// <param name="actual">实际。</param>
    /// <param name="allow">允许。</param>
    /// <param name="failure">失败。</param>
    internal static void ValidateDependency(string path, IEnumerable<string> actual, IEnumerable<string> allow, List<string> failure)
    {
        // extra（额外）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数。
foreach (string extra in actual.Except(allow))
        {
            failure.Add(path + " 引用了未获准的程序集 " + extra);
        }

        // missing（缺失的）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数。
foreach (string missing in allow.Except(actual))
        {
            failure.Add(path + " 缺少登记的依赖 " + missing);
        }
    }

    /// <summary>OutOfBoundsInvoke 的业务处理入口；本方法直接依赖 string.StartsWith(string, StringComparison)、Type.IsAssignableFrom(Type)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ArchitectureBoundaryValidator.Scan()；下游（含内部回调体） string.StartsWith(string, StringComparison)、Type.IsAssignableFrom(Type)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="invokeActor">调用角色。</param>
    /// <param name="member">成员。</param>
    internal static bool OutOfBoundsInvoke(Type invokeActor, MemberInfo member)
    {
        // 程序集。
var assembly = invokeActor.Assembly;
        if (assembly != typeof(GameArchitecture.RequestContext).Assembly && assembly != typeof(WeaponRuntimeModule).Assembly && assembly != typeof(InventoryRulesModel).Assembly && assembly != typeof(MissionRulesModel).Assembly)
        {
            return false;
        }

        // 反射操作使用的类型信息。
var type = member.DeclaringType;
        if (type == null)
        {
            return false;
        }

        return type == typeof(Resources) || type == typeof(Time) || type == typeof(GameObject) || typeof(Component).IsAssignableFrom(type) || type.Namespace == "UnityEngine.SceneManagement" || (type.Namespace != null && type.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal)) || (type == typeof(UnityEngine.Object) && (member.Name.StartsWith("Find", StringComparison.Ordinal) || member.Name == "Instantiate" || member.Name.StartsWith("Destroy", StringComparison.Ordinal)));
    }

    /// <summary>Validate 的业务处理入口；本方法直接依赖 ArchitectureBoundaryValidator.Scan()、Check(string, string, Type, bool, bool, params string[])、IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;.Where&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;(Func&lt;ArchitectureBoundaryValidator.BoundaryRecord, bool&gt;)、List&lt;string&gt;.Add(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()、PhaseSevenCompilationBoundary.OnPreprocessBuild(BuildReport)；下游（含内部回调体） ArchitectureBoundaryValidator.Scan()、Check(string, string, Type, bool, bool, params string[])、IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;.Where&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;(Func&lt;ArchitectureBoundaryValidator.BoundaryRecord, bool&gt;)、List&lt;string&gt;.Add(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failure">失败。</param>
    public static void Validate(List<string> failure)
    {
        // 契约。
const string contract = "Game.Contracts";
        // 根节点。
const string root = "Assets/Scripts/架构/";
        Check(root + "公共契约.asmdef", contract, typeof(GameArchitecture.RequestContext), false, false);
        Check(root + "战斗规则/战斗规则.asmdef", "Game.CombatRules", typeof(WeaponRuntimeModule), false, true, contract);
        Check(root + "背包规则/背包规则.asmdef", "Game.InventoryRules", typeof(InventoryRulesModel), false, false, contract);
        Check(root + "任务规则/任务规则.asmdef", "Game.MissionRules", typeof(MissionRulesModel), false, true, contract);
        Check("Assets/Editor/规则测试/规则测试.asmdef", "Game.RuleTests", null, true, false, contract, "Game.CombatRules", "Game.InventoryRules", "Game.MissionRules");
        // item（当前边界记录）：每轮从扫描的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数。
foreach (var item in ArchitectureBoundaryValidator.Scan().Where( /* 中文含义：横向。 */x => x.category == "规则越界调用"))
        {
            failure.Add(item.sourceFile + " " + item.invokeType + "." + item.invokeMethod + " 调用了 " + item.targetType + "." + item.targetMember + " 指令偏移=" + item.instructionOffset);
        }

        /// <summary>Check 的业务处理入口；本方法直接依赖 Array.Empty&lt;string&gt;()、Assembly.GetName()、File.Exists(string)、File.ReadAllText(string)、JsonUtility.FromJson&lt;PhaseSevenCompilationBoundary.AssemblyDefinition&gt;(string)、List&lt;string&gt;.Add(string)、object.ReferenceEquals(object, object) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 PhaseSevenCompilationBoundary.Validate(List&lt;string&gt;)；下游（含内部回调体） Array.Empty&lt;string&gt;()、Assembly.GetName()、File.Exists(string)、File.ReadAllText(string)、JsonUtility.FromJson&lt;PhaseSevenCompilationBoundary.AssemblyDefinition&gt;(string)、List&lt;string&gt;.Add(string)、object.ReferenceEquals(object, object) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="path">路径。</param>
        /// <param name="nameValue">名称值。</param>
        /// <param name="ownership">归属。</param>
        /// <param name="test">是否验证。</param>
        /// <param name="prohibitEngine">是否禁止引擎。</param>
        /// <param name="dependency">依赖。</param>
        void Check(string path, string nameValue, Type ownership, bool test, bool prohibitEngine, params string[] dependency)
        {
            if (!File.Exists(path))
            {
                failure.Add("缺少程序集定义 " + path);
                return;
            }

            // 配置。
var configuration = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path));
            if (configuration.name != nameValue || (ownership != null && ownership.Assembly.GetName().Name != nameValue))
            {
                failure.Add(path + " 的声明或实际编译归属错误");
            }

            // 按原顺序缓存的实参路径。
string argumentValue3 = path;
            // 空值判断前缓存的配置的引用集合。
string[] availableValue1 = configuration.references;
            // 分支选出的可用值1。
string[] selectedValue2;
            if (!global::System.Object.ReferenceEquals(availableValue1, null))
            {
                selectedValue2 = availableValue1;
            }
            else
            {
                selectedValue2 = Array.Empty<string>();
            }

            ValidateDependency(argumentValue3, selectedValue2, dependency, failure);
            // 短路判断得到的分支判断。
bool conditionResult8;
            if (!configuration.overrideReferences)
            {
                conditionResult8 = true;
            }
            else
            {
                // 判空前缓存的配置的预编译引用集合。
string[] receiverValue4 = configuration.precompiledReferences;
                // 空值判断前缓存的接收者值4的长度。
int? availableValue6;
                if (!global::System.Object.ReferenceEquals(receiverValue4, null))
                {
                    availableValue6 = receiverValue4.Length;
                }
                else
                {
                    availableValue6 = default;
                }

                // 分支选出的可用值6的数值。
int selectedValue7;
                if (availableValue6.HasValue)
                {
                    selectedValue7 = availableValue6.Value;
                }
                else
                {
                    selectedValue7 = 0;
                }

                conditionResult8 = (selectedValue7) != 0;
            }

            if (conditionResult8 || configuration.noEngineReferences != prohibitEngine || configuration.autoReferenced == test)
            {
                failure.Add(path + " 的插件引用或引擎边界已改变");
            }

            // 按原顺序缓存的实参路径。
string argumentValue12 = path;
            // 空值判断前缓存的配置的包含平台集合。
string[] availableValue9 = configuration.includePlatforms;
            // 分支选出的可用值9。
string[] selectedValue10;
            if (!global::System.Object.ReferenceEquals(availableValue9, null))
            {
                selectedValue10 = availableValue9;
            }
            else
            {
                selectedValue10 = Array.Empty<string>();
            }

            // 按原顺序缓存的实参文本集合。
global::System.Collections.Generic.IEnumerable<string> argumentValue13 = selectedValue10;
            // 分支选出的空的返回结果。
string[] selectedValue11;
            if (test)
            {
                selectedValue11 = new[]
                {
                    "Editor"
                };
            }
            else
            {
                selectedValue11 = Array.Empty<string>();
            }

            ValidateDependency(argumentValue12, argumentValue13, selectedValue11, failure);
            // 按原顺序缓存的实参路径。
string argumentValue17 = path;
            // 空值判断前缓存的配置的可空引擎引用集合。
string[] availableValue14 = configuration.optionalUnityReferences;
            // 分支选出的可用值14。
string[] selectedValue15;
            if (!global::System.Object.ReferenceEquals(availableValue14, null))
            {
                selectedValue15 = availableValue14;
            }
            else
            {
                selectedValue15 = Array.Empty<string>();
            }

            // 按原顺序缓存的实参文本集合。
global::System.Collections.Generic.IEnumerable<string> argumentValue18 = selectedValue15;
            // 分支选出的空的返回结果。
string[] selectedValue16;
            if (test)
            {
                selectedValue16 = new[]
                {
                    "TestAssemblies"
                };
            }
            else
            {
                selectedValue16 = Array.Empty<string>();
            }

            ValidateDependency(argumentValue17, argumentValue18, selectedValue16, failure);
        }
    }
}

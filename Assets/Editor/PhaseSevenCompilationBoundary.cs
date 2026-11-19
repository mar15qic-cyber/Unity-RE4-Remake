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
        // failure（失败）：使用位置：作为 PhaseSevenCompilationBoundary.Validate(List<string>) 的参数、作为 string.Join(string, IEnumerable<string>) 的参数、PhaseSevenCompilationBoundary.OnPreprocessBuild(BuildReport)。
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
        /// <summary>中文含义：名称。使用位置：Check(string, string, Type, bool, bool, params string[])。</summary>
        public string name;
        /// <summary>中文含义：references 表示“引用集合”；includePlatforms 表示“包含平台集合”；precompiledReferences 表示“预编译引用集合”；optionalUnityReferences 表示“可空引擎引用集合”。使用位置：Check(string, string, Type, bool, bool, params string[])。；使用位置：Check(string, string, Type, bool, bool, params string[])。；使用位置：Check(string, string, Type, bool, bool, params string[])。；使用位置：Check(string, string, Type, bool, bool, params string[])。</summary>
        public string[] references, includePlatforms, precompiledReferences, optionalUnityReferences;
        /// <summary>中文含义：overrideReferences 表示“是否覆盖引用集合”；noEngineReferences 表示“是否无引擎引用集合”；autoReferenced 表示“是否自动被引用的”。使用位置：Check(string, string, Type, bool, bool, params string[])。；使用位置：Check(string, string, Type, bool, bool, params string[])。；使用位置：Check(string, string, Type, bool, bool, params string[])。</summary>
        public bool overrideReferences, noEngineReferences, autoReferenced;
    }

    /// <summary>ValidateDependency 的业务处理入口；本方法直接依赖 IEnumerable&lt;string&gt;.Except&lt;string&gt;(IEnumerable&lt;string&gt;)、List&lt;string&gt;.Add(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 Check(string, string, Type, bool, bool, params string[])；下游（含内部回调体） IEnumerable&lt;string&gt;.Except&lt;string&gt;(IEnumerable&lt;string&gt;)、List&lt;string&gt;.Add(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">中文含义：路径。使用位置：作为 List&lt;string&gt;.Add(string) 的参数。</param>
    /// <param name="actual">中文含义：实际。使用位置：作为 IEnumerable&lt;string&gt;.Except&lt;string&gt;(IEnumerable&lt;string&gt;) 的参数、PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable&lt;string&gt;, IEnumerable&lt;string&gt;, List&lt;string&gt;)。</param>
    /// <param name="allow">中文含义：允许。使用位置：作为 IEnumerable&lt;string&gt;.Except&lt;string&gt;(IEnumerable&lt;string&gt;) 的参数、PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable&lt;string&gt;, IEnumerable&lt;string&gt;, List&lt;string&gt;)。</param>
    /// <param name="failure">中文含义：失败。使用位置：PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable&lt;string&gt;, IEnumerable&lt;string&gt;, List&lt;string&gt;)。</param>
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
    /// <param name="invokeActor">中文含义：调用角色。使用位置：PhaseSevenCompilationBoundary.OutOfBoundsInvoke(Type, MemberInfo)。</param>
    /// <param name="member">中文含义：成员。使用位置：PhaseSevenCompilationBoundary.OutOfBoundsInvoke(Type, MemberInfo)。</param>
    internal static bool OutOfBoundsInvoke(Type invokeActor, MemberInfo member)
    {
        // assembly（程序集）：使用位置：PhaseSevenCompilationBoundary.OutOfBoundsInvoke(Type, MemberInfo)。
var assembly = invokeActor.Assembly;
        if (assembly != typeof(GameArchitecture.RequestContext).Assembly && assembly != typeof(WeaponRuntimeModule).Assembly && assembly != typeof(InventoryRulesModel).Assembly && assembly != typeof(MissionRulesModel).Assembly)
        {
            return false;
        }

        // type（反射操作使用的类型信息）：使用位置：作为 Type.IsAssignableFrom(Type) 的参数、PhaseSevenCompilationBoundary.OutOfBoundsInvoke(Type, MemberInfo)。
var type = member.DeclaringType;
        if (type == null)
        {
            return false;
        }

        return type == typeof(Resources) || type == typeof(Time) || type == typeof(GameObject) || typeof(Component).IsAssignableFrom(type) || type.Namespace == "UnityEngine.SceneManagement" || (type.Namespace != null && type.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal)) || (type == typeof(UnityEngine.Object) && (member.Name.StartsWith("Find", StringComparison.Ordinal) || member.Name == "Instantiate" || member.Name.StartsWith("Destroy", StringComparison.Ordinal)));
    }

    /// <summary>Validate 的业务处理入口；本方法直接依赖 ArchitectureBoundaryValidator.Scan()、Check(string, string, Type, bool, bool, params string[])、IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;.Where&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;(Func&lt;ArchitectureBoundaryValidator.BoundaryRecord, bool&gt;)、List&lt;string&gt;.Add(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()、PhaseSevenCompilationBoundary.OnPreprocessBuild(BuildReport)；下游（含内部回调体） ArchitectureBoundaryValidator.Scan()、Check(string, string, Type, bool, bool, params string[])、IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;.Where&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;(Func&lt;ArchitectureBoundaryValidator.BoundaryRecord, bool&gt;)、List&lt;string&gt;.Add(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failure">中文含义：失败。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable&lt;string&gt;, IEnumerable&lt;string&gt;, List&lt;string&gt;) 的参数、Check(string, string, Type, bool, bool, params string[])、PhaseSevenCompilationBoundary.Validate(List&lt;string&gt;)。</param>
    public static void Validate(List<string> failure)
    {
        // contract（契约）：使用位置：作为 Check(string, string, Type, bool, bool, params string[]) 的参数。
const string contract = "Game.Contracts";
        // root（根节点）：使用位置：作为 Check(string, string, Type, bool, bool, params string[]) 的参数。
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
        /// <param name="path">中文含义：路径。使用位置：作为 File.Exists(string) 的参数、作为 File.ReadAllText(string) 的参数、作为 List&lt;string&gt;.Add(string) 的参数 等共 4 处。</param>
        /// <param name="nameValue">中文含义：名称值。使用位置：Check(string, string, Type, bool, bool, params string[])。</param>
        /// <param name="ownership">中文含义：归属。使用位置：Check(string, string, Type, bool, bool, params string[])。</param>
        /// <param name="test">中文含义：是否验证。使用位置：Check(string, string, Type, bool, bool, params string[])。</param>
        /// <param name="prohibitEngine">中文含义：是否禁止引擎。使用位置：Check(string, string, Type, bool, bool, params string[])。</param>
        /// <param name="dependency">中文含义：依赖。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable&lt;string&gt;, IEnumerable&lt;string&gt;, List&lt;string&gt;) 的参数。</param>
        void Check(string path, string nameValue, Type ownership, bool test, bool prohibitEngine, params string[] dependency)
        {
            if (!File.Exists(path))
            {
                failure.Add("缺少程序集定义 " + path);
                return;
            }

            // configuration（配置）：保存 JsonUtility.FromJson<PhaseSevenCompilationBoundary.AssemblyDefinition>(string) 的返回结果，供当前步骤使用。使用位置：Check(string, string, Type, bool, bool, params string[])。
var configuration = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path));
            if (configuration.name != nameValue || (ownership != null && ownership.Assembly.GetName().Name != nameValue))
            {
                failure.Add(path + " 的声明或实际编译归属错误");
            }

            // argumentValue3（按原顺序缓存的实参路径）：从路径读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable<string>, IEnumerable<string>, List<string>) 的参数。
string argumentValue3 = path;
            // availableValue1（空值判断前缓存的配置的引用集合）：从配置的引用集合读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、Check(string, string, Type, bool, bool, params string[])。
string[] availableValue1 = configuration.references;
            // selectedValue2（分支选出的可用值1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable<string>, IEnumerable<string>, List<string>) 的参数、Check(string, string, Type, bool, bool, params string[])。
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
            // conditionResult8（短路判断得到的分支判断）：保存短路分支结果，未选中的分支不执行。使用位置：Check(string, string, Type, bool, bool, params string[])。
bool conditionResult8;
            if (!configuration.overrideReferences)
            {
                conditionResult8 = true;
            }
            else
            {
                // receiverValue4（判空前缓存的配置的预编译引用集合）：从配置的预编译引用集合读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、Check(string, string, Type, bool, bool, params string[])。
string[] receiverValue4 = configuration.precompiledReferences;
                // availableValue6（空值判断前缓存的接收者值4的长度）：保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：Check(string, string, Type, bool, bool, params string[])。
int? availableValue6;
                if (!global::System.Object.ReferenceEquals(receiverValue4, null))
                {
                    availableValue6 = receiverValue4.Length;
                }
                else
                {
                    availableValue6 = default;
                }

                // selectedValue7（分支选出的可用值6的数值）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：Check(string, string, Type, bool, bool, params string[])。
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

            // argumentValue12（按原顺序缓存的实参路径）：从路径读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable<string>, IEnumerable<string>, List<string>) 的参数。
string argumentValue12 = path;
            // availableValue9（空值判断前缓存的配置的包含平台集合）：从配置的包含平台集合读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、Check(string, string, Type, bool, bool, params string[])。
string[] availableValue9 = configuration.includePlatforms;
            // selectedValue10（分支选出的可用值9）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：Check(string, string, Type, bool, bool, params string[])。
string[] selectedValue10;
            if (!global::System.Object.ReferenceEquals(availableValue9, null))
            {
                selectedValue10 = availableValue9;
            }
            else
            {
                selectedValue10 = Array.Empty<string>();
            }

            // argumentValue13（按原顺序缓存的实参文本集合）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable<string>, IEnumerable<string>, List<string>) 的参数。
global::System.Collections.Generic.IEnumerable<string> argumentValue13 = selectedValue10;
            // selectedValue11（分支选出的空的返回结果）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable<string>, IEnumerable<string>, List<string>) 的参数、Check(string, string, Type, bool, bool, params string[])。
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
            // argumentValue17（按原顺序缓存的实参路径）：从路径读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable<string>, IEnumerable<string>, List<string>) 的参数。
string argumentValue17 = path;
            // availableValue14（空值判断前缓存的配置的可空引擎引用集合）：从配置的可空引擎引用集合读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、Check(string, string, Type, bool, bool, params string[])。
string[] availableValue14 = configuration.optionalUnityReferences;
            // selectedValue15（分支选出的可用值14）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：Check(string, string, Type, bool, bool, params string[])。
string[] selectedValue15;
            if (!global::System.Object.ReferenceEquals(availableValue14, null))
            {
                selectedValue15 = availableValue14;
            }
            else
            {
                selectedValue15 = Array.Empty<string>();
            }

            // argumentValue18（按原顺序缓存的实参文本集合）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable<string>, IEnumerable<string>, List<string>) 的参数。
global::System.Collections.Generic.IEnumerable<string> argumentValue18 = selectedValue15;
            // selectedValue16（分支选出的空的返回结果）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 PhaseSevenCompilationBoundary.ValidateDependency(string, IEnumerable<string>, IEnumerable<string>, List<string>) 的参数、Check(string, string, Type, bool, bool, params string[])。
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

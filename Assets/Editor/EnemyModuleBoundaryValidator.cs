using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>编辑器配置与校验模块 EnemyModuleBoundaryValidator；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "敌人模块边界校验")]
public static class EnemyModuleBoundaryValidator
{
    /// <summary>ValidateCode 的业务处理入口；本方法直接依赖 ArchitectureBoundaryValidator.Scan()、ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)、Assembly.GetType(string, bool)、IEnumerable&lt;FieldInfo&gt;.Select&lt;FieldInfo, string&gt;(Func&lt;FieldInfo, string&gt;)、IEnumerable&lt;Type&gt;.Any&lt;Type&gt;(Func&lt;Type, bool&gt;)、IEnumerable&lt;Type&gt;.Take&lt;Type&gt;(int)、List&lt;string&gt;.Add(string) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） ArchitectureBoundaryValidator.Scan()、ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)、Assembly.GetType(string, bool)、IEnumerable&lt;FieldInfo&gt;.Select&lt;FieldInfo, string&gt;(Func&lt;FieldInfo, string&gt;)、IEnumerable&lt;Type&gt;.Any&lt;Type&gt;(Func&lt;Type, bool&gt;)、IEnumerable&lt;Type&gt;.Take&lt;Type&gt;(int)、List&lt;string&gt;.Add(string) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failure">中文含义：失败。使用位置：作为 ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;) 的参数、EnemyModuleBoundaryValidator.ValidateCode(List&lt;string&gt;)。</param>
    public static void ValidateCode(List<string> failure)
    {
        // record（当前边界记录集合）：保存 ArchitectureBoundaryValidator.Scan() 的返回结果，供当前步骤使用。使用位置：作为 ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable<ArchitectureBoundaryValidator.BoundaryRecord>, Type, IEnumerable<string>, List<string>) 的参数、EnemyModuleBoundaryValidator.ValidateCode(List<string>)。
var record = ArchitectureBoundaryValidator.Scan();
        // owner（拥有者）：使用位置：EnemyModuleBoundaryValidator.ValidateCode(List<string>)。
Type[] owner =
        {
            typeof(EnemyCombatModule),
            typeof(EnemyPerceptionModule),
            typeof(EnemyActionCoordinator),
            typeof(EnemyAnimationAdapter),
            typeof(CharacterMotionExecutor)
        };
        // type（反射操作使用的类型信息）：每轮从拥有者取得一个元素，仅代表当前这一项。使用位置：作为 ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable<ArchitectureBoundaryValidator.BoundaryRecord>, Type, IEnumerable<string>, List<string>) 的参数。
foreach (var type in owner)
        {
            ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(record, type, type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select( /* 中文含义：字段。 */field => field.Name), failure);
        }

        // traversalAssembly（通行程序集）：使用位置：EnemyModuleBoundaryValidator.ValidateCode(List<string>)。
var traversalAssembly = typeof(HouseThreeTraversalGeometry).Assembly;
        // type（反射操作使用的类型信息）：每轮从取得类型的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable<ArchitectureBoundaryValidator.BoundaryRecord>, Type, IEnumerable<string>, List<string>) 的参数。
foreach (var type in new[]
        {
            traversalAssembly.GetType("DoorwayTraversalOccupancy", true),
            typeof(GameArchitecture.TraversalTokenRegistry)
        }

        )
        {
            ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(record, type, type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select( /* 中文含义：字段。 */field => field.Name), failure);
        }

        // item（当前边界记录）：每轮从记录取得一个元素，仅代表当前这一项。使用位置：作为 IEnumerable<Type>.Any<Type>(Func<Type, bool>) 的参数、作为 List<string>.Add(string) 的参数、EnemyModuleBoundaryValidator.ValidateCode(List<string>)。
foreach (var item in record)
        {
            if (item.category == "运动写入" && (item.invokeType == nameof(VillagerController) || item.invokeType == nameof(EnemyCombatModule) || item.invokeType == nameof(EnemyActionCoordinator) || item.invokeType == nameof(EnemyWindowTraversal) || item.invokeType == nameof(PathAgent)))
            {
                failure.Add("运动越界：" + item.sourceFile + "：" + item.invokeMethod + "，指令偏移=" + item.instructionOffset);
            }

            if (item.category == "角色动作调用" && item.invokeType == nameof(EnemyAIManager))
            {
                failure.Add("敌群越权切换动作：" + item.sourceFile + "：" + item.invokeMethod);
            }

            if (item.category != "动画写入")
            {
                continue;
            }

            if (item.invokeType == nameof(VillagerController) || item.invokeType == nameof(VillagerQTEParticipant) || item.invokeType == nameof(ChainsawManAnimationBridge) || owner.Take(3).Any( /* 中文含义：反射操作使用的类型信息。 */type => item.invokeType == type.FullName))
            {
                failure.Add("动画越界：" + item.sourceFile + "：" + item.invokeMethod + "，指令偏移=" + item.instructionOffset);
            }
        }

        // type（反射操作使用的类型信息）：每轮从拥有者取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数、EnemyModuleBoundaryValidator.ValidateCode(List<string>)。
foreach (var type in owner)
        {
            // field（字段）：每轮从取得字段集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数、作为 Type.IsAssignableFrom(Type) 的参数。
foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!typeof(Component).IsAssignableFrom(field.FieldType))
                {
                    failure.Add("模块公开了可写业务字段：" + type.Name + "." + field.Name);
                }
            }
        }

        // receiverValue1（判空前缓存的取得属性的返回结果）：从取得属性的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyModuleBoundaryValidator.ValidateCode(List<string>)。
global::System.Reflection.PropertyInfo receiverValue1 = typeof(EnemyContext).GetProperty("IsCrawl");
        // optionalValue2（条件访问得到的接收者值1的是否可以写入）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyModuleBoundaryValidator.ValidateCode(List<string>)。
bool? optionalValue2;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            optionalValue2 = receiverValue1.CanWrite;
        }
        else
        {
            optionalValue2 = default;
        }

        if (optionalValue2 != false)
        {
            failure.Add("运动上下文不应拥有第二份爬行姿态");
        }
    }

    /// <summary>ValidateReference 的业务处理入口；本方法直接依赖 Component.GetComponent&lt;ChainsawManAnimationBridge&gt;()、List&lt;string&gt;.Add(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） Component.GetComponent&lt;ChainsawManAnimationBridge&gt;()、List&lt;string&gt;.Add(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="character">中文含义：角色。使用位置：作为 List&lt;string&gt;.Add(string) 的参数、EnemyModuleBoundaryValidator.ValidateReference(VillagerController, string, List&lt;string&gt;)。</param>
    /// <param name="path">中文含义：路径。使用位置：作为 List&lt;string&gt;.Add(string) 的参数。</param>
    /// <param name="failure">中文含义：失败。使用位置：EnemyModuleBoundaryValidator.ValidateReference(VillagerController, string, List&lt;string&gt;)。</param>
    public static void ValidateReference(VillagerController character, string path, List<string> failure)
    {
        if (character.combatModule == null || character.combatModule.gameObject != character.gameObject || character.combatModule.host != character)
        {
            failure.Add(path + "：" + character.name + ".战斗模块");
        }

        if (character.perceptionModule == null || character.perceptionModule.gameObject != character.gameObject || character.perceptionModule.host != character)
        {
            failure.Add(path + "：" + character.name + ".感知模块");
        }

        if (character.actionCoordinator == null || character.actionCoordinator.gameObject != character.gameObject || character.actionCoordinator.host != character)
        {
            failure.Add(path + "：" + character.name + ".动作协调");
        }

        if (character.animationPresenter == null || character.animationPresenter.gameObject != character.gameObject || character.animationPresenter.host != character)
        {
            failure.Add(path + "：" + character.name + ".动画表现");
        }

        if (character.motionExecutor == null || character.motionExecutor.gameObject != character.gameObject || character.motionExecutor.host != character)
        {
            failure.Add(path + "：" + character.name + ".运动执行");
        }

        // chainsaw（电锯）：保存 Component.GetComponent<ChainsawManAnimationBridge>() 的返回结果，供当前步骤使用。使用位置：EnemyModuleBoundaryValidator.ValidateReference(VillagerController, string, List<string>)。
var chainsaw = character.GetComponent<ChainsawManAnimationBridge>();
        if (chainsaw != null && chainsaw.animationPresenter != character.animationPresenter)
        {
            failure.Add(path + "：" + character.name + "：电锯动画表现引用");
        }
    }
}

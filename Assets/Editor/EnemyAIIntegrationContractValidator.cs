using System;
using System.Collections.Generic;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>编辑器配置与校验模块 EnemyAIIntegrationContractValidator；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
public static class EnemyAIIntegrationContractValidator
{
    /// <summary>主要场景路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string MainScenePath = SceneManagerEx.MainScenePath;
    /// <summary>普通行为路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string OrdinaryBehaviorPath = "Assets/AI/敌人主行为树.asset";
    /// <summary>电锯行为路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string ChainsawBehaviorPath = "Assets/AI/ChainsawMan行为树.asset";
    /// <summary>普通控制器路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string OrdinaryControllerPath = "Assets/Animations/Enemy/Enemy.controller";
    /// <summary>电锯控制器路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string ChainsawControllerPath = "Assets/Animations/Enemy/ChainsawMan/ChainsawMan.controller";
    /// <summary>条件类型名称列表。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] ConditionTypeNames =
    {
        "IsEnemyDead",
        "HasPendingHit",
        "HasCrawlRequest",
        "HasStandUpRequest",
        "IsPlayerValid",
        "IsCombatEngaged",
        "HasChasePathFailure",
        "CanSeePlayer",
        "CannotSeePlayer",
        "IsInAttackRange",
        "IsAttackReady",
        "CanAttackNow",
        "CanCrawlAttackNow",
        "CanAttemptRearBiteNeckQTE",
        "CanAttemptLegBiteQTE",
        "CanAttemptChainsawExecutionQTE",
        "CanAttemptFrontGrabQTE",
        "ShouldChasePlayer",
        "ShouldInvestigatePlayer",
        "ShouldHoldCombatPosition",
        "IsPathValid",
        "HasArrived",
        "NeedsChainsawStart",
        "NeedsChainsawIntroTaunt"
    };
    /// <summary>条件化基础类型名称列表。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] ConditionalBaseTypeNames =
    {
        "CanAttemptEnemyQTEBase"
    };
    /// <summary>条件来源路径集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] ConditionSourcePaths =
    {
        "Assets/Scripts/Enemy/BehaviorDesigner/EnemyConditions.cs",
        "Assets/Scripts/Enemy/BehaviorDesigner/NeedsChainsawStart.cs"
    };
    /// <summary>正式预制件路径集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] FormalPrefabPaths =
    {
        "Assets/Prefab/Villager.prefab",
        "Assets/Prefab/Enemies/FemaleGanardEnemy.prefab",
        "Assets/Prefab/Enemies/FemaleGanard2Enemy.prefab",
        "Assets/Prefab/Enemies/ChainsawManEnemy.prefab",
        "Assets/Prefab/Enemies/SawManEnemy.prefab"
    };
    /// <summary>普通动画控制器参数集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] OrdinaryAnimatorParameters =
    {
        "MoveSpeed",
        "MoveX",
        "MoveY",
        "WALK",
        "ATK",
        "AttackIndex",
        "HIT",
        "HitDir",
        "HealthRatio",
        "DIE",
        "IsCrawl",
        "SearchMode",
        "IdleBlend",
        "TurnDir",
        "Stunned",
        "HitTrigger",
        "QTEAction",
        "CanTripOver",
        "StaggerTrigger",
        "StaggerDirection",
        "StaggerLevel"
    };
    /// <summary>电锯动画控制器参数集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] ChainsawAnimatorParameters =
    {
        "MoveSpeed",
        "MoveX",
        "MoveY",
        "WALK",
        "ATK",
        "AttackIndex",
        "HIT",
        "HitDir",
        "HealthRatio",
        "DIE",
        "IsCrawl",
        "SearchMode",
        "IdleBlend",
        "TurnDir",
        "HitTrigger",
        "ChainsawStart",
        "TauntTrigger",
        "CanTripOver",
        "QTEAction",
        "StaggerTrigger",
        "StaggerDirection",
        "StaggerLevel"
    };
    /// <summary>普通行为计数集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] OrdinaryBehaviorCounts =
    {
        "EnemyTimedHitStateSMB=17",
        "EnemyCrawlAttackStateSMB=1",
        "EnemyStandUpStateSMB=1",
        "EnemyQTEStateSMB=4"
    };
    /// <summary>电锯行为计数集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly string[] ChainsawBehaviorCounts =
    {
        "ChainsawStartSMB=1",
        "EnemyTimedAttackStateSMB=3",
        "ChainsawDoubleSwingAttackStateSMB=1",
        "EnemyTimedHitStateSMB=18",
        "EnemyQTEStateSMB=4"
    };
    /// <summary>编辑器菜单调用入口 Validate；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ArchitectureBoundaryValidator.ValidateNewContract(List&lt;string&gt;)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.Log(object)、Debug.LogError(object)、EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List&lt;string&gt;)、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[])、EnemyAIIntegrationContractValidator.ValidateConditionSources(List&lt;string&gt;) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/敌人智能/验证行为树与状态契约")]
    public static void Validate()
    {
        // 失败记录。
var failures = new List<string>();
        ArchitectureBoundaryValidator.ValidateNewContract(failures);
        ValidateLifeOwnership(failures);
        EnemyModuleBoundaryValidator.ValidateCode(failures);
        PlayerModuleBoundaryValidator.Validate(failures);
        PhaseFiveBoundaryValidator.Validate(failures);
        PhaseSevenCompilationBoundary.Validate(failures);
        // path（路径）：每轮从正式预制件路径集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的参数、作为 EnemyModuleBoundaryValidator.ValidateReference(VillagerController, string, List<string>) 的参数。
foreach (string path in FormalPrefabPaths)
        {
            // 预制件。
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            // actor（角色）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 EnemyModuleBoundaryValidator.ValidateReference(VillagerController, string, List<string>) 的参数。
foreach (var actor in prefab.GetComponentsInChildren<VillagerController>(true))
            {
                EnemyModuleBoundaryValidator.ValidateReference(actor, path, failures);
            }
        }

        ValidateConditionTypes(failures);
        ValidateConditionSources(failures);
        ValidateControllerInterface(failures);
        ValidateTypedAnimationNotifications(failures);
        ValidatePrefabBehaviorBindings(failures);
        ValidateAnimatorController(failures, OrdinaryControllerPath, OrdinaryAnimatorParameters, OrdinaryBehaviorCounts);
        ValidateAnimatorController(failures, ChainsawControllerPath, ChainsawAnimatorParameters, ChainsawBehaviorCounts);
        ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(failures);
        ValidateDoor03RouteIfMainSceneIsLoaded(failures);
        if (failures.Count == 0)
        {
            Debug.Log("[敌人智能契约] 全部通过。仅执行只读验证，未烘焙、未发布、未修改寻路图。");
            return;
        }

        // failure（失败）：每轮从失败记录取得一个元素，仅代表当前这一项。使用位置：作为 Debug.LogError(object) 的参数。
foreach (string failure in failures)
        {
            Debug.LogError("[敌人智能契约] " + failure);
        }
    }

    /// <summary>ValidateConditionTypes 的业务处理入口；本方法直接依赖 EnemyAIIntegrationContractValidator.ValidateConditionalType(List&lt;string&gt;, string, string, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） EnemyAIIntegrationContractValidator.ValidateConditionalType(List&lt;string&gt;, string, string, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    private static void ValidateConditionTypes(List<string> failures)
    {
        // typeName（类型名称）：每轮从条件类型名称列表取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateConditionalType(List<string>, string, string, bool) 的参数。
foreach (string typeName in ConditionTypeNames)
        {
            ValidateConditionalType(failures, "Assets/Scripts/Enemy/BehaviorDesigner", typeName, false);
        }

        // typeName（类型名称）：每轮从条件化基础类型名称列表取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateConditionalType(List<string>, string, string, bool) 的参数。
foreach (string typeName in ConditionalBaseTypeNames)
        {
            ValidateConditionalType(failures, "Assets/Scripts/Enemy/BehaviorDesigner/EnemyConditions.cs", typeName, true);
        }
    }

    /// <summary>ValidateLifeOwnership 的业务处理入口；本方法直接依赖 ArchitectureBoundaryValidator.Scan()、ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、GameObject.GetComponentsInChildren&lt;VillagerController&gt;(bool)、List&lt;string&gt;.Add(string)、object.ReferenceEquals(object, object)、Type.GetField(string) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） ArchitectureBoundaryValidator.Scan()、ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、GameObject.GetComponentsInChildren&lt;VillagerController&gt;(bool)、List&lt;string&gt;.Add(string)、object.ReferenceEquals(object, object)、Type.GetField(string) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    private static void ValidateLifeOwnership(List<string> failures)
    {
        // 短路判断得到的分支判断。
bool conditionResult3;
        if (typeof(VillagerController).GetField("currentHealth") != null)
        {
            conditionResult3 = true;
        }
        else
        {
            // 判空前缓存的取得属性的返回结果。
global::System.Reflection.PropertyInfo receiverValue1 = typeof(VillagerController).GetProperty("currentHealth");
            // 条件访问得到的接收者值1的是否可以写入。
bool? optionalValue2;
            if (!global::System.Object.ReferenceEquals(receiverValue1, null))
            {
                optionalValue2 = receiverValue1.CanWrite;
            }
            else
            {
                optionalValue2 = default;
            }

            conditionResult3 = optionalValue2 != false;
        }

        if (conditionResult3)
        {
            failures.Add("敌人血量兼容入口必须只读并转发生命模块");
        }

        ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(ArchitectureBoundaryValidator.Scan(), typeof(CharacterHealthModule), new[] { "currentHealthValue", "deathFact", "initialized" }, failures);
        // path（路径）：每轮从正式预制件路径集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的参数、作为 List<string>.Add(string) 的参数。
foreach (string path in FormalPrefabPaths)
        {
            // 预制件。
GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            // actor（角色）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数、EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List<string>)。
foreach (VillagerController actor in prefab.GetComponentsInChildren<VillagerController>(true))
            {
                if (actor.healthModule == null || actor.healthModule.gameObject != actor.gameObject)
                {
                    failures.Add("资源路径=" + path + "，对象=" + actor.name + "，生命模块引用缺失或绑定到其他对象");
                }
            }
        }
    }

    /// <summary>ValidateConditionalType 的业务处理入口；本方法直接依赖 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、EnemyAIIntegrationContractValidator.FindType(string)、Type.IsAssignableFrom(Type)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.ValidateConditionTypes(List&lt;string&gt;)；下游（含内部回调体） EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、EnemyAIIntegrationContractValidator.FindType(string)、Type.IsAssignableFrom(Type)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    /// <param name="resourcePath">资源路径。</param>
    /// <param name="typeName">类型名称。</param>
    /// <param name="abstractType">是否抽象类型。</param>
    private static void ValidateConditionalType(List<string> failures, string resourcePath, string typeName, bool abstractType)
    {
        // 反射操作使用的类型信息。
Type type = FindType(typeName);
        if (type == null)
        {
            AddFailure(failures, resourcePath, typeName, "类型不存在");
            return;
        }

        if (!typeof(Conditional).IsAssignableFrom(type))
        {
            AddFailure(failures, resourcePath, typeName, "未属于 Conditional 类型族");
        }

        if (abstractType && !type.IsAbstract)
        {
            AddFailure(failures, resourcePath, typeName, "抽象条件基类未声明为抽象类型");
        }
    }

    /// <summary>ValidateConditionSources 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;TextAsset&gt;(string)、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;TextAsset&gt;(string)、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    private static void ValidateConditionSources(List<string> failures)
    {
        // path（路径）：每轮从条件来源路径集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<TextAsset>(string) 的参数、作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
foreach (string path in ConditionSourcePaths)
        {
            // 来源文字资源。
TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (source == null)
            {
                AddFailure(failures, path, "条件源文件", "源文件无法读取");
                continue;
            }

            if (source.text.Contains("TaskStatus.Running"))
            {
                AddFailure(failures, path, "条件任务", "条件实现不得返回 Running");
            }
        }
    }

    /// <summary>ValidateControllerInterface 的业务处理入口；本方法直接依赖 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、PropertyInfo.GetSetMethod(bool)、Type.GetMethod(string, BindingFlags)、Type.GetProperty(string, BindingFlags)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、PropertyInfo.GetSetMethod(bool)、Type.GetMethod(string, BindingFlags)、Type.GetProperty(string, BindingFlags)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    private static void ValidateControllerInterface(List<string> failures)
    {
        // 反射操作使用的类型信息。
Type type = typeof(VillagerController);
        // 路径。
const string path = "Assets/Scripts/Enemy/VillagerController.cs";
        // 状态属性。
PropertyInfo stateProperty = type.GetProperty("CurrentState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (stateProperty == null)
        {
            AddFailure(failures, path, "VillagerController.CurrentState", "状态属性不存在");
        }
        else
        {
            // 写入器。
MethodInfo setter = stateProperty.GetSetMethod(true);
            if (setter != null && setter.IsPublic)
            {
                AddFailure(failures, path, "VillagerController.CurrentState", "状态属性仍有公开设置器");
            }
        }

        // 状态写入器。
MethodInfo stateSetter = type.GetMethod("SetObservableState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (stateSetter == null)
        {
            AddFailure(failures, path, "VillagerController.SetObservableState", "状态写入入口不存在");
        }
        else if (stateSetter.IsPublic)
        {
            AddFailure(failures, path, "VillagerController.SetObservableState", "状态写入入口不得公开");
        }

        // 所需方法集合。
string[] requiredMethods =
        {
            "IsNavigationTraversalProtected",
            "CompleteTraversalAndResumeChase",
            "NotifyAttackStateMachineExited",
            "NotifyCrawlAttackStateExited",
            "NotifyHitStateMachineExited",
            "NotifyStandUpStateExited"
        };
        // methodName（方法名称）：每轮从所需方法集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数、作为 Type.GetMethod(string, BindingFlags) 的参数、作为 Type.GetProperty(string, BindingFlags) 的参数。
foreach (string methodName in requiredMethods)
        {
            // 属性。
PropertyInfo property = type.GetProperty(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // 方法。
MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null && method == null)
            {
                AddFailure(failures, path, "VillagerController." + methodName, "接口不存在");
            }
        }
    }

    /// <summary>ValidateTypedAnimationNotifications 的业务处理入口；本方法直接依赖 EnemyAIIntegrationContractValidator.ValidateTypedNotificationSource(List&lt;string&gt;, string, string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） EnemyAIIntegrationContractValidator.ValidateTypedNotificationSource(List&lt;string&gt;, string, string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    private static void ValidateTypedAnimationNotifications(List<string> failures)
    {
        ValidateTypedNotificationSource(failures, "Assets/Scripts/Enemy/AttackStateMachineSMB.cs", "OnAttackStateMachineExit", "NotifyAttackStateMachineExited");
        ValidateTypedNotificationSource(failures, "Assets/Scripts/Enemy/HitReactStateMachineSMB.cs", "OnHitStateMachineExit", "NotifyHitStateMachineExited");
        ValidateTypedNotificationSource(failures, "Assets/Scripts/Enemy/EnemyCrawlAttackStateSMB.cs", "OnCrawlAttackExit", "NotifyCrawlAttackStateExited");
        ValidateTypedNotificationSource(failures, "Assets/Scripts/Enemy/EnemyStandUpStateSMB.cs", "OnStandUpStateExit", "NotifyStandUpStateExited");
    }

    /// <summary>ValidateTypedNotificationSource 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;TextAsset&gt;(string)、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.ValidateTypedAnimationNotifications(List&lt;string&gt;)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;TextAsset&gt;(string)、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    /// <param name="path">路径。</param>
    /// <param name="legacyMessage">旧版消息。</param>
    /// <param name="typedMethod">指定类型的方法。</param>
    private static void ValidateTypedNotificationSource(List<string> failures, string path, string legacyMessage, string typedMethod)
    {
        // 来源文字资源。
TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if (source == null)
        {
            AddFailure(failures, path, typedMethod, "状态行为源文件无法读取");
            return;
        }

        if (source.text.Contains("\"" + legacyMessage + "\""))
        {
            AddFailure(failures, path, typedMethod, "关键退出事实仍使用字符串消息");
        }

        if (!source.text.Contains(typedMethod))
        {
            AddFailure(failures, path, typedMethod, "未直接调用类型化通知");
        }
    }

    /// <summary>ValidatePrefabBehaviorBindings 的业务处理入口；本方法直接依赖 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、GameObject.GetComponentInChildren&lt;BehaviorTree&gt;(bool)、PrefabUtility.LoadPrefabContents(string)、PrefabUtility.UnloadPrefabContents(GameObject)、string.Equals(string, string, StringComparison)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、GameObject.GetComponentInChildren&lt;BehaviorTree&gt;(bool)、PrefabUtility.LoadPrefabContents(string)、PrefabUtility.UnloadPrefabContents(GameObject)、string.Equals(string, string, StringComparison)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    private static void ValidatePrefabBehaviorBindings(List<string> failures)
    {
        // 当前遍历索引。
for (int i = 0; i < FormalPrefabPaths.Length; i++)
        {
            // 预制件路径。
string prefabPath = FormalPrefabPaths[i];
            // 预期。
string expected;
            if (i < 3)
            {
                expected = OrdinaryBehaviorPath;
            }
            else
            {
                expected = ChainsawBehaviorPath;
            }

            // 根游戏对象。
GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    AddFailure(failures, prefabPath, "BehaviorTree", "预制件无法加载");
                    continue;
                }

                // 行为树。
BehaviorTree tree = root.GetComponentInChildren<BehaviorTree>(true);
                if (tree == null)
                {
                    AddFailure(failures, prefabPath, "BehaviorTree", "正式预制件缺少行为树组件");
                    continue;
                }

                // 实际。
string actual;
                if (tree.ExternalBehavior == null)
                {
                    actual = string.Empty;
                }
                else
                {
                    actual = AssetDatabase.GetAssetPath(tree.ExternalBehavior);
                }

                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    // 按原顺序缓存的实参失败记录。
global::System.Collections.Generic.List<string> argumentValue8 = failures;
                    // 按原顺序缓存的实参预制件路径。
string argumentValue9 = prefabPath;
                    // 按原顺序缓存的实参文字“BehaviorTree”。
string argumentValue10 = "BehaviorTree";
                    // 计算前缓存的左侧文本选择。
string leftOperand7 = "行为树绑定错误，期望=" + expected + "，实际=";
                    // 分支选出的文字“空”。
string selectedValue6;
                    if (string.IsNullOrEmpty(actual))
                    {
                        selectedValue6 = "空";
                    }
                    else
                    {
                        selectedValue6 = actual;
                    }

                    AddFailure(argumentValue8, argumentValue9, argumentValue10, leftOperand7 + (selectedValue6));
                }
            }
            catch (Exception /* 中文含义：捕获的异常信息。保存当前 catch 捕获的异常，供记录和错误处理使用。 */ exception)
            {
                AddFailure(failures, prefabPath, "BehaviorTree", "读取预制件失败: " + exception.Message);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }

    /// <summary>ValidateAnimatorController 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、Dictionary&lt;string, int&gt;.ContainsKey(string)、Dictionary&lt;string, int&gt;.Dictionary()、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)、int.Parse(string)、string.Split(char, StringSplitOptions)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、Dictionary&lt;string, int&gt;.ContainsKey(string)、Dictionary&lt;string, int&gt;.Dictionary()、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)、int.Parse(string)、string.Split(char, StringSplitOptions)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    /// <param name="path">路径。</param>
    /// <param name="requiredParameters">所需参数集合。</param>
    /// <param name="expectedBehaviourCounts">预期行为计数集合。</param>
    private static void ValidateAnimatorController(List<string> failures, string path, string[] requiredParameters, string[] expectedBehaviourCounts)
    {
        // 动画控制器资源。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            AddFailure(failures, path, "AnimatorController", "动画控制器无法加载");
            return;
        }

        // parameter（参数）：每轮从所需参数集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
foreach (string parameter in requiredParameters)
        {
            // 是否已找到。
bool found = false;
            // actual（实际）：每轮从控制器的参数集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
foreach (AnimatorControllerParameter actual in controller.parameters)
            {
                if (actual.name == parameter)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                AddFailure(failures, path, "AnimatorController", "缺少动画参数 " + parameter);
            }
        }

        // 计数集合。
var counts = new Dictionary<string, int>();
        // layer（图层）：每轮从控制器的图层集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>) 的参数。
foreach (AnimatorControllerLayer layer in controller.layers)
        {
            CountStateBehaviours(layer.stateMachine, counts);
        }

        // expected（预期）：每轮从预期行为计数集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
foreach (string expected in expectedBehaviourCounts)
        {
            // 部件集合。
string[] parts = expected.Split('=');
            // 实际。
int actual;
            if (counts.ContainsKey(parts[0]))
            {
                actual = counts[parts[0]];
            }
            else
            {
                actual = 0;
            }

            // 预期值。
int expectedValue = int.Parse(parts[1]);
            if (actual != expectedValue)
            {
                AddFailure(failures, path, parts[0], "关键状态行为数量变化，期望=" + expectedValue + "，实际=" + actual);
            }
        }
    }

    /// <summary>CountStateBehaviours 的业务处理入口；本方法直接依赖 Dictionary&lt;string, int&gt;.ContainsKey(string)、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)、object.GetType()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)、EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List&lt;string&gt;)、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[])；下游（含内部回调体） Dictionary&lt;string, int&gt;.ContainsKey(string)、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateMachine">状态机。</param>
    /// <param name="counts">计数集合。</param>
    private static void CountStateBehaviours(AnimatorStateMachine stateMachine, Dictionary<string, int> counts)
    {
        if (stateMachine == null)
        {
            return;
        }

        // child（子级）：每轮从状态机的状态集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>)。
foreach (ChildAnimatorState child in stateMachine.states)
        {
            // behaviour（行为）：每轮从子级的状态的行为集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>)。
foreach (StateMachineBehaviour behaviour in child.state.behaviours)
            {
                // 类型名称。
string typeName = behaviour.GetType().Name;
                // 赋值前保存的目标计数集合。
global::System.Collections.Generic.Dictionary<string, int> assignmentReceiver12 = counts;
                // 赋值前保存的索引类型名称。
string assignmentIndex13 = typeName;
                if (counts.ContainsKey(typeName))
                {
                    assignmentReceiver12[assignmentIndex13] = counts[typeName] + 1;
                }
                else
                {
                    assignmentReceiver12[assignmentIndex13] = 1;
                }
            }
        }

        // child（子级）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>) 的参数。
foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
        {
            CountStateBehaviours(child.stateMachine, counts);
        }
    }

    /// <summary>ValidateActiveControllerDoesNotUseLegacyAttackBehaviour 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、Dictionary&lt;string, int&gt;.ContainsKey(string)、Dictionary&lt;string, int&gt;.Dictionary()、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、Dictionary&lt;string, int&gt;.ContainsKey(string)、Dictionary&lt;string, int&gt;.Dictionary()、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    private static void ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List<string> failures)
    {
        // 路径集合。
string[] paths =
        {
            OrdinaryControllerPath,
            ChainsawControllerPath
        };
        // path（路径）：每轮从路径集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的参数、作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
foreach (string path in paths)
        {
            // 动画控制器资源。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                continue;
            }

            // 计数集合。
var counts = new Dictionary<string, int>();
            // layer（图层）：每轮从控制器的图层集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>) 的参数。
foreach (AnimatorControllerLayer layer in controller.layers)
            {
                CountStateBehaviours(layer.stateMachine, counts);
            }

            if (counts.ContainsKey("VillagerAttackSMB") && counts["VillagerAttackSMB"] > 0)
            {
                AddFailure(failures, path, "VillagerAttackSMB", "活动动画控制器不得依赖旧攻击状态行为");
            }
        }
    }

    /// <summary>ValidateDoor03RouteIfMainSceneIsLoaded 的业务处理入口；本方法直接依赖 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、SceneManager.GetActiveScene()、VillageNavigationBaker.ValidatePublished，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、SceneManager.GetActiveScene()、VillageNavigationBaker.ValidatePublished。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    private static void ValidateDoor03RouteIfMainSceneIsLoaded(List<string> failures)
    {
        if (EditorSceneManager.GetActiveScene().path != MainScenePath)
        {
            return;
        }

        // 是否有效。
        bool valid = VillageNavigationBaker.ValidatePublished(EditorSceneManager.GetActiveScene(), out string report);
        if (!valid)
        {
            AddFailure(failures, MainScenePath, "VillageNavigationBaker.ValidatePublished", "三号房运行通路只读验证未通过：\n" + report);
        }
    }

    /// <summary>FindType 的业务处理入口；本方法直接依赖 AppDomain.GetAssemblies()、Assembly.GetType(string)、Assembly.GetTypes()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.ValidateConditionalType(List&lt;string&gt;, string, string, bool)；下游（含内部回调体） AppDomain.GetAssemblies()、Assembly.GetType(string)、Assembly.GetTypes()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="typeName">类型名称。</param>
    private static Type FindType(string typeName)
    {
        // assembly（程序集）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIIntegrationContractValidator.FindType(string)。
foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // 直接。
Type direct = assembly.GetType(typeName);
            if (direct != null)
            {
                return direct;
            }

            try
            {
                // type（反射操作使用的类型信息）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIIntegrationContractValidator.FindType(string)。
foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name == typeName)
                    {
                        return type;
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
            }
        }

        return null;
    }

    /// <summary>AddFailure 的业务处理入口；本方法直接依赖 List&lt;string&gt;.Add(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List&lt;string&gt;)、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[])、EnemyAIIntegrationContractValidator.ValidateConditionalType(List&lt;string&gt;, string, string, bool)、EnemyAIIntegrationContractValidator.ValidateConditionSources(List&lt;string&gt;)、EnemyAIIntegrationContractValidator.ValidateControllerInterface(List&lt;string&gt;) 等共 8 处；下游（含内部回调体） List&lt;string&gt;.Add(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">失败记录。</param>
    /// <param name="resourcePath">资源路径。</param>
    /// <param name="typeName">类型名称。</param>
    /// <param name="contract">契约。</param>
    private static void AddFailure(List<string> failures, string resourcePath, string typeName, string contract)
    {
        failures.Add("资源路径=" + resourcePath + "，类型=" + typeName + "，失败契约=" + contract);
    }
}

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
    /// <summary>中文含义：主要场景路径。编译期常量，保持既有值与消费者映射。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数、EnemyAIIntegrationContractValidator.ValidateDoor03RouteIfMainSceneIsLoaded(List&lt;string&gt;)。</summary>
    private const string MainScenePath = SceneManagerEx.MainScenePath;
    /// <summary>中文含义：普通行为路径。编译期常量，保持既有值与消费者映射。使用位置：EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List&lt;string&gt;)。</summary>
    private const string OrdinaryBehaviorPath = "Assets/AI/敌人主行为树.asset";
    /// <summary>中文含义：电锯行为路径。编译期常量，保持既有值与消费者映射。使用位置：EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List&lt;string&gt;)。</summary>
    private const string ChainsawBehaviorPath = "Assets/AI/ChainsawMan行为树.asset";
    /// <summary>中文含义：普通控制器路径。编译期常量，保持既有值与消费者映射。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[]) 的参数、EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List&lt;string&gt;)。</summary>
    private const string OrdinaryControllerPath = "Assets/Animations/Enemy/Enemy.controller";
    /// <summary>中文含义：电锯控制器路径。编译期常量，保持既有值与消费者映射。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[]) 的参数、EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List&lt;string&gt;)。</summary>
    private const string ChainsawControllerPath = "Assets/Animations/Enemy/ChainsawMan/ChainsawMan.controller";
    /// <summary>中文含义：条件类型名称列表。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIIntegrationContractValidator.ValidateConditionTypes(List&lt;string&gt;)。</summary>
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
    /// <summary>中文含义：条件化基础类型名称列表。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIIntegrationContractValidator.ValidateConditionTypes(List&lt;string&gt;)。</summary>
    private static readonly string[] ConditionalBaseTypeNames =
    {
        "CanAttemptEnemyQTEBase"
    };
    /// <summary>中文含义：条件来源路径集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIIntegrationContractValidator.ValidateConditionSources(List&lt;string&gt;)。</summary>
    private static readonly string[] ConditionSourcePaths =
    {
        "Assets/Scripts/Enemy/BehaviorDesigner/EnemyConditions.cs",
        "Assets/Scripts/Enemy/BehaviorDesigner/NeedsChainsawStart.cs"
    };
    /// <summary>中文含义：正式预制件路径集合。只读字段，初始化后不由普通写入口替换。使用位置：EnemyAIIntegrationContractValidator.Validate()、EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List&lt;string&gt;)、EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List&lt;string&gt;)。</summary>
    private static readonly string[] FormalPrefabPaths =
    {
        "Assets/Prefab/Villager.prefab",
        "Assets/Prefab/Enemies/FemaleGanardEnemy.prefab",
        "Assets/Prefab/Enemies/FemaleGanard2Enemy.prefab",
        "Assets/Prefab/Enemies/ChainsawManEnemy.prefab",
        "Assets/Prefab/Enemies/SawManEnemy.prefab"
    };
    /// <summary>中文含义：普通动画控制器参数集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[]) 的参数。</summary>
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
    /// <summary>中文含义：电锯动画控制器参数集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[]) 的参数。</summary>
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
    /// <summary>中文含义：普通行为计数集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[]) 的参数。</summary>
    private static readonly string[] OrdinaryBehaviorCounts =
    {
        "EnemyTimedHitStateSMB=17",
        "EnemyCrawlAttackStateSMB=1",
        "EnemyStandUpStateSMB=1",
        "EnemyQTEStateSMB=4"
    };
    /// <summary>中文含义：电锯行为计数集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[]) 的参数。</summary>
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
        // failures（失败记录）：使用位置：作为 ArchitectureBoundaryValidator.ValidateNewContract(List<string>) 的参数、作为 EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List<string>) 的参数、作为 EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[]) 的参数 等共 16 处。
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
            // prefab（预制件）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.Validate()。
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateConditionalType(List&lt;string&gt;, string, string, bool) 的参数。</param>
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 ArchitectureBoundaryValidator.ValidateStateExclusiveWrite(IEnumerable&lt;ArchitectureBoundaryValidator.BoundaryRecord&gt;, Type, IEnumerable&lt;string&gt;, List&lt;string&gt;) 的参数、EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List&lt;string&gt;)。</param>
    private static void ValidateLifeOwnership(List<string> failures)
    {
        // conditionResult3（短路判断得到的分支判断）：保存短路分支结果，未选中的分支不执行。使用位置：EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List<string>)。
bool conditionResult3;
        if (typeof(VillagerController).GetField("currentHealth") != null)
        {
            conditionResult3 = true;
        }
        else
        {
            // receiverValue1（判空前缓存的取得属性的返回结果）：从取得属性的返回结果读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List<string>)。
global::System.Reflection.PropertyInfo receiverValue1 = typeof(VillagerController).GetProperty("currentHealth");
            // optionalValue2（条件访问得到的接收者值1的是否可以写入）：保存条件访问的结果，接收者为空时保持默认空值，非空时才执行原成员访问。使用位置：EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List<string>)。
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
            // prefab（预制件）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateLifeOwnership(List<string>)。
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    /// <param name="resourcePath">中文含义：资源路径。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    /// <param name="typeName">中文含义：类型名称。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数、作为 EnemyAIIntegrationContractValidator.FindType(string) 的参数。</param>
    /// <param name="abstractType">中文含义：是否抽象类型。使用位置：EnemyAIIntegrationContractValidator.ValidateConditionalType(List&lt;string&gt;, string, string, bool)。</param>
    private static void ValidateConditionalType(List<string> failures, string resourcePath, string typeName, bool abstractType)
    {
        // type（反射操作使用的类型信息）：保存 EnemyAIIntegrationContractValidator.FindType(string) 的返回结果，供当前步骤使用。使用位置：作为 Type.IsAssignableFrom(Type) 的参数、EnemyAIIntegrationContractValidator.ValidateConditionalType(List<string>, string, string, bool)。
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    private static void ValidateConditionSources(List<string> failures)
    {
        // path（路径）：每轮从条件来源路径集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<TextAsset>(string) 的参数、作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
foreach (string path in ConditionSourcePaths)
        {
            // source（来源文字资源）：保存 AssetDatabase.LoadAssetAtPath<TextAsset>(string) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateConditionSources(List<string>)。
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    private static void ValidateControllerInterface(List<string> failures)
    {
        // type（反射操作使用的类型信息）：使用位置：EnemyAIIntegrationContractValidator.ValidateControllerInterface(List<string>)。
Type type = typeof(VillagerController);
        // path（路径）：使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
const string path = "Assets/Scripts/Enemy/VillagerController.cs";
        // stateProperty（状态属性）：保存 Type.GetProperty(string, BindingFlags) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateControllerInterface(List<string>)。
PropertyInfo stateProperty = type.GetProperty("CurrentState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (stateProperty == null)
        {
            AddFailure(failures, path, "VillagerController.CurrentState", "状态属性不存在");
        }
        else
        {
            // setter（写入器）：保存 PropertyInfo.GetSetMethod(bool) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateControllerInterface(List<string>)。
MethodInfo setter = stateProperty.GetSetMethod(true);
            if (setter != null && setter.IsPublic)
            {
                AddFailure(failures, path, "VillagerController.CurrentState", "状态属性仍有公开设置器");
            }
        }

        // stateSetter（状态写入器）：保存 Type.GetMethod(string, BindingFlags) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateControllerInterface(List<string>)。
MethodInfo stateSetter = type.GetMethod("SetObservableState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (stateSetter == null)
        {
            AddFailure(failures, path, "VillagerController.SetObservableState", "状态写入入口不存在");
        }
        else if (stateSetter.IsPublic)
        {
            AddFailure(failures, path, "VillagerController.SetObservableState", "状态写入入口不得公开");
        }

        // requiredMethods（所需方法集合）：使用位置：EnemyAIIntegrationContractValidator.ValidateControllerInterface(List<string>)。
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
            // property（属性）：保存 Type.GetProperty(string, BindingFlags) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateControllerInterface(List<string>)。
PropertyInfo property = type.GetProperty(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            // method（方法）：保存 Type.GetMethod(string, BindingFlags) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateControllerInterface(List<string>)。
MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null && method == null)
            {
                AddFailure(failures, path, "VillagerController." + methodName, "接口不存在");
            }
        }
    }

    /// <summary>ValidateTypedAnimationNotifications 的业务处理入口；本方法直接依赖 EnemyAIIntegrationContractValidator.ValidateTypedNotificationSource(List&lt;string&gt;, string, string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） EnemyAIIntegrationContractValidator.ValidateTypedNotificationSource(List&lt;string&gt;, string, string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.ValidateTypedNotificationSource(List&lt;string&gt;, string, string, string) 的参数。</param>
    private static void ValidateTypedAnimationNotifications(List<string> failures)
    {
        ValidateTypedNotificationSource(failures, "Assets/Scripts/Enemy/AttackStateMachineSMB.cs", "OnAttackStateMachineExit", "NotifyAttackStateMachineExited");
        ValidateTypedNotificationSource(failures, "Assets/Scripts/Enemy/HitReactStateMachineSMB.cs", "OnHitStateMachineExit", "NotifyHitStateMachineExited");
        ValidateTypedNotificationSource(failures, "Assets/Scripts/Enemy/EnemyCrawlAttackStateSMB.cs", "OnCrawlAttackExit", "NotifyCrawlAttackStateExited");
        ValidateTypedNotificationSource(failures, "Assets/Scripts/Enemy/EnemyStandUpStateSMB.cs", "OnStandUpStateExit", "NotifyStandUpStateExited");
    }

    /// <summary>ValidateTypedNotificationSource 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;TextAsset&gt;(string)、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、string.Contains(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.ValidateTypedAnimationNotifications(List&lt;string&gt;)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;TextAsset&gt;(string)、EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)、string.Contains(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    /// <param name="path">中文含义：路径。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;TextAsset&gt;(string) 的参数、作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    /// <param name="legacyMessage">中文含义：旧版消息。使用位置：作为 string.Contains(string) 的参数。</param>
    /// <param name="typedMethod">中文含义：指定类型的方法。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数、作为 string.Contains(string) 的参数。</param>
    private static void ValidateTypedNotificationSource(List<string> failures, string path, string legacyMessage, string typedMethod)
    {
        // source（来源文字资源）：保存 AssetDatabase.LoadAssetAtPath<TextAsset>(string) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateTypedNotificationSource(List<string>, string, string, string)。
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数、EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List&lt;string&gt;)。</param>
    private static void ValidatePrefabBehaviorBindings(List<string> failures)
    {
        // i（当前遍历索引）：使用位置：EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List<string>)。
for (int i = 0; i < FormalPrefabPaths.Length; i++)
        {
            // prefabPath（预制件路径）：使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数、作为 PrefabUtility.LoadPrefabContents(string) 的参数、EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List<string>)。
string prefabPath = FormalPrefabPaths[i];
            // expected（预期）：使用位置：作为 string.Equals(string, string, StringComparison) 的参数、EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List<string>)。
string expected;
            if (i < 3)
            {
                expected = OrdinaryBehaviorPath;
            }
            else
            {
                expected = ChainsawBehaviorPath;
            }

            // root（根游戏对象）：使用位置：作为 PrefabUtility.UnloadPrefabContents(GameObject) 的参数、EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List<string>)。
GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    AddFailure(failures, prefabPath, "BehaviorTree", "预制件无法加载");
                    continue;
                }

                // tree（行为树）：保存 GameObject.GetComponentInChildren<BehaviorTree>(bool) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List<string>)。
BehaviorTree tree = root.GetComponentInChildren<BehaviorTree>(true);
                if (tree == null)
                {
                    AddFailure(failures, prefabPath, "BehaviorTree", "正式预制件缺少行为树组件");
                    continue;
                }

                // actual（实际）：使用位置：作为 string.Equals(string, string, StringComparison) 的参数、作为 string.IsNullOrEmpty(string) 的参数、EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List<string>)。
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
                    // argumentValue8（按原顺序缓存的实参失败记录）：从失败记录读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
global::System.Collections.Generic.List<string> argumentValue8 = failures;
                    // argumentValue9（按原顺序缓存的实参预制件路径）：从预制件路径读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
string argumentValue9 = prefabPath;
                    // argumentValue10（按原顺序缓存的实参文字“BehaviorTree”）：从文字“BehaviorTree”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
string argumentValue10 = "BehaviorTree";
                    // leftOperand7（计算前缓存的左侧文本选择）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
string leftOperand7 = "行为树绑定错误，期望=" + expected + "，实际=";
                    // selectedValue6（分支选出的文字“空”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数、EnemyAIIntegrationContractValidator.ValidatePrefabBehaviorBindings(List<string>)。
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    /// <param name="path">中文含义：路径。使用位置：作为 AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string) 的参数、作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    /// <param name="requiredParameters">中文含义：所需参数集合。使用位置：EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[])。</param>
    /// <param name="expectedBehaviourCounts">中文含义：预期行为计数集合。使用位置：EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[])。</param>
    private static void ValidateAnimatorController(List<string> failures, string path, string[] requiredParameters, string[] expectedBehaviourCounts)
    {
        // controller（动画控制器资源）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            AddFailure(failures, path, "AnimatorController", "动画控制器无法加载");
            return;
        }

        // parameter（参数）：每轮从所需参数集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
foreach (string parameter in requiredParameters)
        {
            // found（是否已找到）：使用位置：EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
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

        // counts（计数集合）：使用位置：作为 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>) 的参数、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
var counts = new Dictionary<string, int>();
        // layer（图层）：每轮从控制器的图层集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>) 的参数。
foreach (AnimatorControllerLayer layer in controller.layers)
        {
            CountStateBehaviours(layer.stateMachine, counts);
        }

        // expected（预期）：每轮从预期行为计数集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
foreach (string expected in expectedBehaviourCounts)
        {
            // parts（部件集合）：保存 string.Split(char, StringSplitOptions) 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<string, int>.ContainsKey(string) 的参数、作为 Dictionary<string, int>.this[string] 的参数、作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数 等共 4 处。
string[] parts = expected.Split('=');
            // actual（实际）：使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
int actual;
            if (counts.ContainsKey(parts[0]))
            {
                actual = counts[parts[0]];
            }
            else
            {
                actual = 0;
            }

            // expectedValue（预期值）：保存 int.Parse(string) 的返回结果，供当前步骤使用。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List<string>, string, string[], string[])。
int expectedValue = int.Parse(parts[1]);
            if (actual != expectedValue)
            {
                AddFailure(failures, path, parts[0], "关键状态行为数量变化，期望=" + expectedValue + "，实际=" + actual);
            }
        }
    }

    /// <summary>CountStateBehaviours 的业务处理入口；本方法直接依赖 Dictionary&lt;string, int&gt;.ContainsKey(string)、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)、object.GetType()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)、EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List&lt;string&gt;)、EnemyAIIntegrationContractValidator.ValidateAnimatorController(List&lt;string&gt;, string, string[], string[])；下游（含内部回调体） Dictionary&lt;string, int&gt;.ContainsKey(string)、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)、object.GetType()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="stateMachine">中文含义：状态机。使用位置：EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)。</param>
    /// <param name="counts">中文含义：计数集合。使用位置：作为 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;) 的参数、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary&lt;string, int&gt;)。</param>
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
                // typeName（类型名称）：使用位置：作为 Dictionary<string, int>.ContainsKey(string) 的参数、作为 Dictionary<string, int>.this[string] 的参数、EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>)。
string typeName = behaviour.GetType().Name;
                // assignmentReceiver12（赋值前保存的目标计数集合）：从计数集合读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>)。
global::System.Collections.Generic.Dictionary<string, int> assignmentReceiver12 = counts;
                // assignmentIndex13（赋值前保存的索引类型名称）：从类型名称读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, int>.this[string] 的参数。
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    private static void ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List<string> failures)
    {
        // paths（路径集合）：使用位置：EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List<string>)。
string[] paths =
        {
            OrdinaryControllerPath,
            ChainsawControllerPath
        };
        // path（路径）：每轮从路径集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的参数、作为 EnemyAIIntegrationContractValidator.AddFailure(List<string>, string, string, string) 的参数。
foreach (string path in paths)
        {
            // controller（动画控制器资源）：保存 AssetDatabase.LoadAssetAtPath<AnimatorController>(string) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List<string>)。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                continue;
            }

            // counts（计数集合）：使用位置：作为 EnemyAIIntegrationContractValidator.CountStateBehaviours(AnimatorStateMachine, Dictionary<string, int>) 的参数、EnemyAIIntegrationContractValidator.ValidateActiveControllerDoesNotUseLegacyAttackBehaviour(List<string>)。
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
    /// <param name="failures">中文含义：失败记录。使用位置：作为 EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string) 的参数。</param>
    private static void ValidateDoor03RouteIfMainSceneIsLoaded(List<string> failures)
    {
        if (EditorSceneManager.GetActiveScene().path != MainScenePath)
        {
            return;
        }

        // valid（是否有效）：保存 VillageNavigationBaker.ValidatePublished 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.ValidateDoor03RouteIfMainSceneIsLoaded(List<string>)。
        bool valid = VillageNavigationBaker.ValidatePublished(EditorSceneManager.GetActiveScene(), out string report);
        if (!valid)
        {
            AddFailure(failures, MainScenePath, "VillageNavigationBaker.ValidatePublished", "三号房运行通路只读验证未通过：\n" + report);
        }
    }

    /// <summary>FindType 的业务处理入口；本方法直接依赖 AppDomain.GetAssemblies()、Assembly.GetType(string)、Assembly.GetTypes()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.ValidateConditionalType(List&lt;string&gt;, string, string, bool)；下游（含内部回调体） AppDomain.GetAssemblies()、Assembly.GetType(string)、Assembly.GetTypes()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="typeName">中文含义：类型名称。使用位置：作为 Assembly.GetType(string) 的参数、EnemyAIIntegrationContractValidator.FindType(string)。</param>
    private static Type FindType(string typeName)
    {
        // assembly（程序集）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：EnemyAIIntegrationContractValidator.FindType(string)。
foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            // direct（直接）：保存 Assembly.GetType(string) 的返回结果，供当前步骤使用。使用位置：EnemyAIIntegrationContractValidator.FindType(string)。
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
    /// <param name="failures">中文含义：失败记录。使用位置：EnemyAIIntegrationContractValidator.AddFailure(List&lt;string&gt;, string, string, string)。</param>
    /// <param name="resourcePath">中文含义：资源路径。使用位置：作为 List&lt;string&gt;.Add(string) 的参数。</param>
    /// <param name="typeName">中文含义：类型名称。使用位置：作为 List&lt;string&gt;.Add(string) 的参数。</param>
    /// <param name="contract">中文含义：契约。使用位置：作为 List&lt;string&gt;.Add(string) 的参数。</param>
    private static void AddFailure(List<string> failures, string resourcePath, string typeName, string contract)
    {
        failures.Add("资源路径=" + resourcePath + "，类型=" + typeName + "，失败契约=" + contract);
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using GameArchitecture;
using Object = UnityEngine.Object;

/// <summary>只接入已经验收的制作源，生成无渲染、无业务组件的隔离采样骨架。</summary>
public static class KnifePreviewIntegrationBuilder
{
    /// <summary>来源目录。编译期常量，保持既有值与消费者映射。</summary>
    private const string SourceDirectory = "Assets/Animations/左臂整体重构/";
    /// <summary>骨架路径。编译期常量，保持既有值与消费者映射。</summary>
    private const string SkeletonPath = "Assets/Animations/小刀战斗/KnifePoseSkeleton.prefab";
    /// <summary>Configure 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、EditorUtility.SetDirty(Object)、Enum.ToString()、GameObject.GetComponentsInChildren&lt;Component&gt;(bool)、InvalidOperationException.InvalidOperationException(string) 等共 20 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatBuilder.Build()、KnifePreviewIntegrationBuilder.Integrate()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、AssetDatabase.LoadAssetAtPath&lt;AnimatorController&gt;(string)、AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、EditorUtility.SetDirty(Object)、Enum.ToString()、GameObject.GetComponentsInChildren&lt;Component&gt;(bool)、InvalidOperationException.InvalidOperationException(string) 等共 20 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="configuration">配置。</param>
    public static void Configure(KnifeCombatConfiguration configuration)
    {
        // 来源游戏对象。
GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/角色/里昂/LeonR4R.fbx");
        // 骨架。
GameObject skeleton = Object.Instantiate(source);
        try
        {
            skeleton.name = "小刀姿态采样骨架";
            skeleton.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            skeleton.transform.localScale = Vector3.one;
            // component（组件）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数、KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)。
foreach (Component component in skeleton.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is Animator)
                {
                    continue;
                }

                Object.DestroyImmediate(component);
            }

            configuration.poseSkeleton = PrefabUtility.SaveAsPrefabAsset(skeleton, SkeletonPath);
        }
        finally
        {
            Object.DestroyImmediate(skeleton);
        }

        configuration.previewIdle = Read("低位待机");
        configuration.previewLocomotionIdle = LeonKnifeAnimationPreview.Read("低位待机");
        configuration.previewLocomotion = new AnimationClip[6];
        // 方向集合。
string[] directions =
        {
            "F",
            "B",
            "L",
            "R",
            "F"
        };
        // 当前元素索引。
for (int index = 0; index < 5; index++)
        {
            configuration.previewLocomotion[index] = LeonKnifeAnimationPreview.ReadSourceMaterial("Knife_St_LocomotionSet_IPC/Walk/Act_Walk_" + directions[index] + "_IPC");
        }

        configuration.previewLocomotion[5] = LeonKnifeAnimationPreview.ReadSourceMaterial("Knife_St_LocomotionSet/IdleTurns/Act_Idle_Turn90R_IPC");
        // 动画控制器资源。
AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(KnifeCombatBuilder.controllerPath);
        // 小刀状态集合。
AnimatorStateMachine knifeStates = null;
        // child（子级）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)。
foreach (ChildAnimatorStateMachine child in controller.layers[AnimatorLayerID.WeaponAction].stateMachine.stateMachines)
        {
            if (child.stateMachine.name == "小刀重构")
            {
                knifeStates = child.stateMachine;
                break;
            }
        }

        // action（动作）：每轮从配置的动作动画片段取得一个元素，仅代表当前这一项。使用位置：作为 InvalidOperationException.InvalidOperationException(string) 的参数、KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)。
foreach (KnifeActionConfiguration action in configuration.actionClip)
        {
            if (knifeStates != null)
            {
                // state（当前子级动画控制器状态）：每轮从小刀状态集合的状态集合取得一个元素，仅代表当前这一项。使用位置：KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)。
foreach (ChildAnimatorState state in knifeStates.states)
                {
                    if (state.state.name == action.action.ToString())
                    {
                        state.state.name = action.StateName;
                    }
                }
            }

            action.sourceStart = 0;
            // 名称。
string name;
            switch (action.action)
            {
                case KnifeAction.EnterGuard:
                    name = "进入防守";
                    break;
                case KnifeAction.HoldGuard:
                case KnifeAction.ThrustRecovery:
                    name = "防守保持";
                    break;
                case KnifeAction.LowerGuard:
                    name = "放下防守";
                    break;
                case KnifeAction.PrimarySlash:
                    name = "右向左主挥砍";
                    break;
                case KnifeAction.Backslash:
                    name = "左向右回切";
                    break;
                case KnifeAction.PrimarySlashRecovery:
                    name = "主挥砍回收";
                    break;
                case KnifeAction.BackslashRecovery:
                    name = "回切回收";
                    break;
                case KnifeAction.ThrustOne:
                    name = "快速前刺循环";
                    break;
                case KnifeAction.ThrustTwo:
                    name = "快速前刺循环";
                    action.sourceStart = .5f;
                    break;
                case KnifeAction.CloseRangeNormal:
                case KnifeAction.CloseRangePerfect:
                    name = "近身完美弹反";
                    break;
                case KnifeAction.HighNormal:
                case KnifeAction.HighPerfect:
                    name = "高位截挡";
                    break;
                case KnifeAction.OutwardDeflectionNormal:
                case KnifeAction.OutwardDeflectionPerfect:
                    name = "斜上外拨弹反";
                    break;
                default:
                    throw new InvalidOperationException("未知小刀动作：" + action.action);
            }

            action.sourceClip = Read(name);
        }

        // preview（预览）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var preview = new LeonKnifeAnimationPreview.PreviewCharacter())
        {
            preview.bakePreviewMesh = false;
            // 小刀。
Transform knife = preview.AnimatorValue.GetBoneTransform(HumanBodyBones.RightHand).GetChild(preview.AnimatorValue.GetBoneTransform(HumanBodyBones.RightHand).childCount - 1);
            // action（动作）：每轮从配置的动作动画片段取得一个元素，仅代表当前这一项。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 Mathf.CeilToInt(float) 的参数、作为 Mathf.Min(float, float) 的参数 等共 4 处。
foreach (KnifeActionConfiguration action in configuration.actionClip)
            {
                if (!action.timing.HasHit)
                {
                    continue;
                }

                // 数量。
int count = Mathf.CeilToInt(action.timing.duration * 120) + 1;
                action.bladeBaseTrajectory = new Vector3[count];
                action.bladeTipTrajectory = new Vector3[count];
                action.sampleInterval = 1f / 120;
                // 帧。
for (int frame = 0; frame < count; frame++)
                {
                    // 时间。
float time = Mathf.Min(action.timing.duration, frame / 120f);
                    preview.Sample(action.sourceClip, action.sourceStart + time, 0, time);
                    action.bladeBaseTrajectory[frame] = knife.TransformPoint(configuration.bladeBaseLocal);
                    action.bladeTipTrajectory[frame] = knife.TransformPoint(configuration.bladeTipLocal);
                }
            }
        }

        EditorUtility.SetDirty(configuration);
        EditorUtility.SetDirty(controller);
    }

    /// <summary>Read 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、FileNotFoundException.FileNotFoundException(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;AnimationClip&gt;(string)、FileNotFoundException.FileNotFoundException(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    private static AnimationClip Read(string name)
    {
        // 动画片段。
AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceDirectory + name + ".anim");
        if (clip == null)
        {
            throw new FileNotFoundException("缺少已经验收的制作源：" + name);
        }

        return clip;
    }

    /// <summary>Integrate 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;KnifeCombatConfiguration&gt;(string)、AssetDatabase.SaveAssets()、InvalidOperationException.InvalidOperationException(string)、KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;KnifeCombatConfiguration&gt;(string)、AssetDatabase.SaveAssets()、InvalidOperationException.InvalidOperationException(string)、KnifePreviewIntegrationBuilder.Configure(KnifeCombatConfiguration)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string Integrate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请先退出播放模式。");
        }

        // 配置。
KnifeCombatConfiguration configuration = AssetDatabase.LoadAssetAtPath<KnifeCombatConfiguration>(KnifeCombatBuilder.configurationPath);
        Configure(configuration);
        AssetDatabase.SaveAssets();
        return "已接入制作源、隔离采样骨架与一致的刀刃轨迹；制作源和已验收网页保持原样。";
    }
}

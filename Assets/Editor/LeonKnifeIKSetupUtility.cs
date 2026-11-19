using System.Linq;
using RootMotion.FinalIK;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>编辑器配置与校验模块 LeonKnifeIKSetupUtility；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
public static class LeonKnifeIKSetupUtility
{
    /// <summary>中文含义：小刀待机路径。编译期常量，保持既有值与消费者映射。使用位置：作为 AssetDatabase.LoadAllAssetsAtPath(string) 的参数。</summary>
    private const string KnifeIdlePath = "Assets/素材库/动画/Knife_MocapAnimPack_1.1/AnimationsFBX/Knife_Stand_Style(St)/Knife_St_IdleSet/Knife_St_Idle_00.fbx";
    /// <summary>编辑器菜单调用入口 RebuildKnifeIK；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AnimationMode.BeginSampling()、AnimationMode.EndSampling()、AnimationMode.InAnimationMode()、AnimationMode.SampleAnimationClip(GameObject, AnimationClip, float)、AnimationMode.StartAnimationMode()、AnimationMode.StopAnimationMode()、Animator.GetBoneTransform(HumanBodyBones) 等共 36 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/重建小刀反向动力学")]
    public static void RebuildKnifeIK()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[小刀反向动力学配置] 请先退出运行模式。");
            return;
        }

        // rig（绑定）：保存 LeonKnifeIKSetupUtility.FindSceneRig() 的返回结果，供当前步骤使用。使用位置：作为 Debug.Log(object, Object) 的参数、作为 Debug.LogError(object, Object) 的参数、作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数 等共 9 处。
LeonAnimRig rig = FindSceneRig();
        if (rig == null)
        {
            Debug.LogError("[小刀反向动力学配置] 当前场景中找不到里昂动画约束组件。");
            return;
        }

        // animator（动画控制器）：保存 Component.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Animator animator = rig.GetComponent<Animator>();
        // fingerController（手指控制器）：保存 Component.GetComponent<LeonFingerIKController>() 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
LeonFingerIKController fingerController = rig.GetComponent<LeonFingerIKController>();
        // visualController（视觉表现控制器）：保存 Component.GetComponent<LeonWeaponVisualController>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeIKSetupUtility.RebuildKnifeIK()。
LeonWeaponVisualController visualController = rig.GetComponent<LeonWeaponVisualController>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("[小刀反向动力学配置] 里昂动画器不存在或不是人形骨骼。", rig);
            return;
        }

        if (rig.rifleRightIK == null || rig.rifleLeftIK == null)
        {
            Debug.LogError("[小刀反向动力学配置] 缺少步枪双手约束，无法复制同构层级。", rig);
            return;
        }

        // knifeVisualRoot（小刀视觉表现根节点）：使用位置：作为 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform knifeVisualRoot;
        if (visualController != null)
        {
            knifeVisualRoot = visualController.GetWeaponVisualRoot(WeaponType.Knife);
        }
        else
        {
            knifeVisualRoot = null;
        }

        if (knifeVisualRoot == null)
        {
            Debug.LogError("[小刀反向动力学配置] 找不到手腕下的真实刀模，无法建立刀具握柄目标。", rig);
            return;
        }

        // idleClip（待机动画片段）：保存 IEnumerable<AnimationClip>.FirstOrDefault<AnimationClip>(Func<AnimationClip, bool>) 的返回结果，供当前步骤使用。使用位置：作为 AnimationMode.SampleAnimationClip(GameObject, AnimationClip, float) 的参数、作为 Mathf.Min(float, float) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
AnimationClip idleClip = AssetDatabase.LoadAllAssetsAtPath(KnifeIdlePath).OfType<AnimationClip>().FirstOrDefault( /* 中文含义：动画片段。 */clip => !clip.name.StartsWith("__preview__"));
        if (idleClip == null)
        {
            Debug.LogError("[小刀反向动力学配置] 找不到小刀站立待机动画。", rig);
            return;
        }

        // rightHand（右手）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        // leftHand（左手）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        // leftForearm（左侧前臂）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform leftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        if (rightHand == null || leftHand == null || leftForearm == null)
        {
            Debug.LogError("[小刀反向动力学配置] 人形骨骼缺少手腕或左前臂。", rig);
            return;
        }

        // player（玩家）：使用位置：作为 AnimationMode.SampleAnimationClip(GameObject, AnimationClip, float) 的参数、作为 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的参数、作为 LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion) 的参数 等共 5 处。
Transform player = rig.transform;
        // rightLocalPosition（右侧局部位置）：使用位置：作为 LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Vector3 rightLocalPosition;
        // rightLocalRotation（右侧局部旋转）：使用位置：作为 LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Quaternion rightLocalRotation;
        // leftLocalPosition（左侧局部位置）：使用位置：作为 LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Vector3 leftLocalPosition;
        // leftLocalRotation（左侧局部旋转）：使用位置：作为 LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Quaternion leftLocalRotation;
        // hintLocalPosition（提示局部位置）：使用位置：作为 Transform.TransformPoint(Vector3) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Vector3 hintLocalPosition;
        AnimationMode.StartAnimationMode();
        try
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(player.gameObject, idleClip, Mathf.Min(0.2f, idleClip.length * 0.25f));
            AnimationMode.EndSampling();
            rightLocalPosition = player.InverseTransformPoint(rightHand.position);
            rightLocalRotation = Quaternion.Inverse(player.rotation) * rightHand.rotation;
            leftLocalPosition = player.InverseTransformPoint(leftHand.position);
            leftLocalRotation = Quaternion.Inverse(player.rotation) * leftHand.rotation;
            // hintWorldPosition（提示世界位置）：使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数。
Vector3 hintWorldPosition = leftForearm.position - player.right * 0.2f - player.forward * 0.04f;
            hintLocalPosition = player.InverseTransformPoint(hintWorldPosition);
            if (fingerController != null)
            {
                fingerController.CreatePersistentTargetsForWeapon(WeaponType.Knife);
            }

            // legacyKnifeRoot（旧版小刀根节点）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform legacyKnifeRoot = player.Find("小刀");
            // legacyFingerTargets（旧版手指目标列表）：使用位置：LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform legacyFingerTargets;
            if (legacyKnifeRoot != null)
            {
                legacyFingerTargets = legacyKnifeRoot.Find("手指目标_Knife");
            }
            else
            {
                legacyFingerTargets = null;
            }

            if (legacyFingerTargets != null)
            {
                legacyFingerTargets.name = "旧版手指目标_Knife_停用";
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
            }
        }

        // targetsRoot（目标列表根节点）：保存 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的参数。
Transform targetsRoot = FindOrCreateChild(player, "IK_Targets");
        // knifeRoot（小刀根节点）：保存 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的参数。
Transform knifeRoot = FindOrCreateChild(targetsRoot, "KnifeIKTargets");
        // rightTarget（右侧目标）：保存 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeIKSetupUtility.GetOrAddLimbIK(GameObject) 的参数、作为 LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform rightTarget = FindOrCreateChild(knifeRoot, "KnifeRightHandTarget");
        // leftTarget（左侧目标）：保存 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeIKSetupUtility.GetOrAddLimbIK(GameObject) 的参数、作为 LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform leftTarget = FindOrCreateChild(knifeRoot, "KnifeLeftHandTarget");
        // leftHint（左侧提示）：保存 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform leftHint = FindOrCreateChild(knifeRoot, "KnifeLeftElbowHint");
        // rightGrip（右侧握持）：保存 LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
Transform rightGrip = FindOrCreateChild(knifeVisualRoot, "KnifeGrip_Right");
        SetPoseFromPlayerSpace(player, rightTarget, rightLocalPosition, rightLocalRotation);
        SetPoseFromPlayerSpace(player, rightGrip, rightLocalPosition, rightLocalRotation);
        SetPoseFromPlayerSpace(player, leftTarget, leftLocalPosition, leftLocalRotation);
        leftHint.position = player.TransformPoint(hintLocalPosition);
        leftHint.rotation = player.rotation;
        // rightIK（右侧反向动力学）：保存 LeonKnifeIKSetupUtility.GetOrAddLimbIK(GameObject) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.CopySerialized(Object, Object) 的参数、作为 EditorUtility.SetDirty(Object) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
LimbIK rightIK = GetOrAddLimbIK(rightTarget.gameObject);
        // leftIK（左侧反向动力学）：保存 LeonKnifeIKSetupUtility.GetOrAddLimbIK(GameObject) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.CopySerialized(Object, Object) 的参数、作为 EditorUtility.SetDirty(Object) 的参数、LeonKnifeIKSetupUtility.RebuildKnifeIK()。
LimbIK leftIK = GetOrAddLimbIK(leftTarget.gameObject);
        EditorUtility.CopySerialized(rig.rifleRightIK, rightIK);
        EditorUtility.CopySerialized(rig.rifleLeftIK, leftIK);
        rightIK.solver.IKPosition = rightTarget.position;
        rightIK.solver.IKRotation = rightTarget.rotation;
        rightIK.solver.target = rightGrip;
        rightIK.solver.IKPositionWeight = 0f;
        rightIK.solver.IKRotationWeight = 0f;
        leftIK.solver.IKPosition = leftTarget.position;
        leftIK.solver.IKRotation = leftTarget.rotation;
        leftIK.solver.target = leftTarget;
        leftIK.solver.IKPositionWeight = 0f;
        leftIK.solver.IKRotationWeight = 0f;
        leftIK.solver.bendModifier = IKSolverLimb.BendModifier.Goal;
        leftIK.solver.bendGoal = leftHint;
        leftIK.solver.bendModifierWeight = 1f;
        Undo.RecordObject(rig, "配置小刀反向动力学");
        rig.knifeRightIK = rightIK;
        rig.knifeLeftIK = leftIK;
        rig.knifeRightTarget = rightGrip;
        rig.knifeLeftTarget = leftTarget;
        rig.knifeLeftHint = leftHint;
        rig.knifeRightHandWeight = 0.68f;
        rig.knifeLeftHandWeight = 0.06f;
        rig.knifeIKBlendDuration = 0.24f;
        EditorUtility.SetDirty(rightIK);
        EditorUtility.SetDirty(leftIK);
        EditorUtility.SetDirty(rightGrip);
        EditorUtility.SetDirty(rig);
        if (fingerController != null)
        {
            EditorUtility.SetDirty(fingerController);
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
        if (fingerController != null)
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(fingerController);
        }

        EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        EditorSceneManager.SaveScene(rig.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[小刀反向动力学配置] 已保留手臂骨链，并把右手求解目标改到真实刀模握柄。", rig);
    }

    /// <summary>编辑器菜单调用入口 TestRuntimeKnifeSwitch；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object, Object)、Debug.LogError(object)、Debug.LogWarning(object)、LeonCombat.ToggleKnifeMode()、Resources.FindObjectsOfTypeAll&lt;LeonCombat&gt;()、Scene.IsValid()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/运行时测试小刀切换")]
    public static void TestRuntimeKnifeSwitch()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[小刀切换测试] 该命令仅在运行模式中生效。");
            return;
        }

        // combat（战斗）：每轮从查找对象集合所属类型全部的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object, Object) 的参数、LeonKnifeIKSetupUtility.TestRuntimeKnifeSwitch()。
foreach (LeonCombat combat in Resources.FindObjectsOfTypeAll<LeonCombat>())
        {
            if (combat == null || !combat.gameObject.scene.IsValid() || !combat.gameObject.scene.isLoaded)
            {
                continue;
            }

            combat.ToggleKnifeMode();
            Debug.Log("[小刀切换测试] 已发起一次完整的小刀切换事务。", combat);
            return;
        }

        Debug.LogError("[小刀切换测试] 运行场景中找不到里昂战斗组件。");
    }

    /// <summary>FindSceneRig 的业务处理入口；本方法直接依赖 Resources.FindObjectsOfTypeAll&lt;LeonAnimRig&gt;()、Scene.IsValid()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeIKSetupUtility.RebuildKnifeIK()；下游（含内部回调体） Resources.FindObjectsOfTypeAll&lt;LeonAnimRig&gt;()、Scene.IsValid()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static LeonAnimRig FindSceneRig()
    {
        // rig（绑定）：每轮从查找对象集合所属类型全部的返回结果取得一个元素，仅代表当前这一项。使用位置：LeonKnifeIKSetupUtility.FindSceneRig()。
foreach (LeonAnimRig rig in Resources.FindObjectsOfTypeAll<LeonAnimRig>())
        {
            if (rig != null && rig.gameObject.scene.IsValid() && rig.gameObject.scene.isLoaded)
            {
                return rig;
            }
        }

        return null;
    }

    /// <summary>FindOrCreateChild 的业务处理入口；本方法直接依赖 GameObject.GameObject(string)、Transform.Find(string)、Transform.SetParent(Transform, bool)、Undo.RegisterCreatedObjectUndo(Object, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeIKSetupUtility.RebuildKnifeIK()；下游（含内部回调体） GameObject.GameObject(string)、Transform.Find(string)、Transform.SetParent(Transform, bool)、Undo.RegisterCreatedObjectUndo(Object, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string)。</param>
    /// <param name="childName">中文含义：子级名称。使用位置：作为 GameObject.GameObject(string) 的参数、作为 Transform.Find(string) 的参数。</param>
    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        // child（子级）：保存 Transform.Find(string) 的返回结果，供当前步骤使用。使用位置：LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string)。
Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        // childObject（子级对象）：使用位置：作为 Undo.RegisterCreatedObjectUndo(Object, string) 的参数、LeonKnifeIKSetupUtility.FindOrCreateChild(Transform, string)。
GameObject childObject = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(childObject, "创建小刀反向动力学目标");
        child = childObject.transform;
        child.SetParent(parent, false);
        return child;
    }

    /// <summary>GetOrAddLimbIK 的业务处理入口；本方法直接依赖 GameObject.GetComponent&lt;LimbIK&gt;()、Undo.AddComponent&lt;LimbIK&gt;(GameObject)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeIKSetupUtility.RebuildKnifeIK()；下游（含内部回调体） GameObject.GetComponent&lt;LimbIK&gt;()、Undo.AddComponent&lt;LimbIK&gt;(GameObject)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="targetObject">中文含义：目标对象。使用位置：作为 Undo.AddComponent&lt;LimbIK&gt;(GameObject) 的参数、LeonKnifeIKSetupUtility.GetOrAddLimbIK(GameObject)。</param>
    private static LimbIK GetOrAddLimbIK(GameObject targetObject)
    {
        // limbIK（肢体反向动力学）：保存 GameObject.GetComponent<LimbIK>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeIKSetupUtility.GetOrAddLimbIK(GameObject)。
LimbIK limbIK = targetObject.GetComponent<LimbIK>();
        if (limbIK != null)
        {
            return limbIK;
        }
        else
        {
            return Undo.AddComponent<LimbIK>(targetObject);
        }
    }

    /// <summary>SetPoseFromPlayerSpace 的业务处理入口；本方法直接依赖 Transform.TransformPoint(Vector3)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeIKSetupUtility.RebuildKnifeIK()；下游（含内部回调体） Transform.TransformPoint(Vector3)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="player">中文含义：玩家。使用位置：LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion)。</param>
    /// <param name="target">中文含义：目标变换节点。使用位置：LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion)。</param>
    /// <param name="localPosition">中文含义：局部位置。使用位置：作为 Transform.TransformPoint(Vector3) 的参数。</param>
    /// <param name="localRotation">中文含义：局部旋转。使用位置：LeonKnifeIKSetupUtility.SetPoseFromPlayerSpace(Transform, Transform, Vector3, Quaternion)。</param>
    private static void SetPoseFromPlayerSpace(Transform player, Transform target, Vector3 localPosition, Quaternion localRotation)
    {
        target.position = player.TransformPoint(localPosition);
        target.rotation = player.rotation * localRotation;
        target.localScale = Vector3.one;
    }
}

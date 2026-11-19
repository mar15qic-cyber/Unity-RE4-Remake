using UnityEngine;
using UnityEditor;

/// <summary>
/// 在敌人骨骼上添加 Bone Collider，让碰撞体跟随动画。
/// 菜单：Tools/Enemy/添加骨骼碰撞体
/// </summary>
public class BoneColliderSetup : EditorWindow
{
    /// <summary>编辑器菜单调用入口 Setup；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int)、Component.GetComponent&lt;CapsuleCollider&gt;()、Debug.Log(object)、Debug.LogError(object)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.SaveScene(Scene) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Enemy/添加骨骼碰撞体")]
    static void Setup()
    {
        // prefab（预制件）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.InstantiatePrefab(Object) 的参数、BoneColliderSetup.Setup()。
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Villager.prefab");
        if (prefab == null)
        {
            Debug.LogError("Villager.prefab not found");
            return;
        }

        // instance（实例）：使用位置：作为 Object.DestroyImmediate(Object) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、BoneColliderSetup.Setup()。
var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // anim（动画控制器）：保存 GameObject.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：作为 BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int) 的参数、BoneColliderSetup.Setup()。
var anim = instance.GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
        {
            Debug.LogError("No humanoid animator");
            DestroyImmediate(instance);
            return;
        }

        // 1. 移除根 CapsuleCollider（改用骨骼碰撞体）
        // rootCol（根节点列）：保存 GameObject.GetComponent<CapsuleCollider>() 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、BoneColliderSetup.Setup()。
var rootCol = instance.GetComponent<CapsuleCollider>();
        if (rootCol != null)
        {
            DestroyImmediate(rootCol);
        }

        // 2. 添加 CharacterJoint + Rigidbody 到骨骼（供物理用，可选）
        // 这里只加 Collider，不加 Ragdoll 物理
        // 3. 在关键骨骼上添加 CapsuleCollider
        AddBoneCollider(anim, HumanBodyBones.Head, "Head", 0.12f, 0.18f, 1);
        AddBoneCollider(anim, HumanBodyBones.Chest, "Chest", 0.18f, 0.35f, 1);
        AddBoneCollider(anim, HumanBodyBones.Spine, "Spine", 0.15f, 0.2f, 1);
        AddBoneCollider(anim, HumanBodyBones.Hips, "Hips", 0.16f, 0.25f, 1);
        AddBoneCollider(anim, HumanBodyBones.LeftUpperLeg, "L_Thigh", 0.08f, 0.35f, 1);
        AddBoneCollider(anim, HumanBodyBones.RightUpperLeg, "R_Thigh", 0.08f, 0.35f, 1);
        AddBoneCollider(anim, HumanBodyBones.LeftLowerLeg, "L_Shin", 0.07f, 0.35f, 1);
        AddBoneCollider(anim, HumanBodyBones.RightLowerLeg, "R_Shin", 0.07f, 0.35f, 1);
        AddBoneCollider(anim, HumanBodyBones.LeftUpperArm, "L_UpperArm", 0.06f, 0.25f, 1);
        AddBoneCollider(anim, HumanBodyBones.RightUpperArm, "R_UpperArm", 0.06f, 0.25f, 1);
        // 4. 验证
        // colliders（碰撞体集合）：保存 GameObject.GetComponentsInChildren<Collider>() 的返回结果，供当前步骤使用。使用位置：作为 Debug.Log(object) 的参数、BoneColliderSetup.Setup()。
var colliders = instance.GetComponentsInChildren<Collider>();
        Debug.Log($"[BoneCollider] 已添加 {colliders.Length} 个骨骼碰撞体");
        // c（当前碰撞体）：每轮从碰撞体集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object) 的参数。
foreach (var c in colliders)
        {
            Debug.Log($"  {c.gameObject.name}: {c.GetType().Name} center={c.bounds.center} size={c.bounds.size}");
        }

        // 5. 保存到 Prefab
        PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Prefab/Villager.prefab");
        Debug.Log("[BoneCollider] Prefab 已保存");
        DestroyImmediate(instance);
        // 6. 同步更新场景中的 Villager
        // vc（虚拟摄像机）：每轮从查找对象集合依据类型的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object) 的参数、BoneColliderSetup.Setup()。
foreach (var vc in Object.FindObjectsByType<VillagerController>(FindObjectsSortMode.None))
        {
            // sceneRootCol（场景根节点列）：保存 Component.GetComponent<CapsuleCollider>() 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、BoneColliderSetup.Setup()。
var sceneRootCol = vc.GetComponent<CapsuleCollider>();
            if (sceneRootCol != null)
            {
                DestroyImmediate(sceneRootCol);
            }

            Debug.Log($"[BoneCollider] 场景 {vc.name}: 已移除根 CapsuleCollider");
        }

        // scene（场景）：保存 SceneManager.GetActiveScene() 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 EditorSceneManager.SaveScene(Scene) 的参数。
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    /// <summary>AddBoneCollider 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;Collider&gt;()、Debug.LogWarning(object)、GameObject.AddComponent&lt;CapsuleCollider&gt;()、Object.DestroyImmediate(Object)、Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 BoneColliderSetup.Setup()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;Collider&gt;()、Debug.LogWarning(object)、GameObject.AddComponent&lt;CapsuleCollider&gt;()、Object.DestroyImmediate(Object)、Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="anim">中文含义：动画控制器。使用位置：BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int)。</param>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、作为 Debug.LogWarning(object) 的参数。</param>
    /// <param name="name">中文含义：名称。</param>
    /// <param name="radius">中文含义：半径。使用位置：BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int)。</param>
    /// <param name="height">中文含义：高度。使用位置：作为 Vector3.Vector3(float, float, float) 的参数、BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int)。</param>
    /// <param name="direction">中文含义：方向。使用位置：BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int)。</param>
    static void AddBoneCollider(Animator anim, HumanBodyBones bone, string name, float radius, float height, int direction)
    {
        // t（当前变换节点）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int)。
var t = anim.GetBoneTransform(bone);
        if (t == null)
        {
            Debug.LogWarning($"骨骼 {bone} 未找到");
            return;
        }

        // 移除旧 Collider
        // existing（已有）：保存 Component.GetComponent<Collider>() 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int)。
var existing = t.GetComponent<Collider>();
        if (existing != null)
        {
            DestroyImmediate(existing);
        }

        // col（列）：保存 GameObject.AddComponent<CapsuleCollider>() 的返回结果，供当前步骤使用。使用位置：BoneColliderSetup.AddBoneCollider(Animator, HumanBodyBones, string, float, float, int)。
var col = t.gameObject.AddComponent<CapsuleCollider>();
        col.radius = radius;
        col.height = height;
        col.center = new Vector3(0f, height * 0.25f, 0f); // 向下偏移到骨骼中段
        col.direction = direction; // Y axis
        col.isTrigger = false;
    }
}

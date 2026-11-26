#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 在 Villager Prefab 的骨骼上自动创建 Hitbox Collider + EnemyHitbox 组件。
/// 菜单：Tools/Enemy/创建骨骼 Hitbox
/// 注意：此菜单会保存 Villager.prefab，但不会保存场景。
/// </summary>
public class HitboxSetup : EditorWindow
{
    /// <summary>半径。编译期常量，保持既有值与消费者映射。</summary>
    const float ARM_RADIUS = 0.10f;
    /// <summary>半径。编译期常量，保持既有值与消费者映射。</summary>
    const float HAND_RADIUS = 0.09f;
    /// <summary>编辑器菜单调用入口 Setup；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.Log(object)、Debug.LogError(object)、GameObject.GetComponent&lt;Animator&gt;()、HitboxSetup.ClearEnemyHitboxes(GameObject)、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)、HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Enemy/创建骨骼 Hitbox")]
    static void Setup()
    {
        // 预制件。
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Villager.prefab");
        if (prefab == null)
        {
            Debug.LogError("Villager.prefab not found");
            return;
        }

        // 实例。
var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // 动画控制器。
var anim = instance.GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
        {
            Debug.LogError("No humanoid animator");
            DestroyImmediate(instance);
            return;
        }

        // 命中盒图层。
int hitboxLayer = LayerMask.NameToLayer("EnemyHitbox");
        if (hitboxLayer < 0)
        {
            Debug.LogError("Layer 'EnemyHitbox' not found");
            DestroyImmediate(instance);
            return;
        }

        // 只清除 EnemyHitbox 自己管理的碰撞体和组件，不删除其他系统的触发器
        ClearEnemyHitboxes(instance);
        // 在骨骼上创建 Hitbox
        CreateHitbox(anim, HumanBodyBones.Head, HitboxType.Head, hitboxLayer, "Sphere");
        CreateHitbox(anim, HumanBodyBones.Chest, HitboxType.Chest, hitboxLayer, "Capsule");
        CreateHitbox(anim, HumanBodyBones.Hips, HitboxType.Pelvis, hitboxLayer, "Capsule");
        // 手臂：沿骨骼到下一节骨骼的实际局部方向布置胶囊
        CreateArmHitbox(anim, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HitboxType.LeftArm, hitboxLayer);
        CreateArmHitbox(anim, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HitboxType.LeftArm, hitboxLayer);
        CreateArmHitbox(anim, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HitboxType.RightArm, hitboxLayer);
        CreateArmHitbox(anim, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HitboxType.RightArm, hitboxLayer);
        // 手掌：球形碰撞体
        CreateHandHitbox(anim, HumanBodyBones.LeftHand, HitboxType.LeftArm, hitboxLayer);
        CreateHandHitbox(anim, HumanBodyBones.RightHand, HitboxType.RightArm, hitboxLayer);
        CreateHitbox(anim, HumanBodyBones.LeftUpperLeg, HitboxType.LeftLeg, hitboxLayer, "Capsule");
        CreateHitbox(anim, HumanBodyBones.LeftLowerLeg, HitboxType.LeftLeg, hitboxLayer, "Capsule");
        CreateHitbox(anim, HumanBodyBones.RightUpperLeg, HitboxType.RightLeg, hitboxLayer, "Capsule");
        CreateHitbox(anim, HumanBodyBones.RightLowerLeg, HitboxType.RightLeg, hitboxLayer, "Capsule");
        // 保存 Prefab（不保存场景）
        PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Prefab/Villager.prefab");
        DestroyImmediate(instance);
        // 同步场景中的实例（不保存场景）
        // vc（虚拟摄像机）：每轮从查找对象集合依据类型的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object) 的参数、作为 HitboxSetup.SetupOnSceneObject(GameObject, int) 的参数。
foreach (var vc in Object.FindObjectsByType<VillagerController>(FindObjectsSortMode.None))
        {
            SetupOnSceneObject(vc.gameObject, hitboxLayer);
            Debug.Log($"场景 {vc.name}: Hitbox 已创建");
        }

        Debug.Log("[HitboxSetup] 完成！未保存场景。");
    }

    /// <summary>
    /// 只删除带有 EnemyHitbox 组件的对象上的 Collider 和 EnemyHitbox，
    /// 不得删除攻击范围、QTE 或其他系统的触发器。
    /// </summary>
    /// <remarks>静态调用链：上游 HitboxSetup.Setup()、HitboxSetup.SetupOnSceneObject(GameObject, int)；下游（含内部回调体） GameObject.GetComponent&lt;Collider&gt;()、GameObject.GetComponentsInChildren&lt;EnemyHitbox&gt;(bool)、Object.DestroyImmediate(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根游戏对象。</param>
    static void ClearEnemyHitboxes(GameObject root)
    {
        // hitbox（命中盒）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数、HitboxSetup.ClearEnemyHitboxes(GameObject)。
foreach (var hitbox in root.GetComponentsInChildren<EnemyHitbox>(true))
        {
            // 当前游戏对象。
var go = hitbox.gameObject;
            DestroyImmediate(hitbox);
            // 列。
var col = go.GetComponent<Collider>();
            if (col != null)
            {
                DestroyImmediate(col);
            }
        }
    }

    /// <summary>CreateHitbox 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponent&lt;SphereCollider&gt;()、Debug.Log(object)、Debug.LogWarning(object)、GameObject.AddComponent&lt;CapsuleCollider&gt;() 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HitboxSetup.Setup()、HitboxSetup.SetupOnSceneObject(GameObject, int)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponent&lt;SphereCollider&gt;()、Debug.Log(object)、Debug.LogWarning(object)、GameObject.AddComponent&lt;CapsuleCollider&gt;() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="anim">动画控制器。</param>
    /// <param name="bone">骨骼。</param>
    /// <param name="type">当前命中部位类别。</param>
    /// <param name="layer">图层。</param>
    /// <param name="colliderType">碰撞体类型。</param>
    static void CreateHitbox(Animator anim, HumanBodyBones bone, HitboxType type, int layer, string colliderType)
    {
        // 骨骼变换。
var boneTransform = anim.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            Debug.LogWarning($"骨骼 {bone} 未找到");
            return;
        }

        // 列。
Collider col;
        if (colliderType == "Sphere")
        {
            // 缩放。
var sc = boneTransform.GetComponent<SphereCollider>();
            if (sc == null)
            {
                sc = boneTransform.gameObject.AddComponent<SphereCollider>();
            }

            sc.radius = GetRadius(type);
            sc.center = GetCenter(type);
            sc.isTrigger = true;
            col = sc;
        }
        else
        {
            // 角色碰撞器。
var cc = boneTransform.GetComponent<CapsuleCollider>();
            if (cc == null)
            {
                cc = boneTransform.gameObject.AddComponent<CapsuleCollider>();
            }

            cc.radius = GetRadius(type);
            cc.height = GetHeight(type);
            cc.center = GetCenter(type);
            cc.direction = 1; // Y
            cc.isTrigger = true;
            col = cc;
        }

        // 命中盒。
var hitbox = boneTransform.GetComponent<EnemyHitbox>();
        if (hitbox == null)
        {
            hitbox = boneTransform.gameObject.AddComponent<EnemyHitbox>();
        }

        hitbox.hitboxType = type;
        boneTransform.gameObject.layer = layer;
        Debug.Log($"  ✓ {bone} → {type} ({colliderType}, r={GetRadius(type)})");
    }

    /// <summary>CreateArmHitbox 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;EnemyHitbox&gt;()、Debug.Log(object)、Debug.LogWarning(object)、GameObject.AddComponent&lt;CapsuleCollider&gt;()、GameObject.AddComponent&lt;EnemyHitbox&gt;() 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HitboxSetup.Setup()、HitboxSetup.SetupOnSceneObject(GameObject, int)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;EnemyHitbox&gt;()、Debug.Log(object)、Debug.LogWarning(object)、GameObject.AddComponent&lt;CapsuleCollider&gt;()、GameObject.AddComponent&lt;EnemyHitbox&gt;() 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="anim">动画控制器。</param>
    /// <param name="bone">骨骼。</param>
    /// <param name="nextBone">下一次骨骼。</param>
    /// <param name="type">当前命中部位类别。</param>
    /// <param name="layer">图层。</param>
    static void CreateArmHitbox(Animator anim, HumanBodyBones bone, HumanBodyBones nextBone, HitboxType type, int layer)
    {
        // 当前变换节点。
var current = anim.GetBoneTransform(bone);
        // 下一个变换节点。
var next = anim.GetBoneTransform(nextBone);
        if (current == null || next == null)
        {
            Debug.LogWarning($"骨骼 {bone} 或 {nextBone} 未找到");
            return;
        }

        // 局部偏移量。
Vector3 localOffset = current.InverseTransformPoint(next.position);
        // 骨骼长度。
float boneLength = localOffset.magnitude;
        if (boneLength < 0.001f)
        {
            Debug.LogWarning($"骨骼 {bone} 到 {nextBone} 的长度为零");
            return;
        }

        // 中心。
Vector3 center = localOffset * 0.5f;
        // 绝对值横向。
float absX = Mathf.Abs(localOffset.x);
        // 绝对值纵向。
float absY = Mathf.Abs(localOffset.y);
        // 绝对值深度。
float absZ = Mathf.Abs(localOffset.z);
        // 方向。
int direction;
        if (absX >= absY && absX >= absZ)
        {
            direction = 0;
        }
        else
        {
            // 分支选出的1。
int selectedValue1;
            if (absY >= absZ)
            {
                selectedValue1 = 1;
            }
            else
            {
                selectedValue1 = 2;
            }

            direction = (selectedValue1);
        }

        // 高度。
float height = boneLength + ARM_RADIUS * 2f * 0.5f;
        // 角色碰撞器。
var cc = current.GetComponent<CapsuleCollider>();
        if (cc == null)
        {
            cc = current.gameObject.AddComponent<CapsuleCollider>();
        }

        cc.radius = ARM_RADIUS;
        cc.height = Mathf.Max(height, ARM_RADIUS * 2f);
        cc.center = center;
        cc.direction = direction;
        cc.isTrigger = true;
        // 命中盒。
var hitbox = current.GetComponent<EnemyHitbox>();
        if (hitbox == null)
        {
            hitbox = current.gameObject.AddComponent<EnemyHitbox>();
        }

        hitbox.hitboxType = type;
        current.gameObject.layer = layer;
        Debug.Log($"  ✓ {bone} → {type} (Capsule, r={ARM_RADIUS}, h={height:F3}, dir={direction}, center=({center.x:F3},{center.y:F3},{center.z:F3}))");
    }

    /// <summary>CreateHandHitbox 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponent&lt;SphereCollider&gt;()、Debug.Log(object)、Debug.LogWarning(object)、GameObject.AddComponent&lt;EnemyHitbox&gt;()、GameObject.AddComponent&lt;SphereCollider&gt;()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HitboxSetup.Setup()、HitboxSetup.SetupOnSceneObject(GameObject, int)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponent&lt;SphereCollider&gt;()、Debug.Log(object)、Debug.LogWarning(object)、GameObject.AddComponent&lt;EnemyHitbox&gt;()、GameObject.AddComponent&lt;SphereCollider&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="anim">动画控制器。</param>
    /// <param name="bone">骨骼。</param>
    /// <param name="type">当前命中部位类别。</param>
    /// <param name="layer">图层。</param>
    static void CreateHandHitbox(Animator anim, HumanBodyBones bone, HitboxType type, int layer)
    {
        // 骨骼变换。
var boneTransform = anim.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            Debug.LogWarning($"骨骼 {bone} 未找到");
            return;
        }

        // 缩放。
var sc = boneTransform.GetComponent<SphereCollider>();
        if (sc == null)
        {
            sc = boneTransform.gameObject.AddComponent<SphereCollider>();
        }

        sc.radius = HAND_RADIUS;
        sc.center = Vector3.zero;
        sc.isTrigger = true;
        // 命中盒。
var hitbox = boneTransform.GetComponent<EnemyHitbox>();
        if (hitbox == null)
        {
            hitbox = boneTransform.gameObject.AddComponent<EnemyHitbox>();
        }

        hitbox.hitboxType = type;
        boneTransform.gameObject.layer = layer;
        Debug.Log($"  ✓ {bone} → {type} (Sphere, r={HAND_RADIUS})");
    }

    /// <summary>SetupOnSceneObject 的业务处理入口；本方法直接依赖 GameObject.GetComponent&lt;Animator&gt;()、HitboxSetup.ClearEnemyHitboxes(GameObject)、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)、HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)、HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HitboxSetup.Setup()；下游（含内部回调体） GameObject.GetComponent&lt;Animator&gt;()、HitboxSetup.ClearEnemyHitboxes(GameObject)、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)、HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)、HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="go">当前游戏对象。</param>
    /// <param name="layer">图层。</param>
    static void SetupOnSceneObject(GameObject go, int layer)
    {
        // 动画控制器。
var anim = go.GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
        {
            return;
        }

        ClearEnemyHitboxes(go);
        CreateHitbox(anim, HumanBodyBones.Head, HitboxType.Head, layer, "Sphere");
        CreateHitbox(anim, HumanBodyBones.Chest, HitboxType.Chest, layer, "Capsule");
        CreateHitbox(anim, HumanBodyBones.Hips, HitboxType.Pelvis, layer, "Capsule");
        CreateArmHitbox(anim, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HitboxType.LeftArm, layer);
        CreateArmHitbox(anim, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, HitboxType.LeftArm, layer);
        CreateArmHitbox(anim, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HitboxType.RightArm, layer);
        CreateArmHitbox(anim, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, HitboxType.RightArm, layer);
        CreateHandHitbox(anim, HumanBodyBones.LeftHand, HitboxType.LeftArm, layer);
        CreateHandHitbox(anim, HumanBodyBones.RightHand, HitboxType.RightArm, layer);
        CreateHitbox(anim, HumanBodyBones.LeftUpperLeg, HitboxType.LeftLeg, layer, "Capsule");
        CreateHitbox(anim, HumanBodyBones.LeftLowerLeg, HitboxType.LeftLeg, layer, "Capsule");
        CreateHitbox(anim, HumanBodyBones.RightUpperLeg, HitboxType.RightLeg, layer, "Capsule");
        CreateHitbox(anim, HumanBodyBones.RightLowerLeg, HitboxType.RightLeg, layer, "Capsule");
    }

    /// <summary>GetRadius 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前命中部位类别。</param>
    static float GetRadius(HitboxType type)
    {
        switch (type)
        {
            case HitboxType.Head:
                return 0.12f;
            case HitboxType.Chest:
                return 0.18f;
            case HitboxType.Pelvis:
                return 0.16f;
            case HitboxType.LeftLeg:
            case HitboxType.RightLeg:
                return 0.1f;
            default:
                return 0.1f;
        }
    }

    /// <summary>GetHeight 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前命中部位类别。</param>
    static float GetHeight(HitboxType type)
    {
        switch (type)
        {
            case HitboxType.Chest:
                return 0.35f;
            case HitboxType.Pelvis:
                return 0.25f;
            case HitboxType.LeftLeg:
            case HitboxType.RightLeg:
                return 0.35f;
            default:
                return 0.2f;
        }
    }

    /// <summary>GetCenter 的业务处理入口；本方法直接依赖 Vector3.Vector3(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)；下游（含内部回调体） Vector3.Vector3(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="type">当前命中部位类别。</param>
    static Vector3 GetCenter(HitboxType type)
    {
        switch (type)
        {
            case HitboxType.Head:
                return new Vector3(0, 0, 0);
            case HitboxType.Chest:
                return new Vector3(0, 0.1f, 0);
            case HitboxType.Pelvis:
                return new Vector3(0, 0, 0);
            case HitboxType.LeftLeg:
            case HitboxType.RightLeg:
                return new Vector3(0, -0.1f, 0);
            default:
                return Vector3.zero;
        }
    }
}
#endif

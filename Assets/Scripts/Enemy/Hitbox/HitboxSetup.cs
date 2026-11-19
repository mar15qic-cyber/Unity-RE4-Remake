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
    /// <summary>中文含义：半径。编译期常量，保持既有值与消费者映射。使用位置：作为 Debug.Log(object) 的参数、作为 Mathf.Max(float, float) 的参数、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。</summary>
    const float ARM_RADIUS = 0.10f;
    /// <summary>中文含义：半径。编译期常量，保持既有值与消费者映射。使用位置：作为 Debug.Log(object) 的参数、HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)。</summary>
    const float HAND_RADIUS = 0.09f;
    /// <summary>编辑器菜单调用入口 Setup；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、Debug.Log(object)、Debug.LogError(object)、GameObject.GetComponent&lt;Animator&gt;()、HitboxSetup.ClearEnemyHitboxes(GameObject)、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)、HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Enemy/创建骨骼 Hitbox")]
    static void Setup()
    {
        // prefab（预制件）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.InstantiatePrefab(Object) 的参数、HitboxSetup.Setup()。
var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Villager.prefab");
        if (prefab == null)
        {
            Debug.LogError("Villager.prefab not found");
            return;
        }

        // instance（实例）：使用位置：作为 HitboxSetup.ClearEnemyHitboxes(GameObject) 的参数、作为 Object.DestroyImmediate(Object) 的参数、作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数 等共 4 处。
var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        // anim（动画控制器）：保存 GameObject.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：作为 HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int) 的参数、作为 HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int) 的参数、作为 HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string) 的参数 等共 4 处。
var anim = instance.GetComponent<Animator>();
        if (anim == null || !anim.isHuman)
        {
            Debug.LogError("No humanoid animator");
            DestroyImmediate(instance);
            return;
        }

        // hitboxLayer（命中盒图层）：保存 LayerMask.NameToLayer(string) 的返回结果，供当前步骤使用。使用位置：作为 HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int) 的参数、作为 HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int) 的参数、作为 HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string) 的参数 等共 5 处。
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
    /// <param name="root">中文含义：根游戏对象。使用位置：HitboxSetup.ClearEnemyHitboxes(GameObject)。</param>
    static void ClearEnemyHitboxes(GameObject root)
    {
        // hitbox（命中盒）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Object.DestroyImmediate(Object) 的参数、HitboxSetup.ClearEnemyHitboxes(GameObject)。
foreach (var hitbox in root.GetComponentsInChildren<EnemyHitbox>(true))
        {
            // go（当前游戏对象）：使用位置：HitboxSetup.ClearEnemyHitboxes(GameObject)。
var go = hitbox.gameObject;
            DestroyImmediate(hitbox);
            // col（列）：保存 GameObject.GetComponent<Collider>() 的返回结果，供当前步骤使用。使用位置：作为 Object.DestroyImmediate(Object) 的参数、HitboxSetup.ClearEnemyHitboxes(GameObject)。
var col = go.GetComponent<Collider>();
            if (col != null)
            {
                DestroyImmediate(col);
            }
        }
    }

    /// <summary>CreateHitbox 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponent&lt;SphereCollider&gt;()、Debug.Log(object)、Debug.LogWarning(object)、GameObject.AddComponent&lt;CapsuleCollider&gt;() 等共 12 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 HitboxSetup.Setup()、HitboxSetup.SetupOnSceneObject(GameObject, int)；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;CapsuleCollider&gt;()、Component.GetComponent&lt;EnemyHitbox&gt;()、Component.GetComponent&lt;SphereCollider&gt;()、Debug.Log(object)、Debug.LogWarning(object)、GameObject.AddComponent&lt;CapsuleCollider&gt;() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="anim">中文含义：动画控制器。使用位置：HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。</param>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、作为 Debug.Log(object) 的参数、作为 Debug.LogWarning(object) 的参数。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：作为 Debug.Log(object) 的参数、作为 HitboxSetup.GetCenter(HitboxType) 的参数、作为 HitboxSetup.GetHeight(HitboxType) 的参数 等共 5 处。</param>
    /// <param name="layer">中文含义：图层。使用位置：HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。</param>
    /// <param name="colliderType">中文含义：碰撞体类型。使用位置：作为 Debug.Log(object) 的参数、HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。</param>
    static void CreateHitbox(Animator anim, HumanBodyBones bone, HitboxType type, int layer, string colliderType)
    {
        // boneTransform（骨骼变换）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。
var boneTransform = anim.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            Debug.LogWarning($"骨骼 {bone} 未找到");
            return;
        }

        // col（列）：使用位置：HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。
Collider col;
        if (colliderType == "Sphere")
        {
            // sc（缩放）：保存 Component.GetComponent<SphereCollider>() 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。
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
            // cc（角色碰撞器）：保存 Component.GetComponent<CapsuleCollider>() 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。
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

        // hitbox（命中盒）：保存 Component.GetComponent<EnemyHitbox>() 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string)。
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
    /// <param name="anim">中文含义：动画控制器。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。</param>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、作为 Debug.Log(object) 的参数、作为 Debug.LogWarning(object) 的参数。</param>
    /// <param name="nextBone">中文含义：下一次骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、作为 Debug.LogWarning(object) 的参数。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：作为 Debug.Log(object) 的参数、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。</param>
    /// <param name="layer">中文含义：图层。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。</param>
    static void CreateArmHitbox(Animator anim, HumanBodyBones bone, HumanBodyBones nextBone, HitboxType type, int layer)
    {
        // current（当前变换节点）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
var current = anim.GetBoneTransform(bone);
        // next（下一个变换节点）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
var next = anim.GetBoneTransform(nextBone);
        if (current == null || next == null)
        {
            Debug.LogWarning($"骨骼 {bone} 或 {nextBone} 未找到");
            return;
        }

        // localOffset（局部偏移量）：保存 Transform.InverseTransformPoint(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Abs(float) 的参数、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
Vector3 localOffset = current.InverseTransformPoint(next.position);
        // boneLength（骨骼长度）：使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
float boneLength = localOffset.magnitude;
        if (boneLength < 0.001f)
        {
            Debug.LogWarning($"骨骼 {bone} 到 {nextBone} 的长度为零");
            return;
        }

        // center（中心）：使用位置：作为 Debug.Log(object) 的参数、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
Vector3 center = localOffset * 0.5f;
        // absX（绝对值横向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
float absX = Mathf.Abs(localOffset.x);
        // absY（绝对值纵向）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
float absY = Mathf.Abs(localOffset.y);
        // absZ（绝对值深度）：保存 Mathf.Abs(float) 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
float absZ = Mathf.Abs(localOffset.z);
        // direction（方向）：使用位置：作为 Debug.Log(object) 的参数、HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
int direction;
        if (absX >= absY && absX >= absZ)
        {
            direction = 0;
        }
        else
        {
            // selectedValue1（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
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

        // height（高度）：使用位置：作为 Debug.Log(object) 的参数、作为 Mathf.Max(float, float) 的参数。
float height = boneLength + ARM_RADIUS * 2f * 0.5f;
        // cc（角色碰撞器）：保存 Component.GetComponent<CapsuleCollider>() 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
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
        // hitbox（命中盒）：保存 Component.GetComponent<EnemyHitbox>() 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int)。
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
    /// <param name="anim">中文含义：动画控制器。使用位置：HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)。</param>
    /// <param name="bone">中文含义：骨骼。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、作为 Debug.Log(object) 的参数、作为 Debug.LogWarning(object) 的参数。</param>
    /// <param name="type">中文含义：当前命中部位类别。使用位置：作为 Debug.Log(object) 的参数、HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)。</param>
    /// <param name="layer">中文含义：图层。使用位置：HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)。</param>
    static void CreateHandHitbox(Animator anim, HumanBodyBones bone, HitboxType type, int layer)
    {
        // boneTransform（骨骼变换）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)。
var boneTransform = anim.GetBoneTransform(bone);
        if (boneTransform == null)
        {
            Debug.LogWarning($"骨骼 {bone} 未找到");
            return;
        }

        // sc（缩放）：保存 Component.GetComponent<SphereCollider>() 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)。
var sc = boneTransform.GetComponent<SphereCollider>();
        if (sc == null)
        {
            sc = boneTransform.gameObject.AddComponent<SphereCollider>();
        }

        sc.radius = HAND_RADIUS;
        sc.center = Vector3.zero;
        sc.isTrigger = true;
        // hitbox（命中盒）：保存 Component.GetComponent<EnemyHitbox>() 的返回结果，供当前步骤使用。使用位置：HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int)。
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
    /// <param name="go">中文含义：当前游戏对象。使用位置：作为 HitboxSetup.ClearEnemyHitboxes(GameObject) 的参数、HitboxSetup.SetupOnSceneObject(GameObject, int)。</param>
    /// <param name="layer">中文含义：图层。使用位置：作为 HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int) 的参数、作为 HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int) 的参数、作为 HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string) 的参数。</param>
    static void SetupOnSceneObject(GameObject go, int layer)
    {
        // anim（动画控制器）：保存 GameObject.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：作为 HitboxSetup.CreateArmHitbox(Animator, HumanBodyBones, HumanBodyBones, HitboxType, int) 的参数、作为 HitboxSetup.CreateHandHitbox(Animator, HumanBodyBones, HitboxType, int) 的参数、作为 HitboxSetup.CreateHitbox(Animator, HumanBodyBones, HitboxType, int, string) 的参数 等共 4 处。
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
    /// <param name="type">中文含义：当前命中部位类别。使用位置：HitboxSetup.GetRadius(HitboxType)。</param>
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
    /// <param name="type">中文含义：当前命中部位类别。使用位置：HitboxSetup.GetHeight(HitboxType)。</param>
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
    /// <param name="type">中文含义：当前命中部位类别。使用位置：HitboxSetup.GetCenter(HitboxType)。</param>
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

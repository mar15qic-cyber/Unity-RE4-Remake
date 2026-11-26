using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.AI;

/// <summary>编辑器配置与校验使用的共享配置资产 FixEnemyFloating；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
public class FixEnemyFloating : EditorWindow
{
    /// <summary>编辑器菜单调用入口 Fix；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;NavMeshAgent&gt;()、Debug.Log(object)、Debug.LogWarning(object)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.SaveScene(Scene) 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Enemy/修复脚部悬空")]
    static void Fix()
    {
        // 敌人集合。
var enemies = Object.FindObjectsByType<VillagerController>(FindObjectsSortMode.None);
        if (enemies.Length == 0)
        {
            Debug.LogWarning("场景中没有 VillagerController");
            return;
        }

        // vc（虚拟摄像机）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：FixEnemyFloating.Fix()。
foreach (var vc in enemies)
        {
            // 当前变换节点。
var t = vc.transform;
            // 寻路代理。
var agent = vc.GetComponent<NavMeshAgent>();
            // 动画控制器。
var anim = vc.GetComponent<Animator>();
            // 1. 确保 applyRootMotion = false
            anim.applyRootMotion = false;
            // 2. 测量脚部到地面的距离
            // 脚部纵向。
float footY = 0f;
            if (anim.isHuman)
            {
                // 左脚。
var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                // 右脚。
var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                if (lf != null)
                {
                    footY = lf.position.y;
                }

                if (rf != null)
                {
                    footY = Mathf.Min(footY, rf.position.y);
                }
            }

            // 遮罩。
int mask = ~(1 << t.gameObject.layer);
            // 地面纵向。
float groundY = 0f;
            if (Physics.Raycast(t.position + Vector3.up * 5f, Vector3.down, out RaycastHit /* 中文含义：游戏宿主。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ gh, 10f, mask))
            {
                groundY = gh.point.y;
            }

            // 浮点大小。
float floatAmount = footY - groundY;
            Debug.Log($"[FixFloating] {t.name}: footY={footY:F3} groundY={groundY:F3} float={floatAmount:F3}m");
            // 3. 设置 baseOffset 补偿
            if (agent.isOnNavMesh)
            {
                agent.baseOffset = -floatAmount;
                Debug.Log($"[FixFloating] baseOffset = {-floatAmount:F3}");
            }
            else
            {
                // 不在 NavMesh 上，直接下沉 transform
                t.position = new Vector3(t.position.x, t.position.y - floatAmount, t.position.z);
                Debug.Log($"[FixFloating] transform Y 调整 {-floatAmount:F3}m");
                // 尝试重新连接 NavMesh
                agent.enabled = false;
                agent.enabled = true;
                if (NavMesh.SamplePosition(t.position, out NavMeshHit /* 中文含义：命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hit, 5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    Debug.Log($"[FixFloating] Warp to {hit.position}, isOnNavMesh={agent.isOnNavMesh}");
                }
            }

            // 4. 再次测量
            if (anim.isHuman)
            {
                // 左脚。
var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (lf != null)
                {
                    // 新浮点。
float newFloat = lf.position.y - groundY;
                    Debug.Log($"[FixFloating] 修复后脚到地面: {newFloat:F3}m");
                }
            }
        }

        // 保存场景
        // 场景。
var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("[FixFloating] 完成！请运行 工具 > Enemy > 烘焙 NavMesh 来修复 NavMesh 高度问题");
    }
}

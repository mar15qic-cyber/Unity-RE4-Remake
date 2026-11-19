using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using System.Reflection;
using System.Text;

/// <summary>编辑器配置与校验使用的共享配置资产 DiagnoseNavMesh；通过序列化引用交给宿主读取，不在此说明中推断实例状态归属。</summary>
public class DiagnoseNavMesh : EditorWindow
{
    /// <summary>编辑器菜单调用入口 Diagnose；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;NavMeshAgent&gt;()、Debug.Log(object)、Debug.LogError(object)、FieldInfo.GetValue(object)、GameObject.Find(string)、GameObject.GetComponent(string)、NavMesh.CalculateTriangulation() 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/Enemy/诊断 NavMesh")]
    static void Diagnose()
    {
        // sb（文本构建器）：使用位置：作为 Debug.Log(object) 的参数、DiagnoseNavMesh.Diagnose()。
var sb = new StringBuilder();
        // ground（地面）：保存 GameObject.Find(string) 的返回结果，供当前步骤使用。使用位置：DiagnoseNavMesh.Diagnose()。
var ground = GameObject.Find("TestGround");
        if (ground == null)
        {
            Debug.LogError("TestGround not found");
            return;
        }

        // surface（表面）：保存 GameObject.GetComponent(string) 的返回结果，供当前步骤使用。使用位置：作为 FieldInfo.GetValue(object) 的参数、DiagnoseNavMesh.Diagnose()。
var surface = ground.GetComponent("NavMeshSurface");
        if (surface == null)
        {
            Debug.LogError("NavMeshSurface not found");
            return;
        }

        sb.AppendLine("=== NavMeshSurface ===");
        // type（反射操作使用的类型信息）：保存 object.GetType() 的返回结果，供当前步骤使用。使用位置：DiagnoseNavMesh.Diagnose()。
var type = surface.GetType();
        // f（当前反射字段信息）：每轮从取得字段集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 StringBuilder.AppendLine(string) 的参数。
foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            sb.AppendLine($"  {f.Name} = {f.GetValue(surface)}");
        }

        sb.AppendLine($"\n=== NavMesh Triangulation ===");
        // tri（三角形）：保存 NavMesh.CalculateTriangulation() 的返回结果，供当前步骤使用。使用位置：作为 StringBuilder.AppendLine(string) 的参数、DiagnoseNavMesh.Diagnose()。
var tri = NavMesh.CalculateTriangulation();
        sb.AppendLine($"  vertices: {tri.vertices.Length}");
        if (tri.vertices.Length > 0)
        {

            // 中文含义：minY 为“最小纵向”。；maxY 为“最大纵向”。
float minY = float.MaxValue, maxY = float.MinValue;
            // i（当前遍历索引）：使用位置：DiagnoseNavMesh.Diagnose()。
for (int i = 0; i < tri.vertices.Length; i++)
            {
                if (tri.vertices[i].y < minY)
                {
                    minY = tri.vertices[i].y;
                }

                if (tri.vertices[i].y > maxY)
                {
                    maxY = tri.vertices[i].y;
                }
            }

            sb.AppendLine($"  Y range: {minY:F3} ~ {maxY:F3}");
        }
        else
        {
            sb.AppendLine("  NO NavMesh data!");
        }

        sb.AppendLine($"\nNavMesh.GetSettingsCount: {NavMesh.GetSettingsCount()}");
        // enemies（敌人集合）：保存 Object.FindObjectsByType<VillagerController>(FindObjectsSortMode) 的返回结果，供当前步骤使用。使用位置：DiagnoseNavMesh.Diagnose()。
var enemies = Object.FindObjectsByType<VillagerController>(FindObjectsSortMode.None);
        // vc（虚拟摄像机）：每轮从敌人集合取得一个元素，仅代表当前这一项。使用位置：作为 NavMesh.SamplePosition(Vector3, out NavMeshHit, float, int) 的参数、作为 StringBuilder.AppendLine(string) 的参数、DiagnoseNavMesh.Diagnose()。
foreach (var vc in enemies)
        {
            // agent（寻路代理）：保存 Component.GetComponent<NavMeshAgent>() 的返回结果，供当前步骤使用。使用位置：作为 StringBuilder.AppendLine(string) 的参数、DiagnoseNavMesh.Diagnose()。
var agent = vc.GetComponent<NavMeshAgent>();
            sb.AppendLine($"\n=== {vc.gameObject.name} ===");
            sb.AppendLine($"  agentTypeID: {agent.agentTypeID}");
            sb.AppendLine($"  areaMask: {agent.areaMask}");
            sb.AppendLine($"  isOnNavMesh: {agent.isOnNavMesh}");
            sb.AppendLine($"  baseOffset: {agent.baseOffset}");
            if (NavMesh.SamplePosition(vc.transform.position, out NavMeshHit /* 中文含义：命中。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hit, 50f, NavMesh.AllAreas))
            {
                sb.AppendLine($"  NavMesh hit: {hit.position} mask={hit.mask}");
                agent.areaMask = -1;
                agent.enabled = false;
                vc.transform.position = hit.position;
                agent.enabled = true;
                // warped（是否扭曲后的）：保存 NavMeshAgent.Warp(Vector3) 的返回结果，供当前步骤使用。使用位置：作为 StringBuilder.AppendLine(string) 的参数。
bool warped = agent.Warp(hit.position);
                sb.AppendLine($"  Warp: ok={warped} isOnNavMesh={agent.isOnNavMesh}");
            }
            else
            {
                sb.AppendLine("  NavMesh.SamplePosition: NO HIT");
            }
        }

        Debug.Log(sb.ToString());
    }
}

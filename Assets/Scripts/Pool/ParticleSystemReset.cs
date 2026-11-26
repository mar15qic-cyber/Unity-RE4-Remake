using UnityEngine;

/// <summary>
/// ParticleSystem 池化重置组件。
/// 自动添加到含 ParticleSystem 的池化实例上。
/// 在初始化时将所有粒子系统的 StopAction 改为 None（防止自行销毁池对象），
/// 在取出时清空并重新播放，在回收时停止并清空。
/// </summary>
public class ParticleSystemReset : MonoBehaviour, IPoolable
{
    /// <summary>全部粒子系统集合。</summary>
    private ParticleSystem[] allParticleSystems;
    /// <summary>Unity 组件初始化回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponentsInChildren&lt;ParticleSystem&gt;(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Awake()
    {
        // 收集所有粒子系统（包括子级）
        allParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
        // 将所有粒子系统的 StopAction 改为 None
        // 防止 ParticleSystem.StopAction.Destroy 销毁池对象
        // 当前遍历索引。
for (int i = 0; i < allParticleSystems.Length; i++)
        {
            if (allParticleSystems[i] == null)
            {
                continue;
            }

            // 主要。
var main = allParticleSystems[i].main;
            main.stopAction = ParticleSystemStopAction.None;
        }
    }

    /// <summary>OnPoolGet 的业务处理入口；本方法直接依赖 ParticleSystem.Clear()、ParticleSystem.Play()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ParticleSystem.Clear()、ParticleSystem.Play()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnPoolGet()
    {
        if (allParticleSystems == null)
        {
            return;
        }

        // 当前遍历索引。
for (int i = 0; i < allParticleSystems.Length; i++)
        {
            if (allParticleSystems[i] == null)
            {
                continue;
            }

            allParticleSystems[i].Clear();
            allParticleSystems[i].Play();
        }
    }

    /// <summary>OnPoolReturn 的业务处理入口；本方法直接依赖 ParticleSystem.Clear()、ParticleSystem.Stop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） ParticleSystem.Clear()、ParticleSystem.Stop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnPoolReturn()
    {
        if (allParticleSystems == null)
        {
            return;
        }

        // 当前遍历索引。
for (int i = 0; i < allParticleSystems.Length; i++)
        {
            if (allParticleSystems[i] == null)
            {
                continue;
            }

            allParticleSystems[i].Stop();
            allParticleSystems[i].Clear();
        }
    }
}

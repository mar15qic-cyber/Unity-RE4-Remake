using UnityEngine;

/// <summary>实例私有的物理查询缓冲。饱和时扩大，达到软上限后完整查询兜底。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "命中查询缓冲")]
internal sealed class HitQueryBuffer
{
    /// <summary>中文含义：缓冲区。使用位置：作为 Array.Resize&lt;RaycastHit&gt;(ref RaycastHit[], int) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数 等共 5 处。</summary>
    private RaycastHit[] buffer = new RaycastHit[64];
    /// <summary>Raycast 的业务处理入口；本方法直接依赖 Array.Resize&lt;RaycastHit&gt;(ref RaycastHit[], int)、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.HasDirectSpaceFromPlayer(Vector3, Vector3)、EnemyPerceptionModule.HasClearLineToPlayer(bool, float)、PursuitNavigationTopology.IsSupportedRegionAwayFromPortals(Vector3, int)、WeaponBase.TryResolveRayHit(Vector3, Vector3, float, int, out RaycastHit)；下游（含内部回调体） Array.Resize&lt;RaycastHit&gt;(ref RaycastHit[], int)、Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction)、Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">中文含义：原点。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="distance">中文含义：距离。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="mask">中文含义：遮罩。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="triggers">中文含义：触发器集合。使用位置：作为 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.RaycastNonAlloc(Vector3, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="count">中文含义：数量。输出参数，由本入口写入并交给调用方。使用位置：HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int)。</param>
    internal RaycastHit[] Raycast(Vector3 origin, Vector3 direction, float distance, int mask, QueryTriggerInteraction triggers, out int count)
    {
        while (true)
        {
            count = Physics.RaycastNonAlloc(origin, direction, buffer, distance, mask, triggers);
            if (count < buffer.Length)
            {
                return buffer;
            }

            if (buffer.Length >= 1024)
            {
                // complete（完成）：保存 Physics.RaycastAll(Vector3, Vector3, float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：HitQueryBuffer.Raycast(Vector3, Vector3, float, int, QueryTriggerInteraction, out int)。
RaycastHit[] complete = Physics.RaycastAll(origin, direction, distance, mask, triggers);
                count = complete.Length;
                return complete;
            }

            System.Array.Resize(ref buffer, buffer.Length * 2);
        }
    }

    /// <summary>SphereCast 的业务处理入口；本方法直接依赖 Array.Resize&lt;RaycastHit&gt;(ref RaycastHit[], int)、Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 WeaponBase.TryResolveSphereHit(Vector3, float, Vector3, float, int, out RaycastHit)；下游（含内部回调体） Array.Resize&lt;RaycastHit&gt;(ref RaycastHit[], int)、Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction)、Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="origin">中文含义：原点。使用位置：作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="radius">中文含义：半径。使用位置：作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="direction">中文含义：方向。使用位置：作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="distance">中文含义：距离。使用位置：作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="mask">中文含义：遮罩。使用位置：作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="triggers">中文含义：触发器集合。使用位置：作为 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的参数、作为 Physics.SphereCastNonAlloc(Vector3, float, Vector3, RaycastHit[], float, int, QueryTriggerInteraction) 的参数。</param>
    /// <param name="count">中文含义：数量。输出参数，由本入口写入并交给调用方。使用位置：HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int)。</param>
    internal RaycastHit[] SphereCast(Vector3 origin, float radius, Vector3 direction, float distance, int mask, QueryTriggerInteraction triggers, out int count)
    {
        while (true)
        {
            count = Physics.SphereCastNonAlloc(origin, radius, direction, buffer, distance, mask, triggers);
            if (count < buffer.Length)
            {
                return buffer;
            }

            if (buffer.Length >= 1024)
            {
                // complete（完成）：保存 Physics.SphereCastAll(Vector3, float, Vector3, float, int, QueryTriggerInteraction) 的返回结果，供当前步骤使用。使用位置：HitQueryBuffer.SphereCast(Vector3, float, Vector3, float, int, QueryTriggerInteraction, out int)。
RaycastHit[] complete = Physics.SphereCastAll(origin, radius, direction, distance, mask, triggers);
                count = complete.Length;
                return complete;
            }

            System.Array.Resize(ref buffer, buffer.Length * 2);
        }
    }
}

using UnityEngine;

/// <summary>实例私有的物理查询缓冲。饱和时扩大，达到软上限后完整查询兜底。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "命中查询缓冲")]
internal sealed class HitQueryBuffer
{
    /// <summary>缓冲区。</summary>
    private RaycastHit[] buffer = new RaycastHit[64];
    /// <summary>由武器、敌人感知与导航等调用；执行射线查询，缓冲饱和时扩容，达到上限后使用完整查询返回全部命中。</summary>
    /// <param name="origin">原点。</param>
    /// <param name="direction">方向。</param>
    /// <param name="distance">距离。</param>
    /// <param name="mask">遮罩。</param>
    /// <param name="triggers">触发器碰撞查询策略。</param>
    /// <param name="count">数量。输出参数，由本入口写入并交给调用方。</param>
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
                // 缓冲达到上限，改用完整查询避免遗漏命中。
RaycastHit[] complete = Physics.RaycastAll(origin, direction, distance, mask, triggers);
                count = complete.Length;
                return complete;
            }

            System.Array.Resize(ref buffer, buffer.Length * 2);
        }
    }

    /// <summary>由武器及敌人窗口通行等调用；执行球体投射，缓冲饱和时扩容，达到上限后使用完整查询返回全部命中。</summary>
    /// <param name="origin">原点。</param>
    /// <param name="radius">半径。</param>
    /// <param name="direction">方向。</param>
    /// <param name="distance">距离。</param>
    /// <param name="mask">遮罩。</param>
    /// <param name="triggers">触发器碰撞查询策略。</param>
    /// <param name="count">数量。输出参数，由本入口写入并交给调用方。</param>
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
                // 缓冲达到上限，改用完整查询避免遗漏命中。
RaycastHit[] complete = Physics.SphereCastAll(origin, radius, direction, distance, mask, triggers);
                count = complete.Length;
                return complete;
            }

            System.Array.Resize(ref buffer, buffer.Length * 2);
        }
    }

    /// <summary>由敌人窗口通行调用；执行胶囊体投射，缓冲饱和时扩容，达到上限后使用完整查询返回全部命中。</summary>
    internal RaycastHit[] CapsuleCast(Vector3 bottom, Vector3 top, float radius, Vector3 direction, float distance, int mask, QueryTriggerInteraction triggers, out int count)
    {
        while (true)
        {
            count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, buffer, distance, mask, triggers);
            if (count < buffer.Length)
            {
                return buffer;
            }

            if (buffer.Length >= 1024)
            {
                RaycastHit[] complete = Physics.CapsuleCastAll(bottom, top, radius, direction, distance, mask, triggers);
                count = complete.Length;
                return complete;
            }

            System.Array.Resize(ref buffer, buffer.Length * 2);
        }
    }
}

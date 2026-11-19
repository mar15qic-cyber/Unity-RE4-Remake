using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 播放句柄：绑定一次播放的完整生命周期
/// 通过句柄编号防止旧计时任务误回收新播放
/// </summary>
public struct AudioPlayHandle
{
    /// <summary>中文含义：句柄编号。全局递增的句柄编号</summary>
    public readonly int handleId;
    /// <summary>中文含义：来源音源。关联的 AudioSource</summary>
    public readonly AudioSource source;
    /// <summary>中文含义：事件配置。关联的事件数据</summary>
    public readonly AudioEventData eventData;
    /// <summary>中文含义：是否有效。是否已失效</summary>
    public bool IsValid => source != null && handleId > 0 && AudioManager.IsValidHandle(handleId);

    /// <summary>构造并初始化 AudioPlayHandle 的声明状态；具体外部协作见静态调用链。</summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">中文含义：编号。使用位置：AudioPlayHandle.AudioPlayHandle(int, AudioSource, AudioEventData)。</param>
    /// <param name="src">中文含义：当前音源。使用位置：AudioPlayHandle.AudioPlayHandle(int, AudioSource, AudioEventData)。</param>
    /// <param name="data">中文含义：当前音频事件配置。使用位置：AudioPlayHandle.AudioPlayHandle(int, AudioSource, AudioEventData)。</param>
    public AudioPlayHandle(int id, AudioSource src, AudioEventData data)
    {
        handleId = id;
        source = src;
        eventData = data;
    }
}

/// <summary>
/// 三维音源对象池：复用 AudioSource，避免每次播放都新建和销毁 GameObject
/// 使用播放句柄系统保证每次 Acquire 最多对应一次 Release
/// </summary>
public class AudioSourcePool
{
    /// <summary>中文含义：对象池。只读字段，初始化后不由普通写入口替换。使用位置：作为 List&lt;AudioSource&gt;.RemoveAt(int) 的参数、作为 List&lt;AudioSource&gt;.this[int] 的参数、作为 Object.Destroy(Object) 的参数 等共 10 处。</summary>
    private readonly List<AudioSource> pool = new List<AudioSource>();
    /// <summary>中文含义：生效。只读字段，初始化后不由普通写入口替换。使用位置：作为 Action&lt;AudioSourcePool.PooledSource&gt;.Invoke(AudioSourcePool.PooledSource) 的参数、作为 List&lt;int&gt;.Add(int) 的参数、AudioSourcePool.Acquire(Vector3, AudioEventData, int) 等共 13 处。</summary>
    private readonly List<PooledSource> active = new List<PooledSource>();
    /// <summary>中文含义：对象池根节点。只读字段，初始化后不由普通写入口替换。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、AudioSourcePool.AudioSourcePool(Transform, int, int, Action&lt;int&gt;)。</summary>
    private readonly Transform poolRoot;
    /// <summary>中文含义：初始尺寸。只读字段，初始化后不由普通写入口替换。使用位置：作为 Mathf.Max(int, int) 的参数、AudioSourcePool.AudioSourcePool(Transform, int, int, Action&lt;int&gt;)。</summary>
    private readonly int initialSize;
    /// <summary>中文含义：最大尺寸。只读字段，初始化后不由普通写入口替换。使用位置：AudioSourcePool.Acquire(Vector3, AudioEventData, int)、AudioSourcePool.AudioSourcePool(Transform, int, int, Action&lt;int&gt;)、AudioSourcePool.DoRelease(AudioSource)。</summary>
    private readonly int maxSize;
    /// <summary>中文含义：句柄已释放。只读字段，初始化后不由普通写入口替换。使用位置：AudioSourcePool.Acquire(Vector3, AudioEventData, int)、AudioSourcePool.AudioSourcePool(Transform, int, int, Action&lt;int&gt;)、AudioSourcePool.RecycleFinished() 等共 5 处。</summary>
    private readonly System.Action<int> handleReleased;
    /// <summary>中文含义：是否对象池已暂停。使用位置：AudioSourcePool.PauseActive(Func&lt;AudioCategory, bool&gt;)、AudioSourcePool.RecycleFinished()、AudioSourcePool.ReleaseAll() 等共 4 处。</summary>
    private bool poolPaused;
    /// <summary>
    /// 池化的音源及其播放句柄信息
    /// </summary>
    public class PooledSource
    {
        /// <summary>中文含义：来源音源。使用位置：作为 AudioPlayHandle.AudioPlayHandle(int, AudioSource, AudioEventData) 的参数、作为 AudioSourcePool.DoRelease(AudioSource) 的参数、作为 List&lt;AudioSource&gt;.Add(AudioSource) 的参数 等共 12 处。</summary>
        public AudioSource source;
        /// <summary>中文含义：句柄编号。使用位置：作为 Action&lt;int&gt;.Invoke(int) 的参数、作为 List&lt;int&gt;.Add(int) 的参数、AudioSourcePool.Acquire(Vector3, AudioEventData, int) 等共 4 处。</summary>
        public int handleId;
        /// <summary>中文含义：事件配置。使用位置：AudioSourcePool.Acquire(Vector3, AudioEventData, int)、AudioSourcePool.GetActiveHandles(AudioEventData)、AudioSourcePool.RecycleFinished() 等共 5 处。</summary>
        public AudioEventData eventData;
        /// <summary>中文含义：是否已释放。使用位置：AudioSourcePool.Acquire(Vector3, AudioEventData, int)、AudioSourcePool.FindLowestPriorityActive()、AudioSourcePool.ForEachActive(Action&lt;AudioSourcePool.PooledSource&gt;) 等共 9 处。</summary>
        public bool isReleased;
        /// <summary>中文含义：是否循环。</summary>
        public bool isLoop;
        /// <summary>中文含义：事件音量。使用位置：AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)、AudioManager.RefreshActiveVolumes(AudioCategory)。</summary>
        public float eventVolume = 1f;
        /// <summary>中文含义：类别。使用位置：作为 Func&lt;AudioCategory, bool&gt;.Invoke(AudioCategory) 的参数、AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float) 等共 4 处。</summary>
        public AudioCategory category = AudioCategory.SFX;
        /// <summary>中文含义：是否已暂停依据系统。使用位置：AudioSourcePool.PauseActive(Func&lt;AudioCategory, bool&gt;)、AudioSourcePool.ResumePaused()。</summary>
        public bool pausedBySystem;
    }

    /// <summary>
    /// 构造对象池
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Awake()；下游（含内部回调体） AudioSourcePool.CreateSource()、List&lt;AudioSource&gt;.Add(AudioSource)、Mathf.Max(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根变换节点。使用位置：AudioSourcePool.AudioSourcePool(Transform, int, int, Action&lt;int&gt;)。</param>
    /// <param name="initialSize">中文含义：初始尺寸。使用位置：作为 Mathf.Max(int, int) 的参数。</param>
    /// <param name="maxSize">中文含义：最大尺寸。使用位置：作为 Mathf.Max(int, int) 的参数。</param>
    /// <param name="handleReleased">中文含义：句柄已释放。使用位置：AudioSourcePool.AudioSourcePool(Transform, int, int, Action&lt;int&gt;)。</param>
    public AudioSourcePool(Transform root, int initialSize, int maxSize, System.Action<int> handleReleased = null)
    {
        this.poolRoot = root;
        this.initialSize = Mathf.Max(1, initialSize);
        this.maxSize = Mathf.Max(this.initialSize, maxSize);
        this.handleReleased = handleReleased;
        // i（当前遍历索引）：使用位置：AudioSourcePool.AudioSourcePool(Transform, int, int, Action<int>)。
for (int i = 0; i < this.initialSize; i++)
        {
            pool.Add(CreateSource());
        }
    }

    /// <summary>
    /// 从池中获取一个 AudioSource，返回包含句柄的 PooledSource
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、AudioEventData.OnPlayFinished()、AudioSource.Stop()、AudioSourcePool.CreateSource()、AudioSourcePool.FindLowestPriorityActive()、AudioSourcePool.PooledSource.PooledSource()、GameObject.SetActive(bool) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="position">中文含义：位置。使用位置：AudioSourcePool.Acquire(Vector3, AudioEventData, int)。</param>
    /// <param name="eventData">中文含义：事件配置。使用位置：AudioSourcePool.Acquire(Vector3, AudioEventData, int)。</param>
    /// <param name="handleId">中文含义：句柄编号。使用位置：AudioSourcePool.Acquire(Vector3, AudioEventData, int)。</param>
    public PooledSource Acquire(Vector3 position, AudioEventData eventData, int handleId)
    {
        // source（来源音源）：使用位置：AudioSourcePool.Acquire(Vector3, AudioEventData, int)。
AudioSource source = null;
        // oldPs（旧粒子系统）：使用位置：作为 Action<int>.Invoke(int) 的参数、作为 List<AudioSourcePool.PooledSource>.Remove(AudioSourcePool.PooledSource) 的参数、AudioSourcePool.Acquire(Vector3, AudioEventData, int)。
PooledSource oldPs = null;
        // 优先从空闲池中取
        if (pool.Count > 0)
        {
            source = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
        }
        // 空闲池为空但未达上限，新建一个
        else if (active.Count + pool.Count < maxSize)
        {
            source = CreateSource();
        }
        // 已达上限，抢占优先级最低的活跃源
        else
        {
            oldPs = FindLowestPriorityActive();
            if (oldPs != null)
            {
                source = oldPs.source;
                // 结算旧事件：使旧句柄失效
                oldPs.isReleased = true;
                // receiverValue1（判空前缓存的旧粒子系统的事件配置）：从旧粒子系统的事件配置读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.Acquire(Vector3, AudioEventData, int)。
global::AudioEventData receiverValue1 = oldPs.eventData;
                if (!global::System.Object.ReferenceEquals(receiverValue1, null))
                {
                    receiverValue1.OnPlayFinished();
                }

                // receiverValue2（判空前缓存的句柄已释放）：从句柄已释放读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.Acquire(Vector3, AudioEventData, int)。
global::System.Action<int> receiverValue2 = handleReleased;
                if (!global::System.Object.ReferenceEquals(receiverValue2, null))
                {
                    receiverValue2.Invoke(oldPs.handleId);
                }

                active.Remove(oldPs);
                source.Stop();
            }
        }

        if (source == null)
        {
            return null;
        }

        source.transform.position = position;
        source.gameObject.SetActive(true);
        // ps（粒子系统）：使用位置：作为 List<AudioSourcePool.PooledSource>.Add(AudioSourcePool.PooledSource) 的参数、AudioSourcePool.Acquire(Vector3, AudioEventData, int)。
var ps = new PooledSource
        {
            source = source,
            handleId = handleId,
            eventData = eventData,
            isReleased = false
        };
        active.Add(ps);
        return ps;
    }

    /// <summary>
    /// 通过句柄回收 AudioSource（幂等，同一句柄只能回收一次）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.ReleaseAfterHandle(int, float, AudioSourcePool)、AudioManager.Stop(AudioPlayHandle)、AudioManager.StopAllInPool(AudioSourcePool, AudioEventData)；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、AudioEventData.OnPlayFinished()、AudioSourcePool.DoRelease(AudioSource)、List&lt;AudioSourcePool.PooledSource&gt;.RemoveAt(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="handleId">中文含义：句柄编号。使用位置：AudioSourcePool.Release(int)。</param>
    public bool Release(int handleId)
    {
        // i（当前遍历索引）：使用位置：作为 List<AudioSourcePool.PooledSource>.RemoveAt(int) 的参数、作为 List<AudioSourcePool.PooledSource>.this[int] 的参数、AudioSourcePool.Release(int)。
for (int i = 0; i < active.Count; i++)
        {
            // ps（粒子系统）：使用位置：作为 Action<int>.Invoke(int) 的参数、作为 AudioSourcePool.DoRelease(AudioSource) 的参数、AudioSourcePool.Release(int)。
var ps = active[i];
            if (ps.handleId == handleId && !ps.isReleased)
            {
                ps.isReleased = true;
                // receiverValue3（判空前缓存的粒子系统的事件配置）：从粒子系统的事件配置读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.Release(int)。
global::AudioEventData receiverValue3 = ps.eventData;
                if (!global::System.Object.ReferenceEquals(receiverValue3, null))
                {
                    receiverValue3.OnPlayFinished();
                }

                // receiverValue4（判空前缓存的句柄已释放）：从句柄已释放读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.Release(int)。
global::System.Action<int> receiverValue4 = handleReleased;
                if (!global::System.Object.ReferenceEquals(receiverValue4, null))
                {
                    receiverValue4.Invoke(ps.handleId);
                }

                DoRelease(ps.source);
                active.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 回收指定 AudioSource（仅用于 RecycleFinished，内部检查有效性）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Update()；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、AudioEventData.OnPlayFinished()、AudioSourcePool.DoRelease(AudioSource)、List&lt;AudioSourcePool.PooledSource&gt;.RemoveAt(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void RecycleFinished()
    {
        if (poolPaused)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：作为 List<AudioSourcePool.PooledSource>.RemoveAt(int) 的参数、作为 List<AudioSourcePool.PooledSource>.this[int] 的参数、AudioSourcePool.RecycleFinished()。
for (int i = active.Count - 1; i >= 0; i--)
        {
            // ps（粒子系统）：使用位置：作为 Action<int>.Invoke(int) 的参数、作为 AudioSourcePool.DoRelease(AudioSource) 的参数、AudioSourcePool.RecycleFinished()。
var ps = active[i];
            if (ps.source == null)
            {
                // receiverValue5（判空前缓存的粒子系统的事件配置）：从粒子系统的事件配置读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.RecycleFinished()。
global::AudioEventData receiverValue5 = ps.eventData;
                if (!global::System.Object.ReferenceEquals(receiverValue5, null))
                {
                    receiverValue5.OnPlayFinished();
                }

                // receiverValue6（判空前缓存的句柄已释放）：从句柄已释放读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.RecycleFinished()。
global::System.Action<int> receiverValue6 = handleReleased;
                if (!global::System.Object.ReferenceEquals(receiverValue6, null))
                {
                    receiverValue6.Invoke(ps.handleId);
                }

                active.RemoveAt(i);
                continue;
            }

            if (!ps.source.isPlaying && !ps.source.loop && !ps.isReleased)
            {
                ps.isReleased = true;
                // receiverValue7（判空前缓存的粒子系统的事件配置）：从粒子系统的事件配置读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.RecycleFinished()。
global::AudioEventData receiverValue7 = ps.eventData;
                if (!global::System.Object.ReferenceEquals(receiverValue7, null))
                {
                    receiverValue7.OnPlayFinished();
                }

                // receiverValue8（判空前缓存的句柄已释放）：从句柄已释放读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.RecycleFinished()。
global::System.Action<int> receiverValue8 = handleReleased;
                if (!global::System.Object.ReferenceEquals(receiverValue8, null))
                {
                    receiverValue8.Invoke(ps.handleId);
                }

                DoRelease(ps.source);
                active.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 停止并回收所有活跃音源，使所有句柄失效
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.OnSceneUnloaded(Scene)、AudioManager.StopSFX()、AudioSourcePool.Dispose()；下游（含内部回调体） Action&lt;int&gt;.Invoke(int)、AudioEventData.OnPlayFinished()、AudioSource.Stop()、GameObject.SetActive(bool)、List&lt;AudioSource&gt;.Add(AudioSource)、List&lt;AudioSourcePool.PooledSource&gt;.Clear()、object.ReferenceEquals(object, object) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ReleaseAll()
    {
        poolPaused = false;
        // i（当前遍历索引）：使用位置：作为 List<AudioSourcePool.PooledSource>.this[int] 的参数、AudioSourcePool.ReleaseAll()。
for (int i = 0; i < active.Count; i++)
        {
            // ps（粒子系统）：使用位置：作为 Action<int>.Invoke(int) 的参数、作为 List<AudioSource>.Add(AudioSource) 的参数、AudioSourcePool.ReleaseAll()。
var ps = active[i];
            if (ps.source != null)
            {
                ps.source.Stop();
                ps.source.clip = null;
                ps.source.gameObject.SetActive(false);
                ps.source.transform.SetParent(poolRoot, false);
                pool.Add(ps.source);
            }

            ps.isReleased = true;
            // receiverValue9（判空前缓存的粒子系统的事件配置）：从粒子系统的事件配置读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.ReleaseAll()。
global::AudioEventData receiverValue9 = ps.eventData;
            if (!global::System.Object.ReferenceEquals(receiverValue9, null))
            {
                receiverValue9.OnPlayFinished();
            }

            // receiverValue10（判空前缓存的句柄已释放）：从句柄已释放读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 object.ReferenceEquals(object, object) 的参数、AudioSourcePool.ReleaseAll()。
global::System.Action<int> receiverValue10 = handleReleased;
            if (!global::System.Object.ReferenceEquals(receiverValue10, null))
            {
                receiverValue10.Invoke(ps.handleId);
            }
        }

        active.Clear();
    }

    /// <summary>
    /// 暂停所有匹配条件的活跃音源。暂停后 RecycleFinished 不会回收任何音源，
    /// 防止被暂停的非循环音效因 isPlaying==false 被提前回收。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.TransitionToPaused()；下游（含内部回调体） AudioSource.Pause()、Func&lt;AudioCategory, bool&gt;.Invoke(AudioCategory)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="shouldPause">中文含义：是否应当暂停。使用位置：AudioSourcePool.PauseActive(Func&lt;AudioCategory, bool&gt;)。</param>
    public void PauseActive(System.Func<AudioCategory, bool> shouldPause)
    {
        if (poolPaused)
        {
            return;
        }

        poolPaused = true;
        // i（当前遍历索引）：使用位置：作为 List<AudioSourcePool.PooledSource>.this[int] 的参数、AudioSourcePool.PauseActive(Func<AudioCategory, bool>)。
for (int i = 0; i < active.Count; i++)
        {
            // ps（粒子系统）：使用位置：作为 Func<AudioCategory, bool>.Invoke(AudioCategory) 的参数、AudioSourcePool.PauseActive(Func<AudioCategory, bool>)。
var ps = active[i];
            if (ps.isReleased || ps.source == null)
            {
                continue;
            }

            if (!shouldPause(ps.category))
            {
                continue;
            }

            if (ps.source.isPlaying)
            {
                ps.pausedBySystem = true;
                ps.source.Pause();
            }
        }
    }

    /// <summary>
    /// 恢复所有被 PauseActive 暂停的音源，从原播放进度继续。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.TransitionToNormal()；下游（含内部回调体） AudioSource.UnPause()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResumePaused()
    {
        if (!poolPaused)
        {
            return;
        }

        poolPaused = false;
        // i（当前遍历索引）：使用位置：作为 List<AudioSourcePool.PooledSource>.this[int] 的参数、AudioSourcePool.ResumePaused()。
for (int i = 0; i < active.Count; i++)
        {
            // ps（粒子系统）：使用位置：AudioSourcePool.ResumePaused()。
var ps = active[i];
            if (!ps.pausedBySystem)
            {
                continue;
            }

            ps.pausedBySystem = false;
            if (ps.source != null)
            {
                ps.source.UnPause();
            }
        }
    }

    /// <summary>
    /// 清理所有资源
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.OnDestroy()；下游（含内部回调体） AudioSourcePool.ReleaseAll()、List&lt;AudioSource&gt;.Clear()、Object.Destroy(Object)、Object.DestroyImmediate(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Dispose()
    {
        ReleaseAll();
        // i（当前遍历索引）：使用位置：作为 List<AudioSource>.this[int] 的参数、AudioSourcePool.Dispose()。
for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && pool[i].gameObject != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(pool[i].gameObject);
                }
                else
                {
                    Object.DestroyImmediate(pool[i].gameObject);
                }
            }
        }

        pool.Clear();
    }

    /// <summary>中文含义：生效数量。获取当前活跃音源数量</summary>
    public int ActiveCount => active.Count;
    /// <summary>中文含义：对象池数量。获取空闲池大小</summary>
    public int PoolCount => pool.Count;

    /// <summary>
    /// 获取活跃音源的句柄列表（用于外部验证）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） List&lt;int&gt;.Add(int)、List&lt;int&gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public List<int> GetActiveHandles()
    {
        // handles（句柄集合）：使用位置：AudioSourcePool.GetActiveHandles()。
var handles = new List<int>();
        // i（当前遍历索引）：使用位置：作为 List<AudioSourcePool.PooledSource>.this[int] 的参数、AudioSourcePool.GetActiveHandles()。
for (int i = 0; i < active.Count; i++)
        {
            if (!active[i].isReleased)
            {
                handles.Add(active[i].handleId);
            }
        }

        return handles;
    }

    /// <summary>
    /// 获取指定事件的活跃句柄列表。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.StopAllInPool(AudioSourcePool, AudioEventData)；下游（含内部回调体） List&lt;int&gt;.Add(int)、List&lt;int&gt;.List()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">中文含义：事件配置。使用位置：AudioSourcePool.GetActiveHandles(AudioEventData)。</param>
    public List<int> GetActiveHandles(AudioEventData eventData)
    {
        // handles（句柄集合）：使用位置：AudioSourcePool.GetActiveHandles(AudioEventData)。
var handles = new List<int>();
        // i（当前遍历索引）：使用位置：作为 List<AudioSourcePool.PooledSource>.this[int] 的参数、AudioSourcePool.GetActiveHandles(AudioEventData)。
for (int i = 0; i < active.Count; i++)
        {
            if (!active[i].isReleased && active[i].eventData == eventData)
            {
                handles.Add(active[i].handleId);
            }
        }

        return handles;
    }

    /// <summary>
    /// 遍历当前活跃音源，用于分类音量变化时立即刷新播放中的声音。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.RefreshActiveVolumes(AudioCategory)；下游（含内部回调体） Action&lt;AudioSourcePool.PooledSource&gt;.Invoke(AudioSourcePool.PooledSource)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="action">中文含义：动作。使用位置：AudioSourcePool.ForEachActive(Action&lt;AudioSourcePool.PooledSource&gt;)。</param>
    public void ForEachActive(System.Action<PooledSource> action)
    {
        if (action == null)
        {
            return;
        }

        // i（当前遍历索引）：使用位置：作为 List<AudioSourcePool.PooledSource>.this[int] 的参数、AudioSourcePool.ForEachActive(Action<AudioSourcePool.PooledSource>)。
for (int i = 0; i < active.Count; i++)
        {
            if (!active[i].isReleased)
            {
                action(active[i]);
            }
        }
    }

    /// <summary>DoRelease 的业务处理入口；本方法直接依赖 AudioSource.Stop()、GameObject.SetActive(bool)、List&lt;AudioSource&gt;.Add(AudioSource)、Object.Destroy(Object)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioSourcePool.RecycleFinished()、AudioSourcePool.Release(int)；下游（含内部回调体） AudioSource.Stop()、GameObject.SetActive(bool)、List&lt;AudioSource&gt;.Add(AudioSource)、Object.Destroy(Object)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">中文含义：来源音源。使用位置：作为 List&lt;AudioSource&gt;.Add(AudioSource) 的参数、作为 Object.Destroy(Object) 的参数、AudioSourcePool.DoRelease(AudioSource)。</param>
    private void DoRelease(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
        source.outputAudioMixerGroup = null;
        source.gameObject.SetActive(false);
        source.transform.SetParent(poolRoot, false);
        source.transform.localPosition = Vector3.zero;
        if (pool.Count >= maxSize)
        {
            Object.Destroy(source.gameObject);
        }
        else
        {
            pool.Add(source);
        }
    }

    /// <summary>CreateSource 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioSourcePool.Acquire(Vector3, AudioEventData, int)、AudioSourcePool.AudioSourcePool(Transform, int, int, Action&lt;int&gt;)；下游（含内部回调体） GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)、GameObject.SetActive(bool)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private AudioSource CreateSource()
    {
        // go（当前游戏对象）：使用位置：AudioSourcePool.CreateSource()。
var go = new GameObject("AudioPool_Source");
        go.transform.SetParent(poolRoot, false);
        go.transform.localPosition = Vector3.zero;
        go.SetActive(false);
        // source（来源音源）：保存 GameObject.AddComponent<AudioSource>() 的返回结果，供当前步骤使用。使用位置：AudioSourcePool.CreateSource()。
var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        return source;
    }

    /// <summary>FindLowestPriorityActive 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 AudioSourcePool.Acquire(Vector3, AudioEventData, int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private PooledSource FindLowestPriorityActive()
    {
        if (active.Count == 0)
        {
            return null;
        }

        // lowest（最低）：使用位置：AudioSourcePool.FindLowestPriorityActive()。
PooledSource lowest = null;
        // lowestPriority（最低优先级）：使用位置：AudioSourcePool.FindLowestPriorityActive()。
int lowestPriority = int.MinValue;
        // ps（粒子系统）：每轮从生效取得一个元素，仅代表当前这一项。使用位置：AudioSourcePool.FindLowestPriorityActive()。
foreach (var ps in active)
        {
            if (ps.isReleased || ps.source == null)
            {
                continue;
            }

            // 非循环的优先抢占
            if (ps.source.loop)
            {
                continue;
            }

            if (ps.source.priority > lowestPriority)
            {
                lowestPriority = ps.source.priority;
                lowest = ps;
            }
        }

        // 如果没有非循环的，就抢占最低优先级的
        if (lowest == null)
        {
            // ps（粒子系统）：每轮从生效取得一个元素，仅代表当前这一项。使用位置：AudioSourcePool.FindLowestPriorityActive()。
foreach (var ps in active)
            {
                if (ps.isReleased || ps.source == null)
                {
                    continue;
                }

                if (ps.source.priority > lowestPriority)
                {
                    lowestPriority = ps.source.priority;
                    lowest = ps;
                }
            }
        }

        return lowest;
    }
}

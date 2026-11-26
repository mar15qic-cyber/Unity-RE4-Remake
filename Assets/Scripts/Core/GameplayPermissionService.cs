using System;
using System.Collections.Generic;
using GameArchitecture;

/// <summary>
/// 许可合并后的不可变快照。服务通过 Current 和 HasChange 同步交给宿主，
/// GameManager.ApplyPermission 将结果应用到时间、音频和输入。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "玩法许可快照")]
public readonly struct GameplayPermissionSnapshot
{
    /// <summary>全部占用均允许时开放玩家输入。</summary>
    public bool AllowInput { get; }
    /// <summary>全部占用均允许时推进任务计时。</summary>
    public bool AllowMissionTimer { get; }
    /// <summary>时间倍率大于零时允许模拟推进。</summary>
    public bool AllowSimulation => TimeMultiplier > 0;
    /// <summary>任一占用要求暂停音频时为 true。</summary>
    public bool PauseAudio { get; }
    /// <summary>所有占用倍率与默认倍率 1 的最小值。</summary>
    public float TimeMultiplier { get; }

    /// <summary>服务合并占用后构造结果，通过值复制传递给消费者。</summary>
    /// <param name="input">是否允许玩家输入。</param>
    /// <param name="timer">是否允许任务计时。</param>
    /// <param name="audioPause">是否暂停音频。</param>
    /// <param name="multiplier">应由宿主应用的时间倍率。</param>
    public GameplayPermissionSnapshot(bool input, bool timer, bool audioPause, float multiplier)
    {
        AllowInput = input;
        AllowMissionTimer = timer;
        PauseAudio = audioPause;
        TimeMultiplier = multiplier;
    }
}

/// <summary>
/// 本局玩法占用的唯一拥有者。启动、界面和终局宿主通过 Request/Update/Release 提交意图，
/// WeightCalculate 合并后更新 Current 并通过 HasChange 同步通知宿主；服务不直接操作引擎状态。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "玩法许可服务")]
public sealed class GameplayPermissionService
{
    /// <summary>不可变占用记录；请求时写入，更新时整份替换，释放时删除。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "玩法许可服务+占用")]
    private readonly struct PermissionLease
    {
        /// <summary>请求方身份，更新与释放时须和句柄一致。</summary>
        internal readonly ObjectIdentity requester;
        /// <summary>该占用是否阻断玩家输入。</summary>
        internal readonly bool blockInput;
        /// <summary>该占用是否阻断任务计时。</summary>
        internal readonly bool blockTimer;
        /// <summary>该占用是否暂停音频。</summary>
        internal readonly bool pauseAudio;
        /// <summary>该占用要求的时间倍率，合并时取最小值。</summary>
        internal readonly float multiplier;
        /// <summary>Request 或 Update 将已经校验的参数复制为内部占用记录。</summary>
        /// <param name="requester">占用所属对象身份。</param>
        /// <param name="input">输入阻断要求。</param>
        /// <param name="timer">任务计时阻断要求。</param>
        /// <param name="audio">音频暂停要求。</param>
        /// <param name="multiplier">非负有限的时间倍率。</param>
        internal PermissionLease(ObjectIdentity requester, bool input, bool timer, bool audio, float multiplier)
        {
            this.requester = requester;
            blockInput = input;
            blockTimer = timer;
            pauseAudio = audio;
            this.multiplier = multiplier;
        }
    }

    /// <summary>服务实例身份序列；原子递增防止不同服务复用身份。</summary>
    private static int lowerElementService;
    /// <summary>本服务身份；跨局或其他服务的句柄不能操作本局占用。</summary>
    private readonly ObjectIdentity identity = new ObjectIdentity(System.Threading.Interlocked.Increment(ref lowerElementService), 1);
    /// <summary>按请求编号保存有效占用；只有本服务可以增删或替换记录。</summary>
    private readonly Dictionary<long, PermissionLease> record = new Dictionary<long, PermissionLease>();
    /// <summary>请求编号序列；释放后不回退，避免旧句柄命中新记录。</summary>
    private long lowerElementRequest;
    /// <summary>关闭标志，关闭后拒绝新增占用和更新。</summary>
    private bool hasClose;
    /// <summary>最近的完整合并结果；新订阅者可直接读取当前状态。</summary>
    public GameplayPermissionSnapshot Current { get; private set; } = new GameplayPermissionSnapshot(true, true, false, 1);
    /// <summary>当前有效占用数量，供宿主和验收查询。</summary>
    public int OccupancyCount => record.Count;

    /// <summary>结果变化后同步广播；GameManager 订阅并在销毁时退订。</summary>
    public event Action<GameplayPermissionSnapshot> HasChange;
    /// <summary>宿主申请占用，写入后立即合并并按需通知；返回句柄供所属对象更新或释放。</summary>
    /// <param name="source">有效请求方身份。</param>
    /// <param name="blockInput">是否阻断玩家输入。</param>
    /// <param name="blockTimer">是否阻断任务计时。</param>
    /// <param name="pauseAudio">是否暂停音频。</param>
    /// <param name="timeMultiplier">非负有限倍率；默认 1，0 暂停模拟。</param>
    /// <returns>新占用句柄；服务关闭或参数无效时返回默认值。</returns>
    public GameplayPermissionHandle Request(ObjectIdentity source, bool blockInput, bool blockTimer, bool pauseAudio, float timeMultiplier = 1)
    {
        if (hasClose || !source.Valid || float.IsNaN(timeMultiplier) || float.IsInfinity(timeMultiplier) || timeMultiplier < 0)
        {
            return default;
        }

        // 新记录编号，同时写入字典并返回请求方。
long id = ++lowerElementRequest;
        record.Add(id, new PermissionLease(source, blockInput, blockTimer, pauseAudio, timeMultiplier));
        WeightCalculate();
        return new GameplayPermissionHandle(identity, source, id);
    }

    /// <summary>持有者释放自己的占用，剩余记录重新合并；不会解除其他请求方的阻断。</summary>
    /// <param name="handle">Request 返回的句柄，须匹配服务、请求编号和请求方。</param>
    /// <returns>是否删除成功；重复释放和外来句柄返回 false。</returns>
    public bool Release(GameplayPermissionHandle handle)
    {
        // item 是按句柄取出的占用，用于核验请求方；保持原短路查询顺序。
        if (handle.Service != identity || !record.TryGetValue(handle.Id, out var /* 中文含义：当前许可租用。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ item) || item.requester != handle.Requester)
        {
            return false;
        }

        record.Remove(handle.Id);
        WeightCalculate();
        return true;
    }

    /// <summary>持有者替换一份有效占用的要求，再通过同一合并和事件链通知宿主。</summary>
    /// <param name="handle">当前有效占用句柄。</param>
    /// <param name="blockInput">更新后的输入阻断要求。</param>
    /// <param name="blockTimer">更新后的计时阻断要求。</param>
    /// <param name="pauseAudio">更新后的音频暂停要求。</param>
    /// <param name="timeMultiplier">更新后的非负有限时间倍率。</param>
    /// <returns>句柄和参数均有效时返回 true。</returns>
    public bool Update(GameplayPermissionHandle handle, bool blockInput, bool blockTimer, bool pauseAudio, float timeMultiplier)
    {
        // item 提供原始占用归属，更新不能把占用转移到另一个请求方。
        if (hasClose || handle.Service != identity || !record.TryGetValue(handle.Id, out var /* 中文含义：当前许可租用。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ item) || item.requester != handle.Requester || float.IsNaN(timeMultiplier) || float.IsInfinity(timeMultiplier) || timeMultiplier < 0)
        {
            return false;
        }

        record[handle.Id] = new PermissionLease(item.requester, blockInput, blockTimer, pauseAudio, timeMultiplier);
        WeightCalculate();
        return true;
    }

    /// <summary>宿主销毁或切场景时调用；幂等清空占用并发布恢复后的默认许可。</summary>
    public void Close()
    {
        if (hasClose)
        {
            return;
        }

        hasClose = true;
        record.Clear();
        WeightCalculate();
    }

    /// <summary>所有写入口共用：阻断取并集，倍率取最小值；快照写入后才同步发送变更通知。</summary>
    private void WeightCalculate()
    {
        // 输入从允许开始，任一占用可阻断。
bool input = true;
        // 计时从允许开始，任一占用可阻断。
bool timer = true;
        // 音频从不暂停开始，任一占用可要求暂停。
bool audio = false;
        // 默认正常速度，占用可通过更小倍率进一步限制。
float multiplier = 1;
        // 本轮参与合并的一份占用；遍历期间不修改字典。
foreach (var item in record.Values)
        {
            input &= !item.blockInput;
            timer &= !item.blockTimer;
            audio |= item.pauseAudio;
            multiplier = Math.Min(multiplier, item.multiplier);
        }

        // 一次性替换完整结果，保证通知期间读取 Current 得到同一版本。
var newValue = new GameplayPermissionSnapshot(input, timer, audio, multiplier);
        // 对比可观察字段，抑制结果未变化的重复通知。
bool change = Current.AllowInput != input || Current.AllowMissionTimer != timer || Current.PauseAudio != audio || Current.TimeMultiplier != multiplier;
        Current = newValue;
        if (change)
        {
            // 复制委托保留原空值条件调用的单次读取语义。
Action<GameplayPermissionSnapshot> handler = HasChange;
            if (handler != null)
            {
                handler(Current);
            }
        }
    }
}

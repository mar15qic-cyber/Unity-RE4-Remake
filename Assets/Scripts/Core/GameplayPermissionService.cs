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
    /// <summary>中文含义：是否允许输入。全部占用均允许时开放玩家输入。</summary>
    public bool AllowInput { get; }
    /// <summary>中文含义：是否允许任务计时器。全部占用均允许时推进任务计时。</summary>
    public bool AllowMissionTimer { get; }
    /// <summary>中文含义：是否允许模拟。时间倍率大于零时允许模拟推进。</summary>
    public bool AllowSimulation => TimeMultiplier > 0;
    /// <summary>中文含义：是否暂停音频。任一占用要求暂停音频时为 true。</summary>
    public bool PauseAudio { get; }
    /// <summary>中文含义：时间倍率。所有占用倍率与默认倍率 1 的最小值。</summary>
    public float TimeMultiplier { get; }

    /// <summary>服务合并占用后构造结果，通过值复制传递给消费者。</summary>
    /// <param name="input">中文含义：是否输入数据。是否允许玩家输入。</param>
    /// <param name="timer">中文含义：是否计时器。是否允许任务计时。</param>
    /// <param name="audioPause">中文含义：是否音频暂停。是否暂停音频。</param>
    /// <param name="multiplier">中文含义：倍率。应由宿主应用的时间倍率。</param>
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
        /// <summary>中文含义：请求方。请求方身份，更新与释放时须和句柄一致。</summary>
        internal readonly ObjectIdentity requester;
        /// <summary>中文含义：是否阻断输入。该占用是否阻断玩家输入。</summary>
        internal readonly bool blockInput;
        /// <summary>中文含义：是否阻断计时器。该占用是否阻断任务计时。</summary>
        internal readonly bool blockTimer;
        /// <summary>中文含义：是否暂停音频。该占用是否暂停音频。</summary>
        internal readonly bool pauseAudio;
        /// <summary>中文含义：倍率。该占用要求的时间倍率，合并时取最小值。</summary>
        internal readonly float multiplier;
        /// <summary>Request 或 Update 将已经校验的参数复制为内部占用记录。</summary>
        /// <param name="requester">中文含义：请求方。占用所属对象身份。</param>
        /// <param name="input">中文含义：是否输入数据。输入阻断要求。</param>
        /// <param name="timer">中文含义：是否计时器。任务计时阻断要求。</param>
        /// <param name="audio">中文含义：是否音频。音频暂停要求。</param>
        /// <param name="multiplier">中文含义：倍率。非负有限的时间倍率。</param>
        internal PermissionLease(ObjectIdentity requester, bool input, bool timer, bool audio, float multiplier)
        {
            this.requester = requester;
            blockInput = input;
            blockTimer = timer;
            pauseAudio = audio;
            this.multiplier = multiplier;
        }
    }

    /// <summary>中文含义：下部元素服务。服务实例身份序列；原子递增防止不同服务复用身份。</summary>
    private static int lowerElementService;
    /// <summary>中文含义：身份标识。本服务身份；跨局或其他服务的句柄不能操作本局占用。</summary>
    private readonly ObjectIdentity identity = new ObjectIdentity(System.Threading.Interlocked.Increment(ref lowerElementService), 1);
    /// <summary>中文含义：当前键值映射表。按请求编号保存有效占用；只有本服务可以增删或替换记录。</summary>
    private readonly Dictionary<long, PermissionLease> record = new Dictionary<long, PermissionLease>();
    /// <summary>中文含义：下部元素请求。请求编号序列；释放后不回退，避免旧句柄命中新记录。</summary>
    private long lowerElementRequest;
    /// <summary>中文含义：是否具有关闭。关闭标志，关闭后拒绝新增占用和更新。</summary>
    private bool hasClose;
    /// <summary>中文含义：当前。最近的完整合并结果；新订阅者可直接读取当前状态。</summary>
    public GameplayPermissionSnapshot Current { get; private set; } = new GameplayPermissionSnapshot(true, true, false, 1);
    /// <summary>中文含义：占用数量。当前有效占用数量，供宿主和验收查询。</summary>
    public int OccupancyCount => record.Count;

    /// <summary>中文含义：是否具有变化。结果变化后同步广播；GameManager 订阅并在销毁时退订。</summary>
    public event Action<GameplayPermissionSnapshot> HasChange;
    /// <summary>宿主申请占用，写入后立即合并并按需通知；返回句柄供所属对象更新或释放。</summary>
    /// <param name="source">中文含义：来源对象身份标识。有效请求方身份。</param>
    /// <param name="blockInput">中文含义：是否阻断输入。是否阻断玩家输入。</param>
    /// <param name="blockTimer">中文含义：是否阻断计时器。是否阻断任务计时。</param>
    /// <param name="pauseAudio">中文含义：是否暂停音频。是否暂停音频。</param>
    /// <param name="timeMultiplier">中文含义：时间倍率。非负有限倍率；默认 1，0 暂停模拟。</param>
    /// <returns>新占用句柄；服务关闭或参数无效时返回默认值。</returns>
    public GameplayPermissionHandle Request(ObjectIdentity source, bool blockInput, bool blockTimer, bool pauseAudio, float timeMultiplier = 1)
    {
        if (hasClose || !source.Valid || float.IsNaN(timeMultiplier) || float.IsInfinity(timeMultiplier) || timeMultiplier < 0)
        {
            return default;
        }

        // 新记录编号，同时写入字典并返回请求方。
        // 中文含义：id 为“编号”。
long id = ++lowerElementRequest;
        record.Add(id, new PermissionLease(source, blockInput, blockTimer, pauseAudio, timeMultiplier));
        WeightCalculate();
        return new GameplayPermissionHandle(identity, source, id);
    }

    /// <summary>持有者释放自己的占用，剩余记录重新合并；不会解除其他请求方的阻断。</summary>
    /// <param name="handle">中文含义：句柄。Request 返回的句柄，须匹配服务、请求编号和请求方。</param>
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
    /// <param name="handle">中文含义：句柄。当前有效占用句柄。</param>
    /// <param name="blockInput">中文含义：是否阻断输入。更新后的输入阻断要求。</param>
    /// <param name="blockTimer">中文含义：是否阻断计时器。更新后的计时阻断要求。</param>
    /// <param name="pauseAudio">中文含义：是否暂停音频。更新后的音频暂停要求。</param>
    /// <param name="timeMultiplier">中文含义：时间倍率。更新后的非负有限时间倍率。</param>
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
        // 中文含义：input 为“是否输入数据”。
bool input = true;
        // 计时从允许开始，任一占用可阻断。
        // 中文含义：timer 为“是否计时器”。
bool timer = true;
        // 音频从不暂停开始，任一占用可要求暂停。
        // 中文含义：audio 为“是否音频”。
bool audio = false;
        // 默认正常速度，占用可通过更小倍率进一步限制。
        // 中文含义：multiplier 为“倍率”。
float multiplier = 1;
        // 本轮参与合并的一份占用；遍历期间不修改字典。
        // 中文含义：item 为“当前许可租用”。每轮从记录的数值集合取得一个元素，仅代表当前这一项。
foreach (var item in record.Values)
        {
            input &= !item.blockInput;
            timer &= !item.blockTimer;
            audio |= item.pauseAudio;
            multiplier = Math.Min(multiplier, item.multiplier);
        }

        // 一次性替换完整结果，保证通知期间读取 Current 得到同一版本。
        // 中文含义：newValue 为“新值”。
var newValue = new GameplayPermissionSnapshot(input, timer, audio, multiplier);
        // 对比可观察字段，抑制结果未变化的重复通知。
        // 中文含义：change 为“是否变化”。
bool change = Current.AllowInput != input || Current.AllowMissionTimer != timer || Current.PauseAudio != audio || Current.TimeMultiplier != multiplier;
        Current = newValue;
        if (change)
        {
            // 复制委托保留原空值条件调用的单次读取语义。
            // 中文含义：handler 为“处理器”。
Action<GameplayPermissionSnapshot> handler = HasChange;
            if (handler != null)
            {
                handler(Current);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>按需采集纯数据，后台格式化和写入；停用时没有诊断分配。</summary>
[DefaultExecutionOrder(-300)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人战斗时间轴")]
public class EnemyCombatTimeline : MonoBehaviour
{
    /// <summary>是否启用记录中。</summary>
    [Header("记录设置")]
    public bool enableRecording;
    /// <summary>是否首个波次仅。</summary>
    public bool firstWaveOnly = true;
    /// <summary>快照间隔。</summary>
    [Min(.1f)]
    public float snapshotInterval = .5f;
    /// <summary>选定敌人。</summary>
    public VillagerController selectedEnemy;
    /// <summary>当前敌人智能管理器。</summary>
    [Header("场景引用")]
    public EnemyAIManager manager;
    /// <summary>玩家。</summary>
    public LeonController player;
    /// <summary>运行诊断模块 Entry；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    private sealed class Entry
    {
        /// <summary>敌人。</summary>
        internal VillagerController enemy;
        /// <summary>number 表示“数量”；wave 表示“波次”；key 表示“键”。</summary>
        internal int number, wave, key;
        /// <summary>上一次位置。</summary>
        internal Vector3 lastPosition;
        /// <summary>lastSampleTime 表示“上一次采样时间”；stillDuration 表示“仍然持续时间”；nextSample 表示“下一次采样”。</summary>
        internal float lastSampleTime, stillDuration, nextSample;
    }

    /// <summary>运行诊断的结构化数据 TimelineEntry；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人战斗时间轴+记录项")]
    private struct TimelineEntry
    {
        /// <summary>sequence 表示“序号”；frame 表示“帧”；enemy 表示“敌人”；wave 表示“波次”。</summary>
        internal int sequence, frame, enemy, wave;
        /// <summary>gameTime 表示“游戏时间”；realTimeTime 表示“实际游戏当前时间”。</summary>
        internal float gameTime, realTimeTime;
        /// <summary>category 表示“类别”；text 表示“文字”。</summary>
        internal string category, text;
        /// <summary>是否快照。</summary>
        internal bool isSnapshot;
        /// <summary>快照。</summary>
        internal CombatTimelineSnapshot snapshot;
        /// <summary>one 表示“第一”；two 表示“第二”；three 表示“第三”；four 表示“第四”；five 表示“第五”；six 表示“第六”；seven 表示“第七”；eight 表示“第八”。</summary>
        internal CombatDiagnosticFields one, two, three, four, five, six, seven, eight;
    }

    /// <summary>运行诊断模块 WriteSession；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人战斗时间轴+写入会话")]
    private sealed class WriteSession
    {
        /// <summary>队列。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly TimelineEntry[] queue = new TimelineEntry[4096];
        /// <summary>backItem 表示“后方物品”；frontItem 表示“前方物品”。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly int[] backItem = new int[4096], frontItem = new int[4096];
        /// <summary>周期位置。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly Dictionary<int, int> cyclePosition = new(32);
        /// <summary>队列锁定。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly object queueLock = new();
        /// <summary>唤醒。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly AutoResetEvent wake = new(false);
        /// <summary>路径。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly string path;
        /// <summary>线程。只读字段，初始化后不由普通写入口替换。</summary>
        private readonly Thread thread;
        /// <summary>emptyPosition 表示“空位置”；queueFirst 表示“队列首个”；queueTail 表示“队列尾部”。</summary>
        private int emptyPosition, queueFirst = -1, queueTail = -1;
        /// <summary>close 表示“是否关闭”；refresh 表示“是否刷新”。</summary>
        private volatile bool close, refresh;
        /// <summary>错误。</summary>
        internal volatile string error;
        /// <summary>missedEvent 表示“未命中的事件”；missedSnapshot 表示“未命中的快照”。</summary>
        internal int missedEvent, missedSnapshot;
        /// <summary>构造并初始化 WriteSession 的声明状态；具体外部协作见静态调用链。</summary>
        /// <remarks>静态调用链：上游 EnemyCombatTimeline.BeginSession()；下游（含内部回调体） Thread.Start()、Thread.Thread(ThreadStart)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="path">路径。</param>
        internal WriteSession(string path)
        {
            this.path = path;
            // 序号。
for (int sequence = 0; sequence < backItem.Length; sequence++)
            {
                backItem[sequence] = sequence + 1;
            }

            backItem[backItem.Length - 1] = -1;
            thread = new Thread(Execute)
            {
                IsBackground = true,
                Name = "战斗时间轴写入"
            };
            thread.Start();
        }

        /// <summary>Enqueue 的业务处理入口；本方法直接依赖 Dictionary&lt;int, int&gt;.TryGetValue(int, out int)、EnemyCombatTimeline.WriteSession.Missed(bool)、EnemyCombatTimeline.WriteSession.RemoveValue(int)、EventWaitHandle.Set()、Monitor.Exit(object)、Monitor.TryEnter(object)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 EnemyCombatTimeline.LateUpdate()、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、EnemyCombatTimeline.Record(VillagerController, string, string)；下游（含内部回调体） Dictionary&lt;int, int&gt;.TryGetValue(int, out int)、EnemyCombatTimeline.WriteSession.Missed(bool)、EnemyCombatTimeline.WriteSession.RemoveValue(int)、EventWaitHandle.Set()、Monitor.Exit(object)、Monitor.TryEnter(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="item">当前时间轴入口。</param>
        internal void Enqueue(in TimelineEntry item)
        {
            if (close || error != null)
            {
                return;
            }

            if (!Monitor.TryEnter(queueLock))
            {
                Missed(item.isSnapshot);
                return;
            }

            try
            {
                // 同一敌人只保留最新周期快照，状态事件不会被周期采样挤出。
                if (item.isSnapshot && cyclePosition.TryGetValue(item.enemy, out int /* 中文含义：前一次位置。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ previousPosition))
                {
                    RemoveValue(previousPosition);
                    Missed(true);
                }

                if (emptyPosition < 0 && !item.isSnapshot && cyclePosition.Count > 0)
                {
                    // 驱逐。
int evict = -1;
                    // pair（当前键值配对）：每轮从周期位置取得一个元素，仅代表当前这一项。使用位置：EnemyCombatTimeline.WriteSession.Enqueue(in EnemyCombatTimeline.TimelineEntry)。
foreach (var pair in cyclePosition)
                    {
                        evict = pair.Value;
                        break;
                    }

                    RemoveValue(evict);
                    Missed(true);
                }

                if (emptyPosition < 0)
                {
                    Missed(item.isSnapshot);
                    return;
                }

                // 位置。
int position = emptyPosition;
                emptyPosition = backItem[position];
                queue[position] = item;
                frontItem[position] = queueTail;
                backItem[position] = -1;
                if (queueTail >= 0)
                {
                    backItem[queueTail] = position;
                }
                else
                {
                    queueFirst = position;
                }

                queueTail = position;
                if (item.isSnapshot)
                {
                    cyclePosition[item.enemy] = position;
                }
            }
            finally
            {
                Monitor.Exit(queueLock);
            }

            // 仅主线程生产和停止；后台退出不会提前释放仍可能使用的唤醒句柄。
            wake.Set();
        }

        /// <summary>RemoveValue 的业务处理入口；本方法直接依赖 Dictionary&lt;int, int&gt;.Remove(int)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 EnemyCombatTimeline.WriteSession.Enqueue(in EnemyCombatTimeline.TimelineEntry)、EnemyCombatTimeline.WriteSession.Extract(out EnemyCombatTimeline.TimelineEntry)；下游（含内部回调体） Dictionary&lt;int, int&gt;.Remove(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="position">位置。</param>
        private void RemoveValue(int position)
        {
            if (queue[position].isSnapshot)
            {
                cyclePosition.Remove(queue[position].enemy);
            }


            // 中文含义：front 为“前方”。；back 为“后方”。
int front = frontItem[position], back = backItem[position];
            if (front >= 0)
            {
                backItem[front] = back;
            }
            else
            {
                queueFirst = back;
            }

            if (back >= 0)
            {
                frontItem[back] = front;
            }
            else
            {
                queueTail = front;
            }

            queue[position] = default;
            backItem[position] = emptyPosition;
            emptyPosition = position;
        }

        /// <summary>Missed 的业务处理入口；本方法直接依赖 Interlocked.Increment(ref int)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 EnemyCombatTimeline.WriteSession.Enqueue(in EnemyCombatTimeline.TimelineEntry)；下游（含内部回调体） Interlocked.Increment(ref int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="snapshot">是否快照。</param>
        private void Missed(bool snapshot)
        {
            if (snapshot)
            {
                Interlocked.Increment(ref missedSnapshot);
            }
            else
            {
                Interlocked.Increment(ref missedEvent);
            }
        }

        /// <summary>Extract 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.WriteSession.RemoveValue(int)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 EnemyCombatTimeline.WriteSession.Execute()；下游（含内部回调体） EnemyCombatTimeline.WriteSession.RemoveValue(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="item">当前时间轴入口。输出参数，由本入口写入并交给调用方。</param>
        private bool Extract(out TimelineEntry item)
        {
            lock (queueLock)
            {
                item = default;
                if (queueFirst < 0)
                {
                    return false;
                }

                item = queue[queueFirst];
                RemoveValue(queueFirst);
                return true;
            }
        }

        /// <summary>RequestRefresh 的业务处理入口；本方法直接依赖 EventWaitHandle.Set()，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 EnemyCombatTimeline.Flush()；下游（含内部回调体） EventWaitHandle.Set()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        internal void RequestRefresh()
        {
            refresh = true;
            if (!close)
            {
                wake.Set();
            }
        }

        /// <summary>Stop 的业务处理入口；本方法直接依赖 EventWaitHandle.Set()、Thread.Join(int)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 EnemyCombatTimeline.EndSession(bool)；下游（含内部回调体） EventWaitHandle.Set()、Thread.Join(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="waiting">是否等待中。</param>
        internal void Stop(bool waiting)
        {
            if (!close)
            {
                close = true;
                wake.Set();
            }

            if (waiting && thread.IsAlive)
            {
                thread.Join(1000);
            }
        }

        /// <summary>Execute 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.AppendValue(StringBuilder)、CombatTimelineSnapshot.AppendValue(StringBuilder)、DateTime.AddSeconds(double)、Directory.CreateDirectory(string)、EnemyCombatTimeline.WriteSession.Extract(out EnemyCombatTimeline.TimelineEntry)、Path.GetDirectoryName(string)、StreamWriter.Flush() 等共 17 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） CombatDiagnosticFields.AppendValue(StringBuilder)、CombatTimelineSnapshot.AppendValue(StringBuilder)、DateTime.AddSeconds(double)、Directory.CreateDirectory(string)、EnemyCombatTimeline.WriteSession.Extract(out EnemyCombatTimeline.TimelineEntry)、Path.GetDirectoryName(string)、StreamWriter.Flush() 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        private void Execute()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // 写入。
using var write = new StreamWriter(path, false, new UTF8Encoding(true), 65536);
                // 文字。
var text = new StringBuilder(4096);
                write.WriteLine("敌人战斗时间轴；数值主线程采集，格式化及文件写入由后台完成。");
                // 下一次刷新。
DateTime nextRefresh = DateTime.UtcNow.AddSeconds(1);
                while (true)
                {
                    while (Extract(out var /* 中文含义：当前时间轴入口。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ item))
                    {
                        text.Clear();
                        text.Append("[记录").Append(item.sequence).Append("][游戏时间").Append(item.gameTime).Append("秒][实时时间").Append(item.realTimeTime).Append("秒][帧").Append(item.frame).Append("][第").Append(item.enemy).Append("号][波次").Append(item.wave).Append("][").Append(item.category).Append("] ");
                        if (item.isSnapshot)
                        {
                            item.snapshot.AppendValue(text);
                        }
                        else
                        {
                            text.Append(item.text);
                            item.one.AppendValue(text);
                            item.two.AppendValue(text);
                            item.three.AppendValue(text);
                            item.four.AppendValue(text);
                            item.five.AppendValue(text);
                            item.six.AppendValue(text);
                            item.seven.AppendValue(text);
                            item.eight.AppendValue(text);
                        }

                        write.WriteLine(text.ToString());
                    }

                    if (close)
                    {
                        break;
                    }

                    if (refresh || DateTime.UtcNow >= nextRefresh)
                    {
                        write.Flush();
                        refresh = false;
                        nextRefresh = DateTime.UtcNow.AddSeconds(1);
                    }

                    wake.WaitOne(100);
                }

                write.WriteLine("遗漏周期快照=" + missedSnapshot + "，遗漏状态事件=" + missedEvent);
                write.Flush();
            }
            catch (Exception /* 中文含义：捕获的异常信息。保存当前 catch 捕获的异常，供记录和错误处理使用。 */ exception)
            {
                error = exception.Message;
            }
        // 唤醒对象由会话持有，避免停用与最后一次唤醒并发时访问已释放句柄。
        }
    }

    /// <summary>生效。</summary>
    private static EnemyCombatTimeline active;
    /// <summary>条目集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<int, Entry> entries = new(32);
    /// <summary>顺序。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<Entry> order = new(32);
    /// <summary>会话。</summary>
    private WriteSession session;
    /// <summary>是否具有启动。</summary>
    private bool hasStartup;
    /// <summary>cursor 表示“光标”；sequence 表示“序号”；nextNumber 表示“下一次数量”；nextSceneNumber 表示“下一次场景数量”。</summary>
    private int cursor, sequence, nextNumber, nextSceneNumber;
    /// <summary>日志路径。</summary>
    public string LogPath { get; private set; }
    /// <summary>是否记录中。</summary>
    public static bool IsRecording => active != null && active.enableRecording && active.session != null && active.session.error == null;

    /// <summary>IsRecord 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.BuildCombatTimelineSummary(VillagerController)、EnemyAIManager.UpdatePlayerPursuitRegion()、EnemyCombatTimeline.LateUpdate()、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields) 等共 18 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    public static bool IsRecord(VillagerController enemy) => IsRecording && enemy != null && (!active.firstWaveOnly || enemy.diagnosticWave <= 1) && (active.selectedEnemy == null || active.selectedEnemy == enemy);
    /// <summary>ResetSession 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.EndSession(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatTimeline.EndSession(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        if (active != null)
        {
            active.EndSession(false);
        }

        active = null;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;EnemyAIManager&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (manager == null)
        {
            manager = GetComponent<EnemyAIManager>();
        }

        if (player == null && manager != null)
        {
            player = manager.PlayerTarget;
        }

        hasStartup = true;
        active = this;
    }

    /// <summary>Unity 组件启用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        if (hasStartup)
        {
            active = this;
        }
    }

    /// <summary>BeginSession 的业务处理入口；本方法直接依赖 DateTime.ToString(string)、EnemyCombatTimeline.Register(VillagerController, int)、EnemyCombatTimeline.WriteSession.WriteSession(string)、Guid.NewGuid()、Guid.ToString(string)、Object.FindObjectsByType&lt;VillagerController&gt;(FindObjectsSortMode)、Path.Combine(string, string, string) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatTimeline.LateUpdate()；下游（含内部回调体） DateTime.ToString(string)、EnemyCombatTimeline.Register(VillagerController, int)、EnemyCombatTimeline.WriteSession.WriteSession(string)、Guid.NewGuid()、Guid.ToString(string)、Object.FindObjectsByType&lt;VillagerController&gt;(FindObjectsSortMode)、Path.Combine(string, string, string) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void BeginSession()
    {
        // 根节点。
string root;
        if (Application.isEditor)
        {
            root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "战斗日志"));
        }
        else
        {
            root = Path.Combine(Application.persistentDataPath, "战斗日志");
        }

        LogPath = Path.Combine(root, "敌人时间轴_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N") + ".txt");
        session = new WriteSession(LogPath);
        // 仅在显式开启记录时补登记场景现存角色，包括不在战斗管理器中的展示角色。
        // enemy（敌人）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 EnemyCombatTimeline.Register(VillagerController, int) 的参数。
foreach (var enemy in FindObjectsByType<VillagerController>(FindObjectsSortMode.None))
        {
            Register(enemy, enemy.diagnosticWave);
        }
    }

    /// <summary>EndSession 的业务处理入口；本方法直接依赖 Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.Clear()、EnemyCombatTimeline.WriteSession.Stop(bool)、List&lt;EnemyCombatTimeline.Entry&gt;.Clear()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatTimeline.LateUpdate()、EnemyCombatTimeline.OnApplicationQuit()、EnemyCombatTimeline.OnDisable()、EnemyCombatTimeline.ResetSession()；下游（含内部回调体） Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.Clear()、EnemyCombatTimeline.WriteSession.Stop(bool)、List&lt;EnemyCombatTimeline.Entry&gt;.Clear()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="waiting">是否等待中。</param>
    void EndSession(bool waiting)
    {
        // 判空前缓存的会话。
global::EnemyCombatTimeline.WriteSession receiverValue2 = session;
        if (!global::System.Object.ReferenceEquals(receiverValue2, null))
        {
            receiverValue2.Stop(waiting);
        }

        session = null;
        entries.Clear();
        order.Clear();
        cursor = sequence = nextNumber = nextSceneNumber = 0;
    }

    /// <summary>Register 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.Count(string, double)、Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.Add(int, EnemyCombatTimeline.Entry)、Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.TryGetValue(int, out EnemyCombatTimeline.Entry)、EnemyCombatTimeline.Entry.Entry()、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、List&lt;EnemyCombatTimeline.Entry&gt;.Add(EnemyCombatTimeline.Entry)、Mathf.Max(float, float) 等共 8 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ChapterOneMissionManager.TrySpawnEnemy(GameObject, MissionEnemyType)、EnemyCombatTimeline.BeginSession()、EnemyCombatTimeline.CreateRecord(VillagerController, string)、VillagerController.OnEnable()、VillagerController.Start()；下游（含内部回调体） CombatDiagnosticFields.Count(string, double)、Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.Add(int, EnemyCombatTimeline.Entry)、Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.TryGetValue(int, out EnemyCombatTimeline.Entry)、EnemyCombatTimeline.Entry.Entry()、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、List&lt;EnemyCombatTimeline.Entry&gt;.Add(EnemyCombatTimeline.Entry)、Mathf.Max(float, float) 等共 8 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="wave">波次。</param>
    public static int Register(VillagerController enemy, int wave = 0)
    {
        if (enemy == null)
        {
            return 0;
        }

        if (wave > 0)
        {
            enemy.diagnosticWave = wave;
        }

        if (!IsRecording)
        {
            return 0;
        }

        // 键。
int key = enemy.GetInstanceID();
        if (active.entries.TryGetValue(key, out var /* 中文含义：前一次物品。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ previousItem))
        {
            previousItem.wave = enemy.diagnosticWave;
            return previousItem.number;
        }

        // 本次新建的当前入口。
global::EnemyCombatTimeline.Entry createdValue3 = new Entry();
        createdValue3.enemy = enemy;
        createdValue3.key = key;
        createdValue3.wave = enemy.diagnosticWave;
        if (wave == 0)
        {
            createdValue3.number = 1000 + ++active.nextSceneNumber;
        }
        else
        {
            createdValue3.number = ++active.nextNumber;
        }

        createdValue3.lastPosition = enemy.transform.position;
        createdValue3.lastSampleTime = Time.time;
        createdValue3.nextSample = Time.unscaledTime + (active.order.Count % 16) * Mathf.Max(.1f, active.snapshotInterval) / 16f;
        // 当前入口。
var item = createdValue3;
        active.entries.Add(key, item);
        active.order.Add(item);
        enemy.diagnosticEnemyNumber = item.number;
        Record(enemy, "登记", CombatDiagnosticFields.Count("波次", item.wave), CombatDiagnosticFields.Count("对象标识", key));
        return item.number;
    }

    /// <summary>Unregister 的业务处理入口；本方法直接依赖 Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.Remove(int, out EnemyCombatTimeline.Entry)、List&lt;EnemyCombatTimeline.Entry&gt;.Remove(EnemyCombatTimeline.Entry)、Object.GetInstanceID()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 VillagerController.OnDisable()；下游（含内部回调体） Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.Remove(int, out EnemyCombatTimeline.Entry)、List&lt;EnemyCombatTimeline.Entry&gt;.Remove(EnemyCombatTimeline.Entry)、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    public static void Unregister(VillagerController enemy)
    {
        if (active == null || enemy == null)
        {
            return;
        }

        if (active.entries.Remove(enemy.GetInstanceID(), out var /* 中文含义：当前入口。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ item))
        {
            active.order.Remove(item);
        }
    }

    /// <summary>Number 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyAIManager.BuildCombatTimelineSummary(VillagerController)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    public static string Number(VillagerController enemy)
    {
        if (enemy == null)
        {
            return "无";
        }
        else
        {
            return "第" + enemy.diagnosticEnemyNumber + "号";
        }
    }

    /// <summary>Record 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.CreateRecord(VillagerController, string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.WriteSession.Enqueue(in EnemyCombatTimeline.TimelineEntry)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyTaskBase.DiagTaskEnd()、EnemyTaskBase.DiagTaskStart()、VillagerController.UpdateCrowdBypass()；下游（含内部回调体） EnemyCombatTimeline.CreateRecord(VillagerController, string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.WriteSession.Enqueue(in EnemyCombatTimeline.TimelineEntry)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="category">类别。</param>
    /// <param name="detail">细节。</param>
    public static void Record(VillagerController enemy, string category, string detail)
    {
        if (!IsRecord(enemy))
        {
            return;
        }

        // 当前时间轴入口。
var item = active.CreateRecord(enemy, category);
        item.text = detail;
        active.session.Enqueue(item);
    }

    /// <summary>Record 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.CreateRecord(VillagerController, string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.WriteSession.Enqueue(in EnemyCombatTimeline.TimelineEntry)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.UpdatePlayerPursuitRegion()、EnemyCombatTimeline.Register(VillagerController, int)、VillagerController.OnDisable()、VillagerController.RecordTimelineAnimationChanges() 等共 11 处；下游（含内部回调体） EnemyCombatTimeline.CreateRecord(VillagerController, string)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.WriteSession.Enqueue(in EnemyCombatTimeline.TimelineEntry)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="category">类别。</param>
    /// <param name="one">第一。</param>
    /// <param name="two">第二。</param>
    /// <param name="three">第三。</param>
    /// <param name="four">第四。</param>
    /// <param name="five">第五。</param>
    /// <param name="six">第六。</param>
    /// <param name="seven">第七。</param>
    /// <param name="eight">第八。</param>
    public static void Record(VillagerController enemy, string category, CombatDiagnosticFields one = default, CombatDiagnosticFields two = default, CombatDiagnosticFields three = default, CombatDiagnosticFields four = default, CombatDiagnosticFields five = default, CombatDiagnosticFields six = default, CombatDiagnosticFields seven = default, CombatDiagnosticFields eight = default)
    {
        if (!IsRecord(enemy))
        {
            return;
        }

        // 当前时间轴入口。
var item = active.CreateRecord(enemy, category);
        item.one = one;
        item.two = two;
        item.three = three;
        item.four = four;
        item.five = five;
        item.six = six;
        item.seven = seven;
        item.eight = eight;
        active.session.Enqueue(item);
    }

    /// <summary>CreateRecord 的业务处理入口；本方法直接依赖 Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.ContainsKey(int)、EnemyCombatTimeline.Register(VillagerController, int)、EnemyCombatTimeline.TimelineEntry.TimelineEntry()、Object.GetInstanceID()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatTimeline.LateUpdate()、EnemyCombatTimeline.Record(VillagerController, string, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields, CombatDiagnosticFields)、EnemyCombatTimeline.Record(VillagerController, string, string)；下游（含内部回调体） Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.ContainsKey(int)、EnemyCombatTimeline.Register(VillagerController, int)、EnemyCombatTimeline.TimelineEntry.TimelineEntry()、Object.GetInstanceID()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="enemy">敌人。</param>
    /// <param name="category">类别。</param>
    private TimelineEntry CreateRecord(VillagerController enemy, string category)
    {
        if (!entries.ContainsKey(enemy.GetInstanceID()))
        {
            Register(enemy, enemy.diagnosticWave);
        }

        return new TimelineEntry
        {
            sequence = ++sequence,
            frame = Time.frameCount,
            enemy = enemy.diagnosticEnemyNumber,
            wave = enemy.diagnosticWave,
            gameTime = Time.timeSinceLevelLoad,
            realTimeTime = Time.realtimeSinceStartup,
            category = category
        };
    }

    /// <summary>Unity 帧末更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogWarning(object, Object)、Dictionary&lt;int, EnemyCombatTimeline.Entry&gt;.Remove(int)、EnemyCombatTimeline.BeginSession()、EnemyCombatTimeline.CreateRecord(VillagerController, string)、EnemyCombatTimeline.EndSession(bool)、EnemyCombatTimeline.IsRecord(VillagerController)、EnemyCombatTimeline.WriteSession.Enqueue(in EnemyCombatTimeline.TimelineEntry) 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void LateUpdate()
    {
        if (!enableRecording)
        {
            if (session != null)
            {
                EndSession(false);
            }

            return;
        }

        if (session == null)
        {
            BeginSession();
        }

        if (session.error != null)
        {
            Debug.LogWarning("战斗时间轴已停止，文件写入失败：" + session.error, this);
            enableRecording = false;
            EndSession(false);
            return;
        }

        // item（当前入口）：每轮从顺序取得一个元素，仅代表当前这一项。使用位置：作为 EnemyCombatTimeline.IsRecord(VillagerController) 的参数、EnemyCombatTimeline.LateUpdate()。
foreach (var item in order)
        {
            if (item.enemy != null && item.enemy.isActiveAndEnabled && IsRecord(item.enemy))
            {
                item.enemy.RecordTimelineAnimationChanges();
            }
        }


        // 中文含义：budget 为“预算”。；checkCount 为“检查数量”。
int budget = 2, checkCount = order.Count;
        while (budget > 0 && checkCount-- > 0 && order.Count > 0)
        {
            if (cursor >= order.Count)
            {
                cursor = 0;
            }

            // 当前入口。
var item = order[cursor++];
            if (item.enemy == null || !item.enemy.isActiveAndEnabled)
            {
                order.Remove(item);
                entries.Remove(item.key);
                continue;
            }

            if (!IsRecord(item.enemy))
            {
                continue;
            }

            if (budget <= 0 || Time.unscaledTime < item.nextSample)
            {
                continue;
            }

            using (CombatPerformanceCounters.snapshotMarker.Auto())
            {
                // 时间值。
float timeValue = Mathf.Max(0, Time.time - item.lastSampleTime);
                // 位移。
float displacement = Vector3.Distance(item.enemy.transform.position, item.lastPosition);
                // 赋值前保存的目标当前入口。
global::EnemyCombatTimeline.Entry assignmentReceiver6 = item;
                if (displacement < .025f)
                {
                    assignmentReceiver6.stillDuration = item.stillDuration + timeValue;
                }
                else
                {
                    assignmentReceiver6.stillDuration = 0;
                }

                item.lastSampleTime = Time.time;
                item.lastPosition = item.enemy.transform.position;
                item.nextSample = Time.unscaledTime + Mathf.Max(.1f, snapshotInterval);
                // 当前时间轴入口。
var record = CreateRecord(item.enemy, "快照");
                record.isSnapshot = true;
                record.snapshot = item.enemy.CollectCombatTimelineSnapshot();
                record.snapshot.sampleDisplacement = displacement;
                record.snapshot.stationaryTime = item.stillDuration;
                session.Enqueue(record);
                CombatPerformanceCounters.snapshotCount++;
                budget--;
            }
        }
    }

    /// <summary>Flush 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.WriteSession.RequestRefresh()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatTimeline.OnApplicationPause(bool)；下游（含内部回调体） EnemyCombatTimeline.WriteSession.RequestRefresh()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [ContextMenu("立即保存时间轴")]
    public void Flush()
    {
        // 判空前缓存的会话。
global::EnemyCombatTimeline.WriteSession receiverValue8 = session;
        if (!global::System.Object.ReferenceEquals(receiverValue8, null))
        {
            receiverValue8.RequestRefresh();
        }
    }

    /// <summary>OnApplicationPause 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.Flush()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatTimeline.Flush()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="paused">是否已暂停。</param>
    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            Flush();
        }
    }

    /// <summary>OnApplicationQuit 的业务处理入口；本方法直接依赖 EnemyCombatTimeline.EndSession(bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatTimeline.EndSession(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnApplicationQuit()
    {
        EndSession(true);
    }

    /// <summary>Unity 组件停用回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） EnemyCombatTimeline.EndSession(bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        EndSession(false);
        if (active == this)
        {
            active = null;
        }
    }

    /// <summary>Describe 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyAIManager.BuildCombatTimelineSummary(VillagerController)、VillagerController.RecordTimelineAnimationChanges()、VillagerController.UpdateChaseProgressRecovery()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">是否值。</param>
    public static string Describe(bool value)
    {
        if (value)
        {
            return "是";
        }
        else
        {
            return "否";
        }
    }

    /// <summary>Describe 的业务处理入口；本方法直接依赖 Dictionary&lt;T, string&gt;.TryGetValue(T, out string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、VillagerController.CollectCombatTimelineSnapshot()、VillagerController.OnDisable()、VillagerController.RecordTimelineAnimationChanges() 等共 8 处；下游（含内部回调体） Dictionary&lt;T, string&gt;.TryGetValue(T, out string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">当前进度。</param>
    public static string Describe</* 中文含义：由调用方指定的通用类型参数。 */ T>(T value)
        where T : struct, Enum
    {
        if (EnumLabels<T>.cache.TryGetValue(value, out var /* 中文含义：文字。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ text))
        {
            return text;
        }
        else
        {
            return "其他状态";
        }
    }

    /// <summary>运行诊断模块 EnumLabels；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "敌人战斗时间轴+枚举文字")]
    private static class EnumLabels</* 中文含义：泛型枚举类型参数。 */ T>
        where T : struct, Enum
    {
        /// <summary>缓存。只读字段，初始化后不由普通写入口替换。</summary>
        internal static readonly Dictionary<T, string> cache = Create();
        /// <summary>Create 的业务处理入口；本方法直接依赖 Dictionary&lt;T, string&gt;.Dictionary()、EnemyCombatTimeline.Describe(string)、Enum.GetValues(Type)、Enum.ToString()，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 EnemyCombatTimeline.EnumLabels&lt;T&gt;.cache；下游（含内部回调体） Dictionary&lt;T, string&gt;.Dictionary()、EnemyCombatTimeline.Describe(string)、Enum.GetValues(Type)、Enum.ToString()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        private static Dictionary<T, string> Create()
        {
            // 字典。
var dictionary = new Dictionary<T, string>();
            // value（当前进度）：每轮从取得数值集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 Dictionary<T, string>.this[T] 的参数、作为 EnemyCombatTimeline.Describe(string) 的参数。
foreach (T value in Enum.GetValues(typeof(T)))
            {
                dictionary[value] = Describe(value.ToString());
            }

            return dictionary;
        }
    }

    /// <summary>Describe 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：上游 EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyCombatTimeline.EnumLabels&lt;T&gt;.Create()、EnemyTaskBase.DiagTaskEnd()、EnemyTaskBase.DiagTaskStart()、VillagerController.SetMovementMode(EnemyMovementMode, string, int)；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    public static string Describe(string value)
    {
        if (value == null)
        {
            return "无";
        }

        switch (value)
        {
            case "True":
                return "是";
            case "False":
                return "否";
            case "Idle":
                return "待机";
            case "Patrol":
                return "巡逻";
            case "Alert":
                return "警戒待命";
            case "Chase":
                return "追击";
            case "Attack":
                return "站立攻击";
            case "CrawlAttack":
                return "爬行攻击";
            case "Staggered":
                return "受击硬直";
            case "Crawl":
                return "爬行";
            case "StandUp":
                return "起身";
            case "Dead":
                return "死亡";
            case "QTE":
                return "配对抓取";
            case "Search":
                return "搜寻";
            case "FullyStopped":
                return "完全停止";
            case "StandingPathRootMotion":
                return "站立寻路移动";
            case "CrawlCodeDriven":
                return "爬行代码移动";
            case "FullRootMotion":
                return "全根运动";
            case "ActionRootMotion":
                return "动作根运动";
            case "Calculating":
                return "计算中";
            case "Valid":
                return "有效";
            case "Arrived":
                return "到达";
            case "Failed":
                return "失败";
            case "Cancelled":
                return "已取消";
            case "None":
                return "无";
            case "Lower":
                return "下段";
            case "Junction":
                return "交接段";
            case "Upper":
                return "上段";
            case "Approach":
                return "入口接近";
            case "WaitingForPassage":
                return "等候通路";
            case "Crossing":
                return "正在穿门";
            case "ClearingEntrance":
                return "离开门口";
            case "BeginChase":
                return "开始追击";
            case "BeginCombatHold":
                return "进入近战等待";
            case "BeginAlert":
                return "进入警戒";
            case "BeginAttack":
                return "开始站立攻击";
            case "BeginCrawlAttack":
                return "开始爬行攻击";
            case "EndAttack":
                return "结束攻击";
            case "BeginHit":
                return "进入受击";
            case "ConsumeHit":
                return "受击收尾";
            case "BeginCrawl":
                return "进入爬行";
            case "BeginStandUp":
                return "开始起身";
            case "BeginDeath":
                return "进入死亡";
            case "Start":
                return "初始化";
            case "OnDisable":
                return "组件停用";
            case "OnEnd":
                return "行为动作退出";
            case "OnUpdate":
                return "行为动作更新";
            case "ExecuteCombat":
                return "战斗追击与占位";
            case "ExecuteChase":
                return "追击动作";
            case "ExecuteAttack":
                return "站立攻击动作";
            case "ExecuteCrawlAttack":
                return "爬行攻击动作";
            case "ExecuteHit":
                return "受击动作";
            case "ExecuteCrawl":
                return "爬行动作";
            case "ExecuteStandUp":
                return "起身动作";
            case "ExecuteDeath":
                return "死亡动作";
            case "ExecutePatrol":
                return "巡逻动作";
            case "ExecuteAlert":
                return "警戒动作";
            case "ExecuteIdle":
                return "待机动作";
            case "TraversalCodeDriven":
                return "特殊通路代码移动";
            case "CompleteTraversalAndResumeChase":
                return "特殊通路结束后恢复追击";
            case "RequestStandUp":
                return "请求起身";
            case "CompleteStandUp":
                return "起身完成";
            case "CancelStandUpAndResumeCrawl":
                return "起身取消后恢复爬行";
            case "ForceCrawlPoseAfterQTE":
                return "配对动作结束后恢复爬行";
            case "EndCrawlAttack":
                return "结束爬行攻击";
            case "RestoreCrawlAfterAttack":
                return "攻击收尾后恢复爬行";
            case "BeginPatrol":
                return "开始巡逻";
            case "BeginStationaryPatrol":
                return "原地巡逻";
            case "BeginIdle":
                return "进入待机";
            case "ApplyDeadChainsawPulse":
                return "死亡电锯余势";
            case "UpdateInvestigation":
                return "更新调查";
            case "NotifyHitStateMachineExited":
                return "受击动画退出";
            case "ApplyKnifeParryStagger":
                return "刀挡引发硬直";
            case "TryApplyFriendlyFire":
                return "同伴误伤";
            case "TryStartFrontGrabQTE":
                return "尝试正面抓取";
            case "TryStartEnemyQTEBase":
                return "尝试配对动作";
            case "TryStartRearBiteNeckQTE":
                return "尝试背后擒抱";
            case "TryStartLegBiteQTE":
                return "尝试咬腿";
            case "TryStartChainsawExecutionQTE":
                return "尝试电锯处决";
            case "ExecuteChaseRecovery":
                return "追击路径恢复";
            case "ChasePlayer":
                return "追击玩家";
            case "InvestigatePlayer":
                return "调查玩家位置";
            case "HoldCombatPosition":
                return "近战待命";
            case "SelectPatrolPoint":
                return "选择巡逻点";
            case "MoveToPatrolPoint":
                return "走向巡逻点";
            case "PatrolInPlace":
                return "原地巡逻动作";
            case "IdleWait":
                return "待机等待";
            case "StopAndClearMovement":
                return "停止并清路径动作";
            case "ExecuteChainsawStart":
                return "电锯启动";
            case "ExecuteChainsawIntroTaunt":
                return "电锯出场威慑";
            default:
                return "其他动作入口";
        }
    }
}

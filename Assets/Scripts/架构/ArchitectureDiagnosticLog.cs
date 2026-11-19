using System;
using System.Collections.Generic;

namespace GameArchitecture
{
    /// <summary>动作和资源拥有者记录的诊断事实分类，不向业务模块发送新的动作请求。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "诊断事件")]
    public enum DiagnosticEvent
    {
        /// <summary>中文含义：请求接受。请求通过审批。</summary>
        [UnityEngine.InspectorName("请求接受")]
        RequestAccept,
        /// <summary>中文含义：请求拒绝。请求被拒绝，原因保存在诊断项中。</summary>
        [UnityEngine.InspectorName("请求拒绝")]
        RequestReject,
        /// <summary>中文含义：状态变化。动作拥有者已经提交状态变化。</summary>
        [UnityEngine.InspectorName("状态变化")]
        StateChange,
        /// <summary>中文含义：资源取得。动作取得关联资源或控制权。</summary>
        [UnityEngine.InspectorName("资源取得")]
        AssetAcquire,
        /// <summary>中文含义：资源释放。动作释放关联资源或控制权。</summary>
        [UnityEngine.InspectorName("资源释放")]
        AssetRelease
    }

    /// <summary>拥有者构造并交给日志的不可变事实条目；请求、动作及前后状态供后续快照诊断关联。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "架构诊断项")]
    public readonly struct ArchitectureDiagnosticEntry
    {
        /// <summary>中文含义：请求。引起该条事实的业务请求上下文。</summary>
        public RequestContext Request { get; }
        /// <summary>中文含义：动作。事实关联的动作句柄，保留对象租用和动作编号。</summary>
        public ActionHandle Action { get; }
        /// <summary>中文含义：事件。本条事实的分类。</summary>
        public DiagnosticEvent Event { get; }
        /// <summary>中文含义：原因。接受、拒绝或变化的关联原因。</summary>
        public RejectionReason Reason { get; }
        /// <summary>中文含义：原始状态。提交变化之前的动作种类。</summary>
        public ActionKind OriginalState { get; }
        /// <summary>中文含义：新状态。提交变化之后的动作种类。</summary>
        public ActionKind NewState { get; }
        /// <summary>中文含义：时间。调用方提供的事实时间，日志不自行读取引擎时钟。</summary>
        public double Time { get; }

        /// <summary>动作拥有者在提交事实时复制数据，随后通过 Record 写入其持有的日志。</summary>
        /// <param name="request">中文含义：请求。关联业务请求。</param>
        /// <param name="action">中文含义：动作。关联动作句柄。</param>
        /// <param name="eventValue">中文含义：事件值。事实分类。</param>
        /// <param name="reason">中文含义：原因。关联原因。</param>
        /// <param name="originalState">中文含义：原始状态。变化前状态。</param>
        /// <param name="newState">中文含义：新状态。变化后状态。</param>
        /// <param name="time">中文含义：时间。调用方提供的事实时间。</param>
        public ArchitectureDiagnosticEntry(RequestContext request, ActionHandle action, DiagnosticEvent eventValue, RejectionReason reason, ActionKind originalState, ActionKind newState, double time)
        {
            Request = request;
            Action = action;
            Event = eventValue;
            Reason = reason;
            OriginalState = originalState;
            NewState = newState;
            Time = time;
        }
    }

    /// <summary>
    /// 固定容量的诊断环形缓冲，由 EnemyActionCoordinator 等拥有者显式持有。
    /// Record 同步写入既有数组，ReadSnapshot 按时间顺序复制为只读集合；日志不广播业务请求。
    /// </summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "游戏架构", "Game.Contracts", "架构诊断记录")]
    public sealed class ArchitectureDiagnosticLog
    {
        /// <summary>中文含义：缓冲区。构造时分配的固定容量缓冲，写满后覆盖最旧条目。</summary>
        private readonly ArchitectureDiagnosticEntry[] buffer;
        /// <summary>中文含义：下部元素位置。下一条记录的写入位置，写入后按容量循环。</summary>
        private int lowerElementPosition;
        /// <summary>中文含义：数量。当前保留的有效条目数，最大为缓冲长度。</summary>
        private int count;
        /// <summary>中文含义：是否启用。调用方控制的记录开关；关闭时 Record 无操作，已有记录仍可读取。</summary>
        public bool Enable { get; set; }

        /// <summary>拥有者创建日志时分配缓冲；容量非正数时抛出参数异常。</summary>
        /// <param name="capacity">中文含义：容量。要保留的最大条目数，默认 256。</param>
        public ArchitectureDiagnosticLog(int capacity = 256)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            buffer = new ArchitectureDiagnosticEntry[capacity];
        }

        /// <summary>拥有者提交事实后调用；开启记录时复制条目并推进写入位置，不创建新集合。</summary>
        /// <param name="item">中文含义：当前架构诊断入口。调用方已经构造的事实条目，只读引用传入后复制到缓冲。</param>
        public void Record(in ArchitectureDiagnosticEntry item)
        {
            if (!Enable)
            {
                return;
            }

            buffer[lowerElementPosition] = item;
            lowerElementPosition = (lowerElementPosition + 1) % buffer.Length;
            count = Math.Min(count + 1, buffer.Length);
        }

        /// <summary>诊断查询链调用；分配独立数组并按最旧到最新排序，后续记录不会改变已返回的快照。</summary>
        /// <returns>当前有效条目的只读副本。</returns>
        public IReadOnlyList<ArchitectureDiagnosticEntry> ReadSnapshot()
        {
            // 为此次查询分配的独立结果数组，不暴露内部环形缓冲。
            // 中文含义：result 为“当前架构诊断入口数组”。
var result = new ArchitectureDiagnosticEntry[count];
            // 最旧有效条目在环形缓冲中的位置。
            // 中文含义：startPoint 为“起始点”。
int startPoint = (lowerElementPosition - count + buffer.Length) % buffer.Length;
            // index 是结果中的时间顺序索引，源位置按缓冲长度循环。
            // 中文含义：index 为“当前元素索引”。
for (int index = 0; index < count; index++)
            {
                result[index] = buffer[(startPoint + index) % buffer.Length];
            }

            return Array.AsReadOnly(result);
        }

        /// <summary>拥有者换局或重新租用时调用；清空缓冲及读写计数，保留记录开关与容量。</summary>
        public void ClearValue()
        {
            Array.Clear(buffer, 0, buffer.Length);
            lowerElementPosition = 0;
            count = 0;
        }
    }
}

using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>运行诊断的结构化数据 CombatDiagnosticFields；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "战斗诊断字段")]
// 队列只传递数值和不可变文字，不向写入线程传递任何引擎对象。
public struct CombatDiagnosticFields
{
    /// <summary>中文含义：名称。使用位置：作为 StringBuilder.Append(string) 的参数、CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Position(string, Vector3) 等共 4 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("名称")]
    [UnityEngine.InspectorName("名称")]
    public string name;
    /// <summary>中文含义：文字。使用位置：CombatDiagnosticFields.Text(string, string)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("文字")]
    [UnityEngine.InspectorName("文字")]
    public string text;
    /// <summary>中文含义：诊断字段中的数字值。使用位置：作为 StringBuilder.Append(string) 的参数、CombatDiagnosticFields.Count(string, double)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("数字")]
    [UnityEngine.InspectorName("数字")]
    public double countCharacter;
    /// <summary>中文含义：坐标。使用位置：作为 CombatDiagnosticFields.AppendCoordinate(StringBuilder, Vector3) 的参数、CombatDiagnosticFields.Position(string, Vector3)。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("坐标")]
    [UnityEngine.InspectorName("坐标")]
    public Vector3 coordinate;
    /// <summary>中文含义：类型。使用位置：CombatDiagnosticFields.AppendValue(StringBuilder)、CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Position(string, Vector3) 等共 4 处。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("类型")]
    [UnityEngine.InspectorName("类型")]
    public byte type;
    /// <summary>Text 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.CombatDiagnosticFields()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.UpdatePlayerPursuitRegion()、VillagerController.OnDisable()、VillagerController.RecordTimelineAnimationChanges() 等共 10 处；下游（含内部回调体） CombatDiagnosticFields.CombatDiagnosticFields()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：CombatDiagnosticFields.Text(string, string)。</param>
    /// <param name="value">中文含义：值。使用位置：CombatDiagnosticFields.Text(string, string)。</param>
    public static CombatDiagnosticFields Text(string name, string value) => new()
    {
        name = name,
        text = value,
        type = 1
    };
    /// <summary>Count 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.CombatDiagnosticFields()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyActionCoordinator.SetObservableState(VillagerState, string, int)、EnemyAIManager.UpdatePlayerPursuitRegion()、EnemyCombatTimeline.Register(VillagerController, int)、VillagerController.RecordTimelineAnimationChanges() 等共 8 处；下游（含内部回调体） CombatDiagnosticFields.CombatDiagnosticFields()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：CombatDiagnosticFields.Count(string, double)。</param>
    /// <param name="value">中文含义：值。使用位置：CombatDiagnosticFields.Count(string, double)。</param>
    public static CombatDiagnosticFields Count(string name, double value) => new()
    {
        name = name,
        countCharacter = value,
        type = 2
    };
    /// <summary>Position 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.CombatDiagnosticFields()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyAIManager.UpdatePlayerPursuitRegion()、VillagerController.TryStartCrowdBypass(VillagerController)；下游（含内部回调体） CombatDiagnosticFields.CombatDiagnosticFields()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">中文含义：名称。使用位置：CombatDiagnosticFields.Position(string, Vector3)。</param>
    /// <param name="value">中文含义：值。使用位置：CombatDiagnosticFields.Position(string, Vector3)。</param>
    public static CombatDiagnosticFields Position(string name, Vector3 value) => new()
    {
        name = name,
        coordinate = value,
        type = 3
    };
    /// <summary>AppendValue 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.AppendCoordinate(StringBuilder, Vector3)、double.ToString(string, IFormatProvider)、StringBuilder.Append(char)、StringBuilder.Append(string)、StringBuilder.Append(StringBuilder)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyCombatTimeline.WriteSession.Execute()；下游（含内部回调体） CombatDiagnosticFields.AppendCoordinate(StringBuilder, Vector3)、double.ToString(string, IFormatProvider)、StringBuilder.Append(char)、StringBuilder.Append(string)、StringBuilder.Append(StringBuilder)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">中文含义：文字。使用位置：作为 CombatDiagnosticFields.AppendCoordinate(StringBuilder, Vector3) 的参数、作为 StringBuilder.Append(StringBuilder) 的参数、CombatDiagnosticFields.AppendValue(StringBuilder)。</param>
    internal void AppendValue(StringBuilder text)
    {
        if (type == 0)
        {
            return;
        }

        text.Append(name).Append('=');
        if (type == 1)
        {
            text.Append(this.text);
        }
        else if (type == 2)
        {
            text.Append(countCharacter.ToString("0.####", CultureInfo.InvariantCulture));
        }
        else
        {
            AppendCoordinate(text, coordinate);
        }

        text.Append('，');
    }

    /// <summary>AppendCoordinate 的业务处理入口；本方法直接依赖 float.ToString(string, IFormatProvider)、StringBuilder.Append(char)、StringBuilder.Append(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 CombatDiagnosticFields.AppendValue(StringBuilder)；下游（含内部回调体） float.ToString(string, IFormatProvider)、StringBuilder.Append(char)、StringBuilder.Append(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">中文含义：文字。使用位置：CombatDiagnosticFields.AppendCoordinate(StringBuilder, Vector3)。</param>
    /// <param name="value">中文含义：值。使用位置：作为 StringBuilder.Append(string) 的参数。</param>
    internal static void AppendCoordinate(StringBuilder text, Vector3 value)
    {
        text.Append('(').Append(value.x.ToString("F3", CultureInfo.InvariantCulture)).Append(',').Append(value.y.ToString("F3", CultureInfo.InvariantCulture)).Append(',').Append(value.z.ToString("F3", CultureInfo.InvariantCulture)).Append(')');
    }
}

/// <summary>运行诊断的结构化数据 CombatTimelineSnapshot；声明字段保存该步骤需要的值，具体读写由使用方执行。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "战斗时间轴快照")]
internal struct CombatTimelineSnapshot
{
    /// <summary>中文含义：当前敌人状态。使用位置：作为 EnemyCombatTimeline.Describe&lt;VillagerState&gt;(VillagerState) 的参数、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    internal VillagerState state;
    /// <summary>中文含义：移动。使用位置：作为 EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode) 的参数、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    internal EnemyMovementMode movement;
    /// <summary>中文含义：路径。使用位置：作为 EnemyCombatTimeline.Describe&lt;PathAgentState&gt;(PathAgentState) 的参数、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    internal PathAgentState path;
    /// <summary>中文含义：reason 表示“原因”；doorway 表示“门洞”；stair 表示“楼梯”；permission 表示“许可”。使用位置：作为 StringBuilder.Append(string) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Text(string, string) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Text(string, string) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Text(string, string) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。</summary>
    internal string reason, doorway, stair, permission;
    /// <summary>中文含义：position 表示“位置”；target 表示“目标”；playerPosition 表示“玩家位置”；waypoint 表示“路径点”；pathDirection 表示“路径方向”；chaseDirection 表示“追逐方向”；surroundTarget 表示“包围目标”；intentDisplacement 表示“意图位移”；avoidanceDisplacement 表示“避障位移”；speedLimitDisplacement 表示“速度限制位移”。使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、作为 Mathf.Abs(float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数 等共 4 处。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、作为 Mathf.Abs(float) 的参数、作为 VillagerController.HorizontalDistance(Vector3, Vector3) 的参数 等共 4 处。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、作为 PathAgent.TryGetCurrentWaypoint(out Vector3) 的参数。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Position(string, Vector3) 的参数、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    internal Vector3 position, target, playerPosition, waypoint, pathDirection, chaseDirection, surroundTarget, intentDisplacement, avoidanceDisplacement, speedLimitDisplacement;
    /// <summary>中文含义：joinCombat 表示“是否加入战斗”；hitReaction 表示“是否受击反应”；attackComplete 表示“是否攻击完成”；stop 表示“是否停止”；hasPlayer 表示“是否具有玩家”；hasPath 表示“是否具有路径”；hasWaypoint 表示“是否具有路径点”；hasAnimation 表示“是否具有动画”；animationTransition 表示“是否动画过渡”；directChase 表示“是否直接追逐”；hasSurroundSlot 表示“是否具有包围槽位”。使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：CombatTimelineSnapshot.AppendValue(StringBuilder)、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：CombatTimelineSnapshot.AppendValue(StringBuilder)、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：CombatTimelineSnapshot.AppendValue(StringBuilder)、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：CombatTimelineSnapshot.AppendValue(StringBuilder)、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 EnemyCombatTimeline.Describe(bool) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：CombatTimelineSnapshot.AppendValue(StringBuilder)、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。</summary>
    internal bool joinCombat, hitReaction, attackComplete, stop, hasPlayer, hasPath, hasWaypoint, hasAnimation, animationTransition, directChase, hasSurroundSlot;
    /// <summary>中文含义：attackCooldown 表示“攻击冷却时间”；rootSpeed 表示“根节点速度”；requestSpeed 表示“请求速度”；blockedTime 表示“阻挡时间”；distance 表示“距离”；heightDifference 表示“高度差值”；angle 表示“角度”。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。</summary>
    internal float attackCooldown, rootSpeed, requestSpeed, blockedTime, distance, heightDifference, angle;
    /// <summary>中文含义：animationProgress 表示“动画进度”；animationSpeed 表示“动画速度”；grabWaiting 表示“抓取等待中”；attackInterval 表示“攻击间隔”；sampleDisplacement 表示“采样位移”；stationaryTime 表示“静止时间”。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyCombatTimeline.LateUpdate()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyCombatTimeline.LateUpdate()。</summary>
    internal float animationProgress, animationSpeed, grabWaiting, attackInterval, sampleDisplacement, stationaryTime;
    /// <summary>中文含义：blockingPathAlly 表示“阻挡中路径同伴”；requestVersion 表示“请求版本”；waypointSequence 表示“路径点序号”；animationIdentifier 表示“动画标识符”；attackCharacterCount 表示“攻击角色数量”；attackerOne 表示“攻击者第一”；attackerTwo 表示“攻击者第二”；attackerThree 表示“攻击者第三”；attackerFour 表示“攻击者第四”。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、VillagerController.CollectCombatTimelineSnapshot()。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 StringBuilder.Append(int) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 StringBuilder.Append(int) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 StringBuilder.Append(int) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 StringBuilder.Append(int) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。</summary>
    internal int blockingPathAlly, requestVersion, waypointSequence, animationIdentifier, attackCharacterCount, attackerOne, attackerTwo, attackerThree, attackerFour;
    /// <summary>中文含义：grabHolder 表示“抓取持有者”；grabWaitingActor 表示“抓取等待中角色”；surroundSlot 表示“包围槽位”。使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。；使用位置：作为 CombatDiagnosticFields.Count(string, double) 的参数、EnemyAIManager.FillCombatTimelineSnapshot(VillagerController, ref CombatTimelineSnapshot)。</summary>
    internal int grabHolder, grabWaitingActor, surroundSlot;
    /// <summary>AppendValue 的业务处理入口；本方法直接依赖 CombatDiagnosticFields.AppendValue(StringBuilder)、CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Position(string, Vector3)、CombatDiagnosticFields.Text(string, string)、EnemyCombatTimeline.Describe(bool)、EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode)、EnemyCombatTimeline.Describe&lt;PathAgentState&gt;(PathAgentState) 等共 11 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyCombatTimeline.WriteSession.Execute()、VillagerController.BuildCombatTimelineSnapshot()；下游（含内部回调体） CombatDiagnosticFields.AppendValue(StringBuilder)、CombatDiagnosticFields.Count(string, double)、CombatDiagnosticFields.Position(string, Vector3)、CombatDiagnosticFields.Text(string, string)、EnemyCombatTimeline.Describe(bool)、EnemyCombatTimeline.Describe&lt;EnemyMovementMode&gt;(EnemyMovementMode)、EnemyCombatTimeline.Describe&lt;PathAgentState&gt;(PathAgentState) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="text">中文含义：文字。使用位置：作为 CombatDiagnosticFields.AppendValue(StringBuilder) 的参数、CombatTimelineSnapshot.AppendValue(StringBuilder)。</param>
    internal void AppendValue(StringBuilder text)
    {
        text.Append("状态=").Append(EnemyCombatTimeline.Describe(state)).Append("，移动=").Append(EnemyCombatTimeline.Describe(movement)).Append("，原因=").Append(reason).Append('，');
        CombatDiagnosticFields.Position("位置", position).AppendValue(text);
        CombatDiagnosticFields.Text("参战", EnemyCombatTimeline.Describe(joinCombat)).AppendValue(text);
        CombatDiagnosticFields.Text("受击待处理", EnemyCombatTimeline.Describe(hitReaction)).AppendValue(text);
        CombatDiagnosticFields.Text("攻击完成", EnemyCombatTimeline.Describe(attackComplete)).AppendValue(text);
        CombatDiagnosticFields.Count("攻击冷却剩余", attackCooldown).AppendValue(text);
        // argumentValue2（按原顺序缓存的实参文字“路径”）：从文字“路径”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 CombatDiagnosticFields.Text(string, string) 的参数。
string argumentValue2 = "路径";
        // selectedValue1（分支选出的文字“无”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 CombatDiagnosticFields.Text(string, string) 的参数、CombatTimelineSnapshot.AppendValue(StringBuilder)。
string selectedValue1;
        if (hasPath)
        {
            selectedValue1 = EnemyCombatTimeline.Describe(path);
        }
        else
        {
            selectedValue1 = "无";
        }

        CombatDiagnosticFields.Text(argumentValue2, selectedValue1).AppendValue(text);
        CombatDiagnosticFields.Text("路径停止", EnemyCombatTimeline.Describe(stop)).AppendValue(text);
        CombatDiagnosticFields.Position("路径目标", target).AppendValue(text);
        CombatDiagnosticFields.Count("动画根速度", rootSpeed).AppendValue(text);
        CombatDiagnosticFields.Count("请求移动速度", requestSpeed).AppendValue(text);
        CombatDiagnosticFields.Count("堵路同伴", blockingPathAlly).AppendValue(text);
        CombatDiagnosticFields.Count("受阻累计", blockedTime).AppendValue(text);
        CombatDiagnosticFields.Text("门洞阶段", doorway).AppendValue(text);
        CombatDiagnosticFields.Text("楼梯分段", stair).AppendValue(text);
        if (hasPlayer)
        {
            CombatDiagnosticFields.Position("玩家位置", playerPosition).AppendValue(text);
            CombatDiagnosticFields.Count("水平距离", distance).AppendValue(text);
            CombatDiagnosticFields.Count("高度差", heightDifference).AppendValue(text);
        }

        if (hasPath)
        {
            CombatDiagnosticFields.Count("寻路请求版本", requestVersion).AppendValue(text);
            CombatDiagnosticFields.Count("路点序号", waypointSequence).AppendValue(text);
            if (hasWaypoint)
            {
                CombatDiagnosticFields.Position("当前路点", waypoint).AppendValue(text);
            }

            CombatDiagnosticFields.Position("路径方向", pathDirection).AppendValue(text);
            CombatDiagnosticFields.Position("追击方向", chaseDirection).AppendValue(text);
            CombatDiagnosticFields.Count("朝向夹角", angle).AppendValue(text);
            CombatDiagnosticFields.Position("意图位移", intentDisplacement).AppendValue(text);
            CombatDiagnosticFields.Position("避让位移", avoidanceDisplacement).AppendValue(text);
            CombatDiagnosticFields.Position("转向限速后位移", speedLimitDisplacement).AppendValue(text);
        }

        if (hasAnimation)
        {
            CombatDiagnosticFields.Count("基础动画标识", animationIdentifier).AppendValue(text);
            CombatDiagnosticFields.Count("动画进度", animationProgress).AppendValue(text);
            CombatDiagnosticFields.Count("动画移动参数", animationSpeed).AppendValue(text);
            CombatDiagnosticFields.Text("动画过渡", EnemyCombatTimeline.Describe(animationTransition)).AppendValue(text);
        }

        CombatDiagnosticFields.Text("许可情况", permission).AppendValue(text);
        CombatDiagnosticFields.Count("当前攻击人数", attackCharacterCount).AppendValue(text);
        text.Append("攻击者序号=").Append(attackerOne).Append(' ').Append(attackerTwo).Append(' ').Append(attackerThree).Append(' ').Append(attackerFour).Append('，');
        CombatDiagnosticFields.Count("抓取持有者", grabHolder).AppendValue(text);
        CombatDiagnosticFields.Count("抓取等待者", grabWaitingActor).AppendValue(text);
        CombatDiagnosticFields.Count("等待剩余", grabWaiting).AppendValue(text);
        CombatDiagnosticFields.Count("全局攻击间隔剩余", attackInterval).AppendValue(text);
        CombatDiagnosticFields.Text("直接追击名额", EnemyCombatTimeline.Describe(directChase)).AppendValue(text);
        if (hasSurroundSlot)
        {
            CombatDiagnosticFields.Count("包围槽", surroundSlot).AppendValue(text);
            CombatDiagnosticFields.Position("包围目标", surroundTarget).AppendValue(text);
        }

        CombatDiagnosticFields.Count("采样位移", sampleDisplacement).AppendValue(text);
        CombatDiagnosticFields.Count("持续静止", stationaryTime).AppendValue(text);
    }
}

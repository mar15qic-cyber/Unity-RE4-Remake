using UnityEngine;

/// <summary>自由环顾的镜头阶段，由玩家持有的状态模型写入，摄像机读取并执行观察或回正；保留原枚举顺序。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "自由视角阶段")]
public enum FreeLookPhase
{
    /// <summary>中文含义：普通状态。正常跟随；只有停止移动后才可解除尚未释放的移动参考锁。</summary>
    [UnityEngine.InspectorName("正常")]
    Normal,
    /// <summary>中文含义：观察。按住自由环顾进行观察，镜头转动不改变已锁定的移动方向参考。</summary>
    [UnityEngine.InspectorName("观察")]
    Observe,
    /// <summary>中文含义：回正。松开观察后由摄像机逐帧向角色朝向回正，移动参考仍保持锁定。</summary>
    [UnityEngine.InspectorName("回正")]
    Recentering
}

/// <summary>
/// LeonController 持有的自由环顾状态。输入桥的有效绑定经控制器传入，运动模块经控制器查询移动方向，
/// 镜头经控制器推进回正；通过同步方法和只读属性通信，镜头阶段与移动参考分别交接，不广播事件。
/// </summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "自由视角状态")]
public sealed class FreeLookState
{
    /// <summary>中文含义：阶段。当前镜头阶段，输入变化、回正推进或重置时更新。</summary>
    public FreeLookPhase Phase { get; private set; }
    /// <summary>中文含义：移动引用是否具有锁定。是否锁定移动参考朝向；回正结束不立即解除，正常阶段停步后才解除。</summary>
    public bool MovementReferenceHasLock { get; private set; }
    /// <summary>中文含义：移动引用角度。第一次进入观察时捕获的水平镜头角，锁定期间用于运动方向解算。</summary>
    public float MovementReferenceAngle { get; private set; }
    /// <summary>中文含义：是否等待中已释放。重置或不允许观察后等待输入层确认松键，防止仍按住时重新进入观察。</summary>
    public bool WaitingReleased { get; private set; }
    /// <summary>中文含义：是否隔离摄像机转向。镜头尚在观察、回正或移动参考仍锁定时，隔离镜头转向对角色移动的影响。</summary>
    public bool IsolateCameraSteering => Phase != FreeLookPhase.Normal || MovementReferenceHasLock;

    /// <summary>中文含义：键绑定是否具有按住。最近一次处理的自由环顾持有状态，用于只在新按下时申请观察。</summary>
    private bool keyBindingHasHeld;
    /// <summary>中文含义：回正开始的角度。松开观察瞬间的水平镜头角，作为本次回正插值起点。</summary>
    private float recenteringStartingAngle;
    /// <summary>中文含义：回正已用时间。本次回正已累计时间，由 AdvanceRecentering 推进，松键或重置时清零。</summary>
    private float recenteringElapsedTime;
    /// <summary>LeonController.UpdateFreeLookState 转发当前有效输入与许可；新按下锁参考，松开观察切入回正。</summary>
    /// <param name="held">中文含义：是否保持按住。有效绑定当前是否按住，不依赖硬编码物理按键。</param>
    /// <param name="allowObserve">中文含义：是否允许观察。当前玩法及动作是否允许进入观察。</param>
    /// <param name="viewHorizontalAngle">中文含义：视图水平角度。当前镜头水平角，用于捕获移动参考或回正起点。</param>
    public void SetKeyBinding(bool held, bool allowObserve, float viewHorizontalAngle)
    {
        if (!held)
        {
            keyBindingHasHeld = false;
            WaitingReleased = false;
            if (Phase == FreeLookPhase.Observe)
            {
                Phase = FreeLookPhase.Recentering;
                recenteringStartingAngle = viewHorizontalAngle;
                recenteringElapsedTime = 0f;
            }

            return;
        }

        if (keyBindingHasHeld)
        {
            return;
        }

        keyBindingHasHeld = true;
        if (!allowObserve || WaitingReleased)
        {
            WaitingReleased = true;
            return;
        }

        if (!MovementReferenceHasLock)
        {
            MovementReferenceAngle = viewHorizontalAngle;
            MovementReferenceHasLock = true;
        }

        Phase = FreeLookPhase.Observe;
    }

    /// <summary>玩家控制器在更新移动输入时调用；观察和回正期间即使停步，也保持旧参考避免路线改变。</summary>
    /// <param name="input">中文含义：输入数据。当前移动向量，只用其平方长度判断是否停步。</param>
    public void UpdateMovementInput(Vector3 input)
    {
        if (Phase == FreeLookPhase.Normal && input.sqrMagnitude <= 0.0001f)
        {
            MovementReferenceHasLock = false;
        }
    }

    /// <summary>LeonLocomotion 经 LeonController 查询；根据参考锁选择捕获朝向或实时镜头水平轴，再合成运动方向。</summary>
    /// <param name="input">中文含义：输入数据。局部移动输入，使用 x 与 z 分量。</param>
    /// <param name="cameraForward">中文含义：摄像机前向。未锁定时采用的镜头前向。</param>
    /// <param name="cameraRight">中文含义：摄像机右侧。未锁定时采用的镜头右向。</param>
    /// <returns>归一化世界移动方向，不在本模型内提交角色位移。</returns>
    public Vector3 CalculateMovementDirection(Vector3 input, Vector3 cameraForward, Vector3 cameraRight)
    {
        // 当前运动前向；锁定时从捕获角重建，未锁定时使用镜头在地面的投影。
        // 中文含义：forwardValue 为“前向值”。
Vector3 forwardValue;
        if (MovementReferenceHasLock)
        {
            forwardValue = Quaternion.Euler(0f, MovementReferenceAngle, 0f) * Vector3.forward;
        }
        else
        {
            forwardValue = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;
        }

        // 当前运动右向；保留独立的参考锁查询，按原分支使用叉积或镜头投影。
        // 中文含义：right 为“右侧”。
Vector3 right;
        if (MovementReferenceHasLock)
        {
            right = Vector3.Cross(Vector3.up, forwardValue);
        }
        else
        {
            right = Vector3.ProjectOnPlane(cameraRight, Vector3.up).normalized;
        }

        return (forwardValue * input.z + right * input.x).normalized;
    }

    /// <summary>摄像机通过玩家控制器逐帧推进回正；完成时只恢复镜头正常阶段，不解除移动参考锁。</summary>
    /// <param name="timeStepSize">中文含义：时间步进尺寸。本次推进时间，负值按零处理。</param>
    /// <param name="duration">中文含义：持续时间。回正目标时长，分母至少为 0.01 秒。</param>
    /// <param name="characterHorizontalAngle">中文含义：角色水平角度。当前角色水平角，作为本帧回正目标。</param>
    /// <returns>跨角度边界平滑插值得到的镜头水平角。</returns>
    public float AdvanceRecentering(float timeStepSize, float duration, float characterHorizontalAngle)
    {
        recenteringElapsedTime += Mathf.Max(0f, timeStepSize);
        // 本次回正的归一化进度，限制为零至一。
        // 中文含义：progress 为“进度”。
float progress = Mathf.Clamp01(recenteringElapsedTime / Mathf.Max(0.01f, duration));
        // 使用角度插值避免跨越正负 180 度时绕远路，目标允许随角色朝向变化。
        // 中文含义：angle 为“角度”。
float angle = Mathf.LerpAngle(recenteringStartingAngle, characterHorizontalAngle, Mathf.SmoothStep(0f, 1f, progress));
        if (progress >= 1f)
        {
            Phase = FreeLookPhase.Normal;
        }

        return angle;
    }

    /// <summary>玩家或镜头接管链中断回正时调用；只结束回正阶段，保留移动参考与松键约束。</summary>
    public void InterruptRecentering()
    {
        if (Phase == FreeLookPhase.Recentering)
        {
            Phase = FreeLookPhase.Normal;
        }
    }

    /// <summary>玩家在动作接管或输入阻断时调用，恢复正常阶段并清移动参考；必须收到真实松键才重新允许观察。</summary>
    public void Reset()
    {
        Phase = FreeLookPhase.Normal;
        MovementReferenceHasLock = false;
        recenteringElapsedTime = 0f;
        WaitingReleased = true;
    }
}

using UnityEngine;

/// <summary>
/// 动画视觉事件中继器。
/// 接收动画剪辑中的Animation Event，转发给协调者。
/// 挂载在拥有Animator的Leon根对象上。
/// 不直接修改任何视觉对象、参数、权重或业务状态。
/// </summary>
public class WeaponVisualEventRelay : MonoBehaviour
{
    /// <summary>中文含义：当前玩家动画动作协调器。使用位置：WeaponVisualEventRelay.OnEquipVisualReveal()、WeaponVisualEventRelay.OnHolsterVisualCommit()、WeaponVisualEventRelay.OnLongGunSupportHandGrip() 等共 5 处。</summary>
    [SerializeField]
    private LeonAnimationActionCoordinator coordinator;
    /// <summary>中文含义：动画控制器。使用位置：作为 LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker) 的参数、WeaponVisualEventRelay.OnEquipVisualReveal()、WeaponVisualEventRelay.OnHolsterVisualCommit() 等共 6 处。</summary>
    [SerializeField]
    private Animator animator;
    /// <summary>中文含义：是否错误已记录。使用位置：WeaponVisualEventRelay.OnEquipVisualReveal()、WeaponVisualEventRelay.OnHolsterVisualCommit()、WeaponVisualEventRelay.OnLongGunSupportHandGrip() 等共 4 处。</summary>
    private bool errorLogged;
    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Component.GetComponent&lt;Animator&gt;()、Component.GetComponent&lt;LeonAnimationActionCoordinator&gt;()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (coordinator == null)
        {
            coordinator = GetComponent<LeonAnimationActionCoordinator>();
        }
    }

    /// <summary>
    /// 动画事件入口：收枪视觉提交点。
    /// 由HolsterPistol_Anim和HolsterRifle_Anim中的Animation Event调用。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object)、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnHolsterVisualCommit()
    {
        if (coordinator == null || animator == null)
        {
            if (!errorLogged)
            {
                Debug.LogError("[VisualEventRelay] Coordinator or Animator missing");
                errorLogged = true;
            }

            return;
        }

        coordinator.NotifyVisualMarker(animator, VisualMarker.HolsterVisualCommit);
    }

    /// <summary>
    /// 动画事件入口：拔枪视觉显示点。
    /// 由EquipPistol_Anim和EquipRifle_Anim中的Animation Event调用。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object)、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnEquipVisualReveal()
    {
        if (coordinator == null || animator == null)
        {
            if (!errorLogged)
            {
                Debug.LogError("[VisualEventRelay] Coordinator or Animator missing");
                errorLogged = true;
            }

            return;
        }

        coordinator.NotifyVisualMarker(animator, VisualMarker.EquipVisualReveal);
    }

    /// <summary>
    /// 动画事件入口：背部步枪隐藏点。
    /// 由EquipRifle_Anim在OnEquipVisualReveal前一帧调用。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object)、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnRifleBackHideBeforeReveal()
    {
        if (coordinator == null || animator == null)
        {
            if (!errorLogged)
            {
                Debug.LogError("[VisualEventRelay] Coordinator or Animator missing");
                errorLogged = true;
            }

            return;
        }

        coordinator.NotifyVisualMarker(animator, VisualMarker.RifleBackHideBeforeReveal);
    }

    /// <summary>
    /// 动画事件入口：长枪副手到达护木握把点。
    /// 该事件只负责触发长枪副手反向动力学淡入，不承担武器显隐。
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.LogError(object)、LeonAnimationActionCoordinator.NotifyVisualMarker(Animator, VisualMarker)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void OnLongGunSupportHandGrip()
    {
        if (coordinator == null || animator == null)
        {
            if (!errorLogged)
            {
                Debug.LogError("[VisualEventRelay] 协调者或动画器未赋值");
                errorLogged = true;
            }

            return;
        }

        coordinator.NotifyVisualMarker(animator, VisualMarker.LongGunSupportHandGrip);
    }
}

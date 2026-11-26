using UnityEngine;

/// <summary>
/// 武器音效组件：挂载在武器模型上，通过 WeaponAudioProfile 配置播放音效
/// 不再直接保存 AudioClip，改为引用 AudioEventData 配置资产
/// 播放路由到 AudioManager 统一管理混音和对象池
/// </summary>
public class WeaponAudio : MonoBehaviour
{
    /// <summary>武器音效配置资产（包含射击和换弹事件）。</summary>
    [Header("武器音效配置")]
    [Tooltip("武器音效配置资产（包含射击和换弹事件）")]
    public WeaponAudioProfile audioProfile;

    public bool UsesStagedReload => audioProfile != null && audioProfile.stagedReload;

    public bool HasBulletImpact => audioProfile != null
        && audioProfile.bodyImpactEvent != null && audioProfile.headImpactEvent != null;

    public void PlayBulletImpact(bool head, Vector3 position)
    {
        if (!HasBulletImpact || AudioManager.Instance == null)
        {
            return;
        }

        AudioEventData impact = audioProfile.bodyImpactEvent;
        if (head)
        {
            impact = audioProfile.headImpactEvent;
        }

        AudioManager.Instance.Play3D(impact, position);
    }

    public void PlayActionMarker(VisualMarker marker)
    {
        if (audioProfile == null || AudioManager.Instance == null)
        {
            return;
        }

        AudioEventData sound = null;
        if (marker == VisualMarker.HolsterVisualCommit)
        {
            sound = audioProfile.holsterEvent;
        }
        else if (marker == VisualMarker.EquipVisualReveal)
        {
            sound = audioProfile.equipEvent;
        }
        else if (marker == VisualMarker.EquipSettled)
        {
            sound = audioProfile.equipSettledEvent;
        }
        else if (UsesStagedReload)
        {
            if (marker == VisualMarker.MagazineDetached)
            {
                sound = audioProfile.magazineOutEvent;
            }
            else if (marker == VisualMarker.MagazineAttached)
            {
                sound = audioProfile.reloadEvent;
            }
            else if (marker == VisualMarker.ReloadMechanism)
            {
                sound = audioProfile.mechanismEvent;
            }
        }

        if (sound != null)
        {
            AudioManager.Instance.Play3D(sound, transform.position);
        }
    }
    /// <summary>
    /// 播放射击音效（三维空间定位）
    /// </summary>
    /// <remarks>静态调用链：上游 WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、Debug.LogWarning(object, Object)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PlayFire()
    {
        if (audioProfile == null || audioProfile.fireEvent == null)
        {
            Debug.LogWarning($"[WeaponAudio] 射击音效未配置: {gameObject.name}", this);
            return;
        }

        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue1 = AudioManager.Instance;
        // 条件访问得到的播放3距离的返回结果。
global::AudioPlayHandle? optionalValue2;
        if (!global::System.Object.ReferenceEquals(receiverValue1, null))
        {
            optionalValue2 = receiverValue1.Play3D(audioProfile.fireEvent, transform.position);
        }
        else
        {
            optionalValue2 = default;
        }
    }

    /// <summary>PlayDryFire 的业务处理入口；本方法直接依赖 AudioManager.Play3D(AudioEventData, Vector3)、Debug.LogWarning(object, Object)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonCombat.ExecuteFirearmAttack()；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、Debug.LogWarning(object, Object)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PlayDryFire()
    {
        if (audioProfile == null || audioProfile.dryFireEvent == null)
        {
            Debug.LogWarning($"[WeaponAudio] 卡壳音效未配置: {gameObject.name}", this);
            return;
        }

        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue3 = AudioManager.Instance;
        // 条件访问得到的播放3距离的返回结果。
global::AudioPlayHandle? optionalValue4;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            optionalValue4 = receiverValue3.Play3D(audioProfile.dryFireEvent, transform.position);
        }
        else
        {
            optionalValue4 = default;
        }
    }

    /// <summary>
    /// 播放换弹音效（三维空间定位）
    /// </summary>
    /// <remarks>静态调用链：上游 LeonWeaponHandler.StartReload()、WeaponBase.StartReload()；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、Debug.LogWarning(object, Object)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PlayReload()
    {
        if (audioProfile == null || audioProfile.reloadEvent == null)
        {
            Debug.LogWarning($"[WeaponAudio] 换弹音效未配置: {gameObject.name}", this);
            return;
        }

        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue5 = AudioManager.Instance;
        // 条件访问得到的播放3距离的返回结果。
global::AudioPlayHandle? optionalValue6;
        if (!global::System.Object.ReferenceEquals(receiverValue5, null))
        {
            optionalValue6 = receiverValue5.Play3D(audioProfile.reloadEvent, transform.position);
        }
        else
        {
            optionalValue6 = default;
        }
    }

    /// <summary>
    /// 播放近战攻击音效
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PlayMelee()
    {
        if (audioProfile == null || audioProfile.meleeEvent == null)
        {
            return;
        }

        // 判空前缓存的音频管理器的实例。
global::AudioManager receiverValue7 = AudioManager.Instance;
        // 条件访问得到的播放3距离的返回结果。
global::AudioPlayHandle? optionalValue8;
        if (!global::System.Object.ReferenceEquals(receiverValue7, null))
        {
            optionalValue8 = receiverValue7.Play3D(audioProfile.meleeEvent, transform.position);
        }
        else
        {
            optionalValue8 = default;
        }
    }
}

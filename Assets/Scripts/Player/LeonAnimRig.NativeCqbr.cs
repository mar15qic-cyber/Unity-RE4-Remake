using UnityEngine;

public partial class LeonAnimRig
{
    [SerializeField] private WeaponBase nativeCqbrWeapon;
    [SerializeField] private Transform nativeCqbrAimSpine;
    [SerializeField] private Vector3 nativeCqbrAimForward;
    [SerializeField] private WeaponBase nativeW870Weapon;
    [SerializeField] private Vector3 nativeW870AimForward;
    [SerializeField] private WeaponBase nativePistolWeapon;
    [SerializeField] private Vector3 nativePistolAimForward;
    [SerializeField] private AnimationClip nativePistolReadyMechanism;
    [SerializeField] private AnimationClip nativePistolEmptyMechanism;
    [SerializeField] private AnimationClip nativePistolEmptyReloadMechanism;
    [SerializeField] private AnimationClip nativePistolTacticalReloadMechanism;
    [SerializeField] private Transform nativePistolIncomingMagazine;
    [SerializeField] private Transform nativePistolOutgoingMagazine;
    [SerializeField] private AnimationClip nativeCqbrShotMechanism;
    [SerializeField] private AnimationClip nativeCqbrLastMechanism;
    [SerializeField] private AnimationClip nativeCqbrDryMechanism;
    [SerializeField] private AnimationClip nativeCqbrEmptyMechanism;
    [SerializeField] private AnimationClip nativeCqbrReloadMechanism;
    [SerializeField] private Transform nativeCqbrBolt;
    [SerializeField] private Transform nativeCqbrMagazine;
    [SerializeField] private Vector3 nativeCqbrOpenBoltPosition;

    private bool nativeCqbrMechanismDirty;
    private bool nativeCqbrReloadSampled;
    private bool nativeCqbrReloadStartedOpen;
    private bool nativeCqbrSpineAdjusted;
    private Quaternion nativeCqbrSpineBeforeAim;
    private Quaternion nativeCqbrSpineAfterAim;
    private float nativeCqbrAimWeight;
    private Transform nativeW870Head;
    private Transform nativeW870Neck;
    private bool nativeW870HeadBindingsRead;
    private Quaternion nativeW870NeckBeforeAim;
    private Quaternion nativeW870NeckAfterAim;
    private bool nativeW870HeadAdjusted;
    private Quaternion nativeW870HeadBeforeAim;
    private Quaternion nativeW870HeadAfterAim;
    private Transform nativeW870SpineMiddle;
    private Transform nativeW870SpineLower;
    private Quaternion nativeW870MiddleBeforeAim;
    private Quaternion nativeW870MiddleAfterAim;
    private Quaternion nativeW870LowerBeforeAim;
    private Quaternion nativeW870LowerAfterAim;
    private bool nativeW870SpineDistributed;
    private WeaponBase nativeAimAppliedWeapon;
    private AnimationClip nativePistolSampledReload;
    private KnifeCombatModule nativeKnifeAim;
    private float nativeKnifePitch;
    private Transform lookPitchNeck;
    private Quaternion lookPitchBefore;
    private Quaternion lookPitchAfter;
    private float lookPitchOffset;
    private bool lookPitchApplied;

    private void UpdateNativeGunPresentation()
    {
        if (lookPitchApplied && lookPitchNeck != null && lookPitchNeck.localRotation.Equals(lookPitchAfter))
        {
            lookPitchNeck.localRotation = lookPitchBefore;
        }
        lookPitchApplied = false;
        if (nativeCqbrSpineAdjusted && nativeCqbrAimSpine.localRotation.Equals(nativeCqbrSpineAfterAim))
        {
            nativeCqbrAimSpine.localRotation = nativeCqbrSpineBeforeAim;
        }
        nativeCqbrSpineAdjusted = false;
        if (nativeW870SpineDistributed)
        {
            if (nativeW870SpineMiddle.localRotation.Equals(nativeW870MiddleAfterAim))
            {
                nativeW870SpineMiddle.localRotation = nativeW870MiddleBeforeAim;
            }
            if (nativeW870SpineLower.localRotation.Equals(nativeW870LowerAfterAim))
            {
                nativeW870SpineLower.localRotation = nativeW870LowerBeforeAim;
            }
            nativeW870SpineDistributed = false;
        }
        if (nativeW870HeadAdjusted && nativeW870Neck != null && nativeW870Neck.localRotation.Equals(nativeW870NeckAfterAim))
        {
            nativeW870Neck.localRotation = nativeW870NeckBeforeAim;
        }
        if (nativeW870HeadAdjusted && nativeW870Head != null && nativeW870Head.localRotation.Equals(nativeW870HeadAfterAim))
        {
            nativeW870Head.localRotation = nativeW870HeadBeforeAim;
        }
        nativeW870HeadAdjusted = false;
        if (nativeKnifeAim != null && nativeKnifeAim.IsGuardAiming)
        {
            return;
        }
        nativeKnifePitch = 0f;
        UpdateNativeFirearmPresentation();
        UpdateRelaxedLookPitch();
    }

    private void UpdateRelaxedLookPitch()
    {
        if (controller == null || controller.playerCamera == null || nativeCqbrAimSpine == null)
        {
            return;
        }
        if (lookPitchNeck == null)
        {
            foreach (Transform bone in nativeCqbrAimSpine.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "head neck upper")
                {
                    lookPitchNeck = bone;
                    break;
                }
            }
        }
        if (lookPitchNeck == null)
        {
            return;
        }

        float targetPitch = 0f;
        bool relaxed = controller.currentState == LeonState.Idle || controller.currentState == LeonState.Walking
            || controller.currentState == LeonState.Running || controller.currentState == LeonState.Sprinting;
        if (!isDead && relaxed && !controller.IsAiming && !controller.IsolateObserveCameraSteering)
        {
            float pitch = Mathf.DeltaAngle(0f, controller.playerCamera.transform.eulerAngles.x);
            targetPitch = Mathf.Clamp(pitch * 0.48f, -29f, 32f);
        }
        lookPitchOffset = Mathf.MoveTowards(lookPitchOffset, targetPitch, 220f * Time.deltaTime);
        if (relaxed && !controller.IsAiming && Mathf.Abs(lookPitchOffset) > 0.01f)
        {
            lookPitchBefore = lookPitchNeck.localRotation;
            lookPitchNeck.rotation = Quaternion.AngleAxis(lookPitchOffset, controller.transform.right) * lookPitchNeck.rotation;
            lookPitchAfter = lookPitchNeck.localRotation;
            lookPitchApplied = true;
        }
    }

    internal void ApplyKnifeAimAfterPose()
    {
        if (!isDead && nativeKnifeAim != null && nativeKnifeAim.IsGuardAiming
            && nativeCqbrAimSpine != null && controller != null && controller.playerCamera != null)
        {
            float pitch = Mathf.DeltaAngle(0f, controller.playerCamera.transform.eulerAngles.x);
            if (controller.IsolateObserveCameraSteering)
            {
                pitch = 0f;
            }
            nativeKnifePitch = Mathf.MoveTowards(nativeKnifePitch, Mathf.Clamp(pitch, -45f, 50f), 220f * Time.deltaTime);
            nativeCqbrSpineBeforeAim = nativeCqbrAimSpine.localRotation;
            nativeCqbrAimSpine.rotation = Quaternion.AngleAxis(nativeKnifePitch, controller.transform.right)
                * nativeCqbrAimSpine.rotation;
            nativeCqbrSpineAfterAim = nativeCqbrAimSpine.localRotation;
            nativeCqbrSpineAdjusted = true;
        }
    }

    private void UpdateNativeFirearmPresentation()
    {
        WeaponBase weapon = null;
        Vector3 forward = Vector3.zero;
        float targetWeight = 0f;
        if (!isDead && driver != null && currentWeapon == WeaponType.Rifle && nativeCqbrWeapon != null)
        {
            SampleNativeCqbrMechanism();
            weapon = nativeCqbrWeapon;
            forward = nativeCqbrAimForward;
            targetWeight = driver.NativeCqbrAimCorrectionWeight;
        }
        else
        {
            RestoreNativeCqbrReloadMechanism();
            if (!isDead && driver != null && currentWeapon == WeaponType.Shotgun)
            {
                weapon = nativeW870Weapon;
                forward = nativeW870AimForward;
                targetWeight = driver.NativeW870AimCorrectionWeight;
            }
            else if (!isDead && driver != null && currentWeapon == WeaponType.Pistol && nativePistolWeapon != null)
            {
                SampleNativePistolMechanism();
                weapon = nativePistolWeapon;
                forward = nativePistolAimForward;
                targetWeight = driver.NativePistolAimCorrectionWeight;
            }
        }
        if (weapon != nativePistolWeapon || isDead)
        {
            RestoreNativePistolReloadMechanism();
        }
        if (nativeAimAppliedWeapon != weapon)
        {
            nativeAimAppliedWeapon = weapon;
            nativeCqbrAimWeight = 0f;
        }

        nativeCqbrAimWeight = Mathf.MoveTowards(nativeCqbrAimWeight,
            targetWeight, Time.deltaTime / 0.10f);
        if (weapon == null || nativeCqbrAimSpine == null || controller == null || controller.playerCamera == null)
        {
            return;
        }

        var cameraTransform = controller.playerCamera.transform;
        var muzzle = weapon.muzzlePoint;
        if (muzzle == null)
        {
            return;
        }
        var aimPoint = cameraTransform.position + cameraTransform.forward * 100f;
        var direction = nativeCqbrAimSpine.TransformDirection(forward);
        var correction = Quaternion.FromToRotation(direction, aimPoint - muzzle.position);
        correction = Quaternion.RotateTowards(Quaternion.identity, correction, 80f);
        Quaternion appliedCorrection = Quaternion.Slerp(Quaternion.identity, correction, nativeCqbrAimWeight);
        bool stabilizeHead = weapon == nativeW870Weapon;
        Quaternion animatedHeadRotation = Quaternion.identity;
        Quaternion animatedNeckRotation = Quaternion.identity;
        if (stabilizeHead)
        {
            if (!nativeW870HeadBindingsRead)
            {
                nativeW870HeadBindingsRead = true;
                var middle = nativeCqbrAimSpine.parent;
                if (middle != null && middle.name == "spine middle" && middle.parent != null && middle.parent.name == "spine lower")
                {
                    nativeW870SpineMiddle = middle;
                    nativeW870SpineLower = middle.parent;
                }
                foreach (Transform bone in nativeCqbrAimSpine.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name == "head")
                    {
                        nativeW870Head = bone;
                    }
                    else if (bone.name == "head neck upper")
                    {
                        nativeW870Neck = bone;
                    }
                }
            }

            if (nativeW870Neck != null)
            {
                animatedNeckRotation = nativeW870Neck.rotation;
                nativeW870NeckBeforeAim = nativeW870Neck.localRotation;
            }
            if (nativeW870Head != null)
            {
                animatedHeadRotation = nativeW870Head.rotation;
                nativeW870HeadBeforeAim = nativeW870Head.localRotation;
            }
        }

        nativeCqbrSpineBeforeAim = nativeCqbrAimSpine.localRotation;
        Quaternion animatedSpine = nativeCqbrAimSpine.rotation;
        if (stabilizeHead && nativeW870SpineMiddle != null && nativeW870SpineLower != null)
        {
            // 深俯角沿胸腰分配，胸骨最终转角仍对准原来的瞄准方向。
            Quaternion animatedMiddle = nativeW870SpineMiddle.rotation;
            nativeW870MiddleBeforeAim = nativeW870SpineMiddle.localRotation;
            nativeW870LowerBeforeAim = nativeW870SpineLower.localRotation;
            nativeW870SpineLower.rotation = Quaternion.Slerp(Quaternion.identity, appliedCorrection, .23f)
                * nativeW870SpineLower.rotation;
            nativeW870SpineMiddle.rotation = Quaternion.Slerp(Quaternion.identity, appliedCorrection, .57f) * animatedMiddle;
            nativeW870LowerAfterAim = nativeW870SpineLower.localRotation;
            nativeW870MiddleAfterAim = nativeW870SpineMiddle.localRotation;
            nativeW870SpineDistributed = true;
        }
        nativeCqbrAimSpine.rotation = appliedCorrection * animatedSpine;
        if (stabilizeHead)
        {
            // 脊柱和手臂负责让枪，头颈只跟随原始观察姿势和小幅瞄准补偿。
            Quaternion gaze = Quaternion.RotateTowards(Quaternion.identity, appliedCorrection, 12f);
            if (nativeW870Neck != null)
            {
                nativeW870Neck.rotation = gaze * animatedNeckRotation;
                nativeW870NeckAfterAim = nativeW870Neck.localRotation;
                nativeW870HeadAdjusted = true;
            }
            if (nativeW870Head != null)
            {
                nativeW870Head.rotation = gaze * animatedHeadRotation;
                nativeW870HeadAfterAim = nativeW870Head.localRotation;
                nativeW870HeadAdjusted = true;
            }
        }
        nativeCqbrSpineAfterAim = nativeCqbrAimSpine.localRotation;
        nativeCqbrSpineAdjusted = true;
    }

    private void SampleNativePistolMechanism()
    {
        var model = nativePistolWeapon.VisualModel;
        if (model.activeInHierarchy && driver.TryReadNativePistolReload(out float progress))
        {
            var reload = nativePistolTacticalReloadMechanism;
            if (driver.NativePistolEmptyReload)
            {
                reload = nativePistolEmptyReloadMechanism;
            }
            if (reload != null)
            {
                reload.SampleAnimation(model, progress * reload.length);
                ClampMagazineScale(nativePistolIncomingMagazine);
                ClampMagazineScale(nativePistolOutgoingMagazine);
                nativePistolSampledReload = reload;
                return;
            }
        }

        RestoreNativePistolReloadMechanism();
        if (!model.activeInHierarchy)
        {
            return;
        }
        // 开火期间保留动画驱动的套筒。其他状态按当前弹仓保持锁开或闭合。
        if (!driver.NativePistolFireCycleActive)
        {
            var motion = nativePistolReadyMechanism;
            if (nativePistolWeapon.currentAmmo == 0)
            {
                motion = nativePistolEmptyMechanism;
            }
            if (motion != null)
            {
                motion.SampleAnimation(model, 0f);
            }
        }
        // 静置枪体的源片会恢复退下的弹匣节点，播放换弹之外只显示枪内弹匣。
        if (nativePistolIncomingMagazine != null)
        {
            nativePistolIncomingMagazine.localScale = Vector3.one;
        }
        if (nativePistolOutgoingMagazine != null)
        {
            nativePistolOutgoingMagazine.localScale = Vector3.one * 0.001f;
        }
    }

    private void RestoreNativePistolReloadMechanism()
    {
        if (nativePistolSampledReload == null)
        {
            return;
        }
        if (nativePistolWeapon != null && nativePistolWeapon.VisualModel != null)
        {
            nativePistolSampledReload.SampleAnimation(nativePistolWeapon.VisualModel, nativePistolSampledReload.length);
            ClampMagazineScale(nativePistolIncomingMagazine);
            ClampMagazineScale(nativePistolOutgoingMagazine);
        }
        nativePistolSampledReload = null;
    }

    private static void ClampMagazineScale(Transform magazine)
    {
        if (magazine != null)
        {
            magazine.localScale = Vector3.Min(Vector3.one,
                Vector3.Max(Vector3.one * 0.001f, magazine.localScale));
        }
    }

    private void SampleNativeCqbrMechanism()
    {
        var model = nativeCqbrWeapon.VisualModel;
        if (nativeCqbrReloadMechanism != null && driver.NativeCqbrReloadActive
            && driver.TryReadNativeCqbrReload(out float reloadProgress))
        {
            if (!nativeCqbrReloadSampled)
            {
                nativeCqbrReloadStartedOpen = nativeCqbrWeapon.currentAmmo == 0;
            }
            nativeCqbrReloadMechanism.SampleAnimation(model, reloadProgress * nativeCqbrReloadMechanism.length);
            // FBX 插值会越过隐藏缩放下界。限制弹匣比例以免中途镜像。
            ClampMagazineScale(nativeCqbrMagazine);
            if (nativeCqbrReloadStartedOpen && reloadProgress < LeonAnimatorDriver.NativeCqbrBoltReleaseTime)
            {
                nativeCqbrBolt.localPosition = nativeCqbrOpenBoltPosition;
            }
            nativeCqbrReloadSampled = true;
            nativeCqbrMechanismDirty = true;
            return;
        }
        RestoreNativeCqbrReloadMechanism();
        if (driver.ReadNativeCqbrMechanism(out float progress, out bool last, out bool dry))
        {
            var motion = nativeCqbrShotMechanism;
            if (last)
            {
                motion = nativeCqbrLastMechanism;
            }
            if (dry)
            {
                motion = nativeCqbrDryMechanism;
            }
            float time = RE4.ChapterOne.ChapterOneRootActor.RifleRecoilSourceFrame(progress * 30f) / motion.frameRate;
            if (dry)
            {
                time = progress * motion.length;
            }
            motion.SampleAnimation(model, time);
            nativeCqbrMechanismDirty = true;
        }
        else if (nativeCqbrWeapon.currentAmmo == 0)
        {
            nativeCqbrEmptyMechanism.SampleAnimation(model, 0f);
            nativeCqbrMechanismDirty = true;
        }
        else if (nativeCqbrMechanismDirty)
        {
            nativeCqbrShotMechanism.SampleAnimation(model, 0f);
            nativeCqbrMechanismDirty = false;
        }
    }

    private void RestoreNativeCqbrReloadMechanism()
    {
        if (!nativeCqbrReloadSampled)
        {
            return;
        }
        nativeCqbrReloadSampled = false;
        nativeCqbrReloadStartedOpen = false;
        if (nativeCqbrWeapon == null || nativeCqbrWeapon.VisualModel == null)
        {
            return;
        }
        nativeCqbrReloadMechanism.SampleAnimation(nativeCqbrWeapon.VisualModel, nativeCqbrReloadMechanism.length);
        if (nativeCqbrWeapon.currentAmmo == 0 && nativeCqbrBolt != null)
        {
            nativeCqbrBolt.localPosition = nativeCqbrOpenBoltPosition;
        }
    }
}

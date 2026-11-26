using UnityEngine;

public partial class LeonAnimatorDriver
{
    private enum NativePace
    {
        Walk, Jog, Sprint, Crouch, HurtWalk, HurtJog,
        ForwardLeft, StrafeLeft, BackLeft, Backstep, BackRight, StrafeRight, ForwardRight,
        AimForward, AimForwardLeft, AimStrafeLeft, AimBackLeft, AimBackstep, AimBackRight, AimStrafeRight, AimForwardRight,
        HurtForwardLeft, HurtStrafeLeft, HurtBackLeft, HurtBackstep, HurtBackRight, HurtStrafeRight, HurtForwardRight
    }
    internal enum NativeDirection { Forward, ForwardLeft, Left, BackLeft, Back, BackRight, Right, ForwardRight }
    private enum NativePhase { Idle, Start, Loop, Stop, Stance, Turn }
    private enum NativeStance { Down, Up, MovingDown, MovingUpLeft, MovingUpRight }

    internal const string NativeIdleName = "NativePistolIdle";
    internal const string NativeCrouchIdleName = "NativePistolCrouchIdle";
    internal const string NativeHurtIdleName = "NativePistolHurtIdle";
    internal const string NativeCqbrIdleName = "NativeCqbrIdle";
    internal const string NativeCqbrHurtIdleName = "NativeCqbrHurtIdle";
    internal const string NativeKnifeIdleName = "NativeKnifeIdle";
    internal static readonly int NativeIdleState = Animator.StringToHash(NativeIdleName);
    private static readonly int NativeCrouchIdleState = Animator.StringToHash(NativeCrouchIdleName);
    private static readonly int NativeHurtIdleState = Animator.StringToHash(NativeHurtIdleName);
    private static readonly int NativeCqbrIdleState = Animator.StringToHash(NativeCqbrIdleName);
    private static readonly int NativeCqbrHurtIdleState = Animator.StringToHash(NativeCqbrHurtIdleName);
    private static readonly int NativeKnifeIdleState = Animator.StringToHash(NativeKnifeIdleName);
    private static readonly int NativeLegacyMove = Animator.StringToHash("MovementLoop");
    private static readonly int NativeLegacyCrouch = Animator.StringToHash("CrouchMovement");
    internal static readonly (string name, string start, string loop, string stop, int startExit,
        int loopEntry, int loopFrames, int stopExit, float commonPhase)[] NativeGaits =
    {
        ("Walk", "LeonMove_0400", "LeonMove_0403_GaitCycle", "LeonMove_0406", 29, 42, 63, 21, 0f),
        ("Jog", "LeonMove_0500", "LeonMove_0520_GaitCycle", "LeonMove_0540", 22, 25, 47, 42, 41f / 47f),
        ("Sprint", "LeonMove_0700", "LeonMove_0720_GaitCycle", "LeonMove_0740", 28, 22, 34, 54, 0f),
        ("Crouch", "LeonMove_1400", "LeonMove_1403_GaitCycle", "LeonMove_1406", 45, 47, 64, 15, 0f),
        ("HurtWalk", "LeonHurt_0400", "LeonHurt_0403_GaitCycle", "LeonHurt_0406", 35, 2, 59, 52, 14f / 59f),
        ("HurtJog", "LeonHurt_0500", "LeonHurt_0520_GaitCycle", "LeonHurt_0540", 24, 22, 47, 95, 40f / 47f)
    };
    internal const string BasicRunLeanParameter = "NativeRunLean";
    private static readonly int BasicRunLeanHash = Animator.StringToHash(BasicRunLeanParameter);
    // 帧数为转身片段结束帧，以及接回 0520 时的同脚相位。
    internal static readonly (string clip, int exitFrame, int loopEntry)[] BasicRunTurns =
    {
        ("LeonMove_0530", 56, 1),
        ("LeonMove_0532", 60, 1),
        ("LeonMove_0534", 60, 25),
        ("LeonMove_0536", 60, 25)
    };
    private static readonly int[,] BasicRunTurnStates = BuildBasicRunTurnHashes();
    private static readonly int[] BasicRunLoopStates =
    {
        Animator.StringToHash("NativePistolBasicRunLoop"),
        Animator.StringToHash("NativeCqbrBasicRunLoop"),
        Animator.StringToHash("NativeKnifeBasicRunLoop")
    };
    private const float WalkReverseBrakeSeconds = 0.08f;
    private bool basicGaitActive;
    private float walkReverseRemaining;
    private Vector3 runTurnHeading;
    private bool previousRunForward;
    private bool basicRunRequested;
    private int runTurnIndex;
    private float basicRunLean;

    internal static string BasicRunTurnStateName(string prefix, string clip) => prefix + "JogTurn" + clip.Substring(9);

    private static int[,] BuildBasicRunTurnHashes()
    {
        string[] prefixes = { "NativePistol", "NativeCqbr", "NativeKnife" };
        var hashes = new int[prefixes.Length, BasicRunTurns.Length];
        for (int row = 0; row < prefixes.Length; row++)
        {
            for (int col = 0; col < BasicRunTurns.Length; col++)
            {
                hashes[row, col] = Animator.StringToHash(BasicRunTurnStateName(prefixes[row], BasicRunTurns[col].clip));
            }
        }
        return hashes;
    }

    internal bool CanUseBasicGait => CanUseNativeGait(false, false)
        && animator.HasState(0, BasicRunLoopStates[BasicRunTurnRow])
        && animator.HasState(0, BasicRunTurnStates[BasicRunTurnRow, 0]);
    internal bool IsBasicGaitActive => ownsNativeGait && basicGaitActive;
    internal bool IsBasicWalkBraking => IsBasicGaitActive && walkReverseRemaining > 0f;
    internal bool IsBasicRunTurning => IsBasicGaitActive && nativePhase == NativePhase.Turn;
    internal bool IsBasicRunning => IsBasicGaitActive && (basicRunRequested
        || nativePace == NativePace.Jog && nativePhase != NativePhase.Idle);

    private int BasicRunTurnRow
    {
        get
        {
            if (currentWeaponType == WeaponType.Rifle)
            {
                return 1;
            }
            if (currentWeaponType == WeaponType.Knife)
            {
                return 2;
            }
            return 0;
        }
    }

    internal void CancelBasicGaitTransition()
    {
        walkReverseRemaining = 0f;
        if (nativePhase == NativePhase.Turn)
        {
            nativeResumeLoopPhase = BasicRunTurns[runTurnIndex].loopEntry / (float)GaitFor(NativePace.Jog).loopFrames;
            PlayNativeGait(NativePhase.Stop, 0.12f);
        }
    }
    // 循环帧和锁脚窗口来自原版标记。镜像片段交换左右脚窗口。
    private static readonly (string name, int startExit, int loopEntry, int loopFrames, int stopExit,
        int leftContact, int rightContact, int contactFrames)[] DirectionalGaits =
    {
        ("ForwardLeft", 43, 37, 67, 67, 21, 55, 20),
        ("StrafeLeft", 35, 31, 65, 67, 16, 50, 20),
        ("BackLeft", 39, 30, 65, 67, 50, 20, 20),
        ("Backstep", 41, 31, 65, 67, 20, 53, 20),
        ("BackRight", 39, 30, 65, 67, 20, 50, 20),
        ("StrafeRight", 36, 33, 65, 63, 23, 53, 20),
        ("ForwardRight", 43, 37, 67, 67, 55, 21, 20),
        ("AimForward", 27, 35, 70, 55, 23, 56, 20),
        ("AimForwardLeft", 27, 35, 70, 55, 23, 58, 20),
        ("AimStrafeLeft", 34, 22, 70, 50, 42, 8, 20),
        ("AimBackLeft", 25, 0, 70, 67, 52, 21, 20),
        ("AimBackstep", 31, 19, 70, 67, 42, 5, 20),
        ("AimBackRight", 25, 0, 70, 67, 21, 52, 20),
        ("AimStrafeRight", 31, 34, 70, 45, 20, 55, 20),
        ("AimForwardRight", 27, 35, 70, 55, 58, 23, 20),
        ("HurtForwardLeft", 32, 35, 63, 65, 17, 52, 15),
        ("HurtStrafeLeft", 38, 33, 70, 62, 22, 56, 15),
        ("HurtBackLeft", 39, 29, 66, 72, 50, 14, 15),
        ("HurtBackstep", 49, 34, 69, 72, 19, 53, 15),
        ("HurtBackRight", 39, 29, 66, 72, 14, 50, 15),
        ("HurtStrafeRight", 49, 28, 66, 69, 17, 42, 15),
        ("HurtForwardRight", 32, 35, 63, 65, 52, 17, 15),
    };
    private static readonly int[,] DirectionalStates = BuildDirectionalHashes();

    internal bool IsNativeDirectionalMoving => ownsNativeGait && IsDirectionalPace(nativePace) && nativePhase != NativePhase.Idle;

    internal bool CanUseNativeDirectionalGait(NativeDirection direction, bool nativeAim, bool injured)
    {
        if (!IsValid)
        {
            return false;
        }

        var pace = DirectionalPace(direction, nativeAim, injured);
        if (IsDirectionalPace(pace))
        {
            int index = DirectionalIndex(pace);
            return animator.HasState(0, DirectionalStates[index, (int)NativePhase.Start]) &&
                animator.HasState(0, DirectionalStates[index, (int)NativePhase.Loop]) &&
                animator.HasState(0, DirectionalStates[index, (int)NativePhase.Stop]);
        }

        return pace == NativePace.HurtWalk && SupportsNativeInjury;
    }

    private static NativePace DirectionalPace(NativeDirection direction, bool nativeAim, bool injured)
    {
        int sector = (int)direction;
        if (injured)
        {
            if (direction == NativeDirection.Forward)
            {
                return NativePace.HurtWalk;
            }
            return (NativePace)((int)NativePace.HurtForwardLeft + sector - 1);
        }
        if (nativeAim)
        {
            return (NativePace)((int)NativePace.AimForward + sector);
        }

        if (direction == NativeDirection.Forward)
        {
            return NativePace.Walk;
        }

        return (NativePace)((int)NativePace.ForwardLeft + sector - 1);
    }

    internal bool UsesNativeDirectionalRootMotion()
    {
        return IsNativeDirectionalMoving || IsDirectionalState(animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
    }

    private static bool IsDirectionalPace(NativePace pace)
    {
        return DirectionalIndex(pace) >= 0 && DirectionalIndex(pace) < DirectionalGaits.Length;
    }

    private static int DirectionalIndex(NativePace pace)
    {
        return (int)pace - (int)NativePace.ForwardLeft;
    }

    private static bool IsDirectionalState(int hash)
    {
        foreach (int state in DirectionalStates)
        {
            if (state != 0 && state == hash)
            {
                return true;
            }
        }

        return false;
    }

    private static int[,] BuildDirectionalHashes()
    {
        var states = new int[DirectionalGaits.Length, 4];
        for (int index = 0; index < DirectionalGaits.Length; index++)
        {
            for (int phase = 1; phase <= 3; phase++)
            {
                states[index, phase] = Animator.StringToHash("Native" + DirectionalGaits[index].name + (NativePhase)phase);
            }
        }

        return states;
    }

    private static (int startExit, int loopEntry, int loopFrames, int stopExit, float commonPhase) GaitFor(NativePace pace)
    {
        if (IsDirectionalPace(pace))
        {
            var gait = DirectionalGaits[DirectionalIndex(pace)];
            return (gait.startExit, gait.loopEntry, gait.loopFrames, gait.stopExit, 0f);
        }

        var standard = NativeGaits[(int)pace];
        return (standard.startExit, standard.loopEntry, standard.loopFrames, standard.stopExit, standard.commonPhase);
    }

    internal static readonly (string state, string clip, int exitFrame, int loopEntry)[] NativeStances =
    {
        ("NativePistolCrouchDown", "LeonMove_0169", 56, -1),
        ("NativePistolCrouchUp", "LeonMove_1169", 56, -1),
        ("NativePistolCrouchMovingDown", "LeonMove_0543", 79, 53),
        ("NativePistolCrouchMovingUpLeft", "LeonMove_1500", 59, 4),
        ("NativePistolCrouchMovingUpRight", "LeonMove_1510", 64, 30)
    };
    private static readonly int[] NativeStanceStates = BuildNativeStanceHashes();
    private static readonly int[,] NativeGaitStates = BuildNativeGaitHashes();
    private static readonly int[,] NativeCqbrStates = BuildNativeGaitHashes("NativeCqbr");
    private static readonly int[,] NativeKnifeStates = BuildNativeGaitHashes("NativeKnife");
    // 接触区间来自重绑循环中脚尖落地至抬离的归一化相位。
    private static readonly (float left, float right, float leftDuration, float rightDuration)[] NativeFootPhases =
    {
        (0.445f, 0.95f, 0.505f, 0.505f),
        (0.345f, 0.86f, 0.32f, 0.32f),
        (0.515f, 0.99f, 0.285f, 0.285f),
        (0.515f, 0.01f, 0.46f, 0.46f),
        (0.59f, 0.132f, 0.625f, 0.59f),
        (0.25f, 0.765f, 0.495f, 0.385f)
    };
    private bool ownsNativeGait;
    private bool holdsNativeInjuryDuringKnifeAction;
    private int requestedNativeState;
    private NativePace nativePace;
    private NativePhase nativePhase;
    private float nativeResumeLoopPhase;
    private NativeStance nativeStance;
    private bool nativeCrouching;
    private WeaponType nativeGaitWeaponType;
    private bool nativeInjuryPoseActive;
    private bool nativeCqbrCarry;
    private bool nativeKnifeGait;
    private bool nativeStanceHasInput;
    internal Vector3 NativeGaitDirection { get; private set; }
    internal bool IsNativeGaitMoving => ownsNativeGait && nativePhase != NativePhase.Idle;
    internal bool IsNativeJogging => ownsNativeGait && (nativePace == NativePace.Jog || nativePace == NativePace.HurtJog)
        && (nativePhase == NativePhase.Start || nativePhase == NativePhase.Loop);
    internal bool IsNativeSprintStopping => ownsNativeGait && nativePhase == NativePhase.Stop && nativePace == NativePace.Sprint;
    private bool IsNativeCrouchRising => ownsNativeGait && !isCrouching
        && (nativeCrouching || nativePhase == NativePhase.Stance);
    internal bool IsRisingForFirearm => IsNativeCrouchRising || IsValid && !isCrouching
        && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == NativeLegacyCrouch;
    internal bool SupportsNativeCrouch => IsValid && animator.HasState(0, NativeCrouchIdleState);
    internal bool SupportsNativeInjury => IsValid && animator.HasState(0, NativeHurtIdleState);

    private string NativeLongGunCrouchPrefix
    {
        get
        {
            if (currentWeaponType == WeaponType.Rifle)
            {
                return "NativeCqbr";
            }
            return "NativeW870";
        }
    }

    internal bool SupportsNativeLongGunCrouch => IsValid && IsLongGun(currentWeaponType)
        && animator.HasState(0, Animator.StringToHash(NativeLongGunCrouchPrefix + "CrouchIdle"));

    private int ResolveNativeStanceState(NativeStance stance)
    {
        if (SupportsNativeLongGunCrouch)
        {
            return Animator.StringToHash(NativeStances[(int)stance].state.Replace("NativePistol", NativeLongGunCrouchPrefix));
        }
        return NativeStanceStates[(int)stance];
    }

    internal bool SupportsNativeCqbr => IsValid && animator.HasState(0, NativeCqbrIdleState);
    internal bool SupportsNativeKnifeGait => IsValid && animator.HasState(0, NativeKnifeIdleState);
    internal bool IsNativeCqbrCarrying => ownsNativeGait && nativeCqbrCarry;
    internal bool IsNativeW870Carrying => ownsNativeGait && SupportsNativeW870Aim;
    internal bool IsNativePistolCarrying => ownsNativeGait && currentWeaponType == WeaponType.Pistol;
    internal const float NativeCqbrRevealTime = (float)RE4.ChapterOne.ChapterOneRootActor.RifleDrawVisibleFrame / RE4.ChapterOne.ChapterOneRootActor.RifleDrawFrames;
    internal const float NativeCqbrGripTime = (float)RE4.ChapterOne.ChapterOneRootActor.RifleGripReadyFrame / RE4.ChapterOne.ChapterOneRootActor.RifleDrawFrames;

    internal bool CanUseNativeGait(bool crouching, bool injured)
    {
        if (!IsValid || traversalLandingBasePoseLocked)
        {
            return false;
        }
        if (currentWeaponType == WeaponType.Rifle)
        {
            return (!crouching || SupportsNativeLongGunCrouch) && SupportsNativeCqbr
                && (!injured || animator.HasState(0, NativeCqbrHurtIdleState));
        }
        if (currentWeaponType == WeaponType.Shotgun)
        {
            return (!crouching || SupportsNativeLongGunCrouch) && SupportsNativeW870Aim && animator.HasState(0, NativeIdleState)
                && (!injured || SupportsNativeInjury);
        }
        if (currentWeaponType == WeaponType.Knife)
        {
            return !crouching && SupportsNativeKnifeCarry && animator.HasState(0, NativeIdleState)
                && (!injured || SupportsNativeInjury);
        }
        return currentWeaponType == WeaponType.Pistol && animator.HasState(0, NativeIdleState)
            && (!crouching || SupportsNativeCrouch) && (!injured || SupportsNativeInjury);
    }

    internal static string NativeGaitStateName(string pace, string phase) => "NativePistol" + pace + phase;
    internal static string NativeCqbrStateName(string pace, string phase) => "NativeCqbr" + pace + phase;
    internal static string NativeKnifeStateName(string pace, string phase) => "NativeKnife" + pace + phase;

    internal static readonly string[] NativeIdleVariationNames =
    {
        "NativeIdleLookA", "NativeIdleLookB", "NativeIdleLookD", "NativeIdleBreath"
    };
    private static readonly int[] NativeIdleVariationStates = System.Array.ConvertAll(NativeIdleVariationNames, Animator.StringToHash);
    private float idleVariationWait;
    private int idleVariationIndex;
    private int playingIdleVariation;

    private void UpdateNativeIdleVariation()
    {
        bool canPlay = ownerController != null && ownerController.currentState == LeonState.Idle &&
            ownerController.IsGrounded && !ownerController.IsAiming && !ownerController.IsFreeLooking &&
            currentWeaponType == WeaponType.Pistol && activeFullBodyAction == FullBodyActionType.None &&
            !nativeCrouching && !nativeInjuryPoseActive;
        if (!canPlay)
        {
            idleVariationWait = 0f;
            if (playingIdleVariation != 0)
            {
                playingIdleVariation = 0;
                PlayNativeGait(NativePhase.Idle, .18f);
            }
            return;
        }

        if (playingIdleVariation != 0)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool playing = state.shortNameHash == playingIdleVariation;
            if ((playing && state.normalizedTime >= .98f) || (!playing && !animator.IsInTransition(0)))
            {
                playingIdleVariation = 0;
                PlayNativeGait(NativePhase.Idle, .22f);
            }
            return;
        }

        idleVariationWait += Time.fixedDeltaTime;
        float idleCycle = animator.GetCurrentAnimatorStateInfo(0).length;
        // 等普通待机走完一轮，再播放环顾等变体。
        if (idleVariationWait < Mathf.Max(6f, idleCycle) || animator.IsInTransition(0))
        {
            return;
        }

        idleVariationWait = 0f;
        int next = NativeIdleVariationStates[idleVariationIndex];
        idleVariationIndex = (idleVariationIndex + 1) % NativeIdleVariationStates.Length;
        if (animator.HasState(0, next))
        {
            playingIdleVariation = next;
            requestedNativeState = next;
            animator.CrossFadeInFixedTime(next, .24f, 0);
        }
    }

    private void WriteLegacyCrouchParameter()
    {
        // 原版蹲姿由基础层接管，避免旧 Any State 和兼容层再次覆盖。
        bool nativeCrouch = currentWeaponType == WeaponType.Pistol && SupportsNativeCrouch;
        nativeCrouch |= SupportsNativeLongGunCrouch;
        animator.SetBool(HashCrouch, isCrouching && !(ownsNativeGait && nativeCrouch));
    }

    private static int[,] BuildNativeGaitHashes(string prefix = "NativePistol")
    {
        int count = NativeGaits.Length;
        if (prefix == "NativeKnife")
        {
            count = (int)NativePace.Crouch;
        }
        var hashes = new int[count, 4];
        for (int index = 0; index < count; index++)
        {
            for (int phase = 1; phase <= 3; phase++)
            {
                string phaseName = ((NativePhase)phase).ToString();
                hashes[index, phase] = Animator.StringToHash(prefix + NativeGaits[index].name + phaseName);
            }
        }
        return hashes;
    }

    private static int[] BuildNativeStanceHashes()
    {
        var hashes = new int[NativeStances.Length];
        for (int index = 0; index < hashes.Length; index++)
        {
            hashes[index] = Animator.StringToHash(NativeStances[index].state);
        }
        return hashes;
    }

    internal void DriveNativeGait(Vector3 heading, float inputMagnitude, bool sprint, bool crouching, bool injured,
        NativeDirection direction, bool nativeAim, bool basicGait, bool running, bool forwardRun, float runLean,
        Vector3 facing, float planarSpeed)
    {
        holdsNativeInjuryDuringKnifeAction = false;
        bool rifle = currentWeaponType == WeaponType.Rifle;
        // 低血量时用原版伤势步态，持刀层只负责上身。
        bool knife = !injured && currentWeaponType == WeaponType.Knife && SupportsNativeKnifeGait;
        if (!ownsNativeGait)
        {
            ownsNativeGait = true;
            nativePhase = NativePhase.Idle;
            requestedNativeState = 0;
            nativeCrouching = crouching && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == NativeLegacyCrouch;
            nativeCqbrCarry = rifle;
            nativeKnifeGait = knife;
            nativeGaitWeaponType = currentWeaponType;
            WriteLegacyCrouchParameter();
        }
        bool injuryChanged = SetNativeInjuryPose(injured && !crouching);
        var phase = ReadNativeGaitState();
        nativeStanceHasInput = inputMagnitude > 0.05f;
        bool wasBasicGait = basicGaitActive;
        basicGaitActive = basicGait;
        basicRunRequested = running;
        bool weaponChanged = nativeGaitWeaponType != currentWeaponType;
        if (wasBasicGait && (!basicGait || weaponChanged))
        {
            CancelBasicGaitTransition();
            phase = ReadNativeGaitState();
        }
        if (wasBasicGait || basicGait)
        {
            basicRunLean = Mathf.MoveTowards(basicRunLean, runLean, 9f * Time.fixedDeltaTime);
            animator.SetFloat(BasicRunLeanHash, basicRunLean);
        }

        bool steerForwardArc = forwardRun && nativeStanceHasInput && previousRunForward;
        previousRunForward = forwardRun && nativeStanceHasInput;
        if (basicGait && UpdateBasicGaitTransition(heading, facing, running, steerForwardArc, planarSpeed, phase))
        {
            return;
        }

        float changeDirectionDot = Vector3.Dot(NativeGaitDirection, heading.normalized);
        if (nativeStanceHasInput)
        {
            idleVariationWait = 0f;
            playingIdleVariation = 0;
            Vector3 nextHeading = heading.normalized;
            bool blendsDirection = IsDirectionalPace(nativePace) || direction != NativeDirection.Forward || nativeAim;
            if (nativePhase != NativePhase.Idle && blendsDirection && changeDirectionDot > .15f)
            {
                nextHeading = Vector3.RotateTowards(NativeGaitDirection, nextHeading,
                    600f * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
            }
            NativeGaitDirection = nextHeading;
        }
        if (nativeCqbrCarry != rifle || nativeKnifeGait != knife || nativeGaitWeaponType != currentWeaponType)
        {
            nativeCqbrCarry = rifle;
            nativeKnifeGait = knife;
            nativeGaitWeaponType = currentWeaponType;
            if (nativePhase == NativePhase.Idle
                || nativePhase != NativePhase.Stance && nativePace is NativePace.Walk or NativePace.Jog or NativePace.Sprint or NativePace.Crouch)
            {
                if (nativePhase == NativePhase.Loop)
                {
                    phase = Mathf.Repeat(phase, GaitFor(nativePace).loopFrames / 60f);
                }
                PlayNativeGait(nativePhase, 0.16f, phase);
                return;
            }
            nativePhase = NativePhase.Idle;
            requestedNativeState = 0;
        }
        if (crouching != nativeCrouching)
        {
            BeginNativeStance(crouching, phase);
            return;
        }

        NativePace desired = NativePace.Jog;
        if (basicGait)
        {
            if (!running)
            {
                desired = DirectionalPace(direction, false, false);
            }
        }
        else if (!crouching && (direction != NativeDirection.Forward || nativeAim))
        {
            desired = DirectionalPace(direction, nativeAim, injured);
        }
        else if (crouching)
        {
            desired = NativePace.Crouch;
        }
        else if (injured)
        {
            desired = NativePace.HurtJog;
            if (!sprint && inputMagnitude <= 0.55f)
            {
                desired = NativePace.HurtWalk;
            }
        }
        else if (sprint)
        {
            desired = NativePace.Sprint;
        }
        else if (inputMagnitude <= 0.55f)
        {
            desired = NativePace.Walk;
        }
        if (nativePhase == NativePhase.Stance)
        {
            if (phase >= NativeStances[(int)nativeStance].exitFrame / 60f)
            {
                FinishNativeStance(desired);
            }
            return;
        }

        if (inputMagnitude <= 0.05f)
        {
            if (nativePhase == NativePhase.Start || nativePhase == NativePhase.Loop)
            {
                nativeResumeLoopPhase = ReadGaitLoopPhase(phase);
                PlayNativeGait(NativePhase.Stop, 0.15f);
            }
            else if (nativePhase == NativePhase.Stop && phase >= GaitFor(nativePace).stopExit / 60f)
            {
                PlayNativeGait(NativePhase.Idle, 0.16f);
            }
            else if (requestedNativeState == 0 || nativePhase == NativePhase.Idle && injuryChanged)
            {
                PlayNativeGait(NativePhase.Idle, 0.18f);
            }
            else if (nativePhase == NativePhase.Idle)
            {
                UpdateNativeIdleVariation();
            }
            return;
        }

        if (nativePhase == NativePhase.Idle)
        {
            nativePace = desired;
            PlayNativeGait(NativePhase.Start, 0.13f);
        }
        else if (nativePhase == NativePhase.Stop || nativePace != desired)
        {
            if (!basicGait && IsDirectionalPace(desired) && nativePace != desired && changeDirectionDot < .15f)
            {
                nativePace = desired;
                PlayNativeGait(NativePhase.Start, .12f);
                return;
            }

            float sourcePhase = nativeResumeLoopPhase;
            if (nativePhase != NativePhase.Stop)
            {
                sourcePhase = ReadGaitLoopPhase(phase);
            }
            float targetPhase = MapGaitLoopPhase(sourcePhase, nativePace, desired);
            float blend = 0.16f;
            if (IsDirectionalPace(nativePace) || IsDirectionalPace(desired))
            {
                blend = 0.20f;
            }

            nativePace = desired;
            PlayNativeGait(NativePhase.Loop, blend, targetPhase * GaitFor(nativePace).loopFrames / 60f);
        }
        else if (nativePhase == NativePhase.Start && phase >= GaitFor(nativePace).startExit / 60f)
        {
            PlayNativeGait(NativePhase.Loop, 0.14f, GaitFor(nativePace).loopEntry / 60f);
        }
        else if (nativePhase == NativePhase.Loop && nativePace == NativePace.Jog && wasBasicGait != basicGait)
        {
            float entry = ReadGaitLoopPhase(phase) * GaitFor(nativePace).loopFrames / 60f;
            PlayNativeGait(NativePhase.Loop, 0.14f, entry);
        }
    }

    private bool UpdateBasicGaitTransition(Vector3 heading, Vector3 facing, bool running, bool steerForwardArc,
        float planarSpeed, float phase)
    {
        if (nativePhase == NativePhase.Turn)
        {
            if (!running || !nativeStanceHasInput || Vector3.Dot(runTurnHeading, heading) < 0.76f)
            {
                CancelBasicGaitTransition();
                return false;
            }
            var turn = BasicRunTurns[runTurnIndex];
            if (phase >= turn.exitFrame / 60f)
            {
                NativeGaitDirection = heading;
                PlayNativeGait(NativePhase.Loop, 0.12f, turn.loopEntry / 60f);
            }
            return true;
        }

        if (walkReverseRemaining > 0f)
        {
            if (!running)
            {
                walkReverseRemaining = Mathf.Max(0f, walkReverseRemaining - Time.fixedDeltaTime);
                if (walkReverseRemaining > 0.0001f)
                {
                    return true;
                }
            }
            walkReverseRemaining = 0f;
            return false;
        }

        bool moving = nativePhase == NativePhase.Start || nativePhase == NativePhase.Loop;
        if (running && nativeStanceHasInput && moving && nativePace == NativePace.Jog && planarSpeed > 0.65f)
        {
            float angle = Vector3.SignedAngle(facing, heading, Vector3.up);
            // 正后方固定从右侧掉头，避免输入与浮点误差反复改变选边。
            if (Mathf.Abs(angle) >= 179.5f)
            {
                angle = 180f;
            }
            float magnitude = Mathf.Abs(angle);
            // 两个前斜输入共用慢跑循环，连续切换时只改变朝向和侧倾。
            if (!steerForwardArc && magnitude >= 72f)
            {
                int index = 0;
                if (angle >= 0f)
                {
                    index = 2;
                }
                if (magnitude >= 135f)
                {
                    index++;
                }
                int turnState = BasicRunTurnStates[BasicRunTurnRow, index];
                if (animator.HasState(0, turnState))
                {
                    runTurnIndex = index;
                    runTurnHeading = heading;
                    nativePhase = NativePhase.Turn;
                    requestedNativeState = turnState;
                    animator.CrossFadeInFixedTime(turnState, 0.08f, 0, 0f, 0f);
                    return true;
                }
            }
        }
        else if (!running && planarSpeed > 0.2f && (moving || nativePhase == NativePhase.Stop)
            && (nativePace == NativePace.Walk || IsBasicDirectionalPace(nativePace)))
        {
            bool reversing = nativeStanceHasInput && Vector3.Dot(NativeGaitDirection, heading) <= -0.707f;
            if (reversing || !nativeStanceHasInput && moving)
            {
                walkReverseRemaining = WalkReverseBrakeSeconds;
                if (moving)
                {
                    nativeResumeLoopPhase = ReadGaitLoopPhase(phase);
                    PlayNativeGait(NativePhase.Stop, WalkReverseBrakeSeconds);
                }
                return true;
            }
        }
        return false;
    }

    private static bool IsBasicDirectionalPace(NativePace pace)
    {
        return pace >= NativePace.ForwardLeft && pace <= NativePace.ForwardRight;
    }

    private bool SetNativeInjuryPose(bool active)
    {
        if (nativeInjuryPoseActive == active)
        {
            return false;
        }
        nativeInjuryPoseActive = active;
        ApplyPresentationMode(0.16f);
        return true;
    }

    private void BeginNativeStance(bool crouching, float currentTime)
    {
        NativeStance stance = NativeStance.Up;
        bool moving = nativeStanceHasInput && nativePhase is NativePhase.Start or NativePhase.Loop;
        if (crouching)
        {
            stance = NativeStance.Down;
            if (moving && nativePace is NativePace.Jog or NativePace.Sprint or NativePace.HurtJog)
            {
                stance = NativeStance.MovingDown;
            }
        }
        else if (moving)
        {
            float phase = GetCommonGaitPhase(currentTime) * 360f;
            float left = Mathf.Abs(Mathf.DeltaAngle(phase, 21f / 64f * 360f));
            float right = Mathf.Abs(Mathf.DeltaAngle(phase, 42f / 64f * 360f));
            stance = NativeStance.MovingUpLeft;
            if (right < left)
            {
                stance = NativeStance.MovingUpRight;
            }
        }

        nativeCrouching = crouching;
        nativeStance = stance;
        nativePhase = NativePhase.Stance;
        requestedNativeState = ResolveNativeStanceState(stance);
        if (SupportsNativeLongGunCrouch)
        {
            longGunCrouchLayerExitPending = false;
            BlendLongGunCrouchLayerTo(0f);
        }
        animator.CrossFadeInFixedTime(requestedNativeState, 0.18f, 0, 0f, 0f);
    }

    private void FinishNativeStance(NativePace desired)
    {
        if (!nativeStanceHasInput)
        {
            PlayNativeGait(NativePhase.Idle, 0.15f);
            return;
        }
        nativePace = desired;
        int entry = NativeStances[(int)nativeStance].loopEntry;
        if (entry < 0)
        {
            PlayNativeGait(NativePhase.Start, 0.16f);
            return;
        }
        var destination = GaitFor(desired);
        float entryTime = entry / 60f;
        if (!nativeCrouching && desired != NativePace.Jog)
        {
            var jog = GaitFor(NativePace.Jog);
            float commonPhase = entry / (float)jog.loopFrames - jog.commonPhase;
            entryTime = Mathf.Repeat(commonPhase + destination.commonPhase, 1f) * destination.loopFrames / 60f;
        }
        PlayNativeGait(NativePhase.Loop, 0.18f, entryTime);
    }

    internal void HoldNativeInjuryDuringKnifeAction()
    {
        if (holdsNativeInjuryDuringKnifeAction)
        {
            return;
        }

        // 小刀事务控制位移，保留伤势待机作为动作和收招的基础姿势。
        holdsNativeInjuryDuringKnifeAction = true;
        basicGaitActive = false;
        walkReverseRemaining = 0f;
        previousRunForward = false;
        ownsNativeGait = true;
        nativeCrouching = false;
        nativeCqbrCarry = false;
        nativeKnifeGait = false;
        NativeGaitDirection = Vector3.zero;
        WriteLegacyCrouchParameter();
        SetNativeInjuryPose(true);
        if (requestedNativeState != NativeHurtIdleState)
        {
            PlayNativeGait(NativePhase.Idle, 0.16f);
        }
    }

    internal void ResetNativeGaitToIdleImmediate()
    {
        ownsNativeGait = false;
        holdsNativeInjuryDuringKnifeAction = false;
        basicGaitActive = false;
        basicRunRequested = false;
        walkReverseRemaining = 0f;
        basicRunLean = 0f;
        previousRunForward = false;
        idleVariationWait = 0f;
        playingIdleVariation = 0;
        nativePhase = NativePhase.Idle;
        nativePace = NativePace.Walk;
        nativeResumeLoopPhase = 0f;
        nativeCrouching = false;
        nativeCqbrCarry = currentWeaponType == WeaponType.Rifle;
        nativeKnifeGait = currentWeaponType == WeaponType.Knife && SupportsNativeKnifeGait;
        nativeGaitWeaponType = currentWeaponType;
        nativeStanceHasInput = false;
        NativeGaitDirection = Vector3.zero;
        requestedNativeState = 0;
        if (!IsValid)
        {
            return;
        }

        animator.SetFloat(HashHorizontal, 0f);
        animator.SetFloat(HashVertical, 0f);
        animator.SetFloat(BasicRunLeanHash, 0f);
        WriteLegacyCrouchParameter();
        if (CanUseNativeGait(false, nativeInjuryPoseActive))
        {
            PlayNativeGait(NativePhase.Idle, 0f, 0f);
        }
        else
        {
            animator.CrossFadeInFixedTime(HashBaseIdle, 0f, 0, 0f, 0f);
        }
        animator.Update(0f);
    }

    internal void ReleaseNativeGait(bool moving, bool crouching = false)
    {
        holdsNativeInjuryDuringKnifeAction = false;
        basicGaitActive = false;
        walkReverseRemaining = 0f;
        basicRunLean = 0f;
        previousRunForward = false;
        if (!ownsNativeGait)
        {
            return;
        }
        if ((MeleeActionKinds.IsWoodenBox(activeFullBodyAction) || MeleeActionKinds.IsEnemyKick(activeFullBodyAction)) && ownerController != null
            && ownerController.currentState == LeonState.MeleeAttacking)
        {
            // 体术遮住基础层时保留当前武器的原版待机，收势可直接混回。
            if (nativePhase != NativePhase.Idle)
            {
                PlayNativeGait(NativePhase.Idle, 0.16f);
            }
            return;
        }

        ownsNativeGait = false;
        idleVariationWait = 0f;
        playingIdleVariation = 0;
        requestedNativeState = 0;
        SetNativeInjuryPose(false);
        NativeGaitDirection = Vector3.zero;
        WriteLegacyCrouchParameter();
        if (IsNativeState(animator.GetCurrentAnimatorStateInfo(0).shortNameHash)
            || animator.IsInTransition(0) && IsNativeState(animator.GetNextAnimatorStateInfo(0).shortNameHash))
        {
            int target = HashBaseIdle;
            if (crouching)
            {
                target = NativeLegacyCrouch;
            }
            else if (moving)
            {
                target = NativeLegacyMove;
            }
            animator.CrossFadeInFixedTime(target, 0.18f, 0, 0f, 0f);
        }
    }

    internal bool TryReadNativeGaitDirection(out Vector3 direction)
    {
        direction = NativeGaitDirection;
        if (MeleeActionKinds.IsWoodenBox(activeFullBodyAction) || MeleeActionKinds.IsEnemyKick(activeFullBodyAction) || holdsNativeInjuryDuringKnifeAction)
        {
            return false;
        }
        if (!ownsNativeGait || !IsNativeState(animator.GetCurrentAnimatorStateInfo(0).shortNameHash)
            && !(animator.IsInTransition(0) && IsNativeState(animator.GetNextAnimatorStateInfo(0).shortNameHash)))
        {
            return false;
        }
        if (nativePhase == NativePhase.Idle || nativePhase == NativePhase.Stance && !nativeStanceHasInput)
        {
            direction = Vector3.zero;
        }
        return true;
    }

    internal void ReadNativeFootContacts(out bool left, out bool right)
    {
        left = false;
        right = false;
        if (!ownsNativeGait || nativePhase != NativePhase.Loop)
        {
            return;
        }

        var current = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            var next = animator.GetNextAnimatorStateInfo(0);
            if (next.shortNameHash != requestedNativeState ||
                !TryGetLoopPace(current.shortNameHash, out NativePace currentPace) ||
                (!IsDirectionalPace(currentPace) && !IsDirectionalPace(nativePace)))
            {
                return;
            }

            // 只有两条循环都在支撑的脚才跨过混合保留锁定。
            ReadFootContacts(current.normalizedTime, currentPace, out bool currentLeft, out bool currentRight);
            ReadFootContacts(next.normalizedTime, nativePace, out bool nextLeft, out bool nextRight);
            left = currentLeft && nextLeft;
            right = currentRight && nextRight;
            return;
        }

        if (current.shortNameHash == requestedNativeState &&
            (IsDirectionalPace(nativePace) || Vector3.Dot(transform.forward, NativeGaitDirection) >= 0.97f))
        {
            ReadFootContacts(current.normalizedTime, nativePace, out left, out right);
        }
    }

    private static bool TryGetLoopPace(int hash, out NativePace pace)
    {
        if (System.Array.IndexOf(BasicRunLoopStates, hash) >= 0)
        {
            pace = NativePace.Jog;
            return true;
        }
        if (hash == Animator.StringToHash("NativeW870CrouchLoop"))
        {
            pace = NativePace.Crouch;
            return true;
        }
        int loop = (int)NativePhase.Loop;
        for (int index = 0; index < DirectionalGaits.Length; index++)
        {
            if (hash == DirectionalStates[index, loop])
            {
                pace = (NativePace)((int)NativePace.ForwardLeft + index);
                return true;
            }
        }
        for (int index = 0; index < NativeGaits.Length; index++)
        {
            if (hash == NativeGaitStates[index, loop] || hash == NativeCqbrStates[index, loop] ||
                (index < NativeKnifeStates.GetLength(0) && hash == NativeKnifeStates[index, loop]))
            {
                pace = (NativePace)index;
                return true;
            }
        }

        pace = default;
        return false;
    }

    private static (float left, float right, float leftDuration, float rightDuration) GaitFootContacts(NativePace pace)
    {
        if (IsDirectionalPace(pace))
        {
            var gait = DirectionalGaits[DirectionalIndex(pace)];
            float frame = 1f / gait.loopFrames;
            return (gait.leftContact * frame, gait.rightContact * frame,
                gait.contactFrames * frame, gait.contactFrames * frame);
        }

        return NativeFootPhases[(int)pace];
    }

    private static void ReadFootContacts(float phase, NativePace pace, out bool left, out bool right)
    {
        var contact = GaitFootContacts(pace);
        left = Mathf.Repeat(phase - contact.left, 1f) < contact.leftDuration;
        right = Mathf.Repeat(phase - contact.right, 1f) < contact.rightDuration;
    }

    private static float MapGaitLoopPhase(float phase, NativePace source, NativePace destination)
    {
        if (!IsDirectionalPace(source) && !IsDirectionalPace(destination))
        {
            return Mathf.Repeat(phase - GaitFor(source).commonPhase + GaitFor(destination).commonPhase, 1f);
        }

        // 按支撑脚校准相位，分别对齐左右脚落地的两个半周期。
        var from = GaitFootContacts(source);
        var to = GaitFootContacts(destination);
        float fromHalf = Mathf.Repeat(from.right - from.left, 1f);
        float toHalf = Mathf.Repeat(to.right - to.left, 1f);
        float elapsed = Mathf.Repeat(phase - from.left, 1f);
        if (elapsed < fromHalf)
        {
            return Mathf.Repeat(to.left + toHalf * (elapsed / fromHalf), 1f);
        }

        float rightElapsed = (elapsed - fromHalf) / (1f - fromHalf);
        return Mathf.Repeat(to.right + (1f - toHalf) * rightElapsed, 1f);
    }

    private void PlayNativeGait(NativePhase phase, float blend, float time = 0f)
    {
        nativePhase = phase;
        requestedNativeState = NativeIdleState;
        if (phase == NativePhase.Idle && nativeCrouching)
        {
            requestedNativeState = NativeCrouchIdleState;
            if (SupportsNativeLongGunCrouch)
            {
                requestedNativeState = Animator.StringToHash(NativeLongGunCrouchPrefix + "CrouchIdle");
            }
        }
        else if (phase == NativePhase.Idle && nativeInjuryPoseActive)
        {
            requestedNativeState = NativeHurtIdleState;
            if (nativeCqbrCarry)
            {
                requestedNativeState = NativeCqbrHurtIdleState;
            }
        }
        else if (phase == NativePhase.Idle && nativeCqbrCarry)
        {
            requestedNativeState = NativeCqbrIdleState;
        }
        else if (phase == NativePhase.Idle && nativeKnifeGait)
        {
            requestedNativeState = NativeKnifeIdleState;
        }
        else if (phase == NativePhase.Loop && nativePace == NativePace.Jog && basicGaitActive)
        {
            requestedNativeState = BasicRunLoopStates[BasicRunTurnRow];
        }
        else if (phase != NativePhase.Idle && IsDirectionalPace(nativePace))
        {
            requestedNativeState = DirectionalStates[DirectionalIndex(nativePace), (int)phase];
        }
        else if (phase != NativePhase.Idle)
        {
            var states = NativeGaitStates;
            if (nativeCqbrCarry)
            {
                states = NativeCqbrStates;
            }
            else if (nativeKnifeGait)
            {
                states = NativeKnifeStates;
            }
            requestedNativeState = states[(int)nativePace, (int)phase];
        }
        if (phase != NativePhase.Idle && nativePace == NativePace.Crouch && SupportsNativeLongGunCrouch)
        {
            requestedNativeState = Animator.StringToHash(NativeLongGunCrouchPrefix + "Crouch" + phase);
        }
        animator.CrossFadeInFixedTime(requestedNativeState, blend, 0, time, 0f);
    }

    private float ReadNativeGaitState()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).shortNameHash == requestedNativeState)
        {
            state = animator.GetNextAnimatorStateInfo(0);
        }
        if (state.shortNameHash != requestedNativeState)
        {
            return 0f;
        }
        return state.normalizedTime * state.length;
    }

    private float GetCommonGaitPhase(float time)
    {
        return Mathf.Repeat(ReadGaitLoopPhase(time) - GaitFor(nativePace).commonPhase, 1f);
    }

    private float ReadGaitLoopPhase(float time)
    {
        var spec = GaitFor(nativePace);
        float frame = time * 60f;
        if (nativePhase == NativePhase.Start)
        {
            frame += spec.loopEntry - spec.startExit;
        }
        return Mathf.Repeat(frame / spec.loopFrames, 1f);
    }

    private static bool IsNativeState(int hash)
    {
        if (System.Array.IndexOf(NativeLongGunCrouchStates, hash) >= 0)
        {
            return true;
        }
        if (System.Array.IndexOf(BasicRunLoopStates, hash) >= 0)
        {
            return true;
        }
        if (hash == NativeIdleState || hash == NativeCrouchIdleState || hash == NativeHurtIdleState
            || hash == NativeCqbrIdleState || hash == NativeCqbrHurtIdleState || hash == NativeKnifeIdleState ||
            IsDirectionalState(hash) || System.Array.IndexOf(NativeIdleVariationStates, hash) >= 0)
        {
            return true;
        }
        foreach (int turn in BasicRunTurnStates)
        {
            if (turn == hash)
            {
                return true;
            }
        }
        foreach (int state in NativeStanceStates)
        {
            if (hash == state)
            {
                return true;
            }
        }
        foreach (int state in NativeGaitStates)
        {
            if (state != 0 && state == hash)
            {
                return true;
            }
        }
        foreach (int state in NativeCqbrStates)
        {
            if (state != 0 && state == hash)
            {
                return true;
            }
        }
        foreach (int state in NativeKnifeStates)
        {
            if (state != 0 && state == hash)
            {
                return true;
            }
        }
        return false;
    }

    private static readonly int[] NativeLongGunCrouchStates = BuildLongGunCrouchHashes();

    private static int[] BuildLongGunCrouchHashes()
    {
        string[] prefixes = { "NativeCqbr", "NativeW870" };
        string[] suffixes = { "CrouchIdle", "CrouchStart", "CrouchLoop", "CrouchStop",
            "CrouchDown", "CrouchUp", "CrouchMovingDown", "CrouchMovingUpLeft", "CrouchMovingUpRight" };
        var hashes = new int[prefixes.Length * suffixes.Length];
        int index = 0;
        foreach (string prefix in prefixes)
        {
            foreach (string suffix in suffixes)
            {
                hashes[index++] = Animator.StringToHash(prefix + suffix);
            }
        }
        return hashes;
    }
}

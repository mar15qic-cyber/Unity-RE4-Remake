using System;
using System.Collections.Generic;
using UnityEngine;

namespace RE4.ChapterOne
{
    [RequireComponent(typeof(Animator), typeof(Rigidbody), typeof(CapsuleCollider))]
    [DefaultExecutionOrder(50)]
    public sealed partial class ChapterOneRootActor : MonoBehaviour
    {
        internal const string WeaponLayerName = "Firearm Upper Body";
        internal const string WeaponArmsLayerName = "Weapon Arms";
        internal const string WeaponRateName = "WeaponDurationScale";
        internal const int RifleDrawVisibleFrame = 12;
        internal const int RifleGripReadyFrame = 45;
        internal const int RifleDrawFrames = 60;
        private const float RifleDrawBlend = 0.18f;
        private const int RifleReloadBoltReleaseFrame = 182;
        private const float StaggerLocomotionBlend = 0.24f;
        private const float StaggerUpperRelease = 0.32f;
        [Serializable]
        public sealed class Take
        {
            public string id;
            public string label;
            public AnimationClip body;
            public AnimationClip mechanism;
            public string weapon;
            public string partnerId;
            public int[] contactFrames;
            public Vector3 pairOrigin;
            public bool loop;
            public bool verticalTravel;
            [NonSerialized] public int state;
            [NonSerialized] public int weaponState;
            [NonSerialized] public int weaponArmsState;
        }

        [Serializable]
        public sealed class Equipment
        {
            public string id;
            public Transform model;
            public Transform muzzle;
            [SerializeField] private bool knifeMountFitted;
            private Transform[] joints;
            private Vector3[] positions;
            private Quaternion[] rotations;
            private Vector3[] scales;

            public void Cache()
            {
                FitKnifeGrip();
                joints = model.GetComponentsInChildren<Transform>(true);
                positions = new Vector3[joints.Length];
                rotations = new Quaternion[joints.Length];
                scales = new Vector3[joints.Length];
                for (int i = 0; i < joints.Length; i++)
                {
                    positions[i] = joints[i].localPosition;
                    rotations[i] = joints[i].localRotation;
                    scales[i] = joints[i].localScale;
                }
            }

            internal void FitKnifeGrip(bool refresh = false)
            {
                if (id != "wp5000" || knifeMountFitted && !refresh)
                {
                    return;
                }
                // 老验收场景中的角色副本在首次缓存时补上护手校准。
                model.localPosition += model.localRotation * Vector3.Scale(model.localScale, Vector3.forward * 0.036f);
                knifeMountFitted = true;
            }

            public void ResetMechanism()
            {
                if (joints == null)
                {
                    return;
                }
                for (int i = 0; i < joints.Length; i++)
                {
                    if (joints[i] != null)
                    {
                        joints[i].SetLocalPositionAndRotation(positions[i], rotations[i]);
                        joints[i].localScale = scales[i];
                    }
                }
            }
        }

        [SerializeField] private string actorName;
        [SerializeField] private Take[] takes;
        [SerializeField] private Equipment[] equipment;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        [SerializeField] private Transform rightHand;
        [SerializeField] private LayerMask worldMask;
        [SerializeField] private LeonActionMotionDriver playerMotion;
        private readonly Dictionary<string, Take> byId = new();
        private Animator animator;
        private Rigidbody body;
        private CapsuleCollider capsule;
        private Take take;
        private Take movementTake;
        private Equipment held;
        private string equippedId;
        private int mode;
        private int markerCursor;
        private float actionTime;
        private float movementTime;
        private float playbackLength;
        private float blockedTime;
        private float speed;
        private Vector2 direction;
        private Vector3 desiredForward;
        private Vector3 previousPosition;
        private Vector3 planarVelocity;
        private float facingVelocity;
        private bool terminal;
        private bool paired;
        private bool releasePairedTail;
        private ChapterOnePairMotion.Track pairTrack;
        private Pose pairStageStart;
        private Vector3 pairEntryOffset;
        private Quaternion pairEntryRotation;
        private float pairBlend;
        private float pairEntryTime;
        private bool finished;
        private bool obstructed;
        private Vector3 supportAnchor;
        private ChapterOneFootPlant leftPlant;
        private ChapterOneFootPlant rightPlant;
        private ChapterOneFootPlant supportPlant;
        private bool supportHoldsHeight;
        private float footWeight;
        private float footTime;
        private int actionId;
        private int locomotionState;
        private float movementEntryPhase;
        private float staggerEntryPhase;
        private float staggerLocomotionBlendRemaining;
        private int staggerReleaseLayer;
        private float staggerReleaseWeight;
        private float staggerReleaseTime;
        private Vector3 movementTurnHeading;
        private Vector2 movementAxis;
        private int weaponLayer;
        private int weaponArmsLayer;
        private int actionLayer;
        private bool upperAction;
        private bool prepareShot;
        private bool reversingAimTransition;
        private float prepareShotUntil;
        private float weaponRate;
        private string weaponPose;
        private bool weaponPoseAimed;
        private int weaponPoseState;
        private int weaponArmsPoseState;
        private int activePoseState;
        private Transform weaponSpine;
        private Transform rifleShoulders;
        private Transform adjustedAimJoint;
        private Quaternion animatedAimRotation;
        private string aimReferenceWeapon;
        private Quaternion aimReferenceInActor;
        private float aimCorrectionWeight;
        private float rifleAimWeight;
        private Transform rifleBolt;
        private Vector3 rifleBoltOpenPosition;
        private bool rifleBoltOpen;
        private bool rifleReloadStartedOpen;
        private static readonly Vector3 RifleAimInShoulders = new(-0.6766877f, 0.2674094f, 0.6859927f);
        private static readonly Vector3 RifleAimInActor = new(-0.0061247f, -0.0181871f, 0.9998158f);
        // 角色与枪机共用这段源帧映射，第 1 帧开火，后坐与枪机循环在同一发内完成。
        private static readonly AnimationCurve RifleRecoilClock = new(
            new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 0f, 0f, 0f),
            new Keyframe(10f, 2f, 0.27f, 0.27f), new Keyframe(16f, 4f, 0.4f, 0.4f),
            new Keyframe(25f, 8.3f, 0.31f, 0.31f), new Keyframe(30f, 9f, 0f, 0f));
        private static readonly int Pace = Animator.StringToHash("Pace");
        private static readonly int MoveX = Animator.StringToHash("MoveX");
        private static readonly int MoveY = Animator.StringToHash("MoveY");
        private static readonly int RifleReload = Animator.StringToHash("CQBR_0810");
        private static readonly int CrouchedRifleReload = Animator.StringToHash("LeonFireExtra_1810_wp4402");
        private static readonly int DurationScale = Animator.StringToHash("DurationScale");
        private static readonly int WeaponDurationScale = Animator.StringToHash(WeaponRateName);

        public event Action<ChapterOneRootActor, Take, int, int> Contact;
        public event Action<ChapterOneRootActor, Take, int> Completed;
        public string ActorName => actorName;
        public Take ActiveTake => take ?? movementTake;
        public Take MovementTake => movementTake;
        public bool IsBusy => ActiveTake != null;
        public bool BlocksLocomotion => take != null && !upperAction;
        internal bool IsCrouching => mode == 1;
        internal bool RifleCarryReady => weaponArmsLayer > 0 && weaponArmsPoseState != 0 && !weaponPoseAimed
            && animator.GetLayerWeight(weaponArmsLayer) >= 0.999f && !animator.IsInTransition(weaponArmsLayer)
            && animator.GetCurrentAnimatorStateInfo(weaponArmsLayer).fullPathHash == weaponArmsPoseState;
        internal bool IsRifleRaisingForShot => take != null && take.id is "CQBR_0140" or "CQBR_0158";
        internal bool RifleDrawGripReady => take != null && take.id == "CQBR_0543"
            && actionTime * take.body.frameRate >= RifleGripReadyFrame;
        internal bool IsRifleReloadRecovering => !upperAction && actionLayer > 0
            && equippedId == "wp4402" && animator.GetCurrentAnimatorStateInfo(actionLayer).shortNameHash == RifleReload;

        private bool IsStanceChange => take != null && take.id is "LeonMove_0169" or "LeonMove_1169"
            || movementTake != null && IsMovingStance(movementTake.id);
        public bool IsTerminal => terminal;
        public bool IsPaired => paired;
        internal bool HasValidStageTrack => take != null && pairTrack != null && pairTrack.Matches(take.body);
        private bool HasHeldModel => held != null && held.model != null;
        internal bool PairedFinished => paired && finished;
        internal float PairMotionError { get; private set; }
        public float ActionTime => take != null ? actionTime : movementTime;
        public int ActionId => actionId;
        public Vector3 RequestedStep { get; private set; }
        public Vector3 ActualStep { get; private set; }
        public float FootDrift { get; private set; }
        public string SupportFoot { get; private set; }
        public Transform Muzzle => held?.muzzle;
        public Transform RightHand => rightHand;
        public Vector3 MotionPosition => body.position;
        public Quaternion MotionRotation => body.rotation;
        public IEnumerable<Take> Takes => takes;
        public string StateName => ActiveTake?.label ?? (speed > 0.01f ? "移动" : "待机");

#if UNITY_EDITOR
        public void Configure(string label, Take[] motions, Equipment[] items, Transform left, Transform right, Transform hand,
            LayerMask world, LeonActionMotionDriver motor)
        {
            actorName = label;
            takes = motions;
            equipment = items;
            leftFoot = left;
            rightFoot = right;
            rightHand = hand;
            worldMask = world;
            playerMotion = motor;
        }
#endif

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.enabled = true;
            animator.applyRootMotion = true;
            weaponLayer = animator.GetLayerIndex(WeaponLayerName);
            weaponArmsLayer = animator.GetLayerIndex(WeaponArmsLayerName);
            if (weaponLayer > 0)
            {
                weaponSpine = transform.Find("Armature/root ground/root hips/spine lower");
                if (weaponSpine != null)
                {
                    rifleShoulders = weaponSpine.Find("spine middle/spine upper");
                }
            }
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            leftPlant = CreateFootPlant(leftFoot);
            rightPlant = CreateFootPlant(rightFoot);
            foreach (Take item in takes)
            {
                if (KnifeHitWindow(item.id).first >= 0)
                {
                    item.contactFrames = Array.Empty<int>();
                }
                int frame = MeleeContactFrame(item.id);
                if (frame >= 0 && (item.contactFrames.Length == 0 || IsBodyMelee(item.id)))
                {
                    item.contactFrames = new[] { frame };
                }
                RegisterTake(item);
            }
            if (weaponLayer > 0)
            {
                RegisterPickupTakes();
                RegisterKnifeTails();
                if (byId.TryGetValue("CQBR_0543", out var rifleDraw))
                {
                    var playback = Array.Find(animator.runtimeAnimatorController.animationClips,
                        clip => clip.name == "CQBR_0543_DrawTiming");
                    if (playback != null)
                    {
                        rifleDraw.body = playback;
                    }
                }
                RegisterGeneratedTake("Knife_Draw", "拔刀", "wp5000");
                RegisterGeneratedTake("Knife_CrouchDraw", "蹲姿拔刀", "wp5000");
                RegisterGeneratedTake("CQBR_0551", "收起 CQBR", "wp4402");
                RegisterGeneratedTake("SG09R_0541", "手枪短拔枪", "wp4000");
                RegisterGeneratedTake("SG09R_1541", "蹲姿拔手枪", "wp4000");
                RegisterGeneratedTake("SG09R_1551", "蹲姿收手枪", "wp4000");
                RegisterGeneratedTake("W870_0541", "霰弹枪短拔枪", "wp4100");
                RegisterGeneratedTake("W870_1541", "蹲姿拔霰弹枪", "wp4100");
                RegisterGeneratedTake("W870_1551", "蹲姿收霰弹枪", "wp4100");
                RegisterGeneratedTake("Knife_1551", "蹲姿收刀", "wp5000");
                RegisterGeneratedTake("LeonMove_0530", "慢跑左转 90°", "");
                RegisterGeneratedTake("LeonMove_0532", "慢跑左转 180°", "");
                RegisterGeneratedTake("LeonMove_0534", "慢跑右转 90°", "");
                RegisterGeneratedTake("LeonMove_0536", "慢跑右转 180°", "");
                RegisterGeneratedTake("LeonIdleTurn_0170", "站姿左转 180°", "");
                RegisterGeneratedTake("LeonIdleTurn_0171", "站姿右转 180°", "");
                RegisterGeneratedTake("LeonDamage_0110", "左侧中受击", "");
                RegisterGeneratedTake("LeonDamage_0112", "右侧中受击", "");
                foreach (string id in DirectionalEdgeIds())
                {
                    RegisterGeneratedTake(id, "移动过渡", "");
                }
            }
            else
            {
                RegisterEnemyMovement();
                RegisterMeleeReactions();
                RegisterEnemyWindows();
                RegisterCrawlCombat();
                foreach (var (id, label) in EnemyGroundClips)
                {
                    RegisterGeneratedTake(id, label, "");
                }
            }
            CacheMeleeJoints(weaponLayer > 0);
            RegisterGroundFinishes(weaponLayer > 0);
            RegisterLegBite(weaponLayer > 0);

            foreach (Equipment item in equipment)
            {
                if (item == null || item.model == null)
                {
                    continue;
                }
                item.Cache();
                item.model.gameObject.SetActive(false);
            }
            CacheRifleBoltPose();

            previousPosition = body.position;
            locomotionState = Animator.StringToHash("Base Layer.Move0");
        }

        private void RegisterTake(Take item)
        {
            item.state = Animator.StringToHash("Base Layer." + item.id);
            item.weaponState = Animator.StringToHash(WeaponLayerName + "." + item.id);
            item.weaponArmsState = Animator.StringToHash(WeaponArmsLayerName + "." + item.id);
            byId.Add(item.id, item);
        }

        private void RegisterGeneratedTake(string id, string label, string weapon)
        {
            var clip = Array.Find(animator.runtimeAnimatorController.animationClips, item => item.name == id);
            if (clip != null && !byId.ContainsKey(id))
                RegisterTake(new Take { id = id, label = label, body = clip, weapon = weapon, contactFrames = Array.Empty<int>() });
        }

        private void Start()
        {
            animator.applyRootMotion = true;
        }

        private static ChapterOneFootPlant CreateFootPlant(Transform ankle)
        {
            if (ankle == null || ankle.parent == null || ankle.parent.parent == null || ankle.parent.parent.parent == null)
                return null;
            var toe = Array.Find(ankle.GetComponentsInChildren<Transform>(true), joint =>
                joint.name.IndexOf("toe", StringComparison.OrdinalIgnoreCase) >= 0 &&
                joint.name.IndexOf("end", StringComparison.OrdinalIgnoreCase) < 0);
            return new ChapterOneFootPlant(ankle, toe != null ? toe : ankle);
        }

        public bool TryGetTake(string id, out Take result) => byId.TryGetValue(id, out result);

        internal static bool IsBodyMelee(string id) =>
            id is "LeonInteract_0530" or "LeonInteract_0531" or "LeonInteract_0800" or "LeonInteract_0800_wp4000m";

        internal static int MeleeContactFrame(string id) => id switch
        {
            "LeonInteract_0530" or "LeonInteract_0531" or "LeonInteract_0800" => 27,
            "LeonInteract_0800_wp4000m" => 29,
            _ => -1
        };

        private void RegisterMeleeReactions()
        {
            RegisterGeneratedTake("Ganado_1015", "站姿击飞", "");
            RegisterGeneratedTake("Ganado_1016", "站姿击飞落地", "");
            RegisterGeneratedTake("Ganado_1100", "跪姿击飞", "");
            RegisterGeneratedTake("Ganado_1101", "跪姿击飞落地", "");
            RegisterGeneratedTake("Ganado_1105", "跪姿击飞 B", "");
            RegisterGeneratedTake("Ganado_1106", "跪姿击飞落地 B", "");
            RegisterGeneratedTake("Ganado_3011", "仰面倒地保持", "");
            RegisterGeneratedTake("Ganado_1410", "撞墙跪倒", "");
            foreach (string id in new[] { "Ganado_1015", "Ganado_1100", "Ganado_1105" })
            {
                if (byId.TryGetValue(id, out var launch))
                {
                    launch.verticalTravel = true;
                }
            }
        }

        public void Equip(string id)
        {
            if (HasHeldModel)
            {
                held.model.gameObject.SetActive(false);
            }
            held = FindEquipment(id);
            equippedId = id;
            ResetAndShowEquipment();
        }

        internal Equipment FindEquipment(string id) => Array.Find(equipment, item => item != null && item.id == id);

        private void CacheRifleBoltPose()
        {
            var rifle = FindEquipment("wp4402");
            if (rifle == null || rifle.model == null || !byId.TryGetValue("CQBR_0500_wp4202h", out var lastRound) || lastRound.mechanism == null)
            {
                return;
            }
            rifleBolt = rifle.model.Find("wp4402_Skeleton/root/_00/_01");
            if (rifleBolt != null)
            {
                lastRound.mechanism.SampleAnimation(rifle.model.gameObject, lastRound.mechanism.length);
                rifleBoltOpenPosition = rifleBolt.localPosition;
                rifle.ResetMechanism();
            }
        }

        internal void SetRifleBoltOpen(bool open)
        {
            rifleBoltOpen = open;
        }

        private void ApplyRifleBoltOpenPose()
        {
            if (held.id == "wp4402" && rifleBolt != null)
            {
                rifleBolt.localPosition = rifleBoltOpenPosition;
            }
        }

        private void ResetAndShowEquipment()
        {
            if (!HasHeldModel)
            {
                held = null;
                return;
            }
            held.ResetMechanism();
            if (rifleBoltOpen)
            {
                ApplyRifleBoltOpenPose();
            }
            held.model.gameObject.SetActive(true);
        }

        public void DropEquipment()
        {
            if (!HasHeldModel)
                return;
            Vector3 worldScale = held.model.lossyScale;
            Transform dropped = Instantiate(held.model, held.model.position, held.model.rotation);
            dropped.localScale = worldScale;
            dropped.gameObject.SetActive(true);
            dropped.name = "脱手 " + held.id;
            var collider = dropped.gameObject.AddComponent<SphereCollider>();
            collider.radius = 0.14f / Mathf.Max(0.001f, Mathf.Abs(dropped.lossyScale.x));
            dropped.gameObject.AddComponent<Rigidbody>();
            Destroy(dropped.gameObject, 15f);
            Equip(null);
        }

        public void Move(Vector3 heading, Vector2 localDirection, float pace, int stance)
        {
            desiredForward = heading;
            direction = localDirection;
            speed = Mathf.Clamp(pace, 0f, 3f);
            mode = stance;
        }

        public bool Play(string id, bool force = false, float duration = 0f, bool isPaired = false, float pairedFade = 0.16f,
            float sourceTime = 0f, float actionFade = 0f)
        {
            if (terminal || (!force && IsBusy) || !TryGetTake(id, out var next) || next.body == null)
                return false;
            if (IsMovementTransition(id) && !isPaired)
                return PlayMovement(next);

            staggerReleaseLayer = 0;
            ReleaseTraversalSill();
            traversalEndTime = 0f;
            vaultExitRemaining = 0f;
            bool wasBlockingLocomotion = BlocksLocomotion;
            string previousId = null;
            if (take != null)
            {
                previousId = take.id;
            }
            if (IsLeonStagger(id) && !wasBlockingLocomotion)
            {
                staggerEntryPhase = GetMovementCyclePhase();
            }
            bool alignShot = IsFirearmShot(id) && (!weaponPoseAimed || IsReloadAction(take?.id) || wasBlockingLocomotion
                || IsRifleReloadRecovering
                || activePoseState != 0 && activePoseState != GetWeaponPoseHash(GetWeaponPoseLayer()));
            upperAction = weaponLayer > 0 && !isPaired && IsWeaponLayerAction(id);
            actionLayer = 0;
            if (upperAction)
            {
                actionLayer = weaponLayer;
                bool rifleLayer = equippedId == "wp4402";
                if (weaponArmsLayer > 0 && (rifleLayer || mode == 1 || IsEquipmentAction(id)))
                {
                    actionLayer = weaponArmsLayer;
                }
            }
            if (!upperAction)
            {
                movementTake = null;
                movementTime = 0f;
            }
            take = next;
            rifleReloadStartedOpen = rifleBoltOpen && (id is "CQBR_0810" or "LeonFireExtra_1810_wp4402");
            if (upperAction && wasBlockingLocomotion)
            {
                if (IsLeonStagger(previousId))
                {
                    EnterStaggerLocomotion(previousId);
                }
                else if (IsKnifeTail(id))
                {
                    EnterLocomotion(true, PrepareKnifeLocomotion(id));
                }
                else
                {
                    EnterLocomotion(true);
                }
            }
            paired = isPaired;
            reversingAimTransition = false;
            releasePairedTail = false;
            pairTrack = null;
            PairMotionError = 0f;
            finished = false;
            playbackLength = duration > 0f ? duration : next.body.length;
            actionTime = Mathf.Clamp(sourceTime, 0f, next.body.length);
            markerCursor = 0;
            if (actionTime > 0f)
            {
                float frame = actionTime * next.body.frameRate;
                while (markerCursor < next.contactFrames.Length && next.contactFrames[markerCursor] < frame)
                {
                    markerCursor++;
                }
            }
            prepareShot = false;
            actionId++;
            blockedTime = 0f;
            // 收枪动作可共用，切换模型由装备事务在动作结束时提交。
            if (!IsHolster(id) && !string.IsNullOrEmpty(next.weapon) && held?.id != next.weapon)
            {
                if (HasHeldModel)
                {
                    held.model.gameObject.SetActive(false);
                }
                held = FindEquipment(next.weapon);
                ResetAndShowEquipment();
            }
            UpdateEquipmentVisibility(next);

            float rate = next.body.length / Mathf.Max(0.01f, playbackLength);
            int layer = actionLayer;
            int hash = GetActionHash(next);
            if (upperAction)
            {
                weaponRate = rate;
                activePoseState = 0;
                prepareShot = IsFirearmShot(id) && (alignShot || animator.GetLayerWeight(layer) < 0.92f);
                prepareShotUntil = alignShot ? Time.time + 0.12f : Time.time;
                animator.SetFloat(WeaponDurationScale, prepareShot ? 0f : rate);
            }
            else
            {
                animator.SetFloat(DurationScale, rate);
                if (isPaired && next.id != "LeonDoors_0007")
                {
                    ResetWeaponLayers();
                }
            }
            if (upperAction && animator.GetLayerWeight(layer) <= 0.01f
                || animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == hash)
            {
                animator.Play(hash, layer, actionTime / Mathf.Max(0.01f, next.body.length));
            }
            else
            {
                float blend = 0.12f;
                if (id == "CQBR_0543")
                {
                    blend = RifleDrawBlend;
                }
                else if (id == "Knife_Draw")
                {
                    blend = 0.18f;
                }
                else if (IsSteeredTransition(id))
                {
                    blend = 0.14f;
                }
                if (next.contactFrames.Length > 0 && !alignShot)
                    blend = Mathf.Min(blend, Mathf.Max(0.035f, next.contactFrames[0] / next.body.frameRate * 0.6f));
                float fade = alignShot ? blend : Mathf.Min(blend, playbackLength * 0.4f);
                if (isPaired)
                {
                    fade = pairedFade;
                }
                else if (actionFade > 0f)
                {
                    fade = actionFade;
                }
                animator.CrossFadeInFixedTime(hash, fade, layer, actionTime, 0f);
            }
            return true;
        }

        internal bool ContinueAimTransition(bool aiming)
        {
            if (!upperAction || finished || take == null
                || take.id is not ("CQBR_0140" or "CQBR_0158" or "Knife_0140" or "Knife_0158"))
            {
                return false;
            }
            // 反向时沿用当前片段和时钟，避免从另一个端点重播。
            reversingAimTransition = (take.id is "CQBR_0140" or "Knife_0140") != aiming;
            float rate = weaponRate;
            if (reversingAimTransition)
            {
                rate = -rate;
            }
            animator.SetFloat(WeaponDurationScale, rate);
            return true;
        }

        internal bool PlayPaired(Take action, ChapterOnePairMotion.Track motion, Pose start, float blend, float sourceTime = 0f)
        {
            if (motion == null || !motion.Matches(action.body) || !animator.HasState(0, action.state)
                || !Play(action.id, true, 0f, true, blend, sourceTime))
            {
                return false;
            }
            SetStageTrack(motion, start, blend, sourceTime);
            return true;
        }

        private void SetStageTrack(ChapterOnePairMotion.Track motion, Pose start, float blend, float sourceTime = 0f)
        {
            pairTrack = motion;
            pairStageStart = start;
            Pose initial = motion.Sample(sourceTime);
            pairEntryOffset = body.position - (start.position + start.rotation * initial.position);
            pairEntryRotation = body.rotation * Quaternion.Inverse(start.rotation * initial.rotation);
            pairBlend = blend;
            pairEntryTime = sourceTime;
        }

        internal void ReleasePairAfterTail()
        {
            if (finished)
            {
                Cancel();
                return;
            }
            releasePairedTail = true;
        }

        public void Cancel()
        {
            if (terminal)
                return;
            ClearAction();
            CancelMovement();
        }

        private void ClearAction()
        {
            if (take == null && !paired && pairTrack == null && !IsTraversing)
                return;
            bool wasUpper = upperAction;
            bool wasPaired = paired;
            bool wasTraversal = traversalEndTime > 0f;
            string previousId = null;
            if (take != null)
            {
                previousId = take.id;
            }
            if (wasUpper && IsLeonStagger(previousId))
            {
                staggerReleaseLayer = actionLayer;
                staggerReleaseWeight = animator.GetLayerWeight(actionLayer);
                staggerReleaseTime = 0f;
            }
            float exitPhase = 0f;
            if (wasTraversal && HasValidStageTrack)
            {
                exitPhase = PrepareVaultExit();
            }
            take = null;
            rifleReloadStartedOpen = false;
            upperAction = false;
            prepareShot = false;
            reversingAimTransition = false;
            paired = false;
            releasePairedTail = false;
            pairTrack = null;
            PairMotionError = 0f;
            finished = false;
            actionId++;
            ReleaseTraversalSill();
            traversalEndTime = 0f;
            if (wasPaired)
            {
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                planarVelocity = Vector3.zero;
                facingVelocity = 0f;
                RequestedStep = Vector3.zero;
            }
            if (wasTraversal)
            {
                body.useGravity = true;
                planarVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            }
            Equip(equippedId);
            if (!wasUpper)
            {
                if (IsLeonStagger(previousId))
                {
                    EnterStaggerLocomotion(previousId);
                }
                else if (PickupContactFrame(previousId) >= 0)
                {
                    EnterLocomotion(true, exitPhase, 0.22f);
                }
                else
                {
                    EnterLocomotion(true, exitPhase);
                }
            }
        }

        private bool PlayMovement(Take next)
        {
            if (BlocksLocomotion)
            {
                return false;
            }
            if (IsMovementStop(next.id))
            {
                movementEntryPhase = GetMovementCyclePhase();
            }
            movementTake = next;
            movementTime = 0f;
            movementTurnHeading = desiredForward;
            movementAxis = direction;
            animator.SetFloat(DurationScale, 1f);
            float fade = 0.14f;
            if (IsEnemyGaitEdge(next.id))
            {
                fade = 0.19f;
            }
            animator.CrossFadeInFixedTime(next.state, fade, 0, 0f, 0f);
            return true;
        }

        public bool TryTurnWhileRunning()
        {
            if (BlocksLocomotion || movementTake != null || desiredForward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            float angle = Vector3.SignedAngle(body.rotation * Vector3.forward, desiredForward, Vector3.up);
            if (Mathf.Abs(angle) < 72f)
            {
                return false;
            }
            string id;
            if (angle < 0f)
            {
                id = "LeonMove_0530";
                if (angle < -135f)
                {
                    id = "LeonMove_0532";
                }
            }
            else
            {
                id = "LeonMove_0534";
                if (angle > 135f)
                {
                    id = "LeonMove_0536";
                }
            }
            return Play(id, true);
        }

        public bool MovementTurnMatchesHeading()
        {
            return desiredForward.sqrMagnitude > 0.01f && Vector3.Angle(movementTurnHeading, desiredForward) <= 40f;
        }

        internal bool MovementMatchesAxis()
        {
            return Vector2.Dot(movementAxis, direction) >= 0.8f;
        }

        internal void PlayMovingStance(bool crouching)
        {
            if (crouching)
            {
                // 0545 的根轨道包含回身，前向转蹲使用 0543。
                Play("LeonMove_0543", true);
                return;
            }
            float phase = GetMovementCyclePhase();
            float firstDistance = Mathf.Abs(Mathf.DeltaAngle(phase * 360f, 21f / 64f * 360f));
            float secondDistance = Mathf.Abs(Mathf.DeltaAngle(phase * 360f, 42f / 64f * 360f));
            if (secondDistance < firstDistance)
            {
                Play("LeonMove_1510", true);
            }
            else
            {
                Play("LeonMove_1500", true);
            }
        }

        public void CancelMovement()
        {
            if (movementTake == null)
            {
                return;
            }
            float phase = 0f;
            if (speed > 0.01f)
            {
                phase = GetMovementCyclePhase();
            }
            else if (IsWalkStop(movementTake.id) || IsEnemyGaitStop(movementTake.id))
            {
                var (_, targetFrame, cycleFrames) = MovementHandoff(movementTake.id);
                if (cycleFrames > 0)
                {
                    phase = targetFrame / (float)cycleFrames;
                }
            }
            movementTake = null;
            movementTime = 0f;
            EnterLocomotion(true, phase);
        }

        private float GetMovementCyclePhase()
        {
            if (movementTake == null)
            {
                if (TryGetActionState(locomotionState, 0, out var state))
                {
                    return Mathf.Repeat(state.normalizedTime, 1f);
                }
                return 0f;
            }
            if (IsMovementStop(movementTake.id))
            {
                return movementEntryPhase;
            }
            var (frame, targetFrame, cycleFrames) = MovementHandoff(movementTake.id);
            if (cycleFrames <= 0)
            {
                return 0f;
            }
            float sourceFrame = movementTime * movementTake.body.frameRate;
            return Mathf.Repeat((targetFrame + sourceFrame - frame) / cycleFrames, 1f);
        }

        public void SetWeaponPose(string id, bool aimed)
        {
            weaponPoseAimed = aimed;
            if (weaponPose == id)
            {
                return;
            }
            weaponPose = id;
            weaponPoseState = 0;
            weaponArmsPoseState = 0;
            if (id != null)
            {
                weaponPoseState = Animator.StringToHash(WeaponLayerName + "." + id);
                weaponArmsPoseState = Animator.StringToHash(WeaponArmsLayerName + "." + id);
            }
        }

        internal static bool IsFirearmShot(string id) =>
            id.StartsWith("SG09R_0500", StringComparison.Ordinal)
            || id.StartsWith("W870_0500", StringComparison.Ordinal)
            || id.StartsWith("CQBR_0500", StringComparison.Ordinal);

        internal static bool IsLeonStagger(string id) => LeonStaggerFrames(id).end > 0;

        // 移动解锁和动作收尾取自原版的 Walk/Run 解锁、转上身和条件结束轨道。
        private static (int move, int end) LeonStaggerFrames(string id) => id switch
        {
            "LeonDamage_0100" => (46, 68),
            "LeonDamage_0102" => (46, 77),
            "LeonDamage_0104" => (42, 85),
            "LeonDamage_0106" => (42, 75),
            "LeonDamage_0110" => (71, 77),
            "LeonDamage_0112" => (71, 80),
            "LeonDamage_0114" => (71, 81),
            "LeonDamage_0116" => (71, 71),
            _ => (0, 0)
        };

        internal static float RifleRecoilSourceFrame(float frame) => RifleRecoilClock.Evaluate(frame);

        internal static bool IsHolster(string id) => id is "SG09R_0551" or "W870_0551" or "CQBR_0551" or "Knife_0551"
            or "SG09R_1551" or "W870_1551" or "Knife_1551";

        private static bool IsEquipmentAction(string id) => IsHolster(id)
            || id is "SG09R_0541" or "W870_0541" or "SG09R_1541" or "W870_1541"
                or "SG09R_0543" or "W870_0543" or "CQBR_0543" or "Knife_Draw" or "Knife_CrouchDraw";

        private static bool IsReloadAction(string id) =>
            id is "SG09R_0810" or "SG09R_0910" or "CQBR_0810"
                or "W870_0805" or "W870_0810" or "W870_0815" or "W870_0817"
                or "LeonFireExtra_1810_wp4000" or "LeonFireExtra_1910_wp4000" or "LeonFireExtra_1810_wp4402"
                or "LeonFireExtra_1805_wp4100" or "LeonFireExtra_1810_wp4100"
                or "LeonFireExtra_1815_wp4100" or "LeonFireExtra_1817_wp4100";

        internal static bool IsWeaponLayerAction(string id) => IsFirearmShot(id) || IsReloadAction(id) || IsEquipmentAction(id)
            || IsKnifeTail(id) || id is "SG09R_0520" or "W870_0520" or "CQBR_0520" or "W870_0510"
                or "CQBR_0140" or "CQBR_0158" or "Knife_0140" or "Knife_0158";

        internal static bool IsRunningAimPose(string id) =>
            id is "SG09R_0160" or "W870_0160" or "CQBR_0160" or "Knife_0160";

        public void HoldTerminal()
        {
            ReleaseTraversalSill();
            traversalEndTime = 0f;
            terminal = true;
            paired = false;
            releasePairedTail = false;
            pairTrack = null;
            PairMotionError = 0f;
            speed = 0f;
            animator.speed = 0f;
            ResetWeaponLayers();
            body.useGravity = true;
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
        }

        public bool CanPlace(Vector3 at)
        {
            Vector3 center = at + body.rotation * Vector3.Scale(capsule.center, transform.lossyScale);
            float radius = capsule.radius * 0.94f;
            float spine = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            return !Physics.CheckCapsule(center - Vector3.up * spine, center + Vector3.up * spine, radius, worldMask,
                QueryTriggerInteraction.Ignore);
        }

        internal bool CanAlignForPair(Vector3 target, float clearance = 0f)
        {
            if (!CanPlace(target))
            {
                return false;
            }
            Vector3 start = body.position + Vector3.up * clearance;
            Vector3 shift = target - start;
            if (shift.sqrMagnitude <= 0.0001f)
            {
                return true;
            }
            Vector3 center = start + body.rotation * Vector3.Scale(capsule.center, transform.lossyScale);
            float radius = capsule.radius * 0.94f;
            float axis = Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            return !Physics.CapsuleCast(center - Vector3.up * axis, center + Vector3.up * axis, radius,
                shift.normalized, shift.magnitude, worldMask, QueryTriggerInteraction.Ignore);
        }

        public void Place(Vector3 position, Quaternion rotation)
        {
            RestoreWeaponAim();
            leftPlant?.Restore();
            rightPlant?.Restore();
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            planarVelocity = Vector3.zero;
            facingVelocity = 0f;
            previousPosition = position;
            supportPlant = null;
            footWeight = 0f;
            staggerLocomotionBlendRemaining = 0f;
            staggerReleaseLayer = 0;
        }

        private void FixedUpdate()
        {
            if (traversalEndTime <= 0f)
            {
                ReleaseTraversalSill();
            }
            // 在 Animator 计算下一步之前还原上一帧的显示修正。
            RestoreMeleeGroundPose();
            RestoreWeaponAim();
            leftPlant?.Restore();
            rightPlant?.Restore();
            ActualStep = body.position - previousPosition;
            previousPosition = body.position;
            Vector3 travelHeading = desiredForward;
            if (take != null && take.id == CrawlBackClip)
            {
                travelHeading = -(body.rotation * Vector3.forward);
            }
            if (mode >= 1 && mode <= 5 && desiredForward.sqrMagnitude > 0.01f)
                travelHeading = Quaternion.LookRotation(desiredForward, Vector3.up) * new Vector3(direction.x, 0f, direction.y);
            if (obstructed && (speed <= 0.01f || travelHeading.sqrMagnitude < 0.01f ||
                !body.SweepTest(travelHeading.normalized, out _, 0.08f, QueryTriggerInteraction.Ignore)))
            {
                obstructed = false;
                blockedTime = 0f;
            }
            float request = Vector3.ProjectOnPlane(RequestedStep, Vector3.up).magnitude;
            float actual = Vector3.ProjectOnPlane(ActualStep, Vector3.up).magnitude;
            blockedTime = request > 0.005f && actual < request * 0.18f ? blockedTime + Time.fixedDeltaTime : 0f;
            obstructed |= blockedTime > 0.14f;
            if (terminal)
                return;

            staggerLocomotionBlendRemaining = Mathf.Max(0f, staggerLocomotionBlendRemaining - Time.fixedDeltaTime);
            if (!BlocksLocomotion && movementTake == null)
                EnterLocomotion(false);
            UpdateWeaponLayer();
            float effectivePace = obstructed ? 0f : speed;
            float dt = Time.fixedDeltaTime;
            animator.SetFloat(Pace, effectivePace, 0.12f, dt);
            animator.SetFloat(MoveX, effectivePace > 0f ? direction.x : 0f, 0.10f, dt);
            animator.SetFloat(MoveY, effectivePace > 0f ? direction.y : 0f, 0.10f, dt);
        }

        private void UpdateWeaponLayer()
        {
            if (weaponLayer <= 0)
            {
                return;
            }
            bool rifleCarry = equippedId == "wp4402" && !weaponPoseAimed && weaponPoseState != 0;
            bool ready = weaponPoseState != 0 && (!BlocksLocomotion || rifleCarry && IsStanceChange);
            int targetLayer = 0;
            if (upperAction)
            {
                targetLayer = actionLayer;
            }
            else if (ready)
            {
                targetLayer = GetWeaponPoseLayer();
                int state = GetWeaponPoseHash(targetLayer);
                if (state != activePoseState)
                {
                    PlayWeaponPose(targetLayer, state);
                    activePoseState = state;
                }
            }
            BlendWeaponLayer(weaponLayer, targetLayer);
            BlendWeaponLayer(weaponArmsLayer, targetLayer);
            if (prepareShot && animator.GetLayerWeight(actionLayer) >= 0.92f && Time.time >= prepareShotUntil)
            {
                prepareShot = false;
                animator.SetFloat(WeaponDurationScale, weaponRate);
            }
        }

        private void PlayWeaponPose(int layer, int state)
        {
            float weight = animator.GetLayerWeight(layer);
            if (weight <= 0.01f)
            {
                animator.Play(state, layer, 0f);
            }
            else
            {
                float fade = 0.16f;
                int previous = animator.GetCurrentAnimatorStateInfo(layer).shortNameHash;
                if (previous == RifleReload || previous == CrouchedRifleReload)
                {
                    // 换弹末帧仍有收枪偏移，给双手留出回到持枪姿态的时间。
                    fade = 0.36f;
                }
                else if (layer == staggerReleaseLayer)
                {
                    fade = StaggerLocomotionBlend;
                }
                animator.CrossFadeInFixedTime(state, fade, layer, 0f, 0f);
            }
        }

        private void BlendWeaponLayer(int layer, int targetLayer)
        {
            if (layer <= 0)
            {
                return;
            }
            if (layer == staggerReleaseLayer)
            {
                if (targetLayer == 0)
                {
                    // 避免上身权重匀速退掉时，与仍在混合的步态合成一次甩腕。
                    staggerReleaseTime += Time.fixedDeltaTime;
                    float progress = staggerReleaseTime / StaggerUpperRelease;
                    animator.SetLayerWeight(layer, Mathf.SmoothStep(staggerReleaseWeight, 0f, progress));
                    if (progress >= 1f)
                    {
                        staggerReleaseLayer = 0;
                    }
                    return;
                }
                staggerReleaseLayer = 0;
            }
            float targetWeight = 0f;
            float blend = 0.18f;
            if (layer == targetLayer)
            {
                targetWeight = 1f;
                blend = 0.10f;
                if (upperAction && take.id == "CQBR_0543")
                {
                    blend = RifleDrawBlend;
                }
            }
            float weight = Mathf.MoveTowards(animator.GetLayerWeight(layer), targetWeight, Time.fixedDeltaTime / blend);
            animator.SetLayerWeight(layer, weight);
        }

        private void ResetWeaponLayers()
        {
            staggerReleaseLayer = 0;
            if (weaponLayer > 0)
            {
                animator.SetLayerWeight(weaponLayer, 0f);
            }
            if (weaponArmsLayer > 0)
            {
                animator.SetLayerWeight(weaponArmsLayer, 0f);
            }
        }

        private int GetWeaponPoseLayer()
        {
            if (weaponArmsLayer > 0 && (equippedId == "wp4402" || mode == 1 || speed > 0.01f && !weaponPoseAimed))
            {
                return weaponArmsLayer;
            }
            return weaponLayer;
        }

        private int GetWeaponPoseHash(int layer)
        {
            if (layer == weaponArmsLayer)
            {
                return weaponArmsPoseState;
            }
            return weaponPoseState;
        }

        private int GetActionHash(Take current)
        {
            if (!upperAction)
            {
                return current.state;
            }
            if (actionLayer == weaponArmsLayer)
            {
                return current.weaponArmsState;
            }
            return current.weaponState;
        }

        private void EnterLocomotion(bool force, float forcedPhase = 0f, float fade = 0.17f)
        {
            if (!animator.isActiveAndEnabled)
            {
                locomotionState = 0;
                return;
            }
            int state = Animator.StringToHash("Base Layer.Move" + mode);
            if (!force && state == locomotionState)
                return;

            var previous = animator.GetCurrentAnimatorStateInfo(0);
            float phase = forcedPhase;
            if (!force)
            {
                phase = Mathf.Repeat(previous.normalizedTime, 1f);
            }
            animator.CrossFade(state, fade / Mathf.Max(0.1f, previous.length), 0, phase, 0f);
            locomotionState = state;
        }

        private void OnAnimatorMove()
        {
            footTime = Time.fixedTime;
            if (!terminal && (paired || pairTrack != null || IsTraversing) && !HasValidStageTrack)
            {
                // 片段重载失效时停止本次舞台动作，让配对协调器释放另一侧。
                ClearAction();
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                planarVelocity = Vector3.zero;
                RequestedStep = Vector3.zero;
                return;
            }
            var action = take;
            bool sampled = false;
            if (action != null && action.body != null && TryGetActionState(GetActionHash(action), actionLayer, out var state))
            {
                actionTime = Mathf.Clamp01(state.normalizedTime) * action.body.length;
                UpdatePickupEntryRate();
                if (IsEnemyGroundLoop(action.id))
                {
                    actionTime = Mathf.Repeat(state.normalizedTime, 1f) * action.body.length;
                }
                if (traversalEndTime > 0f)
                {
                    sampledTraversalAction = actionId;
                    actionTime = Mathf.Min(actionTime, traversalEndTime);
                    if (IsEnemyWindowStart(action.id))
                    {
                        UpdateEnemyWindowCapsule(actionTime * action.body.frameRate);
                    }
                }
                if (HasHeldModel && action.mechanism != null)
                {
                    float mechanismTime = actionTime;
                    if (action.id is "CQBR_0500" or "CQBR_0500_wp4202h")
                    {
                        mechanismTime = RifleRecoilSourceFrame(actionTime * action.body.frameRate) / action.mechanism.frameRate;
                    }
                    action.mechanism.SampleAnimation(held.model.gameObject, Mathf.Min(mechanismTime, action.mechanism.length));
                    // 空仓换弹先延续枪机后移，第 182 源帧交给原版释放轨道。
                    bool beforeBoltRelease = rifleReloadStartedOpen && actionTime * action.body.frameRate < RifleReloadBoltReleaseFrame;
                    if (beforeBoltRelease || rifleBoltOpen && !IsFirearmShot(action.id) && !IsReloadAction(action.id))
                    {
                        ApplyRifleBoltOpenPose();
                    }
                }
                sampled = true;
            }
            CorrectWeaponAim(Time.fixedDeltaTime);
            var current = BlocksLocomotion ? take : movementTake;
            int previousAction = actionId;
            if (sampled && !finished && !prepareShot)
            {
                ReadContactFrames(action);
                if (previousAction == actionId)
                {
                    SampleKnifeBlade(action);
                }
            }

            bool crawling = current != null && IsEnemyCrawlTravel(current.id);
            bool steered = current == null || IsSteeredTransition(current.id) || crawling;
            Quaternion facing = body.rotation;
            if (steered && desiredForward.sqrMagnitude > 0.01f)
            {
                float target = Mathf.Atan2(desiredForward.x, desiredForward.z) * Mathf.Rad2Deg;
                float turnTime = 0.075f;
                float maxTurnSpeed = 720f;
                if (crawling)
                {
                    turnTime = 0.22f;
                    maxTurnSpeed = 105f;
                }
                float nextYaw = Mathf.SmoothDampAngle(facing.eulerAngles.y, target, ref facingVelocity, turnTime,
                    maxTurnSpeed, Time.fixedDeltaTime);
                facing = Quaternion.Euler(0f, nextYaw, 0f);
            }
            else
                facingVelocity = 0f;

            Vector3 delta = animator.deltaPosition;
            Quaternion deltaRotation = steered ? Quaternion.identity : animator.deltaRotation;
            if (steered)
            {
                if (TryReadCrawlBackStep(current, sampled, out var crawlStep))
                {
                    delta = facing * crawlStep;
                }
                else
                {
                    delta = facing * (Quaternion.Inverse(transform.rotation) * delta);
                }
            }
            if (current != null && current.id == "GanadoGround_3001")
            {
                delta = Vector3.zero;
                deltaRotation = Quaternion.identity;
            }

            bool vertical = current?.verticalTravel == true || paired;
            if (!vertical)
                delta.y = 0f;
            bool freeze = terminal || (BlocksLocomotion && (finished || previousAction != actionId));
            if (freeze)
            {
                delta = Vector3.zero;
                deltaRotation = Quaternion.identity;
            }
            if (steered)
            {
                if (freeze || obstructed)
                    planarVelocity = Vector3.zero;
                else
                {
                    float dt = Time.fixedDeltaTime;
                    Vector3 target = delta / dt;
                    if (current == null)
                    {
                        target = BlendVaultExitVelocity(target, facing, dt);
                    }
                    if (speed <= 0.01f && (current == null || IsWalkStop(current.id)))
                    {
                        target = Vector3.zero;
                    }
                    planarVelocity = Vector3.Lerp(planarVelocity, target, 1f - Mathf.Exp(-dt / 0.055f));
                    if (target == Vector3.zero && planarVelocity.sqrMagnitude < 0.0016f)
                        planarVelocity = Vector3.zero;
                }
                delta = planarVelocity * Time.fixedDeltaTime;
                if (!BlocksLocomotion)
                {
                    delta = LimitEnemyApproach(delta);
                }
            }
            else
            {
                planarVelocity = Vector3.zero;
                if (obstructed && !BlocksLocomotion)
                {
                    delta = Vector3.zero;
                }
            }

            if (pairTrack != null && !terminal && previousAction == actionId)
            {
                Pose sourcePose = pairTrack.Sample(actionTime);
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((actionTime - pairEntryTime) / pairBlend));
                Vector3 target = pairStageStart.position + pairStageStart.rotation * sourcePose.position
                    + Vector3.Lerp(pairEntryOffset, Vector3.zero, blend);
                Vector3 correction = target - body.position;
                PairMotionError = correction.magnitude;
                float maxSpeed = 10f;
                if (traversalEndTime > 0f && take != null && IsEnemyWindowStart(take.id) && correction.y < -0.10f)
                {
                    maxSpeed = 16f;
                }
                delta = Vector3.ClampMagnitude(correction, maxSpeed * Time.fixedDeltaTime);
                facing = Quaternion.Slerp(pairEntryRotation, Quaternion.identity, blend)
                    * pairStageStart.rotation * sourcePose.rotation;
                deltaRotation = Quaternion.identity;
            }
            else if (!freeze && previousAction == actionId)
            {
                delta = ApplyMeleeApproach(delta, ref facing);
            }

            float stepUp = current == null ? MeasureStepUp(delta) : 0f;
            body.useGravity = !vertical;
            RequestedStep = delta;
            Vector3 velocity = delta / Time.fixedDeltaTime;
            if (!vertical)
                velocity.y = body.linearVelocity.y;
            Quaternion rotation = facing * deltaRotation;

            if (playerMotion != null)
            {
                var source = terminal ? GameArchitecture.MotionSource.Death
                    : paired ? GameArchitecture.MotionSource.PairedActionAction
                    : vertical ? GameArchitecture.MotionSource.Traversal
                    : BlocksLocomotion ? GameArchitecture.MotionSource.AttackRootMotion
                    : GameArchitecture.MotionSource.NormalMovement;
                var lease = playerMotion.AuthorizeMotion(source, actionId);
                if (playerMotion.CommitSpeed(lease, velocity))
                {
                    if (stepUp > 0f)
                        playerMotion.CommitPosition(lease, body.position + Vector3.up * stepUp);
                    // 清除碰撞留下的转动速度，由当前动作提交朝向。
                    playerMotion.CommitAngleSpeed(lease, Vector3.zero);
                    body.MoveRotation(rotation);
                }
            }
            else
            {
                if (stepUp > 0f)
                    body.position += Vector3.up * stepUp;
                body.linearVelocity = velocity;
                body.angularVelocity = Vector3.zero;
                body.MoveRotation(rotation);
            }
        }

        private static bool IsSteeredTransition(string id) =>
            IsMovementStart(id) || IsMovementStop(id) || IsMovingStance(id) || IsEnemyGaitEdge(id)
                || id is "LeonMove_0169" or "LeonMove_1169"
                or "Knife_0140" or "Knife_0158"
                or "SG09R_0140" or "SG09R_0158" or "W870_0140" or "W870_0158" or "CQBR_0140" or "CQBR_0158";

        internal static bool IsMovementStart(string id) =>
            IsDirectionalWalk(id, '0')
            || id is "LeonMove_0400" or "LeonMove_0500" or "LeonMove_0700" or "Knife_0400" or "LeonHurt_0500";

        internal static bool IsMovementStop(string id) =>
            IsWalkStop(id) || IsEnemyGaitStop(id) || id is "LeonMove_0540" or "LeonMove_0740" or "LeonHurt_0540";

        private static bool IsWalkStop(string id) =>
            id == "LeonMove_0406" || IsDirectionalStop(id);

        internal static bool IsDirectionalStop(string id) => IsDirectionalWalk(id, '6');

        private static bool IsDirectionalWalk(string id, char ending)
        {
            if (id == null || id.Length < 5 || id[^1] != ending || id[^3] != '4'
                || id[^2] is not ('0' or '3' or '4' or '7'))
            {
                return false;
            }
            return id.StartsWith("LeonMove_14", StringComparison.Ordinal)
                || id.StartsWith("SG09R_04", StringComparison.Ordinal)
                || id.StartsWith("W870_04", StringComparison.Ordinal)
                || id.StartsWith("CQBR_04", StringComparison.Ordinal);
        }

        internal static bool IsMovingStance(string id) =>
            id is "LeonMove_0543" or "LeonMove_1500" or "LeonMove_1510";

        internal static IEnumerable<string> DirectionalEdgeIds()
        {
            yield return "LeonMove_0543";
            yield return "LeonMove_0545";
            yield return "LeonMove_1500";
            yield return "LeonMove_1510";
            foreach (string prefix in new[] { "LeonMove_14", "SG09R_04", "W870_04", "CQBR_04" })
            {
                foreach (char directionCode in "0347")
                {
                    if (directionCode == '0' && prefix != "LeonMove_14")
                    {
                        continue;
                    }
                    yield return prefix + directionCode + "0";
                    yield return prefix + directionCode + "6";
                }
            }
        }

        internal static bool IsMovementTurn(string id) =>
            id is "LeonMove_0530" or "LeonMove_0532" or "LeonMove_0534" or "LeonMove_0536";

        internal static bool IsMovementTransition(string id) =>
            IsMovementStart(id) || IsMovementStop(id) || IsMovementTurn(id) || IsMovingStance(id) || IsEnemyGaitEdge(id);

        private float MeasureStepUp(Vector3 delta)
        {
            const float height = 0.18f;
            if (delta.sqrMagnitude < 0.00001f || Mathf.Abs(body.linearVelocity.y) > 0.45f)
                return 0f;
            Vector3 heading = delta.normalized;
            Vector3 bottom = body.position + Vector3.up * 0.02f;
            float distance = capsule.radius + delta.magnitude + 0.015f;
            if (!Physics.Raycast(bottom + Vector3.up * 0.035f, heading, out var riser, distance, worldMask,
                    QueryTriggerInteraction.Ignore) || Mathf.Abs(riser.normal.y) > 0.25f
                || Physics.Raycast(bottom + Vector3.up * height, heading, distance, worldMask, QueryTriggerInteraction.Ignore))
                return 0f;

            Vector3 ledge = bottom + heading * (riser.distance + 0.06f) + Vector3.up * (height + 0.02f);
            if (!Physics.Raycast(ledge, Vector3.down, out var floor, height + 0.03f, worldMask, QueryTriggerInteraction.Ignore)
                || floor.normal.y < 0.7f)
                return 0f;

            float rise = floor.point.y - bottom.y + 0.022f;
            return rise > 0.035f && rise <= height && CanPlace(body.position + Vector3.up * rise) ? rise : 0f;
        }

        private bool TryGetActionState(int hash, int layer, out AnimatorStateInfo state)
        {
            if (animator.IsInTransition(layer))
            {
                state = animator.GetNextAnimatorStateInfo(layer);
                if (state.fullPathHash == hash)
                    return true;
            }
            state = animator.GetCurrentAnimatorStateInfo(layer);
            return state.fullPathHash == hash;
        }

        private void ReadContactFrames(Take current)
        {
            int id = actionId;
            int frame = Mathf.FloorToInt(actionTime * current.body.frameRate + 0.001f);
            while (markerCursor < current.contactFrames.Length && current.contactFrames[markerCursor] <= frame)
            {
                int marker = current.contactFrames[markerCursor++];
                Contact?.Invoke(this, current, id, marker);
                if (id != actionId)
                    return;
            }
        }

        private void LateUpdate()
        {
            RestoreMeleeGroundPose();
            RestoreWeaponAim();
            var current = take;
            if (current != null && current.body != null && TryGetActionState(GetActionHash(current), actionLayer, out var state))
            {
                UpdateEquipmentVisibility(current);

                bool reachedEnd = state.normalizedTime >= 1f - 0.0001f;
                var (moveFrame, endFrame) = LeonStaggerFrames(current.id);
                if (endFrame > 0)
                {
                    float frame = actionTime * current.body.frameRate;
                    reachedEnd = frame >= endFrame;
                    if (!reachedEnd && !upperAction && speed > 0.01f && frame >= moveFrame)
                    {
                        BeginStaggerRecovery(current);
                    }
                }
                if (reversingAimTransition)
                {
                    reachedEnd = state.normalizedTime <= 0.0001f;
                }
                if (traversalEndTime > 0f)
                {
                    reachedEnd = sampledTraversalAction == actionId
                        && state.normalizedTime * current.body.length >= traversalEndTime - 0.0001f;
                }
                if (!terminal && !finished && reachedEnd && !IsEnemyGroundLoop(current.id))
                {
                    int id = actionId;
                    finished = true;
                    Completed?.Invoke(this, current, id);
                    if (id == actionId && !terminal && (!paired || releasePairedTail))
                        ClearAction();
                }
            }

            if (movementTake != null && TryGetActionState(movementTake.state, 0, out var locomotion))
            {
                movementTime = Mathf.Clamp01(locomotion.normalizedTime) * movementTake.body.length;
                if (locomotion.normalizedTime >= 1f || ReachedLocomotionHandoff(movementTake))
                    CancelMovement();
            }
            SampleFeet();
            ApplyMeleeGroundPose();
            CorrectWeaponAim(0f);
        }

        private void BeginStaggerRecovery(Take current)
        {
            int layer = weaponLayer;
            int hash = current.weaponState;
            if (weaponArmsLayer > 0 && (equippedId == "wp4402" || IsCrouching))
            {
                layer = weaponArmsLayer;
                hash = current.weaponArmsState;
            }
            if (layer <= 0 || !animator.HasState(layer, hash))
            {
                return;
            }
            // 上身从全身片段的当前帧接续，基础层独立淡回相位校准后的步态。
            upperAction = true;
            actionLayer = layer;
            activePoseState = 0;
            animator.SetFloat(WeaponDurationScale, 1f);
            animator.Play(hash, layer, actionTime / current.body.length);
            animator.SetLayerWeight(layer, 1f);
            EnterStaggerLocomotion(current.id);
        }

        private void EnterStaggerLocomotion(string id)
        {
            if (speed <= 0.01f)
            {
                EnterLocomotion(true, fade: StaggerLocomotionBlend);
                return;
            }

            // 按受击解锁后 0、3、6 源帧的双脚和膝部位置匹配目标步态的 60 等分相位。
            (int walk, int jog, int sprint, int hurtWalk, int hurtJog, int aimForward,
                int knifeForward, int aimLeft, int aimBack, int aimRight) phase = id switch
            {
                "LeonDamage_0100" => (11, 2, 7, 25, 1, 12, 16, 21, 9, 49),
                "LeonDamage_0102" => (4, 30, 34, 57, 30, 6, 9, 25, 18, 45),
                "LeonDamage_0104" => (16, 5, 12, 28, 3, 16, 24, 39, 59, 31),
                "LeonDamage_0106" => (12, 2, 28, 25, 1, 13, 17, 22, 8, 40),
                "LeonDamage_0110" => (32, 1, 29, 51, 0, 35, 34, 31, 46, 37),
                "LeonDamage_0112" => (8, 27, 31, 22, 58, 9, 13, 27, 41, 43),
                "LeonDamage_0114" => (21, 5, 12, 29, 4, 29, 28, 37, 54, 33),
                "LeonDamage_0116" => (57, 34, 38, 10, 49, 59, 3, 24, 24, 48),
                _ => default
            };
            float normalized = staggerEntryPhase;
            if (mode == 0 || mode == 4 && speed >= 2.5f)
            {
                int sample = phase.walk;
                if (speed >= 2.5f)
                {
                    sample = phase.sprint;
                }
                else if (speed >= 1.5f)
                {
                    sample = phase.jog;
                }
                normalized = sample / 60f;
            }
            else if (mode == 6)
            {
                int sample = phase.hurtWalk;
                if (speed >= 1.5f)
                {
                    sample = phase.hurtJog;
                }
                normalized = sample / 60f;
            }
            else if (mode >= 2 && mode <= 4)
            {
                int sample = phase.aimForward;
                if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                {
                    sample = phase.aimRight;
                    if (direction.x < 0f)
                    {
                        sample = phase.aimLeft;
                    }
                }
                else if (direction.y < 0f)
                {
                    sample = phase.aimBack;
                }
                normalized = sample / 60f;
            }
            else if (mode == 5 && direction.y > Mathf.Abs(direction.x))
            {
                normalized = phase.knifeForward / 60f;
            }
            staggerLocomotionBlendRemaining = StaggerLocomotionBlend;
            EnterLocomotion(true, normalized, StaggerLocomotionBlend);
        }

        private void UpdateEquipmentVisibility(Take current)
        {
            if (!HasHeldModel)
                return;
            // 原版装备显示轨道在收刀第 15 帧与拔步枪第 10 帧切换。
            float frame = actionTime * current.body.frameRate;
            bool visible = current.id switch
            {
                "Knife_Draw" or "Knife_CrouchDraw" => frame > 15f,
                "Knife_0551" or "Knife_1551" => frame < 15f,
                "SG09R_0541" or "SG09R_1541" => frame > 20f,
                "SG09R_0543" or "W870_0541" or "W870_1541" => frame > 10f,
                "CQBR_0543" => frame > RifleDrawVisibleFrame,
                "CQBR_0551" => frame < current.body.length * current.body.frameRate - RifleDrawVisibleFrame,
                "LeonDoors_0007" => false,
                "LeonTraverse_0370" => frame >= 61f,
                "LeonTraverse_0374" => frame >= 115f,
                "LeonTraverse_0375" => false,
                _ => true
            };
            if (current.id.StartsWith("LeonPair_", StringComparison.Ordinal) && string.IsNullOrEmpty(current.weapon))
            {
                // 原版抓取收起武器，普通挣脱第 190 帧才取回。
                visible = current.id == "LeonPair_0020" && frame >= 189.999f;
            }
            if (held.model.gameObject.activeSelf != visible)
                held.model.gameObject.SetActive(visible);
        }

        private void RestoreWeaponAim()
        {
            if (adjustedAimJoint == null)
            {
                return;
            }
            adjustedAimJoint.localRotation = animatedAimRotation;
            adjustedAimJoint = null;
        }

        private void CorrectWeaponAim(float dt)
        {
            if (weaponSpine == null || held == null || held.muzzle == null)
            {
                return;
            }
            if (held.id == "wp4402" || IsCrouching)
            {
                // CQBR 托握跟随烘焙轨道，蹲伏时只播放持枪与换弹。
                aimCorrectionWeight = 0f;
                aimReferenceWeapon = null;
                if (held.id == "wp4402" && !IsCrouching && !BlocksLocomotion)
                {
                    CorrectRifleAim(dt);
                }
                else
                {
                    rifleAimWeight = 0f;
                }
                return;
            }

            rifleAimWeight = 0f;
            int poseLayer = GetWeaponPoseLayer();
            bool ready = !upperAction && weaponPoseAimed && weaponPoseState != 0
                && TryGetActionState(GetWeaponPoseHash(poseLayer), poseLayer, out _);
            bool firing = upperAction && IsFirearmShot(take.id);
            float target = 0f;
            if (!BlocksLocomotion)
            {
                if (firing)
                {
                    target = animator.GetLayerWeight(actionLayer);
                }
                else if (ready)
                {
                    target = animator.GetLayerWeight(poseLayer);
                }
            }
            aimCorrectionWeight = Mathf.MoveTowards(aimCorrectionWeight, target, dt / 0.10f);
            if (target > 0f && (ready || prepareShot || aimReferenceWeapon != held.id))
            {
                // 从枪口标定躯干的父骨架朝向，保留上身轨道的枪手相对位置及后坐。
                Quaternion align = Quaternion.FromToRotation(held.muzzle.forward, transform.forward);
                aimReferenceInActor = Quaternion.Inverse(transform.rotation) * align * weaponSpine.parent.rotation;
                aimReferenceWeapon = held.id;
            }
            if (aimCorrectionWeight <= 0f || aimReferenceWeapon != held.id || BlocksLocomotion)
            {
                return;
            }

            Quaternion correction = transform.rotation * aimReferenceInActor * Quaternion.Inverse(weaponSpine.parent.rotation);
            ApplyAimCorrection(weaponSpine, correction, aimCorrectionWeight);
        }

        private void CorrectRifleAim(float dt)
        {
            if (rifleShoulders == null)
            {
                return;
            }
            float weight = 0f;
            if (IsRifleRaisingForShot)
            {
                float progress = Mathf.Clamp01(actionTime / take.body.length);
                if (take.id == "CQBR_0158")
                {
                    progress = 1f - progress;
                }
                weight = Mathf.SmoothStep(0f, 1f, progress);
            }
            else if (weaponPoseAimed && (take == null || IsFirearmShot(take.id)))
            {
                weight = 1f;
            }
            rifleAimWeight = Mathf.MoveTowards(rifleAimWeight, weight, dt / 0.14f);
            if (rifleAimWeight > 0f)
            {
                // 走跑瞄准共用 CQBR_0160 的枪口朝向，手臂和枪体沿用当前托握与后坐。
                Vector3 carried = rifleShoulders.TransformDirection(RifleAimInShoulders);
                Vector3 aimed = transform.TransformDirection(RifleAimInActor);
                ApplyAimCorrection(weaponSpine, Quaternion.FromToRotation(carried, aimed), rifleAimWeight);
            }
        }

        private void ApplyAimCorrection(Transform joint, Quaternion correction, float weight)
        {
            animatedAimRotation = joint.localRotation;
            adjustedAimJoint = joint;
            joint.rotation = Quaternion.Slerp(Quaternion.identity, correction, weight) * joint.rotation;
        }

        private (int frame, int targetFrame, int cycleFrames) MovementHandoff(string id)
        {
            if (TryGetEnemyGait(id, out var edge))
            {
                return (edge.exitFrame, edge.cycleFrame, edge.cycleFrames);
            }
            // 源帧取原版脚步同步标记，循环接入帧取实际重绑后腿部姿势的采样结果。
            return id switch
            {
                "LeonMove_0400" => (29, 42, 63),
                "LeonMove_0500" => (22, 25, 47),
                "LeonMove_0700" => (28, 22, 34),
                "LeonHurt_0500" => (24, 22, 47),
                "SG09R_0400" => (27, 48, 69),
                "W870_0400" => (30, 48, 69),
                "CQBR_0400" => (27, 45, 69),
                "Knife_0400" => (27, 21, 77),
                "LeonMove_0530" => (56, 1, 47),
                "LeonMove_0532" => (60, 1, 47),
                "LeonMove_0534" or "LeonMove_0536" => (60, 25, 47),
                "LeonMove_0543" => (79, 53, 64),
                "LeonMove_1500" => (59, 4, 47),
                "LeonMove_1510" => (64, 30, 47),
                "LeonMove_1400" => (45, 47, 64),
                "LeonMove_1430" => (38, 41, 61),
                "LeonMove_1440" => (40, 42, 64),
                "LeonMove_1470" => (41, 41, 64),
                "SG09R_0430" or "W870_0430" or "CQBR_0430" => (34, 18, 70),
                "SG09R_0440" or "CQBR_0440" => (31, 28, 70),
                "W870_0440" => (30, 27, 70),
                "SG09R_0470" => (31, 28, 70),
                "W870_0470" or "CQBR_0470" => (31, 32, 70),
                "LeonMove_0406" or "SG09R_0406" or "W870_0406" or "CQBR_0406" => (21, 0, 0),
                "LeonMove_1406" => (15, 44, 60),
                "LeonMove_1436" => (16, 44, 60),
                "LeonMove_1446" => (15, 45, 60),
                "LeonMove_1476" => (15, 19, 60),
                "SG09R_0436" => (43, 0, 0),
                "SG09R_0446" => (50, 0, 0),
                "SG09R_0476" => (45, 0, 0),
                "W870_0436" => (46, 0, 0),
                "W870_0446" or "W870_0476" or "CQBR_0476" => (44, 0, 0),
                "CQBR_0436" => (42, 0, 0),
                "CQBR_0446" => (49, 0, 0),
                _ => (int.MaxValue, 0, 0)
            };
        }

        private bool ReachedLocomotionHandoff(Take current)
        {
            var (frame, _, _) = MovementHandoff(current.id);
            return movementTime * current.body.frameRate >= frame;
        }

        private void SampleFeet()
        {
            if (leftPlant == null || rightPlant == null)
                return;
            leftPlant.Restore();
            rightPlant.Restore();
            leftPlant.Sample(footTime);
            rightPlant.Sample(footTime);

            bool canPlant = !BlocksLocomotion && movementTake == null && !obstructed && speed > 0.01f &&
                staggerLocomotionBlendRemaining <= 0f &&
                ActualStep.sqrMagnitude > 0.00002f &&
                (desiredForward.sqrMagnitude < 0.01f || Vector3.Angle(transform.forward, desiredForward) < 18f);
            bool vaultSupport = traversalEndTime > 0f && take.id == "LeonTraverse_0371"
                && actionTime * take.body.frameRate >= 55f;
            if (paired || terminal || take?.verticalTravel == true && !vaultSupport)
            {
                supportPlant = null;
                footWeight = 0f;
                FootDrift = 0f;
                SupportFoot = "—";
                return;
            }

            canPlant |= vaultSupport && !obstructed && ActualStep.sqrMagnitude > 0.00002f;
            bool running = vaultSupport || speed >= 1.5f;
            bool hold = canPlant && supportPlant != null &&
                CanSupport(supportPlant, running ? 0.095f : 0.085f, running ? 4.8f : 1.55f) &&
                Vector3.ProjectOnPlane(supportPlant.Position - supportAnchor, Vector3.up).sqrMagnitude <= 0.046f;
            if (supportPlant != null && !hold)
            {
                footWeight = Mathf.MoveTowards(footWeight, 0f, Time.deltaTime / 0.055f);
                if (footWeight <= 0f)
                    supportPlant = null;
            }
            if (supportPlant == null && canPlant)
            {
                float contactHeight = running ? 0.075f : 0.058f;
                float contactSpeed = running ? 3.2f : 1.15f;
                bool left = CanSupport(leftPlant, contactHeight, contactSpeed);
                bool right = !vaultSupport && CanSupport(rightPlant, contactHeight, contactSpeed);
                if (left || right)
                {
                    supportPlant = left && (!right || leftPlant.Speed <= rightPlant.Speed) ? leftPlant : rightPlant;
                    supportPlant.BeginContact();
                    supportAnchor = supportPlant.Position;
                    supportHoldsHeight = vaultSupport;
                    footWeight = 1f;
                    FootDrift = 0f;
                    hold = true;
                }
            }

            SupportFoot = hold ? supportPlant == leftPlant ? "左" : "右" : "—";
            if (supportPlant == null)
                return;

            if (hold)
                footWeight = Mathf.MoveTowards(footWeight, 1f, Time.deltaTime / 0.04f);
            supportPlant.Capture();
            supportPlant.Apply(supportAnchor, Mathf.SmoothStep(0f, 1f, footWeight), supportHoldsHeight);
            if (hold)
                FootDrift = Mathf.Max(FootDrift, Vector3.ProjectOnPlane(supportPlant.Position - supportAnchor, Vector3.up).magnitude);
        }

        private bool CanSupport(ChapterOneFootPlant foot, float height, float maximumSpeed)
            => foot.Position.y - transform.position.y < height && foot.Speed < maximumSpeed;
    }
}



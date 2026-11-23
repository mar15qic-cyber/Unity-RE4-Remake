using System;
using GameArchitecture;
using UnityEngine;
using UnityEngine.AI;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop : MonoBehaviour
    {
        private const float RifleShotInterval = 0.10f;
        [Serializable]
        public sealed class Threat
        {
            public ChapterOneRootActor actor;
            public string prefix;
            public string attack;
            public string alert;
            public string equipment;
            public int health = 100;
            [NonSerialized] public bool alerted;
            [NonSerialized] public float attackAfter;
            [NonSerialized] public float repathAfter;
            [NonSerialized] public Vector3[] corners;
            [NonSerialized] public int legStaggerAction;
            [NonSerialized] public bool leftLeg;
            [NonSerialized] internal int meleeReactionAction;
            [NonSerialized] internal int initialHealth;
            [NonSerialized] internal EnemyGroundPhase groundPhase;
            [NonSerialized] internal int groundAction;
            [NonSerialized] internal int groundHitAction;
            [NonSerialized] internal Vector3 groundPreviousStrike;
            [NonSerialized] internal bool permanentGround;
            [NonSerialized] internal float groundSince;
            [NonSerialized] internal float groundStopAfter;
        }

        private sealed class Weapon
        {
            public readonly string Id;
            public readonly string Prefix;
            public readonly int Capacity;
            public readonly WeaponType Type;

            public Weapon(string id, string prefix, int capacity, WeaponType type)
            {
                Id = id;
                Prefix = prefix;
                Capacity = capacity;
                Type = type;
            }
        }

        [SerializeField] private ChapterOneRootActor leon;
        [SerializeField] private Threat[] threats;
        [SerializeField] private Camera view;
        [SerializeField] private Transform pickup;
        [SerializeField] private Transform crate;
        [SerializeField] private Transform vault;
        [SerializeField] private Transform dropVault;
        [SerializeField] private GameObject door;
        [SerializeField] private AnimationClip doorOpening;
        [SerializeField] private AnimationClip doorClosing;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField] private NavMeshData navigation;
        [SerializeField] private LayerMask obstructionMask;
        private readonly Weapon[] weapons =
        {
            new("wp4000", "SG09R", 10, WeaponType.Pistol), new("wp4100", "W870", 5, WeaponType.Shotgun),
            new("wp4402", "CQBR", 20, WeaponType.Rifle), new("wp5000", "Knife", 0, WeaponType.Knife)
        };
        private readonly WeaponRuntimeModule weaponRuntime = new();
        private NavMeshDataInstance meshInstance;
        private AimResolver aimResolver;
        private int slot;
        private int pendingSlot = -1;
        private int health = 100;
        private float yaw;
        private float pitch = 12f;
        private Vector3 cameraPivot;
        private Vector3 cameraPivotVelocity;
        private Vector3 cameraOffset;
        private Vector3 cameraOffsetVelocity;
        private Quaternion cameraRotation;
        private float cameraDistance;
        private float cameraDistanceVelocity;
        private bool cameraReady;
        private float gait;
        private float previousGait;
        private bool aim;
        private bool previousAim;
        private bool crouch;
        private bool crouchTogglePending;
        private bool gunAimRequested;
        private bool standingForGunAim;
        private bool cqbrAimApplied;
        private Vector2 previousMoveAxis;
        private bool reload;
        private bool shellLoading;
        private bool reloadFromEmpty;
        private bool rifleTriggerHeld;
        private int rifleShotAfterRaise = -1;
        private int firedAction = -1;
        private int reloadTransaction;
        private int reloadAfterAction = -1;
        private bool paused;
        private bool step;
        private bool ai = true;
        private bool nearView;
        private int debugActor;
        private Threat meleeTarget;
        private int meleeAction;
        private int queuedKnifeAction;
        private float knifeGuardSince = float.NegativeInfinity;
        private string notice = "";
        private GUIStyle style;
        private Font uiFont;

        public int Loaded => weaponRuntime.Query(slot).magazine;
        public int Reserve => weaponRuntime.QueryCompatibleReserveAmmo(Current.Type);
        public int Health => health;
        public ChapterOneRootActor Player => leon;
        public Threat[] Enemies => threats;
        private Weapon Current => weapons[slot];

#if UNITY_EDITOR
        public void Configure(ChapterOneRootActor player, Threat[] enemies, Camera camera, Transform item,
            Transform breakable, Transform window, GameObject gate, AnimationClip opening, AnimationClip closing,
            GameObject flash, NavMeshData mesh, LayerMask world)
        {
            leon = player;
            threats = enemies;
            view = camera;
            pickup = item;
            crate = breakable;
            vault = window;
            door = gate;
            doorOpening = opening;
            doorClosing = closing;
            muzzleFlash = flash;
            navigation = mesh;
            obstructionMask = world;
        }
#endif

        private void Start()
        {
            weaponRuntime.InitializeEquipment(WeaponType.Pistol, 36, 36, 36);
            for (int i = 0; i < weapons.Length; i++)
                weaponRuntime.InitializeWeapon(i, weapons[i].Capacity);
            if (navigation != null)
                meshInstance = NavMesh.AddNavMeshData(navigation);
            if (!TryGetComponent(out aimResolver))
            {
                aimResolver = gameObject.AddComponent<AimResolver>();
            }
            var aimTarget = new GameObject("验收瞄准点").transform;
            aimTarget.SetParent(transform, false);
            aimResolver.aimMask = obstructionMask | (1 << 10);
            aimResolver.Configure(view, aimTarget);
            leon.Equip(Current.Id);
            leon.Contact += OnLeonContact;
            leon.Completed += OnLeonComplete;
            leon.KnifeBladeSample += OnKnifeBladeSample;
            MakePickupFixtures();
            foreach (Threat threat in threats)
            {
                threat.initialHealth = threat.health;
                threat.actor.Equip(threat.equipment);
                threat.actor.Contact += OnEnemyContact;
                threat.actor.Completed += OnEnemyComplete;
            }
        }

        private void OnDestroy()
        {
            if (leon != null)
            {
                leon.KnifeBladeSample -= OnKnifeBladeSample;
            }
            if (partner != null)
            {
                AbortPair();
            }
            if (meshInstance.valid)
                meshInstance.Remove();
            if (paused)
                Time.timeScale = 1f;
            if (uiFont != null)
                Destroy(uiFont);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6))
                TogglePause();
            if (Input.GetKeyDown(KeyCode.Period) && paused)
                Step();
            if (Input.GetKeyDown(KeyCode.F7))
                nearView = !nearView;
            if (Input.GetKeyDown(KeyCode.T))
                ai = !ai;
            if (paused || health <= 0)
                return;

            yaw = Mathf.Repeat(yaw + Input.GetAxisRaw("Mouse X") * 2.2f, 360f);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 1.7f, -15f, 55f);

            if (partner != null)
            {
                if (Input.GetKeyDown(KeyCode.F))
                {
                    RequestPairEscape();
                }
                return;
            }

            UpdatePlayerMovement();
            for (int i = 0; i < weapons.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    RequestEquip(i);
            }

            ApplyTrigger(Input.GetMouseButtonDown(0), Input.GetMouseButton(0));
            if (Input.GetKeyDown(KeyCode.R))
                TryReload();
            if (Input.GetKeyDown(KeyCode.Q) && slot == 3 && !leon.IsBusy)
            {
                PlayKnifeParry(true);
            }
            if (Input.GetKeyDown(KeyCode.F))
                TryMelee();
            if (Input.GetKeyDown(KeyCode.E))
                Interact();
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TryTraverse();
            }
            if (Input.GetKeyDown(KeyCode.G))
            {
                Threat near = ClosestThreat(2.4f);
                if (near != null)
                    BeginPair(near, near.prefix == "Salvador" ? "LeonPair_1200" : "LeonPair_0000");
            }
        }

        private void UpdatePlayerMovement()
        {
            ApplyPlayerMovement(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                Input.GetMouseButton(1), Input.GetKey(KeyCode.LeftControl), Input.GetKey(KeyCode.LeftShift),
                Input.GetKeyDown(KeyCode.C));
        }

        private void ApplyPlayerMovement(Vector2 axis, bool aimPressed, bool walk, bool sprint, bool toggleCrouch)
        {
            axis = Vector2.ClampMagnitude(axis, 1f);
            gunAimRequested = slot < 3 && aimPressed;
            if (StandBeforeGunAim(axis))
            {
                return;
            }
            if (gunAimRequested)
            {
                toggleCrouch = false;
                crouchTogglePending = false;
                aimPressed = !crouch;
            }
            if (toggleCrouch)
            {
                crouchTogglePending = !crouchTogglePending;
            }
            bool rifleReturningToCarry = slot == 2 && !crouch && !leon.RifleCarryReady;
            toggleCrouch = crouchTogglePending && !rifleReturningToCarry
                && (!leon.IsBusy || IsMovementTransition(leon.ActiveTake) || IsAimTransition(leon.ActiveTake));
            if (toggleCrouch)
            {
                crouch = !crouch;
                crouchTogglePending = false;
            }

            if (slot == 3 && aimPressed && !aim)
                knifeGuardSince = Time.time;
            aim = aimPressed;
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Vector3 forward = heading * Vector3.forward;
            Vector3 desired = heading * new Vector3(axis.x, 0f, axis.y);
            bool runningAim = aim && slot < 3 && !crouch && !walk && sprint && axis.y > 0.9f;
            gait = axis.sqrMagnitude < 0.01f ? 0f : (aim && !runningAim) || crouch || walk
                ? 1f : sprint ? 3f : 2f;

            int stance = crouch ? 1 : aim && !runningAim ? 2 + slot : health <= 35 ? 6 : 0;
            if (runningAim && slot == 2 && health > 35)
            {
                stance = 4;
            }
            bool strafe = (aim && !runningAim) || crouch;
            leon.Move(strafe ? forward : desired, strafe ? axis : Vector2.up, gait, stance);

            string activeMovement = null;
            if (leon.MovementTake != null)
            {
                activeMovement = leon.MovementTake.id;
            }
            bool aimingThroughStop = aim && slot < 3 && gait == 0f && !crouch
                && (previousGait >= 2f || activeMovement is "LeonMove_0540" or "LeonMove_0740" or "LeonHurt_0540");
            bool stoppingRun = gait == 0f && previousGait >= 2f && !crouch;
            string weaponPose = null;
            if (runningAim || aimingThroughStop || crouch && aim)
            {
                weaponPose = Current.Prefix + "_0160";
            }
            else if (!aim && slot == 1)
            {
                weaponPose = Current.Prefix + "_Carry";
            }
            if (slot != 2)
            {
                leon.SetWeaponPose(weaponPose, aim);
            }
            bool interruptedStop = gait > 0f && ChapterOneRootActor.IsMovementStop(activeMovement);
            bool resumedFromStop = interruptedStop && (!ChapterOneRootActor.IsDirectionalStop(activeMovement)
                || GetDirectionCode(axis) == activeMovement[^2]);
            if (interruptedStop || gait > 0f && ChapterOneRootActor.IsMovementStart(activeMovement)
                && (gait != previousGait || toggleCrouch || !leon.MovementMatchesAxis()))
            {
                leon.CancelMovement();
            }
            if (ChapterOneRootActor.IsMovingStance(activeMovement)
                && (gait == 0f || toggleCrouch || !IsForwardStep(axis) || !crouch && gait != 2f))
            {
                leon.CancelMovement();
            }
            if (ChapterOneRootActor.IsMovementTurn(activeMovement)
                && (gait != 2f || aim || crouch || !leon.MovementTurnMatchesHeading()))
            {
                leon.CancelMovement();
            }
            if (gait > 0f && (IsAimTransition(leon.ActiveTake) && slot < 2
                || leon.ActiveTake?.id is "LeonMove_0169" or "LeonMove_1169"))
            {
                leon.Cancel();
            }

            if (toggleCrouch)
            {
                if (gait == 0f && !leon.IsBusy)
                {
                    if (crouch)
                    {
                        leon.Play("LeonMove_0169");
                    }
                    else
                    {
                        leon.Play("LeonMove_1169");
                    }
                }
                else if (IsForwardStep(axis) && !ChapterOneRootActor.IsMovingStance(activeMovement)
                    && (crouch && previousGait >= 2f || !crouch && previousGait > 0f && gait == 2f))
                {
                    leon.PlayMovingStance(crouch);
                }
            }
            else if (slot < 2 && aim != previousAim && !runningAim && !stoppingRun && !crouch && (gait == 0f || previousGait > 0f))
            {
                if (gait > 0f && aim)
                    leon.CancelMovement();
                else if (gait == 0f &&
                    (!leon.IsBusy || IsMovementTransition(leon.ActiveTake) || IsAimTransition(leon.ActiveTake)))
                    leon.Play(Current.Prefix + (aim ? "_0140" : "_0158"), true);
            }
            else if (gait > 0f && previousGait == 0f && !resumedFromStop && !leon.BlocksLocomotion)
            {
                if (crouch)
                {
                    PlayDirectionalEdge("LeonMove_14", axis, '0');
                }
                else if (aim && !runningAim)
                {
                    if (slot < 3)
                    {
                        PlayDirectionalEdge(Current.Prefix + "_04", axis, '0');
                    }
                    else if (IsForwardStep(axis))
                    {
                        leon.Play("Knife_0400", true);
                    }
                }
                else if (health <= 35 && gait >= 2f)
                {
                    leon.Play("LeonHurt_0500", true);
                }
                else if (gait >= 3f)
                {
                    leon.Play("LeonMove_0700", true);
                }
                else if (gait >= 2f)
                {
                    leon.Play("LeonMove_0500", true);
                }
                else
                {
                    leon.Play("LeonMove_0400", true);
                }
            }
            else if (gait == 0f && previousGait > 0f && !leon.BlocksLocomotion)
            {
                if (crouch)
                {
                    PlayDirectionalEdge("LeonMove_14", previousMoveAxis, '6');
                }
                else if (health <= 35 && previousGait >= 2f)
                {
                    leon.Play("LeonHurt_0540", true);
                }
                else if (previousGait >= 3f)
                {
                    leon.Play("LeonMove_0740", true);
                }
                else if (previousGait >= 2f)
                {
                    leon.Play("LeonMove_0540", true);
                }
                else if (aim && previousAim)
                {
                    if (slot < 3)
                    {
                        PlayDirectionalEdge(Current.Prefix + "_04", previousMoveAxis, '6');
                    }
                }
                else if (!previousAim)
                {
                    leon.Play("LeonMove_0406", true);
                }
            }
            else if (gait == 2f && previousGait == 2f && !aim && !crouch && health > 35)
            {
                leon.TryTurnWhileRunning();
            }
            bool raiseFromDraw = aim && leon.RifleDrawGripReady;
            bool releaseBeforeShot = !aim && firedAction != leon.ActionId
                && leon.ActiveTake != null && ChapterOneRootActor.IsFirearmShot(leon.ActiveTake.id);
            if (slot == 2 && aim != cqbrAimApplied && !crouch && (raiseFromDraw || releaseBeforeShot || CanChangeStance()))
            {
                PlayCqbrAimTransition();
            }
            if (slot == 3 && aim != previousAim && CanChangeStance())
            {
                PlayKnifeGuardTransition();
            }
            previousAim = aim;
            previousGait = gait;
            previousMoveAxis = axis;
        }

        private static bool IsForwardStep(Vector2 axis) => axis.y > 0.7f && Mathf.Abs(axis.x) < 0.25f;

        private bool CanChangeStance() =>
            !leon.IsBusy || IsMovementTransition(leon.ActiveTake) || IsAimTransition(leon.ActiveTake);

        private static bool IsStandingTake(ChapterOneRootActor.Take action) =>
            action != null && action.id is "LeonMove_1169" or "LeonMove_1500" or "LeonMove_1510";

        private bool IsStandingTransition() => IsStandingTake(leon.ActiveTake) || IsStandingTake(leon.MovementTake);

        private bool StandBeforeGunAim(Vector2 axis)
        {
            if (gunAimRequested && crouch && CanChangeStance())
            {
                bool moving = previousGait > 0f && IsForwardStep(axis);
                Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
                gait = 0f;
                if (moving)
                {
                    gait = 2f;
                }
                int stance = 0;
                if (health <= 35)
                {
                    stance = 6;
                }
                leon.Move(heading * Vector3.forward, Vector2.up, gait, stance);
                if (moving)
                {
                    leon.PlayMovingStance(false);
                }
                else
                {
                    leon.Play("LeonMove_1169", true);
                }
                crouch = false;
                crouchTogglePending = false;
                previousGait = gait;
                previousMoveAxis = axis;
            }

            standingForGunAim = (gunAimRequested || standingForGunAim)
                && IsStandingTransition();
            if (!standingForGunAim)
            {
                return false;
            }
            aim = false;
            previousAim = false;
            return true;
        }

        private void PlayCqbrAimTransition(float actionFade = 0f)
        {
            string id = "CQBR_0158";
            if (aim)
            {
                id = "CQBR_0140";
            }
            if (leon.ContinueAimTransition(aim) || leon.Play(id, true, actionFade: actionFade))
            {
                SetCqbrAimPose(aim);
            }
        }

        private void SetCqbrAimPose(bool aiming)
        {
            cqbrAimApplied = aiming;
            string id = "CQBR_Carry";
            if (aiming)
            {
                id = "CQBR_0160";
            }
            leon.SetWeaponPose(id, aiming);
        }

        private void PlayDirectionalEdge(string prefix, Vector2 axis, char ending)
        {
            char directionCode = GetDirectionCode(axis);
            if (directionCode != '\0')
            {
                leon.Play(prefix + directionCode + ending, true);
            }
        }

        private static char GetDirectionCode(Vector2 axis)
        {
            if (axis.y > 0.85f)
            {
                return '0';
            }
            if (axis.y < -0.85f)
            {
                return '4';
            }
            if (axis.x < -0.85f)
            {
                return '3';
            }
            if (axis.x > 0.85f)
            {
                return '7';
            }
            return '\0';
        }

        private static bool IsMovementTransition(ChapterOneRootActor.Take take) =>
            take != null && ChapterOneRootActor.IsMovementTransition(take.id);

        private void ApplyTrigger(bool pressed, bool held)
        {
            if (pressed && gunAimRequested && crouch && reload)
            {
                EndReload();
                leon.Cancel();
                return;
            }
            bool canUseRifle = slot == 2 && CanFireGun();
            rifleTriggerHeld = canUseRifle && held;
            if (!canUseRifle)
            {
                rifleShotAfterRaise = -1;
            }
            else if (pressed && leon.IsRifleRaisingForShot)
            {
                // 只接受当前举枪动作的点按，取消动作后旧请求不会开火。
                rifleShotAfterRaise = leon.ActionId;
                return;
            }
            bool continueRifleFire = rifleTriggerHeld && !reload && Loaded > 0 && !leon.IsRifleReloadRecovering;
            if (pressed || continueRifleFire)
            {
                TryFire();
            }
        }

        public bool TryFire()
        {
            if (health <= 0 || partner != null)
                return false;
            if (slot == 3)
                return TryKnifeAttack(Input.GetKey(KeyCode.LeftShift));
            if (!CanFireGun())
            {
                return false;
            }
            var active = leon.ActiveTake;
            if (slot == 2 && leon.IsRifleRaisingForShot)
            {
                return false;
            }
            if (!reload && leon.IsBusy && !IsMovementTransition(active) && !IsAimTransition(active))
                return false;
            return StartGunShot();
        }

        private bool StartGunShot()
        {
            if (!CanFireGun())
            {
                return false;
            }
            string action = Loaded == 0 ? "_0520" : Loaded == 1
                ? slot == 0 ? "_0500_wp4000h" : slot == 1 ? "_0500_wp4100h" : "_0500_wp4202h"
                : "_0500";
            float duration = slot == 2 && Loaded > 0 ? RifleShotInterval : 0f;
            if (!leon.Play(Current.Prefix + action, true, duration))
                return false;
            if (slot == 2)
            {
                SetCqbrAimPose(true);
            }
            if (reload)
                EndReload();
            return true;
        }

        private bool CanFireGun() => slot < 3 && gunAimRequested && aim && !crouch && !leon.IsCrouching
            && !standingForGunAim && !IsStandingTransition();

        private static bool IsAimTransition(ChapterOneRootActor.Take take) =>
            take?.id is "SG09R_0140" or "SG09R_0158" or "W870_0140" or "W870_0158" or "CQBR_0140" or "CQBR_0158"
                or "Knife_0140" or "Knife_0158";

        public bool TryReload()
        {
            if (health <= 0 || partner != null || slot == 3 || Reserve <= 0)
                return false;
            string active = leon.ActiveTake?.id;
            if (active != null && (ChapterOneRootActor.IsFirearmShot(active) || active == "W870_0510"))
            {
                reloadAfterAction = leon.ActionId;
                return true;
            }
            if (leon.IsBusy && !IsMovementTransition(leon.ActiveTake) && !IsAimTransition(leon.ActiveTake))
                return false;
            return BeginReload();
        }

        private bool BeginReload()
        {
            if (Loaded >= Current.Capacity || Reserve <= 0)
                return false;
            reloadFromEmpty = Loaded == 0;
            string action = slot == 1 ? "W870_0805" : slot == 0 && !reloadFromEmpty ? "SG09R_0910" : Current.Prefix + "_0810";
            if (crouch)
            {
                if (slot == 0) action = reloadFromEmpty ? "LeonFireExtra_1810_wp4000" : "LeonFireExtra_1910_wp4000";
                if (slot == 1) action = "LeonFireExtra_1805_wp4100";
                if (slot == 2) action = "LeonFireExtra_1810_wp4402";
            }

            if (!leon.Play(action, true))
                return false;
            reloadTransaction = leon.ActionId;
            reload = weaponRuntime.BeginReload(slot, Current.Capacity, reloadTransaction);
            if (!reload)
                leon.Cancel();
            shellLoading = reload && slot == 1;
            return reload;
        }

        public bool RequestEquip(int next)
        {
            if (next == slot || next < 0 || next >= weapons.Length || health <= 0 || partner != null)
                return false;
            if (leon.IsBusy && !reload && !IsMovementTransition(leon.ActiveTake) && !IsAimTransition(leon.ActiveTake)
                && !ChapterOneRootActor.IsKnifeTail(leon.ActiveTake.id))
                return false;
            EndReload();
            reloadAfterAction = -1;
            pendingSlot = next;
            string holster = Current.Prefix + "_0551";
            if (crouch && leon.TryGetTake(Current.Prefix + "_1551", out var crouchHolster))
            {
                holster = crouchHolster.id;
            }
            if (!leon.Play(holster, true))
            {
                FinishEquip();
            }
            return true;
        }

        private void FinishEquip()
        {
            slot = pendingSlot;
            pendingSlot = -1;
            knifeGuardSince = float.NegativeInfinity;
            rifleTriggerHeld = false;
            rifleShotAfterRaise = -1;
            weaponRuntime.CommitEquipment(Current.Type);
            leon.Equip(Current.Id);
            if (slot == 2)
            {
                SetCqbrAimPose(false);
            }
            string draw = Current.Prefix + "_0543";
            if (slot == 3)
            {
                draw = "Knife_Draw";
                if (crouch && leon.TryGetTake("Knife_CrouchDraw", out var crouchDraw))
                {
                    draw = crouchDraw.id;
                }
            }
            else if (crouch && leon.TryGetTake(Current.Prefix + "_1541", out var crouchDraw))
            {
                draw = crouchDraw.id;
            }
            else if (leon.TryGetTake(Current.Prefix + "_0541", out var shortDraw))
            {
                draw = shortDraw.id;
            }
            leon.Play(draw, true);
        }

        private void OnLeonContact(ChapterOneRootActor actor, ChapterOneRootActor.Take action, int id, int frame)
        {
            if (actor.ActionId != id)
                return;

            if (ResolveInteractionContact(action.id, id))
            {
                return;
            }

            if (action.id.StartsWith(Current.Prefix + "_0500", StringComparison.Ordinal))
            {
                if (CanFireGun() && weaponRuntime.TryFire(slot, false, Time.time, 0f) == RejectionReason.None)
                {
                    firedAction = id;
                    if (slot == 2 && Loaded == 0)
                    {
                        leon.SetRifleBoltOpen(true);
                    }
                    FireHit();
                }
                return;
            }

            if (reload && (action.id.Contains("81", StringComparison.Ordinal) || action.id.Contains("91", StringComparison.Ordinal)))
            {
                bool insertingShell = action.id is "W870_0810" or "LeonFireExtra_1810_wp4100";
                bool contact = slot != 1 || insertingShell;
                if (contact)
                {
                    int loaded = weaponRuntime.CommitReload(slot, Current.Capacity, reloadTransaction, Reserve, slot == 1,
                        count => weaponRuntime.ConsumptionCompatibleReserveAmmo(Current.Type, count));
                    if (slot == 2 && loaded > 0)
                    {
                        leon.SetRifleBoltOpen(false);
                    }
                    if (insertingShell && (Loaded >= Current.Capacity || Reserve == 0))
                    {
                        ExitShellReload();
                    }
                }
            }

            if (ChapterOneRootActor.IsBodyMelee(action.id))
            {
                ResolveMeleeContact(action.id, id);
            }
        }

        private void OnLeonComplete(ChapterOneRootActor actor, ChapterOneRootActor.Take action, int id)
        {
            if (CompleteTraversal(action, id))
            {
                return;
            }
            if (doorPhase == DoorPhase.PlayerOpening && id == doorAction)
            {
                FinishDoorOpening();
            }
            if (health == 0 && action.id.StartsWith("LeonDamage_09", StringComparison.Ordinal))
            {
                leon.HoldTerminal();
                return;
            }
            if (partner != null)
            {
                return;
            }

            if (ChapterOneRootActor.IsLeonStagger(action.id))
            {
                ResumeCombatAfterStagger();
                return;
            }

            if (pendingSlot >= 0 && ChapterOneRootActor.IsHolster(action.id))
            {
                FinishEquip();
                return;
            }
            if (action.id == "CQBR_0543" && slot == 2 && aim && !crouch)
            {
                PlayCqbrAimTransition();
                return;
            }
            if (id == rifleShotAfterRaise)
            {
                rifleShotAfterRaise = -1;
                if (slot == 2 && StartGunShot())
                {
                    return;
                }
            }

            if (FinishKnifeAction(action.id))
            {
                return;
            }

            if (shellLoading)
            {
                if (action.id is "W870_0805" or "LeonFireExtra_1805_wp4100")
                    leon.Play(crouch ? "LeonFireExtra_1810_wp4100" : "W870_0810", true);
                else if (action.id is "W870_0810" or "LeonFireExtra_1810_wp4100")
                {
                    if (Loaded < Current.Capacity && Reserve > 0)
                    {
                        leon.Play(action.id, true);
                    }
                    else
                    {
                        ExitShellReload();
                    }
                }
                else
                {
                    EndReload();
                }
                return;
            }

            EndReload();
            if (slot == 2 && action.id is "CQBR_0810" or "LeonFireExtra_1810_wp4402")
            {
                SetCqbrAimPose(aim && !crouch);
            }
            if (slot == 1 && firedAction == id && leon.Play("W870_0510", true))
            {
                if (reloadAfterAction == id)
                    reloadAfterAction = leon.ActionId;
                return;
            }
            if (reloadAfterAction == id)
            {
                reloadAfterAction = -1;
                if (BeginReload())
                    return;
            }
            if (slot == 2 && aim && rifleTriggerHeld && firedAction == id && Loaded > 0 && !crouchTogglePending)
            {
                StartGunShot();
            }
        }

        private void EndReload()
        {
            if (reload)
                weaponRuntime.CancelReload(slot);
            reload = false;
            shellLoading = false;
            reloadTransaction = -1;
        }

        private void ExitShellReload()
        {
            string action = "W870_0817";
            if (crouch && reloadFromEmpty)
            {
                action = "LeonFireExtra_1815_wp4100";
            }
            else if (crouch)
            {
                action = "LeonFireExtra_1817_wp4100";
            }
            else if (reloadFromEmpty)
            {
                action = "W870_0815";
            }
            if (!leon.Play(action, true))
            {
                EndReload();
            }
        }

        private void FireHit()
        {
            Transform origin = leon.Muzzle;
            if (muzzleFlash != null && origin != null)
            {
                GameObject flash = Instantiate(muzzleFlash, origin.position, origin.rotation);
                Destroy(flash, 1f);
            }

            aimResolver.Resolve(45f);
            if (!aimResolver.HasWorldHit)
            {
                return;
            }
            RaycastHit hit = aimResolver.CurrentHit;
            if (hit.collider == null)
            {
                return;
            }

            foreach (Threat threat in threats)
            {
                if (threat.actor && hit.collider.gameObject == threat.actor.gameObject && !threat.actor.IsTerminal)
                {
                    Vector3 local = threat.actor.transform.InverseTransformPoint(hit.point);
                    bool leg = local.y <= threat.actor.GetComponent<CapsuleCollider>().height * 0.40f;
                    bool left = local.x < 0f;
                    ApplyEnemyDamage(threat, slot == 1 ? 72 : slot == 2 ? 28 : 34,
                        leg ? left ? "_0420" : "_0520" : "_0110", leg, left);
                    break;
                }
            }
        }

        public void DamageEnemy(Threat threat, int damage) => ApplyEnemyDamage(threat, damage, "_0110");

        private void ApplyEnemyDamage(Threat threat, int damage, string reaction, bool leg = false, bool left = false)
        {
            if (threat.health <= 0 || !threat.actor)
                return;
            if (IsInMeleeReaction(threat))
            {
                threat.health = Mathf.Max(0, threat.health - damage);
                return;
            }
            if (partner == threat)
                AbortPair();
            threat.health = Mathf.Max(0, threat.health - damage);
            threat.alerted = true;
            threat.actor.Move(Vector3.zero, Vector2.zero, 0f, 0);
            if (DeferEnemyWindowDamage(threat, reaction, leg, left))
            {
                return;
            }
            PlayEnemyDamageReaction(threat, reaction, leg, left);
        }

        private void PlayEnemyDamageReaction(Threat threat, string reaction, bool leg, bool left)
        {
            bool staggered = HasLegStagger(threat);
            threat.legStaggerAction = 0;
            if (StartEnemyGroundDamage(threat, leg, left, staggered))
            {
                return;
            }
            if (threat.health == 0)
                threat.actor.Play(threat.prefix + (threat.prefix == "Salvador" ? "_0000_dead_2nd_wp5300" : "_0000_dead_0th_com"), true);
            else if (threat.actor.Play(threat.prefix + reaction, true))
            {
                if (leg && threat.prefix == "Ganado")
                {
                    threat.leftLeg = left;
                    threat.legStaggerAction = threat.actor.ActionId;
                }
            }
            else if (reaction != "_0110")
                threat.actor.Play(threat.prefix + "_0110", true);
        }

        private void OnEnemyContact(ChapterOneRootActor actor, ChapterOneRootActor.Take action, int id, int frame)
        {
            Threat source = Array.Find(threats, item => item.actor == actor);
            if (source == null || action.id != source.attack || actor.ActionId != id || health <= 0 || partner != null)
                return;
            if (leon.TraversalOpeningOccupied)
            {
                return;
            }

            Vector3 toLeon = leon.transform.position - actor.transform.position;
            if (toLeon.magnitude > 1.9f || Vector3.Angle(actor.transform.forward, toLeon) > 65f
                || !HasEnemyApproachLine(actor))
            {
                return;
            }

            if (TryParryEnemy(source, -toLeon))
            {
                return;
            }

            bool heavy = source.prefix == "Salvador";
            int damage = 16;
            if (heavy)
            {
                damage = 35;
            }
            ApplyEnemyStrike(toLeon, damage, heavy);
        }

        private void ApplyEnemyStrike(Vector3 toLeon, int damage, bool heavy)
        {
            health = Mathf.Max(0, health - damage);
            EndReload();
            pendingSlot = -1;
            reloadAfterAction = -1;
            rifleShotAfterRaise = -1;
            queuedKnifeAction = 0;
            thrustHitAction = 0;
            crouch = false;
            crouchTogglePending = false;
            standingForGunAim = false;
            if (health == 0)
            {
                leon.Play("LeonDamage_0900", true);
                return;
            }

            Vector3 incoming = Quaternion.Inverse(leon.MotionRotation) * -toLeon;
            leon.Play(SelectStagger(incoming, heavy), true);
        }

        private static string SelectStagger(Vector3 incoming, bool heavy)
        {
            int direction = 4;
            if (Mathf.Abs(incoming.x) > Mathf.Abs(incoming.z))
            {
                direction = 0;
                if (incoming.x > 0f)
                {
                    direction = 2;
                }
            }
            else if (incoming.z < 0f)
            {
                direction = 6;
            }
            int severity = 100;
            if (heavy)
            {
                severity = 110;
            }
            return $"LeonDamage_{severity + direction:D4}";
        }

        private void ResumeCombatAfterStagger()
        {
            if (slot == 2)
            {
                if (aim)
                {
                    PlayCqbrAimTransition(0.18f);
                }
                else
                {
                    SetCqbrAimPose(false);
                }
            }
            else if (slot == 3 && aim)
            {
                PlayKnifeGuardTransition(0.18f);
            }
            else if (slot < 2 && aim && gait == 0f)
            {
                leon.Play(Current.Prefix + "_0140", true, actionFade: 0.18f);
            }
        }

        private void OnEnemyComplete(ChapterOneRootActor actor, ChapterOneRootActor.Take action, int id)
        {
            Threat threat = Array.Find(threats, item => item.actor == actor);
            if (threat == null || threat == partner && actor.IsPaired)
                return;
            if (CompleteEnemyWindow(threat, id))
            {
                return;
            }
            if (AdvanceEnemyGround(threat, id) || AdvanceMeleeReaction(threat, action.id, id))
            {
                return;
            }
            if (threat.legStaggerAction == id && (meleeTarget != threat || meleeAction != leon.ActionId))
                threat.legStaggerAction = 0;
            if (threat.health == 0)
            {
                actor.HoldTerminal();
                actor.DropEquipment();
            }
        }

        private void FixedUpdate()
        {
            if (step)
            {
                step = false;
                Time.timeScale = 0f;
            }
            UpdatePair();
            UpdateTraversal();
            UpdateKnifeAction();
            foreach (Threat threat in threats)
            {
                if (threat == partner)
                {
                    continue;
                }
                UpdateMeleeReaction(threat);
                UpdateEnemyWindow(threat);
                if (UpdateEnemyGround(threat) || !threat.actor || threat.health <= 0 || threat.actor.BlocksLocomotion
                    || (threat == meleeTarget && meleeAction == leon.ActionId))
                {
                    continue;
                }
                Vector3 toward = Vector3.ProjectOnPlane(leon.transform.position - threat.actor.transform.position, Vector3.up);
                float distance = toward.magnitude;
                if (!ai || health <= 0 || distance > 7.8f)
                {
                    threat.actor.DriveEnemy(threat.prefix, Vector3.zero, Vector3.zero, false, false);
                    continue;
                }
                if (!threat.alerted)
                {
                    threat.alerted = true;
                    threat.actor.Move(toward, Vector2.up, 0f, 0);
                    threat.actor.Play(threat.alert);
                    continue;
                }
                if (DriveEnemyThroughWindow(threat))
                {
                    continue;
                }

                Vector3 steer = toward;
                bool direct = HasEnemyApproachLine(threat.actor);
                if (!direct || distance > ChapterOneRootActor.EnemyAttackRange)
                {
                    steer = PathHeading(threat, toward);
                }
                bool settledInRange = threat.actor.DriveEnemy(threat.prefix, steer, toward, true, direct);
                if (settledInRange && Time.time >= threat.attackAfter
                    && Vector3.Angle(threat.actor.transform.forward, toward) < 32f
                    && threat.actor.Play(threat.attack))
                {
                    threat.attackAfter = Time.time + 1.9f;
                }
            }
        }

        private bool HasEnemyApproachLine(ChapterOneRootActor enemy)
        {
            Vector3 height = Vector3.up * 0.9f;
            return !Physics.Linecast(enemy.MotionPosition + height, leon.MotionPosition + height,
                obstructionMask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 PathHeading(Threat threat, Vector3 fallback)
        {
            if (!meshInstance.valid)
                return fallback;
            Vector3 at = threat.actor.transform.position;
            if (Time.time >= threat.repathAfter)
            {
                threat.repathAfter = Time.time + 0.25f;
                var path = new NavMeshPath();
                threat.corners = NavMesh.SamplePosition(at, out var from, 1.2f, NavMesh.AllAreas)
                    && NavMesh.SamplePosition(leon.transform.position, out var to, 1.6f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path)
                    ? path.corners : Array.Empty<Vector3>();
            }

            foreach (Vector3 corner in threat.corners ?? Array.Empty<Vector3>())
            {
                Vector3 local = Vector3.ProjectOnPlane(corner - at, Vector3.up);
                if (local.sqrMagnitude > 0.21f)
                    return local;
            }
            return Vector3.zero;
        }

        private Threat ClosestThreat(float radius, Predicate<Threat> eligible = null)
        {
            Threat near = null;
            foreach (Threat threat in threats)
            {
                if (threat.health <= 0 || !threat.actor || (eligible != null && !eligible(threat)))
                    continue;
                float distance = Vector3.Distance(leon.MotionPosition, threat.actor.MotionPosition);
                if (distance < radius)
                {
                    near = threat;
                    radius = distance;
                }
            }
            return near;
        }

        private bool InMeleeReach(Threat target, float radius)
        {
            if (target == null || !target.actor || target.health <= 0)
                return false;
            Vector3 offset = target.actor.transform.position - leon.transform.position;
            return offset.sqrMagnitude <= radius * radius && Vector3.Angle(leon.transform.forward, offset) < 75f;
        }

        private void LateUpdate()
        {
            UpdateDoor();
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            bool handView = nearView && leon.RightHand != null;
            Vector3 targetPivot = handView ? leon.RightHand.position
                : leon.transform.position + Vector3.up * (crouch ? 1.16f : 1.52f);
            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 targetOffset = new(aim ? 0.45f : 0.62f, 0.14f, handView ? -0.85f : aim ? -1.75f : -3f);
            float dt = Time.deltaTime;

            if (!cameraReady)
            {
                cameraPivot = targetPivot;
                cameraOffset = targetOffset;
                cameraRotation = targetRotation;
                cameraDistance = targetOffset.magnitude;
                cameraReady = true;
            }
            else if (dt > 0f)
            {
                if ((cameraPivot - targetPivot).sqrMagnitude > 6.25f)
                {
                    cameraPivot = targetPivot;
                    cameraPivotVelocity = Vector3.zero;
                }
                else
                    cameraPivot = Vector3.SmoothDamp(cameraPivot, targetPivot, ref cameraPivotVelocity, 0.075f, Mathf.Infinity, dt);

                cameraOffset = Vector3.SmoothDamp(cameraOffset, targetOffset, ref cameraOffsetVelocity, 0.14f, Mathf.Infinity, dt);
                cameraRotation = Quaternion.Slerp(cameraRotation, targetRotation, 1f - Mathf.Exp(-dt / 0.035f));
            }

            Vector3 offset = cameraRotation * cameraOffset;
            Vector3 direction = offset.normalized;
            float allowed = offset.magnitude;
            if (Physics.SphereCast(cameraPivot, 0.16f, direction, out var obstacle, allowed,
                    obstructionMask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(0f, obstacle.distance - 0.07f);

            if (allowed < cameraDistance)
            {
                // 遇到遮挡先缩短镜头臂，移开后再平滑恢复。
                cameraDistance = allowed;
                cameraDistanceVelocity = 0f;
            }
            else if (dt > 0f)
                cameraDistance = Mathf.SmoothDamp(cameraDistance, allowed, ref cameraDistanceVelocity, 0.17f, Mathf.Infinity, dt);

            view.transform.SetPositionAndRotation(cameraPivot + direction * cameraDistance, cameraRotation);
            if (aim && aimResolver != null)
            {
                aimResolver.Resolve(45f);
            }
        }

        private void TogglePause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        private void Step()
        {
            if (!paused)
                return;
            Time.timeScale = 1f;
            step = true;
        }

        private void OnGUI()
        {
            if (style == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 15);
                style = new GUIStyle(GUI.skin.label) { font = uiFont, fontSize = 15, wordWrap = true };
            }

            GUILayout.BeginArea(new Rect(15, 15, 525, 310), GUI.skin.box);
            GUILayout.Label($"第一章动画闭环   生命 {health}   {Current.Prefix}   {Loaded}/{Reserve}", style);
            GUILayout.Label("WASD 移动 · Ctrl 步行 · Shift 冲刺 · C 蹲下 · 右键瞄准 · 左键攻击", style);
            GUILayout.Label("1–4 换武器 · R 换弹 · E 交互 · 空格翻窗/跳落 · F 体术/挣脱 · G 试配对", style);
            GUILayout.Label("F6 暂停 · . 单步 · F7 手部近景 · T 敌人开关", style);
            if (slot == 3)
                GUILayout.Label("小刀 · 连点左键连段 · Shift+左键前刺 · 右键防守 · Q 试完美弹反", style);
            if (slot == 3 && CanStartMelee() && ClosestThreat(1.85f, item => GroundFinishId(item) != null) != null)
            {
                GUILayout.Label("F 小刀处决 · 靠近目标的入场侧", style);
            }
            else if (CanStartMelee() && ClosestThreat(LowerMeleeReach, item => LegMeleeReady(item)
                && InMeleeReach(item, LowerMeleeReach) && HasEnemyApproachLine(item.actor)) != null)
            {
                GUILayout.Label("F 下段体术", style);
            }
            if (pairGroundCommitted)
            {
                GUILayout.Label($"处决接触 {pairGroundSampledFrame:F2} 源帧　刀刃距离 {pairGroundContactGap * 100f:F1} cm", style);
            }
            if (pairLegBite && pairBiteContactHandled)
            {
                GUILayout.Label($"咬腿接触 {pairBiteContactFrame:F2} 源帧　小腿距离 {pairBiteContactGap * 100f:F1} cm", style);
            }
            ChapterOneRootActor debug = debugActor == 0 ? leon : threats[debugActor - 1].actor;
            if (debug != null)
            {
                GUILayout.Label($"{debug.ActorName}　{debug.StateName}　支撑脚 {debug.SupportFoot}　漂移估算 {debug.FootDrift * 100:F1} cm", style);
                var take = debug.ActiveTake;
                int frame = take == null ? 0 : Mathf.FloorToInt(debug.ActionTime * take.body.frameRate);
                GUILayout.Label($"请求 {debug.RequestedStep.magnitude * 100:F1} cm　实际 {debug.ActualStep.magnitude * 100:F1} cm　动作帧 {frame}", style);
                if (take != null && take.contactFrames.Length > 0)
                    GUILayout.Label("接触帧 " + string.Join(", ", take.contactFrames), style);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("切换观察角色"))
                    debugActor = (debugActor + 1) % (threats.Length + 1);
                if (GUILayout.Button(paused ? "继续" : "暂停")) TogglePause();
                if (GUILayout.Button("单步")) Step();
                GUILayout.EndHorizontal();
            }
            if (partner != null)
            {
                GUILayout.Label(PairPrompt(), style);
            }
            else if (!string.IsNullOrEmpty(notice))
                GUILayout.Label(notice, style);
            GUILayout.EndArea();
            if (aim)
                GUI.Box(new Rect(Screen.width * 0.5f - 2, Screen.height * 0.5f - 2, 4, 4), GUIContent.none);
        }
    }
}


using GameArchitecture;
using UnityEngine;

namespace RE4.ChapterOne
{
    public sealed partial class ChapterOneLoop
    {
        private const string DoorOpenTake = "LeonDoors_0007";
        private const int DoorReleaseFrame = 270;

        private enum DoorPhase
        {
            Closed,
            PlayerOpening,
            ReturningClosed,
            FinishingOpen,
            Open,
            Closing
        }

        private Transform interactionTarget;
        private int interactionAction;
        private Transform[] pickupFixtures;
        private DoorPhase doorPhase;
        private int doorAction;
        private float doorSourceTime;

        private void Interact()
        {
            if (leon.BlocksLocomotion || leon.IsTerminal || reload || pendingSlot >= 0 || partner != null)
            {
                return;
            }

            Vector3 at = leon.MotionPosition;
            Transform item = ClosestPickup(at);
            if (item != null)
            {
                TryStartPickup(item, at);
            }
            else if (crate != null && crate.gameObject.activeSelf && Vector3.Distance(at, crate.position) < 1.65f)
            {
                BeginItemInteraction(crate, "LeonInteract_0109");
            }
            else if (door != null && Vector3.Distance(at, door.transform.position) < 2.1f)
            {
                InteractWithDoor(at);
            }
        }

        private void BeginItemInteraction(Transform target, string take)
        {
            if (leon.IsBusy || !leon.Play(take))
            {
                return;
            }
            interactionTarget = target;
            interactionAction = leon.ActionId;
        }

        private bool ResolveInteractionContact(string take, int action)
        {
            if (action != interactionAction || interactionTarget == null)
            {
                return false;
            }

            Transform target = interactionTarget;
            interactionTarget = null;
            if (!target.gameObject.activeSelf)
            {
                return true;
            }
            if (ChapterOneRootActor.PickupContactFrame(take) >= 0)
            {
                Vector3 hand = leon.PickupHandPosition;
                if ((hand - target.position).sqrMagnitude > 0.14f * 0.14f
                    || Physics.Linecast(hand, target.position, obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    notice = "拾取接触未到达";
                    return true;
                }
                WeaponType type = Current.Type;
                if (type == WeaponType.Knife)
                {
                    type = WeaponType.Pistol;
                }
                weaponRuntime.IncreaseCompatibleReserveAmmo(type, 8);
                target.gameObject.SetActive(false);
                notice = "";
            }
            else if (target == crate && take == "LeonInteract_0109")
            {
                target.gameObject.SetActive(false);
            }
            return true;
        }

        private void MakePickupFixtures()
        {
            if (pickup == null)
            {
                return;
            }
            Vector3 basePosition = pickup.position;
            var middle = Instantiate(pickup, transform);
            var high = Instantiate(pickup, transform);
            pickupFixtures = new[] { pickup, middle, high };
            PlacePickupFixture(pickup, "低位弹药拾取", basePosition, 0.57f);
            PlacePickupFixture(middle, "中位弹药拾取", basePosition + Vector3.left * 1.95f, 0.92f);
            PlacePickupFixture(high, "高位弹药拾取", basePosition + Vector3.left * 3.90f, 1.41f);
        }

        private void PlacePickupFixture(Transform item, string label, Vector3 at, float height)
        {
            float ground = at.y - 0.16f;
            if (Physics.Raycast(at + Vector3.up, Vector3.down, out var support, 2f,
                obstructionMask, QueryTriggerInteraction.Ignore))
            {
                ground = support.point.y;
            }
            item.name = label;
            item.position = new Vector3(at.x, ground + height, at.z);

            float shelfHeight = height - item.lossyScale.y * 0.5f;
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.name = label + "台面";
            pedestal.transform.SetParent(transform, false);
            pedestal.transform.position = new Vector3(at.x, ground + shelfHeight * 0.5f, at.z);
            pedestal.transform.localScale = new Vector3(0.36f, shelfHeight, 0.52f);
        }

        private Transform ClosestPickup(Vector3 at)
        {
            if (pickupFixtures == null)
            {
                return null;
            }
            Transform closest = null;
            float distance = 1.15f * 1.15f;
            foreach (Transform item in pickupFixtures)
            {
                if (item == null || !item.gameObject.activeInHierarchy)
                {
                    continue;
                }
                float next = Vector3.ProjectOnPlane(item.position - at, Vector3.up).sqrMagnitude;
                if (next < distance)
                {
                    closest = item;
                    distance = next;
                }
            }
            return closest;
        }

        private void TryStartPickup(Transform item, Vector3 at)
        {
            if (crouch || leon.IsBusy && leon.ActiveTake != leon.MovementTake)
            {
                return;
            }
            if (meleeMotion == null)
            {
                meleeMotion = Resources.Load<ChapterOneMeleeMotion>(ChapterOneMeleeMotion.ResourceName);
            }
            if (meleeMotion == null)
            {
                notice = "拾取手部轨道缺失";
                return;
            }

            ChapterOneRootActor.Take chosen = null;
            ChapterOneMeleeMotion.Track chosenTrack = null;
            Pose start = default;
            float cost = float.PositiveInfinity;
            Consider(ChapterOneRootActor.LowPickup);
            foreach (var pickupTake in ChapterOneRootActor.PickupTakes)
            {
                Consider(pickupTake.id);
            }
            if (chosen == null)
            {
                notice = "靠近拾取点并留出伸手空间";
                return;
            }

            float contactTime = chosen.contactFrames[0] / chosen.body.frameRate;
            if (leon.PlayPickup(chosen, chosenTrack.root, start, Mathf.Min(0.20f, contactTime - 0.035f)))
            {
                interactionTarget = item;
                interactionAction = leon.ActionId;
                notice = "";
            }

            void Consider(string id)
            {
                if (!leon.TryGetTake(id, out var take) || !meleeMotion.TryGetTrack(take.body, out var track))
                {
                    return;
                }
                Vector3 contact = track.SamplePoint(take.contactFrames[0] / take.body.frameRate);
                Vector3 relative = Quaternion.Inverse(leon.MotionRotation) * (item.position - at);
                float heightError = Mathf.Abs(relative.y - contact.y);
                float turn = Mathf.DeltaAngle(Mathf.Atan2(contact.x, contact.z) * Mathf.Rad2Deg,
                    Mathf.Atan2(relative.x, relative.z) * Mathf.Rad2Deg);
                if (heightError > 0.115f || Mathf.Abs(turn) > 50f)
                {
                    return;
                }
                Quaternion facing = leon.MotionRotation * Quaternion.Euler(0f, Mathf.Clamp(turn, -22f, 22f), 0f);
                Vector3 position = item.position - facing * contact;
                position.y = at.y;
                float shift = (position - at).magnitude;
                float score = shift + heightError + 0.009f * Mathf.Abs(turn);
                const float clearance = 0.04f;
                Vector3 offset = Vector3.up * clearance;
                Vector3 end = position + facing * track.root.positions[track.root.positions.Length - 1];
                if (score >= cost || shift > 0.36f || !leon.CanAlignForPair(position + offset, clearance)
                    || !leon.CanAlignForPair(end + offset, clearance)
                    || Physics.Linecast(position + Vector3.up * 1.20f, item.position,
                        obstructionMask, QueryTriggerInteraction.Ignore))
                {
                    return;
                }
                chosen = take;
                chosenTrack = track;
                start = new Pose(position, facing);
                cost = score;
            }
        }

        private void InteractWithDoor(Vector3 at)
        {
            if (doorPhase == DoorPhase.Open && doorClosing != null)
            {
                doorSourceTime = 0f;
                doorPhase = DoorPhase.Closing;
                return;
            }
            if (doorPhase != DoorPhase.Closed || doorOpening == null
                || !leon.TryGetTake(DoorOpenTake, out var take))
            {
                return;
            }
            if (pairMotion == null)
            {
                pairMotion = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
            }
            if (pairMotion == null || !pairMotion.TryGetTrack(take.body, out var track))
            {
                notice = "开门角色根轨道缺失";
                return;
            }

            Transform stage = door.transform;
            Vector3 start = stage.TransformPoint(take.pairOrigin);
            Quaternion facing = stage.rotation * Quaternion.Euler(0f, 180f, 0f);
            Vector3 toward = Vector3.ProjectOnPlane(start - at, Vector3.up);
            if (crouch || toward.sqrMagnitude > 0.65f * 0.65f
                || Vector3.Dot(at - stage.position, stage.forward) < 0.35f
                || Quaternion.Angle(leon.MotionRotation, facing) > 60f
                || !leon.CanAlignForPair(start + Vector3.up * 0.04f, 0.04f))
            {
                notice = "站到双开门正面并面向门扇";
                return;
            }
            // 避免把已贴地的刚体拉入门底地板。
            start.y = at.y;
            if (!leon.PlayPaired(take, track, new Pose(start, facing), 0.34f))
            {
                return;
            }
            leon.ReleasePairAfterTail();
            doorAction = leon.ActionId;
            doorSourceTime = 0f;
            doorPhase = DoorPhase.PlayerOpening;
            doorOpening.SampleAnimation(door, 0f);
            notice = "";
        }

        private void UpdateDoor()
        {
            if (interactionTarget != null && interactionAction != leon.ActionId)
            {
                interactionTarget = null;
            }
            if (door == null || doorPhase == DoorPhase.Closed || doorPhase == DoorPhase.Open)
            {
                return;
            }

            if (doorPhase == DoorPhase.PlayerOpening)
            {
                if (doorAction == leon.ActionId)
                {
                    doorSourceTime = Mathf.Min(leon.ActionTime, doorOpening.length);
                    doorOpening.SampleAnimation(door, doorSourceTime);
                    return;
                }
                // 原版可取消帧之后，门扇独立走完剩余的开门轨道。
                if (doorSourceTime * doorOpening.frameRate >= DoorReleaseFrame)
                {
                    doorPhase = DoorPhase.FinishingOpen;
                }
                else
                {
                    doorPhase = DoorPhase.ReturningClosed;
                }
                return;
            }
            if (doorPhase == DoorPhase.ReturningClosed)
            {
                doorSourceTime = Mathf.Max(0f, doorSourceTime - Time.deltaTime);
                doorOpening.SampleAnimation(door, doorSourceTime);
                if (doorSourceTime == 0f)
                {
                    doorPhase = DoorPhase.Closed;
                }
                return;
            }
            if (doorPhase == DoorPhase.FinishingOpen)
            {
                doorSourceTime = Mathf.Min(doorSourceTime + Time.deltaTime, doorOpening.length);
                doorOpening.SampleAnimation(door, doorSourceTime);
                if (doorSourceTime >= doorOpening.length)
                {
                    FinishDoorOpening();
                }
                return;
            }

            doorSourceTime = Mathf.Min(doorSourceTime + Time.deltaTime, doorClosing.length);
            doorClosing.SampleAnimation(door, doorSourceTime);
            if (doorSourceTime >= doorClosing.length)
            {
                doorPhase = DoorPhase.Closed;
            }
        }

        private void FinishDoorOpening()
        {
            if (door != null)
            {
                doorSourceTime = doorOpening.length;
                doorOpening.SampleAnimation(door, doorSourceTime);
            }
            doorPhase = DoorPhase.Open;
        }
    }
}

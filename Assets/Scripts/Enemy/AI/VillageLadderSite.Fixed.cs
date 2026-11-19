using System.Collections.Generic;
using GameArchitecture;
using UnityEngine;

public sealed partial class VillageLadderSite
{
    private sealed class FixedClimber
    {
        internal ActionHandle Action;
        internal readonly int Version;
        internal bool Falling;

        internal FixedClimber(ActionHandle action, int version)
        {
            Action = action;
            Version = version;
        }
    }

    private VillageTowerTraversal tower;
    private readonly Dictionary<VillagerController, FixedClimber> fixedClimbers = new();
    private VillagerController fixedExitOwner;
    private bool candidateDrop;

    internal bool IsFixed => tower != null;
    internal bool IsRouteOpen => !IsFixed || !tower.HasCollapsed;

    internal static VillageTraversalRegion ReadTargetRegion(HouseThreeRegionNavigation regions, Vector3 position)
    {
        foreach (var site in sites)
        {
            if (site.IsFixed && site.IsRouteOpen && site.tower.ContainsPlatform(position))
            {
                return VillageTraversalRegion.TowerPlatform;
            }
        }

        if (regions != null)
        {
            return regions.TreadRegion(position);
        }

        return VillageTraversalRegion.HouseOuterGround;
    }

    internal void ConfigureFixed(VillageTowerTraversal owner, VillageLadderConfiguration config,
        HouseThreeRegionNavigation regions, EnemyAIManager enemies)
    {
        tower = owner;
        layout = new VillageLadderConfiguration.Site
        {
            displayName = owner.name,
            foot = owner.ladderFoot.position,
            head = owner.ladderHead.position,
            exit = owner.ladderExit.position,
            approachDistance = 0.72f
        };
        configuration = config;
        navigation = regions;
        group = enemies;
        outward = Vector3.ProjectOnPlane(owner.ladderFoot.forward, Vector3.up).normalized;
        ladderLength = Vector3.Distance(Foot, Head);
        State = LadderState.Ready;
    }

    internal void CloseFixedRoute()
    {
        CancelSelection();
        ClearAssignment("塔楼楼板坍塌");
        foreach (var entry in fixedClimbers)
        {
            var actor = entry.Key;
            if (actor != null && actor.Agent != null)
            {
                actor.Agent.InvalidatePursuitResolutionCache();
            }
        }
    }

    private bool CanMountFixedLadder()
    {
        if (tower.HasCollapsed)
        {
            return false;
        }

        foreach (var entry in fixedClimbers)
        {
            if (entry.Key != null && entry.Key.transform.position.y < Foot.y + tower.climberSpacing)
            {
                return false;
            }
        }
        return true;
    }

    internal bool CanAdvanceClimber(VillagerController actor)
    {
        if (!IsFixed || !fixedClimbers.TryGetValue(actor, out var current))
        {
            return true;
        }

        foreach (var entry in fixedClimbers)
        {
            if (entry.Key == null || entry.Value.Falling || entry.Value.Version >= current.Version)
            {
                continue;
            }

            float gap = entry.Key.transform.position.y - actor.transform.position.y;
            if (gap <= tower.climberSpacing)
            {
                return false;
            }
        }
        return true;
    }

    internal void ReleaseEnemyExit(VillagerController actor)
    {
        if (IsFixed)
        {
            if (fixedExitOwner == actor)
            {
                fixedExitOwner = null;
            }
            return;
        }
        ReleaseWindow();
    }

    internal void MarkClimberFalling(VillagerController actor)
    {
        ReleaseEnemyExit(actor);
        if (IsFixed && fixedClimbers.TryGetValue(actor, out var climber))
        {
            climber.Falling = true;
        }
    }

    internal void CheckFallingClimberContact(VillagerController source, Vector3 previous, Vector3 next)
    {
        if (!IsFixed || !tower.enableClimberImpact)
        {
            return;
        }

        foreach (var entry in fixedClimbers)
        {
            var target = entry.Key;
            if (target == null || target == source || target.IsDead || entry.Value.Falling)
            {
                continue;
            }

            Vector3 position = target.transform.position;
            float torsoBottom = position.y + 0.35f;
            float torsoTop = position.y + 1.18f;
            if (previous.y < torsoBottom || next.y > torsoTop)
            {
                continue;
            }

            // 下方角色也在攀升，检测整个躯干区间，避免相邻两帧跳过肩部高度。
            float contactHeight = Mathf.Min(previous.y, torsoTop);
            Vector3 contact = Vector3.Lerp(previous, next, Mathf.InverseLerp(previous.y, next.y, contactHeight));
            Vector3 horizontal = Vector3.ProjectOnPlane(position - contact, Vector3.up);
            if (horizontal.sqrMagnitude <= 0.72f * 0.72f)
            {
                target.ArchitectureReadSpecialTraversal.TryFallFromLadderImpact(this);
            }
        }
    }

    private bool CanBeginPlayerJumpDown(LeonController player)
    {
        if (tower.HasCollapsed || configuration.playerJumpDown == null || configuration.playerJumpLand == null ||
            fixedClimbers.Count != 0 || !player.IsGrounded || !player.CanAct() ||
            !tower.ContainsPlatform(player.transform.position) ||
            (player.transform.position - Exit).sqrMagnitude > 1.15f * 1.15f ||
            GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
        {
            return false;
        }

        Vector3 heading = Vector3.ProjectOnPlane(Head + Outward * .5f - player.transform.position, Vector3.up).normalized;
        if (Vector3.Dot(player.transform.forward, heading) < .25f)
        {
            return false;
        }
        Vector3 descent = Head + Outward * .5f + Vector3.up * .35f;
        foreach (var hit in Physics.SphereCastAll(descent, .18f, Vector3.down, 1.1f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(player.transform) && !tower.IsLadderCollider(hit.collider))
            {
                return false;
            }
        }
        Vector3 launch = player.transform.position + Vector3.up * .65f;
        foreach (var hit in Physics.SphereCastAll(launch, .22f, heading, .65f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(player.transform) && !tower.IsLadderCollider(hit.collider))
            {
                return false;
            }
        }
        return player.TryGetComponent<LeonAnimationActionCoordinator>(out var actions) &&
            actions.CanRequestLadderPush();
    }
}

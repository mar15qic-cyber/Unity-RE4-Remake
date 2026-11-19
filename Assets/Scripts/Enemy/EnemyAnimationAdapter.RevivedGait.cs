using GameArchitecture;
using RE4.ChapterOne;
using UnityEngine;

public sealed partial class EnemyAnimationAdapter
{
    private ChapterOnePairMotion.Track revivedGaitTrack;
    private float revivedGaitTime;
    private float revivedGaitStride;
    private bool revivedGaitActive;
    internal bool HasRevivedGait => revivedGaitActive;
    internal float RevivedGaitStride => revivedGaitStride;

    private bool UpdateRevivedStandingGait()
    {
        if (host.healthModule.LifePhase != EnemyLifePhase.Revived || host.IsDead || host.IsQTEControlled
            || host.IsNavigationTraversalProtected || host.combatModule.NativeCombatActive
            || CurrentState != VillagerState.Idle && CurrentState != VillagerState.Chase)
        {
            if (revivedGaitActive)
            {
                ClearRevivedGait();
                StopNativeCombat();
            }
            return false;
        }
        bool moving = CurrentMovementMode == EnemyMovementMode.StandingPathRootMotion && standingLocomotionVisible
            && requestedStandingMoveParameter > .02f && host.NativeGaitDistanceToStop > .04f;
        string motion = "Revived_6000";
        if (moving)
        {
            motion = "Revived_6140";
            if (revivedGaitTrack != null && (revivedGaitTrack.clip.name == "Revived_6141"
                || revivedGaitTrack.clip.name == "Revived_6140" && revivedGaitTime >= revivedGaitTrack.clip.length))
            {
                motion = "Revived_6141";
            }
        }
        if (revivedGaitTrack == null || revivedGaitTrack.clip.name != motion)
        {
            var bank = Resources.Load<ChapterOnePairMotion>(ChapterOnePairMotion.ResourceName);
            string role = "GanadoMale";
            if (anim.runtimeAnimatorController.name == "MainGanado")
            {
                role = "Ganado";
            }
            if (bank == null || !bank.TryGetVillagerTrack(role, motion, out revivedGaitTrack))
            {
                ClearRevivedGait();
                return false;
            }
            revivedGaitTime = 0f;
        }
        revivedGaitActive = true;
        ResetNativeFootContacts();
        Vector3 previous = revivedGaitTrack.Sample(revivedGaitTime).position;
        float gaitRate = 1f;
        if (moving && motion == "Revived_6140" && host.Player != null
            && (host.Player.transform.position - transform.position).sqrMagnitude <= 3.2f * 3.2f)
        {
            gaitRate = 1.38f;
        }
        float next = revivedGaitTime + Time.deltaTime * gaitRate;
        Vector3 step;
        float duration = revivedGaitTrack.clip.length;
        if (next > duration && motion != "Revived_6140")
        {
            next = Mathf.Repeat(next, duration);
            step = revivedGaitTrack.Sample(duration).position - previous
                + revivedGaitTrack.Sample(next).position - revivedGaitTrack.Sample(0f).position;
        }
        else
        {
            next = Mathf.Min(next, duration);
            step = revivedGaitTrack.Sample(next).position - previous;
        }
        revivedGaitTime = next;
        revivedGaitStride = 0f;
        if (moving)
        {
            revivedGaitStride = Vector3.ProjectOnPlane(step, Vector3.up).magnitude;
        }
        PresentNativeCombat(revivedGaitTrack.clip, revivedGaitTime);
        return true;
    }

    private void ClearRevivedGait()
    {
        revivedGaitActive = false;
        revivedGaitTrack = null;
        revivedGaitTime = 0f;
        revivedGaitStride = 0f;
    }
}

using UnityEngine;

public partial class LeonQTECoordinator
{
    private ExecutionAppearance sawAppearance;
    private int appearanceSession;
    private Transform sawBlade;
    private Transform cutHips;
    private Transform cutHead;
    private Transform cutLeftArm;
    private Transform cutRightArm;
    private float nextSprayFrame;
    private bool floorMarked;
    private int separationDrips;
    private void UpdateSawAppearance(NativePairPlayback pair)
    {
        if (activeKind != QTEKind.ChainsawExecution || phase != QTEPhase.FailureResult || pair.SourceFrame < 124f)
        {
            return;
        }

        if (appearanceSession != activeSessionId)
        {
            ClearSawAppearance();
            appearanceSession = activeSessionId;
            var bridge = participant.Root.GetComponentInChildren<ChainsawManAnimationBridge>();
            if (bridge != null)
            {
                sawBlade = bridge.chainsawDamageOrigin;
            }

            cutHips = GetPlayerBone(HumanBodyBones.Hips);
            cutHead = GetPlayerBone(HumanBodyBones.Head);
            cutLeftArm = GetPlayerBone(HumanBodyBones.LeftUpperArm);
            cutRightArm = GetPlayerBone(HumanBodyBones.RightUpperArm);
        }

        if (sawBlade == null || cutHips == null || cutHead == null)
        {
            return;
        }

        float frame = pair.SourceFrame;
        float along = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(228f, 374f, frame));
        Vector3 center = Vector3.Lerp(cutHips.position, cutHead.position, along);
        Vector3 reach = sawBlade.TransformVector(Vector3.forward * .45425f);
        float fraction = Vector3.Dot(center - sawBlade.position, reach) / Mathf.Max(.00001f, reach.sqrMagnitude);
        Vector3 teeth = sawBlade.position + reach * Mathf.Clamp(fraction, -1f, 1f);
        bool touching = (teeth - center).sqrMagnitude < .30f * .30f;
        if (sawAppearance == null)
        {
            if (!touching || frame > SawDamageContactFrame + 1f)
            {
                return;
            }

            var bank = ExecutionGeometry.Load();
            if (bank == null)
            {
                return;
            }

            var appearance = new ExecutionAppearance();
            if (!appearance.Apply(transform, bank.leon, bank.interior, true))
            {
                return;
            }

            sawAppearance = appearance;
            nextSprayFrame = frame;
        }

        if (frame < 446f)
        {
            // 前半段停在腰腹，向上抬锯后打开胸颈，锯离开后左右两侧继续错开。
            float opening = 0f;
            if (frame < 260f)
            {
                opening = 32f * Mathf.InverseLerp(124f, 260f, frame);
            }
            else if (frame < SawDamageContactFrame)
            {
                opening = Mathf.Lerp(32f, 82f, Mathf.InverseLerp(260f, SawDamageContactFrame, frame));
            }
            else
            {
                opening = Mathf.Lerp(82f, 100f, Mathf.InverseLerp(SawDamageContactFrame, 436f, frame));
            }

            sawAppearance.SetOpening(opening, opening);
        }

        if (touching && frame <= SawDamageContactFrame && frame >= nextSprayFrame)
        {
            Vector3 outward = teeth - center;
            if (outward.sqrMagnitude < .002f)
            {
                outward = -transform.forward;
            }

            pair.ContactVfxPlayed = ChapterOneEffects.TryPlay(ChapterOneEffect.ChainsawBlood, teeth, outward.normalized);
            nextSprayFrame = frame + Mathf.Lerp(20f, 11f, along);
            if (!floorMarked)
            {
                ExecutionBlood.Mark(teeth, Vector3.down, 3f, .66f);
                ExecutionBlood.Mark(teeth, outward.normalized, 1.6f, .48f);
                floorMarked = true;
            }
        }

        UpdateCutPose(frame);
        if (separationDrips < 3 && frame >= 393f + 29f * separationDrips)
        {
            // 锯离开后，剩余血滴从正在落下的左右断面各自流出。
            Transform side = sawAppearance.FirstRoot;
            if (separationDrips == 1)
            {
                side = sawAppearance.SecondRoot;
            }
            Vector3 seam = Vector3.Lerp(cutHips.position, cutHead.position, .4f);
            Vector3 edge = side.TransformPoint(transform.InverseTransformPoint(seam));
            ChapterOneEffects.TryPlay(ChapterOneEffect.StabBlood, edge, Vector3.down);
            if (separationDrips == 2)
            {
                ExecutionBlood.Mark(edge, Vector3.down, 2.4f, .57f);
            }
            separationDrips++;
        }
    }

    private void UpdateCutPose(float frame)
    {
        Vector3 up = (cutHead.position - cutHips.position).normalized;
        Vector3 lateral = transform.right;
        if (cutLeftArm != null && cutRightArm != null)
        {
            lateral = (cutRightArm.position - cutLeftArm.position).normalized;
        }
        Vector3 forward = Vector3.Cross(lateral, up).normalized;
        Vector3 pivot = Vector3.Lerp(cutHips.position, cutHead.position, .44f);
        float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(367f, 431f, frame));
        float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(421f, 509f, frame));
        Quaternion firstTurn = Quaternion.AngleAxis(open * 13f, forward) * Quaternion.AngleAxis(open * 15f, up);
        Quaternion secondTurn = Quaternion.AngleAxis(-open * 17f, forward) * Quaternion.AngleAxis(-open * 19f, up);
        Vector3 firstShift = -lateral * (.115f * open + .045f * settle);
        Vector3 secondShift = lateral * (.14f * open + .062f * settle) + forward * (.045f * settle);
        sawAppearance.MoveHalves(pivot, firstTurn, firstShift, secondTurn, secondShift);
    }

    private void ClearSawAppearance()
    {
        if (sawAppearance != null)
        {
            sawAppearance.Clear();
            sawAppearance = null;
        }

        cutLeftArm = null;
        cutRightArm = null;
        cutHips = null;
        cutHead = null;
        sawBlade = null;
        floorMarked = false;
        separationDrips = 0;
        appearanceSession = 0;
    }
}

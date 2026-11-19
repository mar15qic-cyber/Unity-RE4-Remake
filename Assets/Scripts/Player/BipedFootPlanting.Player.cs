using RootMotion.FinalIK;
using UnityEngine;

public sealed partial class BipedFootPlanting
{
    private LeonFootAction playerFeet;
    private bool previousInjury;

    private static void PreparePlayerLeg(LimbIK leg)
    {
        // 由贴地组件在动画采样后统一求解；关闭 FinalIK 自己的帧回调。
        DisableLegIK(leg);
        if (!leg.solver.initiated)
        {
            leg.solver.Initiate(leg.transform);
        }
    }

    private void UpdatePlayerFeet()
    {
        if (!playerFeet.AllowGroundContact)
        {
            if (hasRootHistory)
            {
                ResetGroundContact();
            }
            return;
        }

        bool injured = playerFeet.leon.IsInjured;
        Vector3 root = transform.position;
        float yaw = transform.eulerAngles.y;
        if (hasRootHistory && (previousInjury != injured || (root - previousFrameRootPosition).sqrMagnitude > 0.25f))
        {
            ResetGroundContact();
        }

        bool stepping = playerFeet.IsStepping;
        if (nativeGait != null && nativeGait.IsNativeGaitMoving)
        {
            stepping = true;
        }
        if (hasRootHistory)
        {
            Vector3 planar = Vector3.ProjectOnPlane(root - previousFrameRootPosition, Vector3.up);
            stepping |= planar.magnitude > 0.08f * Time.deltaTime ||
                Mathf.Abs(Mathf.DeltaAngle(previousFrameOrientation, yaw)) > 8f * Time.deltaTime;
        }
        if (stepping)
        {
            stationaryTime = 0f;
        }
        else
        {
            stationaryTime += Time.deltaTime;
        }
        bool leftContact = false;
        bool rightContact = false;
        if (stepping && nativeGait != null)
        {
            nativeGait.ReadNativeFootContacts(out leftContact, out rightContact);
        }

        bool standing = stationaryTime >= 0.10f;
        SamplePlayerFoot(left, standing, leftContact, injured);
        SamplePlayerFoot(right, standing, rightContact, injured);
        float sink = 0f;
        if (standing && left.levelPlayerSupport && right.levelPlayerSupport &&
            Mathf.Abs(left.playerSurfaceHeight - right.playerSurfaceHeight) < 0.025f &&
            Mathf.Max(Mathf.Abs(left.playerSoleDelta), Mathf.Abs(right.playerSoleDelta)) < 0.065f)
        {
            // 平地保留整套待机姿势，仅补偿胶囊与鞋底的共同高度偏差。
            float common = (left.playerSoleDelta + right.playerSoleDelta) * 0.5f;
            if (Mathf.Abs(common) > 0.018f)
            {
                sink = Mathf.Clamp(common, -0.04f, 0.04f);
            }
            left.groundWeight = 0f;
            right.groundWeight = 0f;
            left.lockValue = false;
            right.lockValue = false;
        }
        else if (left.hasPlayerSupport && right.hasPlayerSupport)
        {
            float limit = 0.17f;
            if (injured)
            {
                limit = 0.125f;
            }
            sink = Mathf.Clamp(Mathf.Min(ComputeSink(left), ComputeSink(right)), -limit, 0f);
        }
        pelvisOffset = Mathf.SmoothDamp(pelvisOffset, sink, ref pelvisSpeed, 0.08f);
        if (Mathf.Abs(pelvisOffset) > 0.0001f)
        {
            WritePelvis(pelvisOffset);
        }
        SolvePlayerLeg(left, injured);
        SolvePlayerLeg(right, injured);
        previousInjury = injured;
        previousFrameOrientation = yaw;
        previousFrameRootPosition = root;
        hasRootHistory = true;
    }

    private void SamplePlayerFoot(FootState foot, bool standing, bool gaitContact, bool injured)
    {
        Transform ankle = foot.FootPosition;
        Vector3 sole = ankle.TransformPoint(foot.sole);
        Vector3 correction = Vector3.zero;
        Quaternion tilt = Quaternion.identity;
        float weight = 0f;
        foot.firmFooting = false;
        foot.hasPlayerSupport = false;
        foot.levelPlayerSupport = false;
        if ((standing || gaitContact) && TryPlayerSupport(foot, sole, out Vector3 surface, out Vector3 normal))
        {
            float rise = surface.y - sole.y;
            foot.hasPlayerSupport = true;
            foot.levelPlayerSupport = normal.y >= 0.995f;
            foot.playerSurfaceHeight = surface.y;
            foot.playerSoleDelta = rise;
            float maxUp = 0.24f;
            float maxDown = 0.22f;
            float tiltLimit = 27f;
            if (injured)
            {
                maxUp = 0.23f;
                maxDown = 0.22f;
                tiltLimit = 15f;
            }
            float limit = maxUp;
            if (rise < 0f)
            {
                limit = maxDown;
            }
            float excess = Mathf.Max(0f, Mathf.Abs(rise) - limit);
            weight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(excess / 0.065f));
            bool level = normal.y >= 0.995f && Mathf.Abs(rise) <= 0.018f;
            if (level)
            {
                weight = 0f;
            }
            foot.firmFooting = weight > 0f;
            if (foot.firmFooting)
            {
                Vector3 up = Vector3.RotateTowards(Vector3.up, normal, tiltLimit * Mathf.Deg2Rad, 0f);
                tilt = Quaternion.FromToRotation(Vector3.up, up);
                if (!foot.lockValue || !standing || Mathf.Abs(foot.anchor.y - surface.y) > 0.045f ||
                    Vector3.ProjectOnPlane(foot.anchor - sole, Vector3.up).sqrMagnitude > 0.0036f)
                {
                    foot.anchor = surface;
                }
                Vector3 target = surface;
                if (standing)
                {
                    target = foot.anchor;
                }
                correction = target - (ankle.position + tilt * (sole - ankle.position));
                correction.y = Mathf.Clamp(correction.y, -maxDown, maxUp);
            }
        }
        foot.lockValue = standing && foot.firmFooting;
        // 离开支撑相后仅衰减上一帧的附加量，摆动脚继续随动画抬起。
        float blend = 1f - Mathf.Exp(-26f * Time.deltaTime);
        foot.displacementCorrection = Vector3.Lerp(foot.displacementCorrection, correction, blend);
        foot.rotationCorrection = Quaternion.Slerp(foot.rotationCorrection, tilt, blend);
        foot.groundWeight = Mathf.MoveTowards(foot.groundWeight, weight, Time.deltaTime * 12f);
        foot.ankleTarget = ankle.position + foot.displacementCorrection;
        foot.targetRotation = foot.rotationCorrection * ankle.rotation;
    }

    private bool TryPlayerSupport(FootState foot, Vector3 sole, out Vector3 surface, out Vector3 normal)
    {
        Vector3 forward = transform.forward;
        if (foot.toe != null)
        {
            Vector3 axis = Vector3.ProjectOnPlane(foot.toe.position - foot.FootPosition.position, Vector3.up);
            if (axis.sqrMagnitude > 0.0001f)
            {
                forward = axis.normalized;
            }
        }
        bool heel = ReadPlayerSurface(sole - forward * 0.045f, out Vector3 heelPoint, out Vector3 heelNormal);
        bool toe = ReadPlayerSurface(sole + forward * 0.085f, out Vector3 toePoint, out Vector3 toeNormal);
        // 跨级或临边时以鞋底中心的真实接触裁决，避免用上一级与下一级拟合出假斜面。
        if (!heel || !toe || Mathf.Abs(heelPoint.y - toePoint.y) > 0.085f || Vector3.Dot(heelNormal, toeNormal) < 0.94f)
        {
            surface = sole;
            normal = Vector3.up;
            if (!heel && !toe)
            {
                return false;
            }
            return ReadPlayerSurface(sole, out surface, out normal);
        }
        surface = Vector3.Lerp(heelPoint, toePoint, 0.045f / 0.13f);
        normal = (heelNormal + toeNormal).normalized;
        return true;
    }

    private bool ReadPlayerSurface(Vector3 point, out Vector3 surface, out Vector3 normal)
    {
        float range = Mathf.Min(maximumLandDifference, 0.34f);
        Ray ray = new Ray(point + Vector3.up * range, Vector3.down);
        float length = range * 2f;
        var stairs = LeonPlayerStairRampRuntime.CurrentStair;
        if (stairs != null && stairs.TryReadFootTread(point, range, out float stepHeight))
        {
            var geometry = HouseThreeTraversalGeometry.Active;
            if (geometry != null && geometry.RaycastNativeStairTread(ray, out RaycastHit tread, length))
            {
                surface = tread.point;
                normal = tread.normal;
            }
            else
            {
                surface = new Vector3(point.x, stepHeight, point.z);
                normal = Vector3.up;
            }
            return true;
        }
        int count = Physics.RaycastNonAlloc(ray, hit, length, groundLayer, QueryTriggerInteraction.Ignore);
        surface = point;
        normal = Vector3.up;
        float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = hit[i];
            if (candidate.distance >= best || candidate.normal.y < 0.63f || candidate.rigidbody != null ||
                candidate.transform.IsChildOf(transform) || playerFeet.motion.IsWalkThroughDetail(candidate.collider) ||
                candidate.collider.GetComponentInParent<Animator>() != null)
            {
                continue;
            }
            surface = candidate.point;
            normal = candidate.normal;
            best = candidate.distance;
        }
        return best < float.PositiveInfinity;
    }

    private static void SolvePlayerLeg(FootState foot, bool injured)
    {
        var solver = foot.chain.solver;
        solver.IKPositionWeight = foot.groundWeight;
        solver.IKRotationWeight = foot.groundWeight;
        if (foot.groundWeight <= 0f)
        {
            return;
        }
        Transform thigh = solver.bone1.transform;
        Transform knee = solver.bone2.transform;
        Transform ankle = solver.bone3.transform;
        Vector3 upper = knee.position - thigh.position;
        Vector3 lower = ankle.position - knee.position;
        float bend = Vector3.Angle(upper, lower);
        float extensionMargin = 65f;
        float flexionMargin = 76f;
        if (injured)
        {
            extensionMargin = 28f;
            // 高侧承重脚允许屈膝迈上一阶；低侧腿仍保留受伤姿态的伸展限制。
            float lift = foot.ankleTarget.y - ankle.position.y;
            flexionMargin = Mathf.Lerp(28f, 70f, Mathf.InverseLerp(0.025f, 0.12f, lift));
        }
        float minBend = Mathf.Clamp(bend - extensionMargin, 6f, 150f);
        float maxBend = Mathf.Clamp(bend + flexionMargin, minBend, 150f);
        float squares = upper.sqrMagnitude + lower.sqrMagnitude;
        float product = 2f * upper.magnitude * lower.magnitude;
        float outer = Mathf.Sqrt(Mathf.Max(0f, squares + product * Mathf.Cos(minBend * Mathf.Deg2Rad)));
        float inner = Mathf.Sqrt(Mathf.Max(0f, squares + product * Mathf.Cos(maxBend * Mathf.Deg2Rad)));
        Vector3 reach = foot.ankleTarget - thigh.position;
        float reachable = Mathf.Clamp(reach.magnitude, inner, outer);
        float excess = Mathf.Abs(reach.magnitude - reachable);
        float available = 1f - Mathf.InverseLerp(0.025f, 0.12f, excess);
        solver.IKPositionWeight *= available;
        solver.IKRotationWeight *= available;
        solver.IKPosition = thigh.position + reach.normalized * reachable;
        solver.IKRotation = foot.targetRotation;
        foot.largeLeg.Read(thigh);
        foot.smallLeg.Read(knee);
        foot.soleValue.Read(ankle);
        solver.Update();
        foot.largeLeg.Record(thigh);
        foot.smallLeg.Record(knee);
        foot.soleValue.Record(ankle);
    }
}

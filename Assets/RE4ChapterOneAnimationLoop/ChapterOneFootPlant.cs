using UnityEngine;

namespace RE4.ChapterOne
{
    internal sealed class ChapterOneFootPlant
    {
        private readonly Transform ankle;
        private readonly Transform knee;
        private readonly Transform thigh;
        private readonly Transform hips;
        private readonly Transform toe;
        private Quaternion thighPose;
        private Quaternion kneePose;
        private Quaternion anklePose;
        private Vector3 hipsPose;
        private Vector3 previousRaw;
        private float previousTime;
        private bool hasSample;
        private bool captured;
        private float speed = float.PositiveInfinity;
        private float hipLowering;
        private Vector3 contactPole;

        public ChapterOneFootPlant(Transform ankle, Transform toe)
            : this(ankle, ankle.parent, ankle.parent.parent, ankle.parent.parent.parent, toe)
        {
        }

        public ChapterOneFootPlant(Transform ankle, Transform knee, Transform thigh, Transform hips, Transform toe)
        {
            this.ankle = ankle;
            this.knee = knee;
            this.thigh = thigh;
            this.hips = hips;
            this.toe = toe;
        }

        public Vector3 Position => toe.position;
        public float Speed => speed;

        public void BeginContact()
        {
            hipLowering = 0f;
            contactPole = Vector3.zero;
        }

        public void Capture()
        {
            thighPose = thigh.localRotation;
            kneePose = knee.localRotation;
            anklePose = ankle.localRotation;
            hipsPose = hips.localPosition;
            captured = true;
        }

        public void Restore()
        {
            if (!captured)
                return;
            thigh.localRotation = thighPose;
            knee.localRotation = kneePose;
            ankle.localRotation = anklePose;
            hips.localPosition = hipsPose;
            captured = false;
        }

        public void Sample(float time)
        {
            float elapsed = time - previousTime;
            if (elapsed < 0.0001f)
                return;
            Vector3 raw = toe.position;
            if (hasSample && elapsed < 0.10f)
            {
                Vector3 change = Vector3.ProjectOnPlane(raw - previousRaw, Vector3.up);
                float measured = change.magnitude / elapsed;
                speed = float.IsInfinity(speed) ? measured : Mathf.Lerp(speed, measured, 0.55f);
            }
            else
            {
                speed = float.PositiveInfinity;
            }
            previousRaw = raw;
            previousTime = time;
            hasSample = true;
        }

        public void Apply(Vector3 anchor, float weight, bool holdHeight, float maximumCorrection = 0.215f, bool allowHipLowering = true)
        {
            Vector3 correction = Vector3.ProjectOnPlane(anchor - toe.position, Vector3.up);
            if (holdHeight)
            {
                // 翻窗落脚接入跑步混合时，脚尖保持在接触高度以上。
                correction.y = Mathf.Max(0f, anchor.y - toe.position.y);
            }
            correction = Vector3.ClampMagnitude(correction, maximumCorrection) * weight;
            Vector3 first = knee.position - thigh.position;
            Vector3 second = ankle.position - knee.position;
            float upperLength = first.magnitude;
            float lowerLength = second.magnitude;
            // 从原始腿姿态取得膝盖弯曲方向，避免锁脚目标跨过腿轴时反向折膝。
            Vector3 rawPole = Vector3.ProjectOnPlane(first, first + second);
            if (rawPole.sqrMagnitude > 0.000025f)
            {
                Vector3 localPole = hips.InverseTransformDirection(rawPole.normalized);
                if (contactPole == Vector3.zero)
                    contactPole = localPole;
                else if (Vector3.Dot(contactPole, localPole) > 0f)
                    contactPole = Vector3.Slerp(contactPole, localPole, 1f - Mathf.Exp(-Time.deltaTime / 0.045f));
            }
            if (contactPole == Vector3.zero)
                return;

            Vector3 ankleTarget = ankle.position + correction;
            Vector3 toTarget = ankleTarget - thigh.position;
            float reach = upperLength + lowerLength - 0.002f;
            if (allowHipLowering)
            {
                float targetLowering = 0f;
                if (toTarget.sqrMagnitude > reach * reach)
                {
                    float horizontal = Vector3.ProjectOnPlane(toTarget, Vector3.up).sqrMagnitude;
                    float reachableHeight = Mathf.Sqrt(Mathf.Max(0f, reach * reach - horizontal));
                    targetLowering = Mathf.Clamp(thigh.position.y - ankleTarget.y - reachableHeight, 0f, 0.09f);
                }
                hipLowering = Mathf.MoveTowards(hipLowering, targetLowering, Time.deltaTime * 0.85f);
                hips.position -= Vector3.up * (hipLowering * weight);
                toTarget = ankleTarget - thigh.position;
            }
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.001f,
                upperLength + lowerLength - 0.0005f);
            Vector3 direction = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(hips.TransformDirection(contactPole), direction);
            if (bend.sqrMagnitude < 0.01f)
                return;
            bend.Normalize();

            float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
            Vector3 newKnee = direction * along + bend * Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
            Vector3 target = thigh.position + direction * distance;
            Quaternion footRotation = ankle.rotation;
            thigh.rotation = Quaternion.FromToRotation(first, newKnee) * thigh.rotation;
            knee.rotation = Quaternion.FromToRotation(ankle.position - knee.position, target - knee.position) * knee.rotation;
            ankle.rotation = footRotation;
        }
    }
}


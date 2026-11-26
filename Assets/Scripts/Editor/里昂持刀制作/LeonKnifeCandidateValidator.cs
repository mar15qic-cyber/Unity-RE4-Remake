using UnityEditor;
using UnityEngine;

/// <summary>对真实预览骨骼逐帧检查资源、接缝、脚位和手腕连续性。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀候选验证")]
public static class LeonKnifeCandidateValidator
{
    /// <summary>编辑器菜单调用入口 MenuCheck；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、LeonKnifeCandidateValidator.Check()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/持刀动作/检查候选动作")]
    public static void MenuCheck()
    {
        Debug.Log(Check());
    }

    /// <summary>Check 的业务处理入口；本方法直接依赖 AnimationUtility.GetCurveBindings(AnimationClip)、AnimationUtility.GetEditorCurve(AnimationClip, EditorCurveBinding)、Animator.GetBoneTransform(HumanBodyBones)、Directory.CreateDirectory(string)、File.WriteAllText(string, string, Encoding)、float.IsInfinity(float)、float.IsNaN(float) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeCandidateValidator.MenuCheck()；下游（含内部回调体） AnimationUtility.GetCurveBindings(AnimationClip)、AnimationUtility.GetEditorCurve(AnimationClip, EditorCurveBinding)、Animator.GetBoneTransform(HumanBodyBones)、Directory.CreateDirectory(string)、File.WriteAllText(string, string, Encoding)、float.IsInfinity(float)、float.IsNaN(float) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string Check()
    {
        // 报告。
var report = new System.Text.StringBuilder();
        // 错误数量。
int errorCount = 0;
        // character（角色）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var character = new LeonKnifeAnimationPreview.PreviewCharacter())
        {
            // 左脚位置。
var leftFootPosition = character.AnimatorValue.GetBoneTransform(UnityEngine.HumanBodyBones.LeftFoot);
            // 右脚位置。
var rightFootPosition = character.AnimatorValue.GetBoneTransform(UnityEngine.HumanBodyBones.RightFoot);
            // 右手。
var rightHand = character.AnimatorValue.GetBoneTransform(UnityEngine.HumanBodyBones.RightHand);
            // 左手。
var leftHand = character.AnimatorValue.GetBoneTransform(UnityEngine.HumanBodyBones.LeftHand);
            // 编号。
for (int id = 0; id < LeonKnifeAnimationAuthoring.clipName.Length; id++)
            {
                // 名称。
string name = LeonKnifeAnimationAuthoring.clipName[id];
                // 动画片段。
var clip = LeonKnifeAnimationPreview.Read(name);
                if (clip == null)
                {
                    report.AppendLine("缺失：" + name);
                    errorCount++;
                    continue;
                }

                // 曲线是否有效。
bool curveValid = true;
                // binding（绑定）：每轮从取得曲线绑定集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 AnimationUtility.GetEditorCurve(AnimationClip, EditorCurveBinding) 的参数、LeonKnifeCandidateValidator.Check()。
foreach (var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))
                {
                    // key（键）：每轮从取得编辑器曲线的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 float.IsInfinity(float) 的参数、作为 float.IsNaN(float) 的参数。
foreach (var key in UnityEditor.AnimationUtility.GetEditorCurve(clip, binding).keys)
                    {
                        if (float.IsNaN(key.value) || float.IsInfinity(key.value))
                        {
                            curveValid = false;
                        }
                    }

                    if (id >= 12 && (binding.propertyName.StartsWith("Root") || binding.propertyName.Contains("Leg") || binding.propertyName.Contains("Foot")))
                    {
                        curveValid = false;
                    }
                }

                if (!curveValid || !clip.humanMotion || clip.events.Length != 0 || UnityEngine.Mathf.Abs(clip.length - LeonKnifeAnimationAuthoring.clipDuration[id]) > 0.001f)
                {
                    errorCount++;
                }

                // 调用前缓存的角色。
global::LeonKnifeAnimationPreview.PreviewCharacter callReceiver4 = character;
                // 按原顺序缓存的实参动画片段。
global::UnityEngine.AnimationClip argumentValue2 = clip;
                // 按原顺序缓存的实参0。
float argumentValue3 = 0;
                // 分支选出的1。
int selectedValue1;
                if (id >= 12)
                {
                    selectedValue1 = 1;
                }
                else
                {
                    selectedValue1 = 0;
                }

                callReceiver4.Sample(argumentValue2, argumentValue3, selectedValue1, 0);
                // 起始左侧。
var startLeft = leftFootPosition.position;
                // 起始右侧。
var startRight = rightFootPosition.position;
                // 启动。
var startup = rightHand.position;
                // 上部手部。
var upperHand = startup;
                // 上部旋转。
var upperRotate = rightHand.rotation;
                // 上部左侧旋转。
var upperLeftRotate = leftHand.rotation;
                // 左手帧角度。
float leftHandFrameAngle = 0f;

                // 中文含义：footOffsetDifference 为“脚部偏移量差值”。；handFrameDistance 为“手部帧距离”。；handFrameAngle 为“手部帧角度”。
float footOffsetDifference = 0, handFrameDistance = 0, handFrameAngle = 0;
                // 帧数量。
int frameCount = UnityEngine.Mathf.RoundToInt(clip.length * 60f);
                // 帧。
for (int frame = 1; frame <= frameCount; frame++)
                {
                    // 时间。
float time = clip.length * frame / frameCount;
                    // 调用前缓存的角色。
global::LeonKnifeAnimationPreview.PreviewCharacter callReceiver8 = character;
                    // 按原顺序缓存的实参动画片段。
global::UnityEngine.AnimationClip argumentValue6 = clip;
                    // 按原顺序缓存的实参时间。
float argumentValue7 = time;
                    // 分支选出的1。
int selectedValue5;
                    if (id >= 12)
                    {
                        selectedValue5 = 1;
                    }
                    else
                    {
                        selectedValue5 = 0;
                    }

                    callReceiver8.Sample(argumentValue6, argumentValue7, selectedValue5, time);
                    footOffsetDifference = UnityEngine.Mathf.Max(footOffsetDifference, UnityEngine.Vector3.Distance(leftFootPosition.position, startLeft), UnityEngine.Vector3.Distance(rightFootPosition.position, startRight));
                    handFrameDistance = UnityEngine.Mathf.Max(handFrameDistance, UnityEngine.Vector3.Distance(rightHand.position, upperHand));
                    handFrameAngle = UnityEngine.Mathf.Max(handFrameAngle, UnityEngine.Quaternion.Angle(rightHand.rotation, upperRotate));
                    leftHandFrameAngle = UnityEngine.Mathf.Max(leftHandFrameAngle, UnityEngine.Quaternion.Angle(leftHand.rotation, upperLeftRotate));
                    upperLeftRotate = leftHand.rotation;
                    upperHand = rightHand.position;
                    upperRotate = rightHand.rotation;
                }

                report.AppendLine(name + "：时长=" + clip.length.ToString("F3") + "；脚位变化=" + footOffsetDifference.ToString("F4") + "米；最大相邻手位=" + handFrameDistance.ToString("F4") + "米；最大相邻手角=" + handFrameAngle.ToString("F2") + "度；首尾手位=" + UnityEngine.Vector3.Distance(rightHand.position, startup).ToString("F4") + "米。");
                if (id < 12 && footOffsetDifference > .005f)
                {
                    report.AppendLine("异常：站立支撑脚漂移超过五毫米。");
                    errorCount++;
                }

                // 计算前缓存的左侧手部帧角度。
float leftOperand10 = handFrameAngle;
                // 分支选出的2f。
float selectedValue9;
                if (id < 2)
                {
                    selectedValue9 = 2f;
                }
                else
                {
                    selectedValue9 = 30f;
                }

                if (leftOperand10 > (selectedValue9))
                {
                    report.AppendLine("异常：手腕相邻帧转角超过连续性检查阈值。");
                    errorCount++;
                }

                report.AppendLine("左手最大相邻转角=" + leftHandFrameAngle.ToString("F2") + "度。");
                // 计算前缓存的左侧左手帧角度。
float leftOperand12 = leftHandFrameAngle;
                // 分支选出的2f。
float selectedValue11;
                if (id < 2)
                {
                    selectedValue11 = 2f;
                }
                else
                {
                    selectedValue11 = 30f;
                }

                if (leftOperand12 > (selectedValue11))
                {
                    report.AppendLine("异常：左手相邻帧转角超过连续性检查阈值。");
                    errorCount++;
                }
            }

            // 接缝。
string[][] seam =
            {
                new[]
                {
                    "低位待机",
                    "右向左主挥砍"
                },
                new[]
                {
                    "右向左主挥砍",
                    "左向右回切"
                },
                new[]
                {
                    "右向左主挥砍",
                    "主挥砍回收"
                },
                new[]
                {
                    "左向右回切",
                    "回切回收"
                },
                new[]
                {
                    "主挥砍回收",
                    "低位待机"
                },
                new[]
                {
                    "回切回收",
                    "低位待机"
                },
                new[]
                {
                    "抬刀",
                    "高位防御"
                },
                new[]
                {
                    "普通格挡反应",
                    "高位防御"
                },
                new[]
                {
                    "弹反成功",
                    "高位防御"
                },
                new[]
                {
                    "放下",
                    "低位待机"
                },
                new[]
                {
                    "拔刀",
                    "高位防御"
                },
                new[]
                {
                    "低位待机",
                    "抬刀"
                },
                new[]
                {
                    "高位防御",
                    "放下"
                },
                new[]
                {
                    "低位待机",
                    "收刀"
                }
            };
            // connect（连接）：每轮从接缝取得一个元素，仅代表当前这一项。使用位置：作为 LeonKnifeAnimationPreview.Read(string) 的参数、作为 StringBuilder.AppendLine(string) 的参数。
foreach (var connect in seam)
            {
                // 前方。
var front = LeonKnifeAnimationPreview.Read(connect[0]);
                // 后方。
var back = LeonKnifeAnimationPreview.Read(connect[1]);
                character.Sample(front, front.length, 0, 0);
                // 位置值。
var positionValue = new UnityEngine.Vector3[(int)UnityEngine.HumanBodyBones.LastBone];
                // 当前遍历索引。
for (int i = 0; i < positionValue.Length; i++)
                {
                    // 骨骼。
var bone = character.AnimatorValue.GetBoneTransform((UnityEngine.HumanBodyBones)i);
                    if (bone)
                    {
                        positionValue[i] = bone.position;
                    }
                }

                character.Sample(back, 0, 0, 0);
                // 偏移量差值。
float offsetDifference = 0;
                // 当前遍历索引。
for (int i = 0; i < positionValue.Length; i++)
                {
                    // 骨骼。
var bone = character.AnimatorValue.GetBoneTransform((UnityEngine.HumanBodyBones)i);
                    if (bone)
                    {
                        offsetDifference = UnityEngine.Mathf.Max(offsetDifference, UnityEngine.Vector3.Distance(positionValue[i], bone.position));
                    }
                }

                report.AppendLine("接缝 " + connect[0] + " → " + connect[1] + "：最大骨位差=" + offsetDifference.ToString("F4") + "米。");
                if (offsetDifference > 0.02f)
                {
                    errorCount++;
                }
            }

            // 攻击。
var attack = LeonKnifeAnimationPreview.Read("移动主挥砍");
            // 方向。
for (int direction = 0; direction <= 6; direction++)
            {
                character.Sample(attack, .3f, direction, .3f);
            }
        }

        report.AppendLine("资源、接缝及连续性异常项：" + errorCount + "。该检查不代替穿模、刀路及动作风格的目视验收。");
        System.IO.Directory.CreateDirectory("持刀动作审阅");
        System.IO.File.WriteAllText("持刀动作审阅/采样检查.txt", report.ToString(), new System.Text.UTF8Encoding(false));
        return report.ToString();
    }
}

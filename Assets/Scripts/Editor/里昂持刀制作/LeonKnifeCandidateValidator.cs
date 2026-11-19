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
        // report（报告）：使用位置：作为 File.WriteAllText(string, string, Encoding) 的参数、LeonKnifeCandidateValidator.Check()。
var report = new System.Text.StringBuilder();
        // errorCount（错误数量）：使用位置：作为 StringBuilder.AppendLine(string) 的参数、LeonKnifeCandidateValidator.Check()。
int errorCount = 0;
        // character（角色）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var character = new LeonKnifeAnimationPreview.PreviewCharacter())
        {
            // leftFootPosition（左脚位置）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonKnifeCandidateValidator.Check()。
var leftFootPosition = character.AnimatorValue.GetBoneTransform(UnityEngine.HumanBodyBones.LeftFoot);
            // rightFootPosition（右脚位置）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonKnifeCandidateValidator.Check()。
var rightFootPosition = character.AnimatorValue.GetBoneTransform(UnityEngine.HumanBodyBones.RightFoot);
            // rightHand（右手）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Angle(Quaternion, Quaternion) 的参数、作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonKnifeCandidateValidator.Check()。
var rightHand = character.AnimatorValue.GetBoneTransform(UnityEngine.HumanBodyBones.RightHand);
            // leftHand（左手）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Quaternion.Angle(Quaternion, Quaternion) 的参数、LeonKnifeCandidateValidator.Check()。
var leftHand = character.AnimatorValue.GetBoneTransform(UnityEngine.HumanBodyBones.LeftHand);
            // id（编号）：使用位置：LeonKnifeCandidateValidator.Check()。
for (int id = 0; id < LeonKnifeAnimationAuthoring.clipName.Length; id++)
            {
                // name（名称）：使用位置：作为 LeonKnifeAnimationPreview.Read(string) 的参数、作为 StringBuilder.AppendLine(string) 的参数。
string name = LeonKnifeAnimationAuthoring.clipName[id];
                // clip（动画片段）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：作为 AnimationUtility.GetCurveBindings(AnimationClip) 的参数、作为 AnimationUtility.GetEditorCurve(AnimationClip, EditorCurveBinding) 的参数、作为 Mathf.Abs(float) 的参数 等共 6 处。
var clip = LeonKnifeAnimationPreview.Read(name);
                if (clip == null)
                {
                    report.AppendLine("缺失：" + name);
                    errorCount++;
                    continue;
                }

                // curveValid（曲线是否有效）：使用位置：LeonKnifeCandidateValidator.Check()。
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

                // callReceiver4（调用前缓存的角色）：从角色读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeCandidateValidator.Check()。
global::LeonKnifeAnimationPreview.PreviewCharacter callReceiver4 = character;
                // argumentValue2（按原顺序缓存的实参动画片段）：从动画片段读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
global::UnityEngine.AnimationClip argumentValue2 = clip;
                // argumentValue3（按原顺序缓存的实参0）：从0读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
float argumentValue3 = 0;
                // selectedValue1（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeCandidateValidator.Check()。
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
                // startLeft（起始左侧）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数。
var startLeft = leftFootPosition.position;
                // startRight（起始右侧）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数。
var startRight = rightFootPosition.position;
                // startup（启动）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonKnifeCandidateValidator.Check()。
var startup = rightHand.position;
                // upperHand（上部手部）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonKnifeCandidateValidator.Check()。
var upperHand = startup;
                // upperRotate（上部旋转）：使用位置：作为 Quaternion.Angle(Quaternion, Quaternion) 的参数、LeonKnifeCandidateValidator.Check()。
var upperRotate = rightHand.rotation;
                // upperLeftRotate（上部左侧旋转）：使用位置：作为 Quaternion.Angle(Quaternion, Quaternion) 的参数、LeonKnifeCandidateValidator.Check()。
var upperLeftRotate = leftHand.rotation;
                // leftHandFrameAngle（左手帧角度）：使用位置：作为 Mathf.Max(float, float) 的参数、作为 StringBuilder.AppendLine(string) 的参数、LeonKnifeCandidateValidator.Check()。
float leftHandFrameAngle = 0f;

                // 中文含义：footOffsetDifference 为“脚部偏移量差值”。；handFrameDistance 为“手部帧距离”。；handFrameAngle 为“手部帧角度”。
float footOffsetDifference = 0, handFrameDistance = 0, handFrameAngle = 0;
                // frameCount（帧数量）：保存 Mathf.RoundToInt(float) 的返回结果，供当前步骤使用。使用位置：LeonKnifeCandidateValidator.Check()。
int frameCount = UnityEngine.Mathf.RoundToInt(clip.length * 60f);
                // frame（帧）：使用位置：LeonKnifeCandidateValidator.Check()。
for (int frame = 1; frame <= frameCount; frame++)
                {
                    // time（时间）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeCandidateValidator.Check()。
float time = clip.length * frame / frameCount;
                    // callReceiver8（调用前缓存的角色）：从角色读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeCandidateValidator.Check()。
global::LeonKnifeAnimationPreview.PreviewCharacter callReceiver8 = character;
                    // argumentValue6（按原顺序缓存的实参动画片段）：从动画片段读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
global::UnityEngine.AnimationClip argumentValue6 = clip;
                    // argumentValue7（按原顺序缓存的实参时间）：从时间读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
float argumentValue7 = time;
                    // selectedValue5（分支选出的1）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeCandidateValidator.Check()。
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

                // leftOperand10（计算前缓存的左侧手部帧角度）：从手部帧角度读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeCandidateValidator.Check()。
float leftOperand10 = handFrameAngle;
                // selectedValue9（分支选出的2f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonKnifeCandidateValidator.Check()。
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
                // leftOperand12（计算前缓存的左侧左手帧角度）：从左手帧角度读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeCandidateValidator.Check()。
float leftOperand12 = leftHandFrameAngle;
                // selectedValue11（分支选出的2f）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonKnifeCandidateValidator.Check()。
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

            // seam（接缝）：使用位置：LeonKnifeCandidateValidator.Check()。
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
                // front（前方）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
var front = LeonKnifeAnimationPreview.Read(connect[0]);
                // back（后方）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
var back = LeonKnifeAnimationPreview.Read(connect[1]);
                character.Sample(front, front.length, 0, 0);
                // positionValue（位置值）：使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonKnifeCandidateValidator.Check()。
var positionValue = new UnityEngine.Vector3[(int)UnityEngine.HumanBodyBones.LastBone];
                // i（当前遍历索引）：使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、LeonKnifeCandidateValidator.Check()。
for (int i = 0; i < positionValue.Length; i++)
                {
                    // bone（骨骼）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonKnifeCandidateValidator.Check()。
var bone = character.AnimatorValue.GetBoneTransform((UnityEngine.HumanBodyBones)i);
                    if (bone)
                    {
                        positionValue[i] = bone.position;
                    }
                }

                character.Sample(back, 0, 0, 0);
                // offsetDifference（偏移量差值）：使用位置：作为 Mathf.Max(float, float) 的参数、作为 StringBuilder.AppendLine(string) 的参数、LeonKnifeCandidateValidator.Check()。
float offsetDifference = 0;
                // i（当前遍历索引）：使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、LeonKnifeCandidateValidator.Check()。
for (int i = 0; i < positionValue.Length; i++)
                {
                    // bone（骨骼）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Vector3.Distance(Vector3, Vector3) 的参数、LeonKnifeCandidateValidator.Check()。
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

            // attack（攻击）：保存 LeonKnifeAnimationPreview.Read(string) 的返回结果，供当前步骤使用。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数。
var attack = LeonKnifeAnimationPreview.Read("移动主挥砍");
            // direction（方向）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、LeonKnifeCandidateValidator.Check()。
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

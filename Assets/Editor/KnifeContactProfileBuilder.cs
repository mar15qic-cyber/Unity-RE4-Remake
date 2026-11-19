using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;
using GameArchitecture;

/// <summary>只读原动画，为普通敌人的实际挥击采样手部轨迹。</summary>
public static class KnifeContactProfileBuilder
{
    /// <summary>中文含义：报告目录。编译期常量，保持既有值与消费者映射。使用位置：作为 Directory.CreateDirectory(string) 的参数、作为 File.WriteAllText(string, string) 的参数。</summary>
    public const string ReportDirectory = "战斗日志/弹反重整";
    /// <summary>中文含义：预制件集合。只读字段，初始化后不由普通写入口替换。使用位置：KnifeContactProfileBuilder.Build()。</summary>
    private static readonly string[] Prefabs =
    {
        "Assets/Prefab/Enemies/FemaleGanardEnemy.prefab",
        "Assets/Prefab/Enemies/FemaleGanard2Enemy.prefab"
    };
    /// <summary>Build 的业务处理入口；本方法直接依赖 AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.SaveAssets()、Dictionary&lt;string, KnifeEnemyAttackProfile[]&gt;.Dictionary()、Dictionary&lt;string, KnifeEnemyAttackProfile[]&gt;.TryGetValue(string, out KnifeEnemyAttackProfile[])、Directory.CreateDirectory(string)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.OpenScene(string) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.LoadAssetAtPath&lt;GameObject&gt;(string)、AssetDatabase.SaveAssets()、Dictionary&lt;string, KnifeEnemyAttackProfile[]&gt;.Dictionary()、Dictionary&lt;string, KnifeEnemyAttackProfile[]&gt;.TryGetValue(string, out KnifeEnemyAttackProfile[])、Directory.CreateDirectory(string)、EditorSceneManager.MarkSceneDirty(Scene)、EditorSceneManager.OpenScene(string) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("请在编辑模式标定。");
        }

        // original（原始）：保存 SceneManager.GetActiveScene() 的返回结果，供当前步骤使用。使用位置：KnifeContactProfileBuilder.Build()。
Scene original = SceneManager.GetActiveScene();
        if (original.isDirty)
        {
            throw new InvalidOperationException("当前场景存在未保存改动，未修改任何资源。");
        }

        // originalPath（原始路径）：使用位置：作为 EditorSceneManager.OpenScene(string) 的参数。
string originalPath = original.path;
        Directory.CreateDirectory(ReportDirectory);
        // report（报告）：使用位置：作为 JsonConvert.SerializeObject(object?, Formatting) 的参数、作为 KnifeContactProfileBuilder.Bake(GameObject, List<object>) 的参数。
List<object> report = new List<object>();
        // profiles（配置档集合）：使用位置：KnifeContactProfileBuilder.Build()。
Dictionary<string, KnifeEnemyAttackProfile[]> profiles = new Dictionary<string, KnifeEnemyAttackProfile[]>();
        // path（路径）：每轮从预制件集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的参数、作为 Dictionary<string, KnifeEnemyAttackProfile[]>.this[string] 的参数、作为 PrefabUtility.LoadPrefabContents(string) 的参数 等共 4 处。
foreach (string path in Prefabs)
        {
            // asset（资源）：保存 AssetDatabase.LoadAssetAtPath<GameObject>(string) 的返回结果，供当前步骤使用。使用位置：作为 KnifeContactProfileBuilder.Bake(GameObject, List<object>) 的参数。
GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // baked（已烘焙）：保存 KnifeContactProfileBuilder.Bake(GameObject, List<object>) 的返回结果，供当前步骤使用。使用位置：作为 EnemyCombatModule.ConfigureKnifeAttackProfiles(KnifeEnemyAttackProfile[]) 的参数、KnifeContactProfileBuilder.Build()。
KnifeEnemyAttackProfile[] baked = Bake(asset, report);
            profiles[path] = baked;
            // content（内容）：保存 PrefabUtility.LoadPrefabContents(string) 的返回结果，供当前步骤使用。使用位置：作为 PrefabUtility.SaveAsPrefabAsset(GameObject, string) 的参数、作为 PrefabUtility.UnloadPrefabContents(GameObject) 的参数、KnifeContactProfileBuilder.Build()。
GameObject content = PrefabUtility.LoadPrefabContents(path);
            try
            {
                content.GetComponent<EnemyCombatModule>().ConfigureKnifeAttackProfiles(baked);
                PrefabUtility.SaveAsPrefabAsset(content, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(content);
            }
        }

        try
        {
            // path（路径）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 EditorSceneManager.OpenScene(string) 的参数。
foreach (string path in new[]
            {
                SceneManagerEx.MainScenePath,
                "Assets/Scenes/小刀战斗验证.unity"
            }

            )
            {
                // scene（场景）：保存 EditorSceneManager.OpenScene(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.MarkSceneDirty(Scene) 的参数、作为 EditorSceneManager.SaveScene(Scene) 的参数。
Scene scene = EditorSceneManager.OpenScene(path);
                // changed（是否发生变化）：使用位置：KnifeContactProfileBuilder.Build()。
bool changed = false;
                // enemy（敌人）：每轮从查找对象集合依据类型的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(Object) 的参数、作为 PrefabUtility.RecordPrefabInstancePropertyModifications(Object) 的参数 等共 4 处。
foreach (EnemyCombatModule enemy in UnityEngine.Object.FindObjectsByType<EnemyCombatModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    // prefabPath（预制件路径）：保存 PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(Object) 的返回结果，供当前步骤使用。使用位置：作为 Dictionary<string, KnifeEnemyAttackProfile[]>.TryGetValue(string, out KnifeEnemyAttackProfile[]) 的参数。
string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(enemy.gameObject);
                    if (profiles.TryGetValue(prefabPath, out KnifeEnemyAttackProfile[] /* baked：由模式匹配、解构或输出参数绑定的当前结果，按原作用域供后续分支使用。使用位置：作为 EnemyCombatModule.ConfigureKnifeAttackProfiles(KnifeEnemyAttackProfile[]) 的参数。 */ /* baked：由模式匹配、解构或输出参数绑定的当前结果，按原作用域供后续分支使用。使用位置：KnifeContactProfileBuilder.Build()。 */ /* baked：由模式匹配、解构或输出参数绑定的当前结果，按原作用域供后续分支使用。使用位置：作为 EnemyCombatModule.ConfigureKnifeAttackProfiles(KnifeEnemyAttackProfile[]) 的参数。 */ /* 中文含义：已烘焙。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ baked))
                    {
                        enemy.ConfigureKnifeAttackProfiles(baked);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);
                        EditorUtility.SetDirty(enemy);
                        changed = true;
                    }
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
        finally
        {
            EditorSceneManager.OpenScene(originalPath);
        }

        AssetDatabase.SaveAssets();
        File.WriteAllText(ReportDirectory + "/敌人逐招时序.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return "两种正式敌人的三种招式已采样并接入正式及验证场景。";
    }

    /// <summary>Bake 的业务处理入口；本方法直接依赖 AnimationClip.SampleAnimation(GameObject, float)、Animator.GetBoneTransform(HumanBodyBones)、EditorSceneManager.ClosePreviewScene(Scene)、EditorSceneManager.NewPreviewScene()、Enum.ToString()、GameObject.GetComponent&lt;Animator&gt;()、IEnumerable&lt; &lt;anonymous type: string hand, float begin, float end, float radius, int samples&gt; &gt;.ToArray&lt; &lt;anonymous type: string hand, float begin, float end, float radius, int samples&gt; &gt;() 等共 21 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeContactProfileBuilder.Build()；下游（含内部回调体） AnimationClip.SampleAnimation(GameObject, float)、Animator.GetBoneTransform(HumanBodyBones)、EditorSceneManager.ClosePreviewScene(Scene)、EditorSceneManager.NewPreviewScene()、Enum.ToString()、GameObject.GetComponent&lt;Animator&gt;()、IEnumerable&lt; &lt;anonymous type: string hand, float begin, float end, float radius, int samples&gt; &gt;.ToArray&lt; &lt;anonymous type: string hand, float begin, float end, float radius, int samples&gt; &gt;() 等共 21 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="asset">中文含义：资源。使用位置：作为 List&lt;object&gt;.Add(object) 的参数、作为 Object.Instantiate&lt;GameObject&gt;(GameObject) 的参数。</param>
    /// <param name="report">中文含义：报告。使用位置：KnifeContactProfileBuilder.Bake(GameObject, List&lt;object&gt;)。</param>
    public static string CalibrateExistingProfiles(LeonController player)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || player == null
            || !player.TryGetComponent(out CapsuleCollider playerCapsule))
        {
            throw new InvalidOperationException("请在编辑模式传入正式 Leon 的胶囊碰撞体。");
        }

        var measurements = new List<string>();
        foreach (string path in Prefabs)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var serialized = new SerializedObject(contents.GetComponent<EnemyCombatModule>());
                SerializedProperty profiles = serialized.FindProperty("knifeAttackConfiguration");
                for (int index = 0; index < profiles.arraySize; index++)
                {
                    SerializedProperty property = profiles.GetArrayElementAtIndex(index);
                    var profile = (KnifeEnemyAttackProfile)property.boxedValue;
                    CalibrateProfile(asset, playerCapsule, profile, index);
                    property.boxedValue = profile;
                    measurements.Add($"{asset.name}/{profile.sourceClip.name}：起手距离 {profile.maximumStartDistance:F2} 米，容差 {profile.contactPadding:F2} 米");
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        return string.Join("\n", measurements);
    }

    public static string CalibrateProductionProfiles(ChapterOneMissionManager mission, KnifeCombatModule knife)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || mission == null || knife == null
            || knife.configuration == null || !knife.TryGetComponent(out CapsuleCollider playerCapsule))
        {
            throw new InvalidOperationException("请在主场景的编辑模式传入正式波次与 Leon。");
        }

        var calibrated = new List<(string path, KnifeEnemyAttackProfile[] unarmed, KnifeEnemyAttackProfile[] armed)>();
        foreach (GameObject prefab in mission.ganadoPrefabs.Distinct())
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            var source = new SerializedObject(prefab.GetComponent<EnemyCombatModule>());
            var authoredArmed = source.FindProperty("standingWeaponAttackProfiles");
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var animator = actor.GetComponent<Animator>();
                RuntimeAnimatorController unarmedController = animator.runtimeAnimatorController;
                var armed = Array.Empty<KnifeEnemyAttackProfile>();
                if (authoredArmed.arraySize > 0)
                {
                    var held = new SerializedObject(actor.GetComponent<EnemyHeldWeapon>());
                    unarmedController = (RuntimeAnimatorController)held.FindProperty("unarmedController").objectReferenceValue;
                    var weapon = (BoxCollider)held.FindProperty("droppedCollider").objectReferenceValue;
                    armed = new KnifeEnemyAttackProfile[authoredArmed.arraySize];
                    for (int move = 0; move < armed.Length; move++)
                    {
                        var profile = (KnifeEnemyAttackProfile)authoredArmed.GetArrayElementAtIndex(move).boxedValue;
                        AnimationClip clip = FindProductionAttackClip(animator.runtimeAnimatorController, move);
                        if (profile.sourceClip != clip || profile.contactSegments.Length != 1)
                        {
                            throw new InvalidOperationException($"{prefab.name} 的持械动作与刃部采样不一致。");
                        }

                        KnifeEnemyAttackSegment original = profile.contactSegments[0];
                        float firstContact = profile.contactNormalizedPoint * clip.length;
                        profile.contactSegments = new[]
                        {
                            SampleProductionBlade(actor, clip, weapon.transform, Mathf.Max(0f, firstContact - .055f), original.end)
                        };
                        profile.usePoseContact = true;
                        profile.promptEarly = .27f;
                        profile.contactPadding = .035f;
                        BakeProductionRoot(profile);
                        if (profile.maximumStartDistance <= 0f)
                        {
                            CalibrateProductionRange(prefab, playerCapsule, profile,
                                source.FindProperty("standingWeaponAttackStartRange").floatValue);
                        }
                        armed[move] = profile;
                    }
                }

                var left = animator.GetComponentsInChildren<Transform>(true).Single(bone => bone.name == "L_Hand");
                var right = animator.GetComponentsInChildren<Transform>(true).Single(bone => bone.name == "R_Hand");
                SerializedProperty existingUnarmed = source.FindProperty("knifeAttackConfiguration");
                var unarmed = new KnifeEnemyAttackProfile[3];
                for (int move = 0; move < unarmed.Length; move++)
                {
                    AnimationClip clip = FindProductionAttackClip(unarmedController, move);
                    KnifeEnemyAttackSegment[] segments;
                    // 原生站立攻击的前摆段。长片段后半段是回收与脚步，保持挥空。
                    if (move == 0)
                    {
                        segments = new[]
                        {
                            SampleProductionFist(actor, clip, right, HumanBodyBones.RightHand, .40f, .51f),
                            SampleProductionFist(actor, clip, left, HumanBodyBones.LeftHand, .55f, .84f)
                        };
                    }
                    else if (move == 1)
                    {
                        segments = new[] { SampleProductionFist(actor, clip, right, HumanBodyBones.RightHand, .65f, .93f) };
                    }
                    else
                    {
                        segments = new[]
                        {
                            SampleProductionFist(actor, clip, left, HumanBodyBones.LeftHand, .50f, .67f),
                            SampleProductionFist(actor, clip, right, HumanBodyBones.RightHand, .68f, .86f)
                        };
                    }

                    var profile = new KnifeEnemyAttackProfile
                    {
                        sourceClip = clip,
                        category = (KnifeIncomingAttackCategory)move,
                        contactNormalizedPoint = segments[0].begin / clip.length,
                        promptEarly = .27f,
                        contactDistance = 1.5f,
                        contactAngle = 120f,
                        contactPadding = .045f,
                        usePoseContact = true,
                        contactSegments = segments
                    };
                    BakeProductionRoot(profile);
                    if (move < existingUnarmed.arraySize)
                    {
                        var previous = (KnifeEnemyAttackProfile)existingUnarmed.GetArrayElementAtIndex(move).boxedValue;
                        profile.maximumStartDistance = previous.maximumStartDistance;
                    }

                    if (profile.maximumStartDistance <= 0f)
                    {
                        CalibrateProductionRange(prefab, playerCapsule, profile, 1.55f);
                    }

                    unarmed[move] = profile;
                }

                calibrated.Add((path, unarmed, armed));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        var report = new List<string>();
        foreach (var (path, unarmed, armed) in calibrated)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.GetComponent<EnemyCombatModule>().ConfigureKnifeAttackProfiles(unarmed);
                if (armed.Length > 0)
                {
                    var target = new SerializedObject(contents.GetComponent<EnemyCombatModule>());
                    var array = target.FindProperty("standingWeaponAttackProfiles");
                    for (int move = 0; move < armed.Length; move++)
                    {
                        array.GetArrayElementAtIndex(move).boxedValue = armed[move];
                    }

                    target.ApplyModifiedPropertiesWithoutUndo();
                }

                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null)
                {
                    throw new InvalidOperationException($"无法保存 {path} 的挥击数据。");
                }

                report.Add($"{contents.name} 起手：{string.Join(" / ", unarmed.Concat(armed).Select(profile => profile.maximumStartDistance.ToString("F2")))} 米");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        knife.configuration.tapWindow = .30f;
        EditorUtility.SetDirty(knife.configuration);
        AssetDatabase.SaveAssetIfDirty(knife.configuration);
        return string.Join("\n", report);
    }

    internal static AnimationClip FindProductionAttackClip(RuntimeAnimatorController controller, int move)
    {
        var overrides = controller as AnimatorOverrideController;
        RuntimeAnimatorController stateController = controller;
        if (overrides != null)
        {
            stateController = overrides.runtimeAnimatorController;
        }

        if (stateController is not AnimatorController graph)
        {
            throw new InvalidOperationException("正式敌人的攻击控制器缺失。");
        }

        string name = $"Attack{move + 1:00}";
        AnimationClip source = FindClip(graph.layers[0].stateMachine);
        if (source == null)
        {
            throw new InvalidOperationException($"{controller.name} 缺少 {name} 的片段。");
        }

        if (overrides != null && overrides[source] != null)
        {
            return overrides[source];
        }

        return source;

        AnimationClip FindClip(AnimatorStateMachine machine)
        {
            foreach (ChildAnimatorState entry in machine.states)
            {
                if (entry.state.name == name && entry.state.motion is AnimationClip clip)
                {
                    return clip;
                }
            }

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                AnimationClip clip = FindClip(child.stateMachine);
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }
    }

    private static KnifeEnemyAttackSegment SampleProductionFist(GameObject actor, AnimationClip clip, Transform hand,
        HumanBodyBones side, float begin, float end)
    {
        string prefix = "R_";
        if (side == HumanBodyBones.LeftHand)
        {
            prefix = "L_";
        }

        SampleProductionPose(actor, clip, begin);
        Transform knuckle = hand.GetComponentsInChildren<Transform>(true).Single(bone => bone.name == prefix + "MiddleF1");
        Vector3 tip = hand.InverseTransformPoint(knuckle.position);
        return SampleProductionSegment(actor, clip, hand, -tip * .12f, tip, side, begin, end, .085f);
    }

    private static KnifeEnemyAttackSegment SampleProductionBlade(GameObject actor, AnimationClip clip, Transform weapon, float begin, float end)
    {
        var skin = weapon.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Vector3[] vertices = skin.sharedMesh.vertices;
        var points = vertices.Select(vertex => weapon.InverseTransformPoint(skin.transform.TransformPoint(vertex)))
            .Where(vertex => vertex.x > .055f && vertex.z > .18f).ToArray();
        if (points.Length == 0)
        {
            throw new InvalidOperationException("正式持械村民缺少斧刃顶点。");
        }

        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (Vector3 point in points)
        {
            bounds.Encapsulate(point);
        }

        Vector3 extents = bounds.extents;
        int lengthAxis = 0;
        if (extents.z > extents.x)
        {
            lengthAxis = 2;
        }

        float scale = Mathf.Abs(weapon.lossyScale[lengthAxis]);
        float radius = Mathf.Clamp(Mathf.Max(extents[(lengthAxis + 1) % 3], extents[(lengthAxis + 2) % 3]) * scale, .028f, .07f);
        Vector3 axis = Vector3.zero;
        axis[lengthAxis] = Mathf.Max(0f, extents[lengthAxis] - radius / Mathf.Max(.0001f, scale));
        return SampleProductionSegment(actor, clip, weapon, bounds.center - axis, bounds.center + axis,
            HumanBodyBones.RightHand, begin, end, radius);
    }

    private static KnifeEnemyAttackSegment SampleProductionSegment(GameObject actor, AnimationClip clip, Transform part,
        Vector3 start, Vector3 tip, HumanBodyBones hand, float begin, float end, float radius)
    {
        int count = Mathf.CeilToInt((end - begin) * 120f) + 1;
        var samples = new Vector3[count];
        var tips = new Vector3[count];
        for (int index = 0; index < count; index++)
        {
            float time = Mathf.Lerp(begin, end, (float)index / (count - 1));
            SampleProductionPose(actor, clip, time);
            samples[index] = actor.transform.InverseTransformPoint(part.TransformPoint(start));
            tips[index] = actor.transform.InverseTransformPoint(part.TransformPoint(tip));
        }

        return new KnifeEnemyAttackSegment
        {
            hand = hand,
            begin = begin,
            end = end,
            radius = radius,
            localPoints = samples,
            localEndPoints = tips,
            contactBonePath = AnimationUtility.CalculateTransformPath(part, actor.transform),
            contactLocalStart = start,
            contactLocalEnd = tip
        };
    }

    private static void SampleProductionPose(GameObject actor, AnimationClip clip, float time)
    {
        actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        clip.SampleAnimation(actor, time);
    }

    private static void BakeProductionRoot(KnifeEnemyAttackProfile profile)
    {
        AnimationClip clip = profile.sourceClip;
        AnimationCurve x = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"));
        AnimationCurve z = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.z"));
        if (x == null || z == null)
        {
            throw new InvalidOperationException($"{clip.name} 缺少平面根位移曲线。");
        }

        float end = profile.contactSegments.Max(segment => segment.end);
        const float interval = KnifeEnemyAttackProfile.RootSampleInterval;
        var points = new Vector3[Mathf.CeilToInt(end / interval) + 1];
        Vector3 origin = new Vector3(x.Evaluate(0), 0f, z.Evaluate(0));
        for (int index = 0; index < points.Length; index++)
        {
            float time = index * interval;
            points[index] = new Vector3(x.Evaluate(time), 0f, z.Evaluate(time)) - origin;
        }

        profile.planarRootTrajectory = points;
    }

    private static void CalibrateProductionRange(GameObject prefab, CapsuleCollider playerCapsule,
        KnifeEnemyAttackProfile profile, float limit)
    {
        float radius = playerCapsule.radius;
        float halfHeight = playerCapsule.height * .5f;
        float minSeparation = prefab.GetComponent<CapsuleCollider>().radius + radius + .02f;
        const float step = .05f;
        float first = Mathf.Ceil(minSeparation / step) * step;
        float maximum = 0f;
        for (float distance = first; distance <= limit + .0001f; distance += step)
        {
            Vector3 center = playerCapsule.center + Vector3.forward * distance;
            if (!CanReachStandingTarget(profile, prefab.transform.localScale, minSeparation, center, radius, halfHeight))
            {
                break;
            }

            maximum = distance;
        }

        if (maximum < first)
        {
            throw new InvalidOperationException($"{prefab.name}/{profile.sourceClip.name} 的正面近距挥击未覆盖 Leon。");
        }

        profile.maximumStartDistance = Mathf.Round((maximum - step) * 100f) / 100f;
    }

    private static void CalibrateProfile(GameObject asset, CapsuleCollider playerCapsule, KnifeEnemyAttackProfile profile, int index)
    {
        profile.contactPadding = .03f;
        profile.windupLateralReduction = 0f;
        if (index == 2)
        {
            profile.contactPadding = .05f;
            profile.windupLateralReduction = .75f;
        }

        float lastContactTime = profile.contactSegments.Max(segment => segment.end);
        const float interval = KnifeEnemyAttackProfile.RootSampleInterval;
        var trajectory = new Vector3[Mathf.CeilToInt(lastContactTime / interval) + 1];
        Scene scene = EditorSceneManager.NewPreviewScene();
        PlayableGraph graph = default;
        try
        {
            var preview = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
            preview.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = preview.GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create();
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clip = AnimationClipPlayable.Create(graph, profile.sourceClip);
            var output = AnimationPlayableOutput.Create(graph, "攻击根位移采样", animator);
            output.SetSourcePlayable(clip);
            graph.Play();
            graph.Evaluate(0f);
            for (int frame = 1; frame < trajectory.Length; frame++)
            {
                graph.Evaluate(interval);
                Vector3 delta = animator.deltaPosition;
                delta.y = 0f;
                delta.x *= 1f - profile.windupLateralReduction;
                trajectory[frame] = trajectory[frame - 1] + delta;
            }
        }
        finally
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }

            EditorSceneManager.ClosePreviewScene(scene);
        }

        profile.planarRootTrajectory = trajectory;

        float playerRadius = playerCapsule.radius * Mathf.Max(Mathf.Abs(playerCapsule.transform.lossyScale.x), Mathf.Abs(playerCapsule.transform.lossyScale.z));
        float playerHalfHeight = playerCapsule.height * Mathf.Abs(playerCapsule.transform.lossyScale.y) * .5f;
        Vector3 playerCenter = playerCapsule.transform.TransformVector(playerCapsule.center);
        float minSeparation = asset.GetComponent<CapsuleCollider>().radius + playerRadius;
        float previousLimit = asset.GetComponent<VillagerController>().attackStartRange + playerRadius;
        const float distanceStep = .05f;
        float firstDistance = Mathf.Ceil(minSeparation / distanceStep) * distanceStep;
        float lastDistance = Mathf.Floor((previousLimit + .0001f) / distanceStep) * distanceStep;
        float maximum = 0f;
        for (float distance = firstDistance; distance <= lastDistance + .0001f; distance += distanceStep)
        {
            Vector3 capsule = Vector3.forward * distance + playerCenter;
            if (!CanReachStandingTarget(profile, asset.transform.localScale, minSeparation, capsule, playerRadius, playerHalfHeight))
            {
                break;
            }

            maximum = distance;
        }

        if (maximum <= firstDistance)
        {
            throw new InvalidOperationException($"{asset.name}/{profile.sourceClip.name} 缺少连续的正面起手范围。");
        }

        // 在采样边界内保留一个距离步长，给转入攻击状态的混合留余量。
        profile.maximumStartDistance = Mathf.Round((maximum - distanceStep) * 100f) / 100f;
    }

    private static bool CanReachStandingTarget(KnifeEnemyAttackProfile profile, Vector3 scale, float minSeparation,
        Vector3 capsule, float radius, float halfHeight)
    {
        Vector3 axis = Vector3.up * Mathf.Max(0f, halfHeight - radius);
        foreach (KnifeEnemyAttackSegment segment in profile.contactSegments)
        {
            for (int index = 0; index < segment.localPoints.Length; index++)
            {
                float time = Mathf.Lerp(segment.begin, segment.end, (float)index / (segment.localPoints.Length - 1));
                float frame = time / KnifeEnemyAttackProfile.RootSampleInterval;
                int previous = Mathf.Min(Mathf.FloorToInt(frame), profile.planarRootTrajectory.Length - 2);
                Vector3 root = Vector3.Lerp(profile.planarRootTrajectory[previous], profile.planarRootTrajectory[previous + 1], frame - previous);
                root.z = Mathf.Min(root.z, Mathf.Max(0f, capsule.z - minSeparation));
                Vector3 hand = root + Vector3.Scale(segment.localPoints[index], scale);
                Vector3 axisPoint = capsule;
                axisPoint.y = Mathf.Clamp(hand.y, capsule.y - axis.y, capsule.y + axis.y);
                float combinedRadius = radius + segment.radius + profile.contactPadding;
                if ((axisPoint - hand).sqrMagnitude <= combinedRadius * combinedRadius)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static KnifeEnemyAttackProfile[] Bake(GameObject asset, List<object> report)
    {
        // preview（预览）：保存 EditorSceneManager.NewPreviewScene() 的返回结果，供当前步骤使用。使用位置：作为 EditorSceneManager.ClosePreviewScene(Scene) 的参数、作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数。
Scene preview = EditorSceneManager.NewPreviewScene();
        // model（模型）：保存 Object.Instantiate<GameObject>(GameObject) 的返回结果，供当前步骤使用。使用位置：作为 AnimationClip.SampleAnimation(GameObject, float) 的参数、作为 SceneManager.MoveGameObjectToScene(GameObject, Scene) 的参数、KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
GameObject model = UnityEngine.Object.Instantiate(asset);
        SceneManager.MoveGameObjectToScene(model, preview);
        try
        {
            // animator（动画控制器）：保存 GameObject.GetComponent<Animator>() 的返回结果，供当前步骤使用。使用位置：作为 Transform.InverseTransformPoint(Vector3) 的参数、KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
Animator animator = model.GetComponent<Animator>();
            // clips（动画片段集合）：使用位置：KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            // result（当前小刀敌人攻击配置档数组）：使用位置：KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
KnifeEnemyAttackProfile[] result = new KnifeEnemyAttackProfile[3];
            // move（移动）：使用位置：作为 IEnumerable<AnimationClip>.First<AnimationClip>(Func<AnimationClip, bool>) 的参数、KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
for (int move = 0; move < 3; move++)
            {
                // clip（动画片段）：保存 IEnumerable<AnimationClip>.First<AnimationClip>(Func<AnimationClip, bool>) 的返回结果，供当前步骤使用。使用位置：作为 List<object>.Add(object) 的参数、KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
AnimationClip clip = clips.First( /* 中文含义：当前动画片段。 */c => c.name == "Zombie_Attack0" + (move + 1));
                // segments（线段集合）：使用位置：作为 List<object>.Add(object) 的参数、KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
List<KnifeEnemyAttackSegment> segments = new List<KnifeEnemyAttackSegment>();
                if (move == 0)
                {
                    segments.Add(CreateSegment(HumanBodyBones.RightHand, .40f, .55f));
                    segments.Add(CreateSegment(HumanBodyBones.LeftHand, .53f, .74f));
                }
                else if (move == 1)
                {
                    segments.Add(CreateSegment(HumanBodyBones.RightHand, .57f, .83f));
                    segments.Add(CreateSegment(HumanBodyBones.LeftHand, .93f, 1.22f));
                }
                else
                {
                    segments.Add(CreateSegment(HumanBodyBones.LeftHand, .59f, .85f));
                }

                // segment（线段）：每轮从线段集合取得一个元素，仅代表当前这一项。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、作为 Mathf.CeilToInt(float) 的参数、作为 Mathf.Lerp(float, float, float) 的参数 等共 4 处。
foreach (KnifeEnemyAttackSegment segment in segments)
                {
                    // count（数量）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
int count = Mathf.CeilToInt((segment.end - segment.begin) * 120f) + 1;
                    segment.localPoints = new Vector3[count];
                    // index（当前元素索引）：使用位置：作为 Mathf.Lerp(float, float, float) 的参数、KnifeContactProfileBuilder.Bake(GameObject, List<object>)。
for (int index = 0; index < count; index++)
                    {
                        // time（时间）：保存 Mathf.Lerp(float, float, float) 的返回结果，供当前步骤使用。使用位置：作为 AnimationClip.SampleAnimation(GameObject, float) 的参数。
float time = Mathf.Lerp(segment.begin, segment.end, index / (float)(count - 1));
                        clip.SampleAnimation(model, time);
                        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        segment.localPoints[index] = model.transform.InverseTransformPoint(animator.GetBoneTransform(segment.hand).position);
                    }
                }

                result[move] = new KnifeEnemyAttackProfile
                {
                    canParry = true,
                    category = (KnifeIncomingAttackCategory)move,
                    sourceClip = clip,
                    contactSegments = segments.ToArray(),
                    promptEarly = .18f,
                    contactDistance = 1.25f,
                    contactAngle = 100,
                    contactNormalizedPoint = segments[0].begin / clip.length
                };
                var original = new SerializedObject(asset.GetComponent<EnemyCombatModule>())
                    .FindProperty("knifeAttackConfiguration");
                if (move < original.arraySize)
                {
                    var previous = (KnifeEnemyAttackProfile)original.GetArrayElementAtIndex(move).boxedValue;
                    if (previous != null && previous.sourceClip == clip)
                    {
                        result[move].contactPadding = previous.contactPadding;
                        result[move].windupLateralReduction = previous.windupLateralReduction;
                        result[move].maximumStartDistance = previous.maximumStartDistance;
                        result[move].planarRootTrajectory = previous.planarRootTrajectory;
                    }
                }

                report.Add(new { prefab = asset.name, clip = clip.name, duration = clip.length, oldEventSeconds = clip.length * .55f, promptLeadSeconds = .18f, segments = segments.Select( /* 中文含义：当前小刀敌人攻击线段。 */s => new { hand = s.hand.ToString(), s.begin, s.end, s.radius, samples = s.localPoints.Length }).ToArray(), note = "项目源动作轨迹标定，不是原作实测数值；实际接触由扫掠决定。" });
            }

            return result;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    /// <summary>CreateSegment 的业务处理入口；本方法直接依赖 KnifeEnemyAttackSegment.KnifeEnemyAttackSegment()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeContactProfileBuilder.Bake(GameObject, List&lt;object&gt;)；下游（含内部回调体） KnifeEnemyAttackSegment.KnifeEnemyAttackSegment()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="hand">中文含义：手部。使用位置：KnifeContactProfileBuilder.CreateSegment(HumanBodyBones, float, float)。</param>
    /// <param name="begin">中文含义：开始。使用位置：KnifeContactProfileBuilder.CreateSegment(HumanBodyBones, float, float)。</param>
    /// <param name="end">中文含义：结束。使用位置：KnifeContactProfileBuilder.CreateSegment(HumanBodyBones, float, float)。</param>
    private static KnifeEnemyAttackSegment CreateSegment(HumanBodyBones hand, float begin, float end)
    {
        return new KnifeEnemyAttackSegment
        {
            hand = hand,
            begin = begin,
            end = end,
            radius = .12f
        };
    }
}

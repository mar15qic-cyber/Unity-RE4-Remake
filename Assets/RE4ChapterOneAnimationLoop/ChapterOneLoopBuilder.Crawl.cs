#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public static int RefreshLegacyCrawl()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("请在编辑模式烘焙爬行动画。");
            }
            int count = 0;
            foreach (string role in new[] { "Ganado", "GanadoMale" })
            {
                string bankPath = Generated + "/Clips/" + role + "_Clips.asset";
                var bank = AssetDatabase.LoadAssetAtPath<ChapterOneBakedClips>(bankPath);
                var current = AssetDatabase.LoadAllAssetsAtPath(bankPath).OfType<AnimationClip>().ToDictionary(clip => clip.name);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Generated + "/" + role + ".prefab");
                var instance = Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                Avatar temporary = null;
                try
                {
                    temporary = BuildLegBiteAvatar(instance);
                    animator.avatar = temporary;
                    if (!temporary || !temporary.isValid || !temporary.isHuman)
                    {
                        throw new InvalidOperationException(role + " 缺少可采样的人形映射。");
                    }
                    var rig = instance.transform.Find(role + "_Skeleton");
                    var left = new ChapterOneArmAim.Arm(Node(instance, "L_Hand"));
                    var right = new ChapterOneArmAim.Arm(Node(instance, "R_Hand"));
                    void FitGround(int frame)
                    {
                        FitCrawlPalm(left);
                        FitCrawlPalm(right);
                    }
                    foreach (var item in ChapterOneRootActor.LegacyCrawlClips)
                    {
                        string path = "Assets/素材库/动画/Zombie_Animations/Animations/" + item.source + ".FBX";
                        var source = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                            .Single(clip => clip.name == item.source && clip.humanMotion);
                        var clip = BakeLegacyHumanoid(source, animator, rig, item.id, false, FitGround, false);
                        var root = ChapterOnePairMotion.ReadTrack(clip);
                        Vector3 delta = root.positions[^1] - root.positions[0];
                        string contact = "";
                        if (item.id == ChapterOneRootActor.CrawlAttackClip)
                        {
                            contact = "19–29（左手扫掠）";
                        }
                        Audit.Add(FormattableString.Invariant(
                            $"{role}\t{clip.name}\t{path}\t{clip.length:F4}\t{clip.frameRate}\t{AnimationUtility.GetCurveBindings(clip).Length}\t0\t{delta.x:F3},{delta.z:F3}\t{contact}\t{LegBiteConnected}"));
                        if (current.TryGetValue(item.id, out var existing))
                        {
                            EditorUtility.CopySerialized(clip, existing);
                            EditorUtility.SetDirty(existing);
                            Object.DestroyImmediate(clip);
                        }
                        else
                        {
                            AssetDatabase.AddObjectToAsset(clip, bank);
                        }
                        count++;
                    }
                    EditorUtility.SetDirty(bank);
                    AssetDatabase.SaveAssetIfDirty(bank);
                    WriteLegacyStates(role, ChapterOneRootActor.LegacyCrawlClips.Select(item => item.id));
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                    if (temporary)
                    {
                        Object.DestroyImmediate(temporary);
                    }
                }
            }
            ChapterOnePairMotion.Refresh();
            return count;
        }

        private static void FitCrawlPalm(ChapterOneArmAim.Arm arm)
        {
            const float floor = 0.048f;
            const float blendHeight = 0.038f;
            Vector3 target = arm.wrist.position;
            if (target.y >= floor + blendHeight)
            {
                return;
            }
            // 在接地前渐进托住掌部。保留根位移、手腕朝向和上臂长度。
            float rise = Mathf.InverseLerp(floor, floor + blendHeight, target.y);
            target.y = floor + blendHeight * rise * rise * (2f - rise);
            arm.Solve(target, arm.wrist.rotation, Vector3.down);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RE4.ChapterOne
{
    public static partial class ChapterOneLoopBuilder
    {
        public static string RefreshTraversalFixtures()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (EditorApplication.isPlaying || !scene.IsValid() || !scene.isLoaded || scene.isDirty)
            {
                throw new InvalidOperationException("请在编辑模式打开并保存独立动画闭环场景。");
            }
            var roots = scene.GetRootGameObjects();
            var terrain = roots.Single(root => root.name == "验收地形").transform;
            var flow = roots.Select(root => root.GetComponent<ChapterOneLoop>()).Single(item => item != null);
            var settings = new SerializedObject(flow);
            var old = (Transform)settings.FindProperty("vault").objectReferenceValue;
            Transform legacy = null;
            if (old != null && old.name == "翻窗起点" && old.parent == null)
            {
                legacy = old;
            }
            var windows = MakeTraversalFixtures(terrain, legacy);
            settings.FindProperty("vault").objectReferenceValue = windows.level;
            settings.FindProperty("dropVault").objectReferenceValue = windows.elevated;
            var navigation = BuildNavigation(terrain);
            AssetDatabase.SaveAssetIfDirty(navigation);
            settings.FindProperty("navigation").objectReferenceValue = navigation;
            settings.ApplyModifiedProperties();
            int tracks = ChapterOnePairMotion.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("无法保存独立场景的翻越设施。");
            }
            return "已保存平地与高处窗口，根轨道数量 " + tracks;
        }

        private static (Transform level, Transform elevated) MakeTraversalFixtures(Transform terrain, Transform legacy)
        {
            var timber = new Color(0.43f, 0.34f, 0.24f);
            var group = NodeAt(terrain, "验收通行设施", Vector3.zero);
            var level = Window("平地窗口", new Vector3(0f, 0f, -0.8f), legacy);
            var elevated = Window("高处跳窗", new Vector3(5.5f, 2f, 0.7f), null);
            Solid(group, "跳窗平台", new Vector3(5.5f, 1f, -1.15f), new Vector3(2.8f, 2f, 3.7f));
            Solid(group, "平台入口斜坡", new Vector3(5.5f, 0.85f, -5.45f), new Vector3(2.8f, 0.34f, 5.4f))
                .rotation = Quaternion.Euler(-22f, 0f, 0f);
            return (level, elevated);

            Transform Window(string name, Vector3 floor, Transform existing)
            {
                Transform frame = NodeAt(group, name, floor);
                Vector3 sillCenter = floor + Vector3.up * 0.51f;
                if (existing != null)
                {
                    existing.name = "窗台";
                    existing.SetParent(frame, true);
                }
                Transform sill = Solid(frame, "窗台", sillCenter, new Vector3(1.8f, 1.02f, 0.28f));
                Solid(frame, "左窗柱", floor + new Vector3(-1.02f, 1.37f, 0f), new Vector3(0.24f, 2.74f, 0.32f));
                Solid(frame, "右窗柱", floor + new Vector3(1.02f, 1.37f, 0f), new Vector3(0.24f, 2.74f, 0.32f));
                Solid(frame, "窗楣", floor + Vector3.up * 2.68f, new Vector3(1.8f, 0.12f, 0.28f));
                return sill;
            }

            Transform Solid(Transform parent, string name, Vector3 position, Vector3 size)
            {
                var item = parent.Find(name);
                if (item == null)
                {
                    item = Box(name, position, size, timber).transform;
                    item.SetParent(parent, true);
                }
                item.SetPositionAndRotation(position, Quaternion.identity);
                item.localScale = size;
                if (!item.TryGetComponent<Collider>(out _))
                {
                    item.gameObject.AddComponent<BoxCollider>();
                }
                return item;
            }
        }

        private static Transform NodeAt(Transform parent, string name, Vector3 position)
        {
            Transform result = parent.Find(name);
            if (result == null)
            {
                result = new GameObject(name).transform;
                result.SetParent(parent, true);
            }
            result.SetPositionAndRotation(position, Quaternion.identity);
            return result;
        }
    }
}
#endif

using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioLibrary 自定义检视面板
/// 提供事件列表管理和直接试听功能
/// 使用临时 AudioSource 实现真实音量音高应用
/// </summary>
[CustomEditor(typeof(AudioLibrary))]
public class AudioLibraryEditor : Editor
{
    /// <summary>资源库。</summary>
    private AudioLibrary library;
    /// <summary>选定事件索引。</summary>
    private int selectedEventIndex = 0;
    // 临时试听音源（与 AudioEventDataEditor 共享）
    /// <summary>预览游戏对象。</summary>
    private static GameObject previewGo;
    /// <summary>预览来源。</summary>
    private static AudioSource previewSource;
    /// <summary>OnEnable 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        library = target as AudioLibrary;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.quitting += OnEditorQuitting;
    }

    /// <summary>OnDisable 的业务处理入口；本方法直接依赖 AudioLibraryEditor.DestroyPreviewSource()、AudioLibraryEditor.StopPreview()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioLibraryEditor.DestroyPreviewSource()、AudioLibraryEditor.StopPreview()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        StopPreview();
        DestroyPreviewSource();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.quitting -= OnEditorQuitting;
    }

    /// <summary>实现基类的 OnInspectorGUI 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioLibraryEditor.DrawTestArea()、Editor.DrawDefaultInspector()、EditorGUILayout.Space(float)、SerializedObject.ApplyModifiedProperties()、SerializedObject.Update()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        EditorGUILayout.Space(8);
        DrawTestArea();
        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>DrawTestArea 的业务处理入口；本方法直接依赖 AudioLibraryEditor.PlayPreview(AudioEventData)、AudioLibraryEditor.StopPreview()、EditorGUILayout.BeginHorizontal(params GUILayoutOption[])、EditorGUILayout.BeginVertical(GUIStyle, params GUILayoutOption[])、EditorGUILayout.EndHorizontal()、EditorGUILayout.EndVertical()、EditorGUILayout.HelpBox(string, MessageType) 等共 16 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioLibraryEditor.OnInspectorGUI()；下游（含内部回调体） AudioLibraryEditor.PlayPreview(AudioEventData)、AudioLibraryEditor.StopPreview()、EditorGUILayout.BeginHorizontal(params GUILayoutOption[])、EditorGUILayout.BeginVertical(GUIStyle, params GUILayoutOption[])、EditorGUILayout.EndHorizontal()、EditorGUILayout.EndVertical()、EditorGUILayout.HelpBox(string, MessageType) 等共 16 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawTestArea()
    {
        EditorGUILayout.LabelField("试听区域", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            if (library.entries == null || library.entries.Count == 0)
            {
                EditorGUILayout.HelpBox("事件列表为空，请先添加音效事件。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            // 事件名称列表。
string[] eventNames = new string[library.entries.Count];
            // 当前遍历索引。
for (int i = 0; i < library.entries.Count; i++)
            {
                // 赋值前保存的目标事件名称列表。
string[] assignmentReceiver1 = eventNames;
                // 赋值前保存的索引当前遍历索引。
int assignmentIndex2 = i;
                if (library.entries[i] != null)
                {
                    // 插值文本使用的资源库的条目集合的名称。
string formatArgument4 = library.entries[i].name;
                    // 分支选出的条目集合的事件配置的名称。
string selectedValue3;
                    if (library.entries[i].eventData != null)
                    {
                        selectedValue3 = library.entries[i].eventData.name;
                    }
                    else
                    {
                        selectedValue3 = "未配置";
                    }

                    assignmentReceiver1[assignmentIndex2] = $"{formatArgument4} ({(selectedValue3)})";
                }
                else
                {
                    assignmentReceiver1[assignmentIndex2] = $"条目 {i} (空)";
                }
            }

            selectedEventIndex = Mathf.Clamp(selectedEventIndex, 0, library.entries.Count - 1);
            selectedEventIndex = EditorGUILayout.Popup("选择事件", selectedEventIndex, eventNames);
            // 选定入口。
var selectedEntry = library.entries[selectedEventIndex];
            if (selectedEntry != null && selectedEntry.eventData != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("事件名称", selectedEntry.name);
                EditorGUILayout.LabelField("配置资产", selectedEntry.eventData.name);
                // 动画片段数量。
int clipCount = 0;
                if (selectedEntry.eventData.clips != null)
                {
                    // 当前遍历索引。
for (int i = 0; i < selectedEntry.eventData.clips.Length; i++)
                    {
                        if (selectedEntry.eventData.clips[i] != null && selectedEntry.eventData.clips[i].clip != null)
                        {
                            clipCount++;
                        }
                    }
                }

                EditorGUILayout.LabelField("可用片段数", clipCount.ToString());
                EditorGUILayout.LabelField("分类", selectedEntry.eventData.category.ToString());
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("随机试听", GUILayout.Height(28)))
                    {
                        PlayPreview(selectedEntry.eventData);
                    }

                    if (GUILayout.Button("停止试听", GUILayout.Height(28)))
                    {
                        StopPreview();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("选中条目未配置事件数据。", MessageType.Warning);
            }
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>PlayPreview 的业务处理入口；本方法直接依赖 AssetDatabase.FindAssets(string)、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;AudioMixer&gt;(string)、AudioEventData.GetMixerGroupPath()、AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventData.SelectClip() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioLibraryEditor.DrawTestArea()；下游（含内部回调体） AssetDatabase.FindAssets(string)、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;AudioMixer&gt;(string)、AudioEventData.GetMixerGroupPath()、AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventData.SelectClip() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    private void PlayPreview(AudioEventData eventData)
    {
        StopPreview();
        EnsurePreviewSource();
        // 音频片段。
var clip = eventData.SelectClip();
        if (clip == null)
        {
            Debug.LogWarning($"[音频试听] 事件没有可用片段。");
            return;
        }

        // 音量。
float volume = eventData.GetVolume();
        // 音高。
float pitch = eventData.GetPitch();
        // 尝试设置混音分组
        // 分组。
AudioMixerGroup group = null;
        if (!string.IsNullOrEmpty(eventData.GetMixerGroupPath()))
        {
            // 混音器集合。
var mixers = AssetDatabase.FindAssets("t:AudioMixer");
            // guid（资源标识）：每轮从混音器集合取得一个元素，仅代表当前这一项。使用位置：作为 AssetDatabase.GUIDToAssetPath(string) 的参数。
foreach (var guid in mixers)
            {
                // 路径。
var path = AssetDatabase.GUIDToAssetPath(guid);
                // 混音器。
var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                if (mixer != null)
                {
                    // 分组集合。
var groups = mixer.FindMatchingGroups(eventData.GetMixerGroupPath());
                    if (groups != null && groups.Length > 0)
                    {
                        group = groups[0];
                        break;
                    }
                }
            }
        }

        previewSource.clip = clip;
        previewSource.volume = volume;
        previewSource.pitch = pitch;
        previewSource.loop = false;
        previewSource.outputAudioMixerGroup = group;
        // 赋值前保存的目标预览来源。
global::UnityEngine.AudioSource assignmentReceiver6 = previewSource;
        if (eventData.spatialMode == AudioEventData.SpatialMode.ThreeD)
        {
            assignmentReceiver6.spatialBlend = 1f;
        }
        else
        {
            assignmentReceiver6.spatialBlend = 0f;
        }

        previewSource.Play();
        // 插值文本使用的动画片段的名称。
string formatArgument9 = clip.name;
        // 插值文本使用的音量。
float formatArgument10 = volume;
        // 插值文本使用的音高。
float formatArgument11 = pitch;
        // 分支选出的分组的名称。
string selectedValue8;
        if (group != null)
        {
            selectedValue8 = group.name;
        }
        else
        {
            selectedValue8 = "无";
        }

        Debug.Log($"[音频试听] 播放: {formatArgument9}, 音量={formatArgument10:F2}, 音高={formatArgument11:F2}, 混音分组={(selectedValue8)}");
    }

    /// <summary>StopPreview 的业务处理入口；本方法直接依赖 AudioSource.Stop()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioLibraryEditor.DrawTestArea()、AudioLibraryEditor.OnDisable()、AudioLibraryEditor.PlayPreview(AudioEventData)；下游（含内部回调体） AudioSource.Stop()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void StopPreview()
    {
        if (previewSource != null)
        {
            if (previewSource.isPlaying)
            {
                previewSource.Stop();
            }

            previewSource.clip = null;
            previewSource.outputAudioMixerGroup = null;
        }
    }

    /// <summary>OnPlayModeChanged 的业务处理入口；本方法直接依赖 AudioLibraryEditor.DestroyPreviewSource()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioLibraryEditor.DestroyPreviewSource()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">当前播放模式状态变化。</param>
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            DestroyPreviewSource();
        }
    }

    /// <summary>OnEditorQuitting 的业务处理入口；本方法直接依赖 AudioLibraryEditor.DestroyPreviewSource()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioLibraryEditor.DestroyPreviewSource()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void OnEditorQuitting()
    {
        DestroyPreviewSource();
    }

    /// <summary>DestroyPreviewSource 的业务处理入口；本方法直接依赖 Object.DestroyImmediate(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioLibraryEditor.OnDisable()、AudioLibraryEditor.OnEditorQuitting()、AudioLibraryEditor.OnPlayModeChanged(PlayModeStateChange)；下游（含内部回调体） Object.DestroyImmediate(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void DestroyPreviewSource()
    {
        if (previewGo != null)
        {
            Object.DestroyImmediate(previewGo);
        }

        previewGo = null;
        previewSource = null;
    }

    /// <summary>EnsurePreviewSource 的业务处理入口；本方法直接依赖 GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioLibraryEditor.PlayPreview(AudioEventData)；下游（含内部回调体） GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void EnsurePreviewSource()
    {
        if (previewGo == null)
        {
            previewGo = new GameObject("__AudioLibraryPreview_Temp__");
            previewGo.hideFlags = HideFlags.HideAndDontSave;
            previewSource = previewGo.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
        }
    }
}

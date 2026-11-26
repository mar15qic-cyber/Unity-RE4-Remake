using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// AudioEventData 自定义检视面板
/// 提供试听、调参、清理功能，无需进入运行模式
/// 使用临时隐藏 AudioSource 实现真实音量音高应用
/// </summary>
[CustomEditor(typeof(AudioEventData))]
public class AudioEventDataEditor : Editor
{
    /// <summary>当前音频事件配置。</summary>
    private AudioEventData data;
    // 试听状态
    /// <summary>预览动画片段索引。</summary>
    private int previewClipIndex = -1;
    /// <summary>预览动画片段名称。</summary>
    private string previewClipName = "";
    /// <summary>是否循环播放。</summary>
    private bool isLooping;
    // 临时试听音源
    /// <summary>预览游戏对象。</summary>
    private static GameObject previewGo;
    /// <summary>预览来源。</summary>
    private static AudioSource previewSource;
    // 折叠状态
    /// <summary>是否动画片段集合折叠面板。</summary>
    private bool clipsFoldout = true;
    /// <summary>是否音量折叠面板。</summary>
    private bool volumeFoldout = true;
    /// <summary>是否音高折叠面板。</summary>
    private bool pitchFoldout = true;
    /// <summary>是否空间音效折叠面板。</summary>
    private bool spatialFoldout = true;
    /// <summary>是否控制折叠面板。</summary>
    private bool controlFoldout = true;
    /// <summary>是否路由折叠面板。</summary>
    private bool routingFoldout = true;
    /// <summary>OnEnable 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnEnable()
    {
        data = target as AudioEventData;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.quitting += OnEditorQuitting;
    }

    /// <summary>OnDisable 的业务处理入口；本方法直接依赖 AudioEventDataEditor.DestroyPreviewSource()、AudioEventDataEditor.StopPreview()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioEventDataEditor.DestroyPreviewSource()、AudioEventDataEditor.StopPreview()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void OnDisable()
    {
        StopPreview();
        DestroyPreviewSource();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.quitting -= OnEditorQuitting;
    }

    /// <summary>
    /// 进入运行模式时停止试听并清理
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioEventDataEditor.StopPreview()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="state">当前播放模式状态变化。</param>
    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingEditMode)
        {
            StopPreview();
        }
    }

    /// <summary>实现基类的 OnInspectorGUI 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioEventDataEditor.DrawClipsSection()、AudioEventDataEditor.DrawControlSection()、AudioEventDataEditor.DrawPitchSection()、AudioEventDataEditor.DrawPreviewSection()、AudioEventDataEditor.DrawResetButton()、AudioEventDataEditor.DrawRoutingSection()、AudioEventDataEditor.DrawSpatialSection() 等共 12 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPreviewSection();
        EditorGUILayout.Space(4);
        DrawClipsSection();
        EditorGUILayout.Space(4);
        DrawVolumeSection();
        EditorGUILayout.Space(4);
        DrawPitchSection();
        EditorGUILayout.Space(4);
        DrawSpatialSection();
        EditorGUILayout.Space(4);
        DrawControlSection();
        EditorGUILayout.Space(4);
        DrawRoutingSection();
        EditorGUILayout.Space(4);
        DrawWarnings();
        EditorGUILayout.Space(4);
        DrawResetButton();
        serializedObject.ApplyModifiedProperties();
    }

#region ===== 试听区域 =====
    /// <summary>DrawPreviewSection 的业务处理入口；本方法直接依赖 AudioEventDataEditor.IsPreviewPlaying()、AudioEventDataEditor.PlayRandomPreview(bool)、AudioEventDataEditor.StopPreview()、EditorGUILayout.BeginHorizontal(params GUILayoutOption[])、EditorGUILayout.BeginVertical(GUIStyle, params GUILayoutOption[])、EditorGUILayout.EndHorizontal()、EditorGUILayout.EndVertical() 等共 14 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） AudioEventDataEditor.IsPreviewPlaying()、AudioEventDataEditor.PlayRandomPreview(bool)、AudioEventDataEditor.StopPreview()、EditorGUILayout.BeginHorizontal(params GUILayoutOption[])、EditorGUILayout.BeginVertical(GUIStyle, params GUILayoutOption[])、EditorGUILayout.EndHorizontal()、EditorGUILayout.EndVertical() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawPreviewSection()
    {
        EditorGUILayout.LabelField("试听", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            // 当前试听状态
            if (IsPreviewPlaying())
            {
                EditorGUILayout.LabelField("正在试听", previewClipName);
                // 按原顺序缓存的实参文字“循环”。
string argumentValue2 = "循环";
                // 分支选出的文字“是”。
string selectedValue1;
                if (isLooping)
                {
                    selectedValue1 = "是";
                }
                else
                {
                    selectedValue1 = "否";
                }

                EditorGUILayout.LabelField(argumentValue2, selectedValue1);
                if (previewSource != null && previewSource.clip != null)
                {
                    // 位置。
float pos = previewSource.time;
                    // 长度。
float len = previewSource.clip.length;
                    EditorGUILayout.LabelField("进度", $"{pos:F1}s / {len:F1}s");
                }
            }
            else
            {
                EditorGUILayout.LabelField("未在试听");
            }

            EditorGUILayout.Space(4);
            // 试听按钮
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("随机试听一次", GUILayout.Height(28)))
                {
                    PlayRandomPreview(false);
                }

                if (GUILayout.Button("循环试听", GUILayout.Height(28)))
                {
                    PlayRandomPreview(true);
                }

                if (GUILayout.Button("停止试听", GUILayout.Height(28)))
                {
                    StopPreview();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("试听使用当前参数（音量、音高、混音分组）。修改参数后再次试听将使用最新值。", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 为单个片段绘制试听按钮
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.DrawClipsSection()；下游（含内部回调体） AudioEventDataEditor.IsPreviewPlaying()、AudioEventDataEditor.PlayClipPreview(AudioClip, int, bool)、AudioEventDataEditor.StopPreview()、GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、GUILayout.Width(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">音频片段。</param>
    /// <param name="index">索引。</param>
    private void DrawClipPreviewButton(AudioClip clip, int index)
    {
        if (clip == null)
        {
            return;
        }

        // 是否正在播放。
bool isPlaying = previewClipIndex == index && IsPreviewPlaying();
        // 标签。
string label;
        if (isPlaying)
        {
            label = "■";
        }
        else
        {
            label = "▶";
        }

        if (GUILayout.Button(label, GUILayout.Width(24), GUILayout.Height(18)))
        {
            if (isPlaying)
            {
                StopPreview();
            }
            else
            {
                PlayClipPreview(clip, index, false);
            }
        }
    }

    /// <summary>
    /// 随机试听
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.DrawPreviewSection()；下游（含内部回调体） AudioEventData.SelectClip()、AudioEventDataEditor.PlayClipPreview(AudioClip, int, bool)、Debug.LogWarning(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="loop">是否循环。</param>
    private void PlayRandomPreview(bool loop)
    {
        if (data.clips == null || data.clips.Length == 0)
        {
            Debug.LogWarning("[音频试听] 没有可播放的片段。");
            return;
        }

        // 音频片段。
AudioClip clip = data.SelectClip();
        if (clip == null)
        {
            Debug.LogWarning("[音频试听] 没有启用的片段。");
            return;
        }

        // 当前元素索引。
int index = -1;
        // 当前遍历索引。
for (int i = 0; i < data.clips.Length; i++)
        {
            if (data.clips[i] != null && data.clips[i].clip == clip)
            {
                index = i;
                break;
            }
        }

        PlayClipPreview(clip, index, loop);
    }

    /// <summary>
    /// 播放指定片段试听——使用临时 AudioSource 真实应用音量和音高
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.DrawClipPreviewButton(AudioClip, int)、AudioEventDataEditor.PlayRandomPreview(bool)；下游（含内部回调体） AssetDatabase.FindAssets(string)、AssetDatabase.GUIDToAssetPath(string)、AssetDatabase.LoadAssetAtPath&lt;AudioMixer&gt;(string)、AudioEventData.GetMixerGroupPath()、AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventDataEditor.EnsurePreviewSource() 等共 14 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">音频片段。</param>
    /// <param name="index">索引。</param>
    /// <param name="loop">是否循环。</param>
    private void PlayClipPreview(AudioClip clip, int index, bool loop)
    {
        StopPreview();
        EnsurePreviewSource();
        if (previewSource == null)
        {
            Debug.LogError("[音频试听] 无法创建试听音源。");
            return;
        }

        // 每次试听使用最新参数
        // 音量。
float volume = data.GetVolume();
        // 音高。
float pitch = data.GetPitch();
        // 尝试设置混音分组
        // 分组。
AudioMixerGroup group = null;
        if (!string.IsNullOrEmpty(data.GetMixerGroupPath()))
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
var groups = mixer.FindMatchingGroups(data.GetMixerGroupPath());
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
        previewSource.loop = loop;
        previewSource.outputAudioMixerGroup = group;
        // 赋值前保存的目标预览来源。
global::UnityEngine.AudioSource assignmentReceiver4 = previewSource;
        if (data.spatialMode == AudioEventData.SpatialMode.ThreeD)
        {
            assignmentReceiver4.spatialBlend = 1f;
        }
        else
        {
            assignmentReceiver4.spatialBlend = 0f;
        }

        previewSource.Play();
        previewClipIndex = index;
        previewClipName = clip.name;
        isLooping = loop;
        // 插值文本使用的动画片段的名称。
string formatArgument7 = clip.name;
        // 插值文本使用的音量。
float formatArgument8 = volume;
        // 插值文本使用的音高。
float formatArgument9 = pitch;
        // 插值文本使用的是否循环。
bool formatArgument10 = loop;
        // 分支选出的分组的名称。
string selectedValue6;
        if (group != null)
        {
            selectedValue6 = group.name;
        }
        else
        {
            selectedValue6 = "无";
        }

        Debug.Log($"[音频试听] 开始播放: {formatArgument7}, 音量={formatArgument8:F2}, 音高={formatArgument9:F2}, 循环={formatArgument10}, 混音分组={(selectedValue6)}");
        EditorApplication.QueuePlayerLoopUpdate();
    }

    /// <summary>
    /// 停止试听
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.DrawClipPreviewButton(AudioClip, int)、AudioEventDataEditor.DrawPreviewSection()、AudioEventDataEditor.OnDisable()、AudioEventDataEditor.OnPlayModeChanged(PlayModeStateChange)、AudioEventDataEditor.PlayClipPreview(AudioClip, int, bool)；下游（含内部回调体） AudioSource.Stop()、Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
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
            Debug.Log("[音频试听] 已停止试听");
        }

        previewClipName = "";
        previewClipIndex = -1;
        isLooping = false;
    }

    /// <summary>DestroyPreviewSource 的业务处理入口；本方法直接依赖 Object.DestroyImmediate(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnDisable()、AudioEventDataEditor.OnEditorQuitting()、AudioEventDataEditor.OnScriptsReloaded()；下游（含内部回调体） Object.DestroyImmediate(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void DestroyPreviewSource()
    {
        if (previewGo != null)
        {
            Object.DestroyImmediate(previewGo);
        }

        previewGo = null;
        previewSource = null;
    }

    /// <summary>OnEditorQuitting 的业务处理入口；本方法直接依赖 AudioEventDataEditor.DestroyPreviewSource()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioEventDataEditor.DestroyPreviewSource()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void OnEditorQuitting()
    {
        DestroyPreviewSource();
    }

    /// <summary>
    /// 检查试听是否正在播放
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.DrawClipPreviewButton(AudioClip, int)、AudioEventDataEditor.DrawPreviewSection()；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private bool IsPreviewPlaying()
    {
        return previewSource != null && previewSource.isPlaying;
    }

    /// <summary>
    /// 确保临时试听音源存在（不在场景中持久化）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.PlayClipPreview(AudioClip, int, bool)；下游（含内部回调体） GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void EnsurePreviewSource()
    {
        if (previewGo == null)
        {
            previewGo = new GameObject("__AudioPreview_Temp__");
            previewGo.hideFlags = HideFlags.HideAndDontSave;
            previewSource = previewGo.AddComponent<AudioSource>();
            previewSource.playOnAwake = false;
        }
    }

    /// <summary>
    /// 清理临时试听音源（程序集重载、编辑器退出时调用）
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitializeCleanup()
    {
        EditorApplication.update += CleanupIfDestroyed;
    }

    /// <summary>CleanupIfDestroyed 的状态或数值处理入口；通过当前参数与已有成员完成计算，不据静态扫描推断额外的运行期绑定。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private static void CleanupIfDestroyed()
    {
        if (previewGo == null && previewSource != null)
        {
            previewSource = null;
        }
    }

#endregion
#region ===== 片段列表 =====
    /// <summary>DrawClipsSection 的业务处理入口；本方法直接依赖 AudioEventDataEditor.DrawClipPreviewButton(AudioClip, int)、EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.BeginHorizontal(GUIStyle, params GUILayoutOption[])、EditorGUILayout.BeginHorizontal(params GUILayoutOption[])、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.EndHorizontal()、EditorGUILayout.LabelField(string, params GUILayoutOption[]) 等共 21 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） AudioEventDataEditor.DrawClipPreviewButton(AudioClip, int)、EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.BeginHorizontal(GUIStyle, params GUILayoutOption[])、EditorGUILayout.BeginHorizontal(params GUILayoutOption[])、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.EndHorizontal()、EditorGUILayout.LabelField(string, params GUILayoutOption[]) 等共 21 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawClipsSection()
    {
        clipsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(clipsFoldout, "片段列表");
        if (clipsFoldout)
        {
            // 动画片段集合属性。
var clipsProp = serializedObject.FindProperty("clips");
            // 当前遍历索引。
for (int i = 0; i < clipsProp.arraySize; i++)
            {
                // 当前序列化的属性。
var entry = clipsProp.GetArrayElementAtIndex(i);
                // 动画片段属性。
var clipProp = entry.FindPropertyRelative("clip");
                // 启用属性。
var enabledProp = entry.FindPropertyRelative("enabled");
                // 权重属性。
var weightProp = entry.FindPropertyRelative("weight");
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                {
                    enabledProp.boolValue = EditorGUILayout.Toggle(enabledProp.boolValue, GUILayout.Width(16));
                    clipProp.objectReferenceValue = EditorGUILayout.ObjectField(clipProp.objectReferenceValue, typeof(AudioClip), false, GUILayout.MinWidth(120));
                    DrawClipPreviewButton(clipProp.objectReferenceValue as AudioClip, i);
                    EditorGUILayout.LabelField("权重", GUILayout.Width(30));
                    weightProp.floatValue = EditorGUILayout.Slider(weightProp.floatValue, 0f, 1f, GUILayout.Width(80));
                    if (GUILayout.Button("↑", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        if (i > 0)
                        {
                            clipsProp.MoveArrayElement(i, i - 1);
                        }
                    }

                    if (GUILayout.Button("↓", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        if (i < clipsProp.arraySize - 1)
                        {
                            clipsProp.MoveArrayElement(i, i + 1);
                        }
                    }

                    if (GUILayout.Button("×", GUILayout.Width(22), GUILayout.Height(18)))
                    {
                        clipsProp.DeleteArrayElementAtIndex(i);
                        EditorGUILayout.EndHorizontal();
                        return;
                    }
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("添加片段", GUILayout.Height(22)))
                {
                    clipsProp.InsertArrayElementAtIndex(clipsProp.arraySize);
                    // 新入口。
var newEntry = clipsProp.GetArrayElementAtIndex(clipsProp.arraySize - 1);
                    newEntry.FindPropertyRelative("enabled").boolValue = true;
                    newEntry.FindPropertyRelative("weight").floatValue = 1f;
                }
            }

            EditorGUILayout.EndHorizontal();
            // 模式属性。
var modeProp = serializedObject.FindProperty("selectionMode");
            EditorGUILayout.PropertyField(modeProp);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

#endregion
#region ===== 音量 =====
    /// <summary>DrawVolumeSection 的业务处理入口；本方法直接依赖 EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、EditorGUILayout.Slider(SerializedProperty, float, float, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、EditorGUILayout.Slider(SerializedProperty, float, float, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawVolumeSection()
    {
        volumeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(volumeFoldout, "音量");
        if (volumeFoldout)
        {
            // 基础音量。
var baseVol = serializedObject.FindProperty("baseVolume");
            EditorGUILayout.Slider(baseVol, 0f, 1f, new GUIContent("基础音量"));
            // 音量范围。
var volRange = serializedObject.FindProperty("volumeRange");
            EditorGUILayout.PropertyField(volRange, new GUIContent("随机音量范围"));
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

#endregion
#region ===== 音高 =====
    /// <summary>DrawPitchSection 的业务处理入口；本方法直接依赖 EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、EditorGUILayout.Slider(SerializedProperty, float, float, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、EditorGUILayout.Slider(SerializedProperty, float, float, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawPitchSection()
    {
        pitchFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(pitchFoldout, "音高");
        if (pitchFoldout)
        {
            // 基础音高。
var basePitch = serializedObject.FindProperty("basePitch");
            EditorGUILayout.Slider(basePitch, 0f, 3f, new GUIContent("基础音高"));
            // 随机音高变化范围。
var pitchRange = serializedObject.FindProperty("pitchRange");
            EditorGUILayout.PropertyField(pitchRange, new GUIContent("随机音高范围"));
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

#endregion
#region ===== 空间设置 =====
    /// <summary>DrawSpatialSection 的业务处理入口；本方法直接依赖 EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.HelpBox(string, MessageType)、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、EditorGUILayout.Slider(SerializedProperty, float, float, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.HelpBox(string, MessageType)、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、EditorGUILayout.Slider(SerializedProperty, float, float, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawSpatialSection()
    {
        spatialFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(spatialFoldout, "空间设置");
        if (spatialFoldout)
        {
            // 模式属性。
var modeProp = serializedObject.FindProperty("spatialMode");
            EditorGUILayout.PropertyField(modeProp, new GUIContent("播放模式"));
            if (data.spatialMode == AudioEventData.SpatialMode.ThreeD)
            {
                // 最小距离。
var minDist = serializedObject.FindProperty("minDistance");
                // 最大距离。
var maxDist = serializedObject.FindProperty("maxDistance");
                // 衰减。
var rolloff = serializedObject.FindProperty("rolloffMode");
                EditorGUILayout.Slider(minDist, 0f, 500f, new GUIContent("最小距离"));
                EditorGUILayout.Slider(maxDist, 0f, 1000f, new GUIContent("最大距离"));
                EditorGUILayout.PropertyField(rolloff, new GUIContent("衰减模式"));
                if (data.maxDistance <= data.minDistance)
                {
                    EditorGUILayout.HelpBox("最大距离必须大于最小距离。", MessageType.Warning);
                }
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

#endregion
#region ===== 播放控制 =====
    /// <summary>DrawControlSection 的业务处理入口；本方法直接依赖 EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、EditorGUILayout.Slider(SerializedProperty, float, float, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string, string)、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、EditorGUILayout.Slider(SerializedProperty, float, float, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string, string)、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawControlSection()
    {
        controlFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(controlFoldout, "播放控制");
        if (controlFoldout)
        {
            // 优先级。
var priority = serializedObject.FindProperty("priority");
            // 冷却。
var cooldown = serializedObject.FindProperty("cooldown");
            // 最大并发。
var maxConcurrent = serializedObject.FindProperty("maxConcurrent");
            // 循环。
var loop = serializedObject.FindProperty("loop");
            EditorGUILayout.Slider(priority, 0, 256, new GUIContent("优先级", "数值越小优先级越高"));
            EditorGUILayout.Slider(cooldown, 0f, 10f, new GUIContent("冷却时间（秒）"));
            EditorGUILayout.Slider(maxConcurrent, 1, 32, new GUIContent("最大并发数"));
            EditorGUILayout.PropertyField(loop, new GUIContent("循环播放"));
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

#endregion
#region ===== 分类与路由 =====
    /// <summary>DrawRoutingSection 的业务处理入口；本方法直接依赖 EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string, string)、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） EditorGUILayout.BeginFoldoutHeaderGroup(bool, string, GUIStyle, Action&lt;Rect&gt;, GUIStyle)、EditorGUILayout.EndFoldoutHeaderGroup()、EditorGUILayout.PropertyField(SerializedProperty, GUIContent, params GUILayoutOption[])、GUIContent.GUIContent(string, string)、GUIContent.GUIContent(string)、SerializedObject.FindProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawRoutingSection()
    {
        routingFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(routingFoldout, "分类与路由");
        if (routingFoldout)
        {
            // 类别属性。
var categoryProp = serializedObject.FindProperty("category");
            EditorGUILayout.PropertyField(categoryProp, new GUIContent("音频分类"));
            // 混音器路径。
var mixerPath = serializedObject.FindProperty("mixerGroupPath");
            EditorGUILayout.PropertyField(mixerPath, new GUIContent("混音分组路径", "留空时由分类自动推断"));
            // 语音优先级。
var voicePriority = serializedObject.FindProperty("voicePriority");
            EditorGUILayout.PropertyField(voicePriority, new GUIContent("语音优先级"));
            // 警告缺失的。
var warnMissing = serializedObject.FindProperty("warnIfMissing");
            EditorGUILayout.PropertyField(warnMissing, new GUIContent("片段缺失时警告"));
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

#endregion
#region ===== 警告 =====
    /// <summary>DrawWarnings 的业务处理入口；本方法直接依赖 EditorGUILayout.HelpBox(string, MessageType)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） EditorGUILayout.HelpBox(string, MessageType)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawWarnings()
    {
        // 是否具有警告。
bool hasWarning = false;
        // 警告集合。
string warnings = "";
        if (data.clips != null)
        {
            // 当前遍历索引。
for (int i = 0; i < data.clips.Length; i++)
            {
                if (data.clips[i] == null || data.clips[i].clip == null)
                {
                    warnings += $"片段 {i} 的音频引用为空。\n";
                    hasWarning = true;
                }
            }
        }

        if (data.clips != null && data.clips.Length > 1)
        {
            // 当前遍历索引。
for (int i = 0; i < data.clips.Length; i++)
            {
                if (data.clips[i] == null || data.clips[i].clip == null)
                {
                    continue;
                }

                // 内层遍历索引。
for (int j = i + 1; j < data.clips.Length; j++)
                {
                    if (data.clips[j] != null && data.clips[j].clip == data.clips[i].clip)
                    {
                        warnings += $"片段 {i} 和 {j} 引用了相同的音频。\n";
                        hasWarning = true;
                    }
                }
            }
        }

        if (data.spatialMode == AudioEventData.SpatialMode.ThreeD && data.maxDistance <= data.minDistance)
        {
            warnings += "三维模式下最大距离必须大于最小距离。\n";
            hasWarning = true;
        }

        if (hasWarning)
        {
            EditorGUILayout.HelpBox(warnings, MessageType.Warning);
        }
    }

#endregion
#region ===== 重置 =====
    /// <summary>DrawResetButton 的业务处理入口；本方法直接依赖 EditorUtility.DisplayDialog(string, string, string, string)、EditorUtility.SetDirty(Object)、GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioEventDataEditor.OnInspectorGUI()；下游（含内部回调体） EditorUtility.DisplayDialog(string, string, string, string)、EditorUtility.SetDirty(Object)、GUILayout.Button(string, params GUILayoutOption[])、GUILayout.Height(float)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void DrawResetButton()
    {
        if (GUILayout.Button("恢复默认参数", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("恢复默认参数", "确定要将所有参数恢复为默认值吗？", "确定", "取消"))
            {
                data.baseVolume = 1f;
                data.volumeRange = new Vector2(1f, 1f);
                data.basePitch = 1f;
                data.pitchRange = new Vector2(1f, 1f);
                data.spatialMode = AudioEventData.SpatialMode.TwoD;
                data.minDistance = 1f;
                data.maxDistance = 50f;
                data.rolloffMode = AudioRolloffMode.Logarithmic;
                data.priority = 128;
                data.cooldown = 0f;
                data.maxConcurrent = 8;
                data.loop = false;
                data.category = AudioCategory.SFX;
                data.mixerGroupPath = "";
                data.voicePriority = AudioEventData.VoicePriority.Normal;
                data.selectionMode = AudioEventData.SelectionMode.Random;
                data.warnIfMissing = true;
                EditorUtility.SetDirty(data);
            }
        }
    }

#endregion
    /// <summary>OnScriptsReloaded 的业务处理入口；本方法直接依赖 AudioEventDataEditor.DestroyPreviewSource()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioEventDataEditor.DestroyPreviewSource()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [UnityEditor.Callbacks.DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        // 脚本重载时清理临时试听对象
        DestroyPreviewSource();
    }
}

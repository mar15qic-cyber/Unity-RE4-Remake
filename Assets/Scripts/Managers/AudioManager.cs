using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 音频管理器：统一管理背景音乐和音效的播放、停止、音量控制
/// 实现数据驱动模式，通过 AudioEventData 配置资产播放音效
/// 实现 IAudioVolumeService 接口供界面模块统一控制音量
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : Singleton<AudioManager>, IAudioVolumeService, IManualSingleton
{
#region ===== 配置引用 =====
    /// <summary>全局音频设置资产。</summary>
    [Header("配置")]
    [Tooltip("全局音频设置资产")]
    public AudioGlobalSettings settings;
    /// <summary>音频事件库资产。</summary>
    [Tooltip("音频事件库资产")]
    public AudioLibrary library;
#endregion
#region ===== 内部音源 =====
    /// <summary>背景音乐来源。</summary>
    private AudioSource bgmSource;
    /// <summary>对象池3距离。</summary>
    private AudioSourcePool pool3D;
    /// <summary>对象池2距离。</summary>
    private AudioSourcePool pool2D;
    /// <summary>对象池根节点。</summary>
    private Transform poolRoot;
#endregion
#region ===== 混音器缓存 =====
    /// <summary>分组查找表。</summary>
    private Dictionary<string, AudioMixerGroup> groupLookup;
    /// <summary>已警告缺失的。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HashSet<string> warnedMissing = new HashSet<string>();
    /// <summary>已警告缺失的混音器参数集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HashSet<string> warnedMissingMixerParameters = new HashSet<string>();
    /// <summary>可用混音器参数集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly HashSet<string> availableMixerParameters = new HashSet<string>();
    /// <summary>背景音乐淡入淡出协程。</summary>
    private Coroutine bgmFadeCoroutine;
    private bool bgmFadingOut;
    /// <summary>当前背景音乐事件。</summary>
    private AudioEventData currentBgmEvent;
    /// <summary>当前背景音乐目标音量。</summary>
    private float currentBgmTargetVolume;
    /// <summary>中文含义：已暂停类别体积区域集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly Dictionary<AudioCategory, float> pausedCategoryVolumes = new Dictionary<AudioCategory, float>();
    /// <summary>中文含义：是否音频类别集合已暂停。</summary>
    private bool audioCategoriesPaused;
    /// <summary>是否音频已暂停。</summary>
    private bool isAudioPaused;
    /// <summary>背景音乐之前是否播放中之前暂停。</summary>
    private bool bgmWasPlayingBeforePause;
    /// <summary>已登记外部来源集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<AudioSource> registeredExternalSources = new List<AudioSource>();
    /// <summary>已暂停外部来源集合。只读字段，初始化后不由普通写入口替换。</summary>
    private readonly List<AudioSource> pausedExternalSources = new List<AudioSource>();
    private Scene mainAudioScene;
#endregion
#region ===== 句柄系统 =====
    /// <summary>下一次句柄编号。</summary>
    private static int nextHandleId = 1;
    /// <summary>有效句柄集合。只读字段，初始化后不由普通写入口替换。</summary>
    private static readonly HashSet<int> validHandles = new HashSet<int>();
    /// <summary>
    /// 检查句柄是否有效
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.ReleaseAfterHandle(int, float, AudioSourcePool)、AudioPlayHandle.IsValid.get；下游（含内部回调体） HashSet&lt;int&gt;.Contains(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="handleId">句柄编号。</param>
    public static bool IsValidHandle(int handleId)
    {
        return handleId > 0 && validHandles.Contains(handleId);
    }

    /// <summary>中文含义：是否音频已暂停。音频是否处于暂停状态（非界面音源已被暂停，新非界面播放请求被抑制）</summary>
    public bool IsAudioPaused => isAudioPaused;

    /// <summary>
    /// 注册外部直接 AudioSource（如电锯备用音源），暂停/恢复时一并处理。
    /// </summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.EnsureChainsawLoopSource()、ChainsawManAnimationBridge.EnsureChainsawStateSource()；下游（含内部回调体） List&lt;AudioSource&gt;.Add(AudioSource)、List&lt;AudioSource&gt;.Contains(AudioSource)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源音源。</param>
    public void RegisterExternalSource(AudioSource source)
    {
        if (source != null && !registeredExternalSources.Contains(source))
        {
            registeredExternalSources.Add(source);
        }
    }

    /// <summary>
    /// 注销外部 AudioSource。
    /// </summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.OnDisable()；下游（含内部回调体） AudioSource.UnPause()、List&lt;AudioSource&gt;.Remove(AudioSource)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="source">来源音源。</param>
    public void UnregisterExternalSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        registeredExternalSources.Remove(source);
        if (pausedExternalSources.Remove(source))
        {
            source.UnPause();
        }
    }

    /// <summary>
    /// 分配新句柄
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)；下游（含内部回调体） HashSet&lt;int&gt;.Add(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private int AllocateHandle()
    {
        // 编号。
int id = nextHandleId++;
        validHandles.Add(id);
        return id;
    }

    /// <summary>
    /// 使句柄失效
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)、AudioManager.ReleaseAfterHandle(int, float, AudioSourcePool)、AudioManager.Stop(AudioPlayHandle)、AudioManager.StopAllInPool(AudioSourcePool, AudioEventData)；下游（含内部回调体） HashSet&lt;int&gt;.Remove(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="id">编号。</param>
    private void InvalidateHandle(int id)
    {
        validHandles.Remove(id);
    }

#endregion
#region ===== IAudioVolumeService 实现 =====
    /// <summary>中文含义：主控音量。当前总音量</summary>
    public float MasterVolume => GetCategoryVolume(AudioCategory.Master);

    private static readonly string[] categoryVolumeKeys =
    {
        "MasterVolume", "SFX_Volume", "BGM_Volume", "AmbientVolume",
        "WeaponVolume", "PlayerVolume", "EnemyVolume", "FootstepVolume", "UIVolume"
    };
    private readonly List<int> activeEventHandles = new();
    private readonly float[] cachedCategoryVolumes = new float[categoryVolumeKeys.Length];

    /// <summary>中文含义：主控音量变化。总音量变化事件</summary>
    public event System.Action<float> MasterVolumeChanged;
    /// <summary>中文含义：类别音量变化。分类音量变化事件</summary>
    public event System.Action<AudioCategory, float> CategoryVolumeChanged;
    /// <summary>
    /// 设置总音量（0~1），立即影响所有音频
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.LoadAndApplyVolumes()、AudioManager.SetCategoryVolume(AudioCategory, float)；下游（含内部回调体） Action&lt;float&gt;.Invoke(float)、AudioManager.GetCategoryVolume(AudioCategory)、AudioManager.WarnMissingMixerParameter(string, bool)、AudioMixer.SetFloat(string, float)、HashSet&lt;string&gt;.Add(string)、Mathf.Clamp01(float)、Mathf.Log10(float) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="value">值。</param>
    public void SetMasterVolume(float value)
    {
        value = Mathf.Clamp01(value);
        cachedCategoryVolumes[(int)AudioCategory.Master] = value;
        PlayerPrefs.SetFloat(categoryVolumeKeys[(int)AudioCategory.Master], value);
        // 是否混音器已应用。
bool mixerApplied = false;
        if (settings != null && settings.masterMixer != null)
        {
            // 有混音器：设置 Master 分组参数
            // 数据表。
float db;
            if (value <= 0.0001f)
            {
                db = -80f;
            }
            else
            {
                db = Mathf.Log10(value) * 20f;
            }

            // 参数。
string param;
            if (string.IsNullOrEmpty(settings.masterVolumeParam))
            {
                param = "MasterVolume";
            }
            else
            {
                param = settings.masterVolumeParam;
            }

            mixerApplied = settings.masterMixer.SetFloat(param, db);
            WarnMissingMixerParameter(param, mixerApplied);
            if (mixerApplied)
            {
                availableMixerParameters.Add(param);
            }
        }

        if (!mixerApplied)
        {
            // 混音器缺少参数时使用 AudioListener.volume 作为可靠回退
            AudioListener.volume = value;
        }
        else
        {
            // 混音器已经承担总音量，避免与 AudioListener 重复衰减
            AudioListener.volume = 1f;
        }

        // 同时更新背景音乐音量（混音器模式下由分类参数控制，回退模式下需要手动应用）
        if (!mixerApplied)
        {
            if (bgmSource != null)
            {
                bgmSource.volume = currentBgmTargetVolume * GetCategoryVolume(AudioCategory.BGM);
            }
        }

        // 判空前缓存的主控音量变化。
global::System.Action<float> receiverValue3 = MasterVolumeChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue3, null))
        {
            receiverValue3.Invoke(value);
        }
    }

    /// <summary>
    /// 保存音量设置，供界面模块通过接口调用。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.OnApplicationPause(bool)、AudioManager.OnDestroy()；下游（含内部回调体） PlayerPrefs.Save()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void SaveVolumeSettings()
    {
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 获取分类音量
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.GetSourceVolume(AudioCategory, float)、AudioManager.PlayBGM(AudioEventData)、AudioManager.RefreshActiveVolumes(AudioCategory)、AudioManager.SetMasterVolume(float)；下游（含内部回调体） PlayerPrefs.GetFloat(string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">类别。</param>
    public float GetCategoryVolume(AudioCategory category)
    {
        int index = (int)category;
        if (index >= 0 && index < cachedCategoryVolumes.Length)
        {
            return cachedCategoryVolumes[index];
        }

        return 1f;
    }

    /// <summary>
    /// 设置分类音量
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.LoadAndApplyVolumes()、AudioManager.SetBGMVolume(float)、AudioManager.SetSFXVolume(float)；下游（含内部回调体） Action&lt;AudioCategory, float&gt;.Invoke(AudioCategory, float)、AudioManager.RefreshActiveVolumes(AudioCategory)、AudioManager.SetMasterVolume(float)、AudioManager.WarnMissingMixerParameter(string, bool)、AudioMixer.SetFloat(string, float)、HashSet&lt;string&gt;.Add(string)、Mathf.Clamp01(float) 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">类别。</param>
    /// <param name="value">值。</param>
    public void SetCategoryVolume(AudioCategory category, float value)
    {
        value = Mathf.Clamp01(value);
        if (category == AudioCategory.Master)
        {
            SetMasterVolume(value);
            // 判空前缓存的类别音量变化。
global::System.Action<global::AudioCategory, float> receiverValue4 = CategoryVolumeChanged;
            if (!global::System.Object.ReferenceEquals(receiverValue4, null))
            {
                receiverValue4.Invoke(category, value);
            }

            return;
        }

        int volumeIndex = (int)category;
        string prefsKey = "SFXVolume";
        if (volumeIndex >= 0 && volumeIndex < cachedCategoryVolumes.Length)
        {
            cachedCategoryVolumes[volumeIndex] = value;
            prefsKey = categoryVolumeKeys[volumeIndex];
        }
        PlayerPrefs.SetFloat(prefsKey, value);
        // 是否混音器已应用。
bool mixerApplied = false;
        if (settings != null && settings.masterMixer != null)
        {
            // 有混音器：设置对应参数
            // 分支选出的参数。
string selectedValue5;
            switch (category)
            {
                case AudioCategory.Master:
                {
                    if (string.IsNullOrEmpty(settings.masterVolumeParam))
                    {
                        selectedValue5 = "MasterVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.masterVolumeParam;
                    }

                    break;
                }

                case AudioCategory.BGM:
                {
                    if (string.IsNullOrEmpty(settings.bgmVolumeParam))
                    {
                        selectedValue5 = "BGMVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.bgmVolumeParam;
                    }

                    break;
                }

                case AudioCategory.SFX:
                {
                    if (string.IsNullOrEmpty(settings.sfxVolumeParam))
                    {
                        selectedValue5 = "SFXVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.sfxVolumeParam;
                    }

                    break;
                }

                case AudioCategory.Ambient:
                {
                    if (string.IsNullOrEmpty(settings.ambientVolumeParam))
                    {
                        selectedValue5 = "AmbientVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.ambientVolumeParam;
                    }

                    break;
                }

                case AudioCategory.Weapon:
                {
                    if (string.IsNullOrEmpty(settings.weaponVolumeParam))
                    {
                        selectedValue5 = "WeaponVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.weaponVolumeParam;
                    }

                    break;
                }

                case AudioCategory.Player:
                {
                    if (string.IsNullOrEmpty(settings.playerVolumeParam))
                    {
                        selectedValue5 = "PlayerVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.playerVolumeParam;
                    }

                    break;
                }

                case AudioCategory.Enemy:
                {
                    if (string.IsNullOrEmpty(settings.enemyVolumeParam))
                    {
                        selectedValue5 = "EnemyVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.enemyVolumeParam;
                    }

                    break;
                }

                case AudioCategory.Footstep:
                {
                    if (string.IsNullOrEmpty(settings.footstepVolumeParam))
                    {
                        selectedValue5 = "FootstepVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.footstepVolumeParam;
                    }

                    break;
                }

                case AudioCategory.UI:
                {
                    if (string.IsNullOrEmpty(settings.uiVolumeParam))
                    {
                        selectedValue5 = "UIVolume";
                    }
                    else
                    {
                        selectedValue5 = settings.uiVolumeParam;
                    }

                    break;
                }

                default:
                {
                    selectedValue5 = "SFXVolume";
                    break;
                }
            }

            // 有混音器：设置对应参数
            // 参数。
string param = selectedValue5;
            // 数据表。
float db;
            if (value <= 0.0001f)
            {
                db = -80f;
            }
            else
            {
                db = Mathf.Log10(value) * 20f;
            }

            mixerApplied = settings.masterMixer.SetFloat(param, db);
            WarnMissingMixerParameter(param, mixerApplied);
            if (mixerApplied)
            {
                availableMixerParameters.Add(param);
            }
        }

        if (!mixerApplied)
        {
            // 无混音器或参数缺失时应用可见的回退音量
            if (category == AudioCategory.BGM && bgmSource != null)
            {
                bgmSource.volume = currentBgmTargetVolume * value;
            }
        }

        // 判空前缓存的类别音量变化。
global::System.Action<global::AudioCategory, float> receiverValue16 = CategoryVolumeChanged;
        if (!global::System.Object.ReferenceEquals(receiverValue16, null))
        {
            receiverValue16.Invoke(category, value);
        }

        RefreshActiveVolumes(category);
    }

#endregion
#region ===== 兼容字段（旧接口） =====
    /// <summary>全局总音量（旧接口，实际由混音器控制）。</summary>
    [Header("兼容设置（旧接口保留）")]
    [Range(0f, 1f)]
    [Tooltip("全局总音量（旧接口，实际由混音器控制）")]
    public float masterVolume = 1f;
    /// <summary>背景音乐音量（旧接口，实际由混音器控制）。</summary>
    [Range(0f, 1f)]
    [Tooltip("背景音乐音量（旧接口，实际由混音器控制）")]
    public float bgmVolume = 1f;
    /// <summary>音效音量（旧接口，实际由混音器控制）。</summary>
    [Range(0f, 1f)]
    [Tooltip("音效音量（旧接口，实际由混音器控制）")]
    public float sfxVolume = 1f;
#endregion
#region ===== 初始化 =====
    /// <summary>实现基类的 Awake 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioLibrary.Initialize()、AudioLibrary.ResetRuntimeState()、AudioManager.InitializeMixerGroups()、AudioManager.LoadAndApplyVolumes()、AudioManager.PreloadWeaponAudio()、AudioSourcePool.AudioSourcePool(Transform, int, int, Action&lt;int&gt;)、Component.GetComponent&lt;AudioSource&gt;() 等共 11 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void Awake()
    {
        base.Awake();
        // 场景切换时重复实例由基类销毁，不再创建音源或注册回调。
        if (Instance != this)
        {
            return;
        }

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        // 音效2距离来源。
var sfx2DSource = GetComponent<AudioSource>();
        if (sfx2DSource == null)
        {
            sfx2DSource = gameObject.AddComponent<AudioSource>();
        }

        sfx2DSource.loop = false;
        sfx2DSource.playOnAwake = false;
        poolRoot = new GameObject("AudioPool_Root").transform;
        poolRoot.SetParent(transform, false);
        poolRoot.localPosition = Vector3.zero;
        // 初始尺寸。
int initialSize;
        if (settings != null)
        {
            initialSize = settings.poolInitialSize;
        }
        else
        {
            initialSize = 16;
        }

        // 最大尺寸。
int maxSize;
        if (settings != null)
        {
            maxSize = settings.poolMaxSize;
        }
        else
        {
            maxSize = 64;
        }

        pool3D = new AudioSourcePool(poolRoot, initialSize, maxSize, InvalidateHandle);
        pool2D = new AudioSourcePool(poolRoot, initialSize / 2, maxSize / 2, InvalidateHandle);
        InitializeMixerGroups();
        if (library != null)
        {
            library.Initialize();
            library.ResetRuntimeState();
            PreloadWeaponAudio();
        }

        LoadAndApplyVolumes();
        mainAudioScene = SceneManager.GetActiveScene();
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    /// <summary>Unity 首次启用后的启动回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.PlayBGM(AudioEventData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Start()
    {
        if (settings != null && settings.autoPlayBGM && settings.bgmEvent != null)
        {
            PlayBGM(settings.bgmEvent);
        }
    }

    /// <summary>实现基类的 OnDestroy 重写入口；框架调用后按本方法分支执行，并通过下游成员完成协作。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.SaveVolumeSettings()、AudioSourcePool.Dispose()、HashSet&lt;int&gt;.Clear()、object.ReferenceEquals(object, object)、Singleton&lt;AudioManager&gt;.OnDestroy()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected override void OnDestroy()
    {
        SaveVolumeSettings();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        // 判空前缓存的对象池3距离。
global::AudioSourcePool receiverValue19 = pool3D;
        if (!global::System.Object.ReferenceEquals(receiverValue19, null))
        {
            receiverValue19.Dispose();
        }

        // 判空前缓存的对象池2距离。
global::AudioSourcePool receiverValue20 = pool2D;
        if (!global::System.Object.ReferenceEquals(receiverValue20, null))
        {
            receiverValue20.Dispose();
        }

        if (HasInstance && Instance == this)
        {
            validHandles.Clear();
        }

        base.OnDestroy();
    }

    /// <summary>OnApplicationPause 的业务处理入口；本方法直接依赖 AudioManager.SaveVolumeSettings()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.SaveVolumeSettings()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pause">是否暂停。</param>
    void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            SaveVolumeSettings();
        }
    }

    /// <summary>Unity 每帧更新回调；状态处理与资源交接按函数内顺序执行，下游协作见调用链。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioSourcePool.RecycleFinished()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    void Update()
    {
        // 判空前缓存的对象池3距离。
global::AudioSourcePool receiverValue21 = pool3D;
        if (!global::System.Object.ReferenceEquals(receiverValue21, null))
        {
            receiverValue21.RecycleFinished();
        }

        // 判空前缓存的对象池2距离。
global::AudioSourcePool receiverValue22 = pool2D;
        if (!global::System.Object.ReferenceEquals(receiverValue22, null))
        {
            receiverValue22.RecycleFinished();
        }
    }

#endregion
#region ===== 混音器初始化 =====
    /// <summary>InitializeMixerGroups 的业务处理入口；本方法直接依赖 AudioMixer.FindMatchingGroups(string)、Dictionary&lt;string, AudioMixerGroup&gt;.ContainsKey(string)、Dictionary&lt;string, AudioMixerGroup&gt;.Dictionary()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.Awake()；下游（含内部回调体） AudioMixer.FindMatchingGroups(string)、Dictionary&lt;string, AudioMixerGroup&gt;.ContainsKey(string)、Dictionary&lt;string, AudioMixerGroup&gt;.Dictionary()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void InitializeMixerGroups()
    {
        groupLookup = new Dictionary<string, AudioMixerGroup>();
        if (settings == null || settings.masterMixer == null)
        {
            return;
        }

        // 分组集合。
AudioMixerGroup[] groups = settings.masterMixer.FindMatchingGroups(string.Empty);
        // 当前遍历索引。
for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && !groupLookup.ContainsKey(groups[i].name))
            {
                groupLookup[groups[i].name] = groups[i];
            }
        }
    }

    /// <summary>
    /// 一次性预热武器分类音效片段：在事件库初始化后遍历所有 Weapon 分类事件，
    /// 对尚未加载的 AudioClip 调用 LoadAudioData，避免开枪瞬间才异步加载导致听觉延迟。
    /// 仅在 Awake（启动/场景准备阶段）调用，禁止在每次开枪时调用。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Awake()；下游（含内部回调体） AudioClip.LoadAudioData()、Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void PreloadWeaponAudio()
    {
        if (library == null || library.entries == null)
        {
            return;
        }

        // 已预加载。
int preloaded = 0;
        // 当前遍历索引。
for (int i = 0; i < library.entries.Count; i++)
        {
            // 当前入口。
var entry = library.entries[i];
            if (entry == null || entry.eventData == null)
            {
                continue;
            }

            if (entry.eventData.category != AudioCategory.Weapon)
            {
                continue;
            }

            // 动画片段集合。
var clips = entry.eventData.clips;
            if (clips == null)
            {
                continue;
            }

            // 内层遍历索引。
for (int j = 0; j < clips.Length; j++)
            {
                if (clips[j] == null || !clips[j].enabled || clips[j].clip == null)
                {
                    continue;
                }

                // 音频片段。
var clip = clips[j].clip;
                if (clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                    preloaded++;
                }
            }
        }

        if (preloaded > 0)
        {
            Debug.Log($"[音频] 武器音效预热完成，已发起 {preloaded} 个片段的加载请求。");
        }
    }

    /// <summary>GetMixerGroup 的业务处理入口；本方法直接依赖 AudioEventData.GetMixerGroupPath()、AudioManager.GetMixerGroupByPath(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)、AudioManager.PlayBGM(AudioEventData)；下游（含内部回调体） AudioEventData.GetMixerGroupPath()、AudioManager.GetMixerGroupByPath(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    private AudioMixerGroup GetMixerGroup(AudioEventData eventData)
    {
        if (eventData == null)
        {
            return null;
        }

        return GetMixerGroupByPath(eventData.GetMixerGroupPath());
    }

    /// <summary>GetMixerGroupByPath 的业务处理入口；本方法直接依赖 AudioMixer.FindMatchingGroups(string)、Dictionary&lt;string, AudioMixerGroup&gt;.TryGetValue(string, out AudioMixerGroup)、string.IsNullOrEmpty(string)、string.LastIndexOf(char)、string.Substring(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.GetMixerGroup(AudioEventData)；下游（含内部回调体） AudioMixer.FindMatchingGroups(string)、Dictionary&lt;string, AudioMixerGroup&gt;.TryGetValue(string, out AudioMixerGroup)、string.IsNullOrEmpty(string)、string.LastIndexOf(char)、string.Substring(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    private AudioMixerGroup GetMixerGroupByPath(string path)
    {
        if (string.IsNullOrEmpty(path) || groupLookup == null)
        {
            return null;
        }

        if (settings != null && settings.masterMixer != null)
        {
            // 匹配到的。
AudioMixerGroup[] matched = settings.masterMixer.FindMatchingGroups(path);
            if (matched != null && matched.Length > 0)
            {
                return matched[0];
            }
        }

        // 分组名称。
string groupName = path;
        // 上一次斩击。
int lastSlash = path.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < path.Length - 1)
        {
            groupName = path.Substring(lastSlash + 1);
        }

        if (groupLookup.TryGetValue(groupName, out AudioMixerGroup /* 中文含义：分组。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ group))
        {
            return group;
        }

        return null;
    }

#endregion
#region ===== 音量管理 =====
    /// <summary>LoadAndApplyVolumes 的业务处理入口；本方法直接依赖 AudioManager.SetCategoryVolume(AudioCategory, float)、AudioManager.SetMasterVolume(float)、PlayerPrefs.GetFloat(string, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.Awake()；下游（含内部回调体） AudioManager.SetCategoryVolume(AudioCategory, float)、AudioManager.SetMasterVolume(float)、PlayerPrefs.GetFloat(string, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void LoadAndApplyVolumes()
    {
        // 统一从 PlayerPrefs 读取
        for (int index = 0; index < categoryVolumeKeys.Length; index++)
        {
            float defaultVolume = 1f;
            if (index == (int)AudioCategory.BGM)
            {
                defaultVolume = 0.8f;
            }
            cachedCategoryVolumes[index] = PlayerPrefs.GetFloat(categoryVolumeKeys[index], defaultVolume);
        }

        float master = GetCategoryVolume(AudioCategory.Master);
        float bgm = GetCategoryVolume(AudioCategory.BGM);
        float sfx = GetCategoryVolume(AudioCategory.SFX);
        masterVolume = master;
        bgmVolume = bgm;
        sfxVolume = sfx;
        if (settings != null && settings.masterMixer != null)
        {
            SetMasterVolume(master);
            for (int index = 1; index < cachedCategoryVolumes.Length; index++)
            {
                SetCategoryVolume((AudioCategory)index, cachedCategoryVolumes[index]);
            }
        }
        else
        {
            // 无混音器回退
            AudioListener.volume = master;
            if (bgmSource != null)
            {
                bgmSource.volume = bgm;
            }
        }
    }

    /// <summary>GetVolumeParameter 的业务处理入口；本方法直接依赖 string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.IsMixerParameterAvailable(AudioCategory)；下游（含内部回调体） string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">类别。</param>
    private string GetVolumeParameter(AudioCategory category)
    {
        if (settings == null)
        {
            return string.Empty;
        }

        // 分支选出的文字“MasterVolume”。
string selectedValue23;
        switch (category)
        {
            case AudioCategory.Master:
            {
                if (string.IsNullOrEmpty(settings.masterVolumeParam))
                {
                    selectedValue23 = "MasterVolume";
                }
                else
                {
                    selectedValue23 = settings.masterVolumeParam;
                }

                break;
            }

            case AudioCategory.BGM:
            {
                if (string.IsNullOrEmpty(settings.bgmVolumeParam))
                {
                    selectedValue23 = "BGMVolume";
                }
                else
                {
                    selectedValue23 = settings.bgmVolumeParam;
                }

                break;
            }

            case AudioCategory.SFX:
            {
                if (string.IsNullOrEmpty(settings.sfxVolumeParam))
                {
                    selectedValue23 = "SFXVolume";
                }
                else
                {
                    selectedValue23 = settings.sfxVolumeParam;
                }

                break;
            }

            case AudioCategory.Ambient:
            {
                if (string.IsNullOrEmpty(settings.ambientVolumeParam))
                {
                    selectedValue23 = "AmbientVolume";
                }
                else
                {
                    selectedValue23 = settings.ambientVolumeParam;
                }

                break;
            }

            case AudioCategory.Weapon:
            {
                if (string.IsNullOrEmpty(settings.weaponVolumeParam))
                {
                    selectedValue23 = "WeaponVolume";
                }
                else
                {
                    selectedValue23 = settings.weaponVolumeParam;
                }

                break;
            }

            case AudioCategory.Player:
            {
                if (string.IsNullOrEmpty(settings.playerVolumeParam))
                {
                    selectedValue23 = "PlayerVolume";
                }
                else
                {
                    selectedValue23 = settings.playerVolumeParam;
                }

                break;
            }

            case AudioCategory.Enemy:
            {
                if (string.IsNullOrEmpty(settings.enemyVolumeParam))
                {
                    selectedValue23 = "EnemyVolume";
                }
                else
                {
                    selectedValue23 = settings.enemyVolumeParam;
                }

                break;
            }

            case AudioCategory.Footstep:
            {
                if (string.IsNullOrEmpty(settings.footstepVolumeParam))
                {
                    selectedValue23 = "FootstepVolume";
                }
                else
                {
                    selectedValue23 = settings.footstepVolumeParam;
                }

                break;
            }

            case AudioCategory.UI:
            {
                if (string.IsNullOrEmpty(settings.uiVolumeParam))
                {
                    selectedValue23 = "UIVolume";
                }
                else
                {
                    selectedValue23 = settings.uiVolumeParam;
                }

                break;
            }

            default:
            {
                selectedValue23 = "SFXVolume";
                break;
            }
        }

        return selectedValue23;
    }

    /// <summary>IsMixerParameterAvailable 的业务处理入口；本方法直接依赖 AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.WarnMissingMixerParameter(string, bool)、AudioMixer.GetFloat(string, out float)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.Contains(string)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.GetSourceVolume(AudioCategory, float)、AudioManager.PlayBGM(AudioEventData)、AudioManager.RefreshActiveVolumes(AudioCategory)；下游（含内部回调体） AudioManager.GetVolumeParameter(AudioCategory)、AudioManager.WarnMissingMixerParameter(string, bool)、AudioMixer.GetFloat(string, out float)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.Contains(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">类别。</param>
    private bool IsMixerParameterAvailable(AudioCategory category)
    {
        if (settings == null || settings.masterMixer == null)
        {
            return false;
        }

        // 参数。
string parameter = GetVolumeParameter(category);
        if (string.IsNullOrEmpty(parameter))
        {
            return false;
        }

        if (availableMixerParameters.Contains(parameter))
        {
            return true;
        }

        // 已忽略。
float ignored;
        // 是否可用。
bool available = settings.masterMixer.GetFloat(parameter, out ignored);
        WarnMissingMixerParameter(parameter, available);
        if (available)
        {
            availableMixerParameters.Add(parameter);
        }

        return available;
    }

    /// <summary>GetSourceVolume 的业务处理入口；本方法直接依赖 AudioManager.GetCategoryVolume(AudioCategory)、AudioManager.IsMixerParameterAvailable(AudioCategory)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)；下游（含内部回调体） AudioManager.GetCategoryVolume(AudioCategory)、AudioManager.IsMixerParameterAvailable(AudioCategory)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">类别。</param>
    /// <param name="eventVolume">事件音量。</param>
    private float GetSourceVolume(AudioCategory category, float eventVolume)
    {
        if (settings == null || settings.masterMixer == null || !IsMixerParameterAvailable(category))
        {
            return eventVolume * GetCategoryVolume(category);
        }

        return eventVolume;
    }

    /// <summary>RefreshActiveVolumes 的业务处理入口；本方法直接依赖 AudioManager.GetCategoryVolume(AudioCategory)、AudioManager.IsMixerParameterAvailable(AudioCategory)、AudioSourcePool.ForEachActive(Action&lt;AudioSourcePool.PooledSource&gt;)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.SetCategoryVolume(AudioCategory, float)；下游（含内部回调体） AudioManager.GetCategoryVolume(AudioCategory)、AudioManager.IsMixerParameterAvailable(AudioCategory)、AudioSourcePool.ForEachActive(Action&lt;AudioSourcePool.PooledSource&gt;)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="category">类别。</param>
    private void RefreshActiveVolumes(AudioCategory category)
    {
        if (settings != null && settings.masterMixer != null && IsMixerParameterAvailable(category))
        {
            return;
        }

        // 刷新。
System.Action<AudioSourcePool.PooledSource> refresh = /* 中文含义：池化的。 */ pooled =>
        {
            if (pooled != null && pooled.category == category && pooled.source != null)
            {
                pooled.source.volume = pooled.eventVolume * GetCategoryVolume(category);
            }
        };
        // 判空前缓存的对象池2距离。
global::AudioSourcePool receiverValue33 = pool2D;
        if (!global::System.Object.ReferenceEquals(receiverValue33, null))
        {
            receiverValue33.ForEachActive(refresh);
        }

        // 判空前缓存的对象池3距离。
global::AudioSourcePool receiverValue34 = pool3D;
        if (!global::System.Object.ReferenceEquals(receiverValue34, null))
        {
            receiverValue34.ForEachActive(refresh);
        }
    }

    // 旧接口兼容
    /// <summary>SetBGMVolume 的业务处理入口；本方法直接依赖 AudioManager.SetCategoryVolume(AudioCategory, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.SetCategoryVolume(AudioCategory, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="volume">音量。</param>
    public void SetBGMVolume(float volume)
    {
        SetCategoryVolume(AudioCategory.BGM, volume);
    }

    /// <summary>SetSFXVolume 的业务处理入口；本方法直接依赖 AudioManager.SetCategoryVolume(AudioCategory, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.SetCategoryVolume(AudioCategory, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="volume">音量。</param>
    public void SetSFXVolume(float volume)
    {
        SetCategoryVolume(AudioCategory.SFX, volume);
    }

#endregion
#region ===== 新接口：通过事件数据播放 =====
    /// <summary>
    /// 播放二维音效（通过事件数据，使用对象池保证并发独立）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(string)；下游（含内部回调体） AudioEventData.CanPlay(AudioEventData.VoicePriority)、AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventData.OnPlayStarted()、AudioEventData.SelectClip()、AudioManager.AllocateHandle()、AudioManager.GetMixerGroup(AudioEventData) 等共 18 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    public AudioPlayHandle Play(AudioEventData eventData)
    {
        if (eventData == null)
        {
            return default;
        }

        if (isAudioPaused && eventData.category != AudioCategory.UI)
        {
            return default;
        }

        if (!eventData.CanPlay(eventData.voicePriority))
        {
            return default;
        }

        // 音频片段。
AudioClip clip = eventData.SelectClip();
        if (clip == null)
        {
            WarnMissing(eventData.name, eventData);
            return default;
        }

        // 句柄编号。
int handleId = AllocateHandle();
        // 事件音量。
float eventVolume = eventData.GetVolume();
        // 音高。
float pitch = eventData.GetPitch();
        // 分组。
AudioMixerGroup group = GetMixerGroup(eventData);
        // 粒子系统。
var ps = pool2D.Acquire(Vector3.zero, eventData, handleId);
        if (ps == null)
        {
            InvalidateHandle(handleId);
            return default;
        }

        ps.source.transform.SetParent(poolRoot, false);
        ps.source.spatialBlend = 0f;
        ps.source.outputAudioMixerGroup = group;
        ps.source.clip = clip;
        ps.eventVolume = eventVolume;
        ps.category = eventData.category;
        ps.source.volume = GetSourceVolume(eventData.category, eventVolume);
        ps.source.pitch = pitch;
        ps.source.priority = eventData.priority;
        ps.source.loop = eventData.loop;
        ps.source.Play();
        eventData.OnPlayStarted();
        if (!eventData.loop)
        {
            // 持续时间。
float duration = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            StartCoroutine(ReleaseAfterHandle(handleId, duration, pool2D, eventData.category == AudioCategory.UI));
        }

        return new AudioPlayHandle(handleId, ps.source, eventData);
    }

    /// <summary>
    /// 播放三维音效（通过事件数据，在世界空间定位）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play3D(string, Vector3)、ChainsawManAnimationBridge.PlayOneShot(AudioEventData, string)、ChainsawManAnimationBridge.PlayTemporaryChainsawAudio(AudioEventData, string)、ChainsawManAnimationBridge.StartIdleLoop(bool)、DoorController.PlayOpenAudio() 等共 13 处；下游（含内部回调体） AudioManager.Play3D(AudioEventData, Vector3, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    /// <param name="position">位置。</param>
    public AudioPlayHandle Play3D(AudioEventData eventData, Vector3 position)
    {
        if (eventData == null)
        {
            return default;
        }

        return Play3D(eventData, position, 1f);
    }

    /// <summary>
    /// 播放三维音效并附加调用方音量倍率。
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play3D(AudioEventData, Vector3)、FootstepController.PlayFootstep()；下游（含内部回调体） AudioEventData.CanPlay(AudioEventData.VoicePriority)、AudioEventData.GetPitch()、AudioEventData.GetVolume()、AudioEventData.OnPlayStarted()、AudioEventData.SelectClip()、AudioManager.AllocateHandle()、AudioManager.GetMixerGroup(AudioEventData) 等共 17 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    /// <param name="position">位置。</param>
    /// <param name="volumeMultiplier">音量倍率。</param>
    public AudioPlayHandle Play3D(AudioEventData eventData, Vector3 position, float volumeMultiplier)
    {
        if (eventData == null || pool3D == null)
        {
            return default;
        }

        if (isAudioPaused && eventData.category != AudioCategory.UI)
        {
            return default;
        }

        if (!eventData.CanPlay(eventData.voicePriority))
        {
            return default;
        }

        // 音频片段。
AudioClip clip = eventData.SelectClip();
        if (clip == null)
        {
            WarnMissing(eventData.name, eventData);
            return default;
        }

        // 句柄编号。
int handleId = AllocateHandle();
        // 事件音量。
float eventVolume = eventData.GetVolume() * Mathf.Max(0f, volumeMultiplier);
        // 音高。
float pitch = eventData.GetPitch();
        // 分组。
AudioMixerGroup group = GetMixerGroup(eventData);
        // 粒子系统。
var ps = pool3D.Acquire(position, eventData, handleId);
        if (ps == null)
        {
            InvalidateHandle(handleId);
            return default;
        }

        ps.source.outputAudioMixerGroup = group;
        ps.source.clip = clip;
        ps.eventVolume = eventVolume;
        ps.category = eventData.category;
        ps.source.volume = GetSourceVolume(eventData.category, eventVolume);
        ps.source.pitch = pitch;
        ps.source.priority = eventData.priority;
        ps.source.loop = eventData.loop;
        ps.source.spatialBlend = 1f;
        ps.source.minDistance = eventData.minDistance;
        ps.source.maxDistance = eventData.maxDistance;
        ps.source.rolloffMode = eventData.rolloffMode;
        ps.source.dopplerLevel = 0f;
        ps.source.spread = 0f;
        ps.source.Play();
        eventData.OnPlayStarted();
        if (!eventData.loop)
        {
            // 持续时间。
float duration = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            StartCoroutine(ReleaseAfterHandle(handleId, duration, pool3D, eventData.category == AudioCategory.UI));
        }

        return new AudioPlayHandle(handleId, ps.source, eventData);
    }

    /// <summary>
    /// 通过事件名称播放二维音效（从事件库查找）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.PlaySfx(string)；下游（含内部回调体） AudioLibrary.GetEvent(string)、AudioManager.Play(AudioEventData)、AudioManager.WarnMissing(string, AudioEventData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventName">事件名称。</param>
    public AudioPlayHandle Play(string eventName)
    {
        if (library == null)
        {
            WarnMissing(eventName, null);
            return default;
        }

        // 事件配置。
AudioEventData eventData = library.GetEvent(eventName);
        if (eventData == null)
        {
            WarnMissing(eventName, null);
            return default;
        }

        return Play(eventData);
    }

    /// <summary>
    /// 通过事件名称播放三维音效（从事件库查找）
    /// </summary>
    /// <remarks>静态调用链：上游 DoorController.PlayOpenAudio()、LeonCombat.OnMeleeHitWindow(AnimationActionTransaction)、LeonCombat.PlayParryFeedback(VillagerController, Vector3, Vector3, KnifeParryResult)、LeonController.PlayDamageFeedback(Vector3)、LeonPlayerAudio.HandleWeaponSwitched(WeaponType) 等共 7 处；下游（含内部回调体） AudioLibrary.GetEvent(string)、AudioManager.Play3D(AudioEventData, Vector3)、AudioManager.WarnMissing(string, AudioEventData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventName">事件名称。</param>
    /// <param name="position">位置。</param>
    public AudioPlayHandle Play3D(string eventName, Vector3 position)
    {
        if (library == null)
        {
            WarnMissing(eventName, null);
            return default;
        }

        // 事件配置。
AudioEventData eventData = library.GetEvent(eventName);
        if (eventData == null)
        {
            WarnMissing(eventName, null);
            return default;
        }

        return Play3D(eventData, position);
    }

    /// <summary>
    /// 停止一个播放句柄（用于循环音效）
    /// </summary>
    /// <remarks>静态调用链：上游 ChainsawManAnimationBridge.StopIdleLoop(bool, bool)、ChainsawManAnimationBridge.StopTemporaryChainsawAudio(bool)、DoorController.PlayOpenAudio()、EnemyAnimationAdapter.PlayVoice(ZombieVoiceType)、LeonPlayerAudio.StopBreathingLoop() 等共 6 处；下游（含内部回调体） AudioManager.InvalidateHandle(int)、AudioSourcePool.Release(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="handle">句柄。</param>
    public void Stop(AudioPlayHandle handle)
    {
        if (!handle.IsValid)
        {
            return;
        }

        // 先尝试三维池
        if (!pool3D.Release(handle.handleId))
        {
            // 再尝试二维池
            pool2D.Release(handle.handleId);
        }

        InvalidateHandle(handle.handleId);
    }

    /// <summary>
    /// 停止指定事件的所有播放
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.StopAllInPool(AudioSourcePool, AudioEventData)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    public void StopAll(AudioEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        StopAllInPool(pool3D, eventData);
        StopAllInPool(pool2D, eventData);
    }

    /// <summary>StopAllInPool 的业务处理入口；本方法直接依赖 AudioManager.InvalidateHandle(int)、AudioSourcePool.GetActiveHandles(AudioEventData)、AudioSourcePool.Release(int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.StopAll(AudioEventData)；下游（含内部回调体） AudioManager.InvalidateHandle(int)、AudioSourcePool.GetActiveHandles(AudioEventData)、AudioSourcePool.Release(int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="pool">对象池。</param>
    /// <param name="eventData">事件配置。</param>
    private void StopAllInPool(AudioSourcePool pool, AudioEventData eventData)
    {
        if (pool == null)
        {
            return;
        }

        // 句柄集合。
        List<int> handles = activeEventHandles;
        pool.CopyActiveHandles(handles, eventData);
        // h（待停止音频的播放句柄）：每轮从句柄集合取得一个元素，仅代表当前这一项。使用位置：作为 AudioManager.InvalidateHandle(int) 的参数、作为 AudioSourcePool.Release(int) 的参数。
foreach (var h in handles)
        {
            if (pool.Release(h))
            {
                InvalidateHandle(h);
            }
        }
    }

#endregion
#region ===== 背景音乐 =====
    /// <summary>PlayBGM 的业务处理入口；本方法直接依赖 AudioEventData.SelectClip()、AudioManager.FadeBGM(float, float, float, bool)、AudioManager.GetCategoryVolume(AudioCategory)、AudioManager.GetMixerGroup(AudioEventData)、AudioManager.IsMixerParameterAvailable(AudioCategory)、AudioManager.WarnMissing(string, AudioEventData)、AudioSource.Play() 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.Start()；下游（含内部回调体） AudioEventData.SelectClip()、AudioManager.FadeBGM(float, float, float, bool)、AudioManager.GetCategoryVolume(AudioCategory)、AudioManager.GetMixerGroup(AudioEventData)、AudioManager.IsMixerParameterAvailable(AudioCategory)、AudioManager.WarnMissing(string, AudioEventData)、AudioSource.Play() 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventData">事件配置。</param>
    public void PlayBGM(AudioEventData eventData)
    {
        if (eventData == null)
        {
            return;
        }

        if (isAudioPaused)
        {
            return;
        }

        // 音频片段。
AudioClip clip = eventData.SelectClip();
        if (clip == null)
        {
            WarnMissing("BGM", eventData);
            return;
        }

        // 分组。
AudioMixerGroup group = GetMixerGroup(eventData);
        bgmSource.outputAudioMixerGroup = group;
        bool resumeCurrentTrack = currentBgmEvent == eventData && bgmSource.isPlaying;
        if (resumeCurrentTrack && !bgmFadingOut)
        {
            return;
        }

        currentBgmEvent = eventData;
        currentBgmTargetVolume = eventData.baseVolume;
        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
        }
        bgmFadingOut = false;

        // 淡入。
float fadeIn;
        if (settings != null)
        {
            fadeIn = settings.bgmFadeInDuration;
        }
        else
        {
            fadeIn = 1f;
        }

        // 无混音器回退：音量 = 事件音量 * BGM分类音量
        // 目标音量。
float targetVol = eventData.baseVolume;
        if (!IsMixerParameterAvailable(AudioCategory.BGM))
        {
            targetVol *= GetCategoryVolume(AudioCategory.BGM);
        }

        if (!resumeCurrentTrack)
        {
            bgmSource.clip = clip;
            bgmSource.pitch = eventData.basePitch;
            bgmSource.loop = true;
            bgmSource.volume = 0f;
            bgmSource.Play();
        }
        bgmFadeCoroutine = StartCoroutine(FadeBGM(bgmSource.volume, targetVol, fadeIn));
    }

    /// <summary>StopBGM 的业务处理入口；本方法直接依赖 AudioManager.FadeBGM(float, float, float, bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioManager.FadeBGM(float, float, float, bool)、MonoBehaviour.StartCoroutine(IEnumerator)、MonoBehaviour.StopCoroutine(Coroutine)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StopBGM()
    {
        if (!bgmSource.isPlaying || bgmFadingOut)
        {
            return;
        }

        // 淡出。
float fadeOut;
        if (settings != null)
        {
            fadeOut = settings.bgmFadeOutDuration;
        }
        else
        {
            fadeOut = 1f;
        }

        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
        }

        bgmFadingOut = true;
        bgmFadeCoroutine = StartCoroutine(FadeBGM(bgmSource.volume, 0f, fadeOut, true));
    }

    internal void StopBGM(AudioEventData owner)
    {
        if (owner != null && currentBgmEvent == owner)
        {
            StopBGM();
        }
    }

    internal bool TryPlayBGMTail(AudioEventData loop, AudioEventData tail)
    {
        if (loop == null || tail == null || currentBgmEvent != loop || bgmSource == null || !bgmSource.isPlaying)
        {
            return false;
        }

        AudioClip clip = tail.SelectClip();
        if (clip == null)
        {
            WarnMissing("BGM", tail);
            return false;
        }

        StopBGMImmediately(loop);
        currentBgmEvent = tail;
        currentBgmTargetVolume = tail.baseVolume;
        bgmSource.outputAudioMixerGroup = GetMixerGroup(tail);
        bgmSource.volume = GetSourceVolume(AudioCategory.BGM, tail.baseVolume);
        bgmSource.clip = clip;
        bgmSource.pitch = tail.basePitch;
        bgmSource.loop = false;
        bgmSource.Play();
        return true;
    }

    internal bool IsBGMPlaying(AudioEventData owner)
    {
        return owner != null && currentBgmEvent == owner && bgmSource != null && bgmSource.isPlaying;
    }

    internal void StopBGMImmediately(AudioEventData owner)
    {
        if (owner == null || currentBgmEvent != owner)
        {
            return;
        }

        ClearBGM();
    }

    private void ClearBGM()
    {
        if (bgmFadeCoroutine != null)
        {
            StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = null;
        }

        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }

        currentBgmEvent = null;
        currentBgmTargetVolume = 0f;
        bgmFadingOut = false;
        bgmWasPlayingBeforePause = false;
    }

    /// <summary>PauseBGM 的业务处理入口；本方法直接依赖 AudioSource.Pause()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioSource.Pause()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
        {
            bgmSource.Pause();
        }
    }

    /// <summary>ResumeBGM 的业务处理入口；本方法直接依赖 AudioSource.UnPause()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioSource.UnPause()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ResumeBGM()
    {
        if (!bgmSource.isPlaying && bgmSource.clip != null)
        {
            bgmSource.UnPause();
        }
    }

    /// <summary>FadeBGM 的业务处理入口；本方法直接依赖 AudioSource.Stop()、Mathf.Lerp(float, float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.PlayBGM(AudioEventData)、AudioManager.StopBGM()；下游（含内部回调体） AudioSource.Stop()、Mathf.Lerp(float, float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="from">来源。</param>
    /// <param name="to">目标。</param>
    /// <param name="duration">持续时间。</param>
    /// <param name="stopAfter">是否停止之后。</param>
    private IEnumerator FadeBGM(float from, float to, float duration, bool stopAfter = false)
    {
        // 已用。
float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        bgmSource.volume = to;
        if (stopAfter)
        {
            bgmSource.Stop();
            currentBgmEvent = null;
            bgmFadingOut = false;
        }

        bgmFadeCoroutine = null;
    }

#endregion
#region ===== 暂停快照 =====
    /// <summary>
    /// 暂停所有非界面音频源（BGM、2D/3D 池中活跃音源、已注册外部音源）。
    /// 暂停期间新非界面播放请求被抑制；不修改 PlayerPrefs 音量设置。
    /// </summary>
    /// <remarks>静态调用链：上游 GameManager.ApplyPermission(GameplayPermissionSnapshot)；下游（含内部回调体） AudioSource.Pause()、AudioSourcePool.PauseActive(Func&lt;AudioCategory, bool&gt;)、List&lt;AudioSource&gt;.Add(AudioSource)、List&lt;AudioSource&gt;.RemoveAt(int)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TransitionToPaused()
    {
        if (isAudioPaused)
        {
            return;
        }

        isAudioPaused = true;
        // 是否应当暂停。
System.Func<AudioCategory, bool> shouldPause = /* 中文含义：类别。 */ category => category != AudioCategory.UI;
        // 判空前缓存的对象池2距离。
global::AudioSourcePool receiverValue37 = pool2D;
        if (!global::System.Object.ReferenceEquals(receiverValue37, null))
        {
            receiverValue37.PauseActive(shouldPause);
        }

        // 判空前缓存的对象池3距离。
global::AudioSourcePool receiverValue38 = pool3D;
        if (!global::System.Object.ReferenceEquals(receiverValue38, null))
        {
            receiverValue38.PauseActive(shouldPause);
        }

        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmWasPlayingBeforePause = true;
            bgmSource.Pause();
        }

        // 当前遍历索引。
for (int i = registeredExternalSources.Count - 1; i >= 0; i--)
        {
            // 当前音源。
AudioSource src = registeredExternalSources[i];
            if (src == null)
            {
                registeredExternalSources.RemoveAt(i);
                continue;
            }

            if (src.isPlaying)
            {
                src.Pause();
                pausedExternalSources.Add(src);
            }
        }
    }

    /// <summary>
    /// 恢复所有被 TransitionToPaused 暂停的音频源，从原播放进度继续。
    /// </summary>
    /// <remarks>静态调用链：上游 GameManager.ApplyPermission(GameplayPermissionSnapshot)；下游（含内部回调体） AudioSource.UnPause()、AudioSourcePool.ResumePaused()、List&lt;AudioSource&gt;.Clear()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void TransitionToNormal()
    {
        if (!isAudioPaused)
        {
            return;
        }

        isAudioPaused = false;
        // 判空前缓存的对象池2距离。
global::AudioSourcePool receiverValue39 = pool2D;
        if (!global::System.Object.ReferenceEquals(receiverValue39, null))
        {
            receiverValue39.ResumePaused();
        }

        // 判空前缓存的对象池3距离。
global::AudioSourcePool receiverValue40 = pool3D;
        if (!global::System.Object.ReferenceEquals(receiverValue40, null))
        {
            receiverValue40.ResumePaused();
        }

        if (bgmWasPlayingBeforePause && bgmSource != null && bgmSource.clip != null)
        {
            bgmSource.UnPause();
        }

        bgmWasPlayingBeforePause = false;
        // 当前遍历索引。
for (int i = pausedExternalSources.Count - 1; i >= 0; i--)
        {
            // 当前音源。
AudioSource src = pausedExternalSources[i];
            if (src != null)
            {
                src.UnPause();
            }
        }

        pausedExternalSources.Clear();
    }

#endregion
#region ===== 旧接口兼容 =====
    /// <summary>PlaySfx 的业务处理入口；本方法直接依赖 AudioManager.Play(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 GameOverScreen.PlayConfirmSound()、GameOverScreen.PlayHoverSound()、InventoryManager.PlayUseAudioEffect()、ItemPickup.Pickup()、MissionClearedScreen.CreateOptionButton(string, string, int, float, float) 等共 14 处；下游（含内部回调体） AudioManager.Play(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="name">名称。</param>
    internal static void PlayInterface(string name)
    {
        AudioManager audio = Instance;
        if (audio != null)
        {
            audio.PlaySfx(name);
        }
    }

    internal static IEnumerator PlayInterfaceBeforeTransition(string name)
    {
        AudioManager audio = Instance;
        if (audio == null)
        {
            yield break;
        }

        AudioPlayHandle handle = audio.Play(name);
        if (handle.IsValid && handle.source.clip != null)
        {
            float duration = handle.source.clip.length / Mathf.Max(0.01f, Mathf.Abs(handle.source.pitch));
            yield return new WaitForSecondsRealtime(duration);
        }
    }

    public void PlaySfx(string name)
    {
        Play(name);
    }

    /// <summary>PlaySFX 的业务处理入口；本方法直接依赖 AudioSource.PlayOneShot(AudioClip, float)、GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)、Object.Destroy(Object, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 ZombieAudioConfig.TestClip(AudioClip[], string)；下游（含内部回调体） AudioSource.PlayOneShot(AudioClip, float)、GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)、Object.Destroy(Object, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">音频片段。</param>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (isAudioPaused)
        {
            return;
        }

        // 创建临时事件数据播放（旧接口兼容）
        // 临时游戏对象。
var tempGo = new GameObject("TempAudio");
        // 当前音源。
var src = tempGo.AddComponent<AudioSource>();
        src.spatialBlend = 0f;
        src.PlayOneShot(clip, sfxVolume);
        Destroy(tempGo, clip.length + 0.1f);
    }

    /// <summary>PlaySFX3D 的业务处理入口；本方法直接依赖 AudioSource.PlayOneShot(AudioClip, float)、GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)、Object.Destroy(Object, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 KnifeCombatModule.PlaySound(AudioClip, Vector3)、WeaponBase.FireInternal(Vector3, Vector3)；下游（含内部回调体） AudioSource.PlayOneShot(AudioClip, float)、GameObject.AddComponent&lt;AudioSource&gt;()、GameObject.GameObject(string)、Object.Destroy(Object, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">音频片段。</param>
    /// <param name="position">位置。</param>
    public void PlaySFX3D(AudioClip clip, Vector3 position)
    {
        if (clip == null)
        {
            return;
        }

        if (isAudioPaused)
        {
            return;
        }

        // 临时游戏对象。
var tempGo = new GameObject("TempAudio3D");
        tempGo.transform.position = position;
        // 当前音源。
var src = tempGo.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.minDistance = 1f;
        src.maxDistance = 50f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.dopplerLevel = 0f;
        src.PlayOneShot(clip, sfxVolume);
        Destroy(tempGo, clip.length + 0.1f);
    }

    /// <summary>StopSFX 的业务处理入口；本方法直接依赖 AudioSourcePool.ReleaseAll()、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioSourcePool.ReleaseAll()、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void StopSFX()
    {
        // 停止所有二维非循环音效
        // 判空前缓存的对象池2距离。
global::AudioSourcePool receiverValue41 = // 停止所有二维非循环音效
 pool2D;
        if (!global::System.Object.ReferenceEquals(receiverValue41, null))
        {
            receiverValue41.ReleaseAll();
        }
    }

    /// <summary>PlayBGM 的业务处理入口；本方法直接依赖 AudioSource.Play()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AudioSource.Play()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="clip">音频片段。</param>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (isAudioPaused)
        {
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = clip;
        bgmSource.volume = bgmVolume;
        bgmSource.Play();
    }

#endregion
#region ===== 内部协程 =====
    /// <summary>
    /// 通过句柄回收（旧计时任务不会误回收新播放）
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play3D(AudioEventData, Vector3, float)；下游（含内部回调体） AudioManager.InvalidateHandle(int)、AudioManager.IsValidHandle(int)、AudioSourcePool.Release(int)、WaitForSeconds.WaitForSeconds(float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="handleId">句柄编号。</param>
    /// <param name="delay">延迟。</param>
    /// <param name="pool">对象池。</param>
    private IEnumerator ReleaseAfterHandle(int handleId, float delay, AudioSourcePool pool, bool unscaledTime)
    {
        if (delay > 0f && unscaledTime)
        {
            yield return new WaitForSecondsRealtime(delay);
        }
        else if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        else
        {
            yield return null;
        }

        // 只有句柄仍然有效时才回收
        if (IsValidHandle(handleId))
        {
            pool.Release(handleId);
            InvalidateHandle(handleId);
        }
    }

#endregion
#region ===== 场景切换处理 =====
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            mainAudioScene = scene;
        }
    }

    private void OnSceneUnloaded(Scene scene)
    {
        // 背包、仓库等叠加场景关闭时，本局仍持有音乐和暂停快照。
        if (scene != mainAudioScene)
        {
            return;
        }

        ClearBGM();
        foreach (AudioSource source in registeredExternalSources)
        {
            if (source != null)
            {
                source.Stop();
            }
        }

        isAudioPaused = false;
        pausedExternalSources.Clear();
        registeredExternalSources.Clear();
        // 判空前缓存的对象池3距离。
global::AudioSourcePool receiverValue42 = pool3D;
        if (!global::System.Object.ReferenceEquals(receiverValue42, null))
        {
            receiverValue42.ReleaseAll();
        }

        // 判空前缓存的对象池2距离。
global::AudioSourcePool receiverValue43 = pool2D;
        if (!global::System.Object.ReferenceEquals(receiverValue43, null))
        {
            receiverValue43.ReleaseAll();
        }

        validHandles.Clear();
        if (library != null && library.entries != null)
        {
            // 当前遍历索引。
for (int i = 0; i < library.entries.Count; i++)
            {
                if (library.entries[i] != null && library.entries[i].eventData != null)
                {
                    library.entries[i].eventData.ResetState();
                }
            }
        }

        warnedMissing.Clear();
        warnedMissingMixerParameters.Clear();
        availableMixerParameters.Clear();
    }

#endregion
#region ===== 诊断 =====
    /// <summary>WarnMissing 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.Contains(string)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.Play(AudioEventData)、AudioManager.Play(string)、AudioManager.Play3D(AudioEventData, Vector3, float)、AudioManager.Play3D(string, Vector3)、AudioManager.PlayBGM(AudioEventData)；下游（含内部回调体） Debug.LogWarning(object)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.Contains(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="eventName">事件名称。</param>
    /// <param name="eventData">事件配置。</param>
    private void WarnMissing(string eventName, AudioEventData eventData)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            eventName = "未命名";
        }

        if (warnedMissing.Contains(eventName))
        {
            return;
        }

        warnedMissing.Add(eventName);
        if (eventData != null && eventData.warnIfMissing)
        {
            Debug.LogWarning($"[音频] 事件 '{eventName}' 缺少可播放的音频片段，请检查配置资产。");
        }
        else if (eventData == null)
        {
            Debug.LogWarning($"[音频] 未找到事件 '{eventName}'，请确认事件库已正确配置。");
        }
    }

    /// <summary>WarnMissingMixerParameter 的业务处理入口；本方法直接依赖 Debug.LogWarning(object)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.Contains(string)、string.IsNullOrEmpty(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 AudioManager.IsMixerParameterAvailable(AudioCategory)、AudioManager.SetCategoryVolume(AudioCategory, float)、AudioManager.SetMasterVolume(float)；下游（含内部回调体） Debug.LogWarning(object)、HashSet&lt;string&gt;.Add(string)、HashSet&lt;string&gt;.Contains(string)、string.IsNullOrEmpty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parameter">参数。</param>
    /// <param name="applied">是否已应用。</param>
    private void WarnMissingMixerParameter(string parameter, bool applied)
    {
        if (applied || string.IsNullOrEmpty(parameter) || warnedMissingMixerParameters.Contains(parameter))
        {
            return;
        }

        warnedMissingMixerParameters.Add(parameter);
        Debug.LogWarning($"[音频] 混音器未暴露音量参数“{parameter}”，已使用回退路径。请检查混音器资产。");
    }
#endregion
}

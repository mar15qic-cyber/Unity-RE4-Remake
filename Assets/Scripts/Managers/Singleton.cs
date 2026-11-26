using UnityEngine;

/// <summary>
/// 标记需要场景手动放置的单例，阻止访问实例时自动创建空物体。
/// </summary>
public interface IManualSingleton
{
}

/// <summary>
/// 全局泛型单例基类
/// 特性：场景不销毁、防重复实例、懒加载自动创建
/// 用法：public class MyManager : Singleton&lt;MyManager&gt; { }
/// </summary>
/// <typeparam name = "T">继承此基类的MonoBehaviour类型</typeparam>
public abstract class Singleton</* 中文含义：由调用方指定的通用类型参数。 */ T> : MonoBehaviour where T : MonoBehaviour
{
#region ===== 单例实例 =====
    /// <summary>中文含义：实例。单例访问入口 首次访问时自动在场景中查找或创建新的GameObject挂载此组件</summary>
    public static T Instance
    {
        get
        {
            // 应用退出/场景卸载时不懒创建，避免泄漏残留 GameObject
            if (isQuitting)
            {
                return instance;
            }

            // 如果实例已被销毁（场景切换时可能发生），清空引用
            if (instance == null)
            {
                // 先在场景中查找已有实例
                instance = FindObjectOfType<T>();
                if (instance == null && typeof(IManualSingleton).IsAssignableFrom(typeof(T)))
                {
                    return instance;
                }

                // 场景中不存在则自动创建
                if (instance == null)
                {
                    // 单例对象。
GameObject singletonObject = new GameObject(typeof(T).Name);
                    instance = singletonObject.AddComponent<T>();
                }
            }

            return instance;
        }
    }

    /// <summary>中文含义：是否具有实例。检查单例实例是否已存在且未被销毁 用于避免在OnDestroy中访问已销毁的单例</summary>
    public static bool HasInstance
    {
        get
        {
            return instance != null;
        }
    }

    /// <summary>中文含义：切换场景时是否保留此单例对象。是否跨场景存活（默认 true）。非持久单例 override 为 false。</summary>
    protected virtual bool Persistent => true;

#endregion
#region ===== 内部字段 =====
    /// <summary>中文含义：实例。静态实例引用（私有，通过Instance属性访问）</summary>
    private static T instance;
    /// <summary>中文含义：是否退出中。应用退出标志，防止退出/卸载时懒创建残留 GameObject</summary>
    private static bool isQuitting = false;
#endregion
#region ===== Unity生命周期 =====
    /// <summary>
    /// 初始化：确保单例唯一性，标记场景不销毁
    /// 子类重写时必须调用 base.Awake()
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.Awake()、GameManager.Awake()、GameObjectPoolManager.Awake()、ItemInspectionUI.Awake()、UIManager.Awake()；下游（含内部回调体） Object.Destroy(Object)、Object.DontDestroyOnLoad(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected virtual void Awake()
    {
        // 关闭脚本域重载时，上一轮运行留下的退出标记必须在新实例初始化时清除。
        isQuitting = false;
        if (instance == null)
        {
            instance = this as T;
            if (Persistent)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 销毁时清理静态引用，防止野指针
    /// </summary>
    /// <remarks>静态调用链：上游 AudioManager.OnDestroy()、ChapterOneMissionManager.OnDestroy()、CombatHUD.OnDestroy()、EnemyAIManager.OnDestroy()、GameManager.OnDestroy() 等共 13 处；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>
    /// 应用退出时设置标志，防止 Instance getter 懒创建新实例
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；没有解析到下游方法调用。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    protected virtual void OnApplicationQuit()
    {
        isQuitting = true;
    }
#endregion
}

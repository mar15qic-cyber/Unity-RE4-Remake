using UnityEngine;
using UnityEditor;

/// <summary>
/// Villager 攻击动画事件处理器：给攻击动画剪辑添加 OnAttackHit 动画事件
/// </summary>
public class VillagerAttackEventPostProcessor : AssetPostprocessor
{
    /// <summary>中文含义：攻击动画片段路径。编译期常量，保持既有值与消费者映射。使用位置：作为 VillagerAttackEventPostProcessor.InjectAttackEvent(string) 的参数。</summary>
    private const string AttackClipPath = "Assets/Animations/Enemy/Zombie Attack.fbx";
    /// <summary>OnPostprocessModel 的业务处理入口；本方法直接依赖 string.Contains(string)、VillagerAttackEventPostProcessor.InjectAttackEvent(string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） string.Contains(string)、VillagerAttackEventPostProcessor.InjectAttackEvent(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">中文含义：根游戏对象。</param>
    void OnPostprocessModel(GameObject root)
    {
        if (!assetPath.Contains("Zombie Attack"))
        {
            return;
        }

        InjectAttackEvent(assetPath);
    }

    /// <summary>
    /// 菜单项：手动触发向攻击动画注入 OnAttackHit 事件
    /// </summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） VillagerAttackEventPostProcessor.InjectAttackEvent(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/VillagerInjectAttackEvent")]
    static void MenuItemInject()
    {
        InjectAttackEvent(AttackClipPath);
    }

    /// <summary>
    /// 给攻击动画剪辑添加 OnAttackHit 事件（在动画中段触发）
    /// </summary>
    /// <remarks>静态调用链：上游 VillagerAttackEventPostProcessor.MenuItemInject()、VillagerAttackEventPostProcessor.OnPostprocessModel(GameObject)；下游（含内部回调体） AnimationEvent.AnimationEvent()、AssetImporter.GetAtPath(string)、AssetImporter.SaveAndReimport()、Debug.Log(object)、Debug.LogError(object)、EditorUtility.SetDirty(Object)、List&lt;AnimationEvent&gt;.Add(AnimationEvent) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="fbxPath">中文含义：模型文件路径。使用位置：作为 AssetImporter.GetAtPath(string) 的参数、作为 Debug.LogError(object) 的参数。</param>
    static void InjectAttackEvent(string fbxPath)
    {
        // importer（导入器）：使用位置：作为 EditorUtility.SetDirty(Object) 的参数、VillagerAttackEventPostProcessor.InjectAttackEvent(string)。
var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("[VillagerEventProcessor] 找不到 ModelImporter: " + fbxPath);
            return;
        }

        // clips（动画片段集合）：使用位置：VillagerAttackEventPostProcessor.InjectAttackEvent(string)。
var clips = importer.clipAnimations;
        // modified（是否修改后的）：使用位置：VillagerAttackEventPostProcessor.InjectAttackEvent(string)。
bool modified = false;
        // clip（动画片段）：每轮从动画片段集合取得一个元素，仅代表当前这一项。使用位置：作为 Debug.Log(object) 的参数、作为 List<AnimationEvent>.List(IEnumerable<AnimationEvent>) 的参数、VillagerAttackEventPostProcessor.InjectAttackEvent(string)。
foreach (var clip in clips)
        {
            if (clip.name != "Zombie_Attack")
            {
                continue;
            }

            // 检查是否已有 OnAttackHit 事件
            // hasHit（是否已经命中）：使用位置：VillagerAttackEventPostProcessor.InjectAttackEvent(string)。
bool hasHit = false;
            // evt（当前动画事件）：每轮从动画片段的事件集合取得一个元素，仅代表当前这一项。使用位置：VillagerAttackEventPostProcessor.InjectAttackEvent(string)。
foreach (var evt in clip.events)
            {
                if (evt.functionName == "OnAttackHit")
                {
                    hasHit = true;
                    break;
                }
            }

            if (hasHit)
            {
                continue;
            }

            // hitEvent（命中事件）：使用位置：作为 Debug.Log(object) 的参数、作为 List<AnimationEvent>.Add(AnimationEvent) 的参数、VillagerAttackEventPostProcessor.InjectAttackEvent(string)。
var hitEvent = new AnimationEvent();
            hitEvent.time = (clip.lastFrame - clip.firstFrame) / 30f * 0.5f;
            hitEvent.functionName = "OnAttackHit";
            hitEvent.messageOptions = SendMessageOptions.DontRequireReceiver;
            // eventList（事件列表）：使用位置：VillagerAttackEventPostProcessor.InjectAttackEvent(string)。
var eventList = new System.Collections.Generic.List<AnimationEvent>(clip.events);
            eventList.Add(hitEvent);
            clip.events = eventList.ToArray();
            modified = true;
            Debug.Log("[VillagerEventProcessor] 已为 Zombie_Attack 添加 OnAttackHit 事件 time=" + hitEvent.time + " clipDuration=" + ((clip.lastFrame - clip.firstFrame) / 30f));
        }

        if (modified)
        {
            importer.clipAnimations = clips;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[VillagerEventProcessor] 攻击动画事件已保存并重新导入");
        }
        else
        {
            Debug.Log("[VillagerEventProcessor] 无需修改（事件已存在或剪辑没找到）");
        }
    }
}

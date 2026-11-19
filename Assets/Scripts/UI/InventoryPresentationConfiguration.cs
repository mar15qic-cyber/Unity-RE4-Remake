using System;
using UnityEngine;

/// <summary>公文包三维展示的共享配置资产，由 AttacheCasePresentationController 和 StoredItemPreview 读取；提供模型、逐物品构图与渲染参数，不保存背包实例数量或位置。</summary>
[CreateAssetMenu(fileName = "背包展示配置", menuName = "游戏界面/背包三维展示")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "背包展示配置")]
public class InventoryPresentationConfiguration : ScriptableObject
{
    /// <summary>单个物品标识对应的三维展示配置，Find 返回现有配置引用，展示宿主负责实例化与排布；保留已有序列化和 JSON 映射。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp", "背包展示配置+物品展示")]
    public class ItemPresentation
    {
        /// <summary>中文含义：物品标识。用于匹配物品定义的既有标识，逐项查询按字符串相等判断。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("物品标识")]
        [UnityEngine.InspectorName("物品标识")]
        [Newtonsoft.Json.JsonProperty("物品标识")]
        public string itemIdentifier;
        /// <summary>中文含义：模型。该物品展示使用的模型预制件，实际对象生命周期归展示宿主。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("模型")]
        [UnityEngine.InspectorName("模型")]
        [Newtonsoft.Json.JsonProperty("模型")]
        public GameObject model;
        /// <summary>中文含义：朝向。该模型在公文包展示中的欧拉角朝向配置。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("朝向")]
        [UnityEngine.InspectorName("朝向")]
        [Newtonsoft.Json.JsonProperty("朝向")]
        public Vector3 orientation;
        /// <summary>中文含义：偏移。模型相对自动排布位置的构图偏移。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("偏移")]
        [UnityEngine.InspectorName("偏移")]
        [Newtonsoft.Json.JsonProperty("偏移")]
        public Vector3 offset;
        /// <summary>中文含义：占格比例。模型相对网格占位区域的比例修正，保留资产原数值。</summary>
        [Range(0.4f, 1.2f)]
        [UnityEngine.Serialization.FormerlySerializedAs("占格比例")]
        [UnityEngine.InspectorName("占格比例")]
        [Newtonsoft.Json.JsonProperty("占格比例")]
        public float gridOccupancyRatio = 0.9f;
    }

    /// <summary>中文含义：公文包模型。公文包主体模型预制件，供三维展示宿主建立背景。</summary>
    [Header("三维资源")]
    [UnityEngine.Serialization.FormerlySerializedAs("公文包模型")]
    [UnityEngine.InspectorName("公文包模型")]
    public GameObject attacheCaseModel;
    /// <summary>中文含义：物品。按标识配置的物品展示数组，Find 使用首个非空匹配项。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("物品")]
    [UnityEngine.InspectorName("物品")]
    public ItemPresentation[] item = Array.Empty<ItemPresentation>();
    /// <summary>中文含义：展示层。三维展示对象使用的隔离渲染层，宿主与展示相机共用。</summary>
    [Header("渲染设置")]
    [Range(8, 31)]
    [UnityEngine.Serialization.FormerlySerializedAs("展示层")]
    [UnityEngine.InspectorName("展示层")]
    public int presentationLayer = 30;
    /// <summary>中文含义：纹理宽度。展示渲染纹理目标宽度，实际分配由展示宿主完成。</summary>
    [Range(512, 2048)]
    [UnityEngine.Serialization.FormerlySerializedAs("纹理宽度")]
    [UnityEngine.InspectorName("纹理宽度")]
    public int textureWidth = 1536;
    /// <summary>中文含义：格线材质。公文包格线使用的共享材质引用。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("格线材质")]
    [UnityEngine.InspectorName("格线材质")]
    public Material cellLineMaterial;
    /// <summary>中文含义：覆盖材质。展示模型的可选覆盖材质，是否应用由展示宿主判断。</summary>
    [UnityEngine.Serialization.FormerlySerializedAs("覆盖材质")]
    [UnityEngine.InspectorName("覆盖材质")]
    public Material overrideMaterial;
    /// <summary>展示或预览宿主按物品标识同步查询，按资产数组顺序返回首个匹配配置，未找到返回 null，不创建默认配置。</summary>
    /// <param name="identifier">中文含义：标识符。要匹配的物品定义标识。</param>
    public ItemPresentation Find(string identifier)
    {
        // 当前数组候选配置，与同名数组字段保持既有兼容命名，只有非空且标识一致时返回。
        // 中文含义：item 为“当前物品表现”。每轮从物品取得一个元素，仅代表当前这一项。
foreach (var item in item)
        {
            if (item != null && item.itemIdentifier == identifier)
            {
                return item;
            }
        }

        return null;
    }
}

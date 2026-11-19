using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>导出真实骨骼矩阵、原始蒙皮及材质，供离线网页任意视角检查。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀网页导出")]
public static class LeonKnifeWebExporter
{
    /// <summary>编辑器配置与校验模块 PreviewDataPackage；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀网页导出+数据包")]
    public class PreviewDataPackage
    {
        /// <summary>中文含义：版本。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("版本")]
        [UnityEngine.InspectorName("版本")]
        [Newtonsoft.Json.JsonProperty("版本")]
        public string version;
        /// <summary>中文含义：网格。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("网格")]
        [UnityEngine.InspectorName("网格")]
        [Newtonsoft.Json.JsonProperty("网格")]
        public List<MeshData> mesh = new List<MeshData>();
        /// <summary>中文含义：材质。使用位置：RegisterMaterial(Material)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("材质")]
        [UnityEngine.InspectorName("材质")]
        [Newtonsoft.Json.JsonProperty("材质")]
        public List<MaterialData> material = new List<MaterialData>();
        /// <summary>中文含义：动作。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("动作")]
        [UnityEngine.InspectorName("动作")]
        [Newtonsoft.Json.JsonProperty("动作")]
        public List<AnimationData> action = new List<AnimationData>();
        /// <summary>中文含义：骨数。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("骨数")]
        [UnityEngine.InspectorName("骨数")]
        [Newtonsoft.Json.JsonProperty("骨数")]
        public int boneCount;
    }

    /// <summary>编辑器配置与校验模块 MeshData；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀网页导出+网格数据")]
    public class MeshData
    {
        /// <summary>中文含义：名称。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("名称")]
        [UnityEngine.InspectorName("名称")]
        [Newtonsoft.Json.JsonProperty("名称")]
        public string name;
        /// <summary>中文含义：顶点。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("顶点")]
        [UnityEngine.InspectorName("顶点")]
        [Newtonsoft.Json.JsonProperty("顶点")]
        public string vertex;
        /// <summary>中文含义：普通状态值。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("法线")]
        [UnityEngine.InspectorName("法线")]
        [Newtonsoft.Json.JsonProperty("法线")]
        public string normal;
        /// <summary>中文含义：纹理坐标。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("纹理坐标")]
        [UnityEngine.InspectorName("纹理坐标")]
        [Newtonsoft.Json.JsonProperty("纹理坐标")]
        public string textureCoordinates;
        /// <summary>中文含义：关节。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("关节")]
        [UnityEngine.InspectorName("关节")]
        [Newtonsoft.Json.JsonProperty("关节")]
        public string closeSection;
        /// <summary>中文含义：权重。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("权重")]
        [UnityEngine.InspectorName("权重")]
        [Newtonsoft.Json.JsonProperty("权重")]
        public string weight;
        /// <summary>中文含义：索引。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("索引")]
        [UnityEngine.InspectorName("索引")]
        [Newtonsoft.Json.JsonProperty("索引")]
        public string index;
        /// <summary>中文含义：材质。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("材质")]
        [UnityEngine.InspectorName("材质")]
        [Newtonsoft.Json.JsonProperty("材质")]
        public int material;
        /// <summary>中文含义：是否刀具。使用位置：作为 List&lt;LeonKnifeWebExporter.MeshData&gt;.Add(LeonKnifeWebExporter.MeshData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("刀具")]
        [UnityEngine.InspectorName("刀具")]
        [Newtonsoft.Json.JsonProperty("刀具")]
        public bool knifeVisual;
    }

    /// <summary>编辑器配置与校验模块 MaterialData；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀网页导出+材质数据")]
    public class MaterialData
    {
        /// <summary>中文含义：名称。使用位置：RegisterMaterial(Material)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("名称")]
        [UnityEngine.InspectorName("名称")]
        [Newtonsoft.Json.JsonProperty("名称")]
        public string name;
        /// <summary>中文含义：图像。使用位置：RegisterMaterial(Material)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("图像")]
        [UnityEngine.InspectorName("图像")]
        [Newtonsoft.Json.JsonProperty("图像")]
        public string image;
        /// <summary>中文含义：颜色。使用位置：RegisterMaterial(Material)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("颜色")]
        [UnityEngine.InspectorName("颜色")]
        [Newtonsoft.Json.JsonProperty("颜色")]
        public float[] color;
        /// <summary>中文含义：是否透明。使用位置：RegisterMaterial(Material)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("透明")]
        [UnityEngine.InspectorName("透明")]
        [Newtonsoft.Json.JsonProperty("透明")]
        public bool transparent;
    }

    /// <summary>编辑器配置与校验模块 AnimationData；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀网页导出+动作数据")]
    public class AnimationData
    {
        /// <summary>中文含义：名称。使用位置：作为 List&lt;LeonKnifeWebExporter.AnimationData&gt;.Add(LeonKnifeWebExporter.AnimationData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("名称")]
        [UnityEngine.InspectorName("名称")]
        [Newtonsoft.Json.JsonProperty("名称")]
        public string name;
        /// <summary>中文含义：矩阵。使用位置：作为 List&lt;LeonKnifeWebExporter.AnimationData&gt;.Add(LeonKnifeWebExporter.AnimationData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("矩阵")]
        [UnityEngine.InspectorName("矩阵")]
        [Newtonsoft.Json.JsonProperty("矩阵")]
        public string matrix;
        /// <summary>中文含义：骨线。使用位置：作为 List&lt;LeonKnifeWebExporter.AnimationData&gt;.Add(LeonKnifeWebExporter.AnimationData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("骨线")]
        [UnityEngine.InspectorName("骨线")]
        [Newtonsoft.Json.JsonProperty("骨线")]
        public string boneLines;
        /// <summary>中文含义：移动。使用位置：作为 List&lt;LeonKnifeWebExporter.AnimationData&gt;.Add(LeonKnifeWebExporter.AnimationData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("移动")]
        [UnityEngine.InspectorName("移动")]
        [Newtonsoft.Json.JsonProperty("移动")]
        public int movement;
        /// <summary>中文含义：帧数。使用位置：作为 List&lt;LeonKnifeWebExporter.AnimationData&gt;.Add(LeonKnifeWebExporter.AnimationData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("帧数")]
        [UnityEngine.InspectorName("帧数")]
        [Newtonsoft.Json.JsonProperty("帧数")]
        public int frameCount;
        /// <summary>中文含义：时长。使用位置：作为 List&lt;LeonKnifeWebExporter.AnimationData&gt;.Add(LeonKnifeWebExporter.AnimationData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("时长")]
        [UnityEngine.InspectorName("时长")]
        [Newtonsoft.Json.JsonProperty("时长")]
        public float duration;
        /// <summary>中文含义：隐刀区间。使用位置：作为 List&lt;LeonKnifeWebExporter.AnimationData&gt;.Add(LeonKnifeWebExporter.AnimationData) 的参数。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("隐刀区间")]
        [UnityEngine.InspectorName("隐刀区间")]
        [Newtonsoft.Json.JsonProperty("隐刀区间")]
        public float[] hideKnifeInterval;
    }

    /// <summary>编辑器配置与校验模块 BoneRecord；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "里昂持刀网页导出+骨记录")]
    sealed class BoneRecord
    {
        /// <summary>中文含义：骨骼。使用位置：作为 List&lt;LeonKnifeWebExporter.BoneRecord&gt;.Add(LeonKnifeWebExporter.BoneRecord) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("骨")]
        [UnityEngine.InspectorName("骨")]
        public Transform bone;
        /// <summary>中文含义：绑定。使用位置：作为 List&lt;LeonKnifeWebExporter.BoneRecord&gt;.Add(LeonKnifeWebExporter.BoneRecord) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。</summary>
        [UnityEngine.Serialization.FormerlySerializedAs("绑定")]
        [UnityEngine.InspectorName("绑定")]
        public Matrix4x4 binding;
    }

    /// <summary>中文含义：检查骨骼。只读字段，初始化后不由普通写入口替换。使用位置：作为 Animator.GetBoneTransform(HumanBodyBones) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。</summary>
    static readonly HumanBodyBones[] checkBone =
    {
        HumanBodyBones.Hips,
        HumanBodyBones.Spine,
        HumanBodyBones.Chest,
        HumanBodyBones.Neck,
        HumanBodyBones.Head,
        HumanBodyBones.LeftShoulder,
        HumanBodyBones.LeftUpperArm,
        HumanBodyBones.LeftLowerArm,
        HumanBodyBones.LeftHand,
        HumanBodyBones.RightShoulder,
        HumanBodyBones.RightUpperArm,
        HumanBodyBones.RightLowerArm,
        HumanBodyBones.RightHand,
        HumanBodyBones.LeftUpperLeg,
        HumanBodyBones.LeftLowerLeg,
        HumanBodyBones.LeftFoot,
        HumanBodyBones.RightUpperLeg,
        HumanBodyBones.RightLowerLeg,
        HumanBodyBones.RightFoot
    };
    /// <summary>编辑器菜单调用入口 MenuExport；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、LeonKnifeWebExporter.Export()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/持刀动作/导出网页预览数据")]
    public static void MenuExport()
    {
        Debug.Log(Export());
    }

    /// <summary>编辑器菜单调用入口 OpenWeb；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Application.OpenURL(string)、File.Exists(string)、InvalidOperationException.InvalidOperationException(string)、Path.GetFullPath(string)、Uri.Uri(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("工具/里昂/持刀动作/打开网页预览")]
    public static void OpenWeb()
    {
        // path（路径）：保存 Path.GetFullPath(string) 的返回结果，供当前步骤使用。使用位置：作为 File.Exists(string) 的参数、作为 Uri.Uri(string) 的参数。
string path = Path.GetFullPath("持刀动作审阅/网页预览/里昂持刀动作预览.html");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("请先导出网页数据，再运行审阅目录中的生成网页脚本。");
        }

        Application.OpenURL(new Uri(path).AbsoluteUri);
    }

    /// <summary>Export 的业务处理入口；本方法直接依赖 LeonKnifeWebExporter.ExportTo(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeWebExporter.MenuExport()；下游（含内部回调体） LeonKnifeWebExporter.ExportTo(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string Export()
    {
        return ExportTo("持刀动作审阅/网页预览/当前动作.json", "连续切削与近身弹反候选");
    }

    /// <summary>ExportLegacy 的业务处理入口；本方法直接依赖 LeonKnifeWebExporter.ExportTo(string, string)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） LeonKnifeWebExporter.ExportTo(string, string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public static string ExportLegacy()
    {
        return ExportTo("持刀动作审阅/网页预览/上版动作.json", "上一版（已否决）");
    }

    /// <summary>ExportTo 的业务处理入口；本方法直接依赖 Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;MeshFilter&gt;()、Dictionary&lt;int, int&gt;.Dictionary()、Dictionary&lt;Material, int&gt;.Dictionary()、Dictionary&lt;string, int&gt;.Dictionary()、Directory.CreateDirectory(string)、File.WriteAllText(string, string) 等共 39 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeWebExporter.Export()、LeonKnifeWebExporter.ExportLegacy()；下游（含内部回调体） Animator.GetBoneTransform(HumanBodyBones)、Component.GetComponent&lt;MeshFilter&gt;()、Dictionary&lt;int, int&gt;.Dictionary()、Dictionary&lt;Material, int&gt;.Dictionary()、Dictionary&lt;string, int&gt;.Dictionary()、Directory.CreateDirectory(string)、File.WriteAllText(string, string) 等共 39 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">中文含义：路径。使用位置：作为 File.WriteAllText(string, string) 的参数、作为 Path.GetDirectoryName(string) 的参数、作为 Path.GetFullPath(string) 的参数。</param>
    /// <param name="version">中文含义：版本。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。</param>
    public static string ExportTo(string path, string version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        // package（包）：使用位置：作为 JsonConvert.SerializeObject(object?) 的参数、LeonKnifeWebExporter.ExportTo(string, string)、RegisterMaterial(Material)。
var package = new PreviewDataPackage
        {
            version = version
        };
        // boneTable（骨骼表）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)、RegisterBone(Transform, Matrix4x4)。
var boneTable = new List<BoneRecord>();
        // boneIndex（骨骼索引）：使用位置：RegisterBone(Transform, Matrix4x4)。
var boneIndex = new Dictionary<string, int>();
        // materialTable（材质表）：使用位置：RegisterMaterial(Material)。
var materialTable = new Dictionary<Material, int>();
        /// <summary>RegisterBone 的业务处理入口；本方法直接依赖 Dictionary&lt;string, int&gt;.Add(string, int)、Dictionary&lt;string, int&gt;.TryGetValue(string, out int)、Enumerable.Range(int, int)、float.ToString(string, IFormatProvider)、IEnumerable&lt;int&gt;.Select&lt;int, string&gt;(Func&lt;int, string&gt;)、LeonKnifeWebExporter.BoneRecord.BoneRecord()、List&lt;LeonKnifeWebExporter.BoneRecord&gt;.Add(LeonKnifeWebExporter.BoneRecord) 等共 9 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeWebExporter.ExportTo(string, string)、RotateIndex(int)；下游（含内部回调体） Dictionary&lt;string, int&gt;.Add(string, int)、Dictionary&lt;string, int&gt;.TryGetValue(string, out int)、Enumerable.Range(int, int)、float.ToString(string, IFormatProvider)、IEnumerable&lt;int&gt;.Select&lt;int, string&gt;(Func&lt;int, string&gt;)、LeonKnifeWebExporter.BoneRecord.BoneRecord()、List&lt;LeonKnifeWebExporter.BoneRecord&gt;.Add(LeonKnifeWebExporter.BoneRecord) 等共 9 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="bone">中文含义：骨骼。使用位置：作为 List&lt;LeonKnifeWebExporter.BoneRecord&gt;.Add(LeonKnifeWebExporter.BoneRecord) 的参数、RegisterBone(Transform, Matrix4x4)。</param>
        /// <param name="binding">中文含义：绑定。使用位置：作为 IEnumerable&lt;int&gt;.Select&lt;int, string&gt;(Func&lt;int, string&gt;) 的参数、作为 List&lt;LeonKnifeWebExporter.BoneRecord&gt;.Add(LeonKnifeWebExporter.BoneRecord) 的参数。</param>
        int RegisterBone(Transform bone, Matrix4x4 binding)
        {
            // key（键）：使用位置：作为 Dictionary<string, int>.Add(string, int) 的参数、作为 Dictionary<string, int>.TryGetValue(string, out int) 的参数。
string key = bone.GetInstanceID() + ":" + string.Join(",", Enumerable.Range(0, 16).Select( /* 中文含义：索引。 */i => binding[i].ToString("F5", System.Globalization.CultureInfo.InvariantCulture)));
            if (boneIndex.TryGetValue(key, out int /* 中文含义：索引。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ index))
            {
                return index;
            }

            index = boneTable.Count;
            boneTable.Add(new BoneRecord { bone = bone, binding = binding });
            boneIndex.Add(key, index);
            return index;
        }

        /// <summary>RegisterMaterial 的业务处理入口；本方法直接依赖 Convert.ToBase64String(byte[])、Dictionary&lt;Material, int&gt;.Add(Material, int)、Dictionary&lt;Material, int&gt;.TryGetValue(Material, out int)、Graphics.Blit(Texture, RenderTexture)、LeonKnifeWebExporter.MaterialData.MaterialData()、List&lt;LeonKnifeWebExporter.MaterialData&gt;.Add(LeonKnifeWebExporter.MaterialData)、Material.HasProperty(string) 等共 18 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 LeonKnifeWebExporter.ExportTo(string, string)；下游（含内部回调体） Convert.ToBase64String(byte[])、Dictionary&lt;Material, int&gt;.Add(Material, int)、Dictionary&lt;Material, int&gt;.TryGetValue(Material, out int)、Graphics.Blit(Texture, RenderTexture)、LeonKnifeWebExporter.MaterialData.MaterialData()、List&lt;LeonKnifeWebExporter.MaterialData&gt;.Add(LeonKnifeWebExporter.MaterialData)、Material.HasProperty(string) 等共 18 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="source">中文含义：来源材质。使用位置：作为 Dictionary&lt;Material, int&gt;.Add(Material, int) 的参数、作为 Dictionary&lt;Material, int&gt;.TryGetValue(Material, out int) 的参数、RegisterMaterial(Material)。</param>
        int RegisterMaterial(Material source)
        {
            if (source && materialTable.TryGetValue(source, out int /* 中文含义：是否具有。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ hasHas))
            {
                return hasHas;
            }

            // colorValue（颜色值）：使用位置：RegisterMaterial(Material)。
global::UnityEngine.Color colorValue;
            if (source && source.HasProperty("_Color"))
            {
                colorValue = source.color;
            }
            else
            {
                colorValue = Color.white;
            }

            // createdValue2（本次新建的当前材质数据）：保存本次创建实例，按原先顺序完成逐项初始化后交给后续使用方。使用位置：RegisterMaterial(Material)。
global::LeonKnifeWebExporter.MaterialData createdValue2 = new MaterialData();
            if (source)
            {
                createdValue2.name = source.name;
            }
            else
            {
                createdValue2.name = "默认";
            }

            createdValue2.color = new[]
            {
                colorValue.r,
                colorValue.g,
                colorValue.b,
                colorValue.a
            };
            createdValue2.transparent = source && source.renderQueue >= 2450;
            // item（当前材质数据）：使用位置：作为 List<LeonKnifeWebExporter.MaterialData>.Add(LeonKnifeWebExporter.MaterialData) 的参数、RegisterMaterial(Material)。
var item = createdValue2;
            if (source && source.mainTexture)
            {
                // originalImage（原始图像）：使用位置：作为 Graphics.Blit(Texture, RenderTexture) 的参数、作为 Mathf.Min(int, int) 的参数、作为 Mathf.RoundToInt(float) 的参数。
var originalImage = source.mainTexture;

                // 中文含义：widthValue 为“宽度值”。；heightValue 为“高度值”。
int widthValue = Mathf.Min(1024, originalImage.width), heightValue = Mathf.Max(1, Mathf.RoundToInt(originalImage.height * (widthValue / (float)originalImage.width)));
                // temporary（临时）：保存 RenderTexture.GetTemporary(int, int, int, RenderTextureFormat, RenderTextureReadWrite) 的返回结果，供当前步骤使用。使用位置：作为 Graphics.Blit(Texture, RenderTexture) 的参数、作为 RenderTexture.ReleaseTemporary(RenderTexture) 的参数、RegisterMaterial(Material)。
var temporary = RenderTexture.GetTemporary(widthValue, heightValue, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                // previous（前一次）：使用位置：RegisterMaterial(Material)。
var previous = RenderTexture.active;
                // image（图像）：使用位置：作为 Convert.ToBase64String(byte[]) 的参数、作为 Object.DestroyImmediate(Object) 的参数、RegisterMaterial(Material)。
var image = new Texture2D(widthValue, heightValue, TextureFormat.RGBA32, false, false);
                try
                {
                    Graphics.Blit(originalImage, temporary);
                    RenderTexture.active = temporary;
                    image.ReadPixels(new Rect(0, 0, widthValue, heightValue), 0, 0);
                    image.Apply();
                    item.image = Convert.ToBase64String(image.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(temporary);
                    Object.DestroyImmediate(image);
                }
            }

            // index（当前元素索引）：使用位置：作为 Dictionary<Material, int>.Add(Material, int) 的参数、RegisterMaterial(Material)。
int index = package.material.Count;
            package.material.Add(item);
            if (source)
            {
                materialTable.Add(source, index);
            }

            return index;
        }

        // character（角色）：本次步骤使用的资源句柄，仅在 using 作用域内使用；离开作用域时按原流程释放。
using (var character = new LeonKnifeAnimationPreview.PreviewCharacter())
        {
            character.bakePreviewMesh = false;
            character.Sample(LeonKnifeAnimationPreview.Read("低位待机"), 0, 0, 0);
            // rightHand（右手）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：作为 Transform.IsChildOf(Transform) 的参数。
var rightHand = character.AnimatorValue.GetBoneTransform(HumanBodyBones.RightHand);
            // renderer（渲染器）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 List<LeonKnifeWebExporter.MeshData>.Add(LeonKnifeWebExporter.MeshData) 的参数、作为 RegisterBone(Transform, Matrix4x4) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
foreach (var renderer in character.Model.GetComponentsInChildren<Renderer>(true))
            {
                // skinning（蒙皮变形）：使用位置：作为 List<LeonKnifeWebExporter.MeshData>.Add(LeonKnifeWebExporter.MeshData) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
var skinning = renderer as SkinnedMeshRenderer;
                if (skinning == null && (!renderer.enabled || renderer.name == "逐帧变形预览"))
                {
                    continue;
                }

                if (skinning != null && skinning.transform.Find("逐帧变形预览") == null)
                {
                    continue;
                }

                // filter（筛选器）：保存 Component.GetComponent<MeshFilter>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
var filter = renderer.GetComponent<MeshFilter>();
                // mesh（网格）：使用位置：作为 LeonKnifeWebExporter.Encode(Array) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
global::UnityEngine.Mesh mesh;
                if (skinning)
                {
                    mesh = skinning.sharedMesh;
                }
                else
                {
                    if (filter)
                    {
                        mesh = filter.sharedMesh;
                    }
                    else
                    {
                        mesh = null;
                    }
                }

                if (!mesh || mesh.vertexCount == 0)
                {
                    continue;
                }

                // point（点）：使用位置：作为 LeonKnifeWebExporter.Encode(Array) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
var point = mesh.vertices;
                // normal（普通状态值）：使用位置：作为 LeonKnifeWebExporter.Encode(Array) 的参数。
var normal = mesh.normals;
                // coordinate（坐标）：使用位置：作为 LeonKnifeWebExporter.Encode(Array) 的参数。
var coordinate = mesh.uv;
                // closeSection（关闭段落）：使用位置：作为 LeonKnifeWebExporter.Encode(Array) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
var closeSection = new float[point.Length * 4];
                // weight（权重）：使用位置：作为 LeonKnifeWebExporter.Encode(Array) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
var weight = new float[point.Length * 4];
                if (skinning)
                {
                    // skinningWeight（蒙皮变形权重）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
var skinningWeight = mesh.boneWeights;
                    // binding（绑定）：使用位置：作为 RegisterBone(Transform, Matrix4x4) 的参数。
var binding = mesh.bindposes;
                    // bone（骨骼）：使用位置：作为 RegisterBone(Transform, Matrix4x4) 的参数。
var bone = skinning.bones;
                    // mapping（映射关系）：使用位置：RotateIndex(int)。
var mapping = new Dictionary<int, int>();
                    /// <summary>RotateIndex 的业务处理入口；本方法直接依赖 Dictionary&lt;int, int&gt;.Add(int, int)、Dictionary&lt;int, int&gt;.TryGetValue(int, out int)、RegisterBone(Transform, Matrix4x4)，调用方通过参数与返回结果交接本次处理。</summary>
                    /// <remarks>静态调用链：上游 LeonKnifeWebExporter.ExportTo(string, string)；下游（含内部回调体） Dictionary&lt;int, int&gt;.Add(int, int)、Dictionary&lt;int, int&gt;.TryGetValue(int, out int)、RegisterBone(Transform, Matrix4x4)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
                    /// <param name="id">中文含义：编号。使用位置：作为 Dictionary&lt;int, int&gt;.Add(int, int) 的参数、作为 Dictionary&lt;int, int&gt;.TryGetValue(int, out int) 的参数、RotateIndex(int)。</param>
                    int RotateIndex(int id)
                    {
                        if (!mapping.TryGetValue(id, out int /* 中文含义：索引。由当前模式匹配、解构或 out 调用取得，供后续分支使用。 */ index))
                        {
                            index = RegisterBone(bone[id], binding[id]);
                            mapping.Add(id, index);
                        }

                        return index;
                    }

                    // i（当前遍历索引）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
for (int i = 0; i < point.Length; i++)
                    {
                        // weightValue（权重值）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
var weightValue = skinningWeight[i];
                        // index（当前元素索引）：使用位置：作为 RotateIndex(int) 的参数。
int[] index =
                        {
                            weightValue.boneIndex0,
                            weightValue.boneIndex1,
                            weightValue.boneIndex2,
                            weightValue.boneIndex3
                        };
                        // value（当前浮点数值数组）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
float[] value =
                        {
                            weightValue.weight0,
                            weightValue.weight1,
                            weightValue.weight2,
                            weightValue.weight3
                        };
                        // j（内层遍历索引）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
for (int j = 0; j < 4; j++)
                        {
                            // assignmentReceiver6（赋值前保存的目标关闭段落）：从关闭段落读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
float[] assignmentReceiver6 = closeSection;
                            // assignmentIndex7（赋值前保存的索引整数计算）：按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
int assignmentIndex7 = i * 4 + j;
                            // selectedValue8（分支选出的0）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
int selectedValue8;
                            if (value[j] > 0)
                            {
                                selectedValue8 = RotateIndex(index[j]);
                            }
                            else
                            {
                                selectedValue8 = 0;
                            }

                            assignmentReceiver6[assignmentIndex7] = selectedValue8;
                            weight[i * 4 + j] = value[j];
                        }
                    }
                }
                else
                {
                    // index（当前元素索引）：保存 RegisterBone(Transform, Matrix4x4) 的返回结果，供当前步骤使用。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
int index = RegisterBone(renderer.transform, Matrix4x4.identity);
                    // i（当前遍历索引）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
for (int i = 0; i < point.Length; i++)
                    {
                        closeSection[i * 4] = index;
                        weight[i * 4] = 1;
                    }
                }

                // child（子级）：使用位置：作为 Mathf.Min(int, int) 的参数、作为 Mesh.GetTriangles(int) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
for (int child = 0; child < mesh.subMeshCount; child++)
                {
                    // material（材质）：使用位置：作为 Mathf.Min(int, int) 的参数、作为 RegisterMaterial(Material) 的参数。
var material = renderer.sharedMaterials;
                    package.mesh.Add(new MeshData { name = renderer.name, vertex = Encode(point.SelectMany( /* 中文含义：向量（三维向量）。 */v => new[] { v.x, v.y, v.z }).ToArray()), normal = Encode(normal.SelectMany( /* 中文含义：向量（三维向量）。 */v => new[] { v.x, v.y, v.z }).ToArray()), textureCoordinates = Encode(coordinate.SelectMany( /* 中文含义：向量（二维向量）。 */v => new[] { v.x, v.y }).ToArray()), closeSection = Encode(closeSection), weight = Encode(weight), index = Encode(mesh.GetTriangles(child)), material = RegisterMaterial(material[Mathf.Min(child, material.Length - 1)]), knifeVisual = !skinning && renderer.transform.IsChildOf(rightHand) });
                }
            }

            package.boneCount = boneTable.Count;
            // nameValue（名称值）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 List<LeonKnifeWebExporter.AnimationData>.Add(LeonKnifeWebExporter.AnimationData) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
foreach (string nameValue in LeonKnifeAnimationAuthoring.clipName.Concat(new[] { "单刀完整回收", "两刀连续回收", "防御完整衔接" }))
            {
                // sequence（序号）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
string[] sequence;
                if (nameValue == "单刀完整回收")
                {
                    sequence = new[]
                    {
                        "右向左主挥砍",
                        "主挥砍回收",
                        "低位待机"
                    };
                }
                else
                {
                    if (nameValue == "两刀连续回收")
                    {
                        sequence = new[]
                        {
                            "右向左主挥砍",
                            "左向右回切",
                            "回切回收",
                            "低位待机"
                        };
                    }
                    else
                    {
                        if (nameValue == "防御完整衔接")
                        {
                            sequence = new[]
                            {
                                "拔刀",
                                "高位防御",
                                "普通格挡反应",
                                "高位防御",
                                "弹反成功",
                                "高位防御",
                                "放下",
                                "低位待机"
                            };
                        }
                        else
                        {
                            sequence = new[]
                            {
                                nameValue
                            };
                        }
                    }
                }

                // clipGroup（动画片段分组）：保存 IEnumerable<AnimationClip>.ToArray<AnimationClip>() 的返回结果，供当前步骤使用。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
var clipGroup = sequence.Select(LeonKnifeAnimationPreview.Read).ToArray();
                // mode（当前整数数组）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
int[] mode = new[]
                {
                    0,
                    1,
                    2,
                    3,
                    4,
                    5,
                    6
                };
                // movement（移动）：每轮从模式取得一个元素，仅代表当前这一项。使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 List<LeonKnifeWebExporter.AnimationData>.Add(LeonKnifeWebExporter.AnimationData) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
foreach (int movement in mode)
                {
                    // totalLong（总计长型）：保存 IEnumerable<AnimationClip>.Sum<AnimationClip>(Func<AnimationClip, float>) 的返回结果，供当前步骤使用。使用位置：作为 List<LeonKnifeWebExporter.AnimationData>.Add(LeonKnifeWebExporter.AnimationData) 的参数、作为 Mathf.Ceil(float) 的参数、作为 Mathf.RoundToInt(float) 的参数 等共 4 处。
float totalLong = clipGroup.Sum( /* 中文含义：当前动画片段。 */item => item.length);
                    if (nameValue == "高位防御" && movement == 5)
                    {
                        totalLong = 3f;
                    }

                    if (movement >= 1 && movement <= 4 && (nameValue == "低位待机" || nameValue == "高位防御" || clipGroup.Length > 1))
                    {
                        // direction（方向）：使用位置：作为 LeonKnifeAnimationPreview.ReadSourceMaterial(string) 的参数。
string[] direction =
                        {
                            "F",
                            "F",
                            "B",
                            "L",
                            "R"
                        };
                        // gait（步态）：保存 LeonKnifeAnimationPreview.ReadSourceMaterial(string) 的返回结果，供当前步骤使用。使用位置：作为 Mathf.Ceil(float) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
var gait = LeonKnifeAnimationPreview.ReadSourceMaterial("Knife_St_LocomotionSet_IPC/Walk/Act_Walk_" + direction[movement] + "_IPC");
                        // 循环展示结束于完整步态周期，避免回到首帧时双腿跳步。
                        totalLong = Mathf.Ceil(totalLong / gait.length) * gait.length;
                    }

                    // frameCount（帧数量）：使用位置：作为 List<LeonKnifeWebExporter.AnimationData>.Add(LeonKnifeWebExporter.AnimationData) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
int frameCount = Mathf.RoundToInt(totalLong * 60) + 1;
                    // matrix（矩阵）：使用位置：作为 LeonKnifeWebExporter.Encode(Array) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
var matrix = new float[frameCount * boneTable.Count * 16];
                    // boneLines（骨骼线集合）：使用位置：作为 LeonKnifeWebExporter.Encode(Array) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
var boneLines = new float[frameCount * checkBone.Length * 3];
                    // frame（帧）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
for (int frame = 0; frame < frameCount; frame++)
                    {

                        // 中文含义：time 为“时间”。；local 为“局部”。
float time = frame * totalLong / (frameCount - 1), local = time;
                        // clip（动画片段）：使用位置：作为 LeonKnifeAnimationPreview.PreviewCharacter.Sample(AnimationClip, float, int, float) 的参数、作为 Mathf.Repeat(float, float) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
AnimationClip clip = clipGroup[clipGroup.Length - 1];
                        // segment（线段）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
for (int segment = 0; segment < clipGroup.Length; segment++)
                        {
                            clip = clipGroup[segment];
                            if (local <= clip.length || segment == clipGroup.Length - 1)
                            {
                                break;
                            }

                            local -= clip.length;
                        }

                        if (clip.name == "低位待机" || clip.name == "高位防御")
                        {
                            local = Mathf.Repeat(local, clip.length);
                        }

                        character.Sample(clip, local, movement, time);
                        // index（当前元素索引）：使用位置：作为 List<LeonKnifeWebExporter.BoneRecord>.this[int] 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
for (int index = 0; index < boneTable.Count; index++)
                        {
                            // matrixValue（矩阵值）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
var matrixValue = boneTable[index].bone.localToWorldMatrix * boneTable[index].binding;
                            // item（物品）：使用位置：作为 Matrix4x4.this[int] 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
for (int item = 0; item < 16; item++)
                            {
                                matrix[(frame * boneTable.Count + index) * 16 + item] = matrixValue[item];
                            }
                        }

                        // index（当前元素索引）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
for (int index = 0; index < checkBone.Length; index++)
                        {
                            // bone（骨骼）：保存 Animator.GetBoneTransform(HumanBodyBones) 的返回结果，供当前步骤使用。使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
var bone = character.AnimatorValue.GetBoneTransform(checkBone[index]);
                            // positionValue（位置值）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
global::UnityEngine.Vector3 positionValue;
                            if (bone)
                            {
                                positionValue = bone.position;
                            }
                            else
                            {
                                positionValue = Vector3.zero;
                            }

                            // start（起始）：使用位置：LeonKnifeWebExporter.ExportTo(string, string)。
int start = (frame * checkBone.Length + index) * 3;
                            boneLines[start] = positionValue.x;
                            boneLines[start + 1] = positionValue.y;
                            boneLines[start + 2] = positionValue.z;
                        }
                    }

                    // hideKnife（隐藏小刀）：使用位置：作为 List<LeonKnifeWebExporter.AnimationData>.Add(LeonKnifeWebExporter.AnimationData) 的参数、LeonKnifeWebExporter.ExportTo(string, string)。
float[] hideKnife;
                    if (nameValue == "防御完整衔接" || nameValue == "拔刀")
                    {
                        hideKnife = new[]
                        {
                            0f,
                            .0375f
                        };
                    }
                    else
                    {
                        if (nameValue == "收刀")
                        {
                            hideKnife = new[]
                            {
                                .46f,
                                .5001f
                            };
                        }
                        else
                        {
                            hideKnife = new float[0];
                        }
                    }

                    package.action.Add(new AnimationData { name = nameValue, movement = movement, duration = totalLong, frameCount = frameCount, matrix = Encode(matrix), boneLines = Encode(boneLines), hideKnifeInterval = hideKnife });
                }
            }
        }

        File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(package));
        return "网页数据已导出：" + Path.GetFullPath(path) + "；网格=" + package.mesh.Count + "；骨矩阵=" + package.boneCount + "；动作及移动组合=" + package.action.Count;
    }

    /// <summary>Encode 的业务处理入口；本方法直接依赖 Buffer.BlockCopy(Array, int, Array, int, int)、Buffer.ByteLength(Array)、Convert.ToBase64String(byte[])，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 LeonKnifeWebExporter.ExportTo(string, string)；下游（含内部回调体） Buffer.BlockCopy(Array, int, Array, int, int)、Buffer.ByteLength(Array)、Convert.ToBase64String(byte[])。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="array">中文含义：数组。使用位置：作为 Buffer.BlockCopy(Array, int, Array, int, int) 的参数、作为 Buffer.ByteLength(Array) 的参数。</param>
    static string Encode(Array array)
    {
        // byteValue（字节值）：使用位置：作为 Buffer.BlockCopy(Array, int, Array, int, int) 的参数、作为 Convert.ToBase64String(byte[]) 的参数。
var byteValue = new byte[Buffer.ByteLength(array)];
        Buffer.BlockCopy(array, 0, byteValue, 0, byteValue.Length);
        return Convert.ToBase64String(byteValue);
    }
}

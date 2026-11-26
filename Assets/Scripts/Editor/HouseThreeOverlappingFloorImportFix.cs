using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>编辑器配置与校验模块 HouseThreeOverlappingFloorImportFix；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "三号屋重叠地面导入修正")]
// 原地图的砖地与四片石土地几乎共面；仅降低这四片底面，保留砖面缺口的支撑。
public sealed class HouseThreeOverlappingFloorImportFix : AssetPostprocessor
{
    /// <summary>OnPostprocessModel 的业务处理入口；本方法直接依赖 GameObject.GetComponentsInChildren&lt;MeshFilter&gt;(bool)、HashSet&lt;int&gt;.Add(int)、HashSet&lt;int&gt;.Contains(int)、HashSet&lt;int&gt;.HashSet()、InvalidOperationException.InvalidOperationException(string)、Mathf.Abs(float)、Mesh.GetTriangles(int) 等共 10 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.GetComponentsInChildren&lt;MeshFilter&gt;(bool)、HashSet&lt;int&gt;.Add(int)、HashSet&lt;int&gt;.Contains(int)、HashSet&lt;int&gt;.HashSet()、InvalidOperationException.InvalidOperationException(string)、Mathf.Abs(float)、Mesh.GetTriangles(int) 等共 10 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="root">根游戏对象。</param>
    private void OnPostprocessModel(GameObject root)
    {
        if (assetPath != "Assets/Models/环境/村庄/VillageDemo.fbx")
        {
            return;
        }

        // 修正前方数量。
int fixFrontCount = 0;
        // filter（筛选器）：每轮从取得组件集合内部子对象集合的返回结果取得一个元素，仅代表当前这一项。使用位置：HouseThreeOverlappingFloorImportFix.OnPostprocessModel(GameObject)。
foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            // 网格。
var mesh = filter.sharedMesh;
            if (!mesh || mesh.subMeshCount <= 73)
            {
                continue;
            }

            // 顶点。
var vertex = mesh.vertices;
            // 当前元素索引。
var index = mesh.GetTriangles(73);
            // 选定顶点。
var selectedVertex = new HashSet<int>();
            // 选定第三角度。
var selectedThreeAngle = new HashSet<int>();
            // 当前遍历索引。
for (int i = 0; i < index.Length; i += 3)
            {
                // 首个。
var first = filter.transform.TransformPoint(vertex[index[i]]);
                // 第二。
var second = filter.transform.TransformPoint(vertex[index[i + 1]]);
                // 第三。
var third = filter.transform.TransformPoint(vertex[index[i + 2]]);
                // 中心。
var center = (first + second + third) / 3f;
                if (center.x <= -12f || center.x >= 0f || center.z <= -22f || center.z >= -8f || Mathf.Abs(first.y + .031101f) > .0001f || Mathf.Abs(second.y + .031101f) > .0001f || Mathf.Abs(third.y + .031101f) > .0001f)
                {
                    continue;
                }

                selectedThreeAngle.Add(i);
                // 内层遍历索引。
for (int j = 0; j < 3; j++)
                {
                    selectedVertex.Add(index[i + j]);
                }
            }

            if (selectedThreeAngle.Count == 0)
            {
                continue;
            }

            if (selectedThreeAngle.Count != 4)
            {
                throw new InvalidOperationException("三号屋底层地面结构已变化，停止局部修正，避免误改地图。");
            }

            // 不移动与其它地表共用的顶点，模型更新后必须重新核实局部范围。
            // 子级网格。
for (int childMesh = 0; childMesh < mesh.subMeshCount; childMesh++)
            {
                // 另一个索引。
var otherIndex = mesh.GetTriangles(childMesh);
                // 当前遍历索引。
for (int i = 0; i < otherIndex.Length; i += 3)
                {
                    if (childMesh == 73 && selectedThreeAngle.Contains(i))
                    {
                        continue;
                    }

                    // 内层遍历索引。
for (int j = 0; j < 3; j++)
                    {
                        if (selectedVertex.Contains(otherIndex[i + j]))
                        {
                            throw new InvalidOperationException("三号屋底层地面与其它面共用顶点，需要重新检查模型。");
                        }
                    }
                }
            }

            // 偏移量。
var offset = filter.transform.InverseTransformVector(Vector3.down * .03f);
            // vertexIndex（顶点索引）：每轮从选定顶点取得一个元素，仅代表当前这一项。使用位置：HouseThreeOverlappingFloorImportFix.OnPostprocessModel(GameObject)。
foreach (int vertexIndex in selectedVertex)
            {
                vertex[vertexIndex] += offset;
            }

            mesh.vertices = vertex;
            mesh.RecalculateBounds();
            fixFrontCount += selectedThreeAngle.Count;
        }

        if (fixFrontCount != 4)
        {
            throw new InvalidOperationException("未找到预期的三号屋重叠底面，需要重新检查模型坐标。");
        }
    }
}

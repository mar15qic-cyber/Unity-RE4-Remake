using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>迁移边界检查返回具体文件与行号，供集成校验和规则测试共用。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "阶段五边界校验")]
public static class PhaseFiveBoundaryValidator
{
    /// <summary>Validate 的业务处理入口；本方法直接依赖 Directory.GetFiles(string, string, SearchOption)、PhaseFiveBoundaryValidator.CheckFile(string, string, List&lt;string&gt;)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） Directory.GetFiles(string, string, SearchOption)、PhaseFiveBoundaryValidator.CheckFile(string, string, List&lt;string&gt;)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failure">失败。</param>
    public static void Validate(List<string> failure)
    {
        // path（路径）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 PhaseFiveBoundaryValidator.CheckFile(string, string, List<string>) 的参数。
foreach (string path in new[]
        {
            "Assets/Scripts/架构/背包规则/InventoryRulesModel.cs",
            "Assets/Scripts/架构/背包规则/StorageRulesModel.cs",
            "Assets/Scripts/架构/背包规则/ItemUseService.cs",
            "Assets/Scripts/架构/任务规则/MissionRulesModel.cs",
            "Assets/Scripts/Core/GameplayPermissionService.cs",
            "Assets/Scripts/Core/MatchFlow.cs",
            "Assets/Scripts/Drop/DropWeightRules.cs"
        }

        )
        {
            CheckFile(path, @"\.Instance\b|Resources\.Load|FindObject|GameUIManager|MonoBehaviour|Time\.timeScale", failure);
        }

        // path（路径）：每轮从取得文件集合的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 PhaseFiveBoundaryValidator.CheckFile(string, string, List<string>) 的参数。
foreach (string path in Directory.GetFiles("Assets/Scripts/UI", "*.cs", SearchOption.AllDirectories))
        {
            CheckFile(path, @"Time\.timeScale\s*=(?!=)", failure);
        }

        // path（路径）：每轮从被遍历的集合取得一个元素，仅代表当前这一项。使用位置：作为 PhaseFiveBoundaryValidator.CheckFile(string, string, List<string>) 的参数。
foreach (string path in new[]
        {
            "Assets/Scripts/Inventory/InventoryManager.cs",
            "Assets/Scripts/Inventory/StorageBoxManager.cs"
        }

        )
        {
            CheckFile(path, @"(?:private|public)\s+(?:readonly\s+)?List<GridItem>\s+\w+\s*[=;]", failure);
        }
    }

    /// <summary>CheckFile 的业务处理入口；本方法直接依赖 File.Exists(string)、File.ReadAllLines(string)、List&lt;string&gt;.Add(string)、Regex.IsMatch(string, string)、string.StartsWith(string)、string.Trim()、string.TrimStart()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 PhaseFiveBoundaryValidator.Validate(List&lt;string&gt;)；下游（含内部回调体） File.Exists(string)、File.ReadAllLines(string)、List&lt;string&gt;.Add(string)、Regex.IsMatch(string, string)、string.StartsWith(string)、string.Trim()、string.TrimStart()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="path">路径。</param>
    /// <param name="expression">表达式。</param>
    /// <param name="failure">失败。</param>
    private static void CheckFile(string path, string expression, List<string> failure)
    {
        if (!File.Exists(path))
        {
            failure.Add("缺少边界文件：" + path);
            return;
        }

        // 行。
string[] row = File.ReadAllLines(path);
        // 当前遍历索引。
for (int i = 0; i < row.Length; i++)
        {
            if (!row[i].TrimStart().StartsWith("//") && Regex.IsMatch(row[i], expression))
            {
                failure.Add(path + ":" + (i + 1) + " 存在越界依赖或状态写入：" + row[i].Trim());
            }
        }
    }
}

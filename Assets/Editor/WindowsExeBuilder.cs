using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectTools.Editor
{
    /// <summary>
    /// Windows x64 exe 打包入口，产物输出到 Builds/Windows/。
    /// </summary>
    /// <remarks>正式 Windows 构建工具，入口执行时会保存已打开场景并输出构建产物；本次只调整源码可读性，不自动调用构建或场景保存。</remarks>
    public static class WindowsExeBuilder
    {
        /// <summary>输出方向。编译期常量，保持既有值与消费者映射。</summary>
        private const string OutputDir = "Builds/Windows";
        /// <summary>结果文件。编译期常量，保持既有值与消费者映射。</summary>
        private const string ResultFile = "Builds/build-result.txt";
        /// <summary>编辑器菜单调用入口 BuildFromMenu；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
        /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） WindowsExeBuilder.Build()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        [MenuItem("工具/打包/Windows EXE")]
        public static void BuildFromMenu()
        {
            Build();
        }

        /// <summary>Build 的业务处理入口；本方法直接依赖 BuildPipeline.BuildPlayer(BuildPlayerOptions)、BuildPlayerOptions.BuildPlayerOptions()、Debug.Log(object)、Debug.LogError(object)、Debug.LogException(Exception)、Dictionary&lt;string, object&gt;.Dictionary()、Directory.CreateDirectory(string) 等共 23 处，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 WindowsExeBuilder.BuildFromMenu()；下游（含内部回调体） BuildPipeline.BuildPlayer(BuildPlayerOptions)、BuildPlayerOptions.BuildPlayerOptions()、Debug.Log(object)、Debug.LogError(object)、Debug.LogException(Exception)、Dictionary&lt;string, object&gt;.Dictionary()、Directory.CreateDirectory(string) 等共 23 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        public static Dictionary<string, object> Build()
        {
            // 打包前保存未保存的场景改动，确保产物包含当前工作内容
            // 任意是否有待处理改动。
bool anyDirty = Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt).Any( /* 中文含义：当前场景。 */s => s.isDirty);
            // 已保存备注。
string savedNote = "";
            if (anyDirty)
            {
                EditorSceneManager.SaveOpenScenes();
                savedNote = "已自动保存未保存的场景改动";
            }

            // 场景集合。
string[] scenes = EditorBuildSettings.scenes.Where( /* 中文含义：当前编辑器构建设置场景。 */s => s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path)).Select( /* 中文含义：当前编辑器构建设置场景。 */s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                return Fail("Build Settings 中没有可用的启用场景");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                // 是否已切换。
bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                if (!switched)
                {
                    return Fail("切换构建目标到 StandaloneWindows64 失败");
                }
            }

            Directory.CreateDirectory(OutputDir);
            // 可执行文件路径。
string exePath = Path.Combine(OutputDir, PlayerSettings.productName + ".exe");
            // 报告。
BuildReport report = null;
            // 失败。
string failure = null;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = exePath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            }
            catch (Exception /* 中文含义：捕获的异常信息。保存当前 catch 捕获的异常，供记录和错误处理使用。 */ e)
            {
                failure = e.Message;
                Debug.LogException(e);
            }

            // 摘要。
BuildSummary summary;
            if (report != null)
            {
                summary = report.summary;
            }
            else
            {
                summary = default;
            }

            // 是否成功。
bool success = failure == null && summary.result == BuildResult.Succeeded;
            // 尺寸组件。
float sizeMb = summary.totalSize / (1024f * 1024f);
            // 本次新建的当前键值映射表。
global::System.Collections.Generic.Dictionary<string, object> createdValue2 = new Dictionary<string, object>();
            // 初始化时使用的索引文字“success”。
string initializerIndex3 = "success";
            createdValue2[initializerIndex3] = success;
            // 初始化时使用的索引文字“result”。
string initializerIndex4 = "result";
            // 分支选出的文字“Exception”。
string selectedValue5;
            if (failure != null)
            {
                selectedValue5 = "Exception";
            }
            else
            {
                selectedValue5 = summary.result.ToString();
            }

            createdValue2[initializerIndex4] = selectedValue5;
            // 初始化时使用的索引文字“exePath”。
string initializerIndex6 = "exePath";
            createdValue2[initializerIndex6] = exePath;
            // 初始化时使用的索引文字“sizeMB”。
string initializerIndex7 = "sizeMB";
            createdValue2[initializerIndex7] = (float)Math.Round(sizeMb, 1);
            // 初始化时使用的索引文字“sceneCount”。
string initializerIndex8 = "sceneCount";
            createdValue2[initializerIndex8] = scenes.Length;
            // 初始化时使用的索引文字“scenes”。
string initializerIndex9 = "scenes";
            createdValue2[initializerIndex9] = scenes;
            // 初始化时使用的索引文字“errors”。
string initializerIndex10 = "errors";
            createdValue2[initializerIndex10] = summary.totalErrors;
            // 初始化时使用的索引文字“warnings”。
string initializerIndex11 = "warnings";
            createdValue2[initializerIndex11] = summary.totalWarnings;
            // 初始化时使用的索引文字“savedDirtyScenes”。
string initializerIndex12 = "savedDirtyScenes";
            createdValue2[initializerIndex12] = savedNote;
            // 初始化时使用的索引文字“failure”。
string initializerIndex13 = "failure";
            createdValue2[initializerIndex13] = failure;
            // 当前键值映射表。
var result = createdValue2;
            // 结果落盘，供工具超时后从外部轮询
            try
            {
                // 按原顺序缓存的实参结果文件。
string argumentValue16 = ResultFile;
                // 插值文本使用的是否成功。
bool formatArgument15 = success;
                // 分支选出的文字“Exception”。
string selectedValue14;
                if (failure != null)
                {
                    selectedValue14 = "Exception";
                }
                else
                {
                    selectedValue14 = summary.result.ToString();
                }

                File.WriteAllText(argumentValue16, $"success={formatArgument15}\nresult={(selectedValue14)}\n" + $"exePath={exePath}\nsizeMB={sizeMb:F1}\nsceneCount={scenes.Length}\n" + $"errors={summary.totalErrors}\nwarnings={summary.totalWarnings}\n" + $"failure={failure}\ntimestamp={DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch
            {
            // 结果文件写失败不影响打包结果本身
            }

            if (success)
            {
                Debug.Log($"打包成功: {exePath} ({sizeMb:F1} MB, 警告 {summary.totalWarnings})");
            }
            else
            {
                // 空值判断前缓存的失败。
string availableValue17 = failure;
                // 分支选出的可用值17。
string selectedValue18;
                if (!global::System.Object.ReferenceEquals(availableValue17, null))
                {
                    selectedValue18 = availableValue17;
                }
                else
                {
                    selectedValue18 = summary.result.ToString();
                }

                Debug.LogError($"打包失败: {selectedValue18} (错误 {summary.totalErrors})");
            }

            return result;
        }

        /// <summary>Fail 的业务处理入口；本方法直接依赖 Debug.LogError(object)、Dictionary&lt;string, object&gt;.Dictionary()，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 WindowsExeBuilder.Build()；下游（含内部回调体） Debug.LogError(object)、Dictionary&lt;string, object&gt;.Dictionary()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="message">消息。</param>
        private static Dictionary<string, object> Fail(string message)
        {
            Debug.LogError(message);
            return new Dictionary<string, object>
            {
                ["success"] = false,
                ["failure"] = message
            };
        }
    }
}

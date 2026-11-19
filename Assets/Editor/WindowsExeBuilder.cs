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
        /// <summary>中文含义：输出方向。编译期常量，保持既有值与消费者映射。使用位置：作为 Directory.CreateDirectory(string) 的参数、作为 Path.Combine(string, string) 的参数。</summary>
        private const string OutputDir = "Builds/Windows";
        /// <summary>中文含义：结果文件。编译期常量，保持既有值与消费者映射。使用位置：WindowsExeBuilder.Build()。</summary>
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
            // anyDirty（任意是否有待处理改动）：保存 IEnumerable<Scene>.Any<Scene>(Func<Scene, bool>) 的返回结果，供当前步骤使用。使用位置：WindowsExeBuilder.Build()。
bool anyDirty = Enumerable.Range(0, EditorSceneManager.sceneCount).Select(EditorSceneManager.GetSceneAt).Any( /* 中文含义：当前场景。 */s => s.isDirty);
            // savedNote（已保存备注）：使用位置：WindowsExeBuilder.Build()。
string savedNote = "";
            if (anyDirty)
            {
                EditorSceneManager.SaveOpenScenes();
                savedNote = "已自动保存未保存的场景改动";
            }

            // scenes（场景集合）：保存 IEnumerable<string>.ToArray<string>() 的返回结果，供当前步骤使用。使用位置：作为 BuildPipeline.BuildPlayer(BuildPlayerOptions) 的参数、作为 File.WriteAllText(string, string) 的参数、WindowsExeBuilder.Build()。
string[] scenes = EditorBuildSettings.scenes.Where( /* 中文含义：当前编辑器构建设置场景。 */s => s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path)).Select( /* 中文含义：当前编辑器构建设置场景。 */s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                return Fail("Build Settings 中没有可用的启用场景");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            {
                // switched（是否已切换）：保存 EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup, BuildTarget) 的返回结果，供当前步骤使用。使用位置：WindowsExeBuilder.Build()。
bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                if (!switched)
                {
                    return Fail("切换构建目标到 StandaloneWindows64 失败");
                }
            }

            Directory.CreateDirectory(OutputDir);
            // exePath（可执行文件路径）：保存 Path.Combine(string, string) 的返回结果，供当前步骤使用。使用位置：作为 BuildPipeline.BuildPlayer(BuildPlayerOptions) 的参数、作为 Debug.Log(object) 的参数、作为 File.WriteAllText(string, string) 的参数 等共 4 处。
string exePath = Path.Combine(OutputDir, PlayerSettings.productName + ".exe");
            // report（报告）：使用位置：WindowsExeBuilder.Build()。
BuildReport report = null;
            // failure（失败）：使用位置：作为 File.WriteAllText(string, string) 的参数、WindowsExeBuilder.Build()。
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

            // summary（摘要）：使用位置：作为 Debug.Log(object) 的参数、作为 Debug.LogError(object) 的参数、作为 File.WriteAllText(string, string) 的参数 等共 4 处。
BuildSummary summary;
            if (report != null)
            {
                summary = report.summary;
            }
            else
            {
                summary = default;
            }

            // success（是否成功）：使用位置：WindowsExeBuilder.Build()。
bool success = failure == null && summary.result == BuildResult.Succeeded;
            // sizeMb（尺寸组件）：使用位置：作为 Debug.Log(object) 的参数、作为 File.WriteAllText(string, string) 的参数、作为 Math.Round(double, int) 的参数。
float sizeMb = summary.totalSize / (1024f * 1024f);
            // createdValue2（本次新建的当前键值映射表）：保存本次创建实例，按原先顺序完成逐项初始化后交给后续使用方。使用位置：WindowsExeBuilder.Build()。
global::System.Collections.Generic.Dictionary<string, object> createdValue2 = new Dictionary<string, object>();
            // initializerIndex3（初始化时使用的索引文字“success”）：从文字“success”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex3 = "success";
            createdValue2[initializerIndex3] = success;
            // initializerIndex4（初始化时使用的索引文字“result”）：从文字“result”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex4 = "result";
            // selectedValue5（分支选出的文字“Exception”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：WindowsExeBuilder.Build()。
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
            // initializerIndex6（初始化时使用的索引文字“exePath”）：从文字“exePath”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex6 = "exePath";
            createdValue2[initializerIndex6] = exePath;
            // initializerIndex7（初始化时使用的索引文字“sizeMB”）：从文字“sizeMB”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex7 = "sizeMB";
            createdValue2[initializerIndex7] = (float)Math.Round(sizeMb, 1);
            // initializerIndex8（初始化时使用的索引文字“sceneCount”）：从文字“sceneCount”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex8 = "sceneCount";
            createdValue2[initializerIndex8] = scenes.Length;
            // initializerIndex9（初始化时使用的索引文字“scenes”）：从文字“scenes”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex9 = "scenes";
            createdValue2[initializerIndex9] = scenes;
            // initializerIndex10（初始化时使用的索引文字“errors”）：从文字“errors”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex10 = "errors";
            createdValue2[initializerIndex10] = summary.totalErrors;
            // initializerIndex11（初始化时使用的索引文字“warnings”）：从文字“warnings”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex11 = "warnings";
            createdValue2[initializerIndex11] = summary.totalWarnings;
            // initializerIndex12（初始化时使用的索引文字“savedDirtyScenes”）：从文字“savedDirtyScenes”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex12 = "savedDirtyScenes";
            createdValue2[initializerIndex12] = savedNote;
            // initializerIndex13（初始化时使用的索引文字“failure”）：从文字“failure”读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 Dictionary<string, object>.this[string] 的参数。
string initializerIndex13 = "failure";
            createdValue2[initializerIndex13] = failure;
            // result（当前键值映射表）：使用位置：WindowsExeBuilder.Build()。
var result = createdValue2;
            // 结果落盘，供工具超时后从外部轮询
            try
            {
                // argumentValue16（按原顺序缓存的实参结果文件）：从结果文件读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 File.WriteAllText(string, string) 的参数。
string argumentValue16 = ResultFile;
                // formatArgument15（插值文本使用的是否成功）：从成功读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：作为 File.WriteAllText(string, string) 的参数。
bool formatArgument15 = success;
                // selectedValue14（分支选出的文字“Exception”）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 File.WriteAllText(string, string) 的参数、WindowsExeBuilder.Build()。
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
                // availableValue17（空值判断前缓存的失败）：从失败读取一次后保存，供后续分支使用。保存空值判断之前的值，只读取一次，非空时不执行回退分支。使用位置：作为 object.ReferenceEquals(object, object) 的参数、WindowsExeBuilder.Build()。
string availableValue17 = failure;
                // selectedValue18（分支选出的可用值17）：保存分支选择的结果，两个分支分别赋值后供原表达式使用，避免重复求值。使用位置：作为 Debug.LogError(object) 的参数、WindowsExeBuilder.Build()。
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
        /// <param name="message">中文含义：消息。使用位置：作为 Debug.LogError(object) 的参数、WindowsExeBuilder.Fail(string)。</param>
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

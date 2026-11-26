using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>编辑器配置与校验模块 PlayerModuleBoundaryValidator；通过方法参数、返回值及声明成员与调用方通信，静态调用索引记录可解析的直接依赖。</summary>
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, "", "Assembly-CSharp-Editor", "玩家模块边界校验")]
public static class PlayerModuleBoundaryValidator
{
    /// <summary>Validate 的业务处理入口；本方法直接依赖 ArchitectureBoundaryValidator.Scan()、CheckReadOnly(Type, params string[])、List&lt;string&gt;.Add(string)、string.StartsWith(string, StringComparison)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 EnemyAIIntegrationContractValidator.Validate()；下游（含内部回调体） ArchitectureBoundaryValidator.Scan()、CheckReadOnly(Type, params string[])、List&lt;string&gt;.Add(string)、string.StartsWith(string, StringComparison)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="failure">失败。</param>
    public static void Validate(List<string> failure)
    {
        CheckReadOnly(typeof(LeonController), "currentState", "currentWeapon");
        CheckReadOnly(typeof(WeaponBase), "currentAmmo", "isReloading");
        CheckReadOnly(typeof(LeonWeaponHandler), "pistolReserveAmmo", "rifleReserveAmmo", "shotgunReserveAmmo");
        // item（当前边界记录）：每轮从扫描的返回结果取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数、PlayerModuleBoundaryValidator.Validate(List<string>)。
foreach (var item in ArchitectureBoundaryValidator.Scan())
        {
            if (item.category == "运动写入" && (item.invokeType == nameof(LeonController) || item.invokeType == nameof(LeonLocomotion) || item.invokeType == nameof(LeonWindowTraversal) || item.invokeType == nameof(LeonHighDropLanding)))
            {
                failure.Add("玩家运动执行越界：" + item.sourceFile + "：" + item.invokeMethod + "：" + item.instructionOffset);
            }

            if (item.category == "动画写入" && (item.invokeType == nameof(LeonController) || item.invokeType == nameof(LeonCombat) || item.invokeType == nameof(LeonQTECoordinator)))
            {
                failure.Add("玩家动画写入越界：" + item.sourceFile + "：" + item.invokeMethod + "：" + item.instructionOffset);
            }

            if (item.category == "运动写入" && item.invokeType == nameof(LeonQTECoordinator) && (item.invokeMethod.StartsWith("SetPlayerWorld", StringComparison.Ordinal) || item.invokeMethod.StartsWith("SetParticipantWorld", StringComparison.Ordinal)))
            {
                failure.Add("配对位移执行越界：" + item.sourceFile + "：" + item.invokeMethod);
            }
        }

        /// <summary>CheckReadOnly 的业务处理入口；本方法直接依赖 List&lt;string&gt;.Add(string)、object.ReferenceEquals(object, object)、Type.GetField(string, BindingFlags)、Type.GetProperty(string)，调用方通过参数与返回结果交接本次处理。</summary>
        /// <remarks>静态调用链：上游 PlayerModuleBoundaryValidator.Validate(List&lt;string&gt;)；下游（含内部回调体） List&lt;string&gt;.Add(string)、object.ReferenceEquals(object, object)、Type.GetField(string, BindingFlags)、Type.GetProperty(string)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
        /// <param name="type">反射操作使用的类型信息。</param>
        /// <param name="member">成员。</param>
        void CheckReadOnly(Type type, params string[] member)
        {
            // name（名称）：每轮从成员取得一个元素，仅代表当前这一项。使用位置：作为 List<string>.Add(string) 的参数、作为 Type.GetField(string, BindingFlags) 的参数、作为 Type.GetProperty(string) 的参数。
foreach (string name in member)
            {
                // 短路判断得到的分支判断。
bool conditionResult3;
                if (type.GetField(name, BindingFlags.Instance | BindingFlags.Public) != null)
                {
                    conditionResult3 = true;
                }
                else
                {
                    // 判空前缓存的取得属性的返回结果。
global::System.Reflection.PropertyInfo receiverValue1 = type.GetProperty(name);
                    // 条件访问得到的接收者值1的是否可以写入。
bool? optionalValue2;
                    if (!global::System.Object.ReferenceEquals(receiverValue1, null))
                    {
                        optionalValue2 = receiverValue1.CanWrite;
                    }
                    else
                    {
                        optionalValue2 = default;
                    }

                    conditionResult3 = optionalValue2 != false;
                }

                if (conditionResult3)
                {
                    failure.Add("业务状态必须只读：" + type.Name + "." + name);
                }
            }
        }
    }
}

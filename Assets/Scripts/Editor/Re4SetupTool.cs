using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor tool to auto-configure LeonR4R with all required components.
/// Run from Unity menu: Tools -> RE4 -> Setup Leon
/// </summary>
public class Re4SetupTool : EditorWindow
{
    /// <summary>编辑器菜单调用入口 SetupLeon；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） Debug.Log(object)、Debug.LogError(object)、EditorUtility.SetDirty(Object)、GameObject.AddComponent&lt;LeonAnimRig&gt;()、GameObject.AddComponent&lt;LeonInputBridge&gt;()、GameObject.Find(string)、GameObject.GetComponent&lt;CapsuleCollider&gt;() 等共 13 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/RE4/Setup Leon (Add All Components)", false, 100)]
    public static void SetupLeon()
    {
        // leon（里昂）：保存 GameObject.Find(string) 的返回结果，供当前步骤使用。使用位置：作为 EditorUtility.SetDirty(Object) 的参数、作为 Undo.AddComponent<CapsuleCollider>(GameObject) 的参数、作为 Undo.AddComponent<Rigidbody>(GameObject) 的参数 等共 4 处。
var leon = GameObject.Find("LeonR4R");
        if (leon == null)
        {
            Debug.LogError("[RE4 Setup] LeonR4R not found!");
            return;
        }

        // bridge（桥接）：保存 GameObject.GetComponent<LeonInputBridge>() 的返回结果，供当前步骤使用。使用位置：Re4SetupTool.SetupLeon()。
var bridge = leon.GetComponent<LeonInputBridge>();
        if (bridge == null)
        {
            leon.AddComponent<LeonInputBridge>();
            Debug.Log("[RE4 Setup] Added LeonInputBridge");
        }

        // ik（反向动力学）：保存 GameObject.GetComponent<LeonAnimRig>() 的返回结果，供当前步骤使用。使用位置：Re4SetupTool.SetupLeon()。
var ik = leon.GetComponent<LeonAnimRig>();
        if (ik == null)
        {
            leon.AddComponent<LeonAnimRig>();
            Debug.Log("[RE4 Setup] Added LeonAnimRig");
        }

        // rb（运动刚体）：保存 GameObject.GetComponent<Rigidbody>() 的返回结果，供当前步骤使用。使用位置：Re4SetupTool.SetupLeon()。
var rb = leon.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody>(leon);
        }

        rb.mass = 80f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // cc（角色碰撞器）：保存 GameObject.GetComponent<CapsuleCollider>() 的返回结果，供当前步骤使用。使用位置：Re4SetupTool.SetupLeon()。
var cc = leon.GetComponent<CapsuleCollider>();
        if (cc == null)
        {
            cc = Undo.AddComponent<CapsuleCollider>(leon);
        }

        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0, 0.9f, 0);
        Debug.Log("[RE4 Setup] LeonR4R setup complete!");
        EditorUtility.SetDirty(leon);
    }

    /// <summary>编辑器菜单调用入口 GenerateInputClass；按当前参数和已绑定配置执行资源操作，下游调用见静态索引。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） AssetDatabase.ImportAsset(string, ImportAssetOptions)、Debug.Log(object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    [MenuItem("Tools/RE4/Generate Input C# Class", false, 100)]
    public static void GenerateInputClass()
    {
        AssetDatabase.ImportAsset("Assets/Scripts/Input/LeonInputActions.inputactions", ImportAssetOptions.ForceUpdate);
        Debug.Log("[RE4 Setup] InputActions reimported.");
    }
}

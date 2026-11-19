using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 独立测试场景专用触发器。正式敌人人工智能不得依赖此组件。
/// </summary>
public class QTEEditorTestTrigger : MonoBehaviour
{
    [Header("测试引用")]
    public LeonQTECoordinator coordinator;
    public VillagerQTEParticipant[] candidates;
    public QTEKind testKind = QTEKind.FrontGrab;

    [Header("编辑器按键")]
    public Key testKey = Key.G;

#if UNITY_EDITOR
    void Update()
    {
        if (Keyboard.current == null ||
            !Keyboard.current[testKey].wasPressedThisFrame ||
            coordinator == null ||
            coordinator.IsActive)
            return;

        VillagerQTEParticipant nearest = null;
        float nearestDistance = float.MaxValue;
        if (candidates != null)
        {
            foreach (var candidate in candidates)
            {
                if (candidate == null || !candidate.IsAvailable) continue;
                float distance = Vector3.Distance(
                    coordinator.transform.position,
                    candidate.transform.position);
                if (distance >= nearestDistance) continue;
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        if (nearest == null)
        {
            Debug.LogWarning("[快速反应事件测试] 没有可用村民参与者。");
            return;
        }

        if (!coordinator.TryStart(testKind, nearest, out int sessionId))
        {
            Debug.LogWarning(
                "[快速反应事件测试] 发起失败: " +
                coordinator.LastRejectionReason);
        }
        else
        {
            Debug.Log("[快速反应事件测试] 会话已建立: " + sessionId);
        }
    }
#endif
}

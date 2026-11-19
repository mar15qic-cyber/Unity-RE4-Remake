using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using GameArchitecture;
[UnityEngine.Scripting.APIUpdating.MovedFrom(true,"","Assembly-CSharp","小刀战斗验证台")]

/// <summary>仅挂在独立验证场景；直接复用正式输入和战斗入口。</summary>
public sealed class KnifeCombatValidationConsole : MonoBehaviour
{
    [Header("验证对象")]
    [UnityEngine.Serialization.FormerlySerializedAs("小刀")]
    [UnityEngine.InspectorName("小刀")]
    public KnifeCombatModule knife;
    [UnityEngine.Serialization.FormerlySerializedAs("输入")]
    [UnityEngine.InspectorName("输入")]
    public LeonInputBridge input;
    [UnityEngine.Serialization.FormerlySerializedAs("验证敌人预制")]
    [UnityEngine.InspectorName("验证敌人预制")]
    public GameObject validateEnemyPreControl;
    [Header("显示")]
    [UnityEngine.Serialization.FormerlySerializedAs("显示面板")]
    [UnityEngine.InspectorName("显示面板")]
    public bool showPanel = true;
    [UnityEngine.Serialization.FormerlySerializedAs("自动运行")]
    [UnityEngine.InspectorName("自动运行")]
    public bool automaticRuntime;
    public bool AcceptanceRuntimeMiddle { get; private set; }
    public string LatestReport { get; private set; } = "尚未运行";
    private readonly List<string> result = new List<string>();
    private GridItem validateKnifeInstance;
    private ItemData originalKnifeDefinition;
    private int originalKnifeDurability, originalKnifeHorizontalCoordinate, originalKnifeVerticalCoordinate, originalKnifeRotation;
    private float originalTimeScale;
    private bool originalInputEnable;
    private readonly List<GameObject> temporaryObject = new List<GameObject>();
    private IEnumerator Start()
    {
        if (knife == null) knife = FindFirstObjectByType<KnifeCombatModule>();
        if (input == null && knife != null) input = knife.GetComponent<LeonInputBridge>();
        yield return new WaitForSeconds(2);
        if (automaticRuntime) BeginAutomaticAcceptance();
    }
    public void BeginAutomaticAcceptance()
    {
        if (!AcceptanceRuntimeMiddle && knife != null && knife.Ready) StartCoroutine(ExecuteAcceptance());
    }
    public void BeginTrajectoryVerify() { if (!AcceptanceRuntimeMiddle) StartCoroutine(TrajectoryVerify()); }
    private IEnumerator TrajectoryVerify()
    {
        bool originalInput = input.enabled; input.enabled = false; Time.timeScale = 1; knife.Interrupt(false); yield return null;
        var record = new List<string>(); Click(); double begin = Time.timeAsDouble;
        while (Time.timeAsDouble - begin < .56)
        {
            yield return new WaitForEndOfFrame();
            var configuration = knife.configuration.Find(knife.CurrentAction);
            if (configuration == null || !configuration.GetTrajectory(knife.CurrentActionTime, out var root, out var tip)) continue;
            var actualRoot = knife.transform.InverseTransformPoint(knife.knifeVisual.TransformPoint(knife.configuration.bladeBaseLocal));
            var actualTip = knife.transform.InverseTransformPoint(knife.knifeVisual.TransformPoint(knife.configuration.bladeTipLocal));
            var animation = knife.GetComponent<Animator>();
            record.Add(knife.CurrentActionTime.ToString("F4") + "｜制作刀尖=" + tip.ToString("F4") + "｜实际刀尖=" + actualTip.ToString("F4") +
                "｜误差=" + Vector3.Distance(tip, actualTip).ToString("F4") + "｜动画进度=" + animation.GetCurrentAnimatorStateInfo(6).normalizedTime.ToString("F4"));
        }
        string directory = Path.Combine(Application.dataPath, "../战斗日志/小刀重构"); Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory, "轨迹核对.txt"), record);
        yield return new WaitForSeconds(.5f); input.enabled = originalInput;
    }
    private void Click()
    {
        double time = Time.timeAsDouble;
        knife.CommitAttack(time); knife.CommitAttackReleased(time + .0001);
    }
    private void Check(string name, bool pass)
    { result.Add((pass ? "通过｜" : "失败｜") + name); LatestReport = string.Join("\n", result); }
    private string[] NewLog(long startPoint) => knife.ReadDiagnosticsAfter(startPoint);
    private int ActionCount(string[] log, string action) => log.Count(row => row.EndsWith("｜动作切换｜" + action, StringComparison.Ordinal));
    private IEnumerator ExecuteAcceptance()
    {
        AcceptanceRuntimeMiddle = true; result.Clear(); originalInputEnable = input != null && input.enabled;
        originalTimeScale = Time.timeScale;
        validateKnifeInstance = knife.inventory.GetFirstItem("knife");
        if (validateKnifeInstance == null) { Check("验证前持有可用小刀", false); AcceptanceRuntimeMiddle = false; yield break; }
        originalKnifeDefinition = validateKnifeInstance.item; originalKnifeDurability = validateKnifeInstance.currentDurability;
        originalKnifeHorizontalCoordinate = validateKnifeInstance.gridX; originalKnifeVerticalCoordinate = validateKnifeInstance.gridY; originalKnifeRotation = validateKnifeInstance.rotation;
        if (input != null) input.enabled = false;
        knife.combat.HandleAim(false); knife.Interrupt(false);
        yield return null;
        Check("正式管线和小刀可用", knife.player.NewPipelineValid && knife.ValidKnife);
        long startPoint = knife.DiagnosticSequence; Click();
        yield return new WaitForSeconds(1.1f);
        var log = NewLog(startPoint);
        Check("单击只有主挥砍及其回收", ActionCount(log, "PrimarySlash") == 1 && ActionCount(log, "Backslash") == 0 && ActionCount(log, "PrimarySlashRecovery") == 1);
        Check("回收结束释放动作事务", !knife.Active && !knife.coordinator.HasActiveFullBodyAction);
        startPoint = knife.DiagnosticSequence; Click(); yield return new WaitForSeconds(.05f);
        for (int iteration = 0; iteration < 8; iteration++) Click();
        yield return new WaitForSeconds(2.15f); log = NewLog(startPoint);
        Check("快速乱点最多排下一刀", ActionCount(log, "PrimarySlash") == 1 && ActionCount(log, "Backslash") == 1);
        startPoint = knife.DiagnosticSequence; knife.CommitGuard(true, Time.timeAsDouble); Click();
        yield return new WaitForSeconds(.75f); log = NewLog(startPoint);
        Check("同帧防守攻击成为单次前刺", ActionCount(log, "ThrustOne") == 1 && ActionCount(log, "ThrustTwo") == 0 && knife.GuardHold);
        knife.CommitGuard(false, Time.timeAsDouble); yield return new WaitForSeconds(.5f);
        startPoint = knife.DiagnosticSequence; knife.CommitGuard(true, Time.timeAsDouble); Click();
        yield return new WaitForSeconds(.05f); Click();
        yield return new WaitForSeconds(1.15f); log = NewLog(startPoint);
        Check("双击前刺交替两个片段", ActionCount(log, "ThrustOne") == 1 && ActionCount(log, "ThrustTwo") == 1);
        knife.Interrupt(false); startPoint = knife.DiagnosticSequence; Click();
        yield return new WaitForSeconds(.03f); knife.CommitGuard(true, Time.timeAsDouble);
        yield return new WaitForSeconds(.05f); Check("起手防守能够取消", knife.CurrentAction == KnifeAction.EnterGuard);
        knife.Interrupt(false); Click(); yield return new WaitForSeconds(.22f);
        knife.CommitGuard(true, Time.timeAsDouble); yield return null; yield return null;
        Check("有效攻击段仍保留动作承诺", knife.CurrentAction == KnifeAction.PrimarySlash);
        yield return new WaitForSeconds(.3f); Check("开放回收点执行待防守", knife.CurrentAction == KnifeAction.EnterGuard);
        knife.Interrupt(false); knife.combat.HandleAim(true);
        knife.CommitGuard(true, Time.timeAsDouble); yield return new WaitForSeconds(.15f);
        Check("防守接管枪械瞄准", !knife.combat.IsAiming && knife.Active);
        knife.CommitGuard(false, Time.timeAsDouble); yield return new WaitForSeconds(.6f);
        Check("右键意图保留并恢复瞄准", knife.combat.IsAiming && !knife.Active);
        knife.combat.HandleAim(false); knife.Interrupt(false);
        Click(); yield return new WaitForSeconds(.05f);
        double progress = knife.CurrentActionTime; Time.timeScale = 0;
        yield return new WaitForSecondsRealtime(.15f);
        Check("暂停不推进动作", Math.Abs(knife.CurrentActionTime - progress) < .000001);
        Time.timeScale = originalTimeScale;
        knife.enabled = false; yield return null;
        Check("禁用清除动作且恢复播放速度", !knife.Active && knife.GetComponent<Animator>().speed > 0);
        knife.enabled = true; yield return null;
        Click(); yield return new WaitForSeconds(.06f);
        Check("重新启用后可再次出刀", knife.CurrentAction == KnifeAction.PrimarySlash);
        knife.Interrupt(false);
        yield return ContactAcceptance();
        knife.Interrupt(false); RestoreValidateKnife(); originalKnifeDefinition = null;
        foreach (var objectValue in temporaryObject) if (objectValue != null) Destroy(objectValue); temporaryObject.Clear();
        if (input != null) input.enabled = originalInputEnable;
        string directory = Path.Combine(Application.dataPath, "../战斗日志/小刀重构"); Directory.CreateDirectory(directory);
        LatestReport = string.Join("\n", result);
        File.WriteAllText(Path.Combine(directory, "运行验收.txt"), LatestReport);
        File.WriteAllLines(Path.Combine(directory, "判定日志.txt"), knife.ReadDiagnostics());
        AcceptanceRuntimeMiddle = false;
    }
    private void OnDisable()
    {
        if (!AcceptanceRuntimeMiddle) return;
        StopAllCoroutines(); Time.timeScale = originalTimeScale;
        knife?.Interrupt(false);
        if (originalKnifeDefinition != null && knife != null && knife.inventory != null) RestoreValidateKnife();
        if (input != null) input.enabled = originalInputEnable;
        foreach (var objectValue in temporaryObject) if (objectValue != null) Destroy(objectValue); temporaryObject.Clear();
        AcceptanceRuntimeMiddle = false;
    }
    private int CurrentDurability => knife.inventory.GetFirstItem("knife")?.currentDurability ?? 0;
    private void RestoreValidateKnife()
    {
        if (originalKnifeDefinition == null) return;
        if (validateKnifeInstance != null && knife.inventory.GetItems().Contains(validateKnifeInstance))
            knife.inventory.DiscardItemAt(validateKnifeInstance.gridX, validateKnifeInstance.gridY);
        if (!knife.inventory.AddItem(originalKnifeDefinition)) { Check("恢复验证小刀", false); return; }
        validateKnifeInstance = knife.inventory.GetFirstItem("knife");
        if (originalKnifeDurability > 0 && originalKnifeDurability < originalKnifeDefinition.maxDurability)
            knife.inventory.TryConsumeDurability("knife", originalKnifeDefinition.maxDurability - originalKnifeDurability, out _);
        for (int iteration = 0; iteration < originalKnifeRotation; iteration++) knife.inventory.RotateItem(validateKnifeInstance);
        knife.inventory.MoveItem(validateKnifeInstance, originalKnifeHorizontalCoordinate, originalKnifeVerticalCoordinate);
    }
    private VillagerController NewBuildTargetDummy(int index)
    {
        var objectValue = Instantiate(validateEnemyPreControl, knife.transform.position + knife.transform.forward * (3 + index), Quaternion.identity);
        objectValue.name = "验证靶人" + index; temporaryObject.Add(objectValue);
        var enemy = objectValue.GetComponent<VillagerController>(); enemy.maxHealth = 10000; return enemy;
    }
    private void IsolateTargetDummy(VillagerController enemy)
    {
        enemy.enabled = false;
        foreach (var rowAs in enemy.GetComponents<Behaviour>())
            if (rowAs is BehaviorDesigner.Runtime.BehaviorTree tree) tree.DisableBehavior();
            else if (rowAs.GetType().Name == "PathAgent") rowAs.enabled = false;
        var rigidbody = enemy.GetComponent<Rigidbody>(); if (rigidbody != null) rigidbody.isKinematic = true;
        enemy.GetComponent<Animator>().speed = 0;
    }
    private KnifeIncomingAttackContext Contact(VillagerController enemy, bool front = true, bool canParry = true)
    {
        enemy.transform.position = knife.transform.position + knife.transform.forward * (front ? 1 : -1);
        enemy.combatModule.BeginAttack();
        return new KnifeIncomingAttackContext { attacker = enemy, attackId = enemy.combatModule.KnifeIncomingAttackId, attackSegment = 0,
            contactTime = Time.timeAsDouble, contactPosition = knife.transform.position + Vector3.up + knife.transform.forward * .35f,
            direction = -knife.transform.forward, canParry = canParry, category = KnifeIncomingAttackCategory.CloseRange, damage = 5 };
    }
    private IEnumerator ContactAcceptance()
    {
        if (validateEnemyPreControl == null) { Check("已绑定接触验证靶人", false); yield break; }
        var first = NewBuildTargetDummy(0); var second = NewBuildTargetDummy(1);
        yield return null; yield return null;
        IsolateTargetDummy(first); IsolateTargetDummy(second);
        yield return null;
        knife.Interrupt(false); knife.CommitGuard(true, Time.timeAsDouble); yield return new WaitForSeconds(.15f);
        float health = knife.player.currentHealth; int durability = CurrentDurability; long startPoint = knife.DiagnosticSequence;
        var incomingAttack = Contact(first); knife.CommitIncomingAttack(incomingAttack); knife.CommitIncomingAttack(incomingAttack);
        yield return null; yield return null;
        Check("普通招架免伤且重复接触只扣一次四点", knife.player.currentHealth == health && CurrentDurability == durability - 4 && NewLog(startPoint).Count(row => row.Contains("来袭裁定｜Normal")) == 1);
        knife.Interrupt(false); double perfectInputTimestamp = Time.timeAsDouble;
        knife.CommitGuard(true, perfectInputTimestamp); yield return null;
        durability = CurrentDurability; health = knife.player.currentHealth; startPoint = knife.DiagnosticSequence;
        var sameFrameFirst = Contact(first); var sameFrameSecond = Contact(second);
        sameFrameFirst.contactTime = sameFrameSecond.contactTime = perfectInputTimestamp + .05;
        knife.CommitIncomingAttack(sameFrameFirst); knife.CommitIncomingAttack(sameFrameSecond);
        yield return null; yield return null;
        var log = NewLog(startPoint);
        Check("同帧双敌一次完美另一击不受保护", log.Count(row => row.Contains("来袭裁定｜Perfect")) == 1 && log.Count(row => row.Contains("来袭裁定｜Failure")) == 1 && CurrentDurability == durability - 1 && knife.player.currentHealth == health - 5);
        yield return new WaitForSeconds(1.8f);
        knife.player.Heal(100);
        knife.Interrupt(false);
        knife.CommitGuard(true, Time.timeAsDouble);
        yield return new WaitForSeconds(.05f);
        knife.CommitIncomingAttack(Contact(first));
        yield return null;
        yield return null;
        health = knife.player.currentHealth;
        knife.CommitIncomingAttack(Contact(first, false)); yield return null; yield return null;
        Check("招架后背后另一攻击仍能伤害", knife.player.currentHealth == health - 5);
        yield return new WaitForSeconds(1.8f); knife.player.Heal(100); knife.Interrupt(false);
        knife.CommitGuard(true, Time.timeAsDouble); yield return new WaitForSeconds(.28f);
        health = knife.player.currentHealth; knife.CommitIncomingAttack(Contact(first, true, false)); yield return null; yield return null;
        Check("不可招架招式穿过持续防守", knife.player.currentHealth == health - 5);
        yield return new WaitForSeconds(1.8f); knife.player.Heal(100); knife.Interrupt(false);
        knife.inventory.TryConsumeDurability("knife", Mathf.Max(0, CurrentDurability - 1), out _);
        knife.CommitGuard(true, Time.timeAsDouble); yield return new WaitForSeconds(.15f);
        health = knife.player.currentHealth; knife.CommitIncomingAttack(Contact(first)); yield return null; yield return null;
        Check("最后一点耐久仍完成普通招架并断刀", knife.player.currentHealth == health && !knife.ValidKnife && !knife.Active);
        knife.CommitIncomingAttack(Contact(second)); yield return null; yield return null;
        Check("断刀后保持输入不再保护", knife.player.currentHealth == health - 5);
        yield return new WaitForSeconds(1.8f); knife.player.Heal(100); knife.Interrupt(false); RestoreValidateKnife();
        // 靶人重叠用于验证同一挥砍多目标和同一目标多碰撞体去重，不参与来袭范围标定。
        var clip = knife.configuration.Find(KnifeAction.PrimarySlash); clip.GetTrajectory(.30, out var root, out var tip);
        Vector3 point = knife.transform.TransformPoint((root + tip) * .5f);
        foreach (var enemy in new[] { first, second })
        {
            enemy.transform.position = new Vector3(point.x, knife.transform.position.y, point.z);
            foreach (var collision in enemy.GetComponentsInChildren<Collider>(true)) collision.enabled = false;
            for (int iteration = 0; iteration < 3; iteration++)
            {
                var targetDummy = new GameObject("重复命中部位" + iteration); targetDummy.transform.SetParent(enemy.transform); targetDummy.transform.position = point;
                var sphere = targetDummy.AddComponent<SphereCollider>(); sphere.radius = .18f; sphere.isTrigger = true;
            }
        }
        Physics.SyncTransforms();
        int firstCount = 0, secondCount = 0;
        void RecordHit(KnifeMeleeHitNotification notification) { if (notification.target == first) firstCount++; if (notification.target == second) secondCount++; }
        knife.CommittedHit += RecordHit; durability = CurrentDurability;
        Click(); yield return new WaitForSeconds(1.1f);
        Check("挥砍同刀多敌且每敌只结算一次", firstCount == 1 && secondCount == 1 && CurrentDurability == durability - 1);
        knife.CommittedHit -= RecordHit;
        // 前刺单刀首目标：两个独立目标的判定体覆盖同一接触区，必须只有一个目标收到通知。
        clip = knife.configuration.Find(KnifeAction.ThrustOne); clip.GetTrajectory(.18, out root, out tip);
        point = knife.transform.TransformPoint((root + tip) * .5f);
        foreach (var enemy in new[] { first, second }) foreach (var collision in enemy.GetComponentsInChildren<SphereCollider>(true)) collision.transform.position = point;
        Physics.SyncTransforms(); firstCount = secondCount = 0; knife.CommittedHit += RecordHit;
        knife.CommitGuard(true, Time.timeAsDouble); Click(); yield return new WaitForSeconds(.8f);
        Check("前刺整刀只结算首先接触的目标", firstCount + secondCount == 1);
        knife.CommittedHit -= RecordHit; knife.Interrupt(false);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); temporaryObject.Add(wall); wall.name = "验证实体遮挡";
        wall.transform.position = Vector3.Lerp(knife.transform.position + Vector3.up * 1.15f, point, .5f);
        wall.transform.localScale = new Vector3(.12f, 2, 2);
        wall.transform.rotation = Quaternion.LookRotation(point - knife.transform.position) * Quaternion.Euler(0, 90, 0);
        Physics.SyncTransforms(); firstCount = secondCount = 0; knife.CommittedHit += RecordHit;
        knife.CommitGuard(true, Time.timeAsDouble); Click(); yield return new WaitForSeconds(.8f);
        Check("实体遮挡后的目标不受刀伤", firstCount + secondCount == 0);
        knife.CommittedHit -= RecordHit;
    }
    private void OnGUI()
    {
        if (!showPanel || knife == null) return;
        GUILayout.BeginArea(new Rect(12, 12, 385, Screen.height - 24), GUI.skin.box);
        GUILayout.Label("小刀战斗验证");
        GUILayout.Label("动作：" + knife.CurrentAction + "　进度：" + knife.CurrentActionTime.ToString("F3"));
        GUI.enabled = !AcceptanceRuntimeMiddle;
        if (GUILayout.Button("运行输入与交接验收")) BeginAutomaticAcceptance();
        if (GUILayout.Button("导出当前判定日志"))
        {
            string directory = Path.Combine(Application.dataPath, "../战斗日志/小刀重构"); Directory.CreateDirectory(directory);
            File.WriteAllLines(Path.Combine(directory, "手动判定日志.txt"), knife.ReadDiagnostics());
        }
        GUI.enabled = true; GUILayout.Label(LatestReport); GUILayout.EndArea();
    }
}

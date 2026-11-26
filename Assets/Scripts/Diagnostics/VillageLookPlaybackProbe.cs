#if VILLAGE_LOOK_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

// 仅在带专用编译符号的验收播放器中启用。
public sealed class VillageLookPlaybackProbe : MonoBehaviour
{
    private string folder;
    private string segment;
    private bool farm;
    private bool pictures;
    private LeonController leon;
    private RE4CameraController rig;
    private struct FrameRow
    {
        public string segment;
        public int frame;
        public float elapsed;
        public FrameTiming timing;
    }

    private readonly List<FrameRow> samples = new List<FrameRow>(40000);
    private readonly FrameTiming[] timings = new FrameTiming[1];
    private float nextDeadline;
    private int lastEvent;
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Attach()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-villageLookProbe");
        if (index < 0 || index + 2 >= args.Length) { return; }
        var host = new GameObject("村庄画面验收");
        DontDestroyOnLoad(host);
        var probe = host.AddComponent<VillageLookPlaybackProbe>();
        probe.folder = args[index + 1];
        probe.farm = args[index + 2] == "farm";
        probe.pictures = Array.IndexOf(args, "-lookPictures") >= 0;
    }

    private void Note(string message)
    {
        File.AppendAllText(Path.Combine(folder, "events.txt"), Time.realtimeSinceStartup.ToString("F2")+" "+message+"\n");
    }

    private void LateUpdate()
    {
        if (Time.realtimeSinceStartup > nextDeadline && nextDeadline > 0f)
        {
            End("阶段超时 " + segment, 3);
            return;
        }
        if (string.IsNullOrEmpty(segment)) { return; }
        FrameTimingManager.CaptureFrameTimings();
        var sample = new FrameRow();
        sample.segment = segment;
        sample.frame = Time.frameCount;
        sample.elapsed = Time.unscaledDeltaTime * 1000f;
        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
        {
            sample.timing = timings[0];
        }
        samples.Add(sample);
    }

    private void End(string message, int code)
    {
        nextDeadline = 0;
        segment = null;
        using (var output = new StreamWriter(Path.Combine(folder, "frames.tsv")))
        {
            output.WriteLine("阶段\t帧\t帧时ms\tCPUms\tGPUms\t主线程ms\t渲染线程ms\t呈现等待ms");
            foreach (FrameRow f in samples)
            {
                var t = f.timing;
                output.WriteLine(FormattableString.Invariant($"{f.segment}\t{f.frame}\t{f.elapsed:F3}\t{t.cpuFrameTime:F3}\t{t.gpuFrameTime:F3}\t{t.cpuMainThreadFrameTime:F3}\t{t.cpuRenderThreadFrameTime:F3}\t{t.cpuMainThreadPresentWaitTime:F3}"));
            }
        }
        Note(message);
        enabled = false;
        Application.Quit(code);
    }

    private void Photo(string name)
    {
        if (pictures) { ScreenCapture.CaptureScreenshot(Path.Combine(folder, name+".png")); }
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "events.txt"),"");
        Application.runInBackground = true;
        nextDeadline = Time.realtimeSinceStartup + 120f;
        QualitySettings.SetQualityLevel(5);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        yield return null;
        Note(SystemInfo.graphicsDeviceName+" "+Screen.width+"x"+Screen.height+" "+QualitySettings.names[QualitySettings.GetQualityLevel()]+" "+SceneManager.GetActiveScene().name);
        var title = FindFirstObjectByType<TitleScreenManager>();
        if (title == null) { End("缺少标题", 2); yield break; }
        title.StartCoroutine((IEnumerator)typeof(TitleScreenManager).GetMethod("LoadGameScene", Private).Invoke(title,null));
        while (SceneManager.GetActiveScene().name != "VillageBattleLighting")
        {
            var loading = FindFirstObjectByType<ChapterLoadingView>();
            if (loading != null)
            {
                loading.SendMessage("Confirm",SendMessageOptions.RequireReceiver);
            }
            yield return null;
        }
        SceneStartupEntry startup = FindFirstObjectByType<SceneStartupEntry>();
        while (startup == null || startup.Phase != SceneStartupEntry.StartupPhase.Runtime)
        {
            yield return null;
            startup = FindFirstObjectByType<SceneStartupEntry>();
        }
        yield return new WaitForSecondsRealtime(1.5f);
        leon = FindFirstObjectByType<LeonController>();
        rig = FindFirstObjectByType<RE4CameraController>();
        var siege = FindFirstObjectByType<VillageSiegeSequence>();

        var match = FindFirstObjectByType<GameManager>();
        var ai = FindFirstObjectByType<EnemyAIManager>();
        var mission = FindFirstObjectByType<ChapterOneMissionManager>();
        var bell = FindFirstObjectByType<VillageBellSequence>(FindObjectsInactive.Include);
        var bd = (PlayableDirector)typeof(VillageBellSequence).GetField("director",Private).GetValue(bell);
        var gate = FindFirstObjectByType<VillageEntranceCombatTrigger>().GetComponent<BoxCollider>();
        var house = FindFirstObjectByType<HouseThreeTraversalGeometry>();
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Note("玩法画质 "+QualitySettings.GetQualityLevel()+" 同步 "+QualitySettings.vSyncCount+" 帧率限制 "+Application.targetFrameRate);
        UnityEngine.Random.InitState(11222026);
        Photo("01-出生"); 
        Place(gate.bounds.center,new Vector3(-1,0,-.3f));
        yield return new WaitForSecondsRealtime(1);
        Place(new Vector3(9,.1f,4),new Vector3(-1,0,-.3f));
        segment = "广场"; Photo("02-广场");
        yield return new WaitForSecondsRealtime(4);
        Note("广场敌人数 "+FindObjectsByType<VillagerController>(FindObjectsSortMode.None).Count(a=>!a.IsDead && a.gameObject.activeInHierarchy));
        segment = "接近";
        if (farm)
        {
            Place(new Vector3(-41.3f,3.3f,-28.15f),new Vector3(-.8f,0,-.6f));
        }
        else
        {
            var inward = house.InteriorGate - house.ExteriorGate;
            Place(house.ExteriorGate, inward);
            yield return new WaitForSecondsRealtime(.55f);
            Place(house.InteriorGate,inward);
        }
        string shot = "三号屋";
        if (farm) { shot = "农场"; }
        Photo(shot+"-触发");
        float wait = Time.realtimeSinceStartup + 5;
        while (siege.CurrentDirector.state != PlayState.Playing && Time.realtimeSinceStartup < wait) { yield return null; }
        if (siege.CurrentDirector.state != PlayState.Playing)
        {
            Note("隔离回放入口 "+shot);
            var entrance = VillageEntrance.House;
            if (farm) { entrance = VillageEntrance.Farm; }
            var actor = FindObjectsByType<VillagerController>(FindObjectsSortMode.None).FirstOrDefault(a=>a.name.Contains("SawMan",StringComparison.OrdinalIgnoreCase));
            siege.Play(match,entrance,actor,farm);
            yield return null;
        }
        if (siege.CurrentDirector.state != PlayState.Playing) { End("登场未播放",4); yield break; }
        var sd = siege.CurrentDirector;
        Note("登场 "+sd.playableAsset.name+" "+sd.duration);
        segment = shot;
        lastEvent=0;
        while (sd.state == PlayState.Playing)
        {
            if (lastEvent==0 && sd.time>.28) { Photo(shot+"-切入");lastEvent++; }
            else if (lastEvent==1 && sd.time>sd.duration*.47) { Photo(shot+"-中段");lastEvent++; }
            else if (lastEvent==2 && sd.time>sd.duration-.55) { Photo(shot+"-切出前");lastEvent++; }
            yield return null;
        }
        Photo(shot+"-交还");
        Note("交还 "+rig.MainCamera.enabled+" "+match.Permission.Current.AllowInput);
        if (farm) { yield return new WaitForSecondsRealtime(.6f); End("完成",0);yield break; }
        segment="室内";
        yield return new WaitForSecondsRealtime(1.5f);
        Place(new Vector3(-8.4f,.3f,-11f),new Vector3(.2f,0,-1f));
        yield return new WaitForSecondsRealtime(1.5f);
        Place(new Vector3(-8.2f,3.5f,-16f),new Vector3(-.2f,0,1f));
        yield return new WaitForSecondsRealtime(1.5f);
        Place(new Vector3(5,.1f,3),Vector3.left);
        nextDeadline = Time.realtimeSinceStartup+155;
        segment="钟响交接";
        bool viaRules = Array.IndexOf(Environment.GetCommandLineArgs(), "-lookMission") >= 0;
        bool interrupt = Array.IndexOf(Environment.GetCommandLineArgs(), "-lookInterrupt") >= 0;
        var output = bd.GetComponentInChildren<VillageBellEffects>(true);
        if (output == null)
        {
            output = bell.GetComponentInChildren<VillageBellEffects>(true);
        }
        var cinematic = output.GetComponent<Camera>();
        float far = cinematic.farClipPlane;
        bool occlusion = cinematic.useOcclusionCulling;
        bool puddle = output.puddle.enabled;
        Color fog = RenderSettings.fogColor;
        float cutoff = RenderSettings.fogEndDistance;
        if (viaRules)
        {
            nextDeadline = Time.realtimeSinceStartup + 280f;
            while (!mission.IsBellEnding)
            {
                var targets = FindObjectsByType<VillagerController>(FindObjectsSortMode.None);
                foreach (var target in targets)
                {
                    if (target.isActiveAndEnabled && !target.IsDead)
                    {
                        target.TakeDamage(100000f, target.transform.position + Vector3.up, HitboxType.Head);
                    }
                }
                yield return new WaitForSecondsRealtime(.8f);
            }
            Note("任务自动鸣钟 " + mission.GanadoKills + "/" + mission.BellKillTarget);
        }
        else
        {
            bell.Begin(match,ai,()=>Note("钟响完成回调"));
        }
        while (!VillageBellSequence.IsPresenting) { yield return null; }
        Note("钟响切镜");
        segment="钟响"; lastEvent=0;
        while (VillageBellSequence.IsPresenting)
        {
            if (interrupt && bd.time > 12.3)
            {
                bell.enabled = false;
                yield return null;
                yield return null;
                var stageRestored = !VillageBellSequence.IsPresenting && !cinematic.enabled && rig.MainCamera.enabled && match.Permission.Current.AllowInput;
                var renderRestored = Mathf.Approximately(far, cinematic.farClipPlane) && cinematic.useOcclusionCulling == occlusion && output.puddle.enabled == puddle && RenderSettings.fogColor == fog && Mathf.Approximately(RenderSettings.fogEndDistance, cutoff);
                var mirrors = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(c => c.name == "村庄过场水面反射");
                Note("水面镜头中断后恢复 状态=" + stageRestored + " 渲染=" + renderRestored + " 遗留镜像=" + mirrors);
                if (stageRestored && renderRestored && mirrors == 0)
                {
                    End("完成", 0);
                }
                else
                {
                    End("中断恢复失败", 6);
                }
                yield break;
            }
            if(lastEvent==0 && bd.time>1.2) { Photo("钟响-切入");lastEvent++; }
            else if(lastEvent==1 && bd.time>12) { Photo("钟响-水面");lastEvent++; }
            else if(lastEvent==2 && bd.time>72) { Photo("钟响-广场");lastEvent++; }
            else if(lastEvent==3 && bd.time>82) { Photo("钟响-切出");lastEvent++; }
            yield return null;
        }
        if (viaRules)
        {
            Note("任务结算=" + mission.IsCompleted + " 对局终态=" + match.IsTerminal);
            if (!mission.IsCompleted || !match.IsTerminal)
            {
                End("任务结算异常", 7);
                yield break;
            }
        }
        Photo("钟响-交还");
        yield return new WaitForSecondsRealtime(.6f);
        End("完成",0);
    }

    private void Place(Vector3 pos, Vector3 forward)
    {
        if (Physics.Raycast(pos + Vector3.up*.75f,Vector3.down,out var hit,2.4f,1,QueryTriggerInteraction.Ignore)) { pos=hit.point+Vector3.up*.07f; }
        forward.y=0;
        var rotation=Quaternion.LookRotation(forward);
        var body=leon.GetComponent<Rigidbody>();
        body.position=pos;
        body.rotation=rotation;
        body.linearVelocity=Vector3.zero;
        leon.transform.SetPositionAndRotation(pos,rotation);
        rig.gameplayDriver.SnapAfterVillageEntrance(rotation);
        Physics.SyncTransforms();
    }
}
#endif

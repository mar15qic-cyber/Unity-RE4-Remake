using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class VillageChickenAudio : MonoBehaviour
{
    public AudioEventData idleVoice;
    public AudioEventData walkVoice;
    public AudioEventData fleeVoice;
    public AudioEventData peck;
    public AudioEventData footstep;

    private static readonly int Walk = Animator.StringToHash("Walk");
    private static readonly int Flee = Animator.StringToHash("Flee");
    private static readonly int Peck = Animator.StringToHash("Peck");
    private static readonly int[] WalkFrames = { 17, 51, 87, 118, 157, 192, 228, 263 };
    private static readonly int[] RunFrames = { 8, 17, 29, 40, 51, 63, 74, 85 };
    private static readonly int[] PeckFrames = { 15, 44, 58, 81, 315, 343, 358, 395, 408, 432, 446, 566 };

    private Animator animator;
    private int previousState;
    private float previousFrame;
    private float nextVoice;
    private AudioPlayHandle voice;

    private void Awake() => animator = GetComponent<Animator>();

    private void LateUpdate()
    {
        if (!AudioManager.HasInstance || AudioManager.Instance.IsAudioPaused || Time.timeScale <= 0f ||
            !animator.isActiveAndEnabled) return;
        var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        int id = state.shortNameHash;
        int length = id == Walk ? 280 : id == Flee ? 90 : id == Peck ? 575 : 1605;
        float frame = state.normalizedTime * length;
        if (previousState != id || frame < previousFrame)
        {
            StopVoice();
            previousState = id;
            previousFrame = -0.001f;
            nextVoice = Time.time;
        }
        var audio = AudioManager.Instance;
        if (voice.IsValid) voice.source.transform.position = transform.position;
        if (Time.time >= nextVoice && !voice.IsValid)
        {
            // Wwise 随机容器按触发间隔重选片段，片段本身不做无缝循环。
            var cue = id == Flee ? fleeVoice : id == Walk ? walkVoice : idleVoice;
            if (cue != null) voice = audio.Play3D(cue, transform.position);
            nextVoice = Time.time + (id == Flee ? Random.Range(.5f, 1f) :
                id == Walk ? Random.Range(.2f, 2.2f) : Random.Range(.2f, 3.2f));
        }
        int[] marks = id == Walk ? WalkFrames : id == Flee ? RunFrames : id == Peck ? PeckFrames : null;
        var contact = id == Peck ? peck : footstep;
        if (marks != null && contact != null)
        {
            foreach (int marker in marks)
            {
                int cycle = Mathf.FloorToInt((frame - marker + .0001f) / length);
                if (cycle >= 0 && cycle * length + marker > previousFrame + .0001f)
                    audio.Play3D(contact, transform.position);
            }
        }
        previousFrame = frame;
    }

    private void StopVoice()
    {
        if (voice.IsValid && AudioManager.HasInstance) AudioManager.Instance.Stop(voice);
        voice = default;
    }

    private void OnDisable()
    {
        StopVoice();
        previousState = 0;
    }
}

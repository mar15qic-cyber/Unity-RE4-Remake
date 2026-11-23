using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>使用标题场景中的村庄布景实时拍摄广场，菜单与镜头独立更新。</summary>
public sealed class TitleVillagePan : MonoBehaviour
{
    private const float ShotSeconds = 38.04f;
    private const float FadeSeconds = 0.65f;
    private static readonly Vector3 StartPosition = new Vector3(4.34f, 2.29f, -0.57f);
    private static readonly Vector3 EndPosition = new Vector3(3.93f, 2.01f, 0.37f);
    private Camera shot;
    private RawImage picture;
    private float elapsed;

    public void Begin(RawImage surface)
    {
        shot = GetComponentInChildren<Camera>(true);
        picture = surface;
        if (gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
        {
            ApplyLighting();
        }
        // 场景直接输出到屏幕，避免延迟缓冲的透明通道再次参与界面混合。
        picture.texture = Texture2D.whiteTexture;
        picture.color = Color.clear;
        shot.targetTexture = null;
        shot.ResetAspect();
        ApplyPose(0f);
        shot.enabled = true;
    }

    private void ApplyLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.578f, .563f, .536f);
        RenderSettings.ambientEquatorColor = new Color(.443f, .423f, .393f);
        RenderSettings.ambientGroundColor = new Color(.238f, .221f, .202f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(.554f, .526f, .488f);
        RenderSettings.fogStartDistance = 42f;
        RenderSettings.fogEndDistance = 208f;
    }

    private void LateUpdate()
    {
        if (shot == null)
        {
            return;
        }
        elapsed = Mathf.Repeat(elapsed + Time.unscaledDeltaTime, ShotSeconds + FadeSeconds * 2f);
        float brightness = 1f;
        if (elapsed >= ShotSeconds + FadeSeconds)
        {
            ApplyPose(0f);
            brightness = (elapsed - ShotSeconds - FadeSeconds) / FadeSeconds;
        }
        else
        {
            ApplyPose(Mathf.Clamp01(elapsed / ShotSeconds));
            if (elapsed > ShotSeconds)
            {
                brightness = 1f - (elapsed - ShotSeconds) / FadeSeconds;
            }
        }
        picture.color = new Color(0f, 0f, 0f, 1f - brightness);
    }

    private void ApplyPose(float t)
    {
        Vector3 position = Vector3.Lerp(StartPosition, EndPosition, t);
        Quaternion rotation = Quaternion.Euler(-Mathf.Lerp(3.40f, 4.33f, t), Mathf.Lerp(-103.72f, -78.77f, t), 0f);
        shot.transform.SetPositionAndRotation(position, rotation);
    }

}

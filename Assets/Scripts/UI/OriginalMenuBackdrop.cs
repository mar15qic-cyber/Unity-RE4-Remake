using UnityEngine;
using UnityEngine.UI;

// 菜单只保留一张场景快照，不持续渲染已暂停的玩法。
public sealed class OriginalMenuBackdrop : MonoBehaviour
{
    private RenderTexture snapshot;

    public static void Show(Transform parent, Material material, Camera source)
    {
        if (parent == null || material == null || source == null)
        {
            return;
        }
        var child = parent.Find("原版菜单场景背景");
        if (child == null)
        {
            child = new GameObject("原版菜单场景背景", typeof(RectTransform), typeof(RawImage),
                typeof(OriginalMenuBackdrop)).transform;
            child.SetParent(parent, false);
            child.SetAsFirstSibling();
            var rect = (RectTransform)child;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
        child.GetComponent<OriginalMenuBackdrop>().Capture(source, material);
    }

    private void Capture(Camera source, Material material)
    {
        if (snapshot == null)
        {
            int height = Mathf.Max(128, Mathf.RoundToInt(640f / source.aspect));
            snapshot = new RenderTexture(640, height, 24);
            snapshot.name = "菜单场景快照";
            snapshot.filterMode = FilterMode.Bilinear;
            snapshot.Create();
        }
        var previous = source.targetTexture;
        try
        {
            source.targetTexture = snapshot;
            source.Render();
        }
        finally
        {
            source.targetTexture = previous;
        }
        var image = GetComponent<RawImage>();
        image.texture = snapshot;
        image.material = material;
        image.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (snapshot != null)
        {
            snapshot.Release();
            Destroy(snapshot);
        }
    }
}

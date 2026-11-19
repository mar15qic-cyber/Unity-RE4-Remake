using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

internal sealed class ChapterLoadingView : MonoBehaviour
{
    private static readonly Color TextColor = new Color(.85f, .83f, .79f);
    private static readonly Vector2 ChapterAnchor = new Vector2(.10f, .725f);
    private static readonly Vector2 ProgressAnchor = new Vector2(.10f, .147f);

    private static readonly (string Heading, string Body)[] Pages =
    {
        ("第 1 章", "2004 年，秋。\n美国特工里昂·S·肯尼迪奉命寻找遭绑架的总统之女。线索把他带到欧洲的一座偏僻村庄。浣熊市的记忆仍未散去，一场新的任务已经开始。"),
        ("村庄入口", "走到铁门前，继续向前推开铁门。穿过入口、走进村庄后，村民才会开始追击。"),
        ("木箱与补给", "靠近带黄色标记的箱子，按交互提示破坏木箱。拾取散落的弹药和药草，为接下来的战斗做准备。"),
        ("通行路线", "利用窗户、楼梯和外平台甩开追击。村民也会沿木梯攀上二楼，从上方推倒梯子可以争取时间。"),
        ("小刀弹反", "看到弹反提示时按下防守键。越接近攻击命中的时刻，越容易触发完美弹反。留意小刀的耐久。")
    };

    private readonly Text[] pageIndicators = new Text[Pages.Length];
    private Text heading;
    private Text body;
    private Text previousPage;
    private Text nextPage;
    private RectTransform progressLine;
    private RectTransform progressFill;
    private RectTransform continueGlow;
    private GameObject continueButton;
    private Image blackout;
    private Texture2D shadeTexture;
    private int pageIndex;
    private bool ready;
    private bool confirmReleased;
    private float targetProgress;
    private float displayedProgress;
    private float readyAt;

    internal bool Confirmed { get; private set; }

    internal static ChapterLoadingView Create(Transform parent, Texture2D photograph, Font font)
    {
        RectTransform root = CreateRect("章节加载界面", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        Stretch(root);
        var view = root.gameObject.AddComponent<ChapterLoadingView>();
        view.Build(photograph, font);
        return view;
    }

    internal static RectTransform CreateBackdrop(Transform parent, string objectName, Texture2D photograph)
    {
        RectTransform rect = CreateRect(objectName, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        Stretch(rect);
        var frame = rect.gameObject.AddComponent<RectMask2D>();

        RectTransform picture = CreateRect("村庄取景", frame.transform, new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        var image = picture.gameObject.AddComponent<RawImage>();
        image.texture = photograph;
        image.raycastTarget = false;
        if (photograph == null)
        {
            image.color = Color.black;
            Stretch(picture);
            return rect;
        }

        var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = (float)photograph.width / photograph.height;
        return rect;
    }

    private void Build(Texture2D photograph, Font font)
    {
        CreateBackdrop(transform, "村庄广场", photograph);
        shadeTexture = AddBackdropShade(transform, .99f, .64f, .24f);
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        heading = CreateText("章节标题", transform, font, "", 26, ChapterAnchor,
            Vector2.zero, new Vector2(430f, 36f), TextAnchor.UpperLeft);
        body = CreateText("章节简介", transform, font, "", 23, ChapterAnchor,
            new Vector2(0f, -55f), new Vector2(400f, 208f), TextAnchor.UpperLeft);
        body.lineSpacing = 1.2f;
        previousPage = CreatePageButton("上一页", "‹", font, 4f, () => ChangePage(-1));
        nextPage = CreatePageButton("下一页", "›", font, 232f, () => ChangePage(1));
        for (int i = 0; i < pageIndicators.Length; i++)
        {
            int index = i;
            pageIndicators[i] = CreatePageButton("选择章节提示", "•", font, 38f + i * 40f, () => ShowPage(index));
        }

        ShowPage(0);
        progressLine = CreateImage("加载进度线", transform, new Color(1f, 1f, 1f, .24f),
            ProgressAnchor, new Vector2(0f, .5f), Vector2.zero, new Vector2(252f, 2f));
        progressFill = CreateImage("加载进度", progressLine, TextColor, new Vector2(0f, .5f),
            new Vector2(0f, .5f), Vector2.zero, new Vector2(0f, 3f));

        RectTransform confirm = CreateImage("继续按钮", transform, new Color(1f, 1f, 1f, 0f),
            ProgressAnchor, Vector2.zero, Vector2.zero, new Vector2(150f, 44f));
        AddButton(confirm, Confirm);
        continueButton = confirm.gameObject;
        RectTransform key = CreateImage("原版空格键", confirm, Color.white,
            new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(0f, 1f), new Vector2(42f, 42f));
        var keyImage = key.GetComponent<Image>();
        keyImage.sprite = Resources.Load<Sprite>("NativeInteractionArt/Key_Space");
        keyImage.preserveAspect = true;
        keyImage.raycastTarget = false;
        if (keyImage.sprite == null)
        {
            keyImage.enabled = false;
        }
        Text continueLabel = CreateText("继续提示", confirm, font, "继续", 23, new Vector2(0f, .5f),
            new Vector2(46f, 1f), new Vector2(92f, 36f), TextAnchor.MiddleLeft);
        continueLabel.rectTransform.pivot = new Vector2(0f, .5f);
        continueLabel.verticalOverflow = VerticalWrapMode.Overflow;
        CreateImage("继续下划线底色", confirm, new Color(1f, 1f, 1f, .22f), Vector2.zero, Vector2.zero,
            Vector2.zero, new Vector2(150f, 3f));
        var track = CreateRect("继续下划线", confirm, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(150f, 3f));
        track.gameObject.AddComponent<RectMask2D>();
        continueGlow = CreateImage("继续进度光带", track, TextColor, Vector2.zero, Vector2.zero,
            new Vector2(-82f, 0f), new Vector2(82f, 3f));
        continueButton.SetActive(false);

        RectTransform cover = CreateImage("进场淡黑", transform, new Color(0f, 0f, 0f, 0f),
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        Stretch(cover);
        blackout = cover.GetComponent<Image>();
    }

    internal static Texture2D AddBackdropShade(Transform parent, float leftOpacity, float edgeOpacity, float overallOpacity)
    {
        var rect = CreateRect("左侧遮光与暗角", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        Stretch(rect);
        var overlay = rect.gameObject.AddComponent<RawImage>();
        const int width = 128;
        const int height = 32;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        var colors = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);
                float left = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.32f, .66f, u));
                float edges = Mathf.Pow(Mathf.Abs(v - .64f) * 1.56f, 2f) * edgeOpacity;
                float shade = 1f - (1f - left * leftOpacity) * (1f - edges) * (1f - overallOpacity);
                colors[y * width + x] = new Color(0f, 0f, 0f, shade);
            }
        }

        texture.SetPixels(colors);
        texture.Apply(false, true);
        overlay.texture = texture;
        overlay.raycastTarget = false;
        return texture;
    }

    private Text CreatePageButton(string objectName, string label, Font font, float x, UnityEngine.Events.UnityAction click)
    {
        RectTransform target = CreateImage(objectName, transform, new Color(1f, 1f, 1f, 0f),
            ChapterAnchor, new Vector2(.5f, .5f), new Vector2(x, -284f), new Vector2(36f, 40f));
        AddButton(target, click);
        return CreateText("分页标记", target, font, label, 25, Vector2.up, Vector2.zero,
            new Vector2(36f, 40f), TextAnchor.MiddleCenter);
    }

    private static void AddButton(RectTransform target, UnityEngine.Events.UnityAction click)
    {
        Image image = target.GetComponent<Image>();
        image.raycastTarget = true;
        var button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(click);
    }

    private void ShowPage(int index)
    {
        pageIndex = index;
        heading.text = Pages[index].Heading;
        body.text = Pages[index].Body;
        for (int i = 0; i < pageIndicators.Length; i++)
        {
            float opacity = .36f;
            if (i == index)
            {
                opacity = 1f;
            }

            pageIndicators[i].color = new Color(TextColor.r, TextColor.g, TextColor.b, opacity);
        }

        previousPage.color = new Color(TextColor.r, TextColor.g, TextColor.b, .56f);
        nextPage.color = previousPage.color;
    }

    private void ChangePage(int direction)
    {
        ShowPage((pageIndex + direction + Pages.Length) % Pages.Length);
    }

    internal void SetProgress(float progress)
    {
        targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(progress));
    }

    internal void ShowContinue()
    {
        displayedProgress = 1f;
        progressFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 252f);
        progressLine.gameObject.SetActive(false);
        ready = true;
        readyAt = Time.unscaledTime;
        continueButton.SetActive(true);
        confirmReleased = !IsConfirmHeld();
    }

    private void Update()
    {
        if (Confirmed)
        {
            return;
        }

        if (ready)
        {
            float phase = Mathf.Repeat((Time.unscaledTime - readyAt) / 1.35f, 1f);
            continueGlow.anchoredPosition = new Vector2(Mathf.Lerp(-82f, 150f, phase), 0f);
        }
        else
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, Time.unscaledDeltaTime * 1.6f);
            progressFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 252f * displayedProgress);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame))
        {
            ChangePage(-1);
        }
        else if (keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame))
        {
            ChangePage(1);
        }

        if (!ready)
        {
            return;
        }

        if (!IsConfirmHeld())
        {
            confirmReleased = true;
        }

        if (confirmReleased && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
            keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame)))
        {
            Confirm();
        }
    }

    private static bool IsConfirmHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return) ||
            keyboard != null && (keyboard.spaceKey.isPressed || keyboard.enterKey.isPressed);
    }

    private void Confirm()
    {
        Confirmed = ready;
    }

    internal IEnumerator FadeToBlack()
    {
        blackout.raycastTarget = true;
        const float duration = .26f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            blackout.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (shadeTexture != null)
        {
            Destroy(shadeTexture);
        }
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(objectName, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform CreateImage(string objectName, Transform parent, Color color, Vector2 anchor,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rect = CreateRect(objectName, parent, anchor, pivot, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static Text CreateText(string objectName, Transform parent, Font font, string value, int size,
        Vector2 anchor, Vector2 position, Vector2 bounds, TextAnchor alignment)
    {
        RectTransform rect = CreateRect(objectName, parent, anchor, Vector2.up, position, bounds);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = TextColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(10210)]
public sealed class NativeInteractionPromptView : MonoBehaviour
{
    [Header("正式玩家与原版提示字体")]
    public LeonQTECoordinator coordinator;
    public LeonInputBridge input;
    public Font font;
    private Canvas canvas;
    private PromptCard main;
    private PromptCard secondary;
    private PromptCard far;
    private PromptCard urgent;
    private PromptCard grabManual;
    private PromptCard grabKnife;
    private Image grabBackdrop;
    private Image grabRingFlare;
    private Image grabProgress;
    private Image grabRingTrack;
    private Image urgentGlow;
    private readonly List<PromptCard> pickupMarkers = new List<PromptCard>();
    private readonly List<IInteractable> interactables = new List<IInteractable>();
    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    private readonly Dictionary<string, Sprite> bindingArt = new Dictionary<string, Sprite>();
    private readonly Dictionary<string, string> bindingLabels = new Dictionary<string, string>();

    private void OnEnable()
    {
        InputSystem.onActionChange += HandleActionChange;
        ClearBindingArt();
    }

    private void HandleActionChange(object action, InputActionChange change)
    {
        if (change == InputActionChange.BoundControlsChanged)
        {
            ClearBindingArt();
        }
    }

    private void ClearBindingArt()
    {
        bindingArt.Clear();
        bindingLabels.Clear();
    }

    private sealed class PromptCard
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image key;
        public Image icon;
        public Image durabilityTrack;
        public Image durability;
        public Image arrow;
        public Image secondArrow;
        public Image background;
        public Image glowBackdrop;
        public Text label;
        public Text fallbackKey;
        public RectTransform holdGroup;
        public Image holdFill;
        public Component target;
        public bool wasNear;
        public int presentedFrame = -1;
        public ItemData labelItem;
        public int labelItemCount;
        public string labelItemName;
        public string pickupLabel;
        public float appearedAt;
    }

    private void Start()
    {
        InitializePresentation();
    }

    private void InitializePresentation()
    {
        if (coordinator == null)
        {
            coordinator = GetComponent<LeonQTECoordinator>();
        }
        if (input == null)
        {
            input = GetComponent<LeonInputBridge>();
        }
        if (coordinator == null || input == null || !coordinator.UsesNativeInteractionPrompts)
        {
            enabled = false;
            return;
        }
        foreach (QTEPromptView view in FindObjectsByType<QTEPromptView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (view.coordinator == coordinator)
            {
                if (font == null && view.keyLabel != null)
                {
                    font = view.keyLabel.font;
                }
                view.enabled = false;
                if (view.rootGroup != null)
                {
                    view.rootGroup.alpha = 0f;
                }
            }
        }
        foreach (WorldInteractionPromptUI view in FindObjectsByType<WorldInteractionPromptUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (font == null && view.promptText != null)
            {
                font = view.promptText.font;
            }
            view.StopAllCoroutines();
            view.enabled = false;
            if (view.promptGroup != null)
            {
                view.promptGroup.alpha = 0f;
            }
        }
        ReleasePresentation();
        var root = new GameObject("原版交互提示", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        main = CreateCard("交互卡片");
        secondary = CreateCard("同时可执行的交互");
        far = CreateCard("远端机会");
        urgent = CreateCard("紧急格挡");
        BuildUrgentGlow();
        grabManual = CreateCard("正面擒抱挣脱");
        grabKnife = CreateCard("正面擒抱反制");
        BuildGrabRing();
        StyleGrabText(main);
        StyleGrabText(secondary);
        StyleGrabText(grabManual);
        StyleGrabText(grabKnife);
        StyleGrabText(urgent);
    }

    private void OnDisable()
    {
        InputSystem.onActionChange -= HandleActionChange;
        ReleasePresentation();
    }

    private void ReleasePresentation()
    {
        if (canvas != null)
        {
            canvas.gameObject.SetActive(false);
            Destroy(canvas.gameObject);
        }
        canvas = null;
        main = null;
        secondary = null;
        far = null;
        urgent = null;
        urgentGlow = null;
        grabManual = null;
        grabKnife = null;
        grabBackdrop = null;
        grabRingFlare = null;
        grabProgress = null;
        grabRingTrack = null;
        pickupMarkers.Clear();
    }

    private Sprite Art(string name)
    {
        if (!sprites.TryGetValue(name, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>("NativeInteractionArt/" + name);
            sprites[name] = sprite;
        }
        return sprite;
    }

    private PromptCard CreateCard(string name)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvas.transform, false);
        var card = new PromptCard { root = root.GetComponent<RectTransform>(), group = root.GetComponent<CanvasGroup>() };
        card.root.sizeDelta = new Vector2(300f, 60f);
        card.group.interactable = false;
        card.group.blocksRaycasts = false;
        card.background = Image(card.root, "原版背景", new Vector2(36f, 0f), new Vector2(240f, 34f));
        card.background.sprite = Art("Background");
        card.background.preserveAspect = false;
        card.glowBackdrop = Image(card.root, "柔光光晕底", new Vector2(-96f, 0f), new Vector2(74f, 74f));
        card.glowBackdrop.sprite = Art("GrabGlowBackdrop");
        card.glowBackdrop.color = new Color32(242, 238, 228, 150);
        card.glowBackdrop.enabled = false;
        card.key = Image(card.root, "按键", new Vector2(-96f, 0f), new Vector2(52f, 52f));
        card.icon = Image(card.root, "图标", new Vector2(-50f, 0f), new Vector2(32f, 32f));
        card.durabilityTrack = Image(card.root, "刀具耐久底槽", new Vector2(-50f, -23f), new Vector2(42f, 5f));
        card.durabilityTrack.sprite = Art("KnifeDurabilityTrack");
        card.durabilityTrack.preserveAspect = false;
        card.durabilityTrack.enabled = false;
        card.durability = Image(card.root, "刀具耐久", new Vector2(-69f, -23f), new Vector2(38f, 3f));
        card.durability.sprite = Art("KnifeDurabilityFill");
        card.durability.rectTransform.pivot = new Vector2(0f, .5f);
        card.durability.preserveAspect = false;
        card.durability.enabled = false;
        card.arrow = Image(card.root, "下标", new Vector2(-96f, -35f), new Vector2(22f, 20f));
        card.arrow.sprite = Art("Arrow");
        card.secondArrow = Image(card.root, "第二下标", new Vector2(-96f, -27f), new Vector2(22f, 20f));
        card.secondArrow.sprite = Art("Arrow");
        card.label = Label(card.root, "内容", new Vector2(70f, 0f), new Vector2(230f, 38f), 24);
        card.fallbackKey = Label(card.root, "改键文字", new Vector2(-96f, 0f), new Vector2(90f, 40f), 20);
        card.fallbackKey.alignment = TextAnchor.MiddleCenter;
        card.group.alpha = 0f;
        return card;
    }

    private static Image Image(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private void ShowCabinetHold(PromptCard card, float progress)
    {
        if (card.holdGroup == null)
        {
            var group = new GameObject("柜体长按提示", typeof(RectTransform));
            card.holdGroup = group.GetComponent<RectTransform>();
            card.holdGroup.SetParent(card.root, false);
            card.holdGroup.anchoredPosition = new Vector2(-96f, 0f);
            // cs_ui0350 的底环和亮环共用一张原版图，十二点起顺时针揭示亮环。
            Image flare = Image(card.holdGroup, "环形阴影", Vector2.zero, new Vector2(60f, 60f));
            flare.sprite = Art("CabinetHoldFlare");
            flare.color = new Color32(0, 0, 0, 204);
            Image track = Image(card.holdGroup, "底环", Vector2.zero, new Vector2(48f, 48f));
            track.sprite = Art("CabinetHoldGauge");
            track.color = new Color32(75, 75, 70, 204);
            card.holdFill = Image(card.holdGroup, "长按进度", Vector2.zero, new Vector2(48f, 48f));
            card.holdFill.sprite = track.sprite;
            card.holdFill.type = UnityEngine.UI.Image.Type.Filled;
            card.holdFill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            card.holdFill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            card.holdFill.fillClockwise = true;
            card.holdFill.color = new Color32(250, 240, 230, 255);
            Text hold = Label(card.holdGroup, "长按标识", new Vector2(0f, 38f), new Vector2(110f, 29f), 18);
            hold.text = "长按";
            hold.alignment = TextAnchor.MiddleCenter;
            hold.color = new Color32(255, 250, 240, 255);
            card.holdGroup.SetSiblingIndex(card.key.transform.GetSiblingIndex());
        }
        card.holdGroup.gameObject.SetActive(true);
        card.holdFill.fillAmount = progress;
        card.background.enabled = false;
        card.label.enabled = false;
        card.key.rectTransform.sizeDelta = new Vector2(42f, 42f);
    }

    private Text Label(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = font;
        text.fontSize = fontSize;
        text.color = new Color32(215, 210, 200, 255);
        text.alignment = TextAnchor.MiddleLeft;
        text.raycastTarget = false;
        return text;
    }

    private void LateUpdate()
    {
        if (main == null)
        {
            InitializePresentation();
        }
        if (canvas == null || coordinator == null || input == null)
        {
            return;
        }
        mainTarget = null;
        PresentCurrentPrompts();
        HideUnused(main);
        HideUnused(secondary);
        HideUnused(far);
        HideUnused(urgent);
        HideUnused(grabManual);
        HideUnused(grabKnife);
        foreach (PromptCard marker in pickupMarkers)
        {
            HideUnused(marker);
        }
    }

    private static void HideUnused(PromptCard card)
    {
        if (card.presentedFrame != Time.frameCount && card.group.alpha != 0f)
        {
            card.group.alpha = 0f;
        }
    }

    private static void ShowCard(PromptCard card)
    {
        card.presentedFrame = Time.frameCount;
        if (card.group.alpha != 1f)
        {
            card.group.alpha = 1f;
        }
    }

    private void PresentCurrentPrompts()
    {
        if (Time.timeScale <= 0f ||
            (GameUIManager.HasInstance && GameUIManager.Instance.IsMissionResultShowing) ||
            (GameManager.HasInstance && (GameManager.Instance.IsTerminal ||
                (!GameManager.Instance.Permission.Current.AllowInput && !coordinator.IsActive))) ||
            (GameUIManager.HasInstance && (GameUIManager.Instance.HasBlockingModal
                || !coordinator.IsActive && !GameUIManager.Instance.CanPlayerAct())))
        {
            return;
        }
        Camera camera = coordinator.ReadPromptCamera();
        if (camera == null)
        {
            return;
        }
        if (coordinator.IsActive)
        {
            if (coordinator.IsFrontGrabActive)
            {
                PresentFrontGrab(camera);
                return;
            }
            if (coordinator.TryReadEscapeRingPrompt(out float ringProgress, out bool ringUrgent, out string ringText))
            {
                PresentEscapeRing(ringProgress, ringUrgent, ringText);
                return;
            }
            if (coordinator.CurrentPhase == QTEPhase.WaitingForInput
                && coordinator.TryGetPromptPresentation(out Transform target, out string text))
            {
                Present(main, new InteractionPromptSnapshot(target, default, InteractionPromptKind.Interact, true,
                    "EscapePress", text, target.position + Vector3.up * 1.5f), camera);
            }
            return;
        }
        coordinator.TryReadInteractionPrompts(out var interact, out var finisher, out var distant);
        if (finisher.IsValid)
        {
            Present(main, finisher, camera);
        }
        if (interact.IsValid)
        {
            PromptCard interactCard = main;
            if (finisher.IsValid)
            {
                interactCard = secondary;
            }
            Present(interactCard, interact, camera);
        }
        if (distant.IsValid && distant.Target != mainTarget && distant.Target != interact.Target)
        {
            Present(far, distant, camera);
        }
        if (coordinator.TryGetChainsawWarning(out var enemy, out var warning))
        {
            PresentChainsawWarning(camera);
        }
        InteractionSystem.CopyRegistered(interactables);
        int count = 0;
        foreach (IInteractable candidate in interactables)
        {
            if (candidate is not ItemPickup pickup || pickup == interact.Target || pickup == mainTarget
                || Vector3.Distance(transform.position, pickup.transform.position) > 10f)
            {
                continue;
            }
            Vector3 position = pickup.transform.position + Vector3.up * .25f;
            if (!LeonQTECoordinator.IsPromptVisible(camera, pickup, position, transform))
            {
                continue;
            }
            if (count == pickupMarkers.Count)
            {
                pickupMarkers.Add(CreateCard("拾取简略标记"));
            }
            Present(pickupMarkers[count], new InteractionPromptSnapshot(pickup, default, InteractionPromptKind.Interact,
                false, "", "", position), camera);
            count++;
        }
    }

    private Component mainTarget;

    private void BuildUrgentGlow()
    {
        urgentGlow = Image(urgent.root, "紧急红光晕", new Vector2(-96f, 0f), new Vector2(118f, 118f));
        urgentGlow.sprite = Art("UrgentRedGlow");
        urgentGlow.color = new Color32(255, 48, 48, 235);
        urgentGlow.enabled = false;
        urgentGlow.transform.SetAsFirstSibling();
    }

    private void BuildGrabRing()
    {
        Sprite gaugeSprite = Art("CabinetHoldGauge");
        grabBackdrop = Image(grabManual.root, "挣脱光晕底盘", new Vector2(-96f, 0f), new Vector2(88f, 88f));
        grabBackdrop.sprite = Art("GrabGlowBackdrop");
        grabBackdrop.color = new Color32(235, 228, 215, 200);
        grabRingFlare = Image(grabManual.root, "挣脱暗影圈", new Vector2(-96f, 0f), new Vector2(68f, 68f));
        grabRingFlare.sprite = Art("CabinetHoldFlare");
        grabRingFlare.color = new Color32(0, 0, 0, 175);
        grabRingTrack = Image(grabManual.root, "挣脱环", new Vector2(-96f, 0f), new Vector2(64f, 64f));
        grabRingTrack.sprite = gaugeSprite;
        grabProgress = Image(grabManual.root, "挣脱进度", new Vector2(-96f, 0f), new Vector2(64f, 64f));
        grabProgress.sprite = gaugeSprite;
        grabProgress.type = UnityEngine.UI.Image.Type.Filled;
        grabProgress.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        grabProgress.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        grabProgress.fillClockwise = true;
        int keySibling = grabManual.key.transform.GetSiblingIndex();
        grabBackdrop.transform.SetSiblingIndex(keySibling);
        grabRingFlare.transform.SetSiblingIndex(keySibling + 1);
        grabRingTrack.transform.SetSiblingIndex(keySibling + 2);
        grabProgress.transform.SetSiblingIndex(keySibling + 3);
    }

    private void PresentEscapeRing(float progress, bool urgentNow, string text)
    {
        var space = (RectTransform)canvas.transform;
        Vector2 manualPosition = new Vector2(25f, -space.rect.height * .26f);
        PresentGrabCard(grabManual, manualPosition, "EscapePress", text, false, true);
        grabProgress.fillAmount = progress;
        grabProgress.color = new Color32(226, 56, 48, 255);
        if (urgentNow)
        {
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 18f);
            byte trackAlpha = (byte)Mathf.RoundToInt(Mathf.Lerp(150f, 220f, pulse));
            grabRingTrack.color = new Color32(232, 66, 58, trackAlpha);
            grabBackdrop.color = new Color32(255, 195, 190, 215);
            grabManual.label.color = new Color32(255, 220, 210, 255);
        }
        else
        {
            grabRingTrack.color = new Color32(105, 98, 90, 175);
            grabBackdrop.color = new Color32(235, 228, 215, 200);
            grabManual.label.color = new Color32(245, 240, 225, 255);
        }
    }

    private void PresentFrontGrab(Camera camera)
    {
        if (!coordinator.TryReadFrontGrabPrompt(out float progress, out bool urgentNow, out var knife, out var target))
        {
            return;
        }

        PresentEscapeRing(progress, urgentNow, "挣脱");
        if (knife == null)
        {
            return;
        }

        var space = (RectTransform)canvas.transform;
        var projected = camera.WorldToScreenPoint(target.position + Vector3.up * 1.44f);
        Vector2 sidePosition = new Vector2(space.rect.width * .24f, space.rect.height * .12f);
        if (projected.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(space, projected, null, out var local))
        {
            sidePosition.x = Mathf.Clamp(local.x + 220f, space.rect.width * .28f, space.rect.xMax - 90f);
            sidePosition.y = Mathf.Clamp(local.y + 58f, -space.rect.height * .02f, space.rect.height * .36f);
        }
        PresentGrabCard(grabKnife, sidePosition, "KnifeEscape", string.Empty, true, false);
        grabKnife.key.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
        grabKnife.fallbackKey.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
        grabKnife.icon.rectTransform.anchoredPosition = new Vector2(-54f, 3f);
        grabKnife.icon.rectTransform.sizeDelta = new Vector2(38f, 38f);
        grabKnife.durabilityTrack.enabled = true;
        grabKnife.durabilityTrack.rectTransform.anchoredPosition = new Vector2(-54f, -19f);
        grabKnife.durabilityTrack.rectTransform.sizeDelta = new Vector2(34f, 5f);
        grabKnife.durability.enabled = knife.DurabilityNormalized > 0f;
        grabKnife.durability.rectTransform.anchoredPosition = new Vector2(-69f, -19f);
        grabKnife.durability.rectTransform.sizeDelta = new Vector2(30f * knife.DurabilityNormalized, 3f);
        grabKnife.durability.color = new Color32(242, 232, 210, 255);
    }

    private void PresentChainsawWarning(Camera camera)
    {
        var space = (RectTransform)canvas.transform;
        Vector3 worldAnchor = transform.position + Vector3.up * 1.42f + camera.transform.right * 0.34f;
        Vector3 screen = camera.WorldToScreenPoint(worldAnchor);
        Vector2 localPosition = new Vector2(90f, 36f);
        if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, null, out var projected))
        {
            localPosition.x = Mathf.Clamp(projected.x + 76f, space.rect.xMin + 130f, space.rect.xMax - 90f);
            localPosition.y = Mathf.Clamp(projected.y + 12f, space.rect.yMin + 70f, space.rect.yMax - 70f);
        }

        urgent.root.anchoredPosition = localPosition;
        ShowCard(urgent);
        urgent.background.enabled = false;
        urgent.arrow.enabled = false;
        urgent.secondArrow.enabled = false;
        urgent.label.enabled = false;
        urgent.durabilityTrack.enabled = false;
        urgent.durability.enabled = false;

        if (urgentGlow != null)
        {
            urgentGlow.enabled = true;
            urgentGlow.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
            float pulseScale = 1f + 0.11f * Mathf.Sin(Time.unscaledTime * 22f);
            urgentGlow.rectTransform.sizeDelta = new Vector2(118f * pulseScale, 118f * pulseScale);
            float alphaWave = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 22f);
            urgentGlow.color = new Color(1f, 0.16f, 0.16f, alphaWave);
        }

        UpdateCardBinding(urgent, "Parry", true, new Vector2(56f, 44f));
        urgent.key.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
        urgent.fallbackKey.rectTransform.anchoredPosition = new Vector2(-96f, 0f);

        urgent.icon.sprite = Art("Knife");
        urgent.icon.enabled = urgent.icon.sprite != null;
        urgent.icon.rectTransform.anchoredPosition = new Vector2(-48f, 0f);
        urgent.icon.rectTransform.sizeDelta = new Vector2(40f, 40f);
    }

    private static void StyleGrabText(PromptCard card)
    {
        foreach (var graphic in new Graphic[] { card.label, card.fallbackKey, card.key, card.icon })
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(0, 0, 0, 198);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }
    }

    private void PresentGrabCard(PromptCard card, Vector2 position, string action, string text, bool knife, bool showLabel)
    {
        card.root.anchoredPosition = position;
        ShowCard(card);
        card.background.enabled = false;
        card.arrow.enabled = false;
        card.secondArrow.enabled = false;
        float keySize = 36f;
        if (knife)
        {
            keySize = 38f;
        }
        else
        {
            card.durabilityTrack.enabled = false;
            card.durability.enabled = false;
        }
        UpdateCardBinding(card, action, true, new Vector2(keySize, keySize));
        if (knife)
        {
            card.icon.sprite = Art("Knife");
        }
        else
        {
            card.icon.sprite = null;
        }
        card.icon.enabled = card.icon.sprite != null;
        card.label.enabled = showLabel;
        if (showLabel)
        {
            if (knife)
            {
                card.label.rectTransform.anchoredPosition = new Vector2(87f, 0f);
            }
            else
            {
                card.label.rectTransform.anchoredPosition = new Vector2(62f, 0f);
            }
            if (card.label.text != text)
            {
                card.label.text = text;
            }
        }
    }

    private void Present(PromptCard card, InteractionPromptSnapshot snapshot, Camera camera)
    {
        Vector3 screen = camera.WorldToScreenPoint(snapshot.Position);
        bool near = snapshot.CanExecute;
        var cabinet = snapshot.Target as WindowBarricade;
        bool groundFinisher = near && snapshot.Kind == InteractionPromptKind.GroundFinisher;
        bool bodyTechnique = near && snapshot.Kind == InteractionPromptKind.BodyTechnique;
        bool blockedFinisher = snapshot.Kind == InteractionPromptKind.GroundFinisherBlocked;
        if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height)
        {
            if (!groundFinisher && (cabinet == null || !cabinet.ShowsHoldPrompt) &&
                (!near || !LeonQTECoordinator.IsPassageInteraction(snapshot.Target)))
            {
                return;
            }
            screen = new Vector3(Screen.width * .5f, Screen.height * .35f, 1f);
        }
        if (card == main)
        {
            mainTarget = snapshot.Target;
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out Vector2 local);
        local += new Vector2(96f, 35f);
        if (card == secondary && Vector2.Distance(main.root.anchoredPosition, local) < 82f)
        {
            local += Vector2.up * 78f;
        }
        card.root.anchoredPosition = local;
        if (card.target != snapshot.Target || card.wasNear != near)
        {
            card.target = snapshot.Target;
            card.wasNear = near;
            card.appearedAt = Time.unscaledTime;
        }
        // cs_ui2020 的对话 ENTER 轨在六帧内从 50 展开到 240，颜色从 0 到 255。
        float reveal = Mathf.Clamp01((Time.unscaledTime - card.appearedAt) * 60f / 6f);
        card.background.enabled = near && snapshot.Kind != InteractionPromptKind.GroundFinisher && !bodyTechnique;
        card.background.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(50f, 240f, reveal), 34f);
        card.background.color = new Color(0f, 0f, 0f, reveal * .65f);
        if (card.glowBackdrop != null)
        {
            card.glowBackdrop.enabled = bodyTechnique;
            if (bodyTechnique)
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.0f);
                float pulse = Mathf.Lerp(0.985f, 1.015f, wave);
                byte glowAlpha = (byte)Mathf.RoundToInt(Mathf.Lerp(138f, 160f, wave));
                card.glowBackdrop.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
                card.glowBackdrop.rectTransform.sizeDelta = new Vector2(74f * pulse, 74f * pulse);
                card.glowBackdrop.color = new Color32(242, 238, 228, glowAlpha);
            }
        }
        bool showLabel = (near || blockedFinisher) && !groundFinisher;
        card.label.enabled = showLabel;
        card.arrow.enabled = !groundFinisher && snapshot.Kind != InteractionPromptKind.EmergencyDefense && !blockedFinisher;
        card.secondArrow.enabled = !near && !blockedFinisher;
        bool showHold = cabinet != null && near;
        if (card.holdGroup != null && card.holdGroup.gameObject.activeSelf != showHold)
        {
            card.holdGroup.gameObject.SetActive(showHold);
        }

        float keySize = 52f;
        Vector2 arrowPosition = new Vector2(-96f, -35f);
        Vector2 labelPosition = new Vector2(70f, 0f);
        Color32 labelColor = new Color32(215, 210, 200, 255);
        if (bodyTechnique)
        {
            keySize = 42f;
            arrowPosition.y = -34f;
            labelPosition.x = 64f;
            labelColor = new Color32(248, 244, 232, 255);
        }
        card.key.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
        card.fallbackKey.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
        card.arrow.rectTransform.anchoredPosition = arrowPosition;
        card.secondArrow.rectTransform.anchoredPosition = new Vector2(-96f, -27f);
        string content = snapshot.Content;
        Sprite icon = null;
        Vector2 iconPosition = new Vector2(-50f, 0f);
        Vector2 iconSize = new Vector2(32f, 32f);
        float durability = 0f;
        if (showHold)
        {
            ShowCabinetHold(card, cabinet.HoldProgress);
        }
        else if (blockedFinisher)
        {
            icon = Art("Knife");
            iconPosition.x = -96f;
            labelPosition.x = 2f;
        }
        else if (groundFinisher)
        {
            keySize = 40f;
            icon = Art("Knife");
            iconPosition = new Vector2(-52f, 2f);
            iconSize = new Vector2(44f, 44f);
            if (InventoryManager.HasInstance)
            {
                var knife = coordinator.ReadGroundPromptKnife();
                if (knife != null)
                {
                    durability = knife.DurabilityNormalized;
                }
            }
        }
        else if (near && snapshot.Target is ItemPickup pickup && pickup.item != null)
        {
            ItemData item = pickup.item;
            icon = item.icon;
            labelPosition.x = 96f;
            if (card.labelItem != item || card.labelItemCount != pickup.count || card.labelItemName != item.displayName)
            {
                card.labelItem = item;
                card.labelItemCount = pickup.count;
                card.labelItemName = item.displayName;
                card.pickupLabel = item.displayName + " ×" + pickup.count;
            }
            content = card.pickupLabel;
        }
        if (showLabel && card.label.text != content)
        {
            card.label.text = content;
        }
        card.label.rectTransform.anchoredPosition = labelPosition;
        card.label.color = labelColor;
        UpdateCardBinding(card, snapshot.InputAction, near, new Vector2(keySize, keySize));
        card.icon.enabled = icon != null;
        if (icon != null)
        {
            card.icon.sprite = icon;
            card.icon.rectTransform.anchoredPosition = iconPosition;
            SetSizeIfChanged(card.icon.rectTransform, iconSize);
        }

        bool showDurability = durability > 0f;
        card.durabilityTrack.enabled = showDurability;
        card.durability.enabled = showDurability;
        if (showDurability)
        {
            card.durabilityTrack.rectTransform.anchoredPosition = new Vector2(-52f, -21f);
            SetSizeIfChanged(card.durabilityTrack.rectTransform, new Vector2(38f, 5f));
            card.durability.rectTransform.anchoredPosition = new Vector2(-69f, -21f);
            SetSizeIfChanged(card.durability.rectTransform, new Vector2(34f * durability, 3f));
            card.durability.color = new Color32(242, 232, 210, 255);
        }
        ShowCard(card);
    }

    private static void SetSizeIfChanged(RectTransform rect, Vector2 size)
    {
        if (rect.sizeDelta != size)
        {
            rect.sizeDelta = size;
        }
    }

    private void UpdateCardBinding(PromptCard card, string action, bool visible, Vector2 size)
    {
        Sprite art = null;
        if (visible)
        {
            if (string.IsNullOrEmpty(action))
            {
                action = "Interact";
            }
            art = ResolveBindingArt(action);
        }
        card.key.enabled = visible && art != null;
        if (card.key.sprite != art)
        {
            card.key.sprite = art;
        }
        SetSizeIfChanged(card.key.rectTransform, size);
        card.fallbackKey.enabled = visible && art == null;
        if (card.fallbackKey.enabled && card.fallbackKey.text != bindingLabels[action])
        {
            card.fallbackKey.text = bindingLabels[action];
        }
    }

    private Sprite ResolveBindingArt(string action)
    {
        if (bindingArt.TryGetValue(action, out Sprite sprite))
        {
            return sprite;
        }

        string path = input.ReadPromptBinding(action, true);
        string key = input.ReadPromptBinding(action);
        bindingLabels[action] = key;
        if (path == "<Mouse>/leftButton")
        {
            key = "LMB";
        }
        else if (path == "<Mouse>/rightButton")
        {
            key = "RMB";
        }
        else if (path.StartsWith("<Keyboard>/") && path.Length > "<Keyboard>/".Length)
        {
            key = path.Substring("<Keyboard>/".Length);
            if (key.Length == 1)
            {
                key = key.ToUpperInvariant();
            }
            else
            {
                key = char.ToUpperInvariant(key[0]) + key.Substring(1);
            }
        }
        sprite = Art("Key_" + key);
        bindingArt.Add(action, sprite);
        return sprite;
    }

}

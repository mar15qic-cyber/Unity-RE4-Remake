using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    private readonly List<PromptCard> pickupMarkers = new List<PromptCard>();
    private readonly List<IInteractable> interactables = new List<IInteractable>();
    private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    private sealed class PromptCard
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image key;
        public Image icon;
        public Image durability;
        public Image arrow;
        public Image secondArrow;
        public Image background;
        public Text label;
        public Text fallbackKey;
        public Component target;
        public bool wasNear;
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
    }

    private void OnDisable()
    {
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
        far = null;
        urgent = null;
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
        card.key = Image(card.root, "按键", new Vector2(-96f, 0f), new Vector2(52f, 52f));
        card.icon = Image(card.root, "图标", new Vector2(-50f, 0f), new Vector2(32f, 32f));
        card.durability = Image(card.root, "刀具耐久", new Vector2(-112f, -21f), new Vector2(32f, 2f));
        card.durability.rectTransform.pivot = new Vector2(0f, .5f);
        card.durability.preserveAspect = false;
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
        main.group.alpha = 0f;
        secondary.group.alpha = 0f;
        mainTarget = null;
        far.group.alpha = 0f;
        urgent.group.alpha = 0f;
        foreach (PromptCard marker in pickupMarkers)
        {
            marker.group.alpha = 0f;
        }
        if (Time.timeScale <= 0f ||
            (GameUIManager.HasInstance && GameUIManager.Instance.IsMissionResultShowing) ||
            (GameManager.HasInstance && (GameManager.Instance.IsTerminal ||
                (!GameManager.Instance.Permission.Current.AllowInput && !coordinator.IsActive))) ||
            (GameUIManager.HasInstance && !GameUIManager.Instance.CanPlayerAct()))
        {
            return;
        }
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }
        if (coordinator.IsActive)
        {
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
            Present(urgent, new InteractionPromptSnapshot(enemy, warning.Action, InteractionPromptKind.EmergencyDefense,
                true, "Parry", "格挡", enemy.transform.position + Vector3.up * 1.6f), camera);
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
            if (!LeonQTECoordinator.IsPromptVisible(camera, pickup, position))
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

    private void Present(PromptCard card, InteractionPromptSnapshot snapshot, Camera camera)
    {
        Vector3 screen = camera.WorldToScreenPoint(snapshot.Position);
        bool near = snapshot.CanExecute;
        bool groundFinisher = near && snapshot.Kind == InteractionPromptKind.GroundFinisher;
        bool blockedFinisher = snapshot.Kind == InteractionPromptKind.GroundFinisherBlocked;
        if (screen.z <= 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height)
        {
            if (!groundFinisher && (!near || !LeonQTECoordinator.IsPassageInteraction(snapshot.Target)))
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
        card.root.anchoredPosition = local + new Vector2(96f, 35f);
        if (card == secondary && Vector2.Distance(main.root.anchoredPosition, card.root.anchoredPosition) < 70f)
        {
            card.root.anchoredPosition += Vector2.up * 48f;
        }
        if (card.target != snapshot.Target || card.wasNear != near)
        {
            card.target = snapshot.Target;
            card.wasNear = near;
            card.appearedAt = Time.unscaledTime;
        }
        // cs_ui2020 的对话 ENTER 轨在六帧内从 50 展开到 240，颜色从 0 到 255。
        float reveal = Mathf.Clamp01((Time.unscaledTime - card.appearedAt) * 60f / 6f);
        card.background.enabled = near && snapshot.Kind != InteractionPromptKind.GroundFinisher;
        card.background.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(50f, 240f, reveal), 34f);
        card.background.color = new Color(0f, 0f, 0f, reveal * .65f);
        card.key.enabled = near;
        card.label.enabled = near || blockedFinisher;
        card.icon.enabled = false;
        card.durability.enabled = false;
        card.fallbackKey.enabled = false;
        card.arrow.enabled = snapshot.Kind != InteractionPromptKind.EmergencyDefense && !blockedFinisher;
        card.secondArrow.enabled = !near && !blockedFinisher;
        card.key.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
        card.fallbackKey.rectTransform.anchoredPosition = card.key.rectTransform.anchoredPosition;
        card.icon.rectTransform.anchoredPosition = new Vector2(-50f, 0f);
        card.icon.rectTransform.sizeDelta = new Vector2(32f, 32f);
        card.arrow.rectTransform.anchoredPosition = new Vector2(-96f, -35f);
        card.secondArrow.rectTransform.anchoredPosition = new Vector2(-96f, -27f);
        card.label.text = snapshot.Content;
        card.label.rectTransform.anchoredPosition = new Vector2(70f, 0f);
        card.key.sprite = ResolveBindingArt(snapshot.InputAction);
        if (near && card.key.sprite == null)
        {
            card.key.enabled = false;
            card.fallbackKey.enabled = true;
            card.fallbackKey.text = input.ReadPromptBinding(snapshot.InputAction);
        }
        if (blockedFinisher)
        {
            card.icon.enabled = true;
            card.icon.sprite = Art("Knife");
            card.icon.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
            card.label.rectTransform.anchoredPosition = new Vector2(2f, 0f);
        }
        else if (groundFinisher)
        {
            card.background.color = Color.clear;
            card.icon.enabled = true;
            card.icon.sprite = Art("Knife");
            card.icon.rectTransform.sizeDelta = new Vector2(52f, 52f);
            card.label.enabled = false;
            card.key.rectTransform.anchoredPosition = new Vector2(-50f, 0f);
            card.fallbackKey.rectTransform.anchoredPosition = card.key.rectTransform.anchoredPosition;
            card.icon.rectTransform.anchoredPosition = new Vector2(-96f, 0f);
            card.arrow.enabled = false;
            card.secondArrow.enabled = false;
            if (InventoryManager.HasInstance)
            {
                var knife = InventoryManager.Instance.GetPreferredKnife();
                if (knife != null)
                {
                    float fraction = knife.DurabilityNormalized;
                    card.durability.enabled = fraction > 0f;
                    card.durability.rectTransform.anchoredPosition = new Vector2(-115f, -23f);
                    card.durability.rectTransform.sizeDelta = new Vector2(38f * fraction, 3f);
                }
            }
        }
        else if (snapshot.Target is ItemPickup item && item.item != null && near)
        {
            card.icon.enabled = item.item.icon != null;
            card.icon.sprite = item.item.icon;
            card.label.text = item.item.displayName + " ×" + item.count;
            card.label.rectTransform.anchoredPosition = new Vector2(96f, 0f);
        }
        card.group.alpha = 1f;
    }

    private Sprite ResolveBindingArt(string action)
    {
        string path = input.ReadPromptBinding(action, true);
        string key = input.ReadPromptBinding(action);
        if (path == "<Mouse>/leftButton")
        {
            key = "LMB";
        }
        else if (path == "<Mouse>/rightButton")
        {
            key = "RMB";
        }
        else if (path.StartsWith("<Keyboard>/"))
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
        return Art("Key_" + key);
    }

}

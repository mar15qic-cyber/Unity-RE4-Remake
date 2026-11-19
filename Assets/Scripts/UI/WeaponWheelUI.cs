using UnityEngine;
using UnityEngine.UI;

public class WeaponWheelUI : MonoBehaviour
{
    public const int SlotCount = 8;
    private static readonly WeaponType[] firearms = { WeaponType.Pistol, WeaponType.Rifle, WeaponType.Shotgun };
    private static readonly Vector2[] positions =
    {
        new Vector2(0f, 93f), new Vector2(-152f, 0f),
        new Vector2(0f, -93f), new Vector2(152f, 0f),
        new Vector2(0f, 186f), new Vector2(-304f, 0f),
        new Vector2(0f, -186f), new Vector2(304f, 0f)
    };

    private sealed class Slot
    {
        public Image background;
        public Image border;
        public Image glow;
        public Image icon;
        public Text key;
        public Text ammo;
        public int magazine = -1;
        public int reserve = -1;
    }

    public LeonController controller;
    public LeonWeaponHandler weaponHandler;
    public Canvas canvas;
    public CanvasGroup rootGroup;
    public RectTransform wheelRoot;

    [Tooltip("忽略小于此值的滚轮输入。")]
    public float scrollDeadZone = 0.5f;
    [Tooltip("快捷栏淡入和淡出的速度。")]
    public float fadeSpeed = 16f;

    private readonly Slot[] slots = new Slot[SlotCount];
    private LeonInputBridge inputBridge;
    private bool previewing;
    private float directFeedbackUntil;
    private int selectedIndex = -1;
    private bool ownsCanvas;

    public bool IsOpen => previewing || Time.unscaledTime < directFeedbackUntil;

    public static string GetSlotActionName(int index)
    {
        return "QuickSelect" + (index + 1);
    }

    internal static bool IsPresentationBlocked()
    {
        return (GameUIManager.HasInstance && (GameUIManager.Instance.currentState != GameUIManager.State.Gameplay
                || GameUIManager.Instance.IsCursorRequested || GameUIManager.Instance.IsPlayerInputBlocked))
            || (GameManager.HasInstance && !GameManager.Instance.Permission.Current.AllowInput)
            || (InventorySceneLoader.HasInstance && (InventorySceneLoader.Instance.IsVisible || InventorySceneLoader.Instance.IsLoading))
            || (StorageBoxUI.Instance != null && StorageBoxUI.Instance.IsVisible);
    }

    private void Start()
    {
        if (controller == null)
        {
            controller = GetComponent<LeonController>();
        }
        if (weaponHandler == null)
        {
            weaponHandler = GetComponent<LeonWeaponHandler>();
        }
        inputBridge = GetComponent<LeonInputBridge>();
    }

    private void Update()
    {
        if (rootGroup == null)
        {
            return;
        }

        if (IsPresentationBlocked() || controller == null || controller.currentState == LeonState.Dead
            || controller.currentState == LeonState.QTE)
        {
            HideImmediately();
            return;
        }

        float targetAlpha = 0f;
        if (IsOpen)
        {
            targetAlpha = 1f;
            RefreshSlots();
        }
        rootGroup.alpha = Mathf.MoveTowards(rootGroup.alpha, targetAlpha, fadeSpeed * Time.unscaledDeltaTime);
        if (rootGroup.alpha <= 0f && !IsOpen)
        {
            rootGroup.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        HideImmediately();
    }

    private void OnDestroy()
    {
        if (ownsCanvas && canvas != null)
        {
            Destroy(canvas.gameObject);
        }
    }

    public bool RegisterWeaponFromInventory(ItemData item)
    {
        if (item == null || item.itemType != ItemType.Weapon)
        {
            return false;
        }

        int index = System.Array.IndexOf(firearms, item.weaponType);
        return IsSlotAvailable(index);
    }

    public bool IsSlotAvailable(int index)
    {
        return index >= 0 && index < firearms.Length && weaponHandler != null
            && weaponHandler.HasWeaponInInventory(firearms[index]) && weaponHandler.GetWeapon(firearms[index]) != null;
    }

    public bool BeginSelectionFromScroll(float scroll)
    {
        if (IsPresentationBlocked() || weaponHandler == null)
        {
            return false;
        }

        selectedIndex = System.Array.IndexOf(firearms, weaponHandler.QuickSelectSelectTarget);
        if (!IsSlotAvailable(selectedIndex) && CombatHUD.HasInstance
            && CombatHUD.Instance.TryGetDisplayedFirearm(out WeaponType currentFirearm))
        {
            selectedIndex = System.Array.IndexOf(firearms, currentFirearm);
        }

        int direction = GetScrollDirection(scroll);
        if (!IsSlotAvailable(selectedIndex))
        {
            if (direction > 0)
            {
                selectedIndex = firearms.Length - 1;
            }
            else
            {
                selectedIndex = 0;
            }
        }

        MovePreview(scroll);
        if (!IsSlotAvailable(selectedIndex))
        {
            return false;
        }

        directFeedbackUntil = 0f;
        previewing = true;
        Show();
        return true;
    }

    public void MovePreview(float scroll)
    {
        int direction = GetScrollDirection(scroll);
        for (int step = 1; step <= firearms.Length; step++)
        {
            int index = (selectedIndex + direction * step + firearms.Length) % firearms.Length;
            if (IsSlotAvailable(index))
            {
                selectedIndex = index;
                return;
            }
        }
    }

    public void ConfirmSelection()
    {
        if (!previewing)
        {
            return;
        }

        previewing = false;
        if (IsSlotAvailable(selectedIndex) && !IsPresentationBlocked())
        {
            weaponHandler.QuickSelectSwitch(firearms[selectedIndex]);
        }
    }

    public void CancelSelection()
    {
        HideImmediately();
    }

    public bool SelectDirectly(int index)
    {
        if (!IsSlotAvailable(index) || IsPresentationBlocked() || !weaponHandler.QuickSelectSwitch(firearms[index]))
        {
            return false;
        }

        selectedIndex = index;
        previewing = false;
        directFeedbackUntil = Time.unscaledTime + 0.8f;
        Show();
        return true;
    }

    private static int GetScrollDirection(float scroll)
    {
        if (scroll > 0f)
        {
            return 1;
        }
        return -1;
    }

    private void Show()
    {
        EnsureView();
        if (inputBridge != null)
        {
            for (int index = 0; index < slots.Length; index++)
            {
                slots[index].key.text = inputBridge.GetQuickSlotBindingDisplay(index);
            }
        }
        RefreshSlots();
        rootGroup.gameObject.SetActive(true);
    }

    private void HideImmediately()
    {
        previewing = false;
        directFeedbackUntil = 0f;
        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.gameObject.SetActive(false);
        }
    }

    private void EnsureView()
    {
        if (slots[0] != null)
        {
            return;
        }

        if (canvas == null)
        {
            var canvasObject = new GameObject("武器快捷栏画布", typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasObject.GetComponent<Canvas>();
            ownsCanvas = true;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
        }

        if (rootGroup == null)
        {
            var root = new GameObject("武器快捷栏", typeof(RectTransform), typeof(CanvasGroup));
            rootGroup = root.GetComponent<CanvasGroup>();
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(canvas.transform, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
        }
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        if (wheelRoot == null)
        {
            wheelRoot = new GameObject("十字槽位", typeof(RectTransform)).GetComponent<RectTransform>();
            wheelRoot.SetParent(rootGroup.transform, false);
            wheelRoot.anchorMin = wheelRoot.anchorMax = new Vector2(0.735f, 0.43f);
            wheelRoot.sizeDelta = new Vector2(752f, 456f);
        }

        Image scrollMark = CreateImage("滚轮操作图标", wheelRoot, Vector2.zero, new Vector2(64f, 64f));
        scrollMark.sprite = CombatHUD.GetNativeHudSprite("ShortcutScroll");
        scrollMark.preserveAspect = true;
        scrollMark.color = new Color(1f, 1f, 1f, 0.92f);
        if (scrollMark.sprite == null)
        {
            scrollMark.enabled = false;
        }

        for (int index = 0; index < slots.Length; index++)
        {
            slots[index] = CreateSlot(index);
        }
    }

    private Slot CreateSlot(int index)
    {
        Image background = CreateImage("武器快捷槽 " + (index + 1), wheelRoot, positions[index], new Vector2(144f, 82f));
        var slot = new Slot { background = background };
        RectTransform rect = background.rectTransform;
        slot.border = CreateImage("槽位边框", rect, Vector2.zero, rect.sizeDelta);
        slot.border.sprite = CombatHUD.GetNativeHudSprite("ShortcutBorder");
        slot.border.type = Image.Type.Sliced;
        slot.border.enabled = slot.border.sprite != null;
        slot.glow = CreateImage("选中光晕", rect, Vector2.zero, new Vector2(158f, 96f));
        slot.glow.sprite = CombatHUD.GetNativeHudSprite("ShortcutHighlight");
        slot.glow.type = Image.Type.Sliced;
        slot.icon = CreateImage("武器图标", rect, new Vector2(0f, 6f), new Vector2(102f, 33f));
        slot.icon.preserveAspect = true;

        Image keyBackground = CreateImage("快捷键底色", rect, new Vector2(-56f, 27f), new Vector2(26f, 22f));
        keyBackground.color = new Color(0.07f, 0.07f, 0.07f, 0.86f);
        slot.key = CreateText("快捷键", keyBackground.rectTransform, Vector2.zero, new Vector2(26f, 22f), 15, TextAnchor.MiddleCenter);
        slot.key.resizeTextForBestFit = true;
        slot.key.resizeTextMinSize = 7;
        slot.key.resizeTextMaxSize = 15;
        slot.ammo = CreateText("弹匣与备弹", rect, new Vector2(29f, -23f), new Vector2(71f, 25f), 19, TextAnchor.MiddleRight);
        return slot;
    }

    private void RefreshSlots()
    {
        for (int index = 0; index < slots.Length; index++)
        {
            Slot slot = slots[index];
            bool available = IsSlotAvailable(index);
            bool selected = available && index == selectedIndex;
            slot.background.color = new Color(0.03f, 0.028f, 0.025f, 0.63f);
            slot.border.color = new Color(0.72f, 0.7f, 0.65f, 0.37f);
            if (selected)
            {
                slot.background.color = new Color(0.16f, 0.145f, 0.13f, 0.64f);
                slot.border.color = new Color(1f, 0.98f, 0.91f, 0.96f);
            }

            slot.glow.enabled = selected && slot.glow.sprite != null;
            slot.glow.color = new Color(1f, 0.98f, 0.92f, 0.62f);
            slot.icon.enabled = available;
            slot.ammo.enabled = available;
            if (available)
            {
                WeaponType type = firearms[index];
                WeaponBase weapon = weaponHandler.GetWeapon(type);
                slot.icon.sprite = CombatHUD.GetFirearmHudSprite(type, weapon);
                slot.icon.color = new Color(0.96f, 0.95f, 0.9f, 0.80f);
                if (selected)
                {
                    slot.icon.color = Color.white;
                }
                int magazine = weapon.currentAmmo;
                int reserve = weaponHandler.GetReserveAmmo(type);
                if (slot.magazine != magazine || slot.reserve != reserve)
                {
                    slot.ammo.text = CombatHUD.FormatAmmo(magazine, reserve);
                    slot.magazine = magazine;
                    slot.reserve = reserve;
                }
            }
        }
    }

    private static Image CreateImage(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        ConfigureRect(image.rectTransform, parent, position, size);
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(string name, RectTransform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        ConfigureRect(text.rectTransform, parent, position, size);
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private static void ConfigureRect(RectTransform rect, RectTransform parent, Vector2 position, Vector2 size)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

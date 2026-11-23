using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 公文包中的合成分页，负责配方列表、材料库存和合成操作。
/// </summary>
public class InventoryCraftingPanel : MonoBehaviour
{
    /// <summary>中文含义：物品列表页面根节点。使用位置：InventoryCraftingPanel.ShowCraftingPage()、InventoryCraftingPanel.ShowItemsPage()。</summary>
    [Header("分页引用")]
    public GameObject itemsPageRoot;
    /// <summary>中文含义：合成页面根节点。使用位置：InventoryCraftingPanel.ShowCraftingPage()、InventoryCraftingPanel.ShowItemsPage()。</summary>
    public GameObject craftingPageRoot;
    /// <summary>中文含义：物品列表分页按钮。使用位置：作为 InventoryCraftingPanel.ApplyTabStyle(Button, bool) 的参数、InventoryCraftingPanel.Initialize(InventoryUI)。</summary>
    public Button itemsTabButton;
    /// <summary>中文含义：合成分页按钮。使用位置：作为 InventoryCraftingPanel.ApplyTabStyle(Button, bool) 的参数、InventoryCraftingPanel.Initialize(InventoryUI)。</summary>
    public Button craftingTabButton;
    /// <summary>中文含义：页面标题文字。使用位置：InventoryCraftingPanel.ShowCraftingPage()、InventoryCraftingPanel.ShowItemsPage()。</summary>
    public Text pageTitleText;
    /// <summary>中文含义：配方列表根节点。使用位置：作为 Transform.SetParent(Transform, bool) 的参数、InventoryCraftingPanel.Refresh()。</summary>
    [Header("配方列表")]
    public RectTransform recipeListRoot;
    public RectTransform ownedMaterialsRoot;
    /// <summary>中文含义：结果图标。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。</summary>
    [Header("配方详情")]
    public Image resultIcon;
    public StoredItemPreview resultModelPreview;
    /// <summary>中文含义：结果名称文字。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。</summary>
    public Text resultNameText;
    /// <summary>中文含义：结果描述文字。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。</summary>
    public Text resultDescriptionText;
    /// <summary>中文含义：需求文字。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。</summary>
    public Text requirementText;
    /// <summary>中文含义：结果数量文字。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。</summary>
    public Text resultCountText;
    /// <summary>中文含义：合成按钮。使用位置：InventoryCraftingPanel.Initialize(InventoryUI)、InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。</summary>
    public Button craftButton;
    /// <summary>中文含义：合成按钮文字。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。</summary>
    public Text craftButtonText;
    [Tooltip("配方分类按钮，依次为全部、弹药、其它、草药。")]
    public Button[] recipeCategoryButtons;
    [Header("原版合成图集")]
    public Sprite nativeRecipeFocus;
    public Sprite nativeMaterialSlot;
    public Sprite nativeMaterialFocus;
    public Sprite nativeListLine;
    public CanvasGroup itemDetailsGroup;
    /// <summary>中文含义：分页普通状态颜色。使用位置：InventoryCraftingPanel.ApplyTabStyle(Button, bool)。</summary>
    [Header("视觉颜色")]
    public Color tabNormalColor = new Color(0.16f, 0.17f, 0.16f, 0.95f);
    /// <summary>中文含义：分页选定颜色。使用位置：InventoryCraftingPanel.ApplyTabStyle(Button, bool)。</summary>
    public Color tabSelectedColor = new Color(0.45f, 0.46f, 0.42f, 1f);
    /// <summary>中文含义：配方普通状态颜色。使用位置：InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)。</summary>
    public Color recipeNormalColor = new Color(0.09f, 0.1f, 0.095f, 0.96f);
    /// <summary>中文含义：配方可用颜色。使用位置：InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)。</summary>
    public Color recipeAvailableColor = new Color(0.2f, 0.23f, 0.18f, 0.98f);
    /// <summary>中文含义：配方选定颜色。使用位置：InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)。</summary>
    public Color recipeSelectedColor = new Color(0.46f, 0.43f, 0.31f, 1f);
    /// <summary>中文含义：可用文字颜色。使用位置：InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)。</summary>
    public Color availableTextColor = new Color(0.92f, 0.9f, 0.78f, 1f);
    /// <summary>中文含义：不可用文字颜色。使用位置：InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)。</summary>
    public Color unavailableTextColor = new Color(0.56f, 0.56f, 0.54f, 1f);
    /// <summary>中文含义：配方控件集合。只读字段，初始化后不由普通写入口替换。使用位置：作为 Object.Destroy(Object) 的参数、InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)、InventoryCraftingPanel.RebuildRecipeList(InventoryManager)。</summary>
    private readonly List<GameObject> recipeWidgets = new List<GameObject>();
    /// <summary>中文含义：背包界面。使用位置：InventoryCraftingPanel.CraftSelectedRecipe()、InventoryCraftingPanel.Initialize(InventoryUI)。</summary>
    private InventoryUI inventoryUI;
    private CanvasGroup itemsPageGroup;
    private float itemDetailsAlpha = 1f;
    private readonly Dictionary<string, ItemData> materialsById = new Dictionary<string, ItemData>();
    /// <summary>中文含义：选定配方。使用位置：作为 InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager) 的参数、作为 InventoryManager.TryCraft(ItemData, out int) 的参数、作为 InventoryUI.ShowPickupToast(string) 的参数 等共 7 处。</summary>
    private ItemData selectedRecipe;
    /// <summary>中文含义：是否已初始化。使用位置：InventoryCraftingPanel.Initialize(InventoryUI)。</summary>
    private bool initialized;
    private int recipeCategory;
    private const float CraftHoldSeconds = 0.65f;
    private InventoryCraftHoldTarget craftButtonHoldTarget;
    private InventoryCraftHoldTarget activeCraftTarget;
    private ItemData heldRecipe;
    private InputAction heldConfirmAction;
    private ButtonControl heldPointerButton;
    private int heldPointerId;
    private double craftHoldStarted;

    private bool CanInteractWithCrafting => craftingPageRoot != null && craftingPageRoot.activeInHierarchy
        && inventoryUI != null && inventoryUI.Interactive
        && (!GameUIManager.HasInstance || GameUIManager.Instance.currentState == GameUIManager.State.Inventory);

    /// <summary>Initialize 的业务处理入口；本方法直接依赖 InventoryCraftingPanel.ShowItemsPage()、UnityEvent.AddListener(UnityAction)、UnityEvent.RemoveListener(UnityAction)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryUI.Start()；下游（含内部回调体） InventoryCraftingPanel.ShowItemsPage()、UnityEvent.AddListener(UnityAction)、UnityEvent.RemoveListener(UnityAction)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="owner">中文含义：拥有者。使用位置：InventoryCraftingPanel.Initialize(InventoryUI)。</param>
    public void Initialize(InventoryUI owner)
    {
        inventoryUI = owner;
        if (initialized)
        {
            return;
        }

        initialized = true;
        if (itemDetailsGroup != null)
        {
            itemDetailsAlpha = itemDetailsGroup.alpha;
        }
        if (itemsPageRoot != null && itemsPageRoot != craftingPageRoot)
        {
            if (!itemsPageRoot.TryGetComponent(out itemsPageGroup))
            {
                itemsPageGroup = itemsPageRoot.AddComponent<CanvasGroup>();
            }
        }
        foreach (ItemData item in Resources.LoadAll<ItemData>("Items"))
        {
            if (!string.IsNullOrEmpty(item.itemId))
            {
                materialsById[item.itemId] = item;
            }
        }
        if (itemsTabButton != null)
        {
            itemsTabButton.onClick.RemoveListener(ShowItemsPage);
            itemsTabButton.onClick.AddListener(ShowItemsPage);
        }

        if (craftingTabButton != null)
        {
            // 标签横条应在页面之前接收射线，避免被物品页或合成页的底板挡住。
            craftingTabButton.transform.parent.SetAsLastSibling();
            if (craftingTabButton.targetGraphic != null)
            {
                craftingTabButton.targetGraphic.raycastTarget = true;
            }
            craftingTabButton.onClick.RemoveListener(ShowCraftingPage);
            craftingTabButton.onClick.AddListener(ShowCraftingPage);
        }

        if (craftButton != null)
        {
            craftButtonHoldTarget = craftButton.gameObject.AddComponent<InventoryCraftHoldTarget>();
            craftButtonHoldTarget.Bind(this, null);
        }

        if (recipeCategoryButtons != null)
        {
            for (int index = 0; index < recipeCategoryButtons.Length; index++)
            {
                var button = recipeCategoryButtons[index];
                if (button != null)
                {
                    int category = index;
                    button.onClick.AddListener(() => SelectCategory(category));
                }
            }
        }
        ShowItemsPage();
    }

    private void Update()
    {
        if (!CanInteractWithCrafting)
        {
            CancelCraftHold();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && recipeCategoryButtons != null && recipeCategoryButtons.Length > 1)
        {
            int lastCategory = recipeCategoryButtons.Length - 1;
            if (keyboard.aKey.wasPressedThisFrame)
            {
                SelectCategory((recipeCategory + lastCategory) % recipeCategoryButtons.Length);
            }
            else if (keyboard.dKey.wasPressedThisFrame)
            {
                SelectCategory((recipeCategory + 1) % recipeCategoryButtons.Length);
            }
        }

        AdvanceCraftHold();
    }

    private void OnDisable()
    {
        CancelCraftHold();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            CancelCraftHold();
        }
    }

    internal void BeginPointerCraftHold(InventoryCraftHoldTarget target, ItemData rowRecipe, PointerEventData eventData)
    {
        if (rowRecipe != null && rowRecipe != selectedRecipe)
        {
            return;
        }

        if (!TryBeginCraftHold(target))
        {
            return;
        }

        heldPointerId = eventData.pointerId;
        Mouse mouse = Mouse.current;
        if (eventData is ExtendedPointerEventData pointer && pointer.device is Mouse activeMouse)
        {
            mouse = activeMouse;
        }

        if (mouse != null)
        {
            heldPointerButton = mouse.leftButton;
        }
    }

    internal void EndPointerCraftHold(InventoryCraftHoldTarget target, int pointerId)
    {
        if (heldConfirmAction == null && activeCraftTarget == target && heldPointerId == pointerId)
        {
            CancelCraftHold();
        }
    }

    internal void CancelCraftHoldOn(InventoryCraftHoldTarget target)
    {
        if (activeCraftTarget == target)
        {
            CancelCraftHold();
        }
    }

    private bool TryBeginCraftHold(InventoryCraftHoldTarget target)
    {
        if (!CanInteractWithCrafting || heldRecipe != null || target == null || selectedRecipe == null)
        {
            return false;
        }

        InventoryManager manager = InventoryManager.Instance;
        if (manager == null || !manager.CanCraft(selectedRecipe))
        {
            return false;
        }

        activeCraftTarget = target;
        heldRecipe = selectedRecipe;
        craftHoldStarted = Time.unscaledTimeAsDouble;
        activeCraftTarget.ShowProgress(0.001f);
        return true;
    }

    private void AdvanceCraftHold()
    {
        if (heldRecipe == null)
        {
            InputAction confirm = inventoryUI.InventoryConfirmAction;
            if (confirm != null && confirm.WasPressedThisFrame() && confirm.IsPressed() && TryBeginCraftHold(craftButtonHoldTarget))
            {
                heldConfirmAction = confirm;
            }

            return;
        }

        bool releasedConfirm = heldConfirmAction != null && !heldConfirmAction.IsPressed();
        bool releasedPointer = heldPointerButton != null && !heldPointerButton.isPressed;
        if (activeCraftTarget == null || heldRecipe != selectedRecipe || releasedConfirm || releasedPointer)
        {
            CancelCraftHold();
            return;
        }

        float elapsed = (float)(Time.unscaledTimeAsDouble - craftHoldStarted);
        activeCraftTarget.ShowProgress(elapsed / CraftHoldSeconds);
        if (elapsed >= CraftHoldSeconds)
        {
            ItemData recipe = heldRecipe;
            CancelCraftHold();
            CraftRecipe(recipe);
        }
    }

    private void CancelCraftHold()
    {
        if (activeCraftTarget != null)
        {
            activeCraftTarget.ShowProgress(0f);
        }

        activeCraftTarget = null;
        heldRecipe = null;
        heldConfirmAction = null;
        heldPointerButton = null;
    }

    public void ShowItemsPage()
    {
        CancelCraftHold();
        if (inventoryUI != null && inventoryUI.Interactive && craftingPageRoot != null && craftingPageRoot.activeSelf)
        {
            AudioManager.PlayInterface(AudioEventNames.UIPage);
        }

        if (itemsPageRoot != null)
        {
            itemsPageRoot.SetActive(true);
            if (itemsPageGroup != null)
            {
                itemsPageGroup.interactable = true;
                itemsPageGroup.blocksRaycasts = true;
            }
        }

        if (craftingPageRoot != null)
        {
            craftingPageRoot.SetActive(false);
        }
        SetCraftingBackdrop(false);

        if (pageTitleText != null)
        {
            pageTitleText.text = "物品";
        }

        ApplyTabStyle(itemsTabButton, true);
        ApplyTabStyle(craftingTabButton, false);
    }

    /// <summary>ShowCraftingPage 的业务处理入口；本方法直接依赖 GameObject.SetActive(bool)、InventoryCraftingPanel.ApplyTabStyle(Button, bool)、InventoryCraftingPanel.Refresh()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） GameObject.SetActive(bool)、InventoryCraftingPanel.ApplyTabStyle(Button, bool)、InventoryCraftingPanel.Refresh()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void ShowCraftingPage()
    {
        if (inventoryUI != null && !inventoryUI.Interactive)
        {
            return;
        }
        if (inventoryUI != null && inventoryUI.Interactive && craftingPageRoot != null && !craftingPageRoot.activeSelf)
        {
            AudioManager.PlayInterface(AudioEventNames.UIPage);
        }

        if (itemsPageGroup != null)
        {
            itemsPageGroup.interactable = false;
            itemsPageGroup.blocksRaycasts = false;
        }

        if (craftingPageRoot != null)
        {
            craftingPageRoot.SetActive(true);
        }
        SetCraftingBackdrop(true);

        if (pageTitleText != null)
        {
            pageTitleText.text = "合成";
        }

        ApplyTabStyle(itemsTabButton, false);
        ApplyTabStyle(craftingTabButton, true);
        Refresh();
    }

    private void SetCraftingBackdrop(bool visible)
    {
        if (itemDetailsGroup != null)
        {
            if (visible)
            {
                itemDetailsGroup.alpha = 0f;
            }
            else
            {
                itemDetailsGroup.alpha = itemDetailsAlpha;
            }
        }
        if (inventoryUI != null)
        {
            inventoryUI.SetCraftingBackdrop(visible);
        }
    }

    /// <summary>Refresh 的业务处理入口；本方法直接依赖 InventoryCraftingPanel.RebuildRecipeList(InventoryManager)、InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)、InventoryManager.GetCraftingRecipes()，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.CraftSelectedRecipe()、InventoryCraftingPanel.SelectRecipe(ItemData)、InventoryCraftingPanel.ShowCraftingPage()、InventoryUI.OnSceneLoaded()、InventoryUI.RefreshGridInternal()；下游（含内部回调体） InventoryCraftingPanel.RebuildRecipeList(InventoryManager)、InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)、InventoryManager.GetCraftingRecipes()。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    public void Refresh()
    {
        CancelCraftHold();
        // manager（当前背包管理器）：使用位置：作为 InventoryCraftingPanel.RebuildRecipeList(InventoryManager) 的参数、作为 InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager) 的参数、InventoryCraftingPanel.Refresh()。
InventoryManager manager = InventoryManager.Instance;
        if (manager == null || recipeListRoot == null)
        {
            return;
        }

        List<ItemData> recipes = manager.GetCraftingRecipes();
        RefreshOwnedMaterials(manager, recipes);
        recipes.RemoveAll(recipe => !IsRecipeInCategory(recipe));
        if (!recipes.Contains(selectedRecipe))
        {
            selectedRecipe = null;
            if (recipes.Count > 0)
            {
                selectedRecipe = recipes[0];
            }
        }
        RebuildRecipeList(manager, recipes);
        if (recipeCategoryButtons != null)
        {
            for (int index = 0; index < recipeCategoryButtons.Length; index++)
            {
                ApplyTabStyle(recipeCategoryButtons[index], recipeCategory == index);
            }
        }
        ShowRecipeDetails(selectedRecipe, manager);
    }

    private void SelectCategory(int category)
    {
        if (inventoryUI != null && !inventoryUI.Interactive)
        {
            return;
        }
        recipeCategory = category;
        Refresh();
        if (recipeListRoot != null && recipeListRoot.GetComponentInParent<ScrollRect>() is ScrollRect scroller)
        {
            scroller.verticalNormalizedPosition = 1f;
        }
    }

    private bool IsRecipeInCategory(ItemData recipe)
    {
        switch (recipeCategory)
        {
            case 1:
                return recipe.itemType == ItemType.Ammo;
            case 2:
                return recipe.itemType != ItemType.Ammo && recipe.itemType != ItemType.Healing;
            case 3:
                return recipe.itemType == ItemType.Healing;
            default:
                return true;
        }
    }

    internal ItemData FindRecipeForIngredient(ItemData item)
    {
        if (item == null || InventoryManager.Instance == null)
        {
            return null;
        }
        foreach (var recipe in InventoryManager.Instance.GetCraftingRecipes())
        {
            if (recipe.craftingIngredients == null)
            {
                continue;
            }
            foreach (var ingredient in recipe.craftingIngredients)
            {
                if (ingredient != null && ingredient.itemId == item.itemId)
                {
                    return recipe;
                }
            }
        }
        return null;
    }

    internal void ShowIngredientRecipe(ItemData recipe)
    {
        recipeCategory = 0;
        selectedRecipe = recipe;
        ShowCraftingPage();
    }

    /// <summary>RebuildRecipeList 的业务处理入口；本方法直接依赖 Dictionary&lt;ItemData, Image&gt;.Clear()、InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)、InventoryManager.GetCraftingRecipes()、List&lt;GameObject&gt;.Clear()、Object.Destroy(Object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.Refresh()；下游（含内部回调体） Dictionary&lt;ItemData, Image&gt;.Clear()、InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)、InventoryManager.GetCraftingRecipes()、List&lt;GameObject&gt;.Clear()、Object.Destroy(Object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="manager">中文含义：当前背包管理器。使用位置：作为 InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager) 的参数、InventoryCraftingPanel.RebuildRecipeList(InventoryManager)。</param>
    private void RebuildRecipeList(InventoryManager manager, List<ItemData> recipes)
    {
        // i（当前遍历索引）：使用位置：作为 List<GameObject>.this[int] 的参数、InventoryCraftingPanel.RebuildRecipeList(InventoryManager)。
for (int i = 0; i < recipeWidgets.Count; i++)
        {
            if (recipeWidgets[i] != null)
            {
                recipeWidgets[i].SetActive(false);
                Destroy(recipeWidgets[i]);
            }
        }

        recipeWidgets.Clear();
        // recipes（配方集合）：保存 InventoryManager.GetCraftingRecipes() 的返回结果，供当前步骤使用。使用位置：作为 InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager) 的参数、InventoryCraftingPanel.RebuildRecipeList(InventoryManager)。
        // i（当前遍历索引）：使用位置：作为 List<ItemData>.this[int] 的参数、InventoryCraftingPanel.RebuildRecipeList(InventoryManager)。
for (int i = 0; i < recipes.Count; i++)
        {
            CreateRecipeWidget(recipes[i], manager);
        }
    }

    /// <summary>CreateRecipeWidget 的业务处理入口；本方法直接依赖 Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;LayoutElement&gt;()、InventoryCraftingPanel.CreateImage(Transform, string, Vector2, Vector2)、InventoryCraftingPanel.CreateText(Transform, string, int, TextAnchor) 等共 15 处，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.RebuildRecipeList(InventoryManager)；下游（含内部回调体） Color.Color(float, float, float, float)、GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Button&gt;()、GameObject.GetComponent&lt;Image&gt;()、GameObject.GetComponent&lt;LayoutElement&gt;()、InventoryCraftingPanel.CreateImage(Transform, string, Vector2, Vector2)、InventoryCraftingPanel.CreateText(Transform, string, int, TextAnchor) 等共 15 处。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="recipe">中文含义：配方。使用位置：作为 Dictionary&lt;ItemData, Image&gt;.this[ItemData] 的参数、作为 GameObject.GameObject(string, params Type[]) 的参数、作为 InventoryCraftingPanel.SelectRecipe(ItemData) 的参数 等共 7 处。</param>
    /// <param name="manager">中文含义：当前背包管理器。使用位置：InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)。</param>
    private void CreateRecipeWidget(ItemData recipe, InventoryManager manager)
    {
        var widget = new GameObject("配方_" + recipe.displayName, typeof(Image), typeof(Button), typeof(LayoutElement));
        widget.transform.SetParent(recipeListRoot, false);
        recipeWidgets.Add(widget);
        var background = widget.GetComponent<Image>();
        background.color = recipeNormalColor;
        if (recipe == selectedRecipe)
        {
            background.color = recipeSelectedColor;
            background.sprite = nativeRecipeFocus;
            if (nativeRecipeFocus != null)
            {
                background.type = Image.Type.Sliced;
            }
        }
        bool canCraft = manager.CanCraft(recipe);
        var layout = widget.GetComponent<LayoutElement>();
        layout.minHeight = 59f;
        layout.preferredHeight = 59f;
        var button = widget.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => SelectRecipe(recipe));
        var holdTarget = widget.AddComponent<InventoryCraftHoldTarget>();
        holdTarget.Bind(this, recipe);

        var icon = CreateImage(widget.transform, "产物图标", new Vector2(12f, -4f), new Vector2(55f, 40f));
        icon.sprite = recipe.icon;
        icon.preserveAspect = true;
        if (icon.sprite == null)
        {
            icon.color = Color.clear;
        }

        var owned = CreateText(widget.transform, "现有数量", 13, TextAnchor.MiddleRight);
        SetRect(owned.rectTransform, 0f, 0f, 0f, .29f, 12f, 2f, 66f, 2f);
        owned.color = new Color(.72f, .71f, .68f, 1f);
        owned.text = manager.GetItemCount(recipe.itemId).ToString();

        var name = CreateText(widget.transform, "产物名称", 23, TextAnchor.MiddleLeft);
        SetRect(name.rectTransform, 0f, 0f, .56f, 1f, 74f, 0f, -8f, 0f);
        name.text = recipe.displayName;
        if (recipe.craftingResultCount > 1)
        {
            name.text += " ×" + recipe.craftingResultCount;
        }
        name.color = availableTextColor;
        if (!canCraft && recipe != selectedRecipe)
        {
            name.color = unavailableTextColor;
        }

        var separator = CreateImage(widget.transform, "材料栏分隔线", Vector2.zero, Vector2.one);
        SetRect(separator.rectTransform, .56f, 0f, .56f, 1f, 0f, 0f, 1f, 0f);
        separator.color = new Color(.72f, .72f, .68f, .35f);
        var rule = CreateImage(widget.transform, "列表分隔线", Vector2.zero, Vector2.one);
        SetRect(rule.rectTransform, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);
        rule.sprite = nativeListLine;
        rule.color = new Color(1f, 1f, 1f, .55f);

        var requirements = ReadIngredients(recipe);
        for (int i = 0; i < requirements.Count; i++)
        {
            float step = .44f / Mathf.Max(3, requirements.Count);
            float left = .56f + step * i;
            if (i > 0)
            {
                var plus = CreateText(widget.transform, "材料相加", 18, TextAnchor.MiddleCenter);
                SetRect(plus.rectTransform, left, 0f, left, 1f, -8f, 0f, 8f, 0f);
                plus.text = "+";
                plus.color = availableTextColor;
            }
            AddMaterialCell(widget.transform, requirements[i].itemId, requirements[i].count, left,
                step, manager.GetItemCount(requirements[i].itemId) >= requirements[i].count, false);
        }
    }

    private void RefreshOwnedMaterials(InventoryManager manager, List<ItemData> recipes)
    {
        if (ownedMaterialsRoot == null)
        {
            return;
        }
        for (int i = ownedMaterialsRoot.childCount - 1; i >= 0; i--)
        {
            GameObject oldCell = ownedMaterialsRoot.GetChild(i).gameObject;
            oldCell.SetActive(false);
            Destroy(oldCell);
        }
        var itemIds = new List<string>();
        foreach (ItemData recipe in recipes)
        {
            foreach (var requirement in ReadIngredients(recipe))
            {
                if (!itemIds.Contains(requirement.itemId))
                {
                    itemIds.Add(requirement.itemId);
                }
            }
        }
        float width = 1f / Mathf.Max(11, itemIds.Count);
        for (int i = 0; i < itemIds.Count; i++)
        {
            int count = manager.GetItemCount(itemIds[i]);
            AddMaterialCell(ownedMaterialsRoot, itemIds[i], count, i * width, width, count > 0, true);
        }
    }

    private static List<CraftingIngredientRequirement> ReadIngredients(ItemData recipe)
    {
        var requirements = new List<CraftingIngredientRequirement>();
        if (recipe.craftingIngredients != null)
        {
            foreach (var requirement in recipe.craftingIngredients)
            {
                if (requirement != null && !string.IsNullOrEmpty(requirement.itemId))
                {
                    requirements.Add(requirement);
                }
            }
        }
        if (requirements.Count == 0 && !string.IsNullOrEmpty(recipe.craftIngredientA))
        {
            requirements.Add(new CraftingIngredientRequirement { itemId = recipe.craftIngredientA });
            if (!string.IsNullOrEmpty(recipe.craftIngredientB))
            {
                if (recipe.craftIngredientB == recipe.craftIngredientA)
                {
                    requirements[0].count = 2;
                }
                else
                {
                    requirements.Add(new CraftingIngredientRequirement { itemId = recipe.craftIngredientB });
                }
            }
        }
        return requirements;
    }

    private void AddMaterialCell(Transform parent, string itemId, int count, float left, float width, bool available, bool stacked)
    {
        var slot = CreateImage(parent, itemId, Vector2.zero, Vector2.one);
        SetRect(slot.rectTransform, left, 0f, left + width, 1f, 4f, 2f, -4f, -2f);
        slot.color = Color.clear;
        var icon = CreateImage(slot.transform, "材料图标", Vector2.zero, Vector2.one);
        icon.preserveAspect = true;
        icon.color = Color.clear;
        if (materialsById.TryGetValue(itemId, out ItemData material) && material.icon != null)
        {
            icon.sprite = material.icon;
            icon.color = Color.white;
            if (!available)
            {
                icon.color = new Color(1f, 1f, 1f, .48f);
            }
        }

        var quantity = CreateText(slot.transform, "材料数量", 18, TextAnchor.MiddleCenter);
        quantity.text = count.ToString();
        quantity.color = availableTextColor;
        if (!available)
        {
            quantity.color = new Color(.83f, .31f, .22f, 1f);
        }
        if (stacked)
        {
            var frame = CreateImage(slot.transform, "原版材料槽", Vector2.zero, Vector2.one);
            frame.sprite = nativeMaterialSlot;
            if (available && nativeMaterialFocus != null)
            {
                frame.sprite = nativeMaterialFocus;
            }
            frame.color = new Color(1f, 1f, 1f, .24f);
            if (available)
            {
                frame.color = new Color(1f, 1f, 1f, .66f);
            }
            SetRect(frame.rectTransform, 0f, .31f, 1f, 1f, 0f, 0f, 0f, 0f);
            frame.transform.SetAsFirstSibling();
            SetRect(icon.rectTransform, .08f, .36f, .92f, .95f, 0f, 0f, 0f, 0f);
            SetRect(quantity.rectTransform, 0f, 0f, 1f, .29f, 0f, 0f, 0f, 0f);
        }
        else
        {
            SetRect(icon.rectTransform, .07f, .12f, .61f, .88f, 0f, 0f, 0f, 0f);
            SetRect(quantity.rectTransform, .61f, 0f, 1f, 1f, 0f, 0f, -3f, 0f);
        }
    }

    private static void SetRect(RectTransform rect, float left, float bottom, float right, float top,
        float leftOffset, float bottomOffset, float rightOffset, float topOffset)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = new Vector2(leftOffset, bottomOffset);
        rect.offsetMax = new Vector2(rightOffset, topOffset);
    }

    private void SelectRecipe(ItemData recipe)
    {
        if (selectedRecipe != recipe)
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseHover);
        }

        selectedRecipe = recipe;
        Refresh();
    }

    /// <summary>ShowRecipeDetails 的业务处理入口；本方法直接依赖 InventoryManager.CanCraft(ItemData)、InventoryManager.GetCraftingRequirementText(ItemData)、Mathf.Max(int, int)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.Refresh()；下游（含内部回调体） InventoryManager.CanCraft(ItemData)、InventoryManager.GetCraftingRequirementText(ItemData)、Mathf.Max(int, int)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="recipe">中文含义：配方。使用位置：作为 InventoryManager.CanCraft(ItemData) 的参数、作为 InventoryManager.GetCraftingRequirementText(ItemData) 的参数、作为 Mathf.Max(int, int) 的参数 等共 4 处。</param>
    /// <param name="manager">中文含义：当前背包管理器。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。</param>
    private void ShowRecipeDetails(ItemData recipe, InventoryManager manager)
    {
        // hasRecipe（是否具有配方）：使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
        bool hasRecipe = recipe != null;
        bool hasModel = false;
        if (resultModelPreview != null && resultModelPreview.isActiveAndEnabled)
        {
            hasModel = resultModelPreview.ShowModel(recipe);
        }
        if (resultIcon != null)
        {
            resultIcon.enabled = !hasModel;
            // assignmentReceiver10（赋值前保存的目标结果图标）：从结果图标读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
global::UnityEngine.UI.Image assignmentReceiver10 = resultIcon;
            if (hasRecipe)
            {
                assignmentReceiver10.sprite = recipe.icon;
            }
            else
            {
                assignmentReceiver10.sprite = null;
            }

            // assignmentReceiver12（赋值前保存的目标结果图标）：从结果图标读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
global::UnityEngine.UI.Image assignmentReceiver12 = resultIcon;
            if (hasRecipe && recipe.icon != null)
            {
                assignmentReceiver12.color = Color.white;
            }
            else
            {
                assignmentReceiver12.color = Color.clear;
            }

            resultIcon.preserveAspect = true;
        }

        if (resultNameText != null)
        {
            // assignmentReceiver14（赋值前保存的目标结果名称文字）：从结果名称文字读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
global::UnityEngine.UI.Text assignmentReceiver14 = resultNameText;
            if (hasRecipe)
            {
                assignmentReceiver14.text = recipe.displayName;
            }
            else
            {
                assignmentReceiver14.text = "请选择配方";
            }
        }

        if (resultDescriptionText != null)
        {
            // assignmentReceiver16（赋值前保存的目标结果描述文字）：从结果描述文字读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
global::UnityEngine.UI.Text assignmentReceiver16 = resultDescriptionText;
            if (hasRecipe)
            {
                assignmentReceiver16.text = recipe.description;
            }
            else
            {
                assignmentReceiver16.text = string.Empty;
            }
        }

        if (requirementText != null)
        {
            // assignmentReceiver18（赋值前保存的目标需求文字）：从需求文字读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
global::UnityEngine.UI.Text assignmentReceiver18 = requirementText;
            if (hasRecipe)
            {
                assignmentReceiver18.text = manager.GetCraftingRequirementText(recipe);
            }
            else
            {
                assignmentReceiver18.text = string.Empty;
            }
        }

        if (resultCountText != null)
        {
            // assignmentReceiver20（赋值前保存的目标结果数量文字）：从结果数量文字读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
global::UnityEngine.UI.Text assignmentReceiver20 = resultCountText;
            if (hasRecipe)
            {
                assignmentReceiver20.text = "产量　×" + Mathf.Max(1, recipe.craftingResultCount);
            }
            else
            {
                assignmentReceiver20.text = string.Empty;
            }
        }

        // canCraft（是否可以合成）：使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
bool canCraft = hasRecipe && manager.CanCraft(recipe);
        if (craftButton != null)
        {
            craftButton.interactable = canCraft;
        }

        if (craftButtonText != null)
        {
            // assignmentReceiver22（赋值前保存的目标合成按钮文字）：从合成按钮文字读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryCraftingPanel.ShowRecipeDetails(ItemData, InventoryManager)。
global::UnityEngine.UI.Text assignmentReceiver22 = craftButtonText;
            if (canCraft)
            {
                assignmentReceiver22.text = "长按合成";
                InputAction confirm = inventoryUI.InventoryConfirmAction;
                if (confirm != null && confirm.bindings.Count > 0)
                {
                    string keyName = confirm.GetBindingDisplayString(0, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                    if (!string.IsNullOrEmpty(keyName))
                    {
                        assignmentReceiver22.text += "  [" + keyName + "]";
                    }
                }
            }
            else
            {
                assignmentReceiver22.text = "材料不足";
            }
        }
    }

    /// <summary>CraftSelectedRecipe 的业务处理入口；本方法直接依赖 InventoryCraftingPanel.Refresh()、InventoryManager.TryCraft(ItemData, out int)、InventoryUI.ShowPickupToast(string)、object.ReferenceEquals(object, object)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：未发现可解析的直接上游调用；下游（含内部回调体） InventoryCraftingPanel.Refresh()、InventoryManager.TryCraft(ItemData, out int)、InventoryUI.ShowPickupToast(string)、object.ReferenceEquals(object, object)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    private void CraftRecipe(ItemData recipe)
    {
        InventoryManager manager = InventoryManager.Instance;
        if (!CanInteractWithCrafting || manager == null || recipe != selectedRecipe)
        {
            return;
        }

        if (manager.TryCraft(recipe, out int craftedCount))
        {
            inventoryUI.ShowPickupToast("合成完成：" + recipe.displayName + " ×" + craftedCount);
        }
        else
        {
            AudioManager.PlayInterface(AudioEventNames.UICaseError);
            inventoryUI.ShowPickupToast("材料不足或公文包空间不足");
        }

        Refresh();
    }

    private void ApplyTabStyle(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        // image（图像）：保存 Component.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：InventoryCraftingPanel.ApplyTabStyle(Button, bool)。
Image image = button.GetComponent<Image>();
        if (image != null)
        {
            if (image.sprite == nativeRecipeFocus && nativeRecipeFocus != null)
            {
                if (selected)
                {
                    image.color = new Color(1f, 1f, 1f, .62f);
                }
                else
                {
                    image.color = Color.clear;
                }
            }
            else if (selected)
            {
                image.color = tabSelectedColor;
            }
            else
            {
                image.color = tabNormalColor;
            }
        }

        // label（标签）：保存 Component.GetComponentInChildren<Text>(bool) 的返回结果，供当前步骤使用。使用位置：InventoryCraftingPanel.ApplyTabStyle(Button, bool)。
Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            // assignmentReceiver28（赋值前保存的目标标签）：从标签读取一次后保存，供后续分支使用。按原表达式顺序保存一次求值结果，后续分支和调用复用此值，避免重新读取接收者或实参。使用位置：InventoryCraftingPanel.ApplyTabStyle(Button, bool)。
global::UnityEngine.UI.Text assignmentReceiver28 = label;
            if (selected)
            {
                assignmentReceiver28.color = Color.white;
            }
            else
            {
                assignmentReceiver28.color = new Color(0.62f, 0.62f, 0.6f, 1f);
            }
        }
    }

    /// <summary>CreateImage 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Image&gt;()、Transform.SetParent(Transform, bool)、Vector2.Vector2(float, float)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="position">中文含义：位置。使用位置：InventoryCraftingPanel.CreateImage(Transform, string, Vector2, Vector2)。</param>
    /// <param name="size">中文含义：尺寸。使用位置：InventoryCraftingPanel.CreateImage(Transform, string, Vector2, Vector2)。</param>
    private static Image CreateImage(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        // gameObject（游戏对象）：使用位置：InventoryCraftingPanel.CreateImage(Transform, string, Vector2, Vector2)。
GameObject gameObject = new GameObject(objectName, typeof(Image));
        gameObject.transform.SetParent(parent, false);
        // image（图像）：保存 GameObject.GetComponent<Image>() 的返回结果，供当前步骤使用。使用位置：InventoryCraftingPanel.CreateImage(Transform, string, Vector2, Vector2)。
Image image = gameObject.GetComponent<Image>();
        // rect（矩形）：使用位置：InventoryCraftingPanel.CreateImage(Transform, string, Vector2, Vector2)。
RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>CreateText 的业务处理入口；本方法直接依赖 GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Resources.GetBuiltinResource&lt;Font&gt;(string)、Transform.SetParent(Transform, bool)，调用方通过参数与返回结果交接本次处理。</summary>
    /// <remarks>静态调用链：上游 InventoryCraftingPanel.CreateRecipeWidget(ItemData, InventoryManager)；下游（含内部回调体） GameObject.GameObject(string, params Type[])、GameObject.GetComponent&lt;Text&gt;()、Resources.GetBuiltinResource&lt;Font&gt;(string)、Transform.SetParent(Transform, bool)。动态事件、动画标记、反射和序列化绑定须结合实际宿主核对；完整直接调用清单见项目通信索引。</remarks>
    /// <param name="parent">中文含义：父级。使用位置：作为 Transform.SetParent(Transform, bool) 的参数。</param>
    /// <param name="objectName">中文含义：对象名称。使用位置：作为 GameObject.GameObject(string, params Type[]) 的参数。</param>
    /// <param name="fontSize">中文含义：字体尺寸。使用位置：InventoryCraftingPanel.CreateText(Transform, string, int, TextAnchor)。</param>
    /// <param name="alignment">中文含义：对齐状态。使用位置：InventoryCraftingPanel.CreateText(Transform, string, int, TextAnchor)。</param>
    private Text CreateText(Transform parent, string objectName, int fontSize, TextAnchor alignment)
    {
        // gameObject（游戏对象）：使用位置：InventoryCraftingPanel.CreateText(Transform, string, int, TextAnchor)。
GameObject gameObject = new GameObject(objectName, typeof(Text));
        gameObject.transform.SetParent(parent, false);
        // text（文字）：保存 GameObject.GetComponent<Text>() 的返回结果，供当前步骤使用。使用位置：InventoryCraftingPanel.CreateText(Transform, string, int, TextAnchor)。
Text text = gameObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (resultNameText != null && resultNameText.font != null)
        {
            text.font = resultNameText.font;
        }
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles shop item selection, weighting by rarity, display in the UI,
/// and purchase flow (with localization-aware text).
/// </summary>
public class Shop : MonoBehaviour
{
    #region Fields

    [Header("All Potential Shop Items")]
    [Tooltip("All items that are eligible to appear in the shop this session.")]
    [SerializeField] private List<ItemSO> allUnboughtItems = new List<ItemSO>();

    [Header("Shop UI")]
    [Tooltip("Parent transform containing the item slot UI elements.")]
    [SerializeField] private Transform storeDisplay;

    [Tooltip("How many items to randomly select and offer for sale.")]
    [SerializeField] private int numberOfItemsToSell = 3;

    [Tooltip("UI shown when leaving the shop (e.g., confirmation/auth screen).")]
    [SerializeField] private AuthenticateUI authenticateUI;

    [Tooltip("Button used to leave the shop and return to the previous screen.")]
    [SerializeField] private Button leaveButton;

    /// <summary>Current items being displayed for sale.</summary>
    private List<ItemSO> items = new List<ItemSO>();

    // Cached singletons
    private Localizer localizer;
    private LobbyAssets lobbyAssets;
    private SoundController soundController;
    private Inventory inventory;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        localizer = Localizer.Instance;
        lobbyAssets = LobbyAssets.Instance;
        soundController = SoundController.Instance;
        inventory = GetComponent<Inventory>();

        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(OnLeaveButtonClicked);
        }
        else
        {
            Debug.LogWarning($"[{nameof(Shop)}] leaveButton is not assigned.");
        }

        LangCon.RefreshRequested += UpdateDisplay;
    }

    private void OnDestroy()
    {
        LangCon.RefreshRequested -= UpdateDisplay;

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveListener(OnLeaveButtonClicked);
        }
    }

    #endregion

    #region Setup & Display

    /// <summary>
    /// Initializes the shop with a new pool of items and updates the UI.
    /// </summary>
    /// <param name="availableItems">All items that may appear in the shop.</param>
    public void StartUp(List<ItemSO> availableItems)
    {
        allUnboughtItems.Clear();
        allUnboughtItems = new List<ItemSO>(availableItems);

        // 1) Filter + weight items to get a list of potential items
        List<ItemSO> weightedItems = GetWeightedItemPool();

        // 2) Randomly pick 'numberOfItemsToSell' from that pool
        List<ItemSO> itemsForSale = PickRandomItems(weightedItems, numberOfItemsToSell);

        // 3) Display them in the UI
        DisplayItems(itemsForSale);

        // Shop starts hidden until explicitly shown
        Hide();
    }

    /// <summary>
    /// Builds a weighted list of items based on rarity.
    /// Higher rarity = fewer entries in the pool.
    /// </summary>
    private List<ItemSO> GetWeightedItemPool()
    {
        List<ItemSO> weightedItems = new List<ItemSO>();

        foreach (ItemSO item in allUnboughtItems)
        {
            if (item == null)
            {
                continue;
            }

            int weight = item.Rarity switch
            {
                Rarity.Poor => 1024,
                Rarity.Common => 512,
                Rarity.Uncommon => 256,
                Rarity.Rare => 128,
                Rarity.Superior => 64,
                Rarity.Epic => 32,
                Rarity.Legendary => 16,
                Rarity.Mythic => 8,
                Rarity.Ancient => 4,
                Rarity.Divine => 2,
                Rarity.Artifact => 1,
                _ => 1
            };

            AddItemMultipleTimes(weightedItems, item, weight);
        }

        return weightedItems;
    }

    /// <summary>
    /// Adds the given item to the list multiple times to represent its weight.
    /// </summary>
    private void AddItemMultipleTimes(List<ItemSO> list, ItemSO item, int count)
    {
        for (int i = 0; i < count; i++)
        {
            list.Add(item);
        }
    }

    /// <summary>
    /// Randomly picks 'amount' distinct items from the weighted pool.
    /// Duplicates of the same item in the result are not allowed.
    /// </summary>
    private List<ItemSO> PickRandomItems(List<ItemSO> weightedItems, int amount)
    {
        List<ItemSO> result = new List<ItemSO>();
        List<ItemSO> tempList = new List<ItemSO>(weightedItems);

        for (int i = 0; i < amount; i++)
        {
            if (tempList.Count == 0)
            {
                break; // No more items to pick
            }

            int randomIndex = Random.Range(0, tempList.Count);
            ItemSO chosenItem = tempList[randomIndex];
            result.Add(chosenItem);

            // Remove all duplicates so that this item cannot be chosen again.
            tempList.RemoveAll(item => item == chosenItem);
        }

        return result;
    }

    /// <summary>
    /// Displays the given list of items in the store UI by assigning them to button slots.
    /// </summary>
    private void DisplayItems(List<ItemSO> itemsForSale)
    {
        if (storeDisplay == null)
        {
            Debug.LogError($"[{nameof(Shop)}] storeDisplay is not assigned.");
            return;
        }

        items = new List<ItemSO>(itemsForSale);
        int slotCount = storeDisplay.childCount;

        for (int i = 0; i < slotCount; i++)
        {
            Transform slot = storeDisplay.GetChild(i);
            Button slotButton = slot.GetComponent<Button>();

            if (slotButton == null)
            {
                Debug.LogWarning($"[{nameof(Shop)}] Slot {i} on '{storeDisplay.name}' has no Button component.");
                continue;
            }

            // Empty / sold-out slot
            if (i >= itemsForSale.Count)
            {
                ConfigureEmptySlot(slot, slotButton);
                continue;
            }

            ItemSO item = itemsForSale[i];
            if (item == null)
            {
                ConfigureEmptySlot(slot, slotButton);
                continue;
            }

            bool canAfford = PlayerPrefs.GetFloat("Gold", 0f) >= item.Cost;
            slotButton.interactable = canAfford;

            // Name text
            TextMeshProUGUI nameText = slot.Find("ItemNameText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI costText = slot.Find("CostText")?.GetComponent<TextMeshProUGUI>();

            if (localizer != null)
            {
                if (nameText != null)
                {
                    nameText.text = $"{localizer.GetLocalizedText(item.Rarity.ToString())} {localizer.GetLocalizedText(item.ItemName)}";
                }

                if (costText != null)
                {
                    costText.text = $"{item.Cost}{localizer.GetLocalizedText(" Gold")}";
                }
            }
            else
            {
                Debug.LogWarning($"[{nameof(Shop)}] Localizer.Instance is null. Item texts will not be localized.");
                if (nameText != null)
                {
                    nameText.text = $"{item.Rarity} {item.ItemName}";
                }

                if (costText != null)
                {
                    costText.text = $"{item.Cost} Gold";
                }
            }

            if (nameText != null && costText != null)
            {
                costText.fontSize = nameText.fontSize;
            }

            // Icon / rarity visuals
            Image bgImage = slot.childCount > 1 ? slot.GetChild(1).GetComponent<Image>() : null;
            Image iconImage = slot.childCount > 4 ? slot.GetChild(4).GetComponent<Image>() : null;
            TextMeshProUGUI modifierText = slot.childCount > 5
                ? slot.GetChild(5).GetChild(0).GetComponent<TextMeshProUGUI>()
                : null;
            GameObject purchasedFlag = slot.childCount > 6 ? slot.GetChild(6).gameObject : null;

            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = item.Sprite;
            }

            if (lobbyAssets != null)
            {
                int rarityIndex = Mathf.Clamp((int)item.Rarity, 0, lobbyAssets.RarityColors.Length - 1);
                Color rarityColor = lobbyAssets.RarityColors[rarityIndex];

                if (bgImage != null)
                {
                    bgImage.color = rarityColor;
                }

                if (iconImage != null)
                {
                    iconImage.color = rarityColor;
                }
            }

            if (modifierText != null)
            {
                modifierText.text = $"+{item.EffectModifier}";
            }

            if (purchasedFlag != null)
            {
                purchasedFlag.SetActive(false);
            }

            // Clear old listeners to avoid duplicates, then add fresh handler
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(() => PrepareToBuyItem(item, slotButton));
        }
    }

    /// <summary>
    /// Rebuilds the current display using the cached item list (for localization refresh, etc.).
    /// </summary>
    private void UpdateDisplay()
    {
        DisplayItems(items);
    }

    #endregion

    #region Shop Actions

    private void OnLeaveButtonClicked()
    {
        Debug.Log("[Shop] Leaving shop.");

        if (authenticateUI != null)
        {
            authenticateUI.Show();
        }

        if (soundController != null)
        {
            soundController.MusicTransition(5);
        }

        Hide();
    }

    private void PrepareToBuyItem(ItemSO item, Button slotButton)
    {
        if (Tutorial.Singleton == null)
        {
            Debug.LogError($"[{nameof(Shop)}] Tutorial.Singleton is null. Cannot show item buy window.");
            return;
        }

        Tutorial.Singleton.ShowItemBuy(item, slotButton);
    }

    /// <summary>
    /// Finalizes purchase of an item from a slot (called after confirmation).
    /// </summary>
    public void BuyItem(ItemSO item, Button slotButton)
    {
        if (item == null || slotButton == null)
        {
            Debug.LogError($"[{nameof(Shop)}] BuyItem called with null item or slotButton.");
            return;
        }

        // Disable this button (or remove the item from the shop UI)
        slotButton.interactable = false;

        float currentGold = PlayerPrefs.GetFloat("Gold", 0f);
        PlayerPrefs.SetFloat("Gold", currentGold - item.Cost);
        PlayerPrefs.SetInt($"{item.Rarity} {item.ItemName}", 1);

        if (inventory == null)
        {
            inventory = GetComponent<Inventory>();
        }

        if (inventory != null)
        {
            inventory.BuyItem(item);
        }
        else
        {
            Debug.LogError($"[{nameof(Shop)}] Inventory component is missing; cannot add purchased item.");
        }

        if (Inventory.Instance != null)
        {
            Inventory.Instance.EquipNewItem();
        }
        else
        {
            Debug.LogWarning($"[{nameof(Shop)}] Inventory.Singleton is null; EquipNewItem was not called.");
        }

        // Show purchased marker if present
        if (slotButton.transform.childCount > 6)
        {
            slotButton.transform.GetChild(6).gameObject.SetActive(true);
        }
    }

    private void ConfigureEmptySlot(Transform slot, Button slotButton)
    {
        slotButton.interactable = false;

        TextMeshProUGUI nameText = slot.Find("ItemNameText")?.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI costText = slot.Find("CostText")?.GetComponent<TextMeshProUGUI>();
        Image iconImage = slot.childCount > 4 ? slot.GetChild(4).GetComponent<Image>() : null;
        Image bgImage = slot.childCount > 1 ? slot.GetChild(1).GetComponent<Image>() : null;

        if (localizer != null && nameText != null)
        {
            nameText.text = localizer.GetLocalizedText("Sold out");
        }
        else if (nameText != null)
        {
            nameText.text = "Sold out";
        }

        if (costText != null)
        {
            costText.text = string.Empty;
        }

        if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        if (lobbyAssets != null && bgImage != null && lobbyAssets.RarityColors.Length > 0)
        {
            bgImage.color = lobbyAssets.RarityColors[lobbyAssets.RarityColors.Length - 1];
        }
    }

    #endregion

    #region Visibility

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    #endregion
}

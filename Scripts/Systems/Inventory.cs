using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the player's owned and equipped items,
/// synchronizes them with PlayerPrefs,
/// and updates the inventory/shop UI accordingly.
/// </summary>
public class Inventory : MonoBehaviour
{
    #region SINGLETON

    /// <summary>
    /// Global reference to the active Inventory instance.
    /// </summary>
    public static Inventory Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[Inventory] Duplicate Inventory instance detected. Destroying this one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        LangCon.RefreshRequested += UpdateUI;
    }

    #endregion

    #region SERIALIZED_FIELDS

    [Header("All Potential Items")]
    [Tooltip("The full list of items that can appear in the shop or be acquired.")]
    public ItemSO[] allItems { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI itemCountText;
    [SerializeField] private Button nextItemButton;
    [SerializeField] private Button backItemButton;
    [SerializeField] private Button rerollButton;
    [SerializeField] private Image equippedItemDisplay;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemDescriptionText;

    [Header("External Scripts")]
    [Tooltip("Reference to the Shop script, if it's on the same GameObject, or drag it in from the Inspector.")]
    [SerializeField] private Shop shop;

    #endregion

    #region RUNTIME_STATE

    /// <summary>
    /// Items currently owned by the player.
    /// </summary>
    private readonly List<ItemSO> ownedItems = new List<ItemSO>();

    /// <summary>
    /// Items not yet owned by the player (used for the shop pool).
    /// </summary>
    private readonly List<ItemSO> unownedItems = new List<ItemSO>();

    /// <summary>
    /// Currently equipped item (if any).
    /// </summary>
    private ItemSO equippedItem;

    /// <summary>
    /// Index into <see cref="ownedItems"/> of the equipped item; -1 means "no item equipped".
    /// </summary>
    private int equippedIndex = -1;

    /// <summary>
    /// Default/blank sprite to show when no item is equipped.
    /// </summary>
    private Sprite defaultItemSprite;

    #endregion

    #region UNITY_LIFECYCLE

    private void Start()
    {
        if (!equippedItemDisplay)
        {
            Debug.LogWarning("[Inventory] Equipped item display is not assigned.");
        }
        else
        {
            // Cache the blank display sprite for when no item is equipped
            defaultItemSprite = equippedItemDisplay.sprite;
        }

        // Hook up button events
        if (nextItemButton != null)
            nextItemButton.onClick.AddListener(() => ChangeEquippedItem(1));
        else
            Debug.LogWarning("[Inventory] Next item button is not assigned.");

        if (backItemButton != null)
            backItemButton.onClick.AddListener(() => ChangeEquippedItem(-1));
        else
            Debug.LogWarning("[Inventory] Back item button is not assigned.");

        InitializeInventory();
    }

    private void OnDestroy()
    {
        LangCon.RefreshRequested -= UpdateUI;
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region INITIALIZATION

    /// <summary>
    /// Initializes owned/unowned items, restores equipped state, updates UI,
    /// and initializes the shop with unowned items.
    /// </summary>
    private void InitializeInventory()
    {
        // 1) Determine which items are owned and which (if any) is equipped.
        int initialIndex;
        List<ItemSO> purchasedItems = GeneratePurchasedItems(out equippedItem, out initialIndex);

        // 2) Store purchased items so we can navigate them.
        ownedItems.Clear();
        ownedItems.AddRange(purchasedItems);
        equippedIndex = initialIndex; // might be -1 if nothing was equipped

        // 3) Update UI (equipped item, counts, etc.).
        UpdateUI();

        // 4) Initialize the shop with the unowned items.
        if (shop == null)
        {
            shop = GetComponent<Shop>();
        }

        if (shop != null)
        {
            shop.StartUp(unownedItems);
        }
        else
        {
            Debug.LogWarning("[Inventory] No Shop script assigned or found.");
        }

        // 5) Reroll button availability
        float currentGold = PlayerPrefs.GetFloat("Gold", 0);
        if (rerollButton != null)
        {
            rerollButton.interactable = currentGold >= 10f;
        }
    }

    #endregion

    #region INVENTORY_BUILDING

    /// <summary>
    /// Builds a list of purchased items by reading PlayerPrefs for each item.
    /// State 1 = purchased, State 2 = purchased + equipped.
    /// Any item with state 0 is not owned and is placed into <see cref="unownedItems"/>.
    /// </summary>
    public List<ItemSO> GeneratePurchasedItems(out ItemSO equippedItem, out int equippedIndex)
    {
        unownedItems.Clear();
        List<ItemSO> purchasedList = new List<ItemSO>();
        equippedItem = null;
        equippedIndex = -1;

        if (allItems == null || allItems.Length == 0)
        {
            Debug.LogWarning("[Inventory] No items defined in 'allItems'.");
            return purchasedList;
        }

        // Loop through all items
        for (int i = 0; i < allItems.Length; i++)
        {
            ItemSO currentItem = allItems[i];
            if (currentItem == null)
            {
                Debug.LogWarning($"[Inventory] Null ItemSO at index {i} in allItems.");
                continue;
            }

            // Use a unique key or item name for PlayerPrefs
            string itemKey = currentItem.Rarity + " " + currentItem.ItemName;
            int state = PlayerPrefs.GetInt(itemKey, 0); // 0 if not found

            switch (state)
            {
                case 1:
                    // Mark as purchased
                    purchasedList.Add(currentItem);
                    break;

                case 2:
                    // Mark as purchased AND equipped
                    purchasedList.Add(currentItem);
                    // If multiple items are set to 2, we keep the last encountered
                    equippedItem = currentItem;
                    equippedIndex = purchasedList.Count - 1;
                    if (PlayerStats.Instance != null)
                    {
                        PlayerStats.Instance.Item = equippedItem;
                    }
                    break;

                default:
                    // 0 or unrecognized means not purchased
                    unownedItems.Add(currentItem);
                    break;
            }
        }

        return purchasedList;
    }

    #endregion

    #region EQUIPPING_LOGIC

    /// <summary>
    /// Moves the equipped item index by <paramref name="step"/> (e.g. +1 or -1), wrapping around.
    /// If the new index is -1, no item is equipped (cleared).
    /// If the new index is out of range, it wraps to -1 (meaning no item).
    /// </summary>
    private void ChangeEquippedItem(int step)
    {
        // Clear currently-equipped flag in PlayerPrefs (if any)
        if (equippedItem != null)
        {
            PlayerPrefs.SetInt(equippedItem.Rarity + " " + equippedItem.ItemName, 1);
        }

        if (ownedItems.Count == 0)
        {
            // No items at all
            equippedIndex = -1;
            equippedItem = null;
            UpdateUI();
            return;
        }

        equippedIndex += step;

        // If we go below -1, wrap to the last item
        // If we go above the last item, wrap to -1 (no item)
        if (equippedIndex < -1)
        {
            equippedIndex = ownedItems.Count - 1;
        }
        else if (equippedIndex >= ownedItems.Count)
        {
            equippedIndex = -1;
        }

        // Determine new equipped item
        equippedItem = (equippedIndex == -1) ? null : ownedItems[equippedIndex];
        UpdateUI();

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.Item = equippedItem;
        }

        if (equippedItem != null)
        {
            PlayerPrefs.SetInt(equippedItem.Rarity + " " + equippedItem.ItemName, 2);
        }
    }

    /// <summary>
    /// Equip the most recently acquired item.
    /// </summary>
    public void EquipNewItem()
    {
        if (ownedItems.Count == 0)
        {
            equippedIndex = -1;
            equippedItem = null;
        }
        else
        {
            equippedIndex = ownedItems.Count - 1;
            equippedItem = ownedItems[equippedIndex];
        }

        UpdateUI();

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.Item = equippedItem;
        }

        if (equippedItem != null)
        {
            PlayerPrefs.SetInt(equippedItem.Rarity + " " + equippedItem.ItemName, 2);
        }
    }

    #endregion

    #region UI_UPDATES

    /// <summary>
    /// Updates the UI to reflect the current gold, item count, and equipped item info.
    /// </summary>
    public void UpdateUI()
    {
        // Update gold
        if (goldText != null)
        {
            goldText.text = PlayerPrefs.GetFloat("Gold", 0).ToString("F0");
        }

        // Show "equippedIndex + 1 / ownedItems.Count" or "0 / 0" if no items exist
        if (itemCountText != null)
        {
            if (ownedItems.Count > 0)
            {
                int displayIndex = (equippedIndex == -1) ? 0 : (equippedIndex + 1);
                itemCountText.text = $"{displayIndex}/{ownedItems.Count}";
            }
            else
            {
                itemCountText.text = "0/0";
            }
        }

        // Update the item info UI
        if (equippedItem != null)
        {
            if (itemNameText != null)
            {
                itemNameText.text =
                    Localizer.Instance.GetLocalizedText(equippedItem.Rarity.ToString()) + " " +
                    Localizer.Instance.GetLocalizedText(equippedItem.ItemName);
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text =
                    $"{Localizer.Instance.GetLocalizedText(equippedItem.Description)} +{equippedItem.EffectModifier}";
            }

            if (equippedItemDisplay != null)
            {
                equippedItemDisplay.sprite = equippedItem.Sprite;
                equippedItemDisplay.color = LobbyAssets.Instance.RarityColors[(int)equippedItem.Rarity];
            }
        }
        else
        {
            if (itemNameText != null)
            {
                itemNameText.text = Localizer.Instance.GetLocalizedText("Nothing");
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = string.Empty;
            }

            if (equippedItemDisplay != null)
            {
                equippedItemDisplay.sprite = defaultItemSprite;
                equippedItemDisplay.color = Color.white;
            }
        }

        // Reroll button availability
        if (rerollButton != null)
        {
            float gold = PlayerPrefs.GetFloat("Gold", 0);
            rerollButton.interactable = gold >= 10f;
        }
    }

    #endregion

    #region SHOP_INTERACTIONS

    /// <summary>
    /// Called by the shop when the player buys a new item.
    /// Adds it to <see cref="ownedItems"/> if not already present and refreshes the UI.
    /// </summary>
    public void BuyItem(ItemSO item)
    {
        if (item == null)
        {
            Debug.LogWarning("[Inventory] Attempted to buy a null item.");
            return;
        }

        if (!ownedItems.Contains(item))
        {
            ownedItems.Add(item);
        }

        UpdateUI();
    }

    /// <summary>
    /// Rerolls the shop selection at the cost of 10 gold.
    /// </summary>
    public void Reroll()
    {
        float currentGold = PlayerPrefs.GetFloat("Gold", 0);
        if (currentGold < 10f)
        {
            Debug.LogWarning("[Inventory] Not enough gold to reroll shop.");
            return;
        }

        PlayerPrefs.SetFloat("Gold", currentGold - 10f);

        Tutorial.Singleton.CloseItemBuy();

        InitializeInventory();
        if (shop != null)
        {
            shop.Show();
        }
    }

    #endregion
}

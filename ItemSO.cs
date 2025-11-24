using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Items/Item")]
public class ItemSO : ScriptableObject
{
    #region Item Data

    [Header("General")]
    [Tooltip("Display name of the item.")]
    [SerializeField] private string itemName;

    [Tooltip("Rarity tier of the item.")]
    [SerializeField] private Rarity rarity;

    [Tooltip("Icon used to represent this item in UI.")]
    [SerializeField] private Sprite sprite;

    [Header("Effect")]
    [Tooltip("Effect applied when this item is equipped or used.")]
    [SerializeField] private ItemEffect effect = ItemEffect.None;

    [Tooltip("Magnitude of the effect (e.g., +5 damage, +10% def).")]
    [SerializeField] private int effectModifier;

    [Header("Cost & Usage")]
    [Min(0)]
    [Tooltip("Base gold cost of this item.")]
    [SerializeField] private int cost;

    [Tooltip("Whether this item can be actively used (e.g., in battle).")]
    [SerializeField] private bool isUsable;

    [Header("Descriptions")]
    [Tooltip("Description shown in tooltips or item details.")]
    [TextArea]
    [SerializeField] private string description;

    [Tooltip("Instructions or extra notes on how to use this item.")]
    [TextArea]
    [SerializeField] private string usage;

    #endregion

    #region Properties

    /// <summary>Display name of the item.</summary>
    public string ItemName => itemName;

    /// <summary>Effect type applied by this item.</summary>
    public ItemEffect Effect => effect;

    /// <summary>Numeric modifier for the effect (positive, negative, or zero).</summary>
    public int EffectModifier => effectModifier;

    /// <summary>Rarity tier of this item.</summary>
    public Rarity Rarity => rarity;

    /// <summary>Icon used to represent this item in UI.</summary>
    public Sprite Sprite => sprite;

    /// <summary>Base gold cost of this item.</summary>
    public int Cost => cost;

    /// <summary>Indicates whether this item can be actively used.</summary>
    public bool Usable => isUsable;

    /// <summary>Localized or user-facing description.</summary>
    public string Description => description;

    /// <summary>Usage instructions or extra notes.</summary>
    public string Usage => usage;

    #endregion
}

/// <summary>
/// Effect an item can apply, either passively (while owned/equipped)
/// or actively (when used).
/// </summary>
public enum ItemEffect
{
    None,

    // Passive bonuses
    Passive_Damage_Bonus,
    Passive_Defense_Bonus,
    Passive_Health_Bonus,
    Passive_Regeneration,
    Passive_Poison_Resistance,

    // Active effects
    Active_Healing,
    Active_TempDef,
    Active_Regen,
    Active_DMG_Bonus,

    Active_DMG_Other,
    Active_Poison_Other,

    // Misc
    Passive_Reward_Bonus,
}

/// <summary>
/// Rarity tier of an item.
/// Higher tiers generally indicate stronger or more unique items.
/// </summary>
public enum Rarity
{
    Poor,       // 0
    Common,     // 1
    Uncommon,   // 2
    Rare,       // 3
    Superior,   // 4
    Epic,       // 5
    Legendary,  // 6
    Mythic,     // 7
    Ancient,    // 8
    Divine,     // 9
    Artifact    // 10
}

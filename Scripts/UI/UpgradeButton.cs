using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents a button that upgrades either:
/// - a specific <see cref="ActionButton"/> (unlocking an ability), or
/// - a player stat (Health, Damage, Crit, etc.).
/// The cost increases based on the current upgrade level.
/// </summary>
public class UpgradeButton : MonoBehaviour
{
    #region Inspector – Upgrade Targets

    [Header("Action Upgrade (Optional)")]
    [Tooltip("If assigned, this button upgrades/unlocks the specified ActionButton (e.g., a special attack).")]
    public ActionButton ActionButton;

    [Header("Special Stat Upgrade (Optional)")]
    [Tooltip("If no ActionButton is assigned, this key indicates which stat to upgrade (e.g., 'Health', 'Damage', 'Crit_Chance', 'ClassSpecial').")]
    [SerializeField] private string specialStatKey;

    [Tooltip("How many times this special stat has already been upgraded.")]
    [SerializeField] private int upgradeLevel;

    #endregion

    #region Inspector – Cost

    [Header("Upgrade Costs")]
    [Tooltip("Base starting cost for this upgrade.")]
    [SerializeField] private int startingCost;

    [Tooltip("Calculated cost for the next upgrade, based on level or class.")]
    [SerializeField] private int cost;

    /// <summary>
    /// The current cost to purchase this upgrade.
    /// </summary>
    public int Cost
    {
        get => cost;
        private set => cost = value;
    }

    #endregion

    #region Inspector – UI

    [Header("UI References")]
    [Tooltip("The clickable button component for this upgrade.")]
    public Button Button;

    [Tooltip("Displays the cost of this upgrade.")]
    [SerializeField] private TextMeshProUGUI costText;

    [Tooltip("Displays the name of the action or special stat being upgraded.")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("Displays the level of the action or special stat.")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("Displays a brief description or effect for the upgrade.")]
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Tooltip("Displays secondary info, such as damage range or stat increment.")]
    [SerializeField] private TextMeshProUGUI secondaryInfoText;

    [Tooltip("Icon or image representing this upgrade.")]
    [SerializeField] private Image iconImage;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        // Small delay so external systems (PlayerStats, Loadout, etc.) have time to initialize.
        Invoke(nameof(InitializeDelayed), 0.1f);
    }

    /// <summary>
    /// Initializes cost and UI state after a short delay.
    /// </summary>
    private void InitializeDelayed()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogError($"[{nameof(UpgradeButton)}] PlayerStats.Instance is null. UpgradeButton will not function correctly.", this);
            return;
        }

        // Action-based upgrade branch
        if (ActionButton != null)
        {
            if (ActionButton.Unlocked)
            {
                gameObject.SetActive(false);
                return;
            }

            // Preserve original behavior: use the inspector-set Cost as the starting reference.
            int initialInspectorCost = Cost;

            string playerClassName = PlayerStats.Instance.playerClass.ToString();
            int classLevel = PlayerPrefs.GetInt(playerClassName + "_Level", 0);

            // Original scaling formula (commented out in your code) is kept as a hint:
            // Cost = (classLevel * (initialInspectorCost * 10)) / 100;

            if (Cost < initialInspectorCost)
            {
                Cost = initialInspectorCost;
            }
        }

        UpdateValues();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Called when the user clicks this Upgrade button.
    /// Deducts cost from the player's upgrade points and applies the upgrade.
    /// </summary>
    public void OnUpgradePressed()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogError($"[{nameof(UpgradeButton)}] PlayerStats.Instance is null. Cannot process upgrade.", this);
            return;
        }

        if (Button != null)
        {
            // Disable the button to prevent double-clicks while processing.
            Button.interactable = false;
        }

        // Deduct the upgrade cost
        PlayerStats.Instance.UpgradePoints -= Cost;

        // Action upgrade vs. stat upgrade
        if (ActionButton != null)
        {
            // Unlock the associated action
            ActionButton.Unlocked = true;
            gameObject.SetActive(false);
        }
        else
        {
            // Apply stat upgrade
            ApplySpecialStatUpgrade();
        }

        // Update other UI that depends on the player’s stats
        if (LoadoutUIController.Instance != null)
        {
            LoadoutUIController.Instance.UpdateUpgrades();
            LoadoutUIController.Instance.UpdateStatsUpgradeMenu();
        }
    }

    /// <summary>
    /// Rebuilds all text, icon, and cost visuals for this upgrade.
    /// Call whenever related stats or unlock state changes.
    /// </summary>
    public void UpdateValues()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogError($"[{nameof(UpgradeButton)}] PlayerStats.Instance is null. Cannot update values.", this);
            return;
        }

        if (ActionButton != null)
        {
            UpdateActionUpgradeValues();
        }
        else
        {
            UpdateSpecialStatUpgradeValues();
        }

        // Cost label
        if (costText != null)
        {
            costText.text = Cost.ToString();
        }

        // Enable/Disable based on whether the player can afford the upgrade
        if (Button != null)
        {
            Button.interactable = PlayerStats.Instance.UpgradePoints >= Cost;
        }
    }

    #endregion

    #region Internal – Action Upgrade Display

    /// <summary>
    /// Updates UI for an action-based upgrade (unlocking an ActionButton).
    /// </summary>
    private void UpdateActionUpgradeValues()
    {
        if (ActionButton == null || ActionButton.ActionSO == null)
        {
            Debug.LogWarning($"[{nameof(UpgradeButton)}] ActionButton or ActionSO missing on action upgrade.", this);
            return;
        }

        // Title
        if (titleText != null)
        {
            titleText.text = Localizer.Instance.GetLocalizedText(ActionButton.ActionSO.ActionName);
        }

        // For now, actions are treated as level 1 (no internal leveling logic here)
        if (levelText != null)
        {
            levelText.text = $"{Localizer.Instance.GetLocalizedText("Lv.")}1";
        }

        // Secondary info – damage range, modified by player stats
        if (secondaryInfoText != null)
        {
            int minDamage = (ActionButton.ActionSO.PrimaryMinDamage + PlayerStats.Instance.MinDamage) *
                            (PlayerStats.Instance.Damage + 1);

            int maxDamage = ActionButton.ActionSO.PrimaryMaxDamage *
                            (PlayerStats.Instance.Damage + 1);

            // Class special synergies
            if (PlayerStats.Instance.classSpecialUnlocked &&
                PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.PoisonAttacks &&
                ActionButton.ActionSO.PrimaryEffect == ActionEffect.Poison)
            {
                maxDamage += (1 * (PlayerStats.Instance.Damage + 1));
            }

            if (PlayerStats.Instance.classSpecialUnlocked &&
                PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.DefenceSteal &&
                ActionButton.ActionSO.PrimaryEffect == ActionEffect.Buff_Defence)
            {
                maxDamage += (1 * (PlayerStats.Instance.Damage + 1));
            }

            secondaryInfoText.text = $"{minDamage} - {maxDamage}";
        }

        // Description
        if (descriptionText != null)
        {
            descriptionText.text = Localizer.Instance.GetLocalizedText(ActionButton.ActionSO.Description);
        }

        // Icon
        if (iconImage != null)
        {
            iconImage.sprite = ActionButton.ActionSO.Icon;
        }
    }

    #endregion

    #region Internal – Special Stat Upgrade Display

    /// <summary>
    /// Updates UI for a stat-based upgrade (Health, Damage, etc.).
    /// </summary>
    private void UpdateSpecialStatUpgradeValues()
    {
        // Compute cost for this level: (level + 1) * startingCost
        Cost = (upgradeLevel + 1) * startingCost;

        // Name / title
        if (titleText != null && titleText.text != "ClassSpecial")
        {
            // e.g. "Health", "Damage", "Crit_Chance", etc.
            titleText.text = Localizer.Instance.GetLocalizedText(specialStatKey);
        }

        // Level label
        if (levelText != null)
        {
            levelText.text = $"{Localizer.Instance.GetLocalizedText("Lv.")}{upgradeLevel}";
        }

        // Secondary info – what this upgrade gives per rank
        if (secondaryInfoText != null)
        {
            switch (specialStatKey)
            {
                case "Health":
                    secondaryInfoText.text = $"+{PlayerStats.Instance.ClassHealth}";
                    break;

                case "Crit_Chance":
                    secondaryInfoText.text = "+5";
                    break;

                case "Damage":
                    secondaryInfoText.text = "+1";
                    break;

                case "Defence":
                    // Same formula used in SpecialUpgrade, but shown as the gain for this rank
                    int defenceStart = PlayerStats.Instance.preDefence + upgradeLevel;
                    int defenceUpgrade = defenceStart * defenceStart - defenceStart + 1;
                    secondaryInfoText.text = $"+{defenceUpgrade}";
                    break;

                case "Action_Points":
                    secondaryInfoText.text = "+1";
                    break;

                default:
                    secondaryInfoText.text = "+?";
                    break;
            }
        }

        // Description text
        if (descriptionText != null)
        {
            if (specialStatKey != "ClassSpecial")
            {
                descriptionText.text = Localizer.Instance.GetLocalizedText(
                    $"Upgrades your {specialStatKey} stat.");
            }
            else
            {
                string descriptionKey = "Error";

                // Note: These strings are fed into Localizer; keep or adjust keys as needed.
                switch (PlayerStats.Instance.choosenClass.SpecialAbility)
                {
                    case ClassSpecial.DefenceSteal:
                        descriptionKey = "Unlocks the passive ability to gain defence from attacks.";
                        break;
                    case ClassSpecial.InfiniteRange:
                        descriptionKey = "Increases the range of all of your attacks.";
                        break;
                    case ClassSpecial.MinDamageIncrease:
                        descriptionKey = "Increases the minimum damage you deal.";
                        break;
                    case ClassSpecial.AutoHeal:
                        descriptionKey = "Unlocks the passive ability to heal from healing spells you perform.";
                        break;

                    case ClassSpecial.DefenceBoost:
                        descriptionKey = "Unlocks the passive ability to no longer lose temporary defence and increases defence recovery.";
                        break;
                    case ClassSpecial.PoisonAttacks:
                        descriptionKey = "Unlocks the passive ability to inflict poison damage on attacks.";
                        break;
                    case ClassSpecial.JesterFix:
                        descriptionKey = "Unlocks the passive ability to remove negative effects from your abilities.";
                        break;
                    case ClassSpecial.Lifesteal:
                        descriptionKey = "Unlocks the passive ability to heal from the damage you deal.";
                        break;
                }

                descriptionText.text = Localizer.Instance.GetLocalizedText(descriptionKey);
            }
        }
    }

    #endregion

    #region Internal – Special Upgrade Logic

    /// <summary>
    /// Applies the selected special stat upgrade to the player's stats
    /// (e.g., +Health, +Defence, +Action Points, etc.).
    /// Also increments <see cref="upgradeLevel"/> and recomputes <see cref="Cost"/>.
    /// </summary>
    private void ApplySpecialStatUpgrade()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogError($"[{nameof(UpgradeButton)}] PlayerStats.Instance is null. Cannot apply special upgrade.", this);
            return;
        }

        switch (specialStatKey)
        {
            case "Health":
                PlayerStats.Instance.MaxHealth += PlayerStats.Instance.ClassHealth;
                break;

            case "Defence":
                // Same non-linear scaling as before.
                int defenceStart = PlayerStats.Instance.preDefence + upgradeLevel;
                PlayerStats.Instance.Defence += defenceStart * defenceStart - defenceStart + 1;
                break;

            case "Action_Points":
                PlayerStats.Instance.MaxActionPoints += 1;
                break;

            case "Damage":
                PlayerStats.Instance.Damage += 1;
                if (BattleHUDController.Instance != null)
                {
                    BattleHUDController.Instance.UpdateActionsValues();
                }
                break;

            case "Crit_Chance":
                PlayerStats.Instance.CritChance += 5;
                break;

            default:
                // Treat anything else as a "ClassSpecial" style purchase
                gameObject.SetActive(false);
                if (LoadoutUIController.Instance != null)
                {
                    LoadoutUIController.Instance.ClassSpecialBuy();
                }
                break;
        }

        // Increment level and recompute cost
        upgradeLevel++;
        Cost = (upgradeLevel + 1) * startingCost;

        // Hard cap: hide Crit_Chance upgrade after 20 levels
        if (specialStatKey == "Crit_Chance" && upgradeLevel >= 20)
        {
            gameObject.SetActive(false);
        }
    }

    #endregion
}

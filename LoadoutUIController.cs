using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadoutUIController : MonoBehaviour
{
    #region Singleton

    public static LoadoutUIController Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(LoadoutUIController)}] Multiple instances detected. Keeping the first one.");
            return;
        }

        Instance = this;
    }

    #endregion

    #region Inspector Fields

    [Header("Classes & Quest")]
    [Tooltip("All available player classes (ScriptableObjects).")]
    public ClassSO[] classes;

    [Tooltip("Index / ID of the selected quest. Used mainly for UI and setup.")]
    public int QuestNumber;

    [Header("UI Roots")]
    [Tooltip("Root object for the class selection scene/panel.")]
    [SerializeField] private GameObject classScene;

    [Tooltip("Parent transform that contains all UpgradeButton children.")]
    [SerializeField] private Transform gameUpgrades;

    [Header("Upgrade Buttons")]
    [Tooltip("Runtime list of active stat/ability upgrades available to the player.")]
    [SerializeField] private List<UpgradeButton> upgrades = new List<UpgradeButton>();

    [Tooltip("Optional array of upgrade buttons tied specifically to actions.")]
    [SerializeField] private UpgradeButton[] actionUpgrades;

    [Header("Navigation / Done")]
    [Tooltip("Button used to confirm upgrades and proceed to the next phase.")]
    public Button doneUpgradeButton;

    #endregion

    #region State

    private bool _upgradesSetUp;          // currently unused, kept for future extension
    private bool _playersSynchronized;
    private bool _pendingTurnOver;

    private RewardUIController RewardUI => RewardUIController.Instance;
    private UIController UI => UIController.Instance;
    private BattleHUDController BattleHUD => BattleHUDController.Instance;

    #endregion

    #region Class Selection & Setup

    /// <summary>
    /// Called when the player chooses a class by name (e.g., "Warrior").
    /// Sets up PlayerStats, actions, upgrades, and transitions to the upgrade screen.
    /// </summary>
    public void ChoosenClass(string newClass)
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogError($"[{nameof(LoadoutUIController)}] PlayerStats.Instance is null; cannot set class.");
            return;
        }

        if (!Enum.TryParse(newClass, true, out PlayerClasses parsedClass))
        {
            Debug.LogError($"[{nameof(LoadoutUIController)}] Failed to parse class name '{newClass}'.");
            return;
        }

        PlayerStats.Instance.playerClass = parsedClass;

        // Find the matching ClassSO
        ClassSO chosenClassSO = null;
        foreach (ClassSO cSO in classes)
        {
            if (cSO != null && parsedClass == cSO.PlayerClass)
            {
                chosenClassSO = cSO;
                break;
            }
        }

        if (chosenClassSO == null)
        {
            Debug.LogError($"[{nameof(LoadoutUIController)}] No ClassSO found for class {parsedClass}.");
            return;
        }

        // Apply base stats from the chosen class
        PlayerStats.Instance.SetUp(chosenClassSO);
        SetUpActionsAndUpgrades(chosenClassSO);

        // Setup player upgrades (based on player level for that class)
        string className = chosenClassSO.PlayerClass.ToString();
        int classLevel = PlayerPrefs.GetInt(className + "_Level", 0);
        float classXP = PlayerPrefs.GetFloat(className + "_XP", 0);

        PlayerStats.Instance.UpgradePoints += classLevel;

        if (RewardUI != null)
        {
            RewardUI.Setup(classLevel, classXP);
        }
        else
        {
            Debug.LogWarning($"[{nameof(LoadoutUIController)}] RewardUIController.Instance is null; reward fields not initialized.");
        }

        // Initialize upgrades and UI
        SetUpUpgrades();
        UpdateUpgrades();
        UpdateStats();

        // Finalize startup
        PlayerStats.Instance.StartUp();

        if (UI != null)
        {
            UI.gState = UIController.GameState.Upgrades;
            UI.Animator.SetTrigger("Trigger");
        }
        else
        {
            Debug.LogWarning($"[{nameof(LoadoutUIController)}] UIController.Instance is null; cannot set state or play animation.");
        }

        // Show or hide relevant actions based on the player's class
        if (BattleHUD != null)
        {
            BattleHUD.SetUpGameActions();
        }

        // Move focus to the done button & start tutorial
        if (doneUpgradeButton != null)
        {
            UINavigationController.Instance.JumpToElement(doneUpgradeButton);
        }

        if (Tutorial.Singleton != null)
        {
            Tutorial.Singleton.StartTutorial(0);
        }

        // Hide tutorial text for higher-level players
        if (classLevel > 5 && BattleHUD != null && BattleHUD.tutorialText != null)
        {
            BattleHUD.tutorialText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Assigns the correct actions to the Battle HUD action slots, using the chosen class's action list.
    /// </summary>
    private void SetUpActionsAndUpgrades(ClassSO myClass)
    {
        if (BattleHUD == null)
        {
            Debug.LogWarning($"[{nameof(LoadoutUIController)}] BattleHUDController.Instance is null; cannot set up actions.");
            return;
        }

        if (myClass == null)
        {
            Debug.LogWarning($"[{nameof(LoadoutUIController)}] myClass is null in SetUpActionsAndUpgrades.");
            return;
        }

        // Assign actions to the BattleHUD action slots
        for (int i = 0; i < myClass.Actions.Length && i < BattleHUD.actions.Count; i++)
        {
            if (BattleHUD.actions[i] != null)
            {
                BattleHUD.actions[i].ActionSO = myClass.Actions[i];
            }
        }

        // Optionally hook up actionUpgrades to specific actions (currently unused logic)
        for (int i = 1; i < actionUpgrades.Length; i++)
        {
            // Example (commented out in original code):
            // actionUpgrades[i].ActionButton.ActionSO = myClass.actions[i];
        }
    }

    #endregion

    #region Quest & Upgrades

    /// <summary>
    /// Called once the quest is chosen/confirmed. Sets quest data and synchronizes players.
    /// </summary>
    public void SetQuest(int newQuest)
    {
        if (RewardUI == null || BattleHUD == null)
        {
            Debug.LogWarning($"[{nameof(LoadoutUIController)}] RewardUIController or BattleHUDController is null in SetQuest.");
        }

        QuestNumber = newQuest;

        // Set quest on Reward and EnemyController
        var quest = LobbyAssets.Instance.quests[newQuest];
        if (RewardUI != null)
        {
            RewardUI.SetQuest(quest);
        }

        EnemyController.Instance.Quest = quest;

        if (doneUpgradeButton != null)
        {
            doneUpgradeButton.interactable = true;
        }

        _playersSynchronized = true;

        // Auto-finish upgrades if autoEndTurn and no upgrade points left, or if we had a pending end
        if ((PlayerStats.Instance.autoEndTurnSetting && PlayerStats.Instance.UpgradePoints <= 0) || _pendingTurnOver)
        {
            doneUpgradeButton?.onClick.Invoke();
        }

        if (BattleHUD != null && BattleHUD.waveCountText != null)
        {
            BattleHUD.waveCountText.text =
                Localizer.Instance.GetLocalizedText("Wave: ") + "\n" +
                "1 / " + quest.waves.Count;
        }
    }

    /// <summary>
    /// Initializes the upgrade list based on what's available for the chosen class and actions.
    /// </summary>
    private void SetUpUpgrades()
    {
        upgrades.Clear();

        if (gameUpgrades == null)
        {
            Debug.LogWarning($"[{nameof(LoadoutUIController)}] gameUpgrades transform is not assigned.");
            return;
        }

        foreach (UpgradeButton upgrade in gameUpgrades.GetComponentsInChildren<UpgradeButton>(true))
        {
            if (upgrade == null)
                continue;

            if (upgrade.ActionButton == null)
            {
                upgrade.UpdateValues();
                upgrades.Add(upgrade);
                upgrade.gameObject.SetActive(true);
            }
            else
            {
                // If it's tied to an action not yet unlocked and for the current class
                if (!upgrade.ActionButton.Unlocked &&
                    upgrade.ActionButton.ActionSO != null &&
                    upgrade.ActionButton.ActionSO.OwnerClass == PlayerStats.Instance.playerClass)
                {
                    upgrade.UpdateValues();
                    upgrades.Add(upgrade);
                    upgrade.gameObject.SetActive(true);
                }
                else
                {
                    upgrade.gameObject.SetActive(false);
                }
            }
        }

        _upgradesSetUp = true;
    }

    /// <summary>
    /// Refreshes all upgrade buttons' values and interactable state.
    /// </summary>
    public void UpdateUpgrades()
    {
        foreach (UpgradeButton upgrade in upgrades)
        {
            if (upgrade == null) continue;

            upgrade.UpdateValues();

            if (upgrade.Cost > PlayerStats.Instance.UpgradePoints)
            {
                if (upgrade.Button != null)
                    upgrade.Button.interactable = false;
            }
            else
            {
                if (upgrade.Button != null)
                    upgrade.Button.interactable = true;
            }
        }

        if (doneUpgradeButton != null)
        {
            UINavigationController.Instance.JumpToElement(doneUpgradeButton);
        }
    }

    /// <summary>
    /// Plays a small animation on each upgrade button in sequence.
    /// </summary>
    public IEnumerator UpgradesAnimation()
    {
        foreach (UpgradeButton upgrade in upgrades)
        {
            if (upgrade != null)
            {
                var animator = upgrade.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetTrigger("Trigger");
                }
            }

            yield return new WaitForSeconds(0.25f / PlayerStats.Instance.playSpeed);
        }
    }

    /// <summary>
    /// Updates the stats shown on the upgrade menu HUD (AP, upgrade points).
    /// Also auto-confirms upgrades if auto-end is enabled and no points remain.
    /// </summary>
    public void UpdateStatsUpgradeMenu()
    {
        if (BattleHUD != null)
        {
            if (BattleHUD.actionPointsText != null)
                BattleHUD.actionPointsText.text = PlayerStats.Instance.MaxActionPoints.ToString();

            if (BattleHUD.upgradePointsText != null)
                BattleHUD.upgradePointsText.text = PlayerStats.Instance.UpgradePoints.ToString();
        }

        if (PlayerStats.Instance.autoEndTurnSetting && PlayerStats.Instance.UpgradePoints <= 0)
        {
            if (_playersSynchronized)
            {
                doneUpgradeButton?.onClick.Invoke();
            }
            else
            {
                _pendingTurnOver = true;
            }
        }
    }

    /// <summary>
    /// Called when the class special upgrade is purchased.
    /// Flags the special as unlocked and applies any immediate stat bonuses.
    /// </summary>
    public void ClassSpecialBuy()
    {
        PlayerStats.Instance.classSpecialUnlocked = true;

        switch (PlayerStats.Instance.choosenClass.SpecialAbility)
        {
            case ClassSpecial.MinDamageIncrease:
                PlayerStats.Instance.MinDamage++;
                if (BattleHUD != null)
                    BattleHUD.UpdateActionsValues();
                break;
                // Other class specials may be handled elsewhere or passively.
        }
    }

    /// <summary>
    /// Updates AP and upgrade points text in the HUD based on PlayerStats.
    /// </summary>
    public void UpdateStats()
    {
        if (BattleHUD == null)
            return;

        if (PlayerStats.Instance.myCharacter != null)
        {
            if (BattleHUD.actionPointsText != null)
                BattleHUD.actionPointsText.text = PlayerStats.Instance.myCharacter.Stats.ActionPoints.ToString();
        }
        else
        {
            if (BattleHUD.actionPointsText != null)
                BattleHUD.actionPointsText.text = "0";
        }

        if (BattleHUD.upgradePointsText != null)
            BattleHUD.upgradePointsText.text = PlayerStats.Instance.UpgradePoints.ToString();
    }

    #endregion
}

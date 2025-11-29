using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UIController;

public class BattleHUDController : MonoBehaviour
{
    #region Singleton

    public static BattleHUDController Instance;

    private void Awake()
    {
        Instance = this;
    }

    #endregion

    #region Inspector References

    [Header("Available Action Buttons")]
    [Tooltip("All possible action buttons that can appear during battle. Filtered by class in SetUpGameActions.")]
    public List<ActionButton> gameActions;

    [Header("Active Action Buttons")]
    [Tooltip("The subset of actions actively used/managed during combat. Often configured in the Inspector.")]
    public List<ActionButton> actions = new List<ActionButton>();

    [Header("Core Buttons")]
    [Tooltip("Button used to end the player's turn.")]
    [SerializeField] public Button endTurnButton;

    [Tooltip("Button that triggers the use of the currently equipped item.")]
    public Button itemButton;

    [Header("Stat Text")]
    [SerializeField] private TextMeshProUGUI defencePointsText;
    [Tooltip("Displays current action points.")]
    public TextMeshProUGUI actionPointsText;
    [Tooltip("Displays current upgrade points.")]
    public TextMeshProUGUI upgradePointsText;
    [SerializeField] private TextMeshProUGUI healthPointsText;

    [Header("Status & Tutorial Text")]
    [Tooltip("Displays current turn number or description.")]
    public TextMeshProUGUI turnText;
    [Tooltip("Displays instructional/tutorial information for the player.")]
    public TextMeshProUGUI tutorialText;
    [Tooltip("Displays the current wave count.")]
    public TextMeshProUGUI waveCountText;

    [Header("Gain Indicators")]
    [Tooltip("Temporary +AP display text, if used.")]
    public TextMeshProUGUI actionPointPlus;
    [Tooltip("Temporary +Upgrade Points display text, if used.")]
    public TextMeshProUGUI upgradePointPlus;

    [Header("Special Summon / Clock")]
    [Tooltip("Displays text related to special summons (if applicable).")]
    public TextMeshProUGUI specialSummonText;
    [Tooltip("Clock pointer UI for turn timer.")]
    public ClockPointer clock;

    #endregion

    #region State

    [Tooltip("If true, temporarily disables game actions (e.g., while resolving an action).")]
    public bool tempDisable;

    [Tooltip("True if the board is currently active/visible.")]
    public bool BoardActive;

    [Tooltip("Tracks whether the player has used their item this turn.")]
    public bool itemUsed;

    [Tooltip("Reference to the currently opened character info panel (if any).")]
    public CharacterInfo ActiveCharacterInfo;

    private TurnTimerUIController TurnTimer => TurnTimerUIController.Instance;
    private UIController UiController => UIController.Instance;

    #endregion

    #region Setup

    /// <summary>
    /// Filters and activates action buttons based on the player's class.
    /// Should be called once when a match starts / loadout is finalized.
    /// </summary>
    public void SetUpGameActions()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning($"[{nameof(BattleHUDController)}] PlayerStats.Instance is null; cannot set up actions.");
            return;
        }

        foreach (ActionButton action in gameActions)
        {
            if (action == null || action.ActionSO == null)
                continue;

            if (action.ActionSO.OwnerClass == PlayerStats.Instance.playerClass ||
                action.ActionSO.OwnerClass == PlayerClasses.None)
            {
                action.gameObject.SetActive(true);
                action.UpdateValues();
            }
            else
            {
                action.gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region Actions & AP Handling

    /// <summary>
    /// Updates interactable state and displayed values for all action buttons and the item button,
    /// based on current Action Points and unlock state.
    /// </summary>
    public void UpdateGameActions()
    {
        if (tempDisable)
            return;

        if (PlayerStats.Instance.myCharacter != null)
        {
            // Update each action button
            foreach (ActionButton action in actions)
            {
                if (action == null || action.ActionSO == null || action.Button == null)
                    continue;

                bool canAfford = (action.ActionSO.Cost <= PlayerStats.Instance.myCharacter.Stats.ActionPoints);
                action.Button.interactable = (canAfford && action.Unlocked);
                action.UpdateValues();
            }

            // Auto end turn if no AP (if the setting is enabled)
            if (PlayerStats.Instance.myCharacter.Stats.ActionPoints <= 0 &&
                PlayerStats.Instance.autoEndTurnSetting)
            {
                if (endTurnButton != null)
                    endTurnButton.interactable = false;
            }

            // Item button logic
            if (PlayerStats.Instance.Item != null)
            {
                if (PlayerStats.Instance.myCharacter.Stats.ActionPoints > 0 &&
                    !itemUsed &&
                    PlayerStats.Instance.Item.Usable &&
                    itemButton != null)
                {
                    itemButton.interactable = true;
                }
            }
            else if (itemButton != null)
            {
                itemButton.interactable = false;
            }
        }
        else
        {
            // No character spawned; disable actions and end turn
            DisableGameActions();
            if (endTurnButton != null)
                endTurnButton.interactable = false;
        }
    }

    /// <summary>
    /// Refreshes damage/text values for all actions, assuming the player character is present.
    /// </summary>
    public void UpdateActionsValues()
    {
        foreach (ActionButton action in actions)
        {
            if (action != null)
                action.UpdateValues();
        }
    }

    /// <summary>
    /// Refreshes damage/text values for all actions for contexts where a character
    /// is not yet spawned (e.g., loadout screens).
    /// </summary>
    public void UpdateActionsValuesNoCharacter()
    {
        foreach (ActionButton action in actions)
        {
            if (action != null)
                action.UpdateValuesNoCharacter();
        }
    }

    /// <summary>
    /// Disables all action buttons and the item button.
    /// Does not alter end turn availability.
    /// </summary>
    public void DisableGameActions()
    {
        foreach (ActionButton action in actions)
        {
            if (action != null && action.Button != null)
            {
                action.Button.interactable = false;
            }
        }

        if (itemButton != null)
            itemButton.interactable = false;
    }

    #endregion

    #region Player Death & Item Use

    /// <summary>
    /// Called when the player dies; disables relevant inputs if still in battle.
    /// </summary>
    public void PlayerDeath()
    {
        if (UiController.gState != GameState.Battle)
            return;

        if (!EnemyController.Instance.enemyTurn && !TurnTimer.turnEnded)
        {
            UiController.EndTurn(false);
        }

        DisableGameActions();

        if (endTurnButton != null)
            endTurnButton.interactable = false;

        if (itemButton != null)
            itemButton.interactable = false;
    }

    /// <summary>
    /// Handles using the equipped item as an action (e.g., healing, buff, damage).
    /// </summary>
    public void ItemButtonPressed()
    {
        if (ActiveCharacterInfo != null)
        {
            ActiveCharacterInfo.HideInfo();
        }

        ItemSO item = PlayerStats.Instance.Item;
        if (item == null)
        {
            Debug.LogWarning($"[{nameof(BattleHUDController)}] ItemButtonPressed called but PlayerStats.Item is null.");
            return;
        }

        ActionTarget target = ActionTarget.Self;
        ActionEffect effect = ActionEffect.None;
        EffectVisual visual = EffectVisual.Heal;

        switch (item.Effect)
        {
            default:
                return;

            case ItemEffect.Active_DMG_Bonus:
                target = ActionTarget.Any_Ally;
                effect = ActionEffect.Buff_Damage;
                visual = EffectVisual.Heal;
                break;

            case ItemEffect.Active_DMG_Other:
                target = ActionTarget.Any;
                effect = ActionEffect.Damage;
                visual = EffectVisual.Sword;
                break;

            case ItemEffect.Active_Healing:
                target = ActionTarget.Any_Ally;
                effect = ActionEffect.Heal;
                visual = EffectVisual.Heal;
                break;

            case ItemEffect.Active_Poison_Other:
                target = ActionTarget.Any;
                effect = ActionEffect.Poison;
                visual = EffectVisual.Sword;
                break;

            case ItemEffect.Active_Regen:
                target = ActionTarget.Any_Ally;
                effect = ActionEffect.Regen;
                visual = EffectVisual.Heal;
                break;

            case ItemEffect.Active_TempDef:
                target = ActionTarget.Any_Ally;
                effect = ActionEffect.Buff_Defence;
                visual = EffectVisual.BuffDef;
                break;
        }

        var attackVars = new AttackVariables
        {
            TargetingMode = target,
            MinDamage = 0,
            MaxDamage = 0,
            ResolvedDamage = item.EffectModifier,
            Effect = effect,
            IsPlayerAction = true,
            AttackerId = GridManager.Instance.MyPlayerLocation,
            ImpactVisual = visual,
            CritMultiplier = 1,
            CritChance = 0,
            IsAllyAiAction = false,
            ActionName = item.Rarity + " " + item.ItemName,
            ActionPointCost = 1,
            TargetId = "",
            ActionId = -1,
        };

        AttackResolver.Instance.Attack(attackVars);
    }

    /// <summary>
    /// Marks the item as used and disables the item button.
    /// </summary>
    public void DisableItemButton()
    {
        if (itemButton != null)
            itemButton.interactable = false;

        itemUsed = true;
    }

    #endregion

    #region Global Interaction Toggles

    /// <summary>
    /// Disables all action buttons and the end turn button.
    /// Does not affect other UI elements.
    /// </summary>
    public void SetAllNonInteractable()
    {
        DisableGameActions();
        if (endTurnButton != null)
            endTurnButton.interactable = false;
    }

    /// <summary>
    /// Temporarily disables all action buttons and end turn, typically while
    /// resolving a chosen action to prevent spam.
    /// </summary>
    public void TemperaryActionButtonsDisable()
    {
        Debug.Log($"[{nameof(BattleHUDController)}] Temporarily disabling action buttons.");
        tempDisable = true;
        DisableGameActions();

        if (endTurnButton != null)
            endTurnButton.interactable = false;
    }

    /// <summary>
    /// Re-enables action buttons based on current AP/state, and re-enables end turn
    /// if the turn has not already ended via the timer.
    /// </summary>
    public void TemperaryActionButtonsEnable()
    {
        tempDisable = false;
        UpdateGameActions();

        if (!TurnTimer.turnEnded && endTurnButton != null)
        {
            endTurnButton.interactable = true;
        }
    }

    #endregion

    #region Utility

    /// <summary>
    /// Returns the action IDs (indices into LobbyAssets.Instance.actions)
    /// for the current class's unlocked actions.
    /// </summary>
    public int[] GetMyActionsIDs()
    {
        var actionsList = new List<ActionSO>();

        foreach (ActionButton action in gameActions)
        {
            if (action == null || action.ActionSO == null)
                continue;

            if (action.ActionSO.OwnerClass == PlayerStats.Instance.playerClass && action.Unlocked)
            {
                actionsList.Add(action.ActionSO);
            }
        }

        int[] ids = actionsList
            .Select(a => System.Array.IndexOf(LobbyAssets.Instance.actions, a))
            .ToArray();

        return ids;
    }

    #endregion
}

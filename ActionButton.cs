using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the behavior of a combat action button in the UI:
/// - Displays the damage range and targeting shorthand (e.g., "10-15 A1").
/// - Triggers the associated action when pressed.
/// - Coordinates with the BattleHUD to disable / re-enable other actions.
/// </summary>
public class ActionButton : MonoBehaviour
{
    #region Fields

    [Header("Action Settings")]
    [Tooltip("ScriptableObject defining this action's damage, effects, and target rules.")]
    [SerializeField] private ActionSO actionSO;

    /// <summary>
    /// Determines whether the player has unlocked this action.
    /// </summary>
    [SerializeField] private bool unlocked = false;

    [Header("UI References")]
    [Tooltip("Displays the potential damage range and target code for this action.")]
    [SerializeField] private TextMeshProUGUI damageDisplay;

    [Tooltip("Icon image for this action.")]
    [SerializeField] private Image image;

    /// <summary>
    /// Short code / abbreviation for the target type (e.g., A1, F, M).
    /// Used for quick readability for players.
    /// </summary>
    private string _targetShortCode;

    #endregion

    #region Properties

    /// <summary>
    /// The underlying <see cref="ActionSO"/> for this button.
    /// </summary>
    public ActionSO ActionSO
    {
        get => actionSO;
        set => actionSO = value;
    }

    /// <summary>
    /// Indicates if this action is currently unlocked.
    /// </summary>
    public bool Unlocked
    {
        get => unlocked;
        set => unlocked = value;
    }

    /// <summary>
    /// The Unity UI Button component for this action.
    /// </summary>
    public Button Button { get; private set; }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Button = GetComponent<Button>();
        if (Button == null)
        {
            Debug.LogWarning($"[{nameof(ActionButton)}] No Button component found on '{name}'. " +
                             "This ActionButton will not be clickable.", this);
        }
    }

    private void Start()
    {
        if (actionSO == null)
        {
            Debug.LogError($"[{nameof(ActionButton)}] No ActionSO assigned on '{name}'. Disabling button.", this);
            if (Button != null) Button.interactable = false;
            return;
        }

        // Hook up click event
        if (Button != null)
        {
            Button.onClick.AddListener(OnButtonPressed);
        }

        // Convert the ActionTarget enum to a short code for display
        _targetShortCode = GetTargetShortCode(actionSO.PrimaryTarget);

        // Some actions may start unlocked by design
        if (actionSO.StartUnlocked)
        {
            unlocked = true;
        }

        UpdateValues();
    }

    #endregion

    #region Button Logic

    /// <summary>
    /// Called when the UI button is pressed. Triggers the action logic and updates the HUD.
    /// </summary>
    private void OnButtonPressed()
    {
        // Hide any open character info panel
        if (BattleHUDController.Instance.ActiveCharacterInfo != null)
        {
            BattleHUDController.Instance.ActiveCharacterInfo.HideInfo();
        }

        // If this action has a secondary effect, temporarily disable other actions
        if (actionSO.SecondaryEffect != ActionEffect.None)
        {
            BattleHUDController.Instance.TemperaryActionButtonsDisable();
        }

        // For certain action targets we also do a pre-emptive spam-prevention disable
        switch (actionSO.PrimaryTarget)
        {
            case ActionTarget.All:
            case ActionTarget.All_Ally:
            case ActionTarget.All_Ally_Front:
            case ActionTarget.All_Back:
            case ActionTarget.All_Front:
            case ActionTarget.All_Front_Mid:
            case ActionTarget.All_Middle:
            case ActionTarget.Everyone:
            case ActionTarget.Self:
            case ActionTarget.Random:
            case ActionTarget.Chosen:
                BattleHUDController.Instance.TemperaryActionButtonsDisable();
                break;
        }

        // Fire the action’s effects
        var cts = new CancellationTokenSource();
        ActionEffects.Instance.ActivateEffects(
            cts.Token,
            actionSO,
            true,
            PlayerStats.Instance.myCharacter,
            null,
            true);

        if (Button != null)
        {
            Button.interactable = false;
        }

        // Refresh AP / HUD
        BattleHUDController.Instance.UpdateGameActions();
    }

    #endregion

    #region UI Updates

    /// <summary>
    /// Updates the UI display for damage range and target type.
    /// Call this whenever the player's damage or the action stats change.
    /// </summary>
    public void UpdateValues()
    {
        if (damageDisplay == null || actionSO == null)
            return;

        int tempDamage = 0;
        if (PlayerStats.Instance.myCharacter != null)
        {
            tempDamage = PlayerStats.Instance.myCharacter.Stats.TempDmg;
        }

        int minDisplay = (actionSO.PrimaryMinDamage + PlayerStats.Instance.MinDamage) *
                         (PlayerStats.Instance.Damage + tempDamage + 1);
        int maxDisplay = (actionSO.PrimaryMaxDamage) *
                         (PlayerStats.Instance.Damage + tempDamage + 1);

        // Class special tweaks
        if (PlayerStats.Instance.classSpecialUnlocked)
        {
            switch (PlayerStats.Instance.choosenClass.SpecialAbility)
            {
                case ClassSpecial.DefenceSteal:
                    if (actionSO.PrimaryEffect == ActionEffect.Buff_Defence)
                    {
                        maxDisplay += 1 * (PlayerStats.Instance.Damage + tempDamage + 1);
                    }
                    break;

                case ClassSpecial.PoisonAttacks:
                    if (actionSO.PrimaryEffect == ActionEffect.Poison)
                    {
                        maxDisplay += 1 * (PlayerStats.Instance.Damage + tempDamage + 1);
                    }
                    break;
            }
        }

        // Damage or "Move" text
        if (actionSO.PrimaryEffect == ActionEffect.Relocate)
        {
            damageDisplay.text = Localizer.Instance.GetLocalizedText("Move");
        }
        else
        {
            damageDisplay.text = $"{minDisplay}-{maxDisplay} {_targetShortCode}";
        }

        // Update icon
        if (image != null && actionSO.Icon != null)
        {
            image.sprite = actionSO.Icon;
        }
    }

    /// <summary>
    /// Variant of <see cref="UpdateValues"/> for screens that don't have a spawned character yet.
    /// </summary>
    public void UpdateValuesNoCharacter()
    {
        if (damageDisplay == null || actionSO == null)
            return;

        int minDisplay = (actionSO.PrimaryMinDamage + PlayerStats.Instance.MinDamage) *
                         (PlayerStats.Instance.Damage + 1);
        int maxDisplay = (actionSO.PrimaryMaxDamage) *
                         (PlayerStats.Instance.Damage + 1);

        if (PlayerStats.Instance.classSpecialUnlocked)
        {
            switch (PlayerStats.Instance.choosenClass.SpecialAbility)
            {
                case ClassSpecial.DefenceSteal:
                    if (actionSO.PrimaryEffect == ActionEffect.Buff_Defence)
                    {
                        maxDisplay += 1 * (PlayerStats.Instance.Damage + 1);
                    }
                    break;

                case ClassSpecial.PoisonAttacks:
                    if (actionSO.PrimaryEffect == ActionEffect.Poison)
                    {
                        maxDisplay += 1 * (PlayerStats.Instance.Damage + 1);
                    }
                    break;
            }
        }

        if (actionSO.PrimaryEffect == ActionEffect.Relocate)
        {
            damageDisplay.text = Localizer.Instance.GetLocalizedText("Move");
        }
        else
        {
            damageDisplay.text = $"{minDisplay}-{maxDisplay} {_targetShortCode}";
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Sets whether this action is unlocked. Does not change any PlayerStats by itself.
    /// </summary>
    public void SetUnlocked(bool value)
    {
        unlocked = value;
        // You can add visual lock/unlock feedback here if desired.
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Maps an <see cref="ActionTarget"/> to its short code for UI display.
    /// </summary>
    private static string GetTargetShortCode(ActionTarget target)
    {
        switch (target)
        {
            case ActionTarget.Any: return "A1";
            case ActionTarget.All: return "A";
            case ActionTarget.Any_Front: return "F1";
            case ActionTarget.Any_Reverse: return "B1";
            case ActionTarget.All_Front_Mid: return "FM";
            case ActionTarget.All_Front: return "F";
            case ActionTarget.All_Back: return "B";
            case ActionTarget.All_Ally: return "A";
            case ActionTarget.All_Middle: return "M";
            case ActionTarget.Any_Ally: return "A1";
            case ActionTarget.Self: return "S";
            case ActionTarget.Random: return "R";
            case ActionTarget.Everyone: return "E";
            case ActionTarget.Any_Ranged: return "A2";
            case ActionTarget.All_Ally_Front: return "F1";
            default: return "?";
        }
    }

    #endregion
}

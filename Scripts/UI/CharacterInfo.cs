using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles displaying character info (crit, special, actions) when hovering or clicking
/// a character's UI slot.
/// </summary>
public class CharacterInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    #region Inspector

    [Header("Character / Actions")]
    [Tooltip("Character component whose stats and actions will be displayed.")]
    [SerializeField] private Character character;

    [Tooltip("UI containers for each action icon + name + damage line.")]
    [SerializeField] private Transform[] actionSlots;

    [Header("UI Elements")]
    [Tooltip("Panel holding the detailed character info UI.")]
    [SerializeField] private GameObject infoPanel;

    [Tooltip("Highlight object shown when hovering over this character.")]
    [SerializeField] private GameObject hoverHighlight;

    [Header("Text References")]
    [Tooltip("Text displaying crit chance and multiplier.")]
    [SerializeField] private TextMeshProUGUI critChanceText;

    [Tooltip("Text displaying the character's special class ability.")]
    [SerializeField] private TextMeshProUGUI specialText;

    #endregion

    #region State

    /// <summary>Internal flag toggled whenever info is shown; used as a stored hover state.</summary>
    private bool hoverActive;

    /// <summary>True if the user setting 'Always display character info' is enabled.</summary>
    private bool hoverAlwaysActive;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Fallback if no Character assigned in Inspector
        if (character == null)
        {
            character = GetComponentInParent<Character>();
            if (character == null)
            {
                Debug.LogWarning($"[{nameof(CharacterInfo)}] No Character reference found on {name} or its parents.", this);
            }
        }

        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    private void Start()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning($"[{nameof(CharacterInfo)}] PlayerStats.Instance is null. Using default hover settings.", this);
            hoverAlwaysActive = false;
        }
        else
        {
            hoverAlwaysActive = PlayerStats.Instance.alwaysDisplayCharacterInfoSetting;
        }

        if (hoverHighlight != null && hoverAlwaysActive)
        {
            hoverHighlight.SetActive(true);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Opens/closes this character's info panel, updates content, and cooperates
    /// with <see cref="BattleHUDController"/> so only one info panel is active at a time.
    /// </summary>
    public void ShowInfo()
    {
        // If the slot's parent button is interactable, treat this as clicking that button instead,
        // and close any currently open info panel.
        var parentButton = transform.parent != null
            ? transform.parent.GetComponent<Button>()
            : null;

        if (parentButton != null && parentButton.interactable)
        {
            parentButton.onClick.Invoke();

            if (BattleHUDController.Instance != null &&
                BattleHUDController.Instance.ActiveCharacterInfo != null)
            {
                BattleHUDController.Instance.ActiveCharacterInfo.HideInfo();
            }

            return;
        }

        // Toggle off if this info panel is already the active one
        if (BattleHUDController.Instance != null &&
            BattleHUDController.Instance.ActiveCharacterInfo == this)
        {
            HideInfo();
            return;
        }

        // Hide the currently active panel (if any)
        if (BattleHUDController.Instance != null &&
            BattleHUDController.Instance.ActiveCharacterInfo != null)
        {
            BattleHUDController.Instance.ActiveCharacterInfo.HideInfo();
        }

        // Mark this as the active info controller
        if (BattleHUDController.Instance != null)
        {
            BattleHUDController.Instance.ActiveCharacterInfo = this;
        }

        if (character == null || character.Stats == null)
        {
            Debug.LogWarning($"[{nameof(CharacterInfo)}] Cannot show info: Character or Stats is null on {name}.", this);
            return;
        }

        // ---- Crit / Special ----
        if (critChanceText != null)
        {
            string critLabel = Localizer.Instance.GetLocalizedText("Crit: ");
            critChanceText.text = $"{critLabel}{character.Stats.CritChance}% x{character.Stats.CritMultiplier}";
        }

        if (specialText != null)
        {
            string specialLabel = Localizer.Instance.GetLocalizedText("Special: ");
            string specialName = Localizer.Instance.GetLocalizedText(character.Stats.ClassSpecial.ToString());
            specialText.text = $"{specialLabel}{specialName}";
        }

        // ---- Actions ----
        if (actionSlots != null &&
            character.Stats.ActionIds != null &&
            LobbyAssets.Instance != null &&
            LobbyAssets.Instance.actions != null)
        {
            for (int i = 0; i < actionSlots.Length; i++)
            {
                var slot = actionSlots[i];
                if (slot == null) continue;

                if (character.Stats.ActionIds.Length <= i)
                {
                    slot.gameObject.SetActive(false);
                    continue;
                }

                int actionIndex = character.Stats.ActionIds[i];
                if (actionIndex < 0 || actionIndex >= LobbyAssets.Instance.actions.Length)
                {
                    Debug.LogWarning($"[{nameof(CharacterInfo)}] Action index {actionIndex} is out of range for {name}.", this);
                    slot.gameObject.SetActive(false);
                    continue;
                }

                ActionSO action = LobbyAssets.Instance.actions[actionIndex];
                slot.gameObject.SetActive(true);

                // Icon
                var iconImage = slot.GetChild(0).GetChild(0).GetChild(0).GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = action.Icon;
                }

                // Name
                var nameText = slot.GetChild(1).GetComponent<TextMeshProUGUI>();
                if (nameText != null)
                {
                    nameText.text = Localizer.Instance.GetLocalizedText(action.ActionName);
                }

                // Target / Damage line
                var damageText = slot.GetChild(2).GetComponent<TextMeshProUGUI>();
                if (damageText != null)
                {
                    string targetShortCode = GetTargetShortCode(action.PrimaryTarget);

                    int minDisplay = action.PrimaryMinDamage * (character.Stats.GetTotalDamageBonus() + 1);
                    int maxDisplay = action.PrimaryMaxDamage * (character.Stats.GetTotalDamageBonus() + 1);

                    damageText.text = $"{minDisplay}-{maxDisplay} {targetShortCode}";
                }
            }
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }

        if (hoverHighlight != null)
        {
            hoverHighlight.SetActive(true);
        }

        hoverActive = !hoverActive; // Preserve original toggle behavior
    }

    /// <summary>
    /// Hides the info panel and restores hover highlight state if appropriate.
    /// </summary>
    public void HideInfo()
    {
        if (BattleHUDController.Instance != null &&
            BattleHUDController.Instance.ActiveCharacterInfo == this)
        {
            BattleHUDController.Instance.ActiveCharacterInfo = null;
        }

        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        if (hoverHighlight != null && !hoverAlwaysActive)
        {
            hoverHighlight.SetActive(hoverActive);
        }
    }

    /// <summary>
    /// Called when the "always show info" setting changes at runtime.
    /// </summary>
    public void ChangeSetting()
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning($"[{nameof(CharacterInfo)}] ChangeSetting called but PlayerStats.Instance is null.", this);
            return;
        }

        hoverAlwaysActive = PlayerStats.Instance.alwaysDisplayCharacterInfoSetting;

        if (hoverHighlight == null)
            return;

        if (hoverAlwaysActive)
        {
            hoverHighlight.SetActive(true);
        }
        else
        {
            // Resets to normal hover behavior
            HideInfo();
        }
    }

    #endregion

    #region Pointer Events

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverHighlight != null)
        {
            hoverHighlight.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverHighlight != null && !hoverAlwaysActive)
        {
            hoverHighlight.SetActive(hoverActive);
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Converts <see cref="ActionTarget"/> to the short code shown in the UI (e.g. F, B, A1).
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

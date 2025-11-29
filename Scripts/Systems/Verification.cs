using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Verification : MonoBehaviour
{
    #region Fields

    [Header("Verification Codes")]
    [Tooltip("Internal code for the current verification action.")]
    [SerializeField] private int code = -1;

    [Header("References")]
    [Tooltip("Reference to the tutorial controller used for reset operations.")]
    [SerializeField] private Tutorial tutorial;

    [Tooltip("Reference to the inventory used for reroll actions.")]
    [SerializeField] private Inventory inventory;

    [Header("UI Elements")]
    [Tooltip("Text element used to display the verification message.")]
    [SerializeField] private TextMeshProUGUI verificationText;

    [Tooltip("Button that triggers a reroll in the item shop.")]
    [SerializeField] private Button rerollButton;

    [Tooltip("Button used to cancel the current verification prompt.")]
    [SerializeField] private Button cancelButton;

    [Tooltip("Button selected when returning to the reset menu after a reset operation.")]
    [SerializeField] private Button resetMenuButton;

    private int lastConfirmedCode = -1;

    // Cached singletons
    private UINavigationController navigationController;
    private Localizer localizer;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        navigationController = UINavigationController.Instance;
        localizer = Localizer.Instance;

        if (navigationController == null)
        {
            Debug.LogError($"[{nameof(Verification)}] UINavigationController.Instance is null.");
        }

        if (localizer == null)
        {
            Debug.LogError($"[{nameof(Verification)}] Localizer.Instance is null.");
        }
    }

    private void OnEnable()
    {
        if (navigationController != null && cancelButton != null)
        {
            navigationController.JumpToElement(cancelButton);
        }
        else if (cancelButton == null)
        {
            Debug.LogWarning($"[{nameof(Verification)}] cancelButton is not assigned.");
        }
    }

    #endregion

    #region Verification Flow

    /// <summary>
    /// Requests a verification prompt for the given code and updates the UI.
    /// </summary>
    /// <param name="newCode">
    /// 0 = reroll,
    /// 1 = reset quest progression,
    /// 2 = reset character levels,
    /// 3 = reset items and gold,
    /// 4 = reset everything.
    /// </param>
    public void VerificationRequested(int newCode)
    {
        code = newCode;

        // Prevent repeated spam verification for the same code;
        // immediately confirm if user just confirmed this action.
        if (lastConfirmedCode == newCode)
        {
            Confirm();
            return;
        }

        if (verificationText == null || localizer == null)
        {
            Debug.LogError($"[{nameof(Verification)}] VerificationRequested called but UI/Localizer references are missing.");
            return;
        }

        switch (code)
        {
            case 0: // reroll
                if (Tutorial.Singleton != null)
                {
                    Tutorial.Singleton.CloseItemBuy();
                }
                else
                {
                    Debug.LogWarning($"[{nameof(Verification)}] Tutorial.Singleton is null while handling reroll verification.");
                }

                verificationText.text = localizer.GetLocalizedText(
                    "Are you sure you want to reroll the item shop? This will cost 10 gold.");
                break;

            case 1: // quest progression
                verificationText.text = localizer.GetLocalizedText(
                    "Are you sure you want to reset your quest progression? You will return to the very first quest.");
                break;

            case 2: // character levels
                verificationText.text = localizer.GetLocalizedText(
                    "Are you sure you want to reset all character levels? All classes you own will return to level 0.");
                break;

            case 3: // items and gold
                verificationText.text = localizer.GetLocalizedText(
                    "Are you sure you want to reset all items and gold? You will have to buy them all over again.");
                break;

            case 4: // reset everything
                verificationText.text = localizer.GetLocalizedText(
                    "Are you sure you want to reset everything? This will clear all saved data except any classes you may have purchased.");
                break;

            default:
                Debug.LogWarning($"[{nameof(Verification)}] Unknown verification code: {code}");
                return;
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Confirms the current verification and performs the associated action.
    /// </summary>
    public void Confirm()
    {
        switch (code)
        {
            case 0: // reroll
                if (inventory == null)
                {
                    Debug.LogError($"[{nameof(Verification)}] Inventory reference is null. Cannot reroll.");
                    break;
                }

                inventory.Reroll();

                if (navigationController != null && rerollButton != null)
                {
                    navigationController.JumpToElement(rerollButton);
                }
                break;

            case 1: // quest progression
            case 2: // character levels
            case 3: // items and gold
            case 4: // reset everything
                if (tutorial == null)
                {
                    Debug.LogError($"[{nameof(Verification)}] Tutorial reference is null. Cannot reset with code {code}.");
                    break;
                }

                tutorial.Reseter(code);
                break;

            default:
                Debug.LogWarning($"[{nameof(Verification)}] Confirm called with unknown code: {code}");
                break;
        }

        lastConfirmedCode = code;
        Cancel();
    }

    /// <summary>
    /// Cancels the current verification and restores focus to the appropriate UI element.
    /// </summary>
    public void Cancel()
    {
        if (navigationController == null)
        {
            Debug.LogWarning($"[{nameof(Verification)}] navigationController is null in Cancel().");
        }

        switch (code)
        {
            case 0: // reroll
                if (navigationController != null && rerollButton != null)
                {
                    navigationController.JumpToElement(rerollButton);
                }
                break;

            case 1: // quest progression
            case 2: // character levels
            case 3: // items and gold
            case 4: // reset everything
                if (navigationController != null && resetMenuButton != null)
                {
                    navigationController.JumpToElement(resetMenuButton);
                }
                break;

            default:
                // No specific focus target for unknown codes
                break;
        }

        gameObject.SetActive(false);
    }

    #endregion
}

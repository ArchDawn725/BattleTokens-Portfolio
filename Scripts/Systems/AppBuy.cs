using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles purchasing of app-based unlocks (e.g. classes) and updates the UI state
/// (price label, purchased label, and button interactability).
/// </summary>
public class AppBuy : MonoBehaviour
{
    #region Inspector

    [Header("UI References")]
    [Tooltip("Text that is shown when this item has been purchased.")]
    [SerializeField] private TextMeshProUGUI purchasedText;

    [Tooltip("Text that shows the price of this item before purchase.")]
    [SerializeField] private TextMeshProUGUI priceText;

    [Tooltip("Purchase button for this item.")]
    [SerializeField] private Button purchaseButton;

    #endregion

    #region Public API

    /// <summary>
    /// Purchase the item associated with the given class ID and update UI / save data.
    /// </summary>
    /// <param name="classId">ID representing which class to unlock.</param>
    public void BuyItem(int classId)
    {
        switch (classId)
        {
            case 0:
                PlayerPrefs.SetInt("KnightClass", 1);
                break;
            case 1:
                PlayerPrefs.SetInt("AssassinClass", 1);
                break;
            case 2:
                PlayerPrefs.SetInt("JesterClass", 1);
                break;
            case 3:
                PlayerPrefs.SetInt("VampireClass", 1);
                break;
            default:
                Debug.LogWarning($"[{nameof(AppBuy)}] Unrecognized classId '{classId}' passed to BuyItem.", this);
                return;
        }

        DisableButton();
    }

    /// <summary>
    /// Marks this item as purchased in the UI and disables further interaction.
    /// </summary>
    public void DisableButton()
    {
        if (purchasedText == null || priceText == null || purchaseButton == null)
        {
            Debug.LogWarning($"[{nameof(AppBuy)}] UI references not fully assigned on '{name}'. Cannot safely update purchase state.", this);
            return;
        }

        purchasedText.gameObject.SetActive(true);
        priceText.gameObject.SetActive(false);
        purchaseButton.interactable = false;
    }

    #endregion
}

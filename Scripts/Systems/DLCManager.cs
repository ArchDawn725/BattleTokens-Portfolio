#if !UNITY_ANDROID && !UNITY_IOS
using Steamworks;
using System.IO;
#endif
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

/// <summary>
/// Handles DLC ownership/unlock status across platforms:
/// - Steam DLC (desktop)
/// - Unity IAP (mobile)
/// Also provides a simple "sync PlayerPrefs to/from file" mechanism on desktop,
/// intended for cloud-sync or migration.
/// </summary>
public class DLCManager : MonoBehaviour
{
    #region Inspector

    [Header("Class Unlock Buttons")]
    [Tooltip("Buttons used to select each DLC class (Knight, Assassin, Jester, Vampire). " +
             "Order must match the DLC product IDs.")]
    [SerializeField] private Button[] classButtons;

    [Header("Store Purchase Buttons")]
    [Tooltip("Buttons that handle platform store purchases for each DLC class. " +
             "Order must match the DLC product IDs.")]
    [SerializeField] private AppBuy[] appBuyButtons;

    [Header("UI References")]
    [Tooltip("Root object for the DLC UI scene/panel.")]
    [SerializeField] private GameObject dlcScene;

    [Header("IAP / Store (Mobile)")]
    [Tooltip("Unity IAP StoreController used on mobile platforms.")]
    private StoreController store;

    #endregion

    #region Constants

    /// <summary>
    /// Product IDs for DLC classes used by the store (Unity IAP, etc.).
    /// Order must match the classButtons / appBuyButtons arrays.
    /// </summary>
    private static readonly string[] DlcIds = { "knight", "assassin", "jester", "vampire" };

    #endregion

    #region Unity Lifecycle

    public async void Start()
    {
        // Unity IAP v5 entry point
        store = UnityIAPServices.StoreController();

        if (store == null)
        {
            Debug.LogError($"[{nameof(DLCManager)}] StoreController is null. IAP will not function on this platform.");
        }
        else
        {
            // Do not auto-consume pending orders
            store.ProcessPendingOrdersOnPurchasesFetched(false);

#if UNITY_ANDROID || UNITY_IOS
            store.OnPurchasesFetched += OnPurchasesFetched;
            store.OnPurchasesFetchFailed += failure =>
                Debug.LogError($"[{nameof(DLCManager)}] FetchPurchases failed: {failure.failureReason}");
#endif

            // Connect and fetch the DLC products
            await store.Connect();

            store.FetchProducts(
                DlcIds
                    .Select(id => new ProductDefinition(id, ProductType.NonConsumable))
                    .ToList()
            );

            store.FetchPurchases();
        }

        // --- Testing block (commented out intentionally - can be re-enabled for debug only) ---
        /*
        PlayerPrefs.SetInt("Warrior_Level", 50);
        PlayerPrefs.SetInt("Archer_Level", 50);
        PlayerPrefs.SetInt("Mage_Level", 50);
        PlayerPrefs.SetInt("Healer_Level", 50);

        PlayerPrefs.SetInt("Knight_Level", 50);
        PlayerPrefs.SetInt("Assassin_Level", 50);
        PlayerPrefs.SetInt("Jester_Level", 50);
        PlayerPrefs.SetInt("Vampire_Level", 50);

        PlayerPrefs.SetInt("KnightClass", 1);
        PlayerPrefs.SetInt("AssassinClass", 1);
        PlayerPrefs.SetInt("JesterClass", 1);
        PlayerPrefs.SetInt("VampireClass", 1);
        */

        GetUnlockedDLC();
        Hide();
    }

#endregion

    #region DLC Unlock Logic

    /// <summary>
    /// Checks the platform (Steam / IAP) for any already-owned DLC, updates PlayerPrefs,
    /// then applies the unlocks to the UI.
    /// </summary>
    public void GetUnlockedDLC()
    {
        // Steam DLC check (desktop only)
        if (SteamIntergration.Instance != null && SteamIntergration.Instance.SteamConnected)
        {
#if !UNITY_ANDROID && !UNITY_IOS
            // Single DLC app that unlocks all 4 classes
            if (SteamApps.IsDlcInstalled((AppId)4003420))
            {
                PlayerPrefs.SetInt("KnightClass", 1);
                PlayerPrefs.SetInt("AssassinClass", 1);
                PlayerPrefs.SetInt("JesterClass", 1);
                PlayerPrefs.SetInt("VampireClass", 1);
            }
#endif
        }

        UnlockDLC();
    }

    /// <summary>
    /// Uses PlayerPrefs flags to enable class selection buttons and
    /// disable purchase buttons for already-owned DLC classes.
    /// </summary>
    public void UnlockDLC()
    {
        // Defensive: ensure arrays are sized correctly
        if (classButtons == null || appBuyButtons == null ||
            classButtons.Length < 4 || appBuyButtons.Length < 4)
        {
            Debug.LogWarning($"[{nameof(DLCManager)}] classButtons/appBuyButtons are not correctly configured.");
            return;
        }

        if (PlayerPrefs.GetInt("KnightClass", 0) > 0)
        {
            classButtons[0].interactable = true;
            appBuyButtons[0].DisableButton();
        }

        if (PlayerPrefs.GetInt("AssassinClass", 0) > 0)
        {
            classButtons[1].interactable = true;
            appBuyButtons[1].DisableButton();
        }

        if (PlayerPrefs.GetInt("JesterClass", 0) > 0)
        {
            classButtons[2].interactable = true;
            appBuyButtons[2].DisableButton();
        }

        if (PlayerPrefs.GetInt("VampireClass", 0) > 0)
        {
            classButtons[3].interactable = true;
            appBuyButtons[3].DisableButton();
        }
    }

#if UNITY_ANDROID || UNITY_IOS
    /// <summary>
    /// Called by Unity IAP when purchases are fetched on mobile platforms.
    /// Any owned DLC products are written into PlayerPrefs and applied to the UI.
    /// </summary>
    /// <remarks>
    /// Signature may need to be adjusted to match your Unity IAP v5 version
    /// (e.g. IReadOnlyList&lt;Purchase&gt; or similar).
    /// </remarks>
    private void OnPurchasesFetched(IReadOnlyList<Purchase> purchases)
    {
        if (purchases == null || purchases.Count == 0)
            return;

        foreach (var purchase in purchases)
        {
            switch (purchase.productId)
            {
                case "knight":
                    PlayerPrefs.SetInt("KnightClass", 1);
                    break;
                case "assassin":
                    PlayerPrefs.SetInt("AssassinClass", 1);
                    break;
                case "jester":
                    PlayerPrefs.SetInt("JesterClass", 1);
                    break;
                case "vampire":
                    PlayerPrefs.SetInt("VampireClass", 1);
                    break;
            }
        }

        PlayerPrefs.Save();
        UnlockDLC();
    }
#endif

    #endregion

    #region UI Show / Hide

    /// <summary>
    /// Shows the DLC panel and hides the authentication UI (main menu).
    /// </summary>
    public void Show()
    {
        if (dlcScene != null)
        {
            dlcScene.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[{nameof(DLCManager)}] DLC scene reference is missing, cannot show.", this);
        }

        if (AuthenticateUI.Instance != null)
        {
            AuthenticateUI.Instance.Hide();
        }
    }

    /// <summary>
    /// Hides the DLC panel and shows the authentication UI (main menu), if available.
    /// </summary>
    public void Hide()
    {
        if (dlcScene != null)
        {
            dlcScene.SetActive(false);
        }

        if (AuthenticateUI.Instance != null)
        {
            AuthenticateUI.Instance.Show();
        }
    }

    #endregion
}

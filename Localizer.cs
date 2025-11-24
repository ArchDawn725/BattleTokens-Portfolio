using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Simple wrapper around Unity's Localization system to fetch localized strings by key.
/// </summary>
public class Localizer : MonoBehaviour
{
    #region Singleton

    public static Localizer Instance { get; private set; }

    #endregion

    #region Fields

    [Header("Localization")]
    [Tooltip("Name of the string table to use for lookups.")]
    [SerializeField] private string tableName = "Testing";

    // Runtime string reference used for lookups.
    private readonly LocalizedString stringReference = new LocalizedString();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(Localizer)}] Duplicate instance on '{name}'. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Bind the table used for all lookups.
        stringReference.TableReference = tableName;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Returns a localized string for the given key.
    /// If the key is missing or resolves to an empty string, the key itself is returned.
    /// </summary>
    public string GetLocalizedText(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[{nameof(Localizer)}] Requested localization with an empty key.");
            return string.Empty;
        }

        stringReference.TableEntryReference = key;

        // GetLocalizedString() typically returns the original key or an empty string if not found.
        string localized = stringReference.GetLocalizedString();

        if (string.IsNullOrEmpty(localized) || localized == key)
        {
            Debug.LogWarning(
                $"[{nameof(Localizer)}] Missing or fallback localization for key '{key}' in table '{tableName}'. Returning key."
            );
            return key;
        }

        return localized;
    }

    #endregion
}

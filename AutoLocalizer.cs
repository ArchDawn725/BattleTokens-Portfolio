using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class AutoLocalizer : MonoBehaviour
{
    #region Fields

    [Header("Localization")]
    [Tooltip("Localization key to use. If left empty, the current text is used as the key.")]
    [SerializeField] private string englishKey;

    [Tooltip("LocalizedString reference used to fetch translated text.")]
    [SerializeField] private LocalizedString stringReference = new LocalizedString();

    private TextMeshProUGUI textReferenceUI;
    private TextMeshPro textReferenceWorld;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Subscribe to global language refresh
        LangCon.RefreshRequested += UpdateLocalizedText;

        // Cache text components (either UI or world-space TMP)
        textReferenceUI = GetComponent<TextMeshProUGUI>();
        textReferenceWorld = GetComponent<TextMeshPro>();

        if (textReferenceUI == null && textReferenceWorld == null)
        {
            Debug.LogWarning($"[{nameof(AutoLocalizer)}] No TextMeshPro component found on '{name}'. Auto-localization will be skipped.");
        }

        // Set the table reference once
        stringReference.TableReference = "Testing";
    }

    private void Start()
    {
        // If no explicit key was configured, fall back to the initial text.
        if (textReferenceUI != null)
        {
            if (string.IsNullOrEmpty(englishKey))
            {
                englishKey = textReferenceUI.text;
            }
        }
        else if (textReferenceWorld != null)
        {
            if (string.IsNullOrEmpty(englishKey))
            {
                englishKey = textReferenceWorld.text;
            }
        }

        // Apply localization using the resolved key.
        UpdateLocalizedText(englishKey);
    }

    private void OnDestroy()
    {
        LangCon.RefreshRequested -= UpdateLocalizedText;
    }

    #endregion

    #region Localization

    /// <summary>
    /// Update the localized text using an explicit key.
    /// </summary>
    public void UpdateLocalizedText(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        if (textReferenceUI == null && textReferenceWorld == null)
        {
            return;
        }

        stringReference.TableEntryReference = key;
        stringReference.RefreshString();

        string localized = stringReference.GetLocalizedString();

        if (textReferenceUI != null)
        {
            textReferenceUI.text = localized;
        }

        if (textReferenceWorld != null)
        {
            textReferenceWorld.text = localized;
        }
    }

    /// <summary>
    /// Update the localized text using the stored englishKey.
    /// This is used as a callback for language changes.
    /// </summary>
    public void UpdateLocalizedText()
    {
        if (string.IsNullOrEmpty(englishKey))
        {
            return;
        }

        if (textReferenceUI == null && textReferenceWorld == null)
        {
            return;
        }

        stringReference.TableEntryReference = englishKey;
        stringReference.RefreshString();

        string localized = stringReference.GetLocalizedString();

        if (textReferenceUI != null)
        {
            textReferenceUI.text = localized;
        }

        if (textReferenceWorld != null)
        {
            textReferenceWorld.text = localized;
        }
    }

    #endregion
}

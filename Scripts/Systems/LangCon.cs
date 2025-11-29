#if UNITY_ANDROID || UNITY_IOS
#else
using Steamworks;
#endif
using System;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Handles language selection, including Steam language auto-detection
/// and triggering UI text refresh when the language changes.
/// </summary>
public class LangCon : MonoBehaviour
{
    #region Singleton & Fields

    public static LangCon Singleton { get; private set; }

    [Header("Language UI")]
    [Tooltip("Buttons used to select a language by index.")]
    [SerializeField] private Button[] langButtons;

    [Tooltip("Sprites corresponding to each language option, used on the change language button.")]
    [SerializeField] private Sprite[] langSprites;

    [Tooltip("Button that shows the current language and opens the language selection UI.")]
    [SerializeField] private Button changeLangButton;

    [Header("Scenes")]
    [Tooltip("Language selection UI root.")]
    [SerializeField] private GameObject langScene;

    [Tooltip("Main UI root that should be enabled once a language is set.")]
    [SerializeField] private GameObject mainScene;

    [Header("State")]
    [Tooltip("Current language index, matching the configured locales list.")]
    [SerializeField] private int langIndex;

    public static event Action RefreshRequested;

    private bool steamActivated;

    // Cached references
    private SteamIntergration steamIntegration;
    private AuthenticateUI authenticateUI;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Debug.LogWarning($"[{nameof(LangCon)}] Duplicate instance on '{name}'. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Singleton = this;

        steamIntegration = SteamIntergration.Instance;
        authenticateUI = AuthenticateUI.Instance;
    }

    private void Start()
    {
        // Small delay to allow external systems (Steam, UGS, etc.) to initialize
        Invoke(nameof(DelayedInitialization), 0.5f);
    }

    #endregion

    #region Initialization & Language Change

    private void DelayedInitialization()
    {
        if (steamIntegration == null)
        {
            Debug.LogWarning($"[{nameof(LangCon)}] SteamIntergration.Instance is null. Falling back to PlayerPrefs language.");
            UseSavedLanguage();
            return;
        }

        Debug.Log($"[{nameof(LangCon)}] Delay SteamConnected = {steamIntegration.SteamConnected}");

        if (!steamIntegration.SteamConnected)
        {
            UseSavedLanguage();
        }
#if UNITY_ANDROID || UNITY_IOS
#else
        else
        {
            SteamLangSetUp(SteamApps.GameLanguage);
        }
#endif
    }

    private void UseSavedLanguage()
    {
        langIndex = PlayerPrefs.GetInt("Lang", 0);
        ChangeLang(langIndex);
        if (mainScene != null)
        {
            mainScene.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[{nameof(LangCon)}] mainScene is not assigned.");
        }
    }

    /// <summary>
    /// Changes the active locale based on an index and updates related UI.
    /// </summary>
    public void ChangeLang(int lang)
    {
        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (locales == null || locales.Count == 0)
        {
            Debug.LogError($"[{nameof(LangCon)}] No locales available in LocalizationSettings.");
            return;
        }

        if (lang < 0 || lang >= locales.Count)
        {
            Debug.LogWarning($"[{nameof(LangCon)}] Requested language index {lang} is out of range. Clamping to valid range.");
            lang = Mathf.Clamp(lang, 0, locales.Count - 1);
        }

        langIndex = lang;
        LocalizationSettings.SelectedLocale = locales[langIndex];
        PlayerPrefs.SetInt("Lang", langIndex);

        if (langButtons != null && langButtons.Length > 0)
        {
            for (int i = 0; i < langButtons.Length; i++)
            {
                if (langButtons[i] != null)
                {
                    langButtons[i].interactable = true;
                }
            }

            if (langIndex >= 0 && langIndex < langButtons.Length && langButtons[langIndex] != null)
            {
                langButtons[langIndex].interactable = false;
            }
            else
            {
                Debug.LogWarning($"[{nameof(LangCon)}] langButtons does not contain an entry for index {langIndex}.");
            }
        }

        if (changeLangButton != null && langSprites != null && langIndex < langSprites.Length)
        {
            changeLangButton.image.sprite = langSprites[langIndex];
        }
        else if (changeLangButton == null)
        {
            Debug.LogWarning($"[{nameof(LangCon)}] changeLangButton is not assigned.");
        }

        RaiseRefresh();
        Return();
    }

    public void Return()
    {
        if (langScene != null)
        {
            langScene.SetActive(false);
        }

        authenticateUI ??= AuthenticateUI.Instance;
        if (authenticateUI != null)
        {
            authenticateUI.Show();
        }
    }

    public void ChangeLangButtonPressed()
    {
        if (langScene != null)
        {
            langScene.SetActive(true);
        }

        authenticateUI ??= AuthenticateUI.Instance;
        if (authenticateUI != null)
        {
            authenticateUI.Hide();
        }
    }

    public static void RaiseRefresh() => RefreshRequested?.Invoke();

    #endregion

    #region Steam Integration

    /// <summary>
    /// Sets the language based on Steam's reported game language the first time it is called.
    /// </summary>
    public void SteamLangSetUp(string lang)
    {
        if (steamActivated)
        {
            return;
        }

        steamActivated = true;

        int savedLangIndex = PlayerPrefs.GetInt("Lang", -1);
        int resolvedIndex = savedLangIndex;

        if (resolvedIndex == -1)
        {
            switch (lang)
            {
                case "english": resolvedIndex = 0; break;
                case "schinese": resolvedIndex = 1; break;
                case "french": resolvedIndex = 2; break;
                case "german": resolvedIndex = 3; break;
                case "japanese": resolvedIndex = 4; break;
                case "polish": resolvedIndex = 5; break;
                case "brazilian": resolvedIndex = 6; break;
                case "russian": resolvedIndex = 7; break;
                case "spanish": resolvedIndex = 8; break;
                default: resolvedIndex = 0; break;
            }
        }

        Debug.Log($"[{nameof(LangCon)}] Steam language index = {resolvedIndex}");
        ChangeLang(resolvedIndex);

        if (mainScene != null)
        {
            mainScene.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[{nameof(LangCon)}] mainScene is not assigned.");
        }
    }

    #endregion
}

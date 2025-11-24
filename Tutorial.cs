using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{
    #region Singleton

    public static Tutorial Singleton { get; private set; }

    private void Awake()
    {
        if (Singleton != null && Singleton != this)
        {
            Debug.LogWarning($"[{nameof(Tutorial)}] Multiple instances detected. Keeping the first one.");
            return;
        }

        Singleton = this;
    }

    #endregion

    #region Inspector References

    [Header("Tutorial Panels")]
    [SerializeField] private GameObject singlePlayerConfirm;
    [SerializeField] private GameObject upgradeTutorial;
    [SerializeField] private GameObject spawnTutorial;
    [SerializeField] private GameObject playTutorial;
    [SerializeField] private GameObject lobbyFail;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject noInternet;
    [SerializeField] private GameObject itemBuy;

    [Header("Pause Menu / Options")]
    public Selectable[] selectables;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider fXSlider;
    [SerializeField] private ToggleButton[] toggleButtons;
    [SerializeField] private Button menuButton;
    [SerializeField] private GameObject resetMenu;
    [SerializeField] private CandleFlicker candleFlicker;
    [SerializeField] private Button[] playSpeedButtons;
    [SerializeField] private Animator animator;

    [Header("Connection Panels")]
    [SerializeField] private GameObject noInternetLobby;

    [Header("Shop")]
    [SerializeField] private Shop shop;

    #endregion

    #region State

    public bool confirmed;

    [SerializeField] private int pauseMenuInt;
    public int phoneInt;

    private ItemSO selectedItem;
    private Button selectedButton;

    // Tutorial index documentation:
    // 0 - auth
    // 1 - input
    // 2 - lang
    // 3 - buy
    // 4 - shop
    // 5 - lobbylist
    // 6 - change lobby code
    // 7 - lobby create
    // 8 - lobby ui
    // 9 - upgrades
    // 10 - game

    #endregion

    #region Public API - Notifications & Dialogs

    public void SinglePlayerConfirmation()
    {
        confirmed = true;

        if (singlePlayerConfirm != null)
        {
            singlePlayerConfirm.SetActive(true);

            if (UINavigationController.Instance != null)
            {
                var confirmButton =
                    singlePlayerConfirm.transform.GetChild(1).GetChild(1).GetComponent<Button>();
                UINavigationController.Instance.JumpToElement(confirmButton);
            }
        }

        PlayerPrefs.SetInt("SinglePlayerConfrim", 1);
    }

    public void NoInternetNotification()
    {
        if (noInternet != null)
        {
            noInternet.SetActive(true);

            if (UINavigationController.Instance != null)
            {
                var button = noInternet.transform.GetChild(1).GetChild(1).GetComponent<Button>();
                UINavigationController.Instance.JumpToElement(button);
            }
        }

        if (noInternetLobby != null)
        {
            noInternetLobby.SetActive(true);
        }
    }

    public void LobbyFailedConfirmation()
    {
        if (lobbyFail != null)
        {
            lobbyFail.SetActive(true);

            if (UINavigationController.Instance != null)
            {
                var button = lobbyFail.transform.GetChild(1).GetChild(1).GetComponent<Button>();
                UINavigationController.Instance.JumpToElement(button);
            }
        }
    }

    #endregion

    #region Pause Menu & Settings

    public void PauseMenu(int menu)
    {
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1);
        }

        if (fXSlider != null)
        {
            fXSlider.value = PlayerPrefs.GetFloat("SoundEffectVolume", 1);
        }

        if (SoundController.Instance != null)
        {
            if (musicSlider != null)
            {
                // prevent stacking listeners
                musicSlider.onValueChanged.RemoveListener(SoundController.Instance.ChangeMusicVolume);
                musicSlider.onValueChanged.AddListener(SoundController.Instance.ChangeMusicVolume);
            }

            if (fXSlider != null)
            {
                fXSlider.onValueChanged.RemoveListener(SoundController.Instance.ChangeSoundEffectVolume);
                fXSlider.onValueChanged.AddListener(SoundController.Instance.ChangeSoundEffectVolume);
            }
        }

        pauseMenuInt = menu;

        if (pauseMenu == null) return;

        if (pauseMenu.activeInHierarchy)
        {
            LeaveMenu();
            return;
        }

        pauseMenu.SetActive(true);

        if (UINavigationController.Instance != null)
        {
            var firstButton = pauseMenu.transform.GetChild(3).GetComponent<Button>();
            UINavigationController.Instance.JumpToElement(firstButton);
        }
    }

    public void PhonePause()
    {
        PauseMenu(phoneInt);
    }

    public void LeaveMenu()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        if (UINavigationController.Instance != null &&
            selectables != null &&
            pauseMenuInt >= 0 &&
            pauseMenuInt < selectables.Length)
        {
            UINavigationController.Instance.JumpToElement(selectables[pauseMenuInt]);
        }
    }

    public void ExitGame()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        switch (pauseMenuInt)
        {
            // main menu / meta scenes
            case 0:
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
                Application.Quit();
                break;

            // in-game / battle scenes
            case 9:
            case 10:
                if (UIController.Instance != null)
                {
                    UIController.Instance.GameOver(0, false, false);
                }
                break;
        }
    }

    #endregion

    #region Tutorial Flow

    public void StartTutorial(int value)
    {
        switch (value)
        {
            case 1:
                if (PlayerPrefs.GetInt("Tutorial", 0) <= value)
                {
                    PlayerPrefs.SetInt("Tutorial", 2);

                    if (spawnTutorial != null)
                    {
                        spawnTutorial.SetActive(true);

                        if (UIController.Instance != null)
                        {
                            var btn = spawnTutorial.transform.GetChild(1).GetChild(1).GetComponent<Button>();
                            UINavigationController.Instance.JumpToElement(btn);
                        }
                    }
                }
                break;

            case 2:
                if (PlayerPrefs.GetInt("Tutorial", 0) <= value)
                {
                    PlayerPrefs.SetInt("Tutorial", 3);

                    if (playTutorial != null)
                    {
                        playTutorial.SetActive(true);

                        if (UIController.Instance != null)
                        {
                            var btn = playTutorial.transform.GetChild(1).GetChild(1).GetComponent<Button>();
                            UINavigationController.Instance.JumpToElement(btn);
                        }
                    }
                }
                break;

            case 3:
                if (PlayerPrefs.GetInt("Tutorial", 0) == 3)
                {
                    PlayerPrefs.SetInt("Tutorial", 4);

                    if (upgradeTutorial != null)
                    {
                        upgradeTutorial.SetActive(true);

                        if (UIController.Instance != null)
                        {
                            var btn = upgradeTutorial.transform.GetChild(1).GetChild(1).GetComponent<Button>();
                            UINavigationController.Instance.JumpToElement(btn);
                        }
                    }
                }
                break;
        }
    }

    private void Start()
    {
        // AutoEndTurn
        int boolenValue = PlayerPrefs.GetInt("AutoEndTurn", 1);
        if (boolenValue != 1)
        {
            PlayerStats.Instance.autoEndTurnSetting = false;
            if (toggleButtons.Length > 0 && toggleButtons[0] != null)
                toggleButtons[0].OnTogglePressed();
        }

        // AlwaysDisplayCharacterInfo
        boolenValue = PlayerPrefs.GetInt("AlwaysDisplayCharacterInfo", 0);
        if (boolenValue != 0)
        {
            PlayerStats.Instance.alwaysDisplayCharacterInfoSetting = true;
            if (toggleButtons.Length > 1 && toggleButtons[1] != null)
                toggleButtons[1].OnTogglePressed();
        }

        // DisableShadows
        boolenValue = PlayerPrefs.GetInt("DisableShadows", 0);
        if (boolenValue != 0)
        {
            PlayerStats.Instance.disableShadows = true;
            if (toggleButtons.Length > 2 && toggleButtons[2] != null)
                toggleButtons[2].OnTogglePressed();
        }

        if (Application.isMobilePlatform && menuButton != null)
        {
            menuButton.gameObject.SetActive(true);
        }

        ChangePlaySpeed(PlayerPrefs.GetInt("PlaySpeed", 1));

        if (PlayerPrefs.GetInt("SinglePlayerConfrim", 0) == 1)
        {
            confirmed = true;
        }
    }

    public void ChangeSetting(int value)
    {
        bool boolenValue;

        switch (value)
        {
            case 0: // Auto end turn
                boolenValue = !PlayerStats.Instance.autoEndTurnSetting;
                PlayerPrefs.SetInt("AutoEndTurn", boolenValue ? 1 : 0);
                PlayerStats.Instance.autoEndTurnSetting = boolenValue;
                break;

            case 1: // Always display character info
                boolenValue = !PlayerStats.Instance.alwaysDisplayCharacterInfoSetting;
                PlayerPrefs.SetInt("AlwaysDisplayCharacterInfo", boolenValue ? 1 : 0);
                PlayerStats.Instance.alwaysDisplayCharacterInfoSetting = boolenValue;

                foreach (CharacterInfo info in FindObjectsByType<CharacterInfo>(FindObjectsSortMode.None))
                {
                    info.ChangeSetting();
                }
                break;

            case 2: // Disable shadows
                boolenValue = !PlayerStats.Instance.disableShadows;
                PlayerPrefs.SetInt("DisableShadows", boolenValue ? 1 : 0);
                PlayerStats.Instance.disableShadows = boolenValue;

                if (candleFlicker != null)
                {
                    candleFlicker.ChangeSetting(boolenValue);
                }
                break;
        }
    }

    #endregion

    #region Reset Logic

    public void ShowReset()
    {
        if (resetMenu != null)
            resetMenu.SetActive(true);
    }

    public void CancelReset()
    {
        if (resetMenu != null)
            resetMenu.SetActive(false);
    }

    public void Reseter(int value)
    {
        switch (value)
        {
            case 1: // reset quests
                PlayerPrefs.SetInt("Quest", 0);
                break;

            case 2: // reset characters
                ResetCharacters();
                break;

            case 3: // reset items
                ResetItems();
                break;

            case 4: // reset everything
                ResetEverything();
                break;

            default:
                Debug.LogWarning("Invalid reset value");
                break;
        }

        SceneManager.LoadScene(0);
    }

    private void ResetCharacters()
    {
        for (int i = 0; i < LoadoutUIController.Instance.classes.Length; i++)
        {
            string className = LoadoutUIController.Instance.classes[i].PlayerClass.ToString();
            PlayerPrefs.SetInt(className + "_Level", 0);
            PlayerPrefs.SetFloat(className + "_XP", 0);
        }

        PlayerPrefs.SetInt("mapPlayers", 7);
        PlayerPrefs.SetInt("aiHelpers", 2);
        PlayerPrefs.SetInt("ChoosenClass", 0);

        // ai0 intentionally left alone
        PlayerPrefs.SetInt("ai1", -1);
        PlayerPrefs.SetInt("ai2", -1);
        PlayerPrefs.SetInt("ai3", -1);
        PlayerPrefs.SetInt("ai4", -1);
        PlayerPrefs.SetInt("ai5", -1);
        PlayerPrefs.SetInt("ai6", -1);
        PlayerPrefs.SetInt("ai7", -1);
        PlayerPrefs.SetInt("ai8", -1);
    }

    private void ResetItems()
    {
        PlayerPrefs.SetFloat("Gold", 0);

        if (Inventory.Instance == null || Inventory.Instance.allItems == null)
            return;

        for (int i = 0; i < Inventory.Instance.allItems.Length; i++)
        {
            var item = Inventory.Instance.allItems[i];
            if (item == null) continue;

            string itemName = item.Rarity + " " + item.ItemName;
            PlayerPrefs.SetInt(itemName, 0);
        }
    }

    private void ResetEverything()
    {
        // Settings
        PlayerPrefs.SetInt("AutoEndTurn", 1);
        PlayerPrefs.SetInt("AlwaysDisplayCharacterInfo", 0);
        PlayerPrefs.SetInt("DisableShadows", 0);
        PlayerPrefs.SetInt("Tutorial", 0);
        PlayerPrefs.SetFloat("MusicVolume", 1);
        PlayerPrefs.SetFloat("SoundEffectVolume", 1);
        PlayerPrefs.SetInt("PlaySpeed", 1);
        PlayerPrefs.SetInt("SinglePlayerConfrim", 0);

        // Core game prefs
        PlayerPrefs.SetInt("mapPlayers", 7);
        PlayerPrefs.SetInt("aiHelpers", 2);
        PlayerPrefs.SetInt("ChoosenClass", 0);

        PlayerPrefs.SetInt("ai1", -1);
        PlayerPrefs.SetInt("ai2", -1);
        PlayerPrefs.SetInt("ai3", -1);
        PlayerPrefs.SetInt("ai4", -1);
        PlayerPrefs.SetInt("ai5", -1);
        PlayerPrefs.SetInt("ai6", -1);
        PlayerPrefs.SetInt("ai7", -1);
        PlayerPrefs.SetInt("ai8", -1);

        // Reset class levels
        for (int i = 0; i < LoadoutUIController.Instance.classes.Length; i++)
        {
            string className = LoadoutUIController.Instance.classes[i].PlayerClass.ToString();
            PlayerPrefs.SetInt(className + "_Level", 0);
            PlayerPrefs.SetFloat(className + "_XP", 0);
        }

        PlayerPrefs.SetInt("Quest", 0);
        PlayerPrefs.SetFloat("Gold", 0);

        // Reset items
        if (Inventory.Instance != null && Inventory.Instance.allItems != null)
        {
            for (int i = 0; i < Inventory.Instance.allItems.Length; i++)
            {
                var item = Inventory.Instance.allItems[i];
                if (item == null) continue;

                string itemName = item.Rarity + " " + item.ItemName;
                PlayerPrefs.SetInt(itemName, 0);
            }
        }
    }

    #endregion

    #region Play Speed

    public void ChangePlaySpeed(int val)
    {
        if (val <= 0) val = 1;

        PlayerStats.Instance.playSpeed = val;
        PlayerPrefs.SetInt("PlaySpeed", val);

        if (playSpeedButtons != null)
        {
            foreach (Button button in playSpeedButtons)
            {
                if (button != null)
                    button.interactable = true;
            }

            int index = Mathf.Clamp(val - 1, 0, playSpeedButtons.Length - 1);
            if (playSpeedButtons.Length > 0 && playSpeedButtons[index] != null)
            {
                playSpeedButtons[index].interactable = false;
            }
        }

        if (animator != null)
        {
            animator.speed = val;
        }
    }

    #endregion

    #region Item Buy Popup

    public void ShowItemBuy(ItemSO newItem, Button slotButton)
    {
        selectedItem = newItem;
        selectedButton = slotButton;

        if (itemBuy == null || selectedItem == null)
            return;

        itemBuy.SetActive(true);

        if (UINavigationController.Instance != null)
        {
            var firstButton = itemBuy.transform.GetChild(0).GetChild(2).GetComponent<Button>();
            UINavigationController.Instance.JumpToElement(firstButton);
        }

        UpdateItemBuyDisplay();
    }

    public void BuyItem()
    {
        if (shop != null && selectedItem != null && selectedButton != null)
        {
            shop.BuyItem(selectedItem, selectedButton);
        }

        CloseItemBuy();
    }

    public void CloseItemBuy()
    {
        if (itemBuy != null)
            itemBuy.SetActive(false);

        if (selectedButton != null && UINavigationController.Instance != null)
        {
            UINavigationController.Instance.JumpToElement(selectedButton);
        }
    }

    private void UpdateItemBuyDisplay()
    {
        if (itemBuy == null || selectedItem == null)
            return;

        Transform root = itemBuy.transform.GetChild(0);

        // Icon
        root.GetChild(0).GetComponent<Image>().sprite = selectedItem.Sprite;

        // Name (Rarity + Name)
        root.GetChild(1).GetComponent<TextMeshProUGUI>().text =
            Localizer.Instance.GetLocalizedText(selectedItem.Rarity.ToString()) + " " +
            Localizer.Instance.GetLocalizedText(selectedItem.ItemName);

        // Description
        root.GetChild(4).GetComponent<TextMeshProUGUI>().text =
            Localizer.Instance.GetLocalizedText(selectedItem.Description);

        // Cost
        root.GetChild(5).GetChild(0).GetComponent<TextMeshProUGUI>().text =
            selectedItem.Cost.ToString();

        // Effect modifier
        root.GetChild(6).GetChild(0).GetComponent<TextMeshProUGUI>().text =
            "+" + selectedItem.EffectModifier;

        // Usage
        root.GetChild(7).GetComponent<TextMeshProUGUI>().text =
            Localizer.Instance.GetLocalizedText("Usage") + "\n" +
            Localizer.Instance.GetLocalizedText(selectedItem.Usage);
    }

    #endregion
}

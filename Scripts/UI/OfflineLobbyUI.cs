using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the UI for an offline lobby:
/// - Shows the local player and AI slots
/// - Lets the player pick a class
/// - Starts the game in offline mode
/// </summary>
public class OfflineLobbyUI : MonoBehaviour
{
    #region Singleton

    public static OfflineLobbyUI Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(OfflineLobbyUI)}] Duplicate instance detected, destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Cache all child slots
        if (playerSlotsContainer == null)
        {
            Debug.LogError($"[{nameof(OfflineLobbyUI)}] Player slots container is not assigned.", this);
            return;
        }

        foreach (Transform child in playerSlotsContainer)
        {
            int slotIndex = child.GetSiblingIndex();
            if (playerSlotDictionary.ContainsKey(slotIndex))
            {
                Debug.LogWarning($"[{nameof(OfflineLobbyUI)}] Duplicate slot index {slotIndex} in {playerSlotsContainer.name}.", this);
                continue;
            }

            playerSlotDictionary[slotIndex] = child;
        }

        if (leaveLobbyButton != null)
        {
            leaveLobbyButton.onClick.AddListener(() =>
            {
                LobbyListUI.Instance.Show();
                LeaveLobby();
            });
        }
        else
        {
            Debug.LogWarning($"[{nameof(OfflineLobbyUI)}] Leave lobby button is not assigned.", this);
        }

        if (readyUpButton != null)
        {
            readyUpButton.onClick.AddListener(ReadyUp);
        }
        else
        {
            Debug.LogWarning($"[{nameof(OfflineLobbyUI)}] Ready-up button is not assigned.", this);
        }
    }

    #endregion

    #region Inspector

    [Header("Player Slots")]
    [Tooltip("Parent transform containing all player/AI lobby slots.")]
    [SerializeField] private Transform playerSlotsContainer;

    [Header("Character Selection")]
    [Tooltip("Buttons used to select the local player's character/class.")]
    [SerializeField] private Button[] characterButtons;

    [Header("Lobby Controls")]
    [Tooltip("Button used to leave the offline lobby.")]
    [SerializeField] private Button leaveLobbyButton;

    [Tooltip("Button used to confirm selection and start the game.")]
    [SerializeField] private Button readyUpButton;

    [Header("Quest Info")]
    [Tooltip("Text showing the selected quest description.")]
    [SerializeField] private TextMeshProUGUI questDescriptionText;

    #endregion

    #region State

    /// <summary>Slot index → slot transform mapping.</summary>
    private readonly Dictionary<int, Transform> playerSlotDictionary = new Dictionary<int, Transform>();

    private int selectedCharacterIndex;
    private int playerLevel;

    private int aiCount;
    private int questIndex;

    /// <summary>
    /// The chosen class index stored to PlayerPrefs ("ChoosenClass").
    /// </summary>
    private int chosenClassIndex;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        Hide();
    }

    #endregion

    #region Lobby Setup & Update

    /// <summary>
    /// Configures the offline lobby with quest and AI configuration.
    /// </summary>
    /// <param name="lobbyName">Unused in offline mode (reserved for UI display if needed).</param>
    /// <param name="maxPlayers">Unused in offline mode; lobby uses slot count instead.</param>
    /// <param name="isPrivate">Unused in offline mode.</param>
    /// <param name="aiCount">Number of AI slots to show.</param>
    /// <param name="quest">Index into the quest list.</param>
    public void SetUpLobby(string lobbyName, int maxPlayers, bool isPrivate, int aiCount, int quest)
    {
        this.aiCount = aiCount;
        questIndex = quest;

        // Default to Warrior level initially
        playerLevel = PlayerPrefs.GetInt("Warrior_Level", 0);

        // Restore previously chosen class if present
        int savedClassIndex = PlayerPrefs.GetInt("ChoosenClass", 0);
        ChangeCharacter(savedClassIndex);

        UpdateLobby();
        Show();
    }

    /// <summary>
    /// Updates the lobby UI: quest description, player slot visuals, and AI slots.
    /// </summary>
    private void UpdateLobby()
    {
        // Quest description
        if (LobbyAssets.Instance != null &&
            LobbyAssets.Instance.quests != null &&
            questIndex >= 0 &&
            questIndex < LobbyAssets.Instance.quests.Length)
        {
            if (questDescriptionText != null)
            {
                questDescriptionText.text =
                    Localizer.Instance.GetLocalizedText(LobbyAssets.Instance.quests[questIndex].description);
            }
        }
        else
        {
            Debug.LogWarning($"[{nameof(OfflineLobbyUI)}] Quest index {questIndex} is out of range.", this);
        }

        // 0 - 9 (max players currently)
        for (int x = 0; x < playerSlotsContainer.childCount; x++)
        {
            if (!playerSlotDictionary.TryGetValue(x, out Transform slot))
                continue;

            var slotUI = slot.GetComponent<LobbyCharacter>();
            if (slotUI == null)
            {
                Debug.LogWarning($"[{nameof(OfflineLobbyUI)}] Slot at index {x} has no LobbyCharacter component.", slot);
                continue;
            }

            // Local player on slot 0
            if (x == 0)
            {
                slot.gameObject.SetActive(true);
                slotUI.UpdateOffline(selectedCharacterIndex, playerLevel);
            }
            // AI slots following the player
            else if (x < 1 + aiCount)
            {
                slot.gameObject.SetActive(true);

                Image slotImage = slot.transform.GetChild(2).GetComponent<Image>();
                if (slotImage != null && LobbyAssets.Instance != null && LobbyAssets.Instance.LobbyAISprite != null)
                {
                    slotImage.sprite = LobbyAssets.Instance.LobbyAISprite;
                }

                slotUI.UpdateAI();
                var button = slot.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = true;
                }
            }
            // Unused slots
            else
            {
                slot.gameObject.SetActive(false);
            }
        }

        Show();
    }

    #endregion

    #region Show / Hide

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Show()
    {
        gameObject.SetActive(true);
    }

    #endregion

    #region Ready / Leave

    /// <summary>
    /// Called when the Ready button is pressed. Saves selected classes and starts the game.
    /// </summary>
    private void ReadyUp()
    {
        // Save the chosen player class
        PlayerPrefs.SetInt("ChoosenClass", chosenClassIndex);

        // Save AI classes if they are valid (not -2)
        if (SpawnManager.Instance != null && SpawnManager.Instance.aiClasses != null)
        {
            for (int i = 1; i < SpawnManager.Instance.aiClasses.Length && i <= 8; i++)
            {
                int aiClass = SpawnManager.Instance.aiClasses[i];
                if (aiClass != -2)
                {
                    PlayerPrefs.SetInt($"ai{i}", aiClass);
                }
            }
        }

        OnlineRelay.Instance.InitializePlayerTracking(1);
        UIController.Instance.SetUp();
        Hide();
        EnemyAiClassFailsafe();
    }

    /// <summary>
    /// Ensures the enemy controller is updated to match lobby AI configuration.
    /// </summary>
    public void EnemyAiClassFailsafe()
    {
        for (int x = 0; x < playerSlotsContainer.childCount; x++)
        {
            var slot = playerSlotsContainer.GetChild(x);
            if (slot.TryGetComponent(out LobbyCharacter lobbyCharacter))
            {
                lobbyCharacter.UpdateEnemyController();
            }
        }
    }

    /// <summary>
    /// Leaves the lobby and clears AI configuration.
    /// </summary>
    private void LeaveLobby()
    {
        Hide();
        Debug.Log("[OfflineLobbyUI] Resetting AI class configuration.");

        if (SpawnManager.Instance != null && SpawnManager.Instance.aiClasses != null)
        {
            for (int i = 0; i < SpawnManager.Instance.aiClasses.Length; i++)
            {
                SpawnManager.Instance.aiClasses[i] = -2;
            }
        }
    }

    #endregion

    #region Character Selection

    /// <summary>
    /// Updates the selected character/class, UI state, and stored preferences.
    /// </summary>
    public void ChangeCharacter(int newCharacterIndex)
    {
        if (characterButtons != null && characterButtons.Length > 0)
        {
            foreach (Button button in characterButtons)
            {
                if (button != null)
                    button.interactable = true;
            }

            if (newCharacterIndex >= 0 &&
                newCharacterIndex < characterButtons.Length &&
                characterButtons[newCharacterIndex] != null)
            {
                characterButtons[newCharacterIndex].interactable = false;
            }
            else
            {
                Debug.LogWarning($"[{nameof(OfflineLobbyUI)}] Character index {newCharacterIndex} is out of range.", this);
            }
        }

        selectedCharacterIndex = newCharacterIndex;
        chosenClassIndex = newCharacterIndex;

        // Load level and set LobbyAssets ChoosenClass based on selection
        switch (newCharacterIndex)
        {
            case 0:
                playerLevel = PlayerPrefs.GetInt("Warrior_Level", 0);
                LobbyAssets.Instance.ChoosenClass = LobbyManager.PlayerCharacter.Warrior.ToString();
                break;
            case 1:
                playerLevel = PlayerPrefs.GetInt("Archer_Level", 0);
                LobbyAssets.Instance.ChoosenClass = LobbyManager.PlayerCharacter.Archer.ToString();
                break;
            case 2:
                playerLevel = PlayerPrefs.GetInt("Mage_Level", 0);
                LobbyAssets.Instance.ChoosenClass = LobbyManager.PlayerCharacter.Mage.ToString();
                break;
            case 3:
                playerLevel = PlayerPrefs.GetInt("Healer_Level", 0);
                LobbyAssets.Instance.ChoosenClass = LobbyManager.PlayerCharacter.Healer.ToString();
                break;
            case 4:
                playerLevel = PlayerPrefs.GetInt("Knight_Level", 0);
                LobbyAssets.Instance.ChoosenClass = LobbyManager.PlayerCharacter.Knight.ToString();
                break;
            case 5:
                playerLevel = PlayerPrefs.GetInt("Assassin_Level", 0);
                LobbyAssets.Instance.ChoosenClass = LobbyManager.PlayerCharacter.Assassin.ToString();
                break;
            case 6:
                playerLevel = PlayerPrefs.GetInt("Jester_Level", 0);
                LobbyAssets.Instance.ChoosenClass = LobbyManager.PlayerCharacter.Jester.ToString();
                break;
            case 7:
                playerLevel = PlayerPrefs.GetInt("Vampire_Level", 0);
                LobbyAssets.Instance.ChoosenClass = LobbyManager.PlayerCharacter.Vampire.ToString();
                break;
            default:
                Debug.LogWarning($"[{nameof(OfflineLobbyUI)}] Unhandled character index {newCharacterIndex}.", this);
                break;
        }

        UpdateLobby();
    }

    #endregion
}

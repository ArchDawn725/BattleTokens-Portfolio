using System.Threading.Tasks;
using TMPro;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyCharacter : MonoBehaviour
{
    #region Serialized Fields

    [Header("Slot Settings")]
    [Tooltip("Index of this player/AI slot (typically matches its position in the lobby).")]
    [SerializeField] private int playerNumber;

    [Tooltip("Internal AI state: -2 = no AI, -1 = random AI, >= 0 = specific AI index.")]
    [SerializeField] private int aiCharacter = -2;

    #endregion

    #region State & Cached Components

    private const int AiNone = -2;
    private const int AiRandom = -1;

    private bool _isAiSetup;

    // Cached UI references (child indices are assumed to be stable in the hierarchy)
    private Image _aiImage;              // child[1]
    private Image _characterFillImage;   // child[2]
    private TextMeshProUGUI _levelText;  // child[3]
    private GameObject _readyIndicator;  // child[4]
    private TextMeshProUGUI _nameText;   // child[5]
    private GameObject _kickIndicator;   // child[6]

    private void Awake()
    {
        // Cache commonly used child components to avoid repeated GetChild/GetComponent calls.
        try
        {
            _aiImage = transform.GetChild(1).GetComponent<Image>();
            _characterFillImage = transform.GetChild(2).GetComponent<Image>();
            _levelText = transform.GetChild(3).GetComponent<TextMeshProUGUI>();
            _readyIndicator = transform.GetChild(4).gameObject;
            _nameText = transform.GetChild(5).GetComponent<TextMeshProUGUI>();
            _kickIndicator = transform.GetChild(6).gameObject;
        }
        catch
        {
            Debug.LogError($"{nameof(LobbyCharacter)} on '{name}' failed to cache child UI components. Check hierarchy indices.", this);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Updates this slot to represent a network player from the lobby.
    /// </summary>
    public void UpdatePlayer(Player player)
    {
        if (!ValidateCoreUi()) return;

        var lobbyAssets = LobbyAssets.Instance;
        var localizer = Localizer.Instance;
        var lobbyManager = LobbyManager.Instance;

        if (lobbyAssets == null || localizer == null || lobbyManager == null)
        {
            Debug.LogError("One or more required singletons (LobbyAssets, Localizer, LobbyManager) are null in UpdatePlayer().", this);
            return;
        }

        // Character sprite
        if (player.Data.TryGetValue(LobbyManager.KEY_PLAYER_CHARACTER, out var charData))
        {
            if (System.Enum.TryParse<LobbyManager.PlayerCharacter>(charData.Value, out var character))
            {
                _characterFillImage.sprite = lobbyAssets.GetSprite(character);
            }
            else
            {
                Debug.LogWarning($"Failed to parse player character enum from value '{charData.Value}'. Using default sprite.", this);
            }
        }

        // Level text
        string levelValue = "0";
        if (player.Data.ContainsKey("ClassLevel"))
        {
            levelValue = player.Data["ClassLevel"].Value;
        }

        _levelText.text = localizer.GetLocalizedText("Lv.") + levelValue;

        // Player name
        if (player.Data.TryGetValue(LobbyManager.KEY_PLAYER_NAME, out var nameData))
        {
            _nameText.text = nameData.Value;
        }
        else
        {
            _nameText.text = lobbyManager.playerName;
        }
    }

    /// <summary>
    /// Updates this slot to represent an AI ally in the lobby.
    /// </summary>
    public void UpdateAI()
    {
        if (!ValidateCoreUi()) return;

        var spawnManager = SpawnManager.Instance;
        var localizer = Localizer.Instance;
        var lobbyManager = LobbyManager.Instance;

        if (spawnManager == null || localizer == null || lobbyManager == null)
        {
            Debug.LogError("One or more required singletons (SpawnManager, Localizer, LobbyManager) are null in UpdateAI().", this);
            return;
        }

        _levelText.text = string.Empty;     // level later on?
        _readyIndicator.SetActive(false);

        if (!_isAiSetup)
        {
            SetUpAI();
            _isAiSetup = true;
        }

        int aiClassIndex = spawnManager.aiClasses[playerNumber];
        if (aiClassIndex > AiNone)
        {
            var ally = spawnManager.availableAIAllies[aiClassIndex];
            _nameText.text = $"{localizer.GetLocalizedText("AI")} - {localizer.GetLocalizedText(ally.DisplayName)}";
        }
        else
        {
            _nameText.text = $"{localizer.GetLocalizedText("AI")} - {localizer.GetLocalizedText("Random")}";
        }

        if (lobbyManager.joinedLobby != null && !lobbyManager.IsLobbyHost())
        {
            ReadAiClasses();
        }
    }

    /// <summary>
    /// Updates this slot for an offline local character (no online lobby).
    /// </summary>
    public void UpdateOffline(int characterIndex, int level)
    {
        if (!ValidateCoreUi()) return;

        var lobbyAssets = LobbyAssets.Instance;
        var localizer = Localizer.Instance;
        var lobbyManager = LobbyManager.Instance;

        if (lobbyAssets == null || localizer == null || lobbyManager == null)
        {
            Debug.LogError("One or more required singletons (LobbyAssets, Localizer, LobbyManager) are null in UpdateOffline().", this);
            return;
        }

        if (characterIndex >= 0 && characterIndex < lobbyAssets.LobbySprites.Length)
        {
            _characterFillImage.sprite = lobbyAssets.LobbySprites[characterIndex];
        }
        else
        {
            Debug.LogWarning($"Character index {characterIndex} is out of bounds for LobbySprites.", this);
        }

        _levelText.text = localizer.GetLocalizedText("Lv.") + level;
        _nameText.text = lobbyManager.playerName;
    }

    /// <summary>
    /// Shows or hides the "ready" indicator for this slot.
    /// </summary>
    public void UpdateReadiness(bool isReady)
    {
        if (!ValidateCoreUi()) return;
        _readyIndicator.SetActive(isReady);
    }

    /// <summary>
    /// Resets this slot to its default state and clears AI configuration.
    /// </summary>
    public void ResetSlot()
    {
        if (!ValidateCoreUi()) return;

        var spawnManager = SpawnManager.Instance;
        if (spawnManager == null)
        {
            Debug.LogError("SpawnManager.Instance is null in Reset().", this);
            return;
        }

        _levelText.text = string.Empty;
        _readyIndicator.SetActive(false);
        _nameText.text = string.Empty;

        Debug.Log($"Resetting LobbyCharacter AI for slot {playerNumber}.", this);
        spawnManager.aiClasses[playerNumber] = AiNone;

        if (_characterFillImage != null)
        {
            _characterFillImage.fillAmount = 1f;
        }

        _isAiSetup = false;
        aiCharacter = AiNone;

        if (_kickIndicator != null)
        {
            _kickIndicator.SetActive(false);
        }
    }

    /// <summary>
    /// Initializes this slot to act as an AI slot.
    /// </summary>
    public void SetUpAI()
    {
        if (!ValidateCoreUi()) return;

        ResetSlot();
        aiCharacter = AiRandom;

        var spawnManager = SpawnManager.Instance;
        var lobbyAssets = LobbyAssets.Instance;
        var localizer = Localizer.Instance;
        var lobbyManager = LobbyManager.Instance;

        if (spawnManager == null || lobbyAssets == null || localizer == null || lobbyManager == null)
        {
            Debug.LogError("One or more required singletons (SpawnManager, LobbyAssets, Localizer, LobbyManager) are null in SetUpAI().", this);
            return;
        }

        // Load previously chosen AI class (if any)
        spawnManager.aiClasses[playerNumber] = PlayerPrefs.GetInt("ai" + transform.GetSiblingIndex(), AiRandom);

        if (lobbyManager.IsLobbyHost())
        {
            Debug.Log($"AI slot {playerNumber} is controlled by host.", this);
        }

        _characterFillImage.fillAmount = 0.5f;
        _aiImage.sprite = lobbyAssets.LobbyAISprite;
        _nameText.text = $"{localizer.GetLocalizedText("AI")} - {localizer.GetLocalizedText("Random")}";

        if (spawnManager.aiClasses[playerNumber] != AiRandom)
        {
            SetAICharacter(spawnManager.aiClasses[playerNumber]);
        }
    }

    /// <summary>
    /// Changes the AI character selection by the given delta.
    /// If aiCharacter == AiNone, this action will attempt to kick the player instead.
    /// </summary>
    public async void ChangeAICharacter(int delta)
    {
        var spawnManager = SpawnManager.Instance;
        var localizer = Localizer.Instance;
        var lobbyAssets = LobbyAssets.Instance;
        var lobbyUpdateThrottler = LobbyUpdateThrottler.Instance;

        if (spawnManager == null || localizer == null || lobbyAssets == null || lobbyUpdateThrottler == null)
        {
            Debug.LogError("One or more required singletons are null in ChangeAICharacter().", this);
            return;
        }

        if (aiCharacter != AiNone)
        {
            int value = spawnManager.aiClasses[playerNumber];
            value += delta;

            if (value >= spawnManager.availableAIAllies.Length) value = AiRandom;
            if (value < AiRandom) value = spawnManager.availableAIAllies.Length - 1;

            // Check DLC restrictions
            if (value > 3 && !TryDLC(value))
            {
                // Move to next candidate and try again
                spawnManager.aiClasses[playerNumber]++;
                ChangeAICharacter(delta);
                return;
            }

            Debug.Log("Succeeded DLC check for AI character change.", this);
            spawnManager.aiClasses[playerNumber] = value;
            aiCharacter = value;

            if (value != AiRandom)
            {
                _aiImage.sprite = lobbyAssets.LobbySprites[value];
                var ally = spawnManager.availableAIAllies[spawnManager.aiClasses[playerNumber]];
                _nameText.text = $"{localizer.GetLocalizedText("AI")} - {localizer.GetLocalizedText(ally.DisplayName)}";
            }
            else
            {
                _aiImage.sprite = lobbyAssets.LobbyAISprite;
                _nameText.text = $"{localizer.GetLocalizedText("AI")} - {localizer.GetLocalizedText("Random")}";
            }

            // Notify other clients
            lobbyUpdateThrottler.UpdateAICharacter();
        }
        else
        {
            // Kick player case
            Debug.Log($"Kicking player in slot {playerNumber}.", this);

            var lobbyUpdate = LobbyUpdateThrottler.Instance;
            if (lobbyUpdate == null)
            {
                Debug.LogError("LobbyUpdateThrottler.Instance is null; cannot kick player.", this);
                return;
            }

            if (_kickIndicator != null)
            {
                _kickIndicator.SetActive(true);
            }

            try
            {
                await lobbyUpdate.KickPlayer(playerNumber);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to kick player in slot {playerNumber}: {ex.Message}", this);
            }
        }
    }

    public void UpdateEnemyController()
    {
        var spawnManager = SpawnManager.Instance;
        if (spawnManager == null)
        {
            Debug.LogError("SpawnManager.Instance is null in UpdateEnemyController().", this);
            return;
        }

        spawnManager.aiClasses[playerNumber] = aiCharacter;
        Debug.Log($"Enemy controller updated from LobbyCharacter slot {playerNumber}: aiCharacter = {aiCharacter}", this);
    }

    #endregion

    #region Private Helpers

    private async void SetAICharacter(int value)
    {
        var spawnManager = SpawnManager.Instance;
        var localizer = Localizer.Instance;
        var lobbyAssets = LobbyAssets.Instance;
        var lobbyUpdateThrottler = LobbyUpdateThrottler.Instance;

        if (spawnManager == null || localizer == null || lobbyAssets == null || lobbyUpdateThrottler == null)
        {
            Debug.LogError("One or more required singletons are null in SetAICharacter().", this);
            return;
        }

        spawnManager.aiClasses[playerNumber] = value;
        aiCharacter = value;

        if (value != AiRandom)
        {
            _aiImage.sprite = lobbyAssets.LobbySprites[value];
            var ally = spawnManager.availableAIAllies[spawnManager.aiClasses[playerNumber]];
            _nameText.text = $"{localizer.GetLocalizedText("AI")} - {localizer.GetLocalizedText(ally.DisplayName)}";
        }
        else
        {
            _aiImage.sprite = lobbyAssets.LobbyAISprite;
            _nameText.text = $"{localizer.GetLocalizedText("AI")} - {localizer.GetLocalizedText("Random")}";
        }

        // Notify other clients
        lobbyUpdateThrottler.UpdateAICharacter();
        await Task.Yield(); // keep async signature valid even if we don't await anything heavy (optional)
    }

    private bool TryDLC(int value)
    {
        switch (value)
        {
            case 4:
                if (PlayerPrefs.GetInt("KnightClass", 0) == 0) return false;
                break;
            case 5:
                if (PlayerPrefs.GetInt("AssassinClass", 0) == 0) return false;
                break;
            case 6:
                if (PlayerPrefs.GetInt("JesterClass", 0) == 0) return false;
                break;
            case 7:
                if (PlayerPrefs.GetInt("VampireClass", 0) == 0) return false;
                break;
        }

        return true;
    }

    /// <summary>
    /// Reads AI classes from the joined lobby data and updates this client.
    /// </summary>
    private void ReadAiClasses()
    {
        var lobbyManager = LobbyManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogError("LobbyManager.Instance is null in ReadAiClasses().", this);
            return;
        }

        Lobby lobby = lobbyManager.joinedLobby;
        if (lobby == null)
        {
            Debug.LogWarning("No joined lobby found in ReadAiClasses().", this);
            return;
        }

        const int maxSlots = 9;
        int[] aiClasses = new int[maxSlots];

        for (int i = 0; i < maxSlots; i++)
        {
            if (lobby.Data.TryGetValue($"AI_{i}", out var entry) &&
                int.TryParse(entry.Value, out int val))
            {
                aiClasses[i] = val; // class id or -1
            }
            else
            {
                aiClasses[i] = AiRandom; // empty slot
            }
        }

        UpdateClientAI(aiClasses);
    }

    private void UpdateClientAI(int[] aiClasses)
    {
        var spawnManager = SpawnManager.Instance;
        var lobbyAssets = LobbyAssets.Instance;

        if (spawnManager == null || lobbyAssets == null)
        {
            Debug.LogError("SpawnManager or LobbyAssets is null in UpdateClientAI().", this);
            return;
        }

        if (playerNumber < 0 || playerNumber >= aiClasses.Length)
        {
            Debug.LogWarning($"playerNumber {playerNumber} is out of range in UpdateClientAI().", this);
            return;
        }

        int value = aiClasses[playerNumber];

        if (value != AiRandom)
        {
            _aiImage.sprite = lobbyAssets.LobbySprites[value];
        }
        else
        {
            _aiImage.sprite = lobbyAssets.LobbyAISprite;
        }

        spawnManager.aiClasses[playerNumber] = value;
        aiCharacter = value;
    }

    private bool ValidateCoreUi()
    {
        if (_characterFillImage == null || _levelText == null || _nameText == null)
        {
            Debug.LogError($"{nameof(LobbyCharacter)} on '{name}' is missing one or more core UI references.", this);
            return false;
        }
        return true;
    }

    #endregion
}

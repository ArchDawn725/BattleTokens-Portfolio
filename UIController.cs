using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    #region Singleton & Types

    public static UIController Instance { get; private set; }

    public enum GameState
    {
        Classes,
        Spawning,
        Battle,
        Upgrades,
    }

    #endregion

    #region Serialized Fields

    [Header("State")]
    [Tooltip("Current high-level state of the game flow.")]
    public GameState gState = GameState.Classes;

    [Header("References")]
    [Tooltip("Handles ready-up state for players and associated timers.")]
    [SerializeField] private PlayerReadyManager _playerReadyManager;

    [Tooltip("Animator controlling high-level UI transitions (scene panels, game over, etc.).")]
    public Animator Animator;

    #endregion

    #region Cached Controllers & Effects

    private ShakeController _shakeController;

    private TurnTimerUIController _turnTimer;
    private LoadoutUIController _loadout;
    private BattleHUDController _battleHUD;
    private RewardUIController _reward;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple {nameof(UIController)} instances detected. Keeping the first and destroying the new one on '{name}'.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (Animator == null)
        {
            Animator = GetComponent<Animator>();
            if (Animator == null)
            {
                Debug.LogError($"{nameof(UIController)} on '{name}' is missing an Animator reference.", this);
            }
        }

        _shakeController = Camera.main != null
            ? Camera.main.GetComponent<ShakeController>()
            : null;

        if (_shakeController == null)
        {
            Debug.LogWarning($"{nameof(ShakeController)} not found on main camera for {nameof(UIController)}.", this);
        }

        _turnTimer = TurnTimerUIController.Instance;
        _loadout = LoadoutUIController.Instance;
        _battleHUD = BattleHUDController.Instance;
        _reward = RewardUIController.Instance;

        if (_playerReadyManager == null)
        {
            Debug.LogError($"{nameof(PlayerReadyManager)} reference is not set on {nameof(UIController)}.", this);
        }

        // Subscribe to events
        OnlineRelay.OnAllPlayersReadyClientSide += HandleAllPlayersReady;
        OnlineRelay.OnAllPlayersReadyUpClientSide += HandleAllPlayersReadyUp;

        if (_playerReadyManager != null && _turnTimer != null)
        {
            _playerReadyManager.OnPlayerReadyUp += _turnTimer.StartNewTimer;
        }
    }

    private void OnDestroy()
    {
        gState = GameState.Classes;

        OnlineRelay.OnAllPlayersReadyClientSide -= HandleAllPlayersReady;
        OnlineRelay.OnAllPlayersReadyUpClientSide -= HandleAllPlayersReadyUp;

        if (_playerReadyManager != null && _turnTimer != null)
        {
            _playerReadyManager.OnPlayerReadyUp -= _turnTimer.StartNewTimer;
        }
    }

    #endregion

    #region Setup & Connection

    public void SetUp()
    {
        if (_loadout == null || _battleHUD == null)
        {
            Debug.LogError("UIController setup failed due to missing loadout or battle HUD references.", this);
            return;
        }

        var stats = PlayerStats.Instance;
        var lobbyAssets = LobbyAssets.Instance;
        var soundController = SoundController.Instance;

        if (stats == null || lobbyAssets == null || soundController == null)
        {
            Debug.LogError("UIController.SetUp() missing one or more singleton instances (PlayerStats, LobbyAssets, SoundController).", this);
            return;
        }

        _loadout.ChoosenClass(lobbyAssets.ChoosenClass);

        // Item button setup
        if (stats.Item == null)
        {
            _battleHUD.itemButton.interactable = false;
            _battleHUD.itemButton.gameObject.SetActive(false);
        }
        else
        {
            _battleHUD.itemButton.image.sprite = stats.Item.Sprite;
            _battleHUD.itemButton.image.color = lobbyAssets.RarityColors[(int)stats.Item.Rarity];
        }

        soundController.NewScene();
        soundController.MusicTransition(3);

        _battleHUD.turnText.text = "0";
        _battleHUD.turnText.color = Color.white;
    }

    public void AllPlayersConnected()
    {
        var relay = OnlineRelay.Instance;
        if (relay == null || _loadout == null)
        {
            Debug.LogError("UIController.AllPlayersConnected() missing OnlineRelay or loadout reference.", this);
            return;
        }

        Debug.Log("[UIController] All players connected.");

        if (relay.IsConnected())
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                relay.SetQuestCall(_loadout.QuestNumber);
            }
        }
        else
        {
            relay.SetQuestCall(_loadout.QuestNumber);
        }
    }

    #endregion

    #region Match Flow

    public void StartMatch()
    {
        if (_turnTimer == null || _loadout == null || _battleHUD == null)
        {
            Debug.LogError("UIController.StartMatch() missing one or more UI references.", this);
            return;
        }

        _turnTimer.turnEnded = false;

        _loadout.UpdateStats();
        _battleHUD.DisableGameActions();

        var spawnManager = SpawnManager.Instance;
        if (spawnManager != null)
        {
            spawnManager.ChooseMySpawn();
        }

        var localizer = Localizer.Instance;
        if (_battleHUD.BoardActive && localizer != null)
        {
            _battleHUD.tutorialText.text = localizer.GetLocalizedText("Choose your spawn location.");
        }

        Tutorial.Singleton.StartTutorial(1);
        _turnTimer.StopTimer();
    }

    public void HandleAllPlayersReady()
    {
        if (_turnTimer == null || _battleHUD == null || _reward == null)
        {
            Debug.LogError("UIController.HandleAllPlayersReady() missing one or more references.", this);
            return;
        }

        var stats = PlayerStats.Instance;
        var enemyController = EnemyController.Instance;
        var navigation = UINavigationController.Instance;
        var localizer = Localizer.Instance;

        Debug.Log($"[UIController] HandleAllPlayersReady in state: {gState}");

        switch (gState)
        {
            case GameState.Classes:
                stats?.StartUp();
                gState = GameState.Upgrades;
                if (_battleHUD.BoardActive)
                {
                    _battleHUD.tutorialText.text = string.Empty;
                }
                break;

            case GameState.Spawning:
                _battleHUD.endTurnButton.interactable = true;

                var spawnManager = SpawnManager.Instance;
                if (spawnManager != null)
                {
                    StartCoroutine(spawnManager.SpawnEnemies());
                }

                gState = GameState.Battle;

                if (stats != null)
                {
                    if (stats.Item == null)
                    {
                        _battleHUD.itemButton.interactable = false;
                    }
                    else
                    {
                        if (stats.Item.Usable)
                        {
                            _battleHUD.itemButton.interactable = true;
                            _battleHUD.itemUsed = false;
                        }
                        else
                        {
                            _battleHUD.itemButton.interactable = false;
                        }
                    }
                }

                navigation?.JumpToElement(_battleHUD.endTurnButton);

                if (_battleHUD.BoardActive && localizer != null)
                {
                    _battleHUD.tutorialText.text = localizer.GetLocalizedText("Choose an action.");
                }

                if (stats != null && enemyController != null)
                {
                    _battleHUD.actionPointPlus.text = "+" + stats.MaxActionPoints;
                    _battleHUD.upgradePointPlus.text = "+" +
                        ((int)Math.Ceiling(enemyController.wave * _reward.quest.pointsModifier)).ToString();
                }

                _shakeController?.Enable();

                break;

            case GameState.Battle:
                if (_battleHUD.BoardActive && localizer != null)
                {
                    _battleHUD.tutorialText.text = localizer.GetLocalizedText("Opponent's turn.");
                }

                _turnTimer.StopTimer();
                _turnTimer.turnEnded = true;
                TurnHandler.Instance?.StartEnemyTurnAsync();
                break;
        }

        _turnTimer.StopTimer();
        _turnTimer.turnEnded = false;
    }

    private void HandleAllPlayersReadyUp()
    {
        if (_turnTimer == null || _battleHUD == null || _reward == null)
        {
            Debug.LogError("UIController.HandleAllPlayersReadyUp() missing one or more references.", this);
            return;
        }

        Debug.Log("[UIController] Players ready for next step (Upgrades -> Spawning).");

        var relay = OnlineRelay.Instance;
        var networkManager = NetworkManager.Singleton;

        if ((networkManager != null && networkManager.IsHost) || relay == null || !relay.IsConnected())
        {
            relay?.ChangeSceneCall(1);
        }

        relay?.GameStartCall();
        gState = GameState.Spawning;

        var localizer = Localizer.Instance;
        if (_battleHUD.BoardActive && localizer != null)
        {
            _battleHUD.tutorialText.text = localizer.GetLocalizedText("Choose your spawn location.");
        }

        var soundController = SoundController.Instance;
        soundController?.MusicTransition(0);

        _battleHUD.turnText.text = "0";
        _battleHUD.turnText.color = Color.white;
        _battleHUD.clock.SetPercent(0);
    }

    public async Task PlayerTurn()
    {
        if (_turnTimer == null || _battleHUD == null || _loadout == null || _reward == null)
        {
            Debug.LogError("UIController.PlayerTurn() missing one or more references.", this);
            return;
        }

        var stats = PlayerStats.Instance;
        var gridManager = GridManager.Instance;
        var enemyController = EnemyController.Instance;
        var navigation = UINavigationController.Instance;
        var localizer = Localizer.Instance;

        _turnTimer.turnEnded = false;

        if (gridManager != null)
        {
            await gridManager.ClearAll();
        }

        bool alive = enemyController != null && await enemyController.PlayerTurn();

        Tutorial.Singleton.StartTutorial(2);
        _battleHUD.UpdateGameActions();
        _loadout.UpdateStats();
        navigation?.JumpToElement(_battleHUD.endTurnButton);

        if (_battleHUD.BoardActive && localizer != null)
        {
            if (stats != null && stats.myCharacter != null)
            {
                _battleHUD.tutorialText.text = localizer.GetLocalizedText("Choose an action.");
            }
            else
            {
                _battleHUD.tutorialText.text = string.Empty;
                _battleHUD.actionPointPlus.text = "+0";
            }
        }

        if (enemyController != null)
        {
            int turn = enemyController.turn;
            _battleHUD.turnText.text = turn.ToString();

            var quest = _reward.quest;
            var waveIndex = enemyController.wave - 1;
            var waveData = quest.waves[waveIndex];

            bool bossWave = waveData.Boss;
            if ((bossWave && turn > 20) || (!bossWave && turn > 10))
            {
                _battleHUD.turnText.color = Color.red;
                _battleHUD.clock.SetPercent(100);
            }
            else
            {
                _battleHUD.turnText.color = Color.white;
                int step = bossWave ? 5 : 10;
                _battleHUD.clock.SetPercent((turn - 1) * step);
            }
        }

        if (alive && stats != null && stats.myCharacter != null)
        {
            _battleHUD.endTurnButton.interactable = true;
            if (stats.myCharacter.Stats.ActionPoints <= 0 && stats.autoEndTurnSetting)
            {
                EndTurn(false);
            }
        }
    }

    public void EndTurn(bool triggerTurnTimer)
    {
        if (_battleHUD == null || _turnTimer == null)
        {
            Debug.LogError("UIController.EndTurn() missing battleHUD or turnTimer references.", this);
            return;
        }

        var relay = OnlineRelay.Instance;
        var stats = PlayerStats.Instance;
        var gridManager = GridManager.Instance;

        if (_battleHUD.ActiveCharacterInfo != null)
        {
            _battleHUD.ActiveCharacterInfo.HideInfo();
        }

        if (relay != null && relay.IsConnected())
        {
            _playerReadyManager?.SetPlayerReadyForTurnEndServerRpc();

            if (triggerTurnTimer)
            {
                _playerReadyManager?.AttemptTimerStartServerRpc();
            }

            var localizer = Localizer.Instance;
            if (_battleHUD.BoardActive && localizer != null)
            {
                _battleHUD.tutorialText.text = localizer.GetLocalizedText("Waiting on other players.");
            }

            if (stats != null && stats.myCharacter != null && gridManager != null)
            {
                relay.CharacterEndTurnCall(gridManager.MyPlayerLocation);
            }
        }
        else
        {
            HandleAllPlayersReady();
        }

        _turnTimer.turnEnded = true;

        var barImage = _turnTimer.transform.GetChild(1).GetChild(0).GetComponent<Image>();
        barImage.color = Color.green;

        _battleHUD.endTurnButton.interactable = false;
        _battleHUD.DisableGameActions();
        _battleHUD.tempDisable = false;
    }

    public void ReadyUp()
    {
        if (_battleHUD == null || _turnTimer == null)
        {
            Debug.LogError("UIController.ReadyUp() missing battleHUD or turnTimer references.", this);
            return;
        }

        if (_battleHUD.ActiveCharacterInfo != null)
        {
            _battleHUD.ActiveCharacterInfo.HideInfo();
        }

        Animator?.SetTrigger("Trigger");

        var relay = OnlineRelay.Instance;
        if (relay != null && relay.IsConnected())
        {
            _playerReadyManager?.SetPlayerReadyForCombatStartServerRpc();
            _playerReadyManager?.AttemptTimerStartServerRpc();
        }
        else
        {
            HandleAllPlayersReadyUp();
        }

        var barImage = _turnTimer.transform.GetChild(1).GetChild(0).GetComponent<Image>();
        barImage.color = Color.green;
    }

    public void RoundWon(int wave)
    {
        if (_battleHUD == null || _loadout == null || _turnTimer == null || _reward == null)
        {
            Debug.LogError("UIController.RoundWon() missing one or more references.", this);
            return;
        }

        var stats = PlayerStats.Instance;
        var enemyController = EnemyController.Instance;
        var gridManager = GridManager.Instance;
        var relay = OnlineRelay.Instance;
        var soundController = SoundController.Instance;
        var navigation = UINavigationController.Instance;

        if (_battleHUD.ActiveCharacterInfo != null)
        {
            _battleHUD.ActiveCharacterInfo.HideInfo();
        }

        gState = GameState.Upgrades;

        if (stats != null)
        {
            stats.UpgradePoints += (int)Math.Ceiling(wave * _reward.quest.pointsModifier);
        }

        float questXp = _reward.QuestXP(wave);
        _reward.UpdateReward(questXp);
        Debug.Log($"QuestXP-W{wave}: {questXp}");

        gridManager?.ClearBoard();

        _loadout.UpdateStatsUpgradeMenu();
        _loadout.UpdateUpgrades();

        _battleHUD.endTurnButton.interactable = false;
        _battleHUD.DisableGameActions();

        if (enemyController != null &&
            enemyController.Quest != null &&
            wave + 1 > enemyController.Quest.waves.Count)
        {
            Debug.Log("Quest completed. Game won.");
            GameOver(wave, true, false);
            return;
        }

        var networkManager = NetworkManager.Singleton;
        if ((networkManager != null && networkManager.IsHost) || relay == null || !relay.IsConnected())
        {
            relay?.ChangeSceneCall(2);
        }

        _turnTimer.StopTimer();
        _turnTimer.turnEnded = false;
        navigation?.JumpToElement(_loadout.doneUpgradeButton);

        if (_battleHUD.BoardActive)
        {
            _battleHUD.tutorialText.text = string.Empty;
        }

        soundController?.MusicTransition(3);
        Tutorial.Singleton.StartTutorial(3);

        _battleHUD.specialSummonText.text = string.Empty;
        _battleHUD.specialSummonText.transform.parent.gameObject.SetActive(false);
        _battleHUD.UpdateActionsValuesNoCharacter();

        _playerReadyManager?.Reset();
        TurnHandler.Instance?.Cancel();

        _battleHUD.actionPointPlus.text = "+0";
        stats?.NewRound();

        _shakeController?.Disable();
    }

    public void GameOver(int wave, bool won, bool hostDisconnected)
    {
        if (Animator != null && Animator.GetBool("GameOver"))
        {
            return;
        }

        if (_battleHUD == null || _turnTimer == null || _reward == null)
        {
            Debug.LogError("UIController.GameOver() missing one or more references.", this);
            return;
        }

        if (Animator != null)
        {
            Animator.SetBool("GameOver", true);
            Animator.SetInteger("Scene", 3);
        }

        if (_battleHUD.ActiveCharacterInfo != null)
        {
            _battleHUD.ActiveCharacterInfo.HideInfo();
        }

        _turnTimer.gameObject.SetActive(false);

        var localizer = Localizer.Instance;

        if (won)
        {
            _battleHUD.waveCountText.text = localizer != null
                ? localizer.GetLocalizedText("Game Won")
                : "Game Won";
            _battleHUD.waveCountText.color = Color.green;
        }
        else if (!hostDisconnected)
        {
            _battleHUD.waveCountText.text = localizer != null
                ? localizer.GetLocalizedText("Game Lost")
                : "Game Lost";
            _battleHUD.waveCountText.color = Color.red;
        }

        _turnTimer.StopTimer();

        _battleHUD.tutorialText.text = string.Empty;
        _battleHUD.specialSummonText.text = string.Empty;
        _battleHUD.specialSummonText.transform.parent.gameObject.SetActive(false);

        _playerReadyManager?.Reset();
        _shakeController?.Disable();

        _reward.GameOver(wave, won, hostDisconnected);
    }

    public void LeaveGame()
    {
        var soundController = SoundController.Instance;
        var lobbyManager = LobbyManager.Instance;
        var networkManager = NetworkManager.Singleton;

        soundController?.MusicTransition(5);
        lobbyManager?.LeaveLobby();

        if (networkManager != null)
        {
            networkManager.Shutdown();
            Destroy(networkManager.gameObject);
        }

        SceneManager.LoadScene(0);
    }

    public void ChangedSceneCalled(int scene)
    {
        if (Animator != null)
        {
            Animator.SetInteger("Scene", scene);
        }

        _shakeController?.ResetEffects();

        if (_battleHUD != null)
        {
            _battleHUD.BoardActive = (scene == 1);
        }
    }

    #endregion
}

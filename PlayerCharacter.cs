using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles setup and lifecycle behavior for the local player's character.
/// </summary>
[RequireComponent(typeof(Character))]
public class PlayerCharacter : MonoBehaviour
{
    #region Fields

    private ulong _clientId;
    private Character _character;

    #endregion

    #region Public Methods

    /// <summary>
    /// Initializes this player character using local player stats and lobby data.
    /// </summary>
    /// <param name="localClientId">The Netcode client ID for this player.</param>
    public void Setup(ulong localClientId)
    {
        if (_character == null)
        {
            _character = GetComponent<Character>();
            if (_character == null)
            {
                Debug.LogError($"{nameof(PlayerCharacter)} on '{name}' is missing a Character component.", this);
                return;
            }
        }

        // Cache instances used more than once in this method.
        var stats = PlayerStats.Instance;
        var lobbyManager = LobbyManager.Instance;
        var battleHud = BattleHUDController.Instance;

        if (stats == null)
        {
            Debug.LogError($"{nameof(PlayerStats)} instance is null. Cannot set up player character.", this);
            return;
        }

        if (lobbyManager == null)
        {
            Debug.LogError($"{nameof(LobbyManager)} instance is null. Cannot set up player character.", this);
            return;
        }

        if (battleHud == null)
        {
            Debug.LogError($"{nameof(BattleHUDController)} instance is null. Cannot set up player character.", this);
            return;
        }

        var special = ClassSpecial.None;
        if (stats.classSpecialUnlocked && stats.choosenClass != null)
        {
            special = stats.choosenClass.SpecialAbility;
        }

        var variables = new CharacterVariables
        {
            LocalClientId = localClientId,
            Name = lobbyManager.playerName,
            Health = stats.MaxHealth,
            Defence = stats.Defence,
            ImageIndex = (int)stats.playerClass,
            Damage = stats.Damage,
            ClassSpecial = special,
            ActionPoints = stats.MaxActionPoints,
            CritChance = stats.CritChance,
            CritMultiplier = stats.CritMulti,
            ActionIds = battleHud.GetMyActionsIDs(),
        };

        _character.SetUp(variables);
        _clientId = localClientId;
    }

    /// <summary>
    /// Signals the start of a new turn for this character.
    /// </summary>
    public void OnNewTurn()
    {
        if (_character == null)
        {
            _character = GetComponent<Character>();
            if (_character == null)
            {
                Debug.LogError($"{nameof(PlayerCharacter)} on '{name}' is missing a Character component.", this);
                return;
            }
        }

        _character.NextTurn();
    }

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        // Cache component early for safety/performance.
        _character = GetComponent<Character>();
        if (_character == null)
        {
            Debug.LogError($"{nameof(PlayerCharacter)} on '{name}' is missing a Character component.", this);
        }
    }

    private void OnDestroy()
    {
        // Cache instances used multiple times in this method.
        var enemyController = EnemyController.Instance;
        var battleHud = BattleHUDController.Instance;
        var relay = OnlineRelay.Instance;
        var networkManager = NetworkManager.Singleton;

        if (enemyController != null && enemyController.allies != null)
        {
            enemyController.allies.Remove(gameObject);
        }
        else
        {
            Debug.LogWarning($"{nameof(EnemyController)} instance or allies list is null when destroying '{name}'.", this);
        }

        if (battleHud == null)
        {
            Debug.LogWarning($"{nameof(BattleHUDController)} instance is null when destroying '{name}'. Player death UI will not be triggered.", this);
            return;
        }

        bool isOnline = relay != null && relay.IsConnected();

        if (isOnline)
        {
            if (networkManager != null && _clientId == networkManager.LocalClientId)
            {
                battleHud.PlayerDeath();
            }
        }
        else
        {
            battleHud.PlayerDeath();
        }
    }

    #endregion
}

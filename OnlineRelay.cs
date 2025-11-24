using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Central relay for all online/networked events.
/// Wraps RPC calls so game systems can call simple methods (e.g. SpawnPlayerCall)
/// and this class handles whether to route them locally or over the network.
/// </summary>
public class OnlineRelay : NetworkBehaviour
{
    #region Singleton

    public static OnlineRelay Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(OnlineRelay)}] Duplicate instance detected, destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired on each client when all players are ready to end the turn.
    /// </summary>
    public static event Action OnAllPlayersReadyClientSide;

    /// <summary>
    /// Fired on each client when all players are ready to start combat.
    /// </summary>
    public static event Action OnAllPlayersReadyUpClientSide;

    #endregion

    #region References & Setup

    [Header("References")]
    [Tooltip("Manages per-player ready states for turn end and combat start.")]
    [SerializeField] private PlayerReadyManager playerReadyManager;

    [Header("Connection Tracking")]
    [Tooltip("Total number of players expected in this online session.")]
    [SerializeField] private int totalPlayers;

    private int connectedPlayers;

    private void Start()
    {
        if (playerReadyManager == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] PlayerReadyManager reference is missing.", this);
        }
        else
        {
            playerReadyManager.OnAllPlayersReadyForTurnEnd += HandleAllPlayersReadyForTurnEnd;
            playerReadyManager.OnAllPlayersReadyForCombatStart += HandleAllPlayersReadyForCombatStart;
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from ready manager events
        if (playerReadyManager != null)
        {
            playerReadyManager.OnAllPlayersReadyForTurnEnd -= HandleAllPlayersReadyForTurnEnd;
            playerReadyManager.OnAllPlayersReadyForCombatStart -= HandleAllPlayersReadyForCombatStart;
        }

        // Notify host-leave only if we are actually in a network session
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsHost &&
            IsConnected())
        {
            HostLeftCall();
        }

        // Unsubscribe from network manager events
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    #endregion

    #region Player Ready Logic

    /// <summary>
    /// Called (on host) when all players are ready to end their turn.
    /// Broadcasts to clients and resets readiness.
    /// </summary>
    private void HandleAllPlayersReadyForTurnEnd()
    {
        NotifyAllClientsReadyClientRpc();
        playerReadyManager.Reset();
    }

    /// <summary>
    /// Called (on host) when all players are ready to start combat.
    /// Broadcasts to clients and resets readiness.
    /// </summary>
    private void HandleAllPlayersReadyForCombatStart()
    {
        NotifyAllClientsReadyUpClientRpc();
        playerReadyManager.Reset();
    }

    [ClientRpc]
    private void NotifyAllClientsReadyClientRpc()
    {
        OnAllPlayersReadyClientSide?.Invoke();
    }

    [ClientRpc]
    private void NotifyAllClientsReadyUpClientRpc()
    {
        OnAllPlayersReadyUpClientSide?.Invoke();
    }

    #endregion

    #region Spawning Players & Enemies

    /// <summary>
    /// Request to spawn a player character at the given location.
    /// Routes to RPC if connected, otherwise calls SpawnManager directly.
    /// </summary>
    public void SpawnPlayerCall(string location, ulong localClientId)
    {
        if (IsConnected())
            SpawnPlayerServerRpc(location, localClientId);
        else
            SpawnManager.Instance.SpawnPlayerCalled(location, localClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnPlayerServerRpc(string location, ulong localClientId)
    {
        SpawnPlayerClientRpc(location, localClientId);
    }

    [ClientRpc]
    private void SpawnPlayerClientRpc(string location, ulong localClientId)
    {
        if (SpawnManager.Instance == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] SpawnManager instance is null in {nameof(SpawnPlayerClientRpc)}.");
            return;
        }

        SpawnManager.Instance.SpawnPlayerCalled(location, localClientId);
    }

    /// <summary>
    /// Request to spawn an enemy or AI ally.
    /// </summary>
    public void SpawnCall(string location, int enemy, bool isAIAlly, int points)
    {
        if (IsConnected())
            SpawnServerRpc(location, enemy, isAIAlly, points);
        else
            SpawnManager.Instance.SpawnEnemyCalled(location, enemy, isAIAlly, points);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnServerRpc(string location, int enemy, bool isAIAlly, int points)
    {
        SpawnClientRpc(location, enemy, isAIAlly, points);
    }

    [ClientRpc]
    private void SpawnClientRpc(string location, int enemy, bool isAIAlly, int points)
    {
        if (SpawnManager.Instance == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] SpawnManager instance is null in {nameof(SpawnClientRpc)}.");
            return;
        }

        SpawnManager.Instance.SpawnEnemyCalled(location, enemy, isAIAlly, points);
    }

    #endregion

    #region Attack Logic

    /// <summary>
    /// Request to resolve an attack across the network.
    /// </summary>
    public void AttackCall(AttackVariables attackVariables)
    {
        if (IsConnected())
            AttackCallServerRpc(attackVariables);
        else
            AttackResolver.Instance.AttackCall(attackVariables);
    }

    [ServerRpc(RequireOwnership = false)]
    private void AttackCallServerRpc(AttackVariables attackVariables)
    {
        AttackClientRpc(attackVariables);
    }

    [ClientRpc]
    private void AttackClientRpc(AttackVariables attackVariables)
    {
        if (AttackResolver.Instance == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] AttackResolver instance is null in {nameof(AttackClientRpc)}.");
            return;
        }

        AttackResolver.Instance.AttackCall(attackVariables);
    }

    /// <summary>
    /// Notifies all clients about an action being used (for AP, cooldowns, etc.).
    /// </summary>
    public void ActionUse(AttackVariables attackVariables)
    {
        if (IsConnected())
            ActionUseServerRpc(attackVariables);
        else
            AttackResolver.Instance.ActionUseRelay(attackVariables);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ActionUseServerRpc(AttackVariables attackVariables)
    {
        ActionUseClientRpc(attackVariables);
    }

    [ClientRpc]
    private void ActionUseClientRpc(AttackVariables attackVariables)
    {
        if (AttackResolver.Instance == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] AttackResolver instance is null in {nameof(ActionUseClientRpc)}.");
            return;
        }

        AttackResolver.Instance.ActionUseRelay(attackVariables);
    }

    #endregion

    #region Character Stat Setup

    /// <summary>
    /// Sets a character's stats on all clients using CharacterVariables.
    /// </summary>
    public void SetCharacterValues(CharacterVariables variables)
    {
        if (IsConnected())
        {
            SetCharacterValuesServerRpc(variables);
        }
        else
        {
            var character = TargetSelectionService.Instance.GetCharacter(variables.Location);
            if (character == null)
            {
                Debug.LogError($"[{nameof(OnlineRelay)}] Character at location '{variables.Location}' not found (offline SetCharacterValues).");
                return;
            }

            character.SetUpCall(variables);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetCharacterValuesServerRpc(CharacterVariables variables)
    {
        SetCharacterValuesClientRpc(variables);
    }

    [ClientRpc]
    private void SetCharacterValuesClientRpc(CharacterVariables variables)
    {
        var character = TargetSelectionService.Instance.GetCharacter(variables.Location);
        if (character == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] Character at location '{variables.Location}' not found (online SetCharacterValues).");
            return;
        }

        character.SetUpCall(variables);
    }

    #endregion

    #region Turn Management & Scene Logic

    /// <summary>
    /// Broadcasts that the player turn has begun.
    /// </summary>
    public void PlayerTurnCall()
    {
        Debug.Log($"[{nameof(OnlineRelay)}] Player turn called.");

        if (IsConnected())
        {
            if (NetworkManager.Singleton.IsHost)
                PlayerTurnCallServerRpc();
        }
        else
        {
            UIController.Instance.PlayerTurn();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayerTurnCallServerRpc()
    {
        PlayerTurnCallClientRpc();
    }

    [ClientRpc]
    private void PlayerTurnCallClientRpc()
    {
        UIController.Instance.PlayerTurn();
    }

    /// <summary>
    /// Broadcasts that the game should start (after all players are ready).
    /// </summary>
    public void GameStartCall()
    {
        if (IsConnected())
            GameStartCallServerRpc();
        else
            UIController.Instance.StartMatch();
    }

    [ServerRpc(RequireOwnership = false)]
    private void GameStartCallServerRpc()
    {
        GameStartCallClientRpc();
    }

    [ClientRpc]
    private void GameStartCallClientRpc()
    {
        UIController.Instance.StartMatch();
    }

    /// <summary>
    /// Broadcasts a scene change index to all clients.
    /// </summary>
    public void ChangeSceneCall(int sceneIndex)
    {
        if (IsConnected())
            ChangeSceneCallServerRpc(sceneIndex);
        else
            UIController.Instance.ChangedSceneCalled(sceneIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChangeSceneCallServerRpc(int sceneIndex)
    {
        ChangeSceneCallClientRpc(sceneIndex);
    }

    [ClientRpc]
    private void ChangeSceneCallClientRpc(int sceneIndex)
    {
        UIController.Instance.ChangedSceneCalled(sceneIndex);
    }

    /// <summary>
    /// Sets the selected quest on all clients.
    /// </summary>
    public void SetQuestCall(int quest)
    {
        if (IsConnected())
            SetQuestCallServerRpc(quest);
        else
            LoadoutUIController.Instance.SetQuest(quest);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetQuestCallServerRpc(int quest)
    {
        SetQuestCallClientRpc(quest);
    }

    [ClientRpc]
    private void SetQuestCallClientRpc(int quest)
    {
        LoadoutUIController.Instance.SetQuest(quest);
    }

    /// <summary>
    /// Requests a character movement from startPos to endPos.
    /// </summary>
    public void MoveCharacterCall(string startPos, string endPos)
    {
        if (IsConnected())
            MoveCharacterCallServerRpc(startPos, endPos);
        else
            AttackResolver.Instance.MoveCharacter(startPos, endPos);
    }

    [ServerRpc(RequireOwnership = false)]
    private void MoveCharacterCallServerRpc(string startPos, string endPos)
    {
        MoveCharacterCallClientRpc(startPos, endPos);
    }

    [ClientRpc]
    private void MoveCharacterCallClientRpc(string startPos, string endPos)
    {
        if (AttackResolver.Instance == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] AttackResolver instance is null in {nameof(MoveCharacterCallClientRpc)}.");
            return;
        }

        AttackResolver.Instance.MoveCharacter(startPos, endPos);
    }

    /// <summary>
    /// Marks a character as having ended their turn (ready-up indicator).
    /// </summary>
    public void CharacterEndTurnCall(string playerLoc)
    {
        if (IsConnected())
            CharacterEndTurnServerRpc(playerLoc);
        // Offline: no-op (existing behavior)
    }

    [ServerRpc(RequireOwnership = false)]
    private void CharacterEndTurnServerRpc(string playerLoc)
    {
        CharacterEndTurnClientRpc(playerLoc);
    }

    [ClientRpc]
    private void CharacterEndTurnClientRpc(string playerLoc)
    {
        var character = TargetSelectionService.Instance.GetCharacter(playerLoc);
        if (character == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] Character at location '{playerLoc}' not found in {nameof(CharacterEndTurnClientRpc)}.");
            return;
        }

        if (character.View != null && character.View.readyUp != null)
            character.View.readyUp.SetActive(true);
        else
            Debug.LogWarning($"[{nameof(OnlineRelay)}] Character view or readyUp root missing for '{playerLoc}'.");
    }

    #endregion

    #region Host Leave Handling

    /// <summary>
    /// Call when the host is leaving the match. Broadcasts to all clients.
    /// </summary>
    public void HostLeftCall()
    {
        if (IsConnected())
            HostLeftCallServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void HostLeftCallServerRpc()
    {
        HostLeftCallClientRpc();
    }

    [ClientRpc]
    private void HostLeftCallClientRpc()
    {
        if (EnemyController.Instance == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] EnemyController instance is null in {nameof(HostLeftCallClientRpc)}.");
            return;
        }

        EnemyController.Instance.HostLeft();
    }

    #endregion

    #region Connection Tracking

    /// <summary>
    /// Starts tracking client connections. Call this when you know how many
    /// players you expect in the session (from lobby, etc.).
    /// </summary>
    public void InitializePlayerTracking(int playerCount)
    {
        totalPlayers = playerCount;
        connectedPlayers = 0;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError($"[{nameof(OnlineRelay)}] NetworkManager is null in {nameof(InitializePlayerTracking)}.");
            return;
        }

        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

        // Host self-connection callback is not guaranteed; simulate if needed.
        Invoke(nameof(HostSelfConnectedDelay), 0.1f);
    }

    private void HostSelfConnectedDelay()
    {
        // For the host, OnClientConnected(0) may not fire the same way.
        OnClientConnected(0);
    }

    private void OnClientConnected(ulong clientId)
    {
        connectedPlayers++;
        Debug.Log($"[{nameof(OnlineRelay)}] Player connected: {clientId}. Total connected: {connectedPlayers}/{totalPlayers}");

        if (connectedPlayers == totalPlayers)
        {
            OnAllPlayersConnected();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        connectedPlayers = Mathf.Max(0, connectedPlayers - 1);
        Debug.Log($"[{nameof(OnlineRelay)}] Player disconnected: {clientId}. Remaining: {connectedPlayers}/{totalPlayers}");
    }

    private void OnAllPlayersConnected()
    {
        Debug.Log($"[{nameof(OnlineRelay)}] All players are connected.");
        UIController.Instance.AllPlayersConnected();
        // Further startup logic can go here.
    }

    /// <summary>
    /// True if a Netcode session is currently active (client or host).
    /// </summary>
    public bool IsConnected()
    {
        return NetworkManager.Singleton != null &&
               (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsHost);
    }

    #endregion
}

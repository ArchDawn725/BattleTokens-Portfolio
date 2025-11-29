using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Handles basic online flow and lobby player name synchronization.
/// Tracks player names on the server and propagates them to all clients.
/// </summary>
public class OnlineManager : NetworkBehaviour
{
    #region Singleton & Core State

    public static OnlineManager Instance { get; private set; }

    [Header("Runtime State")]
    [Tooltip("Local player's chosen display name.")]
    [SerializeField] private string _playerName;

    /// <summary>Local player's chosen display name.</summary>
    public string PlayerName
    {
        get => _playerName;
        private set => _playerName = value;
    }

    /// <summary>Server-side mapping of ClientId → player display name.</summary>
    private readonly Dictionary<ulong, string> _playerNameMap = new Dictionary<ulong, string>();

    #endregion

    #region UI References

    [Header("UI References")]
    [Tooltip("Panel used for initial name entry.")]
    [SerializeField] private GameObject _nameScreen;

    [Tooltip("Main menu/start scene root object.")]
    [SerializeField] private GameObject _startScene;

    [Tooltip("Lobby scene root object.")]
    [SerializeField] private GameObject _lobbyScene;

    [Tooltip("Character select scene root object.")]
    [SerializeField] private GameObject _characterSelectScene;

    [Tooltip("Prefab used to display a player's name in the lobby.")]
    [SerializeField] private GameObject _nameTagPrefab;

    [Tooltip("Parent transform that holds all lobby name tags.")]
    [SerializeField] private Transform _tagHolder;

    [Tooltip("TMP text field used to edit the local player's name.")]
    [SerializeField] private TextMeshProUGUI _nameChangeText;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple {nameof(OnlineManager)} instances detected. Destroying duplicate on '{name}'.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError($"{nameof(OnlineManager)} on '{name}' could not find NetworkManager.Singleton. Online features will not work.", this);
            return;
        }

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Name Entry & Authentication

    /// <summary>
    /// Updates the cached local player name from the input text field.
    /// </summary>
    public void UpdateName()
    {
        if (_nameChangeText == null)
        {
            Debug.LogWarning($"{nameof(OnlineManager)} on '{name}' has no nameChangeText assigned.", this);
            return;
        }

        PlayerName = _nameChangeText.text;
    }

    /// <summary>
    /// Confirms the entered name and proceeds to authentication / start screen.
    /// </summary>
    public void NameReady()
    {
        if (_nameScreen != null)
        {
            _nameScreen.SetActive(false);
        }

        if (_startScene != null)
        {
            _startScene.SetActive(true);
        }

        var lobbyManager = LobbyManager.Instance;
        if (lobbyManager != null)
        {
            lobbyManager.Authenticate(PlayerName);
        }
        else
        {
            Debug.LogError($"{nameof(LobbyManager)} instance is null in {nameof(NameReady)}.", this);
        }
    }

    #endregion

    #region Multiplayer Start / Stop

    /// <summary>
    /// Starts host or client networking and transitions to the lobby scene.
    /// </summary>
    /// <param name="host">True to start as host, false to start as client.</param>
    public void MultiplayerStart(bool host)
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError($"{nameof(OnlineManager)} could not start multiplayer because NetworkManager.Singleton is null.", this);
            return;
        }

        if (host)
        {
            networkManager.StartHost();
        }
        else
        {
            networkManager.StartClient();
        }

        if (_startScene != null)
        {
            _startScene.SetActive(false);
        }

        if (_lobbyScene != null)
        {
            _lobbyScene.SetActive(true);
        }

        // Once the OnlineManager's NetworkObject is spawned, register or send name.
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            if (IsServer)
            {
                _playerNameMap[networkManager.LocalClientId] = PlayerName;
                UpdateAllClientsLobbyUI();
            }
            else if (networkManager.IsClient)
            {
                SendNameToServerServerRpc(PlayerName);
            }
        }
        else
        {
            Debug.LogWarning($"{nameof(OnlineManager)} NetworkObject is not spawned yet; name sync will occur after spawn.", this);
        }
    }

    /// <summary>
    /// Stops multiplayer networking and returns to the start screen.
    /// </summary>
    public void MultiplayerUnStart(bool host)
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.Shutdown();
        }
        else
        {
            Debug.LogWarning($"{nameof(OnlineManager)} attempted to shutdown, but NetworkManager.Singleton is null.", this);
        }

        if (_lobbyScene != null)
        {
            _lobbyScene.SetActive(false);
        }

        if (_startScene != null)
        {
            _startScene.SetActive(true);
        }
    }

    #endregion

    #region Network Callbacks

    private void OnClientConnected(ulong clientId)
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("[OnlineManager] OnClientConnected called but NetworkManager.Singleton is null.", this);
            return;
        }

        // For non-host clients, names will be sent via SendNameToServerServerRpc.
        if (IsServer && clientId == networkManager.LocalClientId && !_playerNameMap.ContainsKey(clientId))
        {
            _playerNameMap[clientId] = PlayerName;
            UpdateAllClientsLobbyUI();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        var networkManager = NetworkManager.Singleton;

        if (IsServer && _playerNameMap.ContainsKey(clientId))
        {
            _playerNameMap.Remove(clientId);
            UpdateAllClientsLobbyUI();
        }

        // If the host shuts down, clients are disconnected and should return to the join screen.
        if (!IsServer && networkManager != null && !networkManager.IsConnectedClient)
        {
            MultiplayerUnStart(networkManager.IsHost);
        }
    }

    #endregion

    #region Name Sync RPCs & Lobby UI

    /// <summary>
    /// Called by a client to send its chosen name to the server.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void SendNameToServerServerRpc(string newName, ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = serverRpcParams.Receive.SenderClientId;
        _playerNameMap[senderId] = newName;
        UpdateAllClientsLobbyUI();
    }

    /// <summary>
    /// Builds a flat string of all player names and notifies all clients to update their lobby UI.
    /// </summary>
    private void UpdateAllClientsLobbyUI()
    {
        List<string> names = new List<string>(_playerNameMap.Values);
        string combinedNames = string.Join("|", names);
        UpdateLobbyUIClientRpc(combinedNames);
    }

    [ClientRpc]
    private void UpdateLobbyUIClientRpc(string combinedNames)
    {
        if (_nameTagPrefab == null || _tagHolder == null)
        {
            Debug.LogWarning($"{nameof(OnlineManager)} cannot update lobby UI because nameTagPrefab or tagHolder is missing.", this);
            return;
        }

        string[] allPlayerNames = combinedNames.Split('|');

        // Optional: clear existing tags to prevent duplicates.
        // Uncomment if one tag per player is desired.
        // foreach (Transform child in _tagHolder)
        // {
        //     Destroy(child.gameObject);
        // }

        foreach (string playerName in allPlayerNames)
        {
            if (!string.IsNullOrEmpty(playerName))
            {
                GameObject newTag = Instantiate(_nameTagPrefab, _tagHolder);
                var text = newTag.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    text.text = playerName;
                }
                else
                {
                    Debug.LogWarning("Instantiated nameTagPrefab does not have a TextMeshProUGUI component.", newTag);
                }
            }
        }
    }

    #endregion

    #region Host Game Flow

    /// <summary>
    /// Host-only entry point for starting the game from the lobby UI.
    /// </summary>
    public void HostStartGameButtonPressed()
    {
        if (IsServer)
        {
            StartGameServerRpc();
        }
        else
        {
            Debug.LogWarning("[OnlineManager] Non-host attempted to start game.", this);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartGameServerRpc(ServerRpcParams serverRpcParams = default)
    {
        if (!IsServer)
            return;

        StartGameClientRpc();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        if (_lobbyScene != null)
        {
            _lobbyScene.SetActive(false);
        }

        if (_characterSelectScene != null)
        {
            _characterSelectScene.SetActive(true);
        }
    }

    #endregion
}

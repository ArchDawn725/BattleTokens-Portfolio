using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Manages player readiness states for various gameplay phases (combat start, turn end),
/// and provides optional heartbeat logic for host-disconnect detection.
/// </summary>
public class PlayerReadyManager : NetworkBehaviour
{
    #region Singleton & Events

    public static PlayerReadyManager Instance { get; private set; }

    /// <summary>
    /// Invoked on the server when all players are ready for combat start.
    /// </summary>
    public event Action OnAllPlayersReadyForCombatStart;

    /// <summary>
    /// Invoked on the server when all players are ready for turn end.
    /// </summary>
    public event Action OnAllPlayersReadyForTurnEnd;

    /// <summary>
    /// Invoked on clients when the server wants all timers to start (relayed via ClientRpc).
    /// </summary>
    public event Action OnPlayerReadyUp;

    private bool _turnTimerTriggered;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple {nameof(PlayerReadyManager)} instances detected. Destroying duplicate on '{name}'.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region Readiness Tracking

    // Tracks each player's readiness for combat start and turn end.
    private readonly Dictionary<ulong, bool> _playerReadyForCombatStartStates = new Dictionary<ulong, bool>();
    private readonly Dictionary<ulong, bool> _playerReadyForTurnEndStates = new Dictionary<ulong, bool>();

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        var networkManager = NetworkManager.Singleton;
        if (IsServer && networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        var networkManager = NetworkManager.Singleton;
        if (IsServer && networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        base.OnNetworkDespawn();
    }

    private void OnClientConnected(ulong clientId)
    {
        _playerReadyForCombatStartStates[clientId] = false;
        _playerReadyForTurnEndStates[clientId] = false;
        Debug.Log($"[PlayerReadyManager] OnClientConnected: {clientId}");
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _playerReadyForCombatStartStates.Remove(clientId);
        _playerReadyForTurnEndStates.Remove(clientId);

        if (AllPlayersReadyForCombatStart())
        {
            OnAllPlayersReadyForCombatStart?.Invoke();
        }

        if (AllPlayersReadyForTurnEnd())
        {
            OnAllPlayersReadyForTurnEnd?.Invoke();
        }

        Debug.Log($"[PlayerReadyManager] OnClientDisconnected: {clientId}");
    }

    private void OnDestroy()
    {
        StopHeartbeat();

        var networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.CustomMessagingManager != null)
        {
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler("Heartbeat");
        }
    }

    #endregion

    #region Readiness Server RPCs

    /// <summary>
    /// Called by a client to indicate readiness for combat start.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerReadyForCombatStartServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;

        if (!_playerReadyForCombatStartStates.ContainsKey(senderClientId))
        {
            Debug.Log($"[PlayerReadyManager] Client {senderClientId} not tracked, ignoring readiness.");
            return;
        }

        if (_playerReadyForCombatStartStates[senderClientId])
            return;

        _playerReadyForCombatStartStates[senderClientId] = true;
        Debug.Log($"[PlayerReadyManager] Client {senderClientId} is ready for combat start.");

        if (AllPlayersReadyForCombatStart())
        {
            OnAllPlayersReadyForCombatStart?.Invoke();
        }

        // Reset timer trigger whenever the first ready mark is received for this phase.
        if (_playerReadyForCombatStartStates.Count(kvp => kvp.Value) == 1)
        {
            _turnTimerTriggered = false;
        }
    }

    /// <summary>
    /// Called by a client to indicate readiness for turn end.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerReadyForTurnEndServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong senderClientId = serverRpcParams.Receive.SenderClientId;

        if (!_playerReadyForTurnEndStates.ContainsKey(senderClientId))
        {
            Debug.Log($"[PlayerReadyManager] Client {senderClientId} not tracked for turn end, ignoring.");
            return;
        }

        if (_playerReadyForTurnEndStates[senderClientId])
            return;

        _playerReadyForTurnEndStates[senderClientId] = true;
        Debug.Log($"[PlayerReadyManager] Client {senderClientId} is ready for turn end.");

        if (AllPlayersReadyForTurnEnd())
        {
            OnAllPlayersReadyForTurnEnd?.Invoke();
        }

        if (_playerReadyForTurnEndStates.Count(kvp => kvp.Value) == 1)
        {
            _turnTimerTriggered = false;
        }
    }

    /// <summary>
    /// Called by a client to request starting the shared turn timer.
    /// Only triggers once per phase on the server.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AttemptTimerStartServerRpc()
    {
        Debug.Log($"[PlayerReadyManager] Attempting timer start. Already triggered: {_turnTimerTriggered}");

        if (_turnTimerTriggered)
            return;

        _turnTimerTriggered = true;
        NotifyAllClientsToStartTimerClientRpc();
    }

    [ClientRpc]
    private void NotifyAllClientsToStartTimerClientRpc()
    {
        OnPlayerReadyUp?.Invoke();
    }

    #endregion

    #region Readiness Checks

    /// <summary>
    /// Returns true if every known player is ready for combat start.
    /// </summary>
    private bool AllPlayersReadyForCombatStart()
    {
        if (_playerReadyForCombatStartStates.Count == 0)
        {
            Debug.Log("[PlayerReadyManager] No players are connected. Cannot be all ready for combat start.");
            return false;
        }

        foreach (bool ready in _playerReadyForCombatStartStates.Values)
        {
            if (!ready)
                return false;
        }

        Debug.Log("[PlayerReadyManager] All players ready for combat start!");
        return true;
    }

    /// <summary>
    /// Returns true if every known player is ready to end their turn.
    /// </summary>
    private bool AllPlayersReadyForTurnEnd()
    {
        if (_playerReadyForTurnEndStates.Count == 0)
        {
            Debug.Log("[PlayerReadyManager] No players are connected. Cannot be all ready for turn end.");
            return false;
        }

        foreach (bool ready in _playerReadyForTurnEndStates.Values)
        {
            if (!ready)
                return false;
        }

        Debug.Log("[PlayerReadyManager] All players ready for turn end!");
        return true;
    }

    #endregion

    #region Readiness Resets

    /// <summary>
    /// Resets all players' "ready for combat start" states to false.
    /// </summary>
    private void ResetAllPlayersReadyForCombatStart()
    {
        var keys = new List<ulong>(_playerReadyForCombatStartStates.Keys);
        foreach (ulong key in keys)
        {
            _playerReadyForCombatStartStates[key] = false;
            Debug.Log($"[PlayerReadyManager] Reset combat start readiness for client {key}.");
        }
    }

    /// <summary>
    /// Resets all players' "ready for turn end" states to false.
    /// </summary>
    private void ResetAllPlayersReadyForTurnEnd()
    {
        var keys = new List<ulong>(_playerReadyForTurnEndStates.Keys);
        foreach (ulong key in keys)
        {
            _playerReadyForTurnEndStates[key] = false;
            Debug.Log($"[PlayerReadyManager] Reset turn end readiness for client {key}.");
        }
    }

    /// <summary>
    /// Resets all readiness state for both combat start and turn end.
    /// </summary>
    public void Reset()
    {
        ResetAllPlayersReadyForCombatStart();
        ResetAllPlayersReadyForTurnEnd();
    }

    #endregion

    #region Heartbeat Logic

    private float _lastHeartbeatTime;

    /// <summary>
    /// Initializes heartbeat behavior.
    /// Host: starts sending heartbeats.
    /// Client: registers handler and begins monitoring host connection.
    /// </summary>
    public void StartUp()
    {
        _lastHeartbeatTime = Time.time;

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        if (networkManager.IsHost)
        {
            StartHeartbeat();
        }

        if (networkManager.IsClient && networkManager.CustomMessagingManager != null)
        {
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler("Heartbeat", OnHeartbeatReceived);
            StartCoroutine(MonitorHostConnection());
        }
    }

    /// <summary>
    /// Starts periodic heartbeat sending (host only).
    /// </summary>
    private void StartHeartbeat()
    {
        CancelInvoke(nameof(SendHeartbeat));
        InvokeRepeating(nameof(SendHeartbeat), 0f, 1f);
    }

    /// <summary>
    /// Stops heartbeat sending on this object.
    /// </summary>
    private void StopHeartbeat()
    {
        CancelInvoke(nameof(SendHeartbeat));
    }

    /// <summary>
    /// Sends a "Heartbeat" named message to all connected clients (host only).
    /// </summary>
    private void SendHeartbeat()
    {
        var networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsHost || networkManager.CustomMessagingManager == null)
            return;

        Debug.Log("[PlayerReadyManager] Sending heartbeat to all clients.");

        foreach (var client in networkManager.ConnectedClients)
        {
            Debug.Log($"[PlayerReadyManager] Sending heartbeat to ClientId: {client.Key}");
            using (var writer = new FastBufferWriter(1, Allocator.Temp))
            {
                networkManager.CustomMessagingManager.SendNamedMessage(
                    "Heartbeat",
                    client.Key,
                    writer
                );
            }
        }
    }

    /// <summary>
    /// Called on the client when the "Heartbeat" message is received from the host.
    /// </summary>
    private void OnHeartbeatReceived(ulong senderClientId, FastBufferReader reader)
    {
        Debug.Log($"[PlayerReadyManager] Heartbeat received from host (ClientId: {senderClientId}).");
        _lastHeartbeatTime = Time.time;
    }

    /// <summary>
    /// Client-side coroutine that monitors time since last heartbeat to detect host disconnection.
    /// </summary>
    private IEnumerator MonitorHostConnection()
    {
        while (true)
        {
            // If no heartbeat has been received in the last 30 seconds, assume disconnection.
            if (Time.time - _lastHeartbeatTime > 30f)
            {
                Debug.LogError("[PlayerReadyManager] Host is unresponsive. Assuming disconnection.");
                HandleHostDisconnection();
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    /// <summary>
    /// Handles host-disconnection behavior such as notifying gameplay systems.
    /// </summary>
    private void HandleHostDisconnection()
    {
        Debug.Log("[PlayerReadyManager] Handling host disconnection...");

        var enemyController = EnemyController.Instance;
        if (enemyController != null)
        {
            enemyController.HostLeft();
        }

        // Additional cleanup or UI transitions can be added here.
    }

    #endregion
}

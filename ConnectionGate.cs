using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Central gate for Netcode connection approval.
/// - Enforces max player count
/// - Enforces protocol version
/// - Prevents duplicate player IDs
/// </summary>
[DisallowMultipleComponent]
public class ConnectionGate : MonoBehaviour
{
    #region SINGLETON

    public static ConnectionGate Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[ConnectionGate] Duplicate instance detected. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region CONFIGURATION

    [Header("Connection Settings")]
    [SerializeField, Tooltip("Maximum number of players allowed to connect.")]
    private int maxPlayers = 9;

    [SerializeField, Tooltip("Protocol version required to connect. Bump when you change network protocol.")]
    private int protocolVersion = 1;

    /// <summary>Read-only access to the current protocol version.</summary>
    public int ProtocolVersion => protocolVersion;

    /// <summary>Read-only access to the maximum player capacity.</summary>
    public int MaxPlayers => maxPlayers;

    #endregion

    #region TRACKING

    // Track who connected so duplicates can be blocked and cleaned up on disconnect.
    private readonly Dictionary<ulong, string> clientIdToPlayerId = new();
    private readonly HashSet<string> connectedPlayerIds = new();

    private NetworkManager networkManager;

    #endregion

    #region UNITY_LIFECYCLE

    private void OnEnable()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[ConnectionGate] NetworkManager.Singleton is null in OnEnable.");
            return;
        }

        networkManager.OnClientDisconnectCallback += OnClientDisconnect;
    }

    private void OnDisable()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager != null)
        {
            networkManager.OnClientDisconnectCallback -= OnClientDisconnect;
            networkManager.ConnectionApprovalCallback -= ApprovalCheck;
        }
    }

    #endregion

    #region APPROVAL_LOGIC

    /// <summary>
    /// Enables connection approval and hooks into Netcode's ConnectionApprovalCallback.
    /// Call this before starting host.
    /// </summary>
    public void EnableApproval()
    {
        networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[ConnectionGate] Cannot enable approval: NetworkManager.Singleton is null.");
            return;
        }

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback -= ApprovalCheck; // Avoid double-subscribe
        networkManager.ConnectionApprovalCallback += ApprovalCheck;

        Debug.Log("[ConnectionGate] Connection approval enabled.");
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                               NetworkManager.ConnectionApprovalResponse response)
    {
        // Safe default: deny until all checks pass.
        response.Approved = false;
        response.CreatePlayerObject = true;
        response.Reason = "Unknown error";

        if (networkManager == null)
        {
            Debug.LogError("[ConnectionGate] ApprovalCheck called but NetworkManager is null.");
            response.Reason = "Server misconfigured";
            return;
        }

        // Approve only the host's local client (self-connection)
        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            response.Reason = "Host self";
            return;
        }

        // Capacity check
        if (networkManager.ConnectedClientsIds.Count >= maxPlayers)
        {
            response.Reason = "Lobby full";
            Debug.LogWarning($"[ConnectionGate] Rejecting client {request.ClientNetworkId}: lobby full.");
            return;
        }

        // Parse payload
        if (!PayloadUtil.TryParse(request.Payload, out var payload))
        {
            response.Reason = "Bad payload";
            Debug.LogWarning($"[ConnectionGate] Rejecting client {request.ClientNetworkId}: bad payload.");
            return;
        }

        // Version gate
        if (payload.protocolVersion != protocolVersion)
        {
            response.Reason = "Version mismatch";
            Debug.LogWarning($"[ConnectionGate] Rejecting client {request.ClientNetworkId}: version mismatch " +
                             $"(client {payload.protocolVersion}, server {protocolVersion}).");
            return;
        }

        // Require stable ID
        if (string.IsNullOrWhiteSpace(payload.playerId))
        {
            response.Reason = "Missing player id";
            Debug.LogWarning($"[ConnectionGate] Rejecting client {request.ClientNetworkId}: missing playerId.");
            return;
        }

        // Duplicate player check
        if (connectedPlayerIds.Contains(payload.playerId))
        {
            response.Reason = "Already connected";
            Debug.LogWarning($"[ConnectionGate] Rejecting client {request.ClientNetworkId}: duplicate playerId '{payload.playerId}'.");
            return;
        }

        // All checks passed
        response.Approved = true;
        response.Reason = "OK";

        connectedPlayerIds.Add(payload.playerId);
        clientIdToPlayerId[request.ClientNetworkId] = payload.playerId;

        Debug.Log($"[ConnectionGate] Approved client {request.ClientNetworkId} (playerId='{payload.playerId}').");
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (clientIdToPlayerId.TryGetValue(clientId, out var playerId))
        {
            clientIdToPlayerId.Remove(clientId);
            connectedPlayerIds.Remove(playerId);
            Debug.Log($"[ConnectionGate] Client {clientId} disconnected. Removed playerId '{playerId}' from tracking.");
        }
    }

    #endregion
}

[Serializable]
public struct ConnectionPayload
{
    public string playerId;       // stable ID (e.g., authentication ID / external platform ID)
    public string displayName;    // optional (can be Unicode)
    public int protocolVersion;   // bump this when network protocol changes
}

public static class PayloadUtil
{
    /// <summary>
    /// Builds a UTF-8 JSON payload for connection approval.
    /// </summary>
    public static byte[] Build(string playerId, string displayName, int version)
    {
        var payload = new ConnectionPayload
        {
            playerId = playerId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            protocolVersion = version
        };

        return Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
    }

    /// <summary>
    /// Attempts to parse a UTF-8 JSON payload into a <see cref="ConnectionPayload"/>.
    /// </summary>
    public static bool TryParse(byte[] bytes, out ConnectionPayload payload)
    {
        payload = default;

        if (bytes == null || bytes.Length == 0)
            return false;

        try
        {
            var json = Encoding.UTF8.GetString(bytes);
            payload = JsonUtility.FromJson<ConnectionPayload>(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

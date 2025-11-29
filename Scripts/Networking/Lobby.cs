using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

/// <summary>
/// Minimal lobby controller using Unity Lobby & Authentication services.
/// Creates a lobby, keeps it alive via heartbeat, and can list/join/leave lobbies.
/// </summary>
public class LobbyCon : MonoBehaviour
{
    #region Fields

    [Header("Lobby Settings")]
    [Tooltip("Name used when creating a new lobby.")]
    [SerializeField] private string lobbyName = "MyLobby";

    [Tooltip("Maximum number of players allowed in the lobby.")]
    [SerializeField] private int maxPlayers = 9;

    [Tooltip("Display name stored on the player when joining/creating a lobby.")]
    [SerializeField] private string playerName = "Player";

    [Header("Heartbeat & Polling")]
    [Tooltip("Seconds between heartbeat pings sent by the host to keep the lobby alive.")]
    [SerializeField] private float heartBeatIntervalSeconds = 15f;

    [Tooltip("Seconds between lobby state refreshes while hosting.")]
    [SerializeField] private float lobbyUpdateIntervalSeconds = 1.1f;

    private Lobby _hostLobby;
    private float _heartBeatTimer;
    private float _updateLobbyTimer;

    #endregion

    #region Service Shortcuts

    private IAuthenticationService AuthService => AuthenticationService.Instance;
    private ILobbyService LobbyService => Unity.Services.Lobbies.LobbyService.Instance;

    #endregion

    #region Unity Lifecycle

    private async void Start()
    {
        try
        {
            await UnityServices.InitializeAsync();

            AuthService.SignedIn += () =>
            {
                Debug.Log($"[LobbyCon] Signed in as: {AuthService.PlayerId}");
            };

            await AuthService.SignInAnonymouslyAsync();

            await CreateLobby();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyCon] Failed to initialize or sign in: {e}");
        }
    }

    private void Update()
    {
        HandleHeartBeat();
        HandleLobbyUpdates();
    }

    #endregion

    #region Heartbeat & Lobby Polling

    private async void HandleHeartBeat()
    {
        if (_hostLobby == null)
            return;

        _heartBeatTimer -= Time.deltaTime;
        if (_heartBeatTimer <= 0f)
        {
            _heartBeatTimer = heartBeatIntervalSeconds;

            try
            {
                await LobbyService.SendHeartbeatPingAsync(_hostLobby.Id);
                Debug.Log("[LobbyCon] Sent heartbeat ping.");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbyCon] Heartbeat failed: {e}");
            }
        }
    }

    private async void HandleLobbyUpdates()
    {
        if (_hostLobby == null)
            return;

        _updateLobbyTimer -= Time.deltaTime;
        if (_updateLobbyTimer <= 0f)
        {
            _updateLobbyTimer = lobbyUpdateIntervalSeconds;

            try
            {
                _hostLobby = await LobbyService.GetLobbyAsync(_hostLobby.Id);
                Debug.Log("[LobbyCon] Lobby state updated.");
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbyCon] Failed to update lobby: {e}");
            }
        }
    }

    #endregion

    #region Lobby Management

    private async System.Threading.Tasks.Task CreateLobby()
    {
        try
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = true,
                Player = GetPlayer(),
            };

            var lobby = await LobbyService.CreateLobbyAsync(lobbyName, maxPlayers, options);
            _hostLobby = lobby;

            PrintPlayers(lobby);
            Debug.Log($"[LobbyCon] Created Lobby: {lobby.Name} (Max {lobby.MaxPlayers})");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyCon] CreateLobby failed: {e}");
        }
    }

    private async void ListLobbies()
    {
        try
        {
            var queryLobbiesOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse queryResponse = await Lobbies.Instance.QueryLobbiesAsync(queryLobbiesOptions);

            Debug.Log($"[LobbyCon] Lobbies Found: {queryResponse.Results.Count}");
            foreach (Lobby lobby in queryResponse.Results)
            {
                Debug.Log($"[LobbyCon] Lobby: {lobby.Name} (Max {lobby.MaxPlayers})");
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyCon] ListLobbies failed: {e}");
        }
    }

    private async void JoinLobby(string code)
    {
        try
        {
            var options = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer(),
            };

            Lobby lobby = await Lobbies.Instance.JoinLobbyByCodeAsync(code, options);
            Debug.Log($"[LobbyCon] Joined lobby by code '{code}': {lobby.Name}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyCon] JoinLobby failed: {e}");
        }
    }

    private async void QuickJoinLobby()
    {
        try
        {
            Lobby lobby = await LobbyService.QuickJoinLobbyAsync();
            Debug.Log($"[LobbyCon] Quick joined lobby: {lobby.Name}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyCon] QuickJoinLobby failed: {e}");
        }
    }

    private async void LeaveLobby()
    {
        if (_hostLobby == null)
        {
            Debug.LogWarning("[LobbyCon] LeaveLobby called, but no host lobby is set.");
            return;
        }

        try
        {
            await LobbyService.RemovePlayerAsync(_hostLobby.Id, AuthService.PlayerId);
            Debug.Log("[LobbyCon] Left lobby.");
            _hostLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyCon] LeaveLobby failed: {e}");
        }
    }

    #endregion

    #region Helpers

    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
            }
        };
    }

    private void PrintPlayers(Lobby lobby)
    {
        if (lobby == null || lobby.Players == null)
        {
            Debug.LogWarning("[LobbyCon] PrintPlayers called with null lobby or lobby.Players.");
            return;
        }

        foreach (Player player in lobby.Players)
        {
            if (player.Data != null && player.Data.TryGetValue("PlayerName", out var nameData))
            {
                Debug.Log($"[LobbyCon] Player {player.Id} - Name: {nameData.Value}");
            }
            else
            {
                Debug.Log($"[LobbyCon] Player {player.Id} - No PlayerName set.");
            }
        }
    }

    #endregion
}

using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

/// <summary>
/// Throttles writes to the Unity Lobby service to avoid hitting rate limits.
/// Queues changes (ready state, level, character, AI config, kicks) and
/// pushes them at a fixed interval.
/// </summary>
public class LobbyUpdateThrottler : MonoBehaviour
{
    #region SINGLETON

    public static LobbyUpdateThrottler Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LobbyUpdateThrottler] Duplicate instance detected; destroying this one.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region FLAGS_AND_PENDING_DATA

    [Header("Flags")]
    private bool readyStatusChanged;
    private bool playerLevelChanged;
    private bool characterChanged;
    private bool aiCharacterChanged;
    private bool kickingPlayer;

    [Header("Pending Values")]
    private bool pendingReadyStatus;
    private int pendingPlayerLevel;
    private int pendingPlayerKick;

    #endregion

    #region THROTTLE_CONFIG

    [Header("Throttle Settings")]
    [SerializeField]
    private float intervalSeconds = 5f;    // UGS write limit suggests >= 5 seconds

    private float timer;

    private LobbyManager lobby => LobbyManager.Instance;
    private SpawnManager spawn => SpawnManager.Instance;

    #endregion

    #region PUBLIC_API

    public void UpdatePlayerReadyStatus(bool isReady)
    {
        if (lobby == null)
        {
            QueueReadyStatus(isReady);
            return;
        }

        try
        {
            lobby.UpdatePlayerReadyStatus(isReady);
        }
        catch
        {
            QueueReadyStatus(isReady);
        }
    }

    public void UpdatePlayerLevel(int level)
    {
        if (lobby == null)
        {
            QueuePlayerLevel(level);
            return;
        }

        try
        {
            lobby.UpdatePlayerLevel(level);
        }
        catch
        {
            QueuePlayerLevel(level);
        }
    }

    public void UpdatePlayerCharacter()
    {
        if (lobby == null)
        {
            characterChanged = true;
            return;
        }

        try
        {
            lobby.UpdatePlayerCharacterToLobby();
        }
        catch
        {
            characterChanged = true;
        }
    }

    public async Task KickPlayer(int value)
    {
        pendingPlayerKick = value;

        if (lobby == null)
        {
            kickingPlayer = true;
            return;
        }

        try
        {
            await lobby.KickPlayerByIndexAsync(pendingPlayerKick);
        }
        catch
        {
            kickingPlayer = true;
        }
    }

    public void UpdateAICharacter()
    {
        aiCharacterChanged = true;
    }

    #endregion

    #region UNITY_UPDATE_LOOP

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < intervalSeconds) return;

        timer = 0f;
        _ = SendPendingUpdates(); // fire-and-forget
    }

    #endregion

    #region INTERNAL_QUEUE_HELPERS

    private void QueueReadyStatus(bool isReady)
    {
        readyStatusChanged = true;
        pendingReadyStatus = isReady;
    }

    private void QueuePlayerLevel(int level)
    {
        playerLevelChanged = true;
        pendingPlayerLevel = level;
    }

    #endregion

    #region PUSH_TO_LOBBY

    /// <summary>
    /// Pushes any pending updates to the lobby. Called at intervals from Update().
    /// </summary>
    private async Task SendPendingUpdates()
    {
        if (lobby == null)
        {
            Debug.LogWarning("[LobbyUpdateThrottler] LobbyManager instance not available; postponing updates.");
            return;
        }

        if (readyStatusChanged)
        {
            try
            {
                lobby.UpdatePlayerReadyStatus(pendingReadyStatus);
                readyStatusChanged = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyUpdateThrottler] Failed to update ready status: {e.Message}");
                readyStatusChanged = true;
            }
        }

        if (playerLevelChanged)
        {
            try
            {
                lobby.UpdatePlayerLevel(pendingPlayerLevel);
                playerLevelChanged = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyUpdateThrottler] Failed to update player level: {e.Message}");
                playerLevelChanged = true;
            }
        }

        if (characterChanged)
        {
            try
            {
                lobby.UpdatePlayerCharacterToLobby();
                characterChanged = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyUpdateThrottler] Failed to update character: {e.Message}");
                characterChanged = true;
            }
        }

        if (aiCharacterChanged)
        {
            try
            {
                await PushAiClassesAsync();
                aiCharacterChanged = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyUpdateThrottler] Failed to update AI character: {e.Message}");
                aiCharacterChanged = true;
            }
        }

        if (kickingPlayer)
        {
            try
            {
                await lobby.KickPlayerByIndexAsync(pendingPlayerKick);
                kickingPlayer = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LobbyUpdateThrottler] Failed to kick player: {e.Message}");
                kickingPlayer = true;
            }
        }
    }

    /// <summary>
    /// Host-side push of AI class configuration into lobby data.
    /// </summary>
    public async Task PushAiClassesAsync()
    {
        if (spawn == null)
        {
            Debug.LogWarning("[LobbyUpdateThrottler] SpawnManager instance not available; cannot push AI classes.");
            return;
        }

        if (lobby == null || lobby.joinedLobby == null)
        {
            Debug.LogWarning("[LobbyUpdateThrottler] No joined lobby found; cannot push AI classes.");
            return;
        }

        int[] aiClasses = spawn.aiClasses;
        var data = new Dictionary<string, DataObject>();

        for (int i = 0; i < aiClasses.Length; i++)
        {
            data[$"AI_{i}"] = new DataObject(
                DataObject.VisibilityOptions.Public,
                aiClasses[i].ToString());
        }

        await Lobbies.Instance.UpdateLobbyAsync(
            lobby.joinedLobby.Id,
            new UpdateLobbyOptions { Data = data });
    }

    #endregion
}

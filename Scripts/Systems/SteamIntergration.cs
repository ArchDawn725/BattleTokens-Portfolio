#if UNITY_ANDROID || UNITY_IOS
#else
using Steamworks;
#endif
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine;

/// <summary>
/// Handles Steam and UGS initialization, connection flags, and Steam rich presence
/// join requests for lobby auto-join.
/// </summary>
public class SteamIntergration : MonoBehaviour
{
    #region Singleton & State

    public static SteamIntergration Instance { get; private set; }

    [Header("Connection State")]
    [Tooltip("True if Steam initialized successfully on this platform.")]
    public bool SteamConnected;

    [Tooltip("True if Unity Gaming Services initialized successfully.")]
    public bool GooglePlayConnected;

    [Tooltip("True if the player is currently in a Steam lobby (enables rich presence join polling).")]
    public bool inLobby;

    [Tooltip("Last processed Steam rich presence 'connect' value to avoid duplicate joins.")]
    [SerializeField] private string lastConnectValue = "";

    // Cached references
    private LobbyManager lobbyManager;

    #endregion

    #region Unity Lifecycle & Initialization

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple {nameof(SteamIntergration)} instances detected. Destroying duplicate on '{name}'.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // --- Steam initialization ---
        try
        {
#if UNITY_ANDROID || UNITY_IOS
            // Steam is not used on mobile builds.
#else
            SteamClient.Init(3585790);
            Debug.Log($"[Steam] Logged in as: {SteamClient.Name}");
            SteamConnected = true;

            // Delay language setup slightly so SteamApps.GameLanguage is available.
            Invoke(nameof(SteamSetUp), 0.25f);
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SteamIntergration] Steam initialization failed: {e}");
        }

        // --- Unity Gaming Services initialization ---
        try
        {
            var options = new InitializationOptions()
                .SetEnvironmentName("production"); // Match the UGS environment configuration.

            await UnityServices.InitializeAsync(options);
            Debug.Log("[SteamIntergration] Unity Gaming Services initialized.");
            GooglePlayConnected = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SteamIntergration] UGS initialization failed: {e}");
        }

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    private void Start()
    {
        lobbyManager = LobbyManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogWarning("[SteamIntergration] LobbyManager.Instance is null at Start. Steam join-by-code may fail.");
        }

        if (SteamConnected)
        {
            CheckForSteamConnectParameter();

            // Optional: hook runtime join event if desired and supported, in which it is not yet.
            try
            {
                //SteamClient.OnGameRichPresenceJoinRequested += OnSteamRichPresenceJoinRequested;
            }
            catch
            {
                Debug.LogWarning("[SteamIntergration] OnGameRichPresenceJoinRequested not available. Falling back to polling.");
            }
        }

#if !UNITY_EDITOR
        Debug.unityLogger.logEnabled = false;

        Application.SetStackTraceLogType(LogType.Log,     StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        Application.SetStackTraceLogType(LogType.Error,   StackTraceLogType.ScriptOnly);
#endif
    }

    private void Update()
    {
#if UNITY_ANDROID || UNITY_IOS
        // No Steam support on mobile.
#else
        if (!SteamConnected)
            return;

        SteamClient.RunCallbacks();

        if (!inLobby || lobbyManager == null)
            return;

        string connectValue = SteamFriends.GetRichPresence("connect");

        if (!string.IsNullOrEmpty(connectValue) && connectValue != lastConnectValue)
        {
            lastConnectValue = connectValue.Trim();

            Debug.Log($"[Steam] Detected new connect request at runtime: {lastConnectValue}");
            lobbyManager.JoinLobbyByCode(lastConnectValue);
        }
#endif
    }

    private void OnApplicationQuit()
    {
        Screen.sleepTimeout = SleepTimeout.SystemSetting;

#if UNITY_ANDROID || UNITY_IOS
        // Nothing to shut down on mobile.
#else
        if (SteamConnected)
        {
            SteamClient.Shutdown();
        }
#endif
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Steam Connect & Callbacks

    /// <summary>
    /// Checks command line arguments for a "+connect" parameter and attempts to join
    /// the specified lobby code on startup.
    /// </summary>
    private void CheckForSteamConnectParameter()
    {
        if (lobbyManager == null)
        {
            Debug.LogWarning("[SteamIntergration] Cannot process +connect parameter: LobbyManager.Instance is null.");
            return;
        }

        string[] args = System.Environment.GetCommandLineArgs();

        foreach (string arg in args)
        {
            if (!arg.StartsWith("+connect"))
                continue;

            // "+connect 12345" -> strip "+connect" portion and trim any whitespace.
            string lobbyCode = arg.Length > 8 ? arg.Substring(8).Trim() : string.Empty;

            if (string.IsNullOrEmpty(lobbyCode))
            {
                Debug.LogWarning("[SteamIntergration] +connect parameter found, but lobby code is empty.");
                continue;
            }

            Debug.Log("[Steam] Launching with Steam connect code: " + lobbyCode);
            lobbyManager.JoinLobbyByCode(lobbyCode);
        }
    }

#if UNITY_ANDROID || UNITY_IOS
    // No Steam callbacks on mobile.
#else
    private void OnSteamRichPresenceJoinRequested(Friend friend, string connect)
    {
        Debug.Log($"[Steam] Join requested at runtime from {friend.Name}: {connect}");

        if (string.IsNullOrEmpty(connect))
            return;

        if (lobbyManager == null)
        {
            Debug.LogError("[SteamIntergration] LobbyManager.Instance is null. Cannot join lobby from rich presence.");
            return;
        }

        lobbyManager.JoinLobbyByCode(connect);
    }

    private void AchievementChanged(Steamworks.Data.Achievement ach, int currentProgress, int progress)
    {
        if (ach.State)
        {
            Debug.Log($"[Steam] Achievement unlocked: {ach.Name}");
        }
    }
#endif

    #endregion

    #region Steam Setup Helpers

    private void SteamSetUp()
    {
#if UNITY_ANDROID || UNITY_IOS
        // No Steam language setup on mobile.
#else
        if (LangCon.Singleton == null)
        {
            Debug.LogWarning("[SteamIntergration] LangCon.Singleton is null. Skipping Steam language setup.");
            return;
        }

        LangCon.Singleton.SteamLangSetUp(SteamApps.GameLanguage);
#endif
    }

    #endregion
}

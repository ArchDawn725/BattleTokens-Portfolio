using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global audio manager for music, UI sounds, and SFX volume.
/// Handles music cross-fades and reacts to lobby joins/leaves for track changes.
/// </summary>
public class SoundController : MonoBehaviour
{
    #region Singleton & Fields

    public static SoundController Instance { get; private set; }

    [Header("Music Sources")]
    [SerializeField] private AudioSource newSceneAudio;
    [SerializeField] private AudioSource[] musicTracks;

    [Header("Volumes")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1f;

    [Range(0f, 1f)]
    public float soundEffectVolume = 1f;

    [Header("Sound Effects Registry")]
    [Tooltip("All AudioSources that should respond to global SFX volume changes.")]
    public List<AudioSource> SoundEffects = new List<AudioSource>();

    [Header("UI Clips")]
    [Tooltip("UI hover sound effect clip.")]
    public AudioClip uIhover;

    [Tooltip("UI press/click sound effect clip.")]
    public AudioClip uIPress;

    [Tooltip("Clip played when a new scene loads.")]
    public AudioClip newScene;

    [Header("Transition Settings")]
    [Tooltip("Crossfade duration in seconds when switching music tracks.")]
    [SerializeField] private float transitionTime = 1f;

    private int currentTrack = -1;

    // Cached singletons
    private LobbyManager lobbyManager;
    private SteamIntergration steamIntegration;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(SoundController)}] Duplicate instance detected on '{name}'. Destroying.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure list is valid even if not set in inspector
        SoundEffects ??= new List<AudioSource>();

        // Cache external singletons
        lobbyManager = LobbyManager.Instance;
        steamIntegration = SteamIntergration.Instance;

        // Register new-scene SFX into the SFX list
        if (newSceneAudio != null && !SoundEffects.Contains(newSceneAudio))
        {
            SoundEffects.Add(newSceneAudio);
        }

        SetUp();
    }

    private void OnEnable()
    {
        if (newSceneAudio != null && !SoundEffects.Contains(newSceneAudio))
        {
            SoundEffects.Add(newSceneAudio);
        }
    }

    private void OnDisable()
    {
        if (SoundEffects != null && newSceneAudio != null)
        {
            SoundEffects.Remove(newSceneAudio);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (SoundEffects != null && newSceneAudio != null)
        {
            SoundEffects.Remove(newSceneAudio);
        }

        if (lobbyManager != null)
        {
            lobbyManager.OnLeftLobby -= LobbyManager_OnLeftLobby;
            lobbyManager.OnJoinedLobby -= LobbyManager_OnJoinedLobby;
        }
    }

    #endregion

    #region Initialization & Volume

    /// <summary>
    /// Subscribes to lobby events. Call once after LobbyManager is ready.
    /// </summary>
    public void StartUp()
    {
        lobbyManager ??= LobbyManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogWarning($"[{nameof(SoundController)}] LobbyManager.Instance is null in StartUp(). Lobby music transitions will be disabled.");
            return;
        }

        lobbyManager.OnLeftLobby -= LobbyManager_OnLeftLobby;
        lobbyManager.OnJoinedLobby -= LobbyManager_OnJoinedLobby;

        lobbyManager.OnLeftLobby += LobbyManager_OnLeftLobby;
        lobbyManager.OnJoinedLobby += LobbyManager_OnJoinedLobby;
    }

    /// <summary>
    /// Loads saved volume settings and applies them to the music tracks and SFX.
    /// </summary>
    private void SetUp()
    {
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", musicVolume);
        soundEffectVolume = PlayerPrefs.GetFloat("SoundEffectVolume", soundEffectVolume);

        // Apply music volume to all tracks
        if (musicTracks != null)
        {
            for (int i = 0; i < musicTracks.Length; i++)
            {
                if (musicTracks[i] != null)
                {
                    musicTracks[i].volume = musicVolume;
                }
            }
        }

        if (newSceneAudio != null)
        {
            newSceneAudio.volume = soundEffectVolume;
        }
        else
        {
            Debug.LogWarning($"[{nameof(SoundController)}] newSceneAudio is not assigned.");
        }

        // Start with a default track if available
        MusicTransition(5);
    }

    public void ChangeMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);

        if (musicTracks != null)
        {
            for (int i = 0; i < musicTracks.Length; i++)
            {
                if (musicTracks[i] != null)
                {
                    musicTracks[i].volume = musicVolume;
                }
            }
        }

        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
    }

    public void ChangeSoundEffectVolume(float volume)
    {
        soundEffectVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("SoundEffectVolume", soundEffectVolume);

        if (SoundEffects == null)
        {
            return;
        }

        foreach (AudioSource source in SoundEffects)
        {
            if (source != null)
            {
                source.volume = soundEffectVolume;
            }
        }
    }

    /// <summary>
    /// Plays the "new scene" sound effect.
    /// </summary>
    public void NewScene()
    {
        if (newSceneAudio == null)
        {
            Debug.LogWarning($"[{nameof(SoundController)}] NewScene called but newSceneAudio is not assigned.");
            return;
        }

        newSceneAudio.Play();
    }

    #endregion

    #region Music Control & Crossfade

    /// <summary>
    /// Transitions from the current track to a new track with cross-fade.
    /// </summary>
    /// <param name="newTrack">Index of the new track in musicTracks.</param>
    public void MusicTransition(int newTrack)
    {
        if (currentTrack == newTrack)
        {
            return;
        }

        AudioSource from = GetSource(currentTrack);
        AudioSource to = GetSource(newTrack);

        StartCoroutine(MixSources(from, to));
        currentTrack = newTrack;
    }

    private AudioSource GetSource(int trackIndex)
    {
        if (musicTracks == null || musicTracks.Length == 0)
        {
            return null;
        }

        if (trackIndex < 0 || trackIndex >= musicTracks.Length)
        {
            return null;
        }

        return musicTracks[trackIndex];
    }

    private IEnumerator MixSources(AudioSource currentPlaying, AudioSource newPlaying)
    {
        if (newPlaying == null)
        {
            yield break; // nothing to fade in
        }

        float t = 0f;
        float invTransition = transitionTime > 0f ? 1f / transitionTime : 1f;

        // Prepare new source
        newPlaying.volume = 0f;
        newPlaying.Play();

        // If there is no current track, just fade in the new one
        if (currentPlaying == null)
        {
            while (t < transitionTime)
            {
                t += Time.deltaTime;
                newPlaying.volume = Mathf.Lerp(0f, musicVolume, t * invTransition);
                yield return null;
            }

            newPlaying.volume = musicVolume;
            yield break;
        }

        // Cross-fade both sources
        while (t < transitionTime)
        {
            t += Time.deltaTime;
            float percent = t * invTransition; // 0 … 1

            currentPlaying.volume = Mathf.Lerp(musicVolume, 0f, percent);
            newPlaying.volume = Mathf.Lerp(0f, musicVolume, percent);

            yield return null;
        }

        currentPlaying.volume = 0f;
        currentPlaying.Pause();
        newPlaying.volume = musicVolume;
    }

    #endregion

    #region Lobby Callbacks

    private void LobbyManager_OnLeftLobby(object sender, EventArgs e)
    {
        Debug.Log("[SoundController] Left Lobby, returning to main menu.");

        steamIntegration ??= SteamIntergration.Instance;
        if (steamIntegration != null)
        {
            steamIntegration.inLobby = false;
        }
        else
        {
            Debug.LogWarning("[SoundController] SteamIntergration.Instance is null during LobbyManager_OnLeftLobby.");
        }

        MusicTransition(5);
    }

    private void LobbyManager_OnJoinedLobby(object sender, LobbyManager.LobbyEventArgs e)
    {
        Debug.Log("[SoundController] Joined Lobby, transitioning to lobby scene.");

        steamIntegration ??= SteamIntergration.Instance;
        if (steamIntegration != null)
        {
            steamIntegration.inLobby = true;
        }
        else
        {
            Debug.LogWarning("[SoundController] SteamIntergration.Instance is null during LobbyManager_OnJoinedLobby.");
        }

        MusicTransition(4);
    }

    #endregion
}

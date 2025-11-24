using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents a single lobby entry in the UI and handles joining that lobby when clicked.
/// </summary>
public class AvailableLobby : MonoBehaviour
{
    #region UI & Data

    [Header("UI References")]
    [Tooltip("Text element displaying the lobby's name.")]
    [SerializeField] private TextMeshProUGUI lobbyNameText;

    [Tooltip("Text element displaying current/maximum players in the lobby.")]
    [SerializeField] private TextMeshProUGUI playersText;

    /// <summary>
    /// The lobby data backing this UI entry.
    /// </summary>
    private Lobby _lobby;

    #endregion

    #region Unity Lifecycle & Handlers

    private void Awake()
    {
        var button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnLobbySelected);
        }
        else
        {
            Debug.LogWarning($"{nameof(AvailableLobby)} on '{name}' is missing a Button component.");
        }
    }

    /// <summary>
    /// Populates this UI entry with data from a lobby result.
    /// </summary>
    /// <param name="lobby">The lobby to display and attempt to join.</param>
    public void UpdateLobby(Lobby lobby)
    {
        _lobby = lobby;

        if (lobbyNameText != null)
        {
            lobbyNameText.text = lobby.Name;
        }

        if (playersText != null)
        {
            playersText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
        }
    }

    /// <summary>
    /// Called when this lobby entry's button is clicked.
    /// Attempts to join the associated lobby via the LobbyManager.
    /// </summary>
    private void OnLobbySelected()
    {
        if (_lobby == null)
        {
            Debug.LogWarning($"[{nameof(AvailableLobby)}] No lobby assigned for '{name}'. Unable to join.");
            return;
        }

        var lobbyManager = LobbyManager.Instance;
        if (lobbyManager == null)
        {
            Debug.LogError($"[{nameof(AvailableLobby)}] LobbyManager.Instance is null. Cannot join lobby '{_lobby.Name}'.");
            return;
        }

        lobbyManager.JoinLobby(_lobby);
    }

    #endregion
}

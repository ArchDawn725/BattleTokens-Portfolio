using Unity.Collections;
using Unity.Netcode;

/// <summary>
/// Networked component that synchronizes a player's display name
/// across all clients using a NetworkVariable.
/// </summary>
public class PlayerNameScript : NetworkBehaviour
{
    #region Fields

    /// <summary>
    /// Networked player name visible to all clients.
    /// Uses FixedString64Bytes for Netcode compatibility.
    /// </summary>
    public readonly NetworkVariable<FixedString64Bytes> PlayerName =
        new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

    /// <summary>
    /// Prefix used when generating a default player name on spawn.
    /// Result is typically "Player{OwnerClientId}".
    /// </summary>
    private const string DefaultNamePrefix = "Player";

    #endregion

    #region NetworkBehaviour Overrides

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // Owner sets the initial name; value is replicated to all clients.
            PlayerName.Value = $"{DefaultNamePrefix}{OwnerClientId}";
        }
    }

    #endregion
}

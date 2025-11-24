using Unity.Netcode;

/// <summary>
/// Packed data used to initialize and synchronize a character over the network.
/// </summary>
[System.Serializable]
public struct CharacterVariables : INetworkSerializable
{
    // NOTE: These are kept as public fields (not properties) so they can be used with ref in NetworkSerialize.

    /// <summary>Owning client ID for this character.</summary>
    public ulong LocalClientId;

    /// <summary>Display name of the character.</summary>
    public string Name;

    /// <summary>Initial and/or current health value.</summary>
    public int Health;

    /// <summary>Base defence value.</summary>
    public int Defence;

    /// <summary>Index into a sprite/portrait collection.</summary>
    public int ImageIndex;

    /// <summary>Base damage bonus of this character.</summary>
    public int Damage;

    /// <summary>Special class trait or passive.</summary>
    public ClassSpecial ClassSpecial;

    /// <summary>Base action points granted at start.</summary>
    public int ActionPoints;

    /// <summary>Critical hit chance (percentage or scaled value, depending on your system).</summary>
    public int CritChance;

    /// <summary>Critical damage multiplier.</summary>
    public float CritMultiplier;

    /// <summary>IDs of actions available to this character.</summary>
    public int[] ActionIds;

    /// <summary>Grid/location identifier for this character (e.g. tile name).</summary>
    public string Location;

    /// <summary>
    /// Custom serialization for Netcode for GameObjects.
    /// IMPORTANT: The order here must match the field declarations.
    /// </summary>
    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref LocalClientId);

        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Health);
        serializer.SerializeValue(ref Defence);
        serializer.SerializeValue(ref ImageIndex);
        serializer.SerializeValue(ref Damage);
        serializer.SerializeValue(ref ClassSpecial);
        serializer.SerializeValue(ref ActionPoints);
        serializer.SerializeValue(ref CritChance);
        serializer.SerializeValue(ref CritMultiplier);
        serializer.SerializeValue(ref ActionIds);
        serializer.SerializeValue(ref Location);
    }
}

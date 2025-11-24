using Unity.Netcode;

[System.Serializable]
public struct AttackVariables : INetworkSerializable
{
    // Who is performing the attack (e.g., grid cell id or character id)
    public string AttackerId;

    // Primary (or initial) target identifier
    public string TargetId;

    // Damage roll range
    public int MinDamage;
    public int MaxDamage;

    // Final resolved damage after crits, buffs, etc.
    public int ResolvedDamage;

    // Gameplay effect (damage, heal, poison, etc.)
    public ActionEffect Effect;

    // Visual effect used for impact (sword slash, arrow, fire, etc.)
    public EffectVisual ImpactVisual;

    // Critical hit configuration
    public float CritChance;
    public float CritMultiplier;

    // How this action selects targets (single, all, random, etc.)
    public ActionTarget TargetingMode;

    // Action point cost to perform this attack
    public int ActionPointCost;

    // Context flags
    public bool IsPlayerAction;
    public bool IsAllyAiAction;

    // Action metadata
    public string ActionName;
    public int ActionId;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref AttackerId);
        serializer.SerializeValue(ref TargetId);
        serializer.SerializeValue(ref MinDamage);
        serializer.SerializeValue(ref MaxDamage);
        serializer.SerializeValue(ref ResolvedDamage);
        serializer.SerializeValue(ref Effect);
        serializer.SerializeValue(ref ImpactVisual);
        serializer.SerializeValue(ref CritChance);
        serializer.SerializeValue(ref CritMultiplier);
        serializer.SerializeValue(ref TargetingMode);
        serializer.SerializeValue(ref ActionPointCost);

        serializer.SerializeValue(ref IsPlayerAction);
        serializer.SerializeValue(ref IsAllyAiAction);
        serializer.SerializeValue(ref ActionName);
        serializer.SerializeValue(ref ActionId);
    }
}

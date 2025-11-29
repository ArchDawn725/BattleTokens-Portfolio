using System;
using UnityEngine;

/// <summary>
/// Singleton that stores and manages the player's core stats (health, damage, defense, etc.).
/// Other systems use this to track and modify the player's state.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    #region Singleton

    public static PlayerStats Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple {nameof(PlayerStats)} instances detected. Destroying duplicate on '{name}'.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Configurable Fields

    [Header("Class Info")]
    [Tooltip("Enum specifying which class the player has chosen (e.g., Warrior, Archer, etc.).")]
    public PlayerClasses playerClass;

    [Tooltip("ScriptableObject containing the stat configuration for the chosen class.")]
    public ClassSO choosenClass;

    [Header("Base Stats")]
    [Tooltip("Baseline defense for this player.")]
    public int Defence;

    [Tooltip("Baseline damage value for this player.")]
    public int Damage;

    [Tooltip("Minimum damage that this player can deal.")]
    public int MinDamage;

    [Tooltip("Critical hit chance (percentage).")]
    public int CritChance;

    [Tooltip("Multiplier applied when landing a critical hit.")]
    public float CritMulti;

    [Tooltip("Upgrade points that can be spent on improving stats or abilities.")]
    public int UpgradePoints;

    [Header("Extended Stats")]
    [Tooltip("Maximum health the player can have.")]
    public int MaxHealth;

    [Tooltip("Maximum number of action points the player can have.")]
    public int MaxActionPoints;

    [Tooltip("The starting health that will be used to initialize MaxHealth.")]
    public int ClassHealth;

    [Header("Player Identity & References")]
    [Tooltip("Player's display name.")]
    public string playerName;

    [Tooltip("Reference to the Character component representing this player in the game world.")]
    public Character myCharacter;

    [Tooltip("Reference to the chosen item that the player starts with.")]
    public ItemSO Item;

    [Header("Unlocks & Settings")]
    [Tooltip("True if this class's special ability has been unlocked.")]
    public bool classSpecialUnlocked;

    [Tooltip("If true, automatically end the player's turn when out of action points.")]
    public bool autoEndTurnSetting = true;

    [Tooltip("If true, always display character info, even when not selected.")]
    public bool alwaysDisplayCharacterInfoSetting = false;

    [Tooltip("If true, disables character shadows for performance or preference.")]
    public bool disableShadows = false;

    [Tooltip("Global speed multiplier applied to certain animations/timers (1 = normal).")]
    public int playSpeed = 1;

    [Tooltip("Raw defence value used to compute actual Defence via a formula.")]
    public int preDefence;

    #endregion

    #region Summon & Events

    [Header("Summon Settings")]
    [Tooltip("Internal values used to track special summon thresholds for the current run.")]
    [SerializeField] private int[] summonValues;

    /// <summary>
    /// Applies group summon logic based on a tracked value index and an amount of progress.
    /// </summary>
    /// <param name="value">Index into summonValues indicating which summon counter to decrement.</param>
    /// <param name="amount">Amount to subtract from the current summon counter.</param>
    public void GroupSummon(int value, int amount)
    {
        if (summonValues == null || summonValues.Length == 0)
        {
            Debug.LogError($"[{nameof(PlayerStats)}] Summon attempted but summonValues is not configured.");
            return;
        }

        switch (value)
        {
            case 0:
                // Guard against index errors
                if (summonValues.Length <= 0)
                {
                    Debug.LogError($"[{nameof(PlayerStats)}] summonValues[0] is not available.");
                    return;
                }

                summonValues[0] -= amount;
                if (summonValues[0] <= 0)
                {
                    summonValues[0] = 100;

                    if (SpawnManager.Instance == null || GridManager.Instance == null || EnemyController.Instance == null)
                    {
                        Debug.LogError($"[{nameof(PlayerStats)}] Required managers are missing for group summon.");
                        return;
                    }

                    string location = GridManager.Instance.RandomEnemyLocation(EnemyClass.Tank);
                    int modifier = (int)EnemyController.Instance.Quest.Modifier;
                    SpawnManager.Instance.SpawnEnemyCall(location, 107, false, modifier);
                }

                if (BattleHUDController.Instance != null)
                {
                    BattleHUDController.Instance.specialSummonText.text = summonValues[0].ToString();
                    BattleHUDController.Instance.specialSummonText.transform.parent.gameObject.SetActive(true);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(PlayerStats)}] BattleHUDController.Instance is null; cannot update special summon UI.");
                }
                break;

            default:
                Debug.LogWarning($"[{nameof(PlayerStats)}] GroupSummon called with unsupported value index: {value}");
                break;
        }
    }

    public event Action<bool> OnCharacterDeath;

    /// <summary>
    /// Invokes the character death event, passing whether the dead character is an ally.
    /// </summary>
    public void TriggerCharacterDeath(bool isAlly)
    {
        OnCharacterDeath?.Invoke(isAlly);
    }

    /// <summary>
    /// Resets per-round summon-related values at the start of a new round.
    /// </summary>
    public void NewRound()
    {
        if (summonValues != null && summonValues.Length > 0)
        {
            summonValues[0] = 100;
        }
    }

    #endregion

    #region Initialization Methods

    /// <summary>
    /// Initializes or re-initializes this player's stats.
    /// Typically called once the player selects a class or the game starts.
    /// </summary>
    public void StartUp()
    {
        // Set the maximum health from a starting health value
        MaxHealth = ClassHealth;

        // Default the player's name to their class name 
        // (could be overridden by a name entry system)
        playerName = playerClass.ToString();
    }

    /// <summary>
    /// Sets up base character stats from the selected class and equipped item.
    /// </summary>
    /// <param name="chosenClassSO">The class definition used to initialize stats.</param>
    public void SetUp(ClassSO chosenClassSO)
    {
        if (chosenClassSO == null)
        {
            Debug.LogError($"[{nameof(PlayerStats)}] SetUp called with null ClassSO.");
            return;
        }

        // Set up based on class
        ClassHealth += chosenClassSO.Health;
        MaxActionPoints += chosenClassSO.ActionPoints;
        preDefence = chosenClassSO.Defence;
        Damage += chosenClassSO.Damage;
        CritMulti += chosenClassSO.CritMulti;
        choosenClass = chosenClassSO;

        // Set up based on item
        if (Item != null)
        {
            switch (Item.Effect)
            {
                case ItemEffect.Passive_Damage_Bonus:
                    Damage += Item.EffectModifier;
                    break;

                case ItemEffect.Passive_Defense_Bonus:
                    preDefence += Item.EffectModifier;
                    break;

                case ItemEffect.Passive_Health_Bonus:
                    ClassHealth += Item.EffectModifier;
                    break;

                case ItemEffect.Passive_Regeneration:
                    // Regeneration handled elsewhere.
                    break;

                case ItemEffect.Passive_Poison_Resistance:
                    // Resistance handled elsewhere.
                    break;
            }
        }

        // Convert preDefence into actual Defence via a simple triangular-number formula.
        Defence += (preDefence * (preDefence + 1)) / 2;
    }

    #endregion
}

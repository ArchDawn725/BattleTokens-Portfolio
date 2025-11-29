using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject representing an enemy configuration:
/// type, class, base stats, actions, crit properties, and AI preferences.
/// </summary>
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Enemies/Enemy")]
public class EnemySO : ScriptableObject
{
    #region Enemy Data

    [Header("Basic Info")]
    [Tooltip("Broad categorization of this enemy (e.g., Goblin, Animal, Undead).")]
    [SerializeField] private EnemyType enemyType;

    [Tooltip("Combat class or role this enemy occupies (also determines spawn row).")]
    [SerializeField] private EnemyClass enemyClass;

    [Tooltip("Display name shown in the UI.")]
    [SerializeField] private string displayName;

    [Tooltip("Sprite used to visually represent this enemy.")]
    [SerializeField] private Sprite image;

    [Header("Population & Scaling")]
    [Tooltip("How many 'population units' this enemy counts as when spawning or balancing waves.")]
    [SerializeField] private float populationCount = 1f;

    [Tooltip("Global multiplier applied to this enemy's stats (HP, defense, damage, etc.).")]
    [SerializeField] private float statsModifier = 1f;

    [Header("Actions & Abilities")]
    [Tooltip("List of actions this enemy can perform during combat.")]
    [SerializeField] private List<ActionSO> actions = new List<ActionSO>();

    [Tooltip("Optional action used when this enemy is spawned (e.g., initial summon effect).")]
    [SerializeField] private ActionSO startingSpawn;
    public ActionSO StartingSpawnAction => startingSpawn;

    [Tooltip("How many action points this enemy can spend per turn.")]
    [SerializeField] private int actionPoints = 1;

    [Tooltip("Indicates whether this enemy is a boss.")]
    [SerializeField] private bool boss;

    [Header("Base Stat Bonuses")]
    [Tooltip("Bonus HP added on top of any base or class-based HP.")]
    [SerializeField] private int hpBonus;

    [Tooltip("Bonus defense on top of any base or class-based defense.")]
    [SerializeField] private int defBonus;

    [Tooltip("Bonus maximum damage on top of any base or class-based damage.")]
    [SerializeField] private int maxDmgBonus;

    [Header("Critical Hit Properties")]
    [Tooltip("Chance (as a percentage) that the enemy lands a critical hit.")]
    [Range(0, 100)]
    [SerializeField] private int critChance;

    [Tooltip("Multiplier applied to damage when this enemy lands a critical hit.")]
    [SerializeField] private float critMulti = 1.5f;

    [Header("Special Traits & AI")]
    [Tooltip("Optional special ability for this enemy (e.g., Lifesteal, Thorns).")]
    [SerializeField] private ClassSpecial special;

    [Tooltip("Preferred upgrade focus for AI-controlled leveling/points.")]
    [SerializeField] private AiUpgradeFocus upgradeFocus = AiUpgradeFocus.Balanced;

    #endregion

    #region Properties

    /// <summary>Broad category or species of this enemy.</summary>
    public EnemyType EnemyType => enemyType;

    /// <summary>Combat class / spawn-row role of this enemy.</summary>
    public EnemyClass EnemyClass => enemyClass;

    /// <summary>Display name shown to the player.</summary>
    public string DisplayName => displayName;

    /// <summary>Sprite used to represent this enemy.</summary>
    public Sprite Image => image;

    /// <summary>Population units this enemy counts as when spawning.</summary>
    public float PopulationCount => populationCount;

    /// <summary>Global stat multiplier applied to this enemy.</summary>
    public float StatsModifier => statsModifier;

    /// <summary>Actions this enemy can take in combat.</summary>
    public List<ActionSO> Actions => actions;

    /// <summary>Optional spawn action used when this enemy first appears.</summary>
    public ActionSO StartingSpawn => startingSpawn;

    /// <summary>Action points available to this enemy per turn.</summary>
    public int ActionPoints => actionPoints;

    /// <summary>True if this enemy is considered a boss.</summary>
    public bool Boss => boss;

    /// <summary>Bonus HP applied to this enemy.</summary>
    public int HpBonus => hpBonus;

    /// <summary>Bonus defense applied to this enemy.</summary>
    public int DefBonus => defBonus;

    /// <summary>Bonus maximum damage applied to this enemy.</summary>
    public int MaxDmgBonus => maxDmgBonus;

    /// <summary>Chance (0–100) for this enemy to land a critical hit.</summary>
    public int CritChance => critChance;

    /// <summary>Damage multiplier applied on critical hits.</summary>
    public float CritMulti => critMulti;

    /// <summary>Optional special ability this enemy has.</summary>
    public ClassSpecial Special => special;

    /// <summary>AI upgrade preference profile for this enemy.</summary>
    public AiUpgradeFocus UpgradeFocus => upgradeFocus;

    #endregion
}

/// <summary>
/// Broad category or species of an enemy.
/// </summary>
public enum EnemyType
{
    Animal,
    Goblin,
    Orc,
    Undead,
    Cult,
    Human,
    Dragon,
    Hive,
    Demon,
    Angel,
    Construct,
    Monster,
    Eldritch
}

/// <summary>
/// Combat role / spawn-row class of the enemy.
/// Primarily determines where the enemy spawns in the formation.
/// </summary>
public enum EnemyClass
{
    // Front row
    Melee_DPS,
    Tank,

    // Middle row
    Ranged_DPS,
    Assassin,

    // Back row
    Support,
    AOE_DPS,
    Boss,

    Random,
}

/// <summary>
/// Preferred upgrade focus used by AI when spending upgrade points.
/// </summary>
public enum AiUpgradeFocus
{
    Balanced,         // All stats treated equally.
    Tank_Health,      // Health prioritized, then defense, then damage.
    Tank_Defence,     // Defense prioritized, then health, then damage.
    Ranged_DPS,       // Damage prioritized, then health, then defense.
    Melee_DPS,        // Damage prioritized, then health, then defense.
    Healer,           // Support/healing oriented priorities.
    Summoner,         // Focused on summoning potency/support.
    Ranged_BloodMagic // Glass-cannon / life-cost oriented caster.
}

using UnityEngine;

/// <summary>
/// ScriptableObject representing a class configuration (e.g., Warrior, Mage).
/// Stores base stats, crit values, special traits, and available actions.
/// </summary>
[CreateAssetMenu(fileName = "NewClass", menuName = "Classes/Class")]
public class ClassSO : ScriptableObject
{
    #region Class Data

    [Header("Class Identity")]
    [Tooltip("Which class this ScriptableObject represents (e.g., Warrior, Archer).")]
    [SerializeField] private PlayerClasses playerClass;

    [Header("Core Stats")]
    [Tooltip("Base health value for this class.")]
    [SerializeField] private int health;

    [Tooltip("How many action points this class starts with or regenerates each turn.")]
    [SerializeField] private int actionPoints;

    [Tooltip("Base defense for this class, used to reduce incoming damage.")]
    [SerializeField] private int defence;

    [Tooltip("Base maximum damage bonus for this class.")]
    [SerializeField] private int damage;

    [Header("Critical Stats")]
    [Tooltip("Multiplier applied to damage when this class lands a critical hit.")]
    [SerializeField] private float critMulti;

    [Header("Special Ability")]
    [Tooltip("Optional special trait or passive (e.g., Lifesteal, Regen, Pack Tactics).")]
    [SerializeField] private ClassSpecial specialAbility;

    [Header("Available Actions")]
    [Tooltip("Actions this class can use in combat.")]
    [SerializeField] private ActionSO[] actions;

    #endregion

    #region Properties

    /// <summary>Class identity this asset represents.</summary>
    public PlayerClasses PlayerClass => playerClass;

    /// <summary>Base health for this class.</summary>
    public int Health => health;

    /// <summary>Base action points for this class.</summary>
    public int ActionPoints => actionPoints;

    /// <summary>Base defence value for this class.</summary>
    public int Defence => defence;

    /// <summary>Base max damage bonus for this class.</summary>
    public int Damage => damage;

    /// <summary>Critical hit damage multiplier for this class.</summary>
    public float CritMulti => critMulti;

    /// <summary>Special ability or passive this class has.</summary>
    public ClassSpecial SpecialAbility => specialAbility;

    /// <summary>Set of actions available to this class.</summary>
    public ActionSO[] Actions => actions;

    #endregion
}

/// <summary>
/// Special traits or perks a class can have.
/// </summary>
public enum ClassSpecial
{
    None,
    Lifesteal,
    HealthRegen,

    InfiniteHealthRegeneration,
    CounterAttack,
    Zombification,
    Resurrection_Lich,
    Transformation_Cultist,
    PackTactics,

    DefenceSteal,
    InfiniteRange, 
    MinDamageIncrease,
    AutoHeal,
    DefenceBoost,
    PoisonAttacks,
    JesterFix,
}

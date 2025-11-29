using UnityEngine;

/// <summary>
/// ScriptableObject representing an action used by a player or enemy.
/// Contains targeting data, effects, damage ranges, spawn info, and visual hooks.
/// </summary>
[CreateAssetMenu(fileName = "NewAction", menuName = "Actions/Action")]
public class ActionSO : ScriptableObject
{
    #region Action Data

    [Header("General Info")]
    [Tooltip("Class or faction this action belongs to (player or enemy).")]
    [SerializeField] private PlayerClasses ownerClass;

    [Tooltip("Display name of this action.")]
    [SerializeField] private string actionName;

    [TextArea]
    [Tooltip("Description or flavor text for this action.")]
    [SerializeField] private string description;

    [Tooltip("Icon representing this action in the UI.")]
    [SerializeField] private Sprite icon;

    [Header("Targets")]
    [Tooltip("Primary target group for this action.")]
    [SerializeField] private ActionTarget primaryTarget;

    [Tooltip("Secondary target group (optional).")]
    [SerializeField] private ActionTarget secondaryTarget;

    [Tooltip("Tertiary target group (optional).")]
    [SerializeField] private ActionTarget tertiaryTarget;

    [Header("Effects")]
    [Tooltip("Primary effect of this action (damage, heal, buff, etc.).")]
    [SerializeField] private ActionEffect primaryEffect;

    [Tooltip("Secondary effect (optional).")]
    [SerializeField] private ActionEffect secondaryEffect;

    [Tooltip("Tertiary effect (optional).")]
    [SerializeField] private ActionEffect tertiaryEffect;

    [Header("Cost & Damage")]
    [Tooltip("Action point cost or similar resource cost for using this action.")]
    [SerializeField] private int cost;

    [Tooltip("Minimum damage for the primary effect.")]
    [SerializeField] private int primaryMinDamage;

    [Tooltip("Minimum damage for the secondary effect (if applicable).")]
    [SerializeField] private int secondaryMinDamage;

    [Tooltip("Minimum damage for the tertiary effect (if applicable).")]
    [SerializeField] private int tertiaryMinDamage;

    [Tooltip("Maximum damage for the primary effect.")]
    [SerializeField] private int primaryMaxDamage;

    [Tooltip("Maximum damage for the secondary effect (if applicable).")]
    [SerializeField] private int secondaryMaxDamage;

    [Tooltip("Maximum damage for the tertiary effect (if applicable).")]
    [SerializeField] private int tertiaryMaxDamage;

    [Header("Spawning")]
    [Tooltip("Enemy/unit types that can be spawned by this action.")]
    [SerializeField] private EnemySO[] spawnableEnemies;

    [Header("Visual / VFX")]
    [Tooltip("Hit effect to play for the primary target.")]
    [SerializeField] private EffectVisual primaryHitEffect;

    [Tooltip("Hit effect to play for the secondary target.")]
    [SerializeField] private EffectVisual secondaryHitEffect;

    [Tooltip("Hit effect to play for the tertiary target.")]
    [SerializeField] private EffectVisual tertiaryHitEffect;

    [Header("Unlocking")]
    [Tooltip("If true, this action is available from the start without unlocking.")]
    [SerializeField] private bool startUnlocked;

    #endregion

    #region Properties

    public PlayerClasses OwnerClass => ownerClass;

    public string ActionName => actionName;

    public string Description => description;

    public Sprite Icon => icon;

    public ActionTarget PrimaryTarget => primaryTarget;
    public ActionTarget SecondaryTarget => secondaryTarget;
    public ActionTarget TertiaryTarget => tertiaryTarget;

    public ActionEffect PrimaryEffect => primaryEffect;
    public ActionEffect SecondaryEffect => secondaryEffect;
    public ActionEffect TertiaryEffect => tertiaryEffect;

    public int Cost => cost;

    public int PrimaryMinDamage => primaryMinDamage;
    public int SecondaryMinDamage => secondaryMinDamage;
    public int TertiaryMinDamage => tertiaryMinDamage;

    public int PrimaryMaxDamage => primaryMaxDamage;
    public int SecondaryMaxDamage => secondaryMaxDamage;
    public int TertiaryMaxDamage => tertiaryMaxDamage;

    public EnemySO[] SpawnableEnemies => spawnableEnemies;

    public EffectVisual PrimaryHitEffect => primaryHitEffect;
    public EffectVisual SecondaryHitEffect => secondaryHitEffect;
    public EffectVisual TertiaryHitEffect => tertiaryHitEffect;

    public bool StartUnlocked => startUnlocked;

    #endregion
}

/// <summary>
/// Defines player and enemy classes that can own actions.
/// </summary>
public enum PlayerClasses
{
    Warrior,
    Archer,
    Mage,
    Healer,
    Knight,
    Assassin,
    Jester,
    Vampire,
    Enemy,
    None
}

/// <summary>
/// Describes the gameplay effect an action applies.
/// Names with unusual casing/spelling are preserved for compatibility.
/// </summary>
public enum ActionEffect
{
    None,
    Damage,
    Pierce,
    Buff_Defence,
    Debuff_Defence,
    Heal,
    Buff_Damage,
    Debuff_Damage,
    Poison,
    Protect,
    Regen,
    Spawn,
    Stun,           
    SpecialSummon, 
    Relocate,
}

/// <summary>
/// Describes how an action selects its targets.
/// </summary>
public enum ActionTarget
{
    Any,            // Any single valid target.
    Any_Ranged,     // Any single valid target in the first 2 rows, excludig empty rows.
    All,            // All enemies or allies in range.
    Any_Front,      // A single target in the front row.
    All_Front,      // All targets in the front row.
    All_Middle,     // All targets in the middle row.
    All_Back,       // All targets in the back row.
    Any_Ally,       // A single ally (e.g., for healing).
    All_Ally,       // All allies.
    All_Front_Mid,  // Front + middle rows.
    Self,           // The caster only.
    Random,         // A random valid target.
    Everyone,       // All combat participants.
    Chosen,         // A target specifically selected by the user.
    Relocate,
    All_Ally_Front,
    Any_Reverse
}

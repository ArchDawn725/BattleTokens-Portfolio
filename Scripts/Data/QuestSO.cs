using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewQuest", menuName = "Quests/Quest")]
public class QuestSO : ScriptableObject
{
    #region Quest Data

    [Header("Basic Info")]
    [Tooltip("Display name shown in UI for this quest.")]
    [SerializeField] private string questName;

    [TextArea]
    [Tooltip("Short description of the quest.")]
    public string description;

    [Header("Waves")]
    [Tooltip("Sequential waves that make up this quest.")]
    public List<Wave> waves = new List<Wave>();

    [Header("Rewards")]
    [Tooltip("Base rewards granted for this quest.")]
    public Reward reward;

    [Header("Difficulty & Modifiers")]
    [Tooltip("Difficulty rating used for progression/balancing (e.g., 1–1000, >1000 for survival).")]
    public float difficultyLevel;

    [Tooltip("Optional global modifier applied to quest scaling.")]
    public float Modifier = 1f; // Name preserved for compatibility.

    [Tooltip("Special rules or behaviors applied to this quest.")]
    public QuestSpecial Special;

    [Tooltip("Multiplier applied to per-wave points/upgrade rewards.")]
    public float pointsModifier = 1f;

    #endregion

    #region Properties

    /// <summary>
    /// Read-only access to the quest's display name.
    /// </summary>
    public string QuestName => questName;

    #endregion
}

[System.Serializable]
public class Wave
{
    [Tooltip("Enemies that can appear in this wave.")]
    public List<EnemySO> possibleEnemies = new List<EnemySO>();

    [Tooltip("If true, this wave is treated as a boss wave.")]
    public bool Boss;
}

[System.Serializable]
public class Reward
{
    [Tooltip("Total XP budget used for this quest's payout distribution.")]
    public int experiencePoints;

    [Tooltip("Multiplier applied to gold earned from this quest.")]
    public float goldMulti = 1f;

    [Tooltip("Optional item rewarded on quest completion.")]
    public ItemSO item;
}

public enum QuestSpecial
{
    None,
    Carriage,
    Soldier,
    TwoSoldiers,
}

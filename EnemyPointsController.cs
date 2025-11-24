using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Calculates and applies enemy stats based on a point budget and enemy definition.
/// </summary>
public class EnemyPointsController : MonoBehaviour
{
    #region CONFIG

    // These constants document the current tuning in one place.
    private const float AnimalHpScaleDivisor = 5f;
    private const float HumanPointScaleDivisor = 5f;
    private const float ConstructDefScaleDivisor = 15f;
    private const float DragonPointScaleDivisor = 10f;
    private const float DemonDamageScaleDivisor = 20f;

    #endregion

    #region PUBLIC API

    /// <summary>
    /// Builds and applies final stats for a spawned enemy based on its definition and upgrade points.
    /// </summary>
    /// <param name="enemyDefinition">Source ScriptableObject describing the enemy.</param>
    /// <param name="upgradePoints">Points used to level its stats.</param>
    /// <param name="enemyAI">Enemy AI component that will own the actions.</param>
    /// <param name="character">Character component to initialize with final stats.</param>
    public void InitializeEnemy(
        EnemySO enemyDefinition,
        int upgradePoints,
        EnemyAI enemyAI,
        Character character)
    {
        if (enemyDefinition == null)
        {
            Debug.LogError($"{nameof(EnemyPointsController)}: Enemy definition is null. Aborting initialization.");
            return;
        }

        if (enemyAI == null)
        {
            Debug.LogError($"{nameof(EnemyPointsController)}: EnemyAI reference is null for '{enemyDefinition.DisplayName}'.");
            return;
        }

        if (character == null)
        {
            Debug.LogError($"{nameof(EnemyPointsController)}: Character reference is null for '{enemyDefinition.DisplayName}'.");
            return;
        }

        // Base stats from the definition
        int baseHealth = enemyDefinition.HpBonus;
        int baseDefence = enemyDefinition.DefBonus;
        int baseDamage = enemyDefinition.MaxDmgBonus;

        // Scale point budget by global stats modifier
        upgradePoints *= Mathf.Max(1, Mathf.RoundToInt(enemyDefinition.StatsModifier));

        // Type-specific adjustments
        switch (enemyDefinition.EnemyType)
        {
            case EnemyType.Animal:
                // Health scaling
                baseHealth += Mathf.CeilToInt(EnemyController.Instance.Quest.Modifier / AnimalHpScaleDivisor);
                break;

            case EnemyType.Goblin:
                // Intentional: relies on spawning more units instead of raw stats.
                break;

            case EnemyType.Orc:
                // Intentional: relies on spawning more units instead of raw stats.
                break;

            case EnemyType.Undead:
                character.Stats.SetUndeadResistance(true);
                break;

            case EnemyType.Human:
                // Extra upgrade points
                upgradePoints += Mathf.CeilToInt(EnemyController.Instance.Quest.Modifier / HumanPointScaleDivisor);
                break;

            case EnemyType.Monster:
                // Reserved for unique abilities.
                break;

            case EnemyType.Construct:
                // Defence scaling
                baseDefence += Mathf.CeilToInt(EnemyController.Instance.Quest.Modifier / ConstructDefScaleDivisor);
                break;

            case EnemyType.Eldritch:
                // Reserved for extreme ability variants.
                break;

            case EnemyType.Dragon:
                // Extra upgrade points for stronger stat package.
                upgradePoints += Mathf.CeilToInt(EnemyController.Instance.Quest.Modifier / DragonPointScaleDivisor);
                break;

            case EnemyType.Demon:
                // Damage scaling
                baseDamage += Mathf.CeilToInt(EnemyController.Instance.Quest.Modifier / DemonDamageScaleDivisor);
                break;
        }

        // Distribute points into Health / Defence / Damage / ActionPoints levels
        List<int> upgradeLevels = CalculateUpgradeLevels(upgradePoints, enemyDefinition);

        // Copy actions from the enemy definition into the AI
        if (enemyDefinition.Actions != null && enemyDefinition.Actions.Count > 0)
        {
            for (int i = 0; i < enemyDefinition.Actions.Count; i++)
            {
                if (enemyDefinition.Actions[i] != null)
                {
                    enemyAI.Actions.Add(enemyDefinition.Actions[i]);
                }
            }
        }

        // Compute final stats
        string displayName = enemyDefinition.DisplayName;

        // Health level (index 0) starts at 0; increment once to avoid zero multipliers.
        upgradeLevels[0]++;

        int finalHealth = Mathf.RoundToInt((baseHealth * upgradeLevels[0]) * enemyDefinition.StatsModifier);

        // Defence uses a quadratic-ish scaling based on upgraded defence.
        baseDefence += upgradeLevels[1];
        int defenceOffset = (baseDefence == 0) ? 0 : 1;
        int finalDefence = baseDefence * baseDefence - baseDefence + defenceOffset;

        int finalDamage = Mathf.RoundToInt(baseDamage + upgradeLevels[2]);
        int finalActionPoints = enemyDefinition.ActionPoints + (upgradeLevels[3] - 1);

        // Map actions to indices for network payload
        int[] actionIds = enemyDefinition.Actions != null
            ? enemyDefinition.Actions
                .Select(a => System.Array.IndexOf(LobbyAssets.Instance.actions, a))
                .ToArray()
            : System.Array.Empty<int>();

        // If something failed to map, logging is useful once during tuning.
        if (actionIds.Any(id => id < 0))
        {
            Debug.LogWarning($"{nameof(EnemyPointsController)}: One or more actions for '{displayName}' " +
                             "could not be resolved to LobbyAssets indices.");
        }

        int imageIndex = System.Array.IndexOf(LobbyAssets.Instance.characterSprites, enemyDefinition.Image);

        var characterVariables = new CharacterVariables
        {
            LocalClientId = 0,
            Name = displayName,
            Health = finalHealth,
            Defence = finalDefence,
            ImageIndex = imageIndex,
            Damage = finalDamage,
            ClassSpecial = enemyDefinition.Special,
            ActionPoints = finalActionPoints,
            CritChance = enemyDefinition.CritChance,
            CritMultiplier = enemyDefinition.CritMulti,
            ActionIds = actionIds,
            // Location left unset here; it is handled by the spawning system.
        };

        character.SetUp(characterVariables);
    }

    #endregion

    #region UPGRADE LOGIC

    /// <summary>
    /// Distributes a pool of upgrade points into health, defence, damage, and action-point levels
    /// using the enemy's upgrade focus.
    /// </summary>
    /// <remarks>
    /// Indices:
    /// 0 = Health, 1 = Defence, 2 = Damage, 3 = Action Points.
    /// </remarks>
    private List<int> CalculateUpgradeLevels(int points, EnemySO enemyDefinition)
    {
        // index 0 = Health, 1 = Defence, 2 = Damage, 3 = ActionPoints
        List<int> upgradeLevels = new List<int> { 0, 0, 0, 1 };

        // 1. Build a weight array based on the AI’s upgrade focus.
        float[] weights = GetWeightsForFocus(enemyDefinition.UpgradeFocus);

        // 2. Spend points until none remain.
        while (points > 0)
        {
            // Build a list of indices we can still afford:
            // cost = level + 1 for stats, or some other rule for AP (here: level * 20).
            List<int> affordable = new List<int>();
            for (int i = 0; i < upgradeLevels.Count; i++)
            {
                if (i < 3)
                {
                    if (points >= upgradeLevels[i] + 1)
                        affordable.Add(i);
                }
                else
                {
                    int cost = upgradeLevels[i] * 20;
                    if (points >= cost)
                        affordable.Add(i);
                }
            }

            if (affordable.Count == 0)
                break; // nothing left we can buy
        }

        return upgradeLevels;
    }

    /// <summary>
    /// Returns weight values for each upgrade slot based on the enemy's upgrade focus.
    /// </summary>
    private float[] GetWeightsForFocus(AiUpgradeFocus focus)
    {
        switch (focus)
        {
            case AiUpgradeFocus.Tank_Health:
                return new[] { 3f, 2f, 1f, 1f };
            case AiUpgradeFocus.Tank_Defence:
                return new[] { 2f, 3f, 1f, 1f };
            case AiUpgradeFocus.Ranged_DPS:
            case AiUpgradeFocus.Melee_DPS:
                return new[] { 1f, 1f, 3f, 1.5f };
            case AiUpgradeFocus.Healer:
                return new[] { 2f, 1.5f, 1f, 1.5f };
            case AiUpgradeFocus.Summoner:
                return new[] { 1.5f, 1.5f, 1f, 2f };
            case AiUpgradeFocus.Ranged_BloodMagic:
                return new[] { 1f, 1f, 3f, 2f };
            case AiUpgradeFocus.Balanced:
            default:
                return new[] { 1f, 1f, 1f, 1f };
        }
    }

    #endregion
}

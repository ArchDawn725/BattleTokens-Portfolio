using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ActionEffects : MonoBehaviour
{
    public static ActionEffects Instance;

    #region Tunables

    [Header("Timing")]
    [Tooltip("Base delay between primary/secondary/tertiary stages (before playSpeed scaling).")]
    [SerializeField] private float stageDelaySeconds = 0.1f;

    [Header("Spawning")]
    [Tooltip("Spawn 'amount' multiplier used for non-boss spawns (passed to SpawnEnemy).")]
    [SerializeField] private float nonBossSpawnAmount = 0.1f;

    [Header("Crit Rolls")]
    [Tooltip("Max value for crit roll (Random.Range(0, critRollMax)).")]
    [SerializeField] private int critRollMax = 100;

    [Header("Action IDs")]
    [SerializeField] private int primaryActionId = 1;
    [SerializeField] private int secondaryActionId = 2;
    [SerializeField] private int tertiaryActionId = 3;

    #endregion

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    public async Task ActivateEffects(
        CancellationToken token,
        ActionSO action,
        bool isPlayerAction,
        Character character,
        EnemySO enemySO,
        bool isAllyAi)
    {
        float scaledDelay = stageDelaySeconds / PlayerStats.Instance.playSpeed;

        await ActivateSecondaryEffect(token, action, isPlayerAction, character, enemySO, isAllyAi);
        await Awaitable.WaitForSecondsAsync(scaledDelay, token);

        await ActivatePrimaryEffect(token, action, isPlayerAction, character, enemySO, isAllyAi);
        await Awaitable.WaitForSecondsAsync(scaledDelay, token);

        await ActivateTertiaryEffect(token, action, isPlayerAction, character, enemySO, isAllyAi);
        await Awaitable.WaitForSecondsAsync(scaledDelay, token);
    }

    #region Primary

    private async Task ActivatePrimaryEffect(
        CancellationToken token,
        ActionSO action,
        bool isPlayerAction,
        Character character,
        EnemySO enemySO,
        bool isAllyAi)
    {
        int minDamageBonus = 0;
        int baseDamage = character.Stats.DamageBonus;
        int tempDamage = character.Stats.TempDmg;

        // --- Player / Enemy specific passive bonuses -------------------------
        if (isPlayerAction)
        {
            minDamageBonus = PlayerStats.Instance.MinDamage;

            if (PlayerStats.Instance.classSpecialUnlocked &&
                PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.PoisonAttacks &&
                action.PrimaryEffect == ActionEffect.Poison)
            {
                // Extra damage on poison actions
                baseDamage += 1;
            }

            if (PlayerStats.Instance.classSpecialUnlocked &&
                PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.DefenceSteal &&
                action.PrimaryEffect == ActionEffect.Buff_Defence)
            {
                // Extra damage when buffing defence (for steal)
                baseDamage += 1;
            }
        }
        else
        {
            if (enemySO.Special == ClassSpecial.MinDamageIncrease)
            {
                minDamageBonus++;
            }

            if (enemySO.Special == ClassSpecial.PoisonAttacks &&
                action.PrimaryEffect == ActionEffect.Poison)
            {
                baseDamage += 1;
            }

            if (enemySO.Special == ClassSpecial.DefenceSteal &&
                action.PrimaryEffect == ActionEffect.Buff_Defence)
            {
                baseDamage += 1;
            }
        }

        int minPossibleDamage = (action.PrimaryMinDamage + minDamageBonus) * (baseDamage + tempDamage + 1);
        int maxPossibleDamage = (action.PrimaryMaxDamage) * (baseDamage + tempDamage + 1);

        // Lifesteal + Pierce special case (no normal damage roll)
        if (isPlayerAction)
        {
            if (PlayerStats.Instance.classSpecialUnlocked &&
                PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.Lifesteal &&
                action.PrimaryEffect == ActionEffect.Pierce)
            {
                maxPossibleDamage = -1;
                minPossibleDamage = 0;
            }
        }
        else
        {
            if (enemySO.Special == ClassSpecial.Lifesteal &&
                action.PrimaryEffect == ActionEffect.Pierce)
            {
                maxPossibleDamage = -1;
                minPossibleDamage = 0;
            }
        }

        // Spawning logic
        if (action.PrimaryEffect == ActionEffect.Spawn)
        {
            int effectiveMaxDamage = maxPossibleDamage;

            // Crit for spawn scaling
            float critMultiplier = 1;
            if (Random.Range(0, critRollMax) < enemySO.CritChance)
            {
                critMultiplier = enemySO.CritMulti;
            }

            if (critMultiplier > 1)
            {
                effectiveMaxDamage *= 2;
            }

            await SpawnManager.Instance.SpawnEnemy(
                action.SpawnableEnemies,
                effectiveMaxDamage,
                enemySO.Boss ? 0f : nonBossSpawnAmount);
        }

        var attackVars = new AttackVariables
        {
            TargetId = string.Empty,
            MinDamage = minPossibleDamage,
            MaxDamage = maxPossibleDamage,
            ResolvedDamage = 0,
            ActionName = action.ActionName,
            ActionId = primaryActionId,
            Effect = action.PrimaryEffect,
            TargetingMode = action.PrimaryTarget,
            ImpactVisual = action.PrimaryHitEffect,
            ActionPointCost = action.Cost,
            IsPlayerAction = isPlayerAction,
            AttackerId = character.transform.parent.parent.name,
            CritMultiplier = character.Stats.CritMultiplier,
            CritChance = character.Stats.CritChance,
            IsAllyAiAction = isAllyAi,
        };

        await AttackResolver.Instance.Attack(attackVars);

        // AI side still needs to broadcast ActionUse for animations / AP sync
        if (!isPlayerAction)
        {
            OnlineRelay.Instance.ActionUse(attackVars);
        }
    }

    #endregion

    #region Secondary

    private async Task ActivateSecondaryEffect(
        CancellationToken token,
        ActionSO action,
        bool isPlayerAction,
        Character character,
        EnemySO enemySO,
        bool isAllyAi)
    {
        if (action.SecondaryEffect == ActionEffect.None) return;

        int minDamageBonus = 0;
        int baseDamage = character.Stats.DamageBonus;
        int tempDamage = character.Stats.TempDmg;

        // --- Player / Enemy specific passive bonuses -------------------------
        if (isPlayerAction)
        {
            // JesterFix cancels secondary effects completely
            if (PlayerStats.Instance.classSpecialUnlocked &&
                PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.JesterFix)
            {
                return;
            }

            minDamageBonus = PlayerStats.Instance.MinDamage;

            if (PlayerStats.Instance.classSpecialUnlocked &&
                PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.Lifesteal &&
                action.SecondaryEffect == ActionEffect.Regen)
            {
                baseDamage += 1;
            }

            if (PlayerStats.Instance.classSpecialUnlocked &&
                PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.Lifesteal &&
                action.SecondaryEffect == ActionEffect.Pierce)
            {
                // No standard damage roll
                baseDamage = 0;
                minDamageBonus = 0;
            }
        }
        else
        {
            // Player JesterFix negates enemy secondary effects
            if (PlayerStats.Instance.choosenClass.SpecialAbility == ClassSpecial.JesterFix)
            {
                return;
            }

            // Prevent bosses from self-damaging on specific secondary self-damage effects
            if (enemySO.Boss &&
                action.SecondaryTarget == ActionTarget.Self &&
                action.SecondaryEffect == ActionEffect.Damage)
            {
                return;
            }

            if (enemySO.Special == ClassSpecial.MinDamageIncrease)
            {
                minDamageBonus++;
            }

            if (enemySO.Special == ClassSpecial.Lifesteal &&
                action.SecondaryEffect == ActionEffect.Regen)
            {
                baseDamage += 1;
            }

            if (enemySO.Special == ClassSpecial.Lifesteal &&
                action.SecondaryEffect == ActionEffect.Pierce)
            {
                baseDamage = 0;
                minDamageBonus = 0;
            }
        }

        int minPossibleDamage = (action.PrimaryMinDamage + minDamageBonus) * (baseDamage + tempDamage + 1);
        int maxPossibleDamage = (action.PrimaryMaxDamage) * (baseDamage + tempDamage + 1);

        // Spawning logic
        if (action.SecondaryEffect == ActionEffect.Spawn)
        {
            int effectiveMaxDamage = maxPossibleDamage;

            float critMultiplier = 1;
            if (Random.Range(0, critRollMax) < enemySO.CritChance)
            {
                critMultiplier = enemySO.CritMulti;
            }

            if (critMultiplier > 1)
            {
                effectiveMaxDamage *= 2;
            }

            await SpawnManager.Instance.SpawnEnemy(
                action.SpawnableEnemies,
                effectiveMaxDamage,
                enemySO.Boss ? 0f : nonBossSpawnAmount);
        }

        var attackVars = new AttackVariables
        {
            TargetId = string.Empty,
            MinDamage = minPossibleDamage,
            MaxDamage = maxPossibleDamage,
            ResolvedDamage = 0,
            ActionName = string.Empty,
            ActionId = secondaryActionId,
            Effect = action.SecondaryEffect,
            TargetingMode = action.SecondaryTarget,
            ImpactVisual = action.SecondaryHitEffect,
            ActionPointCost = 0,
            IsPlayerAction = isPlayerAction,
            AttackerId = character.transform.parent.parent.name,
            CritMultiplier = character.Stats.CritMultiplier,
            CritChance = character.Stats.CritChance,
            IsAllyAiAction = isAllyAi,
        };

        await AttackResolver.Instance.Attack(attackVars);
    }

    #endregion

    #region Tertiary

    private async Task ActivateTertiaryEffect(
        CancellationToken token,
        ActionSO action,
        bool isPlayerAction,
        Character character,
        EnemySO enemySO,
        bool isAllyAi)
    {
        if (action.TertiaryEffect == ActionEffect.None) return;

        int minDamageBonus = 0;
        int baseDamage = character.Stats.DamageBonus;
        int tempDamage = character.Stats.TempDmg;

        if (isPlayerAction)
        {
            minDamageBonus = PlayerStats.Instance.MinDamage;
        }
        else
        {
            if (enemySO.Special == ClassSpecial.MinDamageIncrease)
            {
                minDamageBonus++;
            }
        }

        int minPossibleDamage = (action.PrimaryMinDamage + minDamageBonus) * (baseDamage + tempDamage + 1);
        int maxPossibleDamage = (action.PrimaryMaxDamage) * (baseDamage + tempDamage + 1);

        // Spawning logic
        if (action.TertiaryEffect == ActionEffect.Spawn)
        {
            int effectiveMaxDamage = maxPossibleDamage;

            float critMultiplier = 1;
            if (Random.Range(0, critRollMax) < enemySO.CritChance)
            {
                critMultiplier = enemySO.CritMulti;
            }

            if (critMultiplier > 1)
            {
                effectiveMaxDamage *= 2;
            }

            await SpawnManager.Instance.SpawnEnemy(
                action.SpawnableEnemies,
                effectiveMaxDamage,
                enemySO.Boss ? 0f : nonBossSpawnAmount);
        }

        var attackVars = new AttackVariables
        {
            TargetId = string.Empty,
            MinDamage = minPossibleDamage,
            MaxDamage = maxPossibleDamage,
            ResolvedDamage = 0,
            ActionName = string.Empty,
            ActionId = tertiaryActionId,
            Effect = action.TertiaryEffect,
            TargetingMode = action.TertiaryTarget,
            ImpactVisual = action.TertiaryHitEffect,
            ActionPointCost = 0,
            IsPlayerAction = isPlayerAction,
            AttackerId = character.transform.parent.parent.name,
            CritMultiplier = character.Stats.CritMultiplier,
            CritChance = character.Stats.CritChance,
            IsAllyAiAction = isAllyAi,
        };

        await AttackResolver.Instance.Attack(attackVars);
    }

    #endregion
}

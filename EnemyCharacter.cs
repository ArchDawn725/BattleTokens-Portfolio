using System.Threading;
using UnityEngine;

/// <summary>
/// Wrapper around an enemy character instance:
/// - Creates and configures <see cref="EnemyAI"/> and <see cref="EnemyPointsController"/>.
/// - Applies wave/turn-based debuffs.
/// - Handles special behaviors (boss spawns, cult buffs, pack tactics).
/// </summary>
[DisallowMultipleComponent]
public class EnemyCharacter : MonoBehaviour
{
    #region Runtime References

    /// <summary>The underlying enemy definition (stats, type, specials, etc.).</summary>
    private EnemySO enemySO;

    /// <summary>The attached Character component representing stats and view.</summary>
    public Character Character { get; private set; }

    /// <summary>The AI brain controlling this enemy or AI ally.</summary>
    public EnemyAI EnemyAI { get; private set; }

    /// <summary>Handles stat allocation based on points / difficulty.</summary>
    private EnemyPointsController pointsController;

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes this enemy instance with its data, AI, and stat scaling.
    /// Called by <see cref="SpawnManager"/> after instantiation.
    /// </summary>
    /// <param name="so">Enemy definition ScriptableObject.</param>
    /// <param name="points">Budget used to scale stats up or down.</param>
    /// <param name="isAlly">True if this enemy is actually an AI ally.</param>
    public void SetUp(EnemySO so, int points, bool isAlly)
    {
        Character = GetComponent<Character>();
        if (Character == null)
        {
            Debug.LogError($"[{nameof(EnemyCharacter)}] Missing Character component on {name}.", this);
            return;
        }

        enemySO = so;

        // AI allies with special abilities are slightly cheaper.
        if (isAlly && so.Special != ClassSpecial.None)
        {
            points -= 10;
        }

        EnemyAI = gameObject.AddComponent<EnemyAI>();
        pointsController = gameObject.AddComponent<EnemyPointsController>();

        EnemyAI.SetUp(isAlly, Character, this, so);
        pointsController.InitializeEnemy(so, points, EnemyAI, Character);

        // Boss-specific spawn behavior.
        if (so.Boss)
        {
            PerformStartingSpawn();
        }

        // Apply special pack-tactics / post-spawn bonuses once spawning is fully done.
        if (SpawnManager.Instance != null)
        {
            if (SpawnManager.Instance.doneSpawning)
            {
                ApplyDelayedSpecialEffects();
            }
            else
            {
                SpawnManager.Instance.OnDoneSpawning += ApplyDelayedSpecialEffects;
            }
        }
        else
        {
            Debug.LogWarning($"[{nameof(EnemyCharacter)}] SpawnManager.Instance is null, cannot subscribe to OnDoneSpawning.", this);
        }

        // Apply over-time defence debuff if fight is going too long.
        ApplyOverTimerDefenceDebuff();
        Character.View.UpdateUI();
    }

    #endregion

    #region Turn Handling

    /// <summary>
    /// Begins this enemy's AI turn, passing in a cancellation token that can be used to abort.
    /// </summary>
    public void NewTurn(CancellationToken token)
    {
        if (EnemyAI == null)
        {
            Debug.LogWarning($"[{nameof(EnemyCharacter)}] NewTurn called but EnemyAI is null on {name}.", this);
            return;
        }

        EnemyAI.NewTurn(token);
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnCharacterDeath += HandleCharacterDeath;
        }
        else
        {
            Debug.LogWarning($"[{nameof(EnemyCharacter)}] PlayerStats.Instance is null in OnEnable.", this);
        }
    }

    private void OnDisable()
    {
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnCharacterDeath -= HandleCharacterDeath;
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnDoneSpawning -= ApplyDelayedSpecialEffects;
        }
    }

    private void OnDestroy()
    {
        // Remove this object from the EnemyController tracking lists.
        if (EnemyController.Instance != null)
        {
            EnemyController.Instance.enemies.Remove(gameObject);
            EnemyController.Instance.aiAllies.Remove(gameObject);
        }

        // Extra safety in case OnDisable was not called (domain reload, etc.)
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnDoneSpawning -= ApplyDelayedSpecialEffects;
        }

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.OnCharacterDeath -= HandleCharacterDeath;
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Called whenever a character dies (ally/enemy). Used for cult-style buffs.
    /// </summary>
    private void HandleCharacterDeath(bool isAlly)
    {
        if (EnemyAI == null || Character == null || Character.Stats == null)
            return;

        // Only react if the dead unit aligns with this AI's allegiance.
        if (EnemyAI.IsAiAlly == isAlly && enemySO != null && enemySO.EnemyType == EnemyType.Cult)
        {
            Character.Stats.AdjustTempDmg(1);
            Character.View.UpdateUI();
        }
    }

    #endregion

    #region Boss & Special Behavior

    /// <summary>
    /// Handles boss "starting spawn" behavior (e.g., summoning additional enemies).
    /// </summary>
    private void PerformStartingSpawn()
    {
        if (enemySO == null)
        {
            Debug.LogWarning($"[{nameof(EnemyCharacter)}] PerformStartingSpawn called but enemySO is null.", this);
            return;
        }

        Debug.Log($"[{nameof(EnemyCharacter)}] Starting spawn behavior for boss: {enemySO.DisplayName}");

        ActionSO chosenAction = enemySO.StartingSpawnAction;
        if (chosenAction == null || chosenAction.PrimaryEffect != ActionEffect.Spawn)
            return;

        if (EnemyController.Instance == null || SpawnManager.Instance == null)
        {
            Debug.LogWarning($"[{nameof(EnemyCharacter)}] EnemyController or SpawnManager is null, cannot perform boss spawn.", this);
            return;
        }

        // Number of "players" (allies + AI allies + any quest specials).
        float players = EnemyController.Instance.allies.Count + SpawnManager.Instance.AIAlliesCount;
        if (EnemyController.Instance.Quest.Special != QuestSpecial.None) players++;
        if (EnemyController.Instance.Quest.Special == QuestSpecial.TwoSoldiers) players++;
        if (players > 9) players = 9;

        float spawnCount = chosenAction.PrimaryMaxDamage * players;
        int maxPossibleDamage = (chosenAction.PrimaryMaxDamage + Character.Stats.TempDmg) *
                                (Character.Stats.DamageBonus + 1);

        Debug.Log($"[{nameof(EnemyCharacter)}] Starting Spawn Count: {spawnCount} (MaxPossibleDamage: {maxPossibleDamage})");

        // For now, we pass 'maxPossibleDamage' as points and use an amount based on whether it's a boss.
        SpawnManager.Instance.SpawnEnemy(
            chosenAction.SpawnableEnemies,
            maxPossibleDamage,
            enemySO.Boss ? 0f : 0.1f);
    }

    /// <summary>
    /// Applies special effects that depend on all spawns being finished
    /// (e.g., Pack Tactics bonus based on total enemies).
    /// </summary>
    private void ApplyDelayedSpecialEffects()
    {
        if (enemySO == null || Character == null || Character.Stats == null)
            return;

        switch (enemySO.Special)
        {
            case ClassSpecial.PackTactics:
                if (EnemyController.Instance != null)
                {
                    int alliesCount = EnemyController.Instance.enemies.Count - 1;
                    int bonus = alliesCount / 2;
                    Character.Stats.AdjustTempDmg(bonus);
                    Character.View.UpdateUI();
                }
                break;
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnDoneSpawning -= ApplyDelayedSpecialEffects;
        }
    }

    /// <summary>
    /// Applies a defence debuff if the wave has gone on too long
    /// (different thresholds for boss vs non-boss waves).
    /// </summary>
    private void ApplyOverTimerDefenceDebuff()
    {
        if (EnemyController.Instance == null || Character == null || Character.Stats == null)
            return;

        int wave = EnemyController.Instance.wave;
        int turn = EnemyController.Instance.turn;

        if (wave <= 0 || EnemyController.Instance.Quest == null)
            return;

        int overTimerDefDebuff = 0;

        // Boss waves get stricter debuff after turn 20
        if (EnemyController.Instance.Quest.waves[wave - 1].Boss && turn > 20)
        {
            overTimerDefDebuff = ((turn - 20) * (turn - 21) / 2) + 1;
        }
        // Non-boss waves start debuffing after turn 10
        else if (!EnemyController.Instance.Quest.waves[wave - 1].Boss && turn > 10)
        {
            overTimerDefDebuff = ((turn - 10) * (turn - 11) / 2) + 1;
        }

        if (overTimerDefDebuff > 0)
        {
            Character.Stats.AdjustTempDef(-overTimerDefDebuff);
        }
    }

    #endregion
}

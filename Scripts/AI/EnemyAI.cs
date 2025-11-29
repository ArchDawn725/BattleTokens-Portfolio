using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple enemy / AI ally brain:
/// - Chooses actions from a list of <see cref="ActionSO"/>s.
/// - Applies basic filtering so it doesn't waste heals/buffs.
/// - Notifies listeners when its turn is finished.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    #region Events

    /// <summary>
    /// Raised exactly once when this AI has completed its turn (or is forced to end it).
    /// </summary>
    public event System.Action<EnemyCharacter> OnTurnFinished;

    #endregion

    #region Fields

    private bool _turnReported;

    [Header("Actions")]
    [Tooltip("List of possible actions this enemy can perform.")]
    public List<ActionSO> Actions = new List<ActionSO>();

    /// <summary>
    /// If true, this AI is treated as an allied AI (uses ally grid targeting),
    /// otherwise it is treated as an enemy AI.
    /// </summary>
    [Tooltip("If true, treat this as an AI ally instead of a hostile enemy.")]
    public bool IsAiAlly;

    private Character _character;
    private EnemyCharacter _enemyCharacter;
    private EnemySO _enemyDefinition;

    #endregion

    #region Setup

    /// <summary>
    /// Initializes this AI with its owning <see cref="Character"/>, wrapper <see cref="EnemyCharacter"/>,
    /// and definition data <see cref="EnemySO"/>.
    /// </summary>
    public void SetUp(bool isAlly, Character character, EnemyCharacter enemy, EnemySO enemyDefinition)
    {
        IsAiAlly = isAlly;
        _character = character;
        _enemyCharacter = enemy;
        _enemyDefinition = enemyDefinition;
    }

    #endregion

    #region Turn Logic

    /// <summary>
    /// Starts a new AI turn. If the character is dead or disabled, the turn finishes immediately.
    /// </summary>
    public void NewTurn(CancellationToken token)
    {
        _turnReported = false;

        if (_character == null || _character.Stats.IsDead || !gameObject.activeInHierarchy)
        {
            ReportTurnFinished();
            return;
        }

        _ = TakeTurnLoop(token); // fire and forget, completion is tracked by event + timeout
    }

    /// <summary>
    /// Main asynchronous loop that chooses and executes actions until the AI runs out of AP
    /// or dies. Respects the provided <see cref="CancellationToken"/>.
    /// </summary>
    private async Task TakeTurnLoop(CancellationToken token)
    {
        // Guard: no actions configured → finish immediately
        if (Actions.Count <= 0)
        {
            await Awaitable.WaitForSecondsAsync(0.25f / PlayerStats.Instance.playSpeed, token);
            Debug.LogWarning($"[EnemyAI] Enemy '{name}' has no actions to perform.", this);
            ReportTurnFinished();
            return;
        }

        while (_character != null &&
               !_character.Stats.IsDead &&
               _character.Stats.ActionPoints > 0 &&
               !token.IsCancellationRequested)
        {
            List<ActionSO> availableActions = GetAvailableActions();

            if (availableActions.Count == 0)
            {
                Debug.LogWarning($"[EnemyAI] Enemy '{name}' has no valid actions; falling back to full list.", this);
                availableActions = new List<ActionSO>(Actions);
                if (availableActions.Count == 0)
                {
                    // Absolute guard: no actions at all
                    break;
                }
            }

            ActionSO chosenAction = availableActions[Random.Range(0, availableActions.Count)];

            // Execute action effects
            await ActionEffects.Instance.ActivateEffects(token, chosenAction, false, _character, _enemyDefinition, IsAiAlly);

            // If the character still has AP, wait a short time before next action
            if (_character != null &&
                !_character.Stats.IsDead &&
                _character.Stats.ActionPoints > 0 &&
                !token.IsCancellationRequested)
            {
                await Awaitable.WaitForSecondsAsync(0.15f / PlayerStats.Instance.playSpeed, token);
            }
        }

        ReportTurnFinished();
    }

    /// <summary>
    /// Called by external code (e.g., timeout) to forcibly end this AI's turn safely.
    /// </summary>
    public void ForceEndTurnSafe()
    {
        StopAllCoroutines(); // existing behavior kept; async work is still cancelled via token/timeout
        ReportTurnFinished();
    }

    /// <summary>
    /// Ensures <see cref="OnTurnFinished"/> is only invoked once.
    /// </summary>
    private void ReportTurnFinished()
    {
        if (_turnReported) return;
        _turnReported = true;

        if (_enemyCharacter == null)
        {
            Debug.LogWarning("[EnemyAI] Turn finished, but EnemyCharacter reference is null.", this);
        }

        OnTurnFinished?.Invoke(_enemyCharacter);
    }

    #endregion

    #region Decision Logic

    /// <summary>
    /// Returns a filtered list of actions that make sense in the current board state.
    /// For example: avoids healing at full health, or hitting empty rows.
    /// </summary>
    private List<ActionSO> GetAvailableActions()
    {
        List<ActionSO> availableActions = new List<ActionSO>(Actions);

        // Iterate over original list so we can remove from the copy
        for (int i = Actions.Count - 1; i >= 0; i--)
        {
            ActionSO action = Actions[i];
            bool invalidAction = false;

            // NOTE: If you implement explicit AP costs per action, check them here.

            switch (action.PrimaryTarget)
            {
                case ActionTarget.Self:
                    invalidAction = EvaluateSelfTargetAction(action);
                    break;

                case ActionTarget.All_Front:
                    if (TargetSelectionService.Instance.AnyEnemiesInRow(IsAiAlly, 'F') <= 1)
                        invalidAction = true;
                    break;

                case ActionTarget.All_Middle:
                    if (TargetSelectionService.Instance.AnyEnemiesInRow(IsAiAlly, 'M') <= 1)
                        invalidAction = true;
                    break;

                case ActionTarget.All_Back:
                    if (TargetSelectionService.Instance.AnyEnemiesInRow(IsAiAlly, 'B') <= 1)
                        invalidAction = true;
                    break;

                case ActionTarget.All_Front_Mid:
                    // '2' means front & middle
                    if (TargetSelectionService.Instance.AnyEnemiesInRow(IsAiAlly, '2') <= 1)
                        invalidAction = true;
                    break;

                case ActionTarget.All:
                    if (TargetSelectionService.Instance.AnyEnemiesInRow(IsAiAlly, '3') <= 1)
                        invalidAction = true;
                    break;

                case ActionTarget.Any_Ally:
                    invalidAction = EvaluateAnyAllyAction(action);
                    break;

                case ActionTarget.All_Ally:
                    invalidAction = EvaluateAllAllyAction(action);
                    break;

                default:
                    // For other targets (single enemy, random, etc.), we keep them valid by default.
                    invalidAction = false;
                    break;
            }

            if (invalidAction)
            {
                availableActions.Remove(action);
            }
        }

        return availableActions;
    }

    /// <summary>
    /// Evaluates whether a self-targeted action should be considered invalid.
    /// </summary>
    private bool EvaluateSelfTargetAction(ActionSO action)
    {
        switch (action.PrimaryEffect)
        {
            case ActionEffect.Heal:
                // At or above max HP → healing is pointless
                if (_character.Stats.Health >= _character.Stats.MaxHealth)
                    return true;
                break;

            case ActionEffect.Buff_Defence:
                // Depending on the AI's row position, we may decide buffing self is wasteful
                if (transform.parent != null && transform.parent.name.Length > 1)
                {
                    char row = transform.parent.name[1];

                    // In middle row, if there's already someone in front, skip self-buff
                    if (row == 'M' &&
                        TargetSelectionService.Instance.AnyEnemiesInRow(!IsAiAlly, 'F') > 0)
                    {
                        return true;
                    }

                    // In back row, if there are allies in front/mid, skip self-buff
                    if (row == 'B' &&
                        TargetSelectionService.Instance.AnyEnemiesInRow(!IsAiAlly, '2') > 0)
                    {
                        return true;
                    }
                }
                break;

            case ActionEffect.Spawn:
                // Over-time safety: stop spawning after turn X depending on boss/non-boss
                if (EnemyController.Instance.Quest.waves[EnemyController.Instance.wave - 1].Boss &&
                    EnemyController.Instance.turn > 20)
                {
                    return true;
                }

                if (!EnemyController.Instance.Quest.waves[EnemyController.Instance.wave - 1].Boss &&
                    EnemyController.Instance.turn > 10)
                {
                    return true;
                }

                // Check if there are enough open slots for the spawn
                int spawnCount = (int)(action.PrimaryMaxDamage * EnemyController.Instance.allies.Count);
                List<Button> openSpots = IsAiAlly
                    ? GridManager.Instance.allyGrid.FindAll(btn => btn.transform.GetChild(2).childCount == 0)
                    : GridManager.Instance.enemyGrid.FindAll(btn => btn.transform.GetChild(2).childCount == 0);

                if (spawnCount > openSpots.Count)
                {
                    return true;
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Evaluates an ally-targeted (single ally) action, e.g. heal/protect.
    /// Returns true if it should be considered invalid.
    /// </summary>
    private bool EvaluateAnyAllyAction(ActionSO action)
    {
        switch (action.PrimaryEffect)
        {
            case ActionEffect.Heal:
                // Look for any ally that is missing health; if none, healing is pointless
                {
                    bool needsHealing = false;
                    var sourceGrid = IsAiAlly ? GridManager.Instance.allyGrid : GridManager.Instance.enemyGrid;
                    var targets = sourceGrid.FindAll(btn => btn.transform.GetChild(2).childCount > 0);

                    foreach (Button target in targets)
                    {
                        Character candidate = target.transform.GetChild(2).GetChild(0).GetComponent<Character>();
                        if (candidate != null && candidate.Stats.Health < candidate.Stats.MaxHealth)
                        {
                            needsHealing = true;
                            break;
                        }
                    }

                    return !needsHealing;
                }

            case ActionEffect.Protect:
                // If there's only 1 unit on that side, protecting an ally is pointless
                int count = IsAiAlly ? EnemyController.Instance.allies.Count : EnemyController.Instance.enemies.Count;
                if (count <= 1)
                {
                    return true;
                }
                break;
        }

        return false;
    }

    /// <summary>
    /// Evaluates an action that targets all allies (e.g., AoE heal).
    /// Returns true if it should be considered invalid.
    /// </summary>
    private bool EvaluateAllAllyAction(ActionSO action)
    {
        switch (action.PrimaryEffect)
        {
            case ActionEffect.Heal:
                // Require at least 2 allies that need healing before using an AoE heal
                bool needsHealing = false;
                int healingTargets = 0;
                var sourceGrid = IsAiAlly ? GridManager.Instance.allyGrid : GridManager.Instance.enemyGrid;
                var targets = sourceGrid.FindAll(btn => btn.transform.GetChild(2).childCount > 0);

                foreach (Button target in targets)
                {
                    Character candidate = target.transform.GetChild(2).GetChild(0).GetComponent<Character>();
                    if (candidate != null && candidate.Stats.Health < candidate.Stats.MaxHealth)
                    {
                        healingTargets++;
                        if (healingTargets > 1)
                        {
                            needsHealing = true;
                            break;
                        }
                    }
                }

                return !needsHealing;
        }

        return false;
    }

    #endregion

    #region Unity Callbacks

    private void OnDisable()
    {
        // If this object is disabled mid-turn, make sure listeners aren't left hanging
        ReportTurnFinished();
    }

    #endregion
}

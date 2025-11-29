using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Coordinates the flow of enemy/AI turns and hands control back to players.
/// Handles cancellation, timeouts, and ensuring only one enemy turn runs at a time.
/// </summary>
public class TurnHandler : MonoBehaviour
{
    #region Singleton

    public static TurnHandler Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(TurnHandler)}] Multiple instances found, destroying duplicate on {name}.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region References

    [Header("References")]
    [SerializeField]
    [Tooltip("Controller responsible for managing enemies and AI allies.")]
    private EnemyController enemyController;

    private bool isHostDriver;

    #endregion

    #region Turn State & Cancellation

    [Header("Turn State")]
    [Tooltip("True while an enemy/AI turn is currently executing.")]
    private bool isEnemyTurnActive;

    [Tooltip("Internal guard to prevent processing the same enemy turn twice.")]
    private bool doubleTurnGuardTriggered;

    private CancellationTokenSource turnCancellationSource;

    #endregion

    #region AI Timing Settings

    [Header("AI Timing")]
    [Tooltip("Minimum timeout (in seconds) for a single AI unit's turn.")]
    private const float MinTurnDurationSeconds = 1.5f;

    [Tooltip("Maximum timeout (in seconds) for a single AI unit's turn.")]
    private const float MaxTurnDurationSeconds = 15f;

    [Tooltip("Seconds of timeout per action point (before playSpeed scaling).")]
    private const float TimeOutPerActionSeconds = 2.5f;

    private const float PreEnemyPhaseClearDelaySeconds = 0.1f;
    private const float BetweenGroupsDelaySeconds = 0.25f;

    private static readonly TimeSpan ForcedEndGracePeriod = TimeSpan.FromMilliseconds(100);

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (enemyController == null)
        {
            enemyController = EnemyController.Instance;
            if (enemyController == null)
            {
                Debug.LogError($"[{nameof(TurnHandler)}] EnemyController reference is not set and could not be found. Enemy turns will not run.");
            }
        }
    }

    private void OnDestroy()
    {
        Cancel();
        CleanupTurnCancellation();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Begins the enemy/AI turn sequence. Only the host (or offline) will drive AI,
    /// then control is returned to the players.
    /// </summary>
    public async Task StartEnemyTurnAsync()
    {
        // Determine if this instance is responsible for driving the AI
        isHostDriver = NetworkManager.Singleton == null
                       || NetworkManager.Singleton.IsHost
                       || !OnlineRelay.Instance.IsConnected();

        // Clear grid interaction before AI begins
        await GridManager.Instance.ClearAll();
        await Awaitable.WaitForSecondsAsync(PreEnemyPhaseClearDelaySeconds);

        // If a previous enemy turn is somehow still in progress, cancel that one
        if (turnCancellationSource != null)
        {
            Debug.LogWarning("[TurnHandler] Enemy turn already in progress, cancelling previous.");
            Cancel();
            BattleHUDController.Instance.endTurnButton.interactable = true;
            return;
        }

        turnCancellationSource = new CancellationTokenSource();

        try
        {
            await EnemyTurnAsync(turnCancellationSource.Token);
        }
        catch (OperationCanceledException e)
        {
            Debug.LogWarning($"[TurnHandler] Enemy turn cancelled: {e.Message}");
        }
        finally
        {
            CleanupTurnCancellation();

            if (doubleTurnGuardTriggered)
            {
                doubleTurnGuardTriggered = false;
            }
            else
            {
                isEnemyTurnActive = false;
            }
        }
    }

    /// <summary>
    /// Requests cancellation of the current enemy turn (if any).
    /// </summary>
    public void Cancel()
    {
        if (turnCancellationSource == null)
            return;

        if (!turnCancellationSource.IsCancellationRequested)
            turnCancellationSource.Cancel();
    }

    #endregion

    #region Enemy Turn Flow

    private async Task EnemyTurnAsync(CancellationToken token)
    {
        await Awaitable.WaitForSecondsAsync(BetweenGroupsDelaySeconds / PlayerStats.Instance.playSpeed, token);

        // Failsafe - ensure timer is stopped at start of enemy phase
        TurnTimerUIController.Instance.StopTimer();

        if (enemyController != null && enemyController.CheckGameStatus())
        {
            isEnemyTurnActive = false;
            Cancel();
            return;
        }

        if (isEnemyTurnActive)
        {
            Debug.LogWarning("[TurnHandler] Attempted to start an enemy turn while another is active.");
            doubleTurnGuardTriggered = true;
            Cancel();
            return;
        }

        isEnemyTurnActive = true;

        // Only host (or offline) runs AI logic
        if (!isHostDriver || enemyController == null)
            return;

        // AI Allies (if any)
        if (enemyController.aiAllies.Count > 0)
        {
            await RunAITurnsAsync(true, token);
        }

        await Awaitable.WaitForSecondsAsync(BetweenGroupsDelaySeconds / PlayerStats.Instance.playSpeed, token);

        if (enemyController.CheckGameStatus())
        {
            isEnemyTurnActive = false;
            Cancel();
            return;
        }

        // Enemies
        await RunAITurnsAsync(false, token);

        await Awaitable.WaitForSecondsAsync(BetweenGroupsDelaySeconds / PlayerStats.Instance.playSpeed, token);

        if (enemyController.CheckGameStatus())
        {
            isEnemyTurnActive = false;
            Cancel();
            return;
        }

        // Hand control back to players
        isEnemyTurnActive = false;
        OnlineRelay.Instance.PlayerTurnCall();
    }

    private async Task RunAITurnsAsync(bool allies, CancellationToken token)
    {
        var list = allies ? enemyController.aiAllies : enemyController.enemies;
        var snapshot = new List<GameObject>(list); // snapshot to avoid collection modification during iteration

        foreach (var go in snapshot)
        {
            if (!go) continue;
            if (!go.TryGetComponent(out EnemyCharacter enemy)) continue;
            if (enemy.Character == null || enemy.Character.Stats.IsDead) continue;

            // Timeout based on this enemy's action points
            int actionsPerTurn = Mathf.Max(1, enemy.Character.Stats.ActionPoints);
            float timeoutSeconds = Mathf.Clamp(
                actionsPerTurn * TimeOutPerActionSeconds,
                MinTurnDurationSeconds,
                MaxTurnDurationSeconds);

            var tcs = new TaskCompletionSource<bool>();

            void OnTurnFinishedHandler(EnemyCharacter finishedEnemy)
            {
                if (ReferenceEquals(finishedEnemy, enemy))
                    tcs.TrySetResult(true);
            }

            try
            {
                enemy.EnemyAI.OnTurnFinished += OnTurnFinishedHandler;

                // Only host/offline actually triggers the turn
                if (isHostDriver)
                    enemy.NewTurn(token);

                // Wait for completion OR timeout (scaled by playSpeed)
                var finishedTask = tcs.Task;
                var timeoutTask = Task.Delay(
                    TimeSpan.FromSeconds(timeoutSeconds / PlayerStats.Instance.playSpeed),
                    token);

                var completed = await Task.WhenAny(finishedTask, timeoutTask);

                if (completed != finishedTask)
                {
                    // Timeout: force the AI to end its turn and give it a brief chance to raise the event
                    Debug.LogWarning($"[TurnHandler] Enemy '{enemy.name}' timed out after {timeoutSeconds:0.0}s, forcing end of turn.");
                    enemy.EnemyAI.ForceEndTurnSafe();

                    await Task.WhenAny(finishedTask, Task.Delay(ForcedEndGracePeriod, token));
                }
            }
            finally
            {
                if (enemy != null)
                    enemy.EnemyAI.OnTurnFinished -= OnTurnFinishedHandler;
            }
        }
    }

    #endregion

    #region Helpers

    private void CleanupTurnCancellation()
    {
        if (turnCancellationSource == null)
            return;

        turnCancellationSource.Dispose();
        turnCancellationSource = null;
    }

    #endregion
}

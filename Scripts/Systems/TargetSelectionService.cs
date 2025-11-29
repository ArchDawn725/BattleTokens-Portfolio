using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TargetSelectionService : MonoBehaviour
{
    #region Singleton

    public static TargetSelectionService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(TargetSelectionService)}] Duplicate instance detected, destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region Selection State

    /// <summary>
    /// Current selection mode for grid button presses
    /// (spawning, attacking, relocating, etc.).
    /// </summary>
    [Header("Selection State")]
    [Tooltip("Current selection mode for board input (spawn, attack, relocate, etc.).")]
    public SelectionMode CurrentSelectionMode;

    public enum SelectionMode
    {
        None,
        ChoosingSpawn,
        Attacking,
        Relocating
    }

    /// <summary>
    /// Cached attack data when the player has chosen an action but not yet chosen a target.
    /// </summary>
    [Tooltip("Pending attack/action data, used when waiting for the player to select a target tile.")]
    public AttackVariables PendingAttackData;

    [Tooltip("True while an action is waiting for a target selection.")]
    public bool HasPendingAction;

    #endregion

    #region Shortcuts

    private GridManager Grid => GridManager.Instance;
    private SpawnManager Spawner => SpawnManager.Instance;
    private AttackResolver AttackResolver => AttackResolver.Instance;

    #endregion

    #region Public API

    /// <summary>
    /// Called when a board/grid button is pressed by the player.
    /// The behavior depends on the current selection mode.
    /// </summary>
    public void OnGridButtonPressed(Button button)
    {
        if (button == null)
        {
            Debug.LogWarning($"[{nameof(TargetSelectionService)}] OnGridButtonPressed received a null button.");
            return;
        }

        if (Grid == null || Spawner == null || AttackResolver == null)
        {
            Debug.LogError($"[{nameof(TargetSelectionService)}] Missing dependencies (Grid/Spawner/AttackResolver).");
            return;
        }

        // Cache what the mode *was* when we started this call, so we can
        // react correctly after resetting it at the end.
        var previousMode = CurrentSelectionMode;

        switch (CurrentSelectionMode)
        {
            case SelectionMode.ChoosingSpawn:
                HandleSpawnSelection(button);
                break;

            case SelectionMode.Attacking:
                HandleAttackSelection(button);
                break;

            case SelectionMode.Relocating:
                HandleRelocateSelection(button);
                break;

            case SelectionMode.None:
            default:
                // No active selection mode; ignore presses silently
                break;
        }

        // Clear board state and reset selection
        Grid.ClearAll();
        CurrentSelectionMode = SelectionMode.None;

        // Re-enable basic HUD actions and refresh AP/availability
        BattleHUDController.Instance.TemperaryActionButtonsEnable();
        BattleHUDController.Instance.UpdateGameActions();

        // NOTE: Bug fix:
        // Original code checked `if (choice != Choice.Relocating)` *after*
        // setting choice to none, so the condition was always true.
        // Here we correctly check the mode *before* it was reset.
        if (previousMode != SelectionMode.Relocating && BattleHUDController.Instance.endTurnButton != null)
        {
            UINavigationController.Instance.JumpToElement(BattleHUDController.Instance.endTurnButton);
        }
    }

    /// <summary>
    /// Makes the specified buttons available for user selection during targeting.
    /// Also subscribes to death events so we can refresh if a target dies mid-selection.
    /// </summary>
    public void EnableButtonsForUserSelection(List<Button> targetButtons)
    {
        if (targetButtons == null)
        {
            Debug.LogWarning($"[{nameof(TargetSelectionService)}] EnableButtonsForUserSelection received a null list.");
            return;
        }

        PlayerStats.Instance.OnCharacterDeath += RefreshButtonsOnDeath;

        foreach (Button btn in targetButtons)
        {
            if (btn == null) continue;

            btn.interactable = true;

            // If there's a child button (spawned character), enable it too
            if (btn.transform.GetChild(2).childCount > 0)
            {
                Button childButton = btn.transform.GetChild(2).GetChild(0).GetComponent<Button>();
                if (childButton != null)
                    childButton.interactable = true;
            }
        }

        if (targetButtons.Count > 0 && BattleHUDController.Instance.BoardActive)
        {
            UINavigationController.Instance.JumpToElement(targetButtons[0]);
            BattleHUDController.Instance.tutorialText.text = Localizer.Instance.GetLocalizedText("Choose a target.");
        }
    }

    /// <summary>
    /// Returns the <see cref="Character"/> occupying the given board location, or null if none.
    /// </summary>
    public Character GetCharacter(string location)
    {
        if (string.IsNullOrEmpty(location))
        {
            Debug.LogWarning($"[{nameof(TargetSelectionService)}] GetCharacter called with an empty location.");
            return null;
        }

        if (Grid == null)
        {
            Debug.LogError($"[{nameof(TargetSelectionService)}] GridManager.Instance is null in GetCharacter.");
            return null;
        }

        Button btn = Grid.GetLocation(location);
        if (btn != null && btn.transform.GetChild(2).childCount > 0)
        {
            Character character = btn.transform.GetChild(2).GetChild(0).GetComponent<Character>();
            if (character != null)
                return character;
        }

        Debug.LogWarning($"[{nameof(TargetSelectionService)}] Could not find character at: {location}");
        return null;
    }

    #endregion

    #region Internal Handlers

    private void HandleSpawnSelection(Button button)
    {
        // Tile must be empty
        if (button.transform.GetChild(2).childCount > 0)
            return;

        // Record the spawn location, spawn the player there
        Grid.MyPlayerLocation = button.name;
        Spawner.SpawnPlayer(Grid.MyPlayerLocation);
    }

    private void HandleAttackSelection(Button button)
    {
        if (!HasPendingAction)
            return;

        // Use the pending action details to attack the chosen target
        AttackVariables attackVars = PendingAttackData;
        attackVars.TargetId = button.name;

        HasPendingAction = false;

        AttackResolver.ToAttack(attackVars);

        // -1 is your "item" action sentinel
        if (attackVars.ActionId == -1)
        {
            BattleHUDController.Instance.DisableItemButton();
        }

        OnlineRelay.Instance.ActionUse(attackVars);
        PlayerStats.Instance.OnCharacterDeath -= RefreshButtonsOnDeath;
    }

    private void HandleRelocateSelection(Button button)
    {
        Debug.Log($"[{nameof(TargetSelectionService)}] Moving to tile: {button.name}");

        // Move character via network
        OnlineRelay.Instance.MoveCharacterCall(Grid.MyPlayerLocation, button.name);
        Grid.MyPlayerLocation = button.name;

        // Log this as an action use (Relocate)
        AttackVariables relocateVars = new AttackVariables
        {
            AttackerId = button.name,
            ActionPointCost = 1,
            ActionName = "Relocate",
            ActionId = 0,

            TargetingMode = ActionTarget.Relocate,
            Effect = ActionEffect.Relocate,
            IsPlayerAction = true,
            IsAllyAiAction = false,
            ResolvedDamage = 0,
            MaxDamage = 0,
            MinDamage = 0,
            CritMultiplier = 1,
            CritChance = 0,
            ImpactVisual = EffectVisual.Heal,
            TargetId = button.name,
        };

        OnlineRelay.Instance.ActionUse(relocateVars);
    }

    #endregion

    #region Event Callbacks

    /// <summary>
    /// Called when a character dies. If we have a pending action, we
    /// re-run the attack resolution to refresh valid target buttons.
    /// </summary>
    private async void RefreshButtonsOnDeath(bool isAlly)
    {
        if (HasPendingAction)
        {
            Debug.Log($"[{nameof(TargetSelectionService)}] Refreshing buttons after a character death.");
            await AttackResolver.Attack(PendingAttackData);
        }
    }

    #endregion

    #region Row/Targeting Helpers

    /// <summary>
    /// Finds the front row from the given buttons. If none exist, tries the middle row, then back row.
    /// Returns only tiles that contain a living character.
    /// </summary>
    public List<Button> FindFrontRow(List<Button> buttons)
    {
        if (buttons == null) return new List<Button>();

        // Front row
        List<Button> front = FindLivingRow(buttons, 'F');
        if (front.Count > 0) return front;

        // Middle row
        List<Button> mid = FindLivingRow(buttons, 'M');
        if (mid.Count > 0) return mid;

        // Back row
        List<Button> back = FindLivingRow(buttons, 'B');
        if (back.Count > 0) return back;

        return new List<Button>();
    }

    /// <summary>
    /// Finds the back row first, then mid, then front, returning tiles that contain a living character.
    /// </summary>
    public List<Button> FindFrontRowReversed(List<Button> buttons)
    {
        if (buttons == null) return new List<Button>();

        // Back row
        List<Button> back = FindLivingRow(buttons, 'B');
        if (back.Count > 0) return back;

        // Middle row
        List<Button> mid = FindLivingRow(buttons, 'M');
        if (mid.Count > 0) return mid;

        // Front row
        List<Button> front = FindLivingRow(buttons, 'F');
        if (front.Count > 0) return front;

        return new List<Button>();
    }

    /// <summary>
    /// Returns tiles in the front and mid rows. If either row is empty,
    /// adds tiles from the back row as a fallback.
    /// </summary>
    public List<Button> FindFrontTwoRows(List<Button> buttons)
    {
        List<Button> result = new List<Button>();
        if (buttons == null) return result;

        bool missingRow = false;

        // Front row
        List<Button> front = FindLivingRow(buttons, 'F');
        if (front.Count > 0) result.AddRange(front);
        else missingRow = true;

        // Middle row
        List<Button> mid = FindLivingRow(buttons, 'M');
        if (mid.Count > 0) result.AddRange(mid);
        else missingRow = true;

        // If front or mid are missing, add back row as well
        if (missingRow)
        {
            List<Button> back = FindLivingRow(buttons, 'B');
            if (back.Count > 0) result.AddRange(back);
        }

        return result;
    }

    /// <summary>
    /// Finds all tiles in the requested row, falling back to other rows if empty.
    /// </summary>
    public List<Button> FindRow(List<Button> buttons, char rowLetter)
    {
        if (buttons == null) return new List<Button>();

        List<Button> availableButtons;

        switch (rowLetter)
        {
            case 'F':
                availableButtons = FindLivingRow(buttons, 'F');
                if (availableButtons.Count == 0) availableButtons = FindLivingRow(buttons, 'M');
                if (availableButtons.Count == 0) availableButtons = FindLivingRow(buttons, 'B');
                break;

            case 'M':
                availableButtons = FindLivingRow(buttons, 'M');
                if (availableButtons.Count == 0) availableButtons = FindLivingRow(buttons, 'B');
                if (availableButtons.Count == 0) availableButtons = FindLivingRow(buttons, 'F');
                break;

            case 'B':
                availableButtons = FindLivingRow(buttons, 'B');
                if (availableButtons.Count == 0) availableButtons = FindLivingRow(buttons, 'M');
                if (availableButtons.Count == 0) availableButtons = FindLivingRow(buttons, 'F');
                break;

            default:
                availableButtons = new List<Button>();
                break;
        }

        return availableButtons;
    }

    /// <summary>
    /// Finds all buttons without children in the specified row (F, M, or B).
    /// Used mainly for spawning.
    /// </summary>
    public List<Button> FindEmptyRow(List<Button> buttons, char rowLetter)
    {
        if (buttons == null) return new List<Button>();

        return buttons.FindAll(btn =>
            btn.transform.GetChild(2).childCount == 0 &&
            btn.name.Length > 1 &&
            btn.name[1] == rowLetter);
    }

    /// <summary>
    /// Finds all tiles that contain a living character in the front or middle row.
    /// If one of those rows is empty, also includes back-row tiles.
    /// </summary>
    public List<Button> FindFMRow(List<Button> buttons)
    {
        List<Button> result = new List<Button>();
        if (buttons == null || Grid == null) return result;

        List<Button> frontRow = Grid.FindAllAlive(buttons, 'F');
        List<Button> middleRow = Grid.FindAllAlive(buttons, 'M');

        if (frontRow.Count > 0) result.AddRange(frontRow);
        if (middleRow.Count > 0) result.AddRange(middleRow);

        // If either row is empty, include back row too
        if (middleRow.Count <= 0 || frontRow.Count <= 0)
        {
            result.AddRange(Grid.FindAllAlive(buttons, 'B'));
        }

        return result;
    }

    /// <summary>
    /// Checks how many occupied tiles are in the requested row(s).
    /// 'F' / 'M' / 'B' behave like FindRow.
    /// '2' = front + middle (plus back as fallback),
    /// '3' = all rows.
    /// Returns the count of matching occupied tiles.
    /// </summary>
    public int AnyEnemiesInRow(bool targetingEnemy, char rowLetter)
    {
        if (Grid == null) return 0;

        // If targeting enemies, we look at enemyGrid; otherwise, look at allyGrid
        List<Button> occupiedButtons = targetingEnemy
            ? Grid.enemyGrid.FindAll(btn => btn.transform.GetChild(2).childCount > 0)
            : Grid.allyGrid.FindAll(btn => btn.transform.GetChild(2).childCount > 0);

        if (occupiedButtons == null || occupiedButtons.Count == 0)
            return 0;

        List<Button> rowButtons = new List<Button>();

        switch (rowLetter)
        {
            case 'F':
                rowButtons = FilterByRowWithFallback(occupiedButtons, 'F', 'M', 'B');
                break;
            case 'M':
                rowButtons = FilterByRowWithFallback(occupiedButtons, 'M', 'B', 'F');
                break;
            case 'B':
                rowButtons = FilterByRowWithFallback(occupiedButtons, 'B', 'M', 'F');
                break;
            case '2':
                // Front + Middle together; if very low count, add back row
                rowButtons = occupiedButtons.FindAll(btn =>
                    btn.name.Length > 1 && (btn.name[1] == 'F' || btn.name[1] == 'M'));

                if (rowButtons.Count <= 1)
                {
                    rowButtons.AddRange(
                        occupiedButtons.FindAll(btn => btn.name.Length > 1 && btn.name[1] == 'B')
                    );
                }
                break;
            case '3':
                rowButtons = occupiedButtons.FindAll(btn =>
                    btn.name.Length > 1 &&
                    (btn.name[1] == 'F' || btn.name[1] == 'M' || btn.name[1] == 'B'));
                break;
            default:
                return 0;
        }

        return rowButtons.Count;
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Returns all buttons in the given row letter that contain a living Character.
    /// </summary>
    private List<Button> FindLivingRow(List<Button> buttons, char rowLetter)
    {
        if (buttons == null) return new List<Button>();

        return buttons.FindAll(btn =>
        {
            if (btn.name.Length < 2 || btn.name[1] != rowLetter) return false;
            if (btn.transform.GetChild(2).childCount == 0) return false;

            Character ch = btn.transform.GetChild(2).GetChild(0).GetComponent<Character>();
            return ch != null && !ch.Stats.IsDead;
        });
    }

    /// <summary>
    /// Filters for a primary row; if empty, falls back to two alternates in order.
    /// </summary>
    private List<Button> FilterByRowWithFallback(List<Button> buttons, char primary, char fallback1, char fallback2)
    {
        List<Button> result = buttons.FindAll(btn =>
            btn.name.Length > 1 && btn.name[1] == primary);

        if (result.Count == 0)
        {
            result = buttons.FindAll(btn =>
                btn.name.Length > 1 && btn.name[1] == fallback1);
        }

        if (result.Count == 0)
        {
            result = buttons.FindAll(btn =>
                btn.name.Length > 1 && btn.name[1] == fallback2);
        }

        return result;
    }

    #endregion
}

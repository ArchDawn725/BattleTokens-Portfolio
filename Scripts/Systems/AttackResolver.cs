using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class AttackResolver : MonoBehaviour
{
    public static AttackResolver Instance;
    private void Awake()
    {
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────
    // Tunables / Magic Numbers
    // ─────────────────────────────────────────────────────────────
    [Header("Timing")]
    [Tooltip("Delay used after cancelling a previous attack (before starting a new one).")]
    [SerializeField] private float cancelPreviousAttackDelaySeconds = 0.1f;

    [Tooltip("Delay between single-target hits (before playSpeed scaling).")]
    [SerializeField] private float shortPerTargetDelaySeconds = 0.1f;

    [Tooltip("Delay between multi-target hits (before playSpeed scaling).")]
    [SerializeField] private float longPerTargetDelaySeconds = 0.2f;

    [Header("Crit")]
    [Tooltip("Max value for crit roll Random.Range(0, critRollMax).")]
    [SerializeField] private int critRollMax = 100;

    // These reflect your grid layout: index 2 is the character container,
    // index 0 is the character object inside that container.
    private const int CharacterContainerChildIndex = 2;
    private const int CharacterObjectChildIndex = 0;

    private const string NoCallerId = "None";

    // ─────────────────────────────────────────────────────────────
    // Dependencies / State
    // ─────────────────────────────────────────────────────────────
    private CancellationTokenSource _attackCts;
    private GridManager grid => GridManager.Instance;
    private TargetSelectionService selector => TargetSelectionService.Instance;

    // ─────────────────────────────────────────────────────────────
    // Movement
    // ─────────────────────────────────────────────────────────────
    public void MoveCharacter(string startPos, string endPos)
    {
        Button oldButton = grid.GetLocation(startPos);
        Button newButton = grid.GetLocation(endPos);

        if (oldButton == null || newButton == null)
        {
            Debug.LogWarning($"[AttackResolver] MoveCharacter failed: {startPos} -> {endPos}");
            return;
        }

        Transform oldContainer = oldButton.transform.GetChild(CharacterContainerChildIndex);
        Transform newContainer = newButton.transform.GetChild(CharacterContainerChildIndex);

        if (oldContainer.childCount > 0)
        {
            oldContainer.GetChild(CharacterObjectChildIndex)
                        .SetParent(newContainer, false);
        }

        selector.GetCharacter(endPos)?.View.UpdateUI();
    }

    // ─────────────────────────────────────────────────────────────
    // Entry point for resolving an attack
    // ─────────────────────────────────────────────────────────────
    public async Task Attack(AttackVariables attackVariables)
    {
        // Cancel any in-progress attack sequence
        if (_attackCts != null)
        {
            Debug.LogWarning("Attack called while another attack is in progress, cancelling previous.");
            _attackCts.Cancel();
            _attackCts.Dispose();
            _attackCts = null;

            float cancelDelay = cancelPreviousAttackDelaySeconds / PlayerStats.Instance.playSpeed;
            await Awaitable.WaitForSecondsAsync(cancelDelay);
        }

        _attackCts = new CancellationTokenSource();

        await grid.ClearAll(_attackCts);
        float clearDelay = shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed;
        await Awaitable.WaitForSecondsAsync(clearDelay, _attackCts.Token);

        if (attackVariables.IsPlayerAction)
        {
            await HandlePlayerAttack(attackVariables);
        }
        else if (attackVariables.IsAllyAiAction)
        {
            // AI-controlled ally
            attackVariables.IsAllyAiAction = true;
            await HandleAIAttack(attackVariables);
        }
        else
        {
            // Enemy
            attackVariables.IsAllyAiAction = false;
            await HandleAIAttack(attackVariables);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Player attack routing (choose target vs auto-apply)
    // ─────────────────────────────────────────────────────────────
    private async Task HandlePlayerAttack(AttackVariables attackVariables)
    {
        selector.CurrentSelectionMode = TargetSelectionService.SelectionMode.Attacking;
        selector.PendingAttackData = attackVariables;
        selector.HasPendingAction = true;

        switch (attackVariables.TargetingMode)
        {
            // Single-target, choose from enemy grid
            case ActionTarget.Any:
                {
                    List<Button> targets = grid.enemyGrid
                        .FindAll(HasCharacter);
                    selector.EnableButtonsForUserSelection(targets);
                    break;
                }

            // Single-target, front row enemies only
            case ActionTarget.Any_Front:
                {
                    List<Button> occupied = grid.enemyGrid.FindAll(HasCharacter);
                    List<Button> frontRow = selector.FindFrontRow(occupied);
                    selector.EnableButtonsForUserSelection(frontRow);
                    break;
                }

            // Single-target, front two rows or any (InfiniteRange)
            case ActionTarget.Any_Ranged:
                {
                    var attacker = selector.GetCharacter(attackVariables.AttackerId);

                    if (attacker != null && attacker.Stats.ClassSpecial == ClassSpecial.InfiniteRange)
                    {
                        List<Button> targets = grid.enemyGrid.FindAll(HasCharacter);
                        selector.EnableButtonsForUserSelection(targets);
                    }
                    else
                    {
                        List<Button> occupied = grid.enemyGrid.FindAll(HasCharacter);
                        List<Button> frontTwo = selector.FindFrontTwoRows(occupied);
                        selector.EnableButtonsForUserSelection(frontTwo);
                    }
                    break;
                }

            // Single-target, choose from ally grid
            case ActionTarget.Any_Ally:
                {
                    List<Button> allies = grid.allyGrid
                        .FindAll(HasCharacter);
                    selector.EnableButtonsForUserSelection(allies);
                    break;
                }

            // Single-target, reverse-front (back → front)
            case ActionTarget.Any_Reverse:
                {
                    List<Button> occupied = grid.enemyGrid.FindAll(HasCharacter);
                    List<Button> reversedFront = selector.FindFrontRowReversed(occupied);
                    selector.EnableButtonsForUserSelection(reversedFront);
                    break;
                }

            // Target selection for Relocate
            case ActionTarget.Relocate:
                {
                    selector.CurrentSelectionMode = TargetSelectionService.SelectionMode.Relocating;

                    foreach (Button button in grid.allyGrid)
                    {
                        bool empty = !HasCharacter(button);
                        button.interactable = empty;
                    }

                    UINavigationController.Instance.JumpToElement(grid.allyGrid[0]);
                    if (BattleHUDController.Instance.BoardActive)
                    {
                        BattleHUDController.Instance.tutorialText.text =
                            Localizer.Instance.GetLocalizedText("Choose a target.");
                    }

                    break;
                }

            // Random targeting with JesterFix special case
            case ActionTarget.Random:
                {
                    var attacker = selector.GetCharacter(attackVariables.AttackerId);

                    if (attacker != null && attacker.Stats.ClassSpecial == ClassSpecial.JesterFix)
                    {
                        List<Button> occupied = grid.enemyGrid.FindAll(HasCharacter);
                        List<Button> frontRow = selector.FindFrontRow(occupied);
                        selector.EnableButtonsForUserSelection(frontRow);
                    }
                    else
                    {
                        // Resolve immediately as multi-target
                        UINavigationController.Instance.JumpToElement(BattleHUDController.Instance.endTurnButton);
                        await HandlePlayerMultiTargetAttack(attackVariables);
                        OnlineRelay.Instance.ActionUse(attackVariables);
                    }

                    break;
                }

            // Multi-target / self / chosen applied immediately
            default:
            case ActionTarget.All:
            case ActionTarget.All_Ally:
            case ActionTarget.All_Front_Mid:
            case ActionTarget.All_Front:
            case ActionTarget.All_Middle:
            case ActionTarget.All_Back:
            case ActionTarget.Self:
            case ActionTarget.Everyone:
            case ActionTarget.Chosen:
            case ActionTarget.All_Ally_Front:
                {
                    UINavigationController.Instance.JumpToElement(BattleHUDController.Instance.endTurnButton);
                    await HandlePlayerMultiTargetAttack(attackVariables);
                    OnlineRelay.Instance.ActionUse(attackVariables);
                    break;
                }
        }

        BattleHUDController.Instance.UpdateGameActions();
    }

    private async Task HandlePlayerMultiTargetAttack(AttackVariables attackVariables)
    {
        switch (attackVariables.TargetingMode)
        {
            case ActionTarget.All:
                await ApplyEffectToAll(grid.enemyGrid, attackVariables);
                break;

            case ActionTarget.All_Ally:
                await ApplyEffectToAll(grid.allyGrid, attackVariables);
                break;

            case ActionTarget.All_Front_Mid:
                await ApplyEffectToFM(grid.enemyGrid, attackVariables);
                break;

            case ActionTarget.All_Front:
                await ApplyEffectToRow(grid.enemyGrid, 'F', attackVariables);
                break;

            case ActionTarget.All_Middle:
                await ApplyEffectToRow(grid.enemyGrid, 'M', attackVariables);
                break;

            case ActionTarget.All_Back:
                await ApplyEffectToRow(grid.enemyGrid, 'B', attackVariables);
                break;

            case ActionTarget.Self:
                attackVariables.TargetId = attackVariables.AttackerId;
                ToAttack(attackVariables);
                await Awaitable.WaitForSecondsAsync(shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed);
                break;

            case ActionTarget.Random:
                await ApplyEffectToRandom(grid.enemyGrid, attackVariables);
                break;

            case ActionTarget.Everyone:
                if (attackVariables.AttackerId != NoCallerId)
                {
                    var attacker = selector.GetCharacter(attackVariables.AttackerId);

                    if (attacker != null && attacker.Stats.ClassSpecial == ClassSpecial.JesterFix)
                    {
                        if (attackVariables.Effect == ActionEffect.Damage)
                        {
                            await ApplyEffectToAll(grid.enemyGrid, attackVariables);
                        }
                        else if (attackVariables.Effect == ActionEffect.Heal)
                        {
                            await ApplyEffectToAll(grid.allyGrid, attackVariables);
                        }
                    }
                    else
                    {
                        await ApplyEffectToEveryone(attackVariables);
                    }
                }
                else
                {
                    await ApplyEffectToEveryone(attackVariables);
                }
                break;

            case ActionTarget.Chosen:
                ToAttack(attackVariables);
                await Awaitable.WaitForSecondsAsync(shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed);
                break;

            case ActionTarget.All_Ally_Front:
                await ApplyEffectToRow(grid.allyGrid, 'F', attackVariables);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // AI attack routing
    // ─────────────────────────────────────────────────────────────
    private async Task HandleAIAttack(AttackVariables attackVariables)
    {
        // From AI perspective:
        //  - If IsAllyAiAction: sourceGrid = enemyGrid (they are attacking enemies)
        //  - Else: sourceGrid = allyGrid (player side)
        List<Button> sourceGrid = attackVariables.IsAllyAiAction ? grid.enemyGrid : grid.allyGrid;

        switch (attackVariables.TargetingMode)
        {
            case ActionTarget.Any:
                await ApplyEffectToRandom(sourceGrid, attackVariables);
                break;

            case ActionTarget.Any_Front:
                {
                    List<Button> front = selector.FindFrontRow(sourceGrid);
                    if (front.Count > 0)
                    {
                        int idx = Random.Range(0, front.Count);
                        attackVariables.TargetId = front[idx].name;
                        ToAttack(attackVariables);
                        await Awaitable.WaitForSecondsAsync(shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed);
                    }
                    break;
                }

            case ActionTarget.Any_Reverse:
                {
                    List<Button> frontReversed = selector.FindFrontRowReversed(sourceGrid);
                    if (frontReversed.Count > 0)
                    {
                        int idx = Random.Range(0, frontReversed.Count);
                        attackVariables.TargetId = frontReversed[idx].name;
                        ToAttack(attackVariables);
                        await Awaitable.WaitForSecondsAsync(shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed);
                    }
                    break;
                }

            case ActionTarget.Any_Ranged:
                {
                    var attacker = selector.GetCharacter(attackVariables.AttackerId);

                    if (attacker != null && attacker.Stats.ClassSpecial == ClassSpecial.InfiniteRange)
                    {
                        await ApplyEffectToRandom(sourceGrid, attackVariables);
                    }
                    else
                    {
                        List<Button> frontTwo = selector.FindFrontTwoRows(sourceGrid);
                        if (frontTwo.Count > 0)
                        {
                            int idx = Random.Range(0, frontTwo.Count);
                            attackVariables.TargetId = frontTwo[idx].name;
                            ToAttack(attackVariables);
                            await Awaitable.WaitForSecondsAsync(shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed);
                        }
                    }
                    break;
                }

            case ActionTarget.Any_Ally:
                sourceGrid = attackVariables.IsAllyAiAction ? grid.allyGrid : grid.enemyGrid;
                await ApplyEffectToRandom(sourceGrid, attackVariables);
                break;

            case ActionTarget.All:
                await ApplyEffectToAll(sourceGrid, attackVariables);
                break;

            case ActionTarget.All_Ally:
                sourceGrid = attackVariables.IsAllyAiAction ? grid.allyGrid : grid.enemyGrid;
                await ApplyEffectToAll(sourceGrid, attackVariables);
                break;

            case ActionTarget.All_Front_Mid:
                await ApplyEffectToFM(sourceGrid, attackVariables);
                break;

            case ActionTarget.All_Front:
                await ApplyEffectToRow(sourceGrid, 'F', attackVariables);
                break;

            case ActionTarget.All_Middle:
                await ApplyEffectToRow(sourceGrid, 'M', attackVariables);
                break;

            case ActionTarget.All_Back:
                await ApplyEffectToRow(sourceGrid, 'B', attackVariables);
                break;

            case ActionTarget.Self:
                attackVariables.TargetId = attackVariables.AttackerId;
                ToAttack(attackVariables);
                await Awaitable.WaitForSecondsAsync(shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed);
                break;

            case ActionTarget.Random:
                {
                    List<Button> front = selector.FindFrontRow(sourceGrid);
                    if (front.Count > 0)
                    {
                        int idx = Random.Range(0, front.Count);
                        attackVariables.TargetId = front[idx].name;
                        ToAttack(attackVariables);
                        await Awaitable.WaitForSecondsAsync(shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed);
                    }
                    break;
                }

            case ActionTarget.Everyone:
                if (attackVariables.Effect == ActionEffect.Damage)
                {
                    await ApplyEffectToAll(sourceGrid, attackVariables);
                }
                else if (attackVariables.Effect == ActionEffect.Heal)
                {
                    sourceGrid = attackVariables.IsAllyAiAction ? grid.allyGrid : grid.enemyGrid;
                    await ApplyEffectToAll(sourceGrid, attackVariables);
                }
                else
                {
                    List<Button> combined = grid.allyGrid.Concat(grid.enemyGrid).ToList();
                    await ApplyEffectToAll(combined, attackVariables);
                }
                break;

            case ActionTarget.Chosen:
                ToAttack(attackVariables);
                await Awaitable.WaitForSecondsAsync(shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed);
                break;

            case ActionTarget.All_Ally_Front:
                await ApplyEffectToRow(grid.allyGrid, 'F', attackVariables);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Networking hooks
    // ─────────────────────────────────────────────────────────────
    public void ToAttack(AttackVariables attackVariables)
    {
        if (attackVariables.ResolvedDamage == 0)
        {
            // Roll damage
            attackVariables.ResolvedDamage =
                Random.Range(attackVariables.MinDamage, attackVariables.MaxDamage + 1);

            // Ensure default crit multiplier is 1
            if (attackVariables.CritMultiplier == 0)
            {
                attackVariables.CritMultiplier = 1;
            }

            // Crit roll
            if (Random.Range(0, critRollMax) > attackVariables.CritChance)
            {
                // No crit
                attackVariables.CritMultiplier = 1;
            }

            attackVariables.ResolvedDamage =
                Mathf.CeilToInt(attackVariables.ResolvedDamage * attackVariables.CritMultiplier);
        }

        OnlineRelay.Instance.AttackCall(attackVariables);
    }

    public void ActionUseRelay(AttackVariables attackVariables)
    {
        Debug.Log("Player used action: " + attackVariables.AttackerId);
        Button callerButton = grid.GetLocation(attackVariables.AttackerId);

        if (callerButton == null)
        {
            Debug.LogError("[AttackResolver] No caller");
            return;
        }

        Character callerChar = GetCharacterFromButton(callerButton);
        callerChar?.ActionUse(attackVariables);
    }

    public void AttackCall(AttackVariables attackVariables)
    {
        Button callerButton = null;
        Character callerChar = null;

        if (attackVariables.AttackerId != NoCallerId)
        {
            callerButton = grid.GetLocation(attackVariables.AttackerId);
            if (callerButton == null)
            {
                Debug.LogError("[AttackResolver] No caller");
                return;
            }
            callerChar = GetCharacterFromButton(callerButton);
        }

        Button targetButton = grid.GetLocation(attackVariables.TargetId);

        if (targetButton == null)
        {
            Debug.LogWarning("[AttackResolver] No target");
            if (callerChar != null)
            {
                callerChar.View.AttackAni(attackVariables);
            }
            return;
        }

        if (!HasCharacter(targetButton)) return;

        Character targetChar = GetCharacterFromButton(targetButton);

        // Play attack animation on caller
        if (callerChar != null)
        {
            // This computed animDamage isn't currently used, but kept for potential VFX scaling
            int animDamage = attackVariables.ResolvedDamage - targetChar.Stats.GetTotalDefence();
            callerChar.View.AttackAni(attackVariables);
        }

        // Apply the hit
        targetChar?.Combat.Hit(attackVariables);
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private static bool HasCharacter(Button btn)
    {
        return btn.transform.GetChild(CharacterContainerChildIndex).childCount > 0;
    }

    private static Character GetCharacterFromButton(Button btn)
    {
        if (!HasCharacter(btn)) return null;

        return btn.transform
                  .GetChild(CharacterContainerChildIndex)
                  .GetChild(CharacterObjectChildIndex)
                  .GetComponent<Character>();
    }

    private async Task ApplyEffectToAll(List<Button> buttonList, AttackVariables attackVariables)
    {
        List<Button> targets = buttonList.Where(HasCharacter).ToList();

        float delay = longPerTargetDelaySeconds / PlayerStats.Instance.playSpeed;

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            attackVariables.TargetId = targets[i].name;
            ToAttack(attackVariables);
            await Awaitable.WaitForSecondsAsync(delay);
        }
    }

    private async Task ApplyEffectToRandom(List<Button> buttonList, AttackVariables attackVariables)
    {
        float delay = shortPerTargetDelaySeconds / PlayerStats.Instance.playSpeed;

        if (attackVariables.Effect == ActionEffect.Heal)
        {
            List<Character> healTargets = ChooseHealTargets(buttonList);

            if (healTargets.Count > 0)
            {
                int index = Random.Range(0, healTargets.Count);
                attackVariables.TargetId = healTargets[index].transform.parent.parent.name;
                Debug.Log("Found heal target: " + attackVariables.TargetId);
                ToAttack(attackVariables);
                await Awaitable.WaitForSecondsAsync(delay);
            }
        }
        else if (attackVariables.Effect == ActionEffect.Protect)
        {
            List<Button> targets = buttonList.FindAll(btn =>
            {
                if (!HasCharacter(btn)) return false;
                if (btn.transform.name == attackVariables.AttackerId) return false;

                Character c = GetCharacterFromButton(btn);
                return c != null && !c.Stats.IsDead;
            });

            if (targets.Count > 0)
            {
                int index = Random.Range(0, targets.Count);
                attackVariables.TargetId = targets[index].name;
                ToAttack(attackVariables);
                await Awaitable.WaitForSecondsAsync(delay);
            }
        }
        else
        {
            List<Button> targets = buttonList.FindAll(btn =>
            {
                if (!HasCharacter(btn)) return false;

                Character c = GetCharacterFromButton(btn);
                return c != null && !c.Stats.IsDead;
            });

            if (targets.Count > 0)
            {
                int index = Random.Range(0, targets.Count);
                attackVariables.TargetId = targets[index].name;
                ToAttack(attackVariables);
                await Awaitable.WaitForSecondsAsync(delay);
            }
        }
    }

    private List<Character> ChooseHealTargets(List<Button> buttonList)
    {
        List<Button> targets = buttonList.Where(HasCharacter).ToList();
        List<Character> possibleTargets = new List<Character>();

        foreach (Button target in targets)
        {
            Character c = GetCharacterFromButton(target);
            if (c == null) continue;

            if (c.Stats.Health < c.Stats.MaxHealth && !c.Stats.IsDead)
            {
                possibleTargets.Add(c);
            }
        }

        return possibleTargets;
    }

    private async Task ApplyEffectToRow(List<Button> buttonList, char rowChar, AttackVariables attackVariables)
    {
        List<Button> occupied = buttonList.Where(HasCharacter).ToList();
        List<Button> row = selector.FindRow(occupied, rowChar);

        if (row == null) return;

        float delay = longPerTargetDelaySeconds / PlayerStats.Instance.playSpeed;

        for (int i = row.Count - 1; i >= 0; i--)
        {
            attackVariables.TargetId = row[i].name;
            ToAttack(attackVariables);
            await Awaitable.WaitForSecondsAsync(delay);
        }
    }

    private async Task ApplyEffectToFM(List<Button> buttonList, AttackVariables attackVariables)
    {
        List<Button> occupied = buttonList.Where(HasCharacter).ToList();
        List<Button> rows = selector.FindFMRow(occupied);

        if (rows == null) return;

        float delay = longPerTargetDelaySeconds / PlayerStats.Instance.playSpeed;

        for (int i = rows.Count - 1; i >= 0; i--)
        {
            attackVariables.TargetId = rows[i].name;
            ToAttack(attackVariables);
            await Awaitable.WaitForSecondsAsync(delay);
        }
    }

    private async Task ApplyEffectToEveryone(AttackVariables attackVariables)
    {
        List<Button> allTargets = grid.enemyGrid.Where(HasCharacter)
            .Concat(grid.allyGrid.Where(HasCharacter))
            .ToList();

        float delay = longPerTargetDelaySeconds / PlayerStats.Instance.playSpeed;

        for (int i = allTargets.Count - 1; i >= 0; i--)
        {
            attackVariables.TargetId = allTargets[i].name;
            ToAttack(attackVariables);
            await Awaitable.WaitForSecondsAsync(delay);
        }
    }
}

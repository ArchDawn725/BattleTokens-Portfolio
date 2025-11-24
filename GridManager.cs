using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class GridManager : MonoBehaviour
{
    #region SINGLETON

    public static GridManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region FIELDS_AND_PROPERTIES

    [Header("Player")]
    public string MyPlayerLocation;

    [Header("Grids")]
    [SerializeField] public List<Button> allyGrid;
    [SerializeField] public List<Button> enemyGrid;
    [SerializeField] private List<Button> grid;

    private TargetSelectionService Selector => TargetSelectionService.Instance;
    private EnemyController EnemyCon => EnemyController.Instance;

    #endregion

    #region CLEARING

    /// <summary>
    /// Disables interaction on all grid buttons and clears their selection state.
    /// Kept as Task to support existing async usages (e.g. await ClearAll()).
    /// </summary>
    public Task ClearAll(CancellationTokenSource ct = null)
    {
        InternalClearAll();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Synchronous implementation used by both ClearAll() and ClearBoard().
    /// </summary>
    private void InternalClearAll()
    {
        foreach (Button button in grid)
        {
            if (button == null) continue;

            button.interactable = false;

            if (button.TryGetComponent(out BorderOnSelect border))
            {
                border.OnDeselect(null);
            }

            Transform slotRoot = button.transform.GetChild(2);
            if (slotRoot.childCount > 0)
            {
                Button innerButton = slotRoot.GetChild(0).GetComponent<Button>();
                if (innerButton != null)
                {
                    innerButton.interactable = false;
                }
            }
        }
    }

    /// <summary>
    /// Destroys all ally characters from the board and clears all grid interactions.
    /// </summary>
    public void ClearBoard()
    {
        List<Button> usedAllyButtons = allyGrid.FindAll(b => b.transform.GetChild(2).childCount > 0);

        foreach (Button button in usedAllyButtons)
        {
            Transform slotRoot = button.transform.GetChild(2);

            for (int i = slotRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(slotRoot.GetChild(i).gameObject);
            }
        }

        InternalClearAll();
        EnemyCon.aiAllies.Clear();
    }

    #endregion

    #region SPAWNING

    public bool IsMyCharacterAlive()
    {
        return Selector.GetCharacter(MyPlayerLocation) != null;
    }

    /// <summary>
    /// Picks a random valid enemy slot name for a given enemy class.
    /// </summary>
    public string RandomEnemyLocation(EnemyClass eClass)
    {
        List<Button> availableSpots = enemyGrid
            .FindAll(btn => btn.transform.GetChild(2).childCount == 0);

        if (availableSpots.Count == 0)
        {
            Debug.LogWarning("[GridManager] No free spots available for new spawns!");
            return null;
        }

        Button spawnLocation = ChooseEnemySpawnLocation(availableSpots, eClass);
        return spawnLocation != null ? spawnLocation.name : null;
    }

    /// <summary>
    /// Chooses a spawn location from free spots based on the enemy's class (front/mid/back preference).
    /// </summary>
    public Button ChooseEnemySpawnLocation(List<Button> freeSpots, EnemyClass eClass)
    {
        List<Button> availableSpots;

        switch (eClass)
        {
            default:
            case EnemyClass.Melee_DPS:
            case EnemyClass.Tank:
                // Prefer front row; fall back to middle, then back.
                availableSpots = Selector.FindEmptyRow(freeSpots, 'F');
                if (availableSpots.Count == 0) availableSpots = Selector.FindEmptyRow(freeSpots, 'M');
                if (availableSpots.Count == 0) availableSpots = Selector.FindEmptyRow(freeSpots, 'B');
                break;

            case EnemyClass.Ranged_DPS:
            case EnemyClass.Assassin:
                // Prefer middle; fall back to back, then front.
                availableSpots = Selector.FindEmptyRow(freeSpots, 'M');
                if (availableSpots.Count == 0) availableSpots = Selector.FindEmptyRow(freeSpots, 'B');
                if (availableSpots.Count == 0) availableSpots = Selector.FindEmptyRow(freeSpots, 'F');
                break;

            case EnemyClass.AOE_DPS:
            case EnemyClass.Support:
            case EnemyClass.Boss:
                // Prefer back row; fall back to middle, then front.
                availableSpots = Selector.FindEmptyRow(freeSpots, 'B');
                if (availableSpots.Count == 0) availableSpots = Selector.FindEmptyRow(freeSpots, 'M');
                if (availableSpots.Count == 0) availableSpots = Selector.FindEmptyRow(freeSpots, 'F');
                break;

            case EnemyClass.Random:
                availableSpots = freeSpots;
                break;
        }

        if (availableSpots == null || availableSpots.Count == 0)
            return null;

        int randomIndex = Random.Range(0, availableSpots.Count);
        return availableSpots[randomIndex];
    }

    #endregion

    #region QUERY_HELPERS

    public Button GetLocation(string location)
    {
        return grid.FirstOrDefault(b => b != null && b.name == location);
    }

    /// <summary>
    /// Finds all alive characters in a given row for a set of grid buttons.
    /// </summary>
    public List<Button> FindAllAlive(List<Button> buttons, char rowLetter)
    {
        return buttons.FindAll(btn =>
        {
            if (btn == null) return false;

            // Must be in the specified row (e.g., 'F', 'M', 'B')
            if (btn.name.Length < 2 || btn.name[1] != rowLetter)
                return false;

            Transform slotRoot = btn.transform.GetChild(2);
            if (slotRoot.childCount == 0)
                return false;

            Character ch = slotRoot.GetChild(0).GetComponent<Character>();
            return ch != null && !ch.Stats.IsDead;
        });
    }

    #endregion
}

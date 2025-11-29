using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using static TargetSelectionService;
using Random = UnityEngine.Random;

public class SpawnManager : MonoBehaviour
{
    #region Singleton

    public static SpawnManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(SpawnManager)}] Duplicate instance detected, destroying this one.", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    #endregion

    #region Inspector Fields

    [Header("AI Allies")]
    [Tooltip("EnemySO entries that can be used as AI allies (friendly units controlled by AI).")]
    public EnemySO[] availableAIAllies;

    [Header("Enemy Configuration")]
    [Tooltip("All enemy ScriptableObjects that can be spawned (including bosses and specials).")]
    [SerializeField] private List<EnemySO> enemySOs = new List<EnemySO>();

    [Header("Spawnable Character Prefab")]
    [Tooltip("The character prefab to spawn into grid slots.")]
    [SerializeField] private GameObject character;

    [Header("AI Setup")]
    [Tooltip("Number of AI allies that should be present for this quest/battle.")]
    public int AIAlliesCount;
    [Tooltip("Index array referencing availableAIAllies for each AI slot. -2 = unused, -1 = random, >=0 = explicit index.")]
    public int[] aiClasses;

    #endregion

    #region State & Events

    /// <summary>
    /// Fired when all scheduled spawns for this wave are complete and the player turn can begin.
    /// </summary>
    public event Action OnDoneSpawning;

    [Tooltip("True once all enemies/allies for the current wave have finished spawning.")]
    public bool doneSpawning;

    private int spawnedCount;
    private int requiredSpawnCount;

    private EnemyController enemyCon => EnemyController.Instance;
    private GridManager grid => GridManager.Instance;

    #endregion

    #region Spawn Amount Calculation

    /// <summary>
    /// Calculates how many "population units" to spawn for the current wave.
    /// </summary>
    private float CalculateSpawnAmount(float multiplier)
    {
        float waveModifier = (enemyCon.wave - 1) / 10f;           // Increase spawn amount every 10 waves
        float questModifier = enemyCon.Quest.Modifier / 10f;      // Increase based on difficulty

        // Include AI allies in the player count
        float players = enemyCon.allies.Count + AIAlliesCount;

        // Some quest specials add extra pseudo-players
        if (enemyCon.Quest.Special != QuestSpecial.None) { players++; }
        if (enemyCon.Quest.Special == QuestSpecial.TwoSoldiers) { players++; }

        if (players > 9) { players = 9; }

        return (players + waveModifier + questModifier) * multiplier;
    }

    #endregion

    #region Wave & Enemy Spawning

    /// <summary>
    /// Main coroutine that advances the wave, spawns enemies for that wave,
    /// then spawns AI allies/special allies, and finally hands control to the players.
    /// Only the host (or offline) actually drives the spawn logic.
    /// </summary>
    public IEnumerator SpawnEnemies()
    {
        enemyCon.wave++;

        // If we've exceeded quest waves, the player wins.
        if (enemyCon.wave > enemyCon.Quest.waves.Count)
        {
            UIController.Instance.GameOver(enemyCon.wave - 1, true, false);
            yield break;
        }

        // Only host or offline should spawn
        if (NetworkManager.Singleton == null ||
            (!NetworkManager.Singleton.IsHost && OnlineRelay.Instance.IsConnected()))
        {
            yield break;
        }

        spawnedCount = 0;
        requiredSpawnCount = 1;     // used as a baseline for Spawned() accounting
        doneSpawning = false;

        float spawnModifier = CalculateSpawnAmount(1f);

        var currentWave = enemyCon.Quest.waves[enemyCon.wave - 1];
        List<EnemySO> spawnableEnemies = currentWave.possibleEnemies;
        List<EnemySO> chosenEnemies = new List<EnemySO>();

        if (!currentWave.Boss)
        {
            // Non-boss wave: keep adding random enemies until we reach the spawnModifier "population"
            float population = 0f;
            while (population < spawnModifier && spawnableEnemies.Count > 0)
            {
                int idx = Random.Range(0, spawnableEnemies.Count);
                EnemySO chosen = spawnableEnemies[idx];
                chosenEnemies.Add(chosen);
                population += chosen.PopulationCount + 0.25f;
            }
        }
        else
        {
            // Boss wave: pick exactly one from the list
            if (spawnableEnemies.Count > 0)
            {
                int idx = Random.Range(0, spawnableEnemies.Count);
                chosenEnemies.Add(spawnableEnemies[idx]);
            }
            else
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] Boss wave has no possibleEnemies configured.");
            }
        }

        requiredSpawnCount += chosenEnemies.Count;

        for (int i = chosenEnemies.Count - 1; i >= 0; i--)
        {
            // Find empty enemy grid slots
            List<Button> availableSpots = grid.enemyGrid.FindAll(btn => btn.transform.GetChild(2).childCount == 0);
            if (availableSpots.Count == 0)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] No empty spots available for enemies.");
                Spawned();
                continue;
            }

            Button spawnLocation = grid.ChooseEnemySpawnLocation(availableSpots, chosenEnemies[i].EnemyClass);
            if (spawnLocation == null)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] No valid spawn location returned for enemy '{chosenEnemies[i].name}'.");
                Spawned();
                continue;
            }

            int enemyIndex = enemySOs.IndexOf(chosenEnemies[i]);
            if (enemyIndex < 0)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] EnemySO '{chosenEnemies[i].name}' not found in enemySOs list.");
                Spawned();
                continue;
            }

            // Points for boss vs non-boss
            int points;
            if (enemySOs[enemyIndex].Boss)
            {
                points = CalculateEnemyPoints() * (enemyCon.allies.Count + enemyCon.aiAllies.Count);
            }
            else
            {
                points = CalculateEnemyPoints();
            }

            SpawnEnemyCall(spawnLocation.name, enemyIndex, false, points);

            yield return new WaitForSeconds(0.15f / PlayerStats.Instance.playSpeed);
        }

        // After normal enemies, spawn AI allies
        StartCoroutine(SpawnAIAllies());
    }

    /// <summary>
    /// Spawns AI allies based on aiClasses configuration.
    /// Handles random classes (-1) and applies a failsafe if counts mismatch.
    /// </summary>
    private IEnumerator SpawnAIAllies()
    {
        List<EnemySO> chosenAIAllies = new List<EnemySO>();

        // Validate that aiClasses has the expected number of entries
        int configuredClassCount = 0;
        for (int i = 0; i < aiClasses.Length; i++)
        {
            if (aiClasses[i] > -2) { configuredClassCount++; }
        }

        if (configuredClassCount != AIAlliesCount)
        {
            ApplyAiClassesFailsafe();
            yield return new WaitForSeconds(0.5f);
        }

        for (int i = 0; i < aiClasses.Length; i++)
        {
            if (aiClasses[i] > -2)
            {
                int value = aiClasses[i];
                if (value == -1)
                {
                    if (availableAIAllies == null || availableAIAllies.Length == 0)
                    {
                        Debug.LogWarning($"[{nameof(SpawnManager)}] No available AI allies configured, cannot assign random AI class.");
                        continue;
                    }

                    value = Random.Range(0, availableAIAllies.Length);
                    aiClasses[i] = value;
                }

                if (value < 0 || value >= availableAIAllies.Length)
                {
                    Debug.LogWarning($"[{nameof(SpawnManager)}] AI class index {value} is out of range.");
                    continue;
                }

                chosenAIAllies.Add(availableAIAllies[value]);
            }
        }

        requiredSpawnCount += chosenAIAllies.Count;

        List<Button> availableSpots = grid.allyGrid.FindAll(btn => btn.transform.GetChild(2).childCount == 0);

        for (int i = chosenAIAllies.Count - 1; i >= 0; i--)
        {
            if (availableSpots.Count == 0)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] No empty spots available for AI allies.");
                Spawned();
                continue;
            }

            Button spawnLocation = grid.ChooseEnemySpawnLocation(availableSpots, chosenAIAllies[i].EnemyClass);
            if (spawnLocation == null)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] No valid spawn location returned for AI ally '{chosenAIAllies[i].name}'.");
                Spawned();
                continue;
            }

            int enemyIndex = enemySOs.IndexOf(chosenAIAllies[i]);
            if (enemyIndex < 0)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] AI ally EnemySO '{chosenAIAllies[i].name}' not found in enemySOs list.");
                Spawned();
                continue;
            }

            SpawnEnemyCall(spawnLocation.name, enemyIndex, true, CalculateEnemyPoints());

            yield return new WaitForSeconds(0.15f / PlayerStats.Instance.playSpeed);
        }

        StartCoroutine(SpawnSpecialAIAllies());
    }

    /// <summary>
    /// Spawns special AI allies based on QuestSpecial (Carriage, Soldier, TwoSoldiers).
    /// </summary>
    private IEnumerator SpawnSpecialAIAllies()
    {
        List<EnemySO> chosenAIAllies = new List<EnemySO>();

        switch (enemyCon.Quest.Special)
        {
            case QuestSpecial.None:
                break;
            case QuestSpecial.Carriage:
                if (enemySOs.Count > 109) chosenAIAllies.Add(enemySOs[109]);
                else Debug.LogWarning($"[{nameof(SpawnManager)}] Carriage index 109 is out of range in enemySOs.");
                break;
            case QuestSpecial.Soldier:
                if (enemySOs.Count > 110) chosenAIAllies.Add(enemySOs[110]);
                else Debug.LogWarning($"[{nameof(SpawnManager)}] Soldier index 110 is out of range in enemySOs.");
                break;
            case QuestSpecial.TwoSoldiers:
                if (enemySOs.Count > 110)
                {
                    chosenAIAllies.Add(enemySOs[110]);
                    chosenAIAllies.Add(enemySOs[110]);
                }
                else Debug.LogWarning($"[{nameof(SpawnManager)}] Soldier index 110 is out of range in enemySOs.");
                break;
        }

        requiredSpawnCount += chosenAIAllies.Count;

        if (chosenAIAllies.Count > 0)
        {
            List<Button> availableSpots = grid.allyGrid.FindAll(btn => btn.transform.GetChild(2).childCount == 0);

            for (int i = chosenAIAllies.Count - 1; i >= 0; i--)
            {
                if (availableSpots.Count == 0)
                {
                    Debug.LogWarning($"[{nameof(SpawnManager)}] No empty spots available for special AI allies.");
                    Spawned();
                    continue;
                }

                Button spawnLocation = grid.ChooseEnemySpawnLocation(availableSpots, chosenAIAllies[i].EnemyClass);
                if (spawnLocation == null)
                {
                    Debug.LogWarning($"[{nameof(SpawnManager)}] No valid spawn location returned for special AI ally '{chosenAIAllies[i].name}'.");
                    Spawned();
                    continue;
                }

                int enemyIndex = enemySOs.IndexOf(chosenAIAllies[i]);
                if (enemyIndex < 0)
                {
                    Debug.LogWarning($"[{nameof(SpawnManager)}] Special AI EnemySO '{chosenAIAllies[i].name}' not found in enemySOs list.");
                    Spawned();
                    continue;
                }

                SpawnEnemyCall(spawnLocation.name, enemyIndex, true, CalculateEnemyPoints());

                yield return new WaitForSeconds(0.15f / PlayerStats.Instance.playSpeed);
            }
        }

        // If this is not a boss wave, mark spawning complete once all calls have been made.
        if (!enemyCon.Quest.waves[enemyCon.wave - 1].Boss)
        {
            Spawned();
        }
    }

    /// <summary>
    /// Must be called once per successfully spawned unit.
    /// When the 'spawnedCount' reaches 'requiredSpawnCount', the player turn is started.
    /// </summary>
    public void Spawned()
    {
        spawnedCount++;
        Debug.Log($"[{nameof(SpawnManager)}] Spawned: {spawnedCount} / {requiredSpawnCount}");

        if (spawnedCount >= requiredSpawnCount && !doneSpawning)
        {
            doneSpawning = true;

            OnlineRelay.Instance.PlayerTurnCall();
            BattleHUDController.Instance.UpdateGameActions();
            OnDoneSpawning?.Invoke();
        }
    }

    #endregion

    #region Action-based Spawning

    /// <summary>
    /// Spawns additional enemies via an in-combat action (summon etc.)
    /// directly on the enemy grid.
    /// </summary>
    public async Task SpawnEnemy(EnemySO[] spawnables, int points, float amount)
    {
        List<EnemySO> spawnable = spawnables.ToList();
        List<EnemySO> chosen = new List<EnemySO>();
        float population = 0f;

        if (amount == 0)
        {
            amount = CalculateSpawnAmount(0.3f);
        }

        if (points > enemyCon.Quest.Modifier && enemyCon.Quest.difficultyLevel < 1001)
        {
            points = (int)enemyCon.Quest.Modifier;
        }

        // Choose enemies until we reach the desired "amount" population
        while (population < amount && spawnable.Count > 0)
        {
            int num = Random.Range(0, spawnable.Count);
            chosen.Add(spawnable[num]);
            population += spawnable[num].PopulationCount + 0.25f;

            await Awaitable.WaitForSecondsAsync(0.1f / PlayerStats.Instance.playSpeed);
        }

        requiredSpawnCount += chosen.Count;

        for (int i = chosen.Count - 1; i >= 0; i--)
        {
            // Place them in empty slots on enemy grid
            List<Button> availableSpots = grid.enemyGrid.FindAll(btn => btn.transform.GetChild(2).childCount == 0);

            if (availableSpots.Count == 0)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] No free spots available for additional spawned enemies.");
                break;
            }

            Button spawnLocation = grid.ChooseEnemySpawnLocation(availableSpots, chosen[i].EnemyClass);
            if (spawnLocation == null)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] No valid spawn location returned for additional spawn '{chosen[i].name}'.");
                continue;
            }

            int enemyIndex = enemySOs.IndexOf(chosen[i]);
            if (enemyIndex < 0)
            {
                Debug.LogWarning($"[{nameof(SpawnManager)}] Additional spawn EnemySO '{chosen[i].name}' not found in enemySOs list.");
                continue;
            }

            SpawnEnemyCall(spawnLocation.name, enemyIndex, false, points);

            await Awaitable.WaitForSecondsAsync(0.1f / PlayerStats.Instance.playSpeed);
        }

        Spawned();
    }

    #endregion

    #region Player Spawning

    /// <summary>
    /// Chooses a random valid ally-grid square and spawns the local player there.
    /// Used when the player fails to choose in time.
    /// </summary>
    public void SpawnMyPlayerOnRandomSquare()
    {
        List<Button> availableSpots = grid.allyGrid.FindAll(btn => btn.transform.GetChild(2).childCount == 0);
        if (availableSpots.Count == 0)
        {
            Debug.LogWarning($"[{nameof(SpawnManager)}] No empty ally spots available to spawn player.");
            return;
        }

        Button randomSpawn = grid.ChooseEnemySpawnLocation(availableSpots, EnemyClass.Random);
        if (randomSpawn == null)
        {
            Debug.LogWarning($"[{nameof(SpawnManager)}] Random spawn location could not be chosen for player.");
            return;
        }

        grid.MyPlayerLocation = randomSpawn.name;
        SpawnPlayer(randomSpawn.name);

        // Clear all interactable buttons and reset choice
        grid.ClearAll();
        TargetSelectionService.Instance.CurrentSelectionMode = SelectionMode.None;
    }

    /// <summary>
    /// Sends a request to spawn the local player at the given grid location.
    /// </summary>
    public void SpawnPlayer(string location)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError($"[{nameof(SpawnManager)}] NetworkManager is null in {nameof(SpawnPlayer)}.");
            return;
        }

        OnlineRelay.Instance.SpawnPlayerCall(location, NetworkManager.Singleton.LocalClientId);
        Invoke(nameof(SpawnDelay), 0.5f);
    }

    private void SpawnDelay()
    {
        UIController.Instance.EndTurn(true);
    }

    /// <summary>
    /// Actually instantiates the player object when the network relay confirms spawn.
    /// </summary>
    public void SpawnPlayerCalled(string location, ulong localClientId)
    {
        var button = grid.GetLocation(location);
        if (button == null)
        {
            Debug.LogWarning($"[{nameof(SpawnManager)}] SpawnPlayerCalled: grid location '{location}' not found.");
            return;
        }

        button.interactable = false;
        GameObject playerObj = SpawnCharacter(location);
        if (playerObj == null) return;

        enemyCon.allies.Add(playerObj);

        PlayerCharacter pc = playerObj.AddComponent<PlayerCharacter>();
        pc.Setup(localClientId);
    }

    #endregion

    #region Enemy Spawning (Network Calls)

    /// <summary>
    /// Wrapper that asks the OnlineRelay to spawn an enemy/AI ally,
    /// but only if location is valid and (if online) we are the host.
    /// </summary>
    public void SpawnEnemyCall(string location, int enemyIndex, bool isAIAlly, int points)
    {
        if (string.IsNullOrEmpty(location)) return;

        if (!OnlineRelay.Instance.IsConnected() ||
            (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost))
        {
            OnlineRelay.Instance.SpawnCall(location, enemyIndex, isAIAlly, points);
        }
    }

    /// <summary>
    /// Called by OnlineRelay to actually create the enemy in the scene.
    /// </summary>
    public void SpawnEnemyCalled(string location, int enemyIndex, bool isAIAlly, int points)
    {
        GameObject newEnemy = SpawnCharacter(location);
        if (newEnemy == null) return;

        if (isAIAlly)
            enemyCon.aiAllies.Add(newEnemy);
        else
            enemyCon.enemies.Add(newEnemy);

        EnemyCharacter newChar = newEnemy.AddComponent<EnemyCharacter>();
        newChar.SetUp(enemySOs[enemyIndex], points, isAIAlly);
    }

    #endregion

    #region Point Calculations

    /// <summary>
    /// Calculates enemy upgrade points based on completed waves and quest modifiers.
    /// </summary>
    public int CalculateEnemyPoints()
    {
        int lastCompletedWave = enemyCon.wave - 1;
        int cumulativePoints = 0;

        // Sum ceil(i * pointsModifier) for each finished wave
        for (int i = 1; i <= lastCompletedWave; i++)
        {
            cumulativePoints += Mathf.CeilToInt(i * enemyCon.Quest.pointsModifier);
        }

        Debug.Log($"[{nameof(SpawnManager)}] Cumulative points: {cumulativePoints}");

        int waveModifier = enemyCon.wave / 5;
        int questModifier = (int)enemyCon.Quest.Modifier;

        int totalPoints = cumulativePoints + waveModifier + questModifier;

        Debug.Log($"[{nameof(SpawnManager)}] Total points: {totalPoints}");
        return totalPoints;
    }

    /// <summary>
    /// Calculates points for enemies spawned by a boss effect (offset by five waves).
    /// </summary>
    public int CalculateBossSpawnEnemyPoints()
    {
        int newWave = enemyCon.wave - 5;
        if (newWave <= 0) { newWave = 1; }

        int lastCompletedWave = newWave - 1;

        int waveModifier = newWave / 5;
        int triangularPoints = (lastCompletedWave * (lastCompletedWave - 1)) / 2;
        int totalPoints = triangularPoints + waveModifier + lastCompletedWave + (int)enemyCon.Quest.Modifier;

        Debug.Log($"[{nameof(SpawnManager)}] BossSpawn - lastCompletedWave: {lastCompletedWave}, waveModifier: {waveModifier}, triangularPoints: {triangularPoints}, totalPoints: {totalPoints}");

        return totalPoints;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Spawns the character prefab under the correct grid cell.
    /// </summary>
    private GameObject SpawnCharacter(string location)
    {
        Button gridButton = grid.GetLocation(location);
        if (gridButton == null)
        {
            Debug.LogWarning($"[{nameof(SpawnManager)}] Grid location '{location}' not found.");
            return null;
        }

        if (character == null)
        {
            Debug.LogError($"[{nameof(SpawnManager)}] Character prefab is not assigned.");
            return null;
        }

        return Instantiate(character, gridButton.transform.GetChild(2));
    }

    /// <summary>
    /// Failsafe for AI class configuration mismatches between expected AI allies and aiClasses.
    /// </summary>
    private void ApplyAiClassesFailsafe()
    {
        Debug.LogWarning($"[{nameof(SpawnManager)}] AI class count mismatch. Applying failsafe.");

        if (OnlineRelay.Instance.IsConnected())
        {
            if (LobbyUI.Instance != null)
                LobbyUI.Instance.EnemyAiClassFailsafe();
            else
                Debug.LogWarning($"[{nameof(SpawnManager)}] LobbyUI.Instance is null while applying AI class failsafe.");
        }
        else
        {
            if (OfflineLobbyUI.Instance != null)
                OfflineLobbyUI.Instance.EnemyAiClassFailsafe();
            else
                Debug.LogWarning($"[{nameof(SpawnManager)}] OfflineLobbyUI.Instance is null while applying AI class failsafe.");
        }
    }

    /// <summary>
    /// Enables player spawn selection on the ally grid and disables other HUD controls.
    /// </summary>
    public void ChooseMySpawn()
    {
        requiredSpawnCount = 100; // High value so normal Spawned() logic doesn't complete early
        enemyCon.turn = 0;
        enemyCon.gameEnded = false;

        TargetSelectionService.Instance.CurrentSelectionMode = SelectionMode.ChoosingSpawn;

        // Only enable ally grid buttons that are empty
        foreach (Button button in grid.allyGrid)
        {
            button.interactable = (button.transform.GetChild(2).childCount == 0);
        }

        // Disable all other UI interactions
        BattleHUDController.Instance.SetAllNonInteractable();

        if (grid.allyGrid.Count > 0)
        {
            UINavigationController.Instance.JumpToElement(grid.allyGrid[0]);
        }
        else
        {
            Debug.LogWarning($"[{nameof(SpawnManager)}] Ally grid is empty when calling {nameof(ChooseMySpawn)}.");
        }
    }

    #endregion
}

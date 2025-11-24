using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    #region Singleton

    public static EnemyController Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(EnemyController)}] Multiple instances detected. Keeping the first one.");
            return;
        }

        Instance = this;
    }

    #endregion

    #region Inspector

    [Header("Turn / Wave Settings")]
    [Tooltip("Turn threshold before OverTimer is activated in non-boss waves.")]
    [SerializeField] private int nonBossOverTimeThreshold = 10;

    [Tooltip("Turn threshold before OverTimer is activated in boss waves.")]
    [SerializeField] private int bossOverTimeThreshold = 20;

    [Tooltip("Delay in seconds before checking game status after changes.")]
    [SerializeField] private float statusCheckDelay = 0.1f;

    [Header("References")]
    [SerializeField] private PlayerReadyManager playerReadyManager;
    [SerializeField] private OnlineRelay onlineRelay;

    #endregion

    #region State

    public int turn;
    public int wave;      // 1-based index for current wave
    public QuestSO Quest;
    public bool enemyTurn;
    public bool gameEnded;

    public List<GameObject> enemies = new List<GameObject>();
    public List<GameObject> allies = new List<GameObject>();
    public List<GameObject> aiAllies = new List<GameObject>();

    #endregion

    #region Turn Flow

    public async Task<bool> PlayerTurn()
    {
        enemyTurn = false;
        turn++;

        Debug.Log("New turn call");

        // All non-player-controlled characters (enemies + AI allies)
        List<GameObject> currentCharacters = enemies.Concat(aiAllies).ToList();

        foreach (GameObject obj in currentCharacters)
        {
            if (obj == null) continue;

            Character ch = obj.GetComponent<Character>();
            if (ch != null)
            {
                ch.NextTurn();
            }
        }

        // Player allies
        if (allies.Count > 0)
        {
            foreach (GameObject ally in allies)
            {
                if (ally == null) continue;

                PlayerCharacter pc = ally.GetComponent<PlayerCharacter>();
                if (pc != null)
                {
                    pc.OnNewTurn();
                }
            }
        }

        // OverTimer (defence debuff) after long fights
        if (TryGetWaveIsBoss(wave - 1, out bool isBossWave))
        {
            if (isBossWave && turn > bossOverTimeThreshold)
            {
                await OverTimer(true);
            }
            else if (!isBossWave && turn > nonBossOverTimeThreshold)
            {
                await OverTimer(false);
            }
        }

        // If the player has no character, force end turn (battle is effectively lost)
        if (PlayerStats.Instance.myCharacter == null)
        {
            UIController.Instance.EndTurn(false);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Applies an "OverTimer" defence debuff to everyone after too many turns.
    /// </summary>
    private async Task OverTimer(bool isBossWave)
    {
        // Only the host should trigger this in online mode
        if (OnlineRelayInstance != null && OnlineRelayInstance.IsConnected() && !NetworkManager.Singleton.IsHost)
        {
            return;
        }

        int damage = isBossWave
            ? turn - bossOverTimeThreshold
            : turn - nonBossOverTimeThreshold;

        if (damage <= 0) return;

        AttackVariables attackVars = new AttackVariables
        {
            TargetingMode = ActionTarget.Everyone,
            ResolvedDamage = damage,
            MinDamage = 0,
            MaxDamage = 0,
            Effect = ActionEffect.Debuff_Defence,
            IsPlayerAction = false,
            AttackerId = "None",
            ImpactVisual = EffectVisual.DebuffDef,
            CritMultiplier = 1,
            CritChance = 0,
            IsAllyAiAction = false,
            ActionName = "Tired",
            ActionId = 0,
            ActionPointCost = 0,
            TargetId = string.Empty,
        };

        await AttackResolver.Instance.Attack(attackVars);
    }

    #endregion

    #region Game End / Wave Logic

    public void HostLeft()
    {
        UIController.Instance.GameOver(wave, false, true);
    }

    public void CheckGameStatusDelay()
    {
        Invoke(nameof(CheckGameStatus), statusCheckDelay);
    }

    /// <summary>
    /// Checks if the wave or game has ended:
    /// - If enemies are gone and allies remain → wave won.
    /// - If allies are gone → game over.
    /// </summary>
    public bool CheckGameStatus()
    {
        if (turn <= 0) return false;

        bool enemiesRemain = enemies.Count > 0;
        bool friendliesRemain = allies.Count > 0 || aiAllies.Count > 0;

        // If both sides still have characters, the game continues
        if (enemiesRemain && friendliesRemain)
        {
            Debug.Log($"Check Enemies: {enemies.Count}, Allies: {allies.Count}, AI Allies: {aiAllies.Count}");
            return false;
        }

        if (gameEnded) return false;

        gameEnded = true;

        if (friendliesRemain)
        {
            // Allies remain, wave is beaten
            Debug.Log("Wave: " + wave);

            // Next wave is 0-based index = wave (current is wave - 1)
            if (TryGetWaveIsBoss(wave, out bool nextIsBoss))
            {
                if (nextIsBoss)
                {
                    BattleHUDController.Instance.waveCountText.text =
                        Localizer.Instance.GetLocalizedText("Boss Wave!") + "\n" +
                        (wave + 1).ToString() + " / " + Quest.waves.Count;
                    BattleHUDController.Instance.waveCountText.color = Color.red;
                }
                else
                {
                    BattleHUDController.Instance.waveCountText.text =
                        Localizer.Instance.GetLocalizedText("Wave: ") + "\n" +
                        (wave + 1).ToString() + " / " + Quest.waves.Count;
                    BattleHUDController.Instance.waveCountText.color = Color.white;
                }
            }

            UIController.Instance.RoundWon(wave);
        }
        else
        {
            // All allies gone = game over
            UIController.Instance.GameOver(wave, false, false);
        }

        return true;
    }

    #endregion

    #region Helpers

    private OnlineRelay OnlineRelayInstance
    {
        get
        {
            if (onlineRelay == null)
            {
                onlineRelay = OnlineRelay.Instance;
            }
            return onlineRelay;
        }
    }

    /// <summary>
    /// Tries to read the "Boss" flag from the wave at the given index.
    /// We don't need the concrete wave type here; we just rely on it having a bool Boss.
    /// </summary>
    private bool TryGetWaveIsBoss(int index, out bool isBoss)
    {
        isBoss = false;

        if (Quest == null || Quest.waves == null || Quest.waves.Count == 0)
        {
            Debug.LogWarning($"[{nameof(EnemyController)}] Quest or waves not configured.");
            return false;
        }

        if (index < 0 || index >= Quest.waves.Count)
        {
            Debug.LogWarning($"[{nameof(EnemyController)}] Wave index {index} is out of range for Quest '{Quest.name}'.");
            return false;
        }

        var waveData = Quest.waves[index];
        if (waveData == null)
        {
            Debug.LogWarning($"[{nameof(EnemyController)}] Wave at index {index} is null in Quest '{Quest.name}'.");
            return false;
        }

        // This will compile because the compiler knows the element type of Quest.waves,
        // even though we don't name that type explicitly here.
        isBoss = waveData.Boss;
        return true;
    }

    #endregion
}

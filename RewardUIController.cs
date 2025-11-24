using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class RewardUIController : MonoBehaviour
{
    #region Singleton

    public static RewardUIController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"Multiple {nameof(RewardUIController)} instances detected. Destroying duplicate on '{name}'.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #endregion

    #region Serialized UI References & Quest

    [Header("Game Over UI")]
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private TextMeshProUGUI gameOverDisc;
    [SerializeField] private Slider gameOverXPSlider;
    [SerializeField] private Button gameOverButton;
    [SerializeField] private Animator playAnimator;

    [field: Header("Quest")]
    [field: Tooltip("Quest configuration used to determine XP, gold, and progression.")]
    [field: SerializeField]
    public QuestSO quest { get; private set; }
    public void SetQuest(QuestSO newQuest) { quest = newQuest; }

    #endregion

    #region XP State

    private int startingLevel;
    private float startingXP;
    private float startingXPNeeded;

    #endregion

    #region Reward & Steam Integration

    public void UpdateReward(float amount)
    {
        var stats = PlayerStats.Instance;
        if (stats == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] PlayerStats.Instance is null. Cannot update reward.");
            return;
        }

        var item = stats.Item;
        if (item != null && item.Effect == ItemEffect.Passive_Reward_Bonus)
        {
            amount *= (item.EffectModifier * 0.1f) + 1f;
        }

        amount += 0.000005f;
        amount = MathF.Round(amount, 5);
        Debug.Log($"[{nameof(RewardUIController)}] Reward XP: {amount}");

        string playerClassName = stats.playerClass.ToString();

        // XP
        float currentXP = PlayerPrefs.GetFloat(playerClassName + "_XP");
        PlayerPrefs.SetFloat(playerClassName + "_XP", currentXP + amount);

        // Gold (scaled by quest)
        if (quest == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] Quest reference is null. Cannot apply gold multiplier.");
            return;
        }

        float goldAmount = amount * quest.reward.goldMulti;
        float currentGold = PlayerPrefs.GetFloat("Gold");
        PlayerPrefs.SetFloat("Gold", currentGold + goldAmount);
    }

    private void GameOverSteamUpdate(int level)
    {
        var steamIntegration = SteamIntergration.Instance;
        if (steamIntegration == null || !steamIntegration.SteamConnected)
        {
            return;
        }

        string className = string.Empty;

        switch (PlayerStats.Instance.choosenClass.PlayerClass)
        {
            case PlayerClasses.Archer: className = "Archer"; break;
            case PlayerClasses.Warrior: className = "Warrior"; break;
            case PlayerClasses.Mage: className = "Mage"; break;
            case PlayerClasses.Healer: className = "Healer"; break;
            case PlayerClasses.Knight: className = "Knight"; break;
            case PlayerClasses.Assassin: className = "Assassin"; break;
            case PlayerClasses.Jester: className = "Jester"; break;
            case PlayerClasses.Vampire: className = "Vampire"; break;
            default: return;
        }

#if UNITY_ANDROID || UNITY_IOS
#else
        Steamworks.SteamUserStats.SetStat(className, level);
#endif
    }

    #endregion

    #region Game Over Level-Up & XP Bar

    public void Setup(int classLevel, float xp)
    {
        startingLevel = classLevel;
        startingXP = xp;
        startingXPNeeded = (startingLevel * (startingLevel + 1)) + 1; ;
    }

    public void UpdateGameOverUI(float amount)
    {
        var stats = PlayerStats.Instance;
        if (stats == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] PlayerStats.Instance is null. Cannot update GameOver UI.");
            return;
        }

        if (gameOverText == null || gameOverXPSlider == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] GameOver UI references are not assigned.");
            return;
        }

        startingLevel++;
        gameOverText.text =
            $"{Localizer.Instance.GetLocalizedText(stats.playerClass.ToString())} {Localizer.Instance.GetLocalizedText("Lv.")}{startingLevel}";

        startingXPNeeded = (startingLevel * (startingLevel + 1)) + 1;
        float maxValue = startingXPNeeded;

        var xpSlider = gameOverXPSlider.GetComponent<XPSlider>();
        if (xpSlider == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] XPSlider component missing from gameOverXPSlider.");
            return;
        }

        xpSlider.UpdateSlider(maxValue);

        string playerClassName = stats.playerClass.ToString();
        int currentLevel = PlayerPrefs.GetInt(playerClassName + "_Level", 0);
        PlayerPrefs.SetInt(playerClassName + "_Level", currentLevel + 1);

        GameOverSteamUpdate(startingLevel);
    }

    private void OverLevelFailsafe()
    {
        if (gameOverXPSlider == null || gameOverText == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] GameOver slider/text not assigned for OverLevelFailsafe.");
            return;
        }

        // Increase the slider's minimum, update displayed level, recalc XP needed.
        gameOverXPSlider.minValue = ((startingLevel - 1) * ((startingLevel - 1) + 1)) + 1;
        startingLevel++;

        gameOverText.text =
            $"{Localizer.Instance.GetLocalizedText(PlayerStats.Instance.playerClass.ToString())} {Localizer.Instance.GetLocalizedText("Lv.")}{startingLevel}";

        startingXPNeeded = (startingLevel * (startingLevel + 1)) + 1;
        gameOverXPSlider.maxValue = startingXPNeeded;

        string playerClassName = PlayerStats.Instance.playerClass.ToString();
        int currentLevel = PlayerPrefs.GetInt(playerClassName + "_Level", 0);
        PlayerPrefs.SetInt(playerClassName + "_Level", currentLevel + 1);

        GameOverSteamUpdate(startingLevel);
    }

    #endregion

    #region Quest XP Calculation

    // waveIndex: 0..N-1 => per-wave payout, -1 => victory bonus, -2 => pity (50% of first-wave)
    public float QuestXP(int waveIndex)
    {
        if (quest == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] Quest reference is null in QuestXP().");
            return 0f;
        }

        int N = quest.waves.Count;
        if (N <= 0)
            return 0f;

        float xp = quest.reward.experiencePoints;

        const float VictoryShare = 0.10f; // 10% victory bonus
        const float Bias = 0.5f;          // small offset to balance early waves (tweak 0.25..1f)

        // Weights per wave: w_i = i + Bias, with i = 1..N (1-based for math)
        float S = N * (N + 1) / 2f; // sum of i
        float W = S + Bias * N;     // sum of (i + Bias)
        float w1 = 1f + Bias;       // first-wave weight (i = 1)

        // Budget: sum(waves) + victory + pity = xp
        // sum(waves) = k * W
        // pity = 50% of first-wave payout = 0.5 * k * w1
        float k = (1f - VictoryShare) * xp / (W + 0.5f * w1);

        // Per-wave payout (0-based index -> weight uses i = waveIndex + 1)
        if (waveIndex >= 0 && waveIndex < N)
        {
            int i = waveIndex + 1;
            return k * (i + Bias);
        }

        // Victory bonus
        if (waveIndex == -1)
            return xp * VictoryShare;

        // Loser pity (50% of first wave payout)
        if (waveIndex == -2)
            return 0.5f * k * w1;

        return 0f;
    }

    #endregion

    #region Game Over Flow

    public void GameOver(int wave, bool won, bool hostDisconnected)
    {
        GameOverSteamUpdate(startingLevel);

        if (playAnimator != null)
        {
            playAnimator.SetTrigger("Trigger");
        }
        else
        {
            Debug.LogWarning($"[{nameof(RewardUIController)}] playAnimator is not assigned.");
        }

        var stats = PlayerStats.Instance;
        if (stats == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] PlayerStats.Instance is null. Cannot finalize GameOver UI.");
            return;
        }

        if (gameOverDisc == null || gameOverText == null || gameOverXPSlider == null)
        {
            Debug.LogError($"[{nameof(RewardUIController)}] GameOver UI references are missing.");
            return;
        }

        // Win / lose text
        if (won)
        {
            gameOverDisc.text = Localizer.Instance.GetLocalizedText("You won!");

            // Quest progression (non-survival quests only)
            if ((NetworkManager.Singleton == null || NetworkManager.Singleton.IsHost || !OnlineRelay.Instance.IsConnected()) &&
                quest != null &&
                quest.difficultyLevel < 1000 &&
                quest.difficultyLevel == PlayerPrefs.GetInt("Quest", 0))
            {
                int currentQuest = PlayerPrefs.GetInt("Quest", 0);
                PlayerPrefs.SetInt("Quest", currentQuest + 1);

#if UNITY_ANDROID || UNITY_IOS
#else
                var steamIntegration = SteamIntergration.Instance;
                if (steamIntegration != null && steamIntegration.SteamConnected)
                {
                    Steamworks.SteamUserStats.SetStat("Quest", quest.difficultyLevel + 1);
                }
#endif
            }

            UpdateReward(QuestXP(-1));
        }
        else
        {
            if (!hostDisconnected)
            {
                gameOverDisc.text = Localizer.Instance.GetLocalizedText("You have been defeated.");
            }
            else
            {
                gameOverDisc.text = Localizer.Instance.GetLocalizedText("Host left the game.");
            }
        }

        if (EnemyController.Instance != null && EnemyController.Instance.turn > 1)
        {
            float pityXP = QuestXP(-2);
            UpdateReward(pityXP);
            Debug.Log($"[{nameof(RewardUIController)}] QuestXP(-2) pity: {pityXP}");
        }

#if UNITY_ANDROID || UNITY_IOS
#else
        if (quest != null && quest.difficultyLevel > 1000)
        {
            var steamIntegration = SteamIntergration.Instance;
            if (steamIntegration != null && steamIntegration.SteamConnected)
            {
                Steamworks.SteamUserStats.SetStat("Survival", wave);
            }
        }
#endif

        // Final class level and wave-based XP
        gameOverText.text =
            $"{Localizer.Instance.GetLocalizedText(stats.playerClass.ToString())}  {Localizer.Instance.GetLocalizedText("Lv.")}{startingLevel}";

        // Failsafe if XP overshot the level cap
        if (startingXP > startingXPNeeded)
        {
            OverLevelFailsafe();
        }

        float minValue = ((startingLevel - 1) * ((startingLevel - 1) + 1)) + 1;
        float value = startingXP;
        float maxValue = startingXPNeeded;

        string playerClassName = stats.playerClass.ToString();
        float finalXP = PlayerPrefs.GetFloat(playerClassName + "_XP");
        float sliderMoveAmount = finalXP - startingXP;

        var xpSlider = gameOverXPSlider.GetComponent<XPSlider>();
        if (xpSlider != null)
        {
            StartCoroutine(xpSlider.StartSlider(minValue, maxValue, value, sliderMoveAmount));
        }
        else
        {
            Debug.LogError($"[{nameof(RewardUIController)}] XPSlider component missing from gameOverXPSlider.");
        }

        if (gameOverButton != null)
        {
            UINavigationController.Instance.JumpToElement(gameOverButton);
        }

        SoundController.Instance.MusicTransition(1);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        if (TurnHandler.Instance != null)
        {
            TurnHandler.Instance.Cancel();
        }
    }

    #endregion
}

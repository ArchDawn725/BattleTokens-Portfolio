using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Character : MonoBehaviour
{
    public CharacterStats Stats { get; private set; }
    public CharacterView View { get; private set; }
    public CharacterCombat Combat { get; private set; }

    #region Tunables

    [Header("Spawn Failsafe")]
    [SerializeField] private float spawnFailsafeDelaySeconds = 1f;

    [Header("Regen & Specials")]
    [Tooltip("Minimum regen maintained by HealthRegen class special.")]
    [SerializeField] private int healthRegenMinAmount = 1;

    [Tooltip("Amount of regen granted each turn by InfiniteHealthRegeneration.")]
    [SerializeField] private int infiniteHealthRegenAmount = 1;

    [Header("Temp Effects Decay")]
    [Tooltip("Divisor used when recovering negative temporary defence each turn.")]
    [SerializeField] private float negativeDefRecoveryDivisor = 10f;

    [Tooltip("Per-turn TempDmg recovery for negative damage buffs.")]
    [SerializeField] private int tempDmgNegativeRecoveryPerTurn = 1;

    [Tooltip("Per-turn TempDef decay for positive defence buffs.")]
    [SerializeField] private int tempDefPositiveDecayPerTurn = 1;

    [Tooltip("Per-turn TempDmg decay for positive damage buffs.")]
    [SerializeField] private int tempDmgPositiveDecayPerTurn = 1;

    [Header("Regen Display Colors")]
    [SerializeField] private Color regenHealColor = Color.green;
    [SerializeField] private Color regenDamageColor = Color.red;

    [Header("Action Wait Times (seconds)")]
    [SerializeField] private float waitSingleTarget = 0.25f;
    [SerializeField] private float waitSmallAoE = 0.5f;    // front / middle / back / ally front
    [SerializeField] private float waitMediumAoE = 0.75f;  // front + mid
    [SerializeField] private float waitLargeAoE = 1f;      // all allies / all enemies
    [SerializeField] private float waitEveryone = 1.25f;   // everyone

    [Header("On-Death Spawns")]
    [Tooltip("Enemy index spawned when Ressurection_Lich dies.")]
    [SerializeField] private int resurrectionLichEnemyIndex = 106;

    [Tooltip("Enemy index spawned when Transformation_Cultist dies.")]
    [SerializeField] private int transformationCultistEnemyIndex = 108;

    [Tooltip("Enemy index for zombies spawned when a Zombified unit dies.")]
    [SerializeField] private int zombifiedEnemyIndex = 98;

    #endregion

    private void Start()
    {
        Invoke(nameof(SpawnFailsafe), spawnFailsafeDelaySeconds);
    }

    private void SpawnFailsafe()
    {
        if (Stats == null)
        {
            // last resort failsafe
            if (GridManager.Instance.MyPlayerLocation == transform.parent.parent.name)
            {
                SpawnManager.Instance.ChooseMySpawn();
            }

            Destroy(gameObject);
        }
    }

    public void SetUp(CharacterVariables variables)
    {
        // If this is the local player's character, notify OnlineRelay and set references in PlayerStats.
        if (variables.LocalClientId == NetworkManager.Singleton.LocalClientId)
        {
            variables.Location = transform.parent.parent.name;
            OnlineRelay.Instance.SetCharacterValues(variables);

            if (PlayerStats.Instance.myCharacter == null && GetComponent<EnemyCharacter>() == null)
            {
                PlayerStats.Instance.myCharacter = this;
            }
        }
    }

    public void SetUpCall(CharacterVariables variables)
    {
        Stats = new CharacterStats(variables);
        View = GetComponent<CharacterView>();
        Combat = GetComponent<CharacterCombat>();

        View.SetUpCall(variables);
        Combat.SetUpCall(variables);

        SpawnManager.Instance.Spawned();
    }

    public void ButtonPress()
    {
        // Do not save; character can be moved around
        transform.parent.parent.GetComponent<Button>().onClick.Invoke();
    }

    public void NextTurn()
    {
        Stats.SetProtectedBy(null);
        Stats.ActionPointsNewTurn();

        TempEffects();

        // If player character and this is my tile, apply passive item effects
        if (transform.parent.name == GridManager.Instance.MyPlayerLocation &&
            PlayerStats.Instance.Item != null)
        {
            ItemSO item = PlayerStats.Instance.Item;

            switch (item.Effect)
            {
                default:
                    break;

                case ItemEffect.Passive_Regeneration:
                    if (Stats.TempRegen < item.EffectModifier)
                    {
                        Stats.AdjustTempRegen(item.EffectModifier);
                        if (Stats.TempRegen > item.EffectModifier)
                        {
                            Stats.SetHealthRegen(item.EffectModifier);
                        }
                    }
                    break;

                case ItemEffect.Passive_Poison_Resistance:
                    if (Stats.TempRegen < 0)
                    {
                        Stats.AdjustTempRegen(item.EffectModifier);
                        if (Stats.TempRegen > 0)
                        {
                            Stats.SetHealthRegen(0);
                        }
                    }
                    break;
            }
        }

        // Class specials that adjust regen
        switch (Stats.ClassSpecial)
        {
            default:
            case ClassSpecial.None:
                break;

            case ClassSpecial.HealthRegen:
                if (EnemyController.Instance.turn > 1 && Stats.TempRegen < healthRegenMinAmount)
                {
                    // maintains a health regen at N or more
                    Stats.AdjustTempRegen(healthRegenMinAmount);
                }
                break;

            case ClassSpecial.InfiniteHealthRegeneration:
                // Health regen never decreases
                if (EnemyController.Instance.turn > 1)
                {
                    Stats.AdjustTempRegen(infiniteHealthRegenAmount);
                }
                break;
        }

        if (Stats.TempRegen != 0)
        {
            Regeneration();
        }

        // Each turn, move TempRegen back toward 0 by 1
        if (Stats.TempRegen < 0)
        {
            Stats.AdjustTempRegen(1);
        }
        else if (Stats.TempRegen > 0)
        {
            Stats.AdjustTempRegen(-1);
        }

        View.UpdateUI();
        View.readyUp.SetActive(false);
    }

    private void TempEffects()
    {
        if (Stats.ActionPoints >= Stats.StartingActionPoints)
        {
            Stats.SetWebbed(false);
        }
        // TODO: webbing animation?

        // If no temp effects, exit early
        if (Stats.TempDef == 0 && Stats.TempDmg == 0) return;

        int amount = 0;
        bool isHealing = false;
        Color textColor = Color.gray;

        // Negative tempDef (debuff) slowly recovers
        if (Stats.TempDef < 0)
        {
            amount = Mathf.CeilToInt(Stats.Defence / negativeDefRecoveryDivisor);

            if (Stats.TempDef < 0 && Stats.ClassSpecial == ClassSpecial.DefenceBoost)
            {
                amount += Mathf.CeilToInt(Stats.Defence / negativeDefRecoveryDivisor);
            }

            Stats.AdjustTempDef(amount);

            if (Stats.TempDef > 0)
            {
                Stats.SetTempDef(0);
            }

            isHealing = true;
        }

        // Negative tempDmg slowly recovers
        if (Stats.TempDmg < 0)
        {
            Stats.AdjustTempDmg(tempDmgNegativeRecoveryPerTurn);
            isHealing = true;
            amount = tempDmgNegativeRecoveryPerTurn;
        }

        // Positive tempDef slowly decays
        if (Stats.TempDef > 0)
        {
            if (Stats.ClassSpecial != ClassSpecial.DefenceBoost)
            {
                Stats.AdjustTempDef(-tempDefPositiveDecayPerTurn);
            }
            amount = -tempDefPositiveDecayPerTurn;
        }

        // Positive tempDmg slowly decays
        if (Stats.TempDmg > 0)
        {
            Stats.AdjustTempDmg(-tempDmgPositiveDecayPerTurn);
            amount = -tempDmgPositiveDecayPerTurn;
        }

        View.HitAnimation(amount, isHealing, textColor, false, EffectVisual.Heal);
    }

    /// <summary>
    /// Handles applying or removing regeneration (positive or negative).
    /// If TempRegen is positive, it heals. If it's negative, it deals self-damage (Poison).
    /// </summary>
    private void Regeneration()
    {
        Debug.Log("Regen called");

        // Positive Regen
        if (Stats.TempRegen >= 0)
        {
            bool isHealing = true;
            Color textColor = regenHealColor;

            Stats.AdjustHealth(Stats.TempRegen);
            Stats.CapHealth();

            // Negative value in popup to indicate healing
            View.HitAnimation(-Stats.TempRegen, isHealing, textColor, false, EffectVisual.Heal);
        }
        else
        {
            bool isHealing = false;
            Color textColor = regenDamageColor;

            Stats.AdjustHealth(Stats.TempRegen);

            View.HitAnimation(Stats.TempRegen, isHealing, textColor, false, EffectVisual.Fire);

            if (Stats.Health <= 0)
            {
                Combat.TriggerDeath();
            }
        }
    }

    public async Task ActionUse(AttackVariables attackVariables)
    {
        Stats.AdjustActionPoints(-attackVariables.ActionPointCost);
        Combat.TriggerClassSpecial(attackVariables);

        if (!string.IsNullOrEmpty(attackVariables.ActionName))
        {
            View.SpawnWordPopup(Localizer.Instance.GetLocalizedText(attackVariables.ActionName), Color.white);
        }

        if (PlayerStats.Instance.myCharacter == this)
        {
            BattleHUDController.Instance.UpdateGameActions();
            await GridManager.Instance.ClearAll();
            LoadoutUIController.Instance.UpdateStats();
            BattleHUDController.Instance.TemperaryActionButtonsEnable();

            float waitTime = GetPlayerWaitTime(attackVariables.TargetingMode);

            if (Stats.ActionPoints <= 0 && PlayerStats.Instance.autoEndTurnSetting)
            {
                BattleHUDController.Instance.endTurnButton.interactable = false;
                await Awaitable.WaitForSecondsAsync(waitTime / PlayerStats.Instance.playSpeed);
                UIController.Instance.EndTurn(true);
            }
            else if (Stats.ActionPoints <= 0 && BattleHUDController.Instance.BoardActive)
            {
                BattleHUDController.Instance.tutorialText.text =
                    Localizer.Instance.GetLocalizedText("You are out of actions, end your turn.");
            }
        }
    }

    private float GetPlayerWaitTime(ActionTarget targetingMode)
    {
        switch (targetingMode)
        {
            default:
            case ActionTarget.Self:
            case ActionTarget.Relocate:
            case ActionTarget.Random:
            case ActionTarget.Chosen:
            case ActionTarget.Any_Reverse:
            case ActionTarget.Any_Ranged:
            case ActionTarget.Any_Front:
            case ActionTarget.Any_Ally:
            case ActionTarget.Any:
                return waitSingleTarget;

            case ActionTarget.All_Back:
            case ActionTarget.All_Middle:
            case ActionTarget.All_Front:
            case ActionTarget.All_Ally_Front:
                return waitSmallAoE;

            case ActionTarget.All_Front_Mid:
                return waitMediumAoE;

            case ActionTarget.All_Ally:
            case ActionTarget.All:
                return waitLargeAoE;

            case ActionTarget.Everyone:
                return waitEveryone;
        }
    }

    private void OnDestroy()
    {
        if (EnemyController.Instance != null)
        {
            EnemyController.Instance.CheckGameStatusDelay();
        }

        // On-death spawns for specific class specials
        int questModifierPoints = (int)EnemyController.Instance.Quest.Modifier;

        if (Stats.ClassSpecial == ClassSpecial.Resurrection_Lich)
        {
            SpawnManager.Instance.SpawnEnemyCall(
                GridManager.Instance.RandomEnemyLocation(EnemyClass.AOE_DPS),
                resurrectionLichEnemyIndex,
                false,
                questModifierPoints);
        }

        if (Stats.ClassSpecial == ClassSpecial.Transformation_Cultist)
        {
            SpawnManager.Instance.SpawnEnemyCall(
                GridManager.Instance.RandomEnemyLocation(EnemyClass.Tank),
                transformationCultistEnemyIndex,
                false,
                questModifierPoints);
        }

        if (Stats.Zombified && UIController.Instance.gState == UIController.GameState.Battle)
        {
            SpawnManager.Instance.SpawnEnemyCall(
                GridManager.Instance.RandomEnemyLocation(EnemyClass.Melee_DPS),
                zombifiedEnemyIndex,
                false,
                questModifierPoints);
        }
    }
}

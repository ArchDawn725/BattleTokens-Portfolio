using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using static Steamworks.InventoryItem;

public class CharacterCombat : MonoBehaviour
{
    private CharacterView view;
    private CharacterStats stats;

    #region Tunables & Colors

    [Header("Defence / Damage Tuning")]
    [Tooltip("Fraction of pre-mitigated damage used to reduce temporary defence on single-target hits.")]
    [SerializeField] private float defenceLossFromDamageFraction = 0.1f; // preDamage / 10

    [Tooltip("Fraction of post-mitigated damage converted to healing for Lifesteal.")]
    [SerializeField] private float lifestealFraction = 0.5f; // half

    [Tooltip("Fraction of damage converted to temporary defence for Defence Steal.")]
    [SerializeField] private float defenceStealFraction = 0.2f; // newDamage / 5

    [Tooltip("Fraction of healing converted to self-heal for AutoHeal.")]
    [SerializeField] private float autoHealHealthFraction = 0.5f; // newDamage / 2

    [Tooltip("Fraction of healing converted to temp defence for AutoHeal.")]
    [SerializeField] private float autoHealDefenceFraction = 0.25f; // newDamage / 4

    [Tooltip("AP lost when stunned.")]
    [SerializeField] private int stunActionPointLoss = 1;

    [Tooltip("Base damage multiplier used for counter-attacks.")]
    [SerializeField] private int counterAttackDamageMultiplier = 2;

    [Tooltip("Base damage used for poison-on-hit effects.")]
    [SerializeField] private int poisonOnHitBaseDamage = 1;

    [Tooltip("Group summon slot/index used by special summons.")]
    [SerializeField] private int groupSummonSlotIndex = 0;

    [Tooltip("ActionId used by OverTimer (defence debuff) so DefenceBoost can ignore it.")]
    [SerializeField] private int overTimerActionId = 0;

    [Header("Hit Popup Colors")]
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private Color healColor = Color.green;
    [SerializeField] private Color buffColor = Color.gray;
    [SerializeField] private Color poisonColor = Color.blue;
    [SerializeField] private Color regenColor = Color.cyan;
    [SerializeField] private Color defaultTextColor = Color.black;

    #endregion

    public void SetUpCall(CharacterVariables variables)
    {
        stats = GetComponent<CharacterStats>();
        view = GetComponent<CharacterView>();
    }

    public void Hit(AttackVariables attackVariables)
    {
        if (stats == null) stats = GetComponent<CharacterStats>();
        if (view == null) view = GetComponent<CharacterView>();

        bool isHealing = false;
        Color textColor = defaultTextColor;

        switch (attackVariables.Effect)
        {
            case ActionEffect.Damage:
                {
                    int preDamage = attackVariables.ResolvedDamage;

                    // If not protected, or if the protector is also mutually protected by me, take the damage.
                    if (stats.ProtectedBy == null ||
                        TargetSelectionService.Instance.GetCharacter(stats.ProtectedBy) == null ||
                        (TargetSelectionService.Instance.GetCharacter(stats.ProtectedBy)?.Stats.ProtectedBy == transform.parent.parent.name))
                    {
                        Debug.Log("Not protected");

                        // Reduce damage by defence
                        attackVariables.ResolvedDamage -= stats.GetTotalDefence();
                        attackVariables.ResolvedDamage = Mathf.Max(attackVariables.ResolvedDamage, 0);

                        textColor = damageColor;
                        stats.AdjustHealth(-attackVariables.ResolvedDamage);

                        // Lowers temp defence if single-target damage is taken
                        if (attackVariables.TargetingMode == ActionTarget.Any ||
                            attackVariables.TargetingMode == ActionTarget.Any_Ranged ||
                            attackVariables.TargetingMode == ActionTarget.Random ||
                            attackVariables.TargetingMode == ActionTarget.Any_Front ||
                            attackVariables.TargetingMode == ActionTarget.Any_Reverse ||
                            attackVariables.TargetingMode == ActionTarget.Chosen)
                        {
                            int amount = Mathf.CeilToInt(preDamage * defenceLossFromDamageFraction);

                            // Regular attacks no longer reduce defence into negatives
                            if (stats.GetTotalDefence() - amount <= 0)
                            {
                                amount = stats.GetTotalDefence();
                            }

                            if (amount < 0) amount = 0;

                            stats.AdjustTempDef(-amount);
                        }
                    }
                    else
                    {
                        // Redirect damage to protector (host only in online)
                        if (!OnlineRelay.Instance.IsConnected() || NetworkManager.Singleton.IsHost)
                        {
                            attackVariables.TargetingMode = ActionTarget.Chosen;
                            attackVariables.IsPlayerAction = false;
                            attackVariables.TargetId = stats.ProtectedBy;
                            attackVariables.IsAllyAiAction = false;

                            AttackResolver.Instance.Attack(attackVariables);
                        }
                    }
                    break;
                }

            case ActionEffect.Pierce:
                // Ignores defence
                attackVariables.ResolvedDamage = Mathf.Max(attackVariables.ResolvedDamage, 0);
                textColor = damageColor;
                stats.AdjustHealth(-attackVariables.ResolvedDamage);
                break;

            case ActionEffect.Heal:
                // Positive damage interpreted as healing
                attackVariables.ResolvedDamage = Mathf.Max(attackVariables.ResolvedDamage, 0);
                stats.AdjustHealth(attackVariables.ResolvedDamage);
                stats.CapHealth();

                isHealing = true;
                textColor = healColor;

                // Negative value for popup to visually indicate healing
                attackVariables.ResolvedDamage *= -1;
                break;

            case ActionEffect.Buff_Defence:
                stats.AdjustTempDef(attackVariables.ResolvedDamage);
                isHealing = true;
                textColor = buffColor;
                break;

            case ActionEffect.Debuff_Defence:
                // Ignore OverTimer debuff if the unit has DefenceBoost
                if (stats.ClassSpecial == ClassSpecial.DefenceBoost &&
                    attackVariables.ActionId == overTimerActionId)
                {
                    break;
                }

                stats.AdjustTempDef(-attackVariables.ResolvedDamage);
                textColor = buffColor;
                break;

            case ActionEffect.Buff_Damage:
                stats.AdjustTempDmg(attackVariables.ResolvedDamage);
                isHealing = true;
                textColor = buffColor;
                break;

            case ActionEffect.Debuff_Damage:
                stats.AdjustTempDmg(-attackVariables.ResolvedDamage);
                textColor = buffColor;
                break;

            case ActionEffect.Poison:
                if (stats.ProtectedBy == null
                    || TargetSelectionService.Instance.GetCharacter(stats.ProtectedBy) == null
                    || (TargetSelectionService.Instance.GetCharacter(stats.ProtectedBy)?.Stats.ProtectedBy == transform.parent.parent.name))
                {
                    // Poison damage is stored as negative regen
                    attackVariables.ResolvedDamage = Mathf.Max(attackVariables.ResolvedDamage, 0);
                    textColor = poisonColor;
                    stats.AdjustTempRegen(-attackVariables.ResolvedDamage);
                }
                else
                {
                    // Redirect poison to protector
                    if (!OnlineRelay.Instance.IsConnected() || NetworkManager.Singleton.IsHost)
                    {
                        attackVariables.TargetingMode = ActionTarget.Chosen;
                        attackVariables.IsPlayerAction = false;
                        attackVariables.AttackerId = stats.ProtectedBy;
                        attackVariables.IsAllyAiAction = false;

                        AttackResolver.Instance.Attack(attackVariables);
                    }
                }
                break;

            case ActionEffect.Protect:
                // Mark who is protecting me
                stats.SetProtectedBy(attackVariables.AttackerId);
                break;

            case ActionEffect.Regen:
                attackVariables.ResolvedDamage = Mathf.Max(attackVariables.ResolvedDamage, 0);

                isHealing = true;
                textColor = regenColor;

                stats.AdjustTempRegen(attackVariables.ResolvedDamage);
                stats.CapHealth();

                // Negative in popup to visually indicate healing/regeneration
                attackVariables.ResolvedDamage *= -1;
                break;

            case ActionEffect.Stun:
                if (stats.ProtectedBy == null
                    || TargetSelectionService.Instance.GetCharacter(stats.ProtectedBy) == null
                    || (TargetSelectionService.Instance.GetCharacter(stats.ProtectedBy)?.Stats.ProtectedBy == transform.parent.parent.name))
                {
                    stats.AdjustTempDef(-attackVariables.ResolvedDamage);
                    stats.AdjustActionPoints(-stunActionPointLoss);
                    stats.SetWebbed(true);

                    textColor = buffColor;
                }
                else
                {
                    if (!OnlineRelay.Instance.IsConnected() || NetworkManager.Singleton.IsHost)
                    {
                        attackVariables.TargetingMode = ActionTarget.Chosen;
                        attackVariables.IsPlayerAction = false;
                        attackVariables.AttackerId = stats.ProtectedBy;
                        attackVariables.IsAllyAiAction = false;

                        AttackResolver.Instance.Attack(attackVariables);
                    }
                }
                break;

            case ActionEffect.SpecialSummon:
                PlayerStats.Instance.GroupSummon(groupSummonSlotIndex, attackVariables.ResolvedDamage);
                textColor = defaultTextColor;
                view.HitAnimation(attackVariables.ResolvedDamage, true, textColor,
                                  attackVariables.CritMultiplier > 1,
                                  attackVariables.ImpactVisual);
                return;

            case ActionEffect.Relocate:
                Debug.Log("Relocate effect triggered.");
                break;
        }

        view.HitAnimation(attackVariables.ResolvedDamage, isHealing, textColor,
                          attackVariables.CritMultiplier > 1,
                          attackVariables.ImpactVisual);

        // If caller is "None", nothing more to do
        if (string.IsNullOrEmpty(attackVariables.AttackerId) ||
            attackVariables.AttackerId == "None")
        {
            view.UpdateUI();

            if (stats.Health <= 0)
            {
                TriggerDeath();
            }
            return;
        }

        // Caller character (for Zombification / PoisonAttacks checks)
        Button callerButton = GridManager.Instance.GetLocation(attackVariables.AttackerId);
        Character callerChar = callerButton.transform.GetChild(2).GetChild(0).GetComponent<Character>();

        if (callerChar != null &&
            callerChar.Stats.ClassSpecial == ClassSpecial.Zombification &&
            attackVariables.ResolvedDamage > 0)
        {
            Debug.Log("Zombified!");
            stats.SetZombified(true);
        }

        view.UpdateUI();

        // Death check
        if (stats.Health <= 0)
        {
            TriggerDeath();
            return;
        }

        // Counter-attack and poison-on-hit hooks
        if (stats.ClassSpecial == ClassSpecial.CounterAttack &&
            attackVariables.ActionId == 1)
        {
            TriggerCounterAttack(attackVariables.AttackerId, attackVariables.Effect);
        }

        if (callerChar != null &&
            callerChar.Stats.ClassSpecial == ClassSpecial.PoisonAttacks)
        {
            TriggerPoisionAttacked(attackVariables, callerChar);
        }
    }

    public void TriggerClassSpecial(AttackVariables attackVariables)
    {
        if (stats == null) stats = GetComponent<CharacterStats>();
        if (view == null) view = GetComponent<CharacterView>();

        switch (stats.ClassSpecial)
        {
            case ClassSpecial.Lifesteal:
                if (attackVariables.Effect == ActionEffect.Damage)
                {
                    Character targetCharacter = TargetSelectionService.Instance.GetCharacter(attackVariables.AttackerId);
                    if (targetCharacter == null) break;

                    int afterDamage = attackVariables.ResolvedDamage - targetCharacter.Stats.GetTotalDefence();
                    if (afterDamage <= 0) break;

                    int healAmount = Mathf.CeilToInt(afterDamage * lifestealFraction);

                    stats.AdjustHealth(healAmount);
                    stats.CapHealth();

                    view.HitAnimation(healAmount, true, healColor, false, EffectVisual.Heal);
                }
                break;

            case ClassSpecial.DefenceSteal:
                if (attackVariables.Effect == ActionEffect.Damage ||
                    attackVariables.Effect == ActionEffect.Heal)
                {
                    int gainedDef = Mathf.CeilToInt(attackVariables.ResolvedDamage * defenceStealFraction);

                    stats.AdjustTempDef(gainedDef);
                    view.HitAnimation(gainedDef, true, buffColor, false, EffectVisual.BuffDef);
                }
                break;

            case ClassSpecial.AutoHeal:
                if (attackVariables.Effect == ActionEffect.Heal)
                {
                    int healAmount = Mathf.CeilToInt(attackVariables.ResolvedDamage * autoHealHealthFraction);

                    stats.AdjustHealth(healAmount);
                    stats.CapHealth();

                    view.HitAnimation(healAmount, true, healColor, false, EffectVisual.Heal);
                }

                if (attackVariables.Effect == ActionEffect.Buff_Defence)
                {
                    int defGain = Mathf.CeilToInt(attackVariables.ResolvedDamage * autoHealDefenceFraction);

                    stats.AdjustTempDef(defGain);
                    view.HitAnimation(defGain, true, buffColor, false, EffectVisual.BuffDef);
                }
                break;
        }

        view.UpdateUI();
    }

    private void TriggerCounterAttack(string attackCaller, ActionEffect effect)
    {
        if (OnlineRelay.Instance.IsConnected() && !NetworkManager.Singleton.IsHost)
            return;

        // Ignore non-offensive effects
        switch (effect)
        {
            case ActionEffect.Spawn:
            case ActionEffect.Heal:
            case ActionEffect.SpecialSummon:
            case ActionEffect.Relocate:
            case ActionEffect.Buff_Damage:
            case ActionEffect.Buff_Defence:
            case ActionEffect.Protect:
            case ActionEffect.Regen:
            case ActionEffect.None:
                return;
        }

        int maxDamage = counterAttackDamageMultiplier * (stats.GetTotalDamageBonus() + 1);

        AttackVariables attackVars = new AttackVariables
        {
            TargetingMode = ActionTarget.Chosen,
            MinDamage = 0,
            MaxDamage = maxDamage,
            ResolvedDamage = 0,
            Effect = ActionEffect.Damage,
            IsPlayerAction = false,
            TargetId = attackCaller,
            AttackerId = transform.parent.parent.name,
            ImpactVisual = EffectVisual.Sword,
            CritMultiplier = stats.CritMultiplier,
            CritChance = stats.CritChance,
            IsAllyAiAction = false,
            ActionName = "Counter attack",
            ActionId = 0,
            ActionPointCost = 0,
        };

        AttackResolver.Instance.Attack(attackVars);
    }

    private void TriggerPoisionAttacked(AttackVariables attackVariables, Character callerChar)
    {
        if (OnlineRelay.Instance.IsConnected() && !NetworkManager.Singleton.IsHost)
            return;

        Debug.Log("Poison Attacked Triggered");

        if (attackVariables.Effect == ActionEffect.Poison) return;

        int maxDamage = poisonOnHitBaseDamage * (callerChar.Stats.GetTotalDamageBonus() + 1);

        AttackVariables attackVars = new AttackVariables
        {
            TargetingMode = ActionTarget.Chosen,
            MinDamage = 0,
            MaxDamage = maxDamage,
            ResolvedDamage = 0,
            Effect = ActionEffect.Poison,
            IsPlayerAction = false,
            TargetId = transform.parent.parent.name,
            AttackerId = attackVariables.AttackerId,
            ImpactVisual = EffectVisual.Sword,
            CritMultiplier = stats.CritMultiplier,
            CritChance = stats.CritChance,
            IsAllyAiAction = false,
            ActionName = "Poisoned",
            ActionPointCost = 0,
            ActionId = 0,
        };

        AttackResolver.Instance.Attack(attackVars);
    }

    public void TriggerDeath()
    {
        if (!stats.UndeadResistance)
        {
            view.DeathAni();

            bool isAlly;
            if (TryGetComponent<EnemyCharacter>(out EnemyCharacter enemy))
            {
                isAlly = enemy.EnemyAI.IsAiAlly;
            }
            else
            {
                isAlly = true;
            }

            PlayerStats.Instance.TriggerCharacterDeath(isAlly);
            Destroy(gameObject, 1f);
        }
        else
        {
            view.SpawnWordPopup(Localizer.Instance.GetLocalizedText("Undead Resistance"), Color.white);
            stats.SetHealth(1);
            stats.SetUndeadResistance(false);
        }
    }
}

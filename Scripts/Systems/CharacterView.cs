using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterView : MonoBehaviour
{
    #region Inspector: Core UI

    [Header("UI: Text & Sliders")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("Primary health bar.")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("Secondary health bar (e.g., mirrored or overlay).")]
    [SerializeField] private Slider healthSlider2;

    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI defenceText;
    [SerializeField] private TextMeshProUGUI defenceText2;
    [SerializeField] private TextMeshProUGUI damageBonusText;
    [SerializeField] private TextMeshProUGUI damageBonusText2;
    [SerializeField] private TextMeshProUGUI actionPointText;

    #endregion

    #region Inspector: Status Effects

    [Header("Status Effects (by index)")]
    [Tooltip("0: Regen, 1: Poison, 2: Zombification, 3: DefBuff, 4: DefDebuff, 5: DMGBuff, 6: DMGDebuff, 7: Protected, 8: Webbed")]
    [SerializeField] private Transform[] statusEffects;

    #endregion

    #region Inspector: Visuals & Audio

    [Header("Visuals & Audio")]
    [SerializeField] private Animator animator;

    [Tooltip("Component responsible for playing visual impact / spell effects.")]
    [SerializeField] private CharacterEffects effects;

    [Tooltip("Component responsible for playing character SFX (hit, attack, death, etc.).")]
    [SerializeField] private CharacterSoundEffectPlayer audioPlayer;

    #endregion

    #region Inspector: Popups

    [Header("Popup")]
    [Tooltip("Prefab used to show damage / heal popups above the character.")]
    [SerializeField] private GameObject damagePopupPrefab;

    [Tooltip("Sorting order applied to the popup's Canvas so it appears on top.")]
    [SerializeField] private int popupSortingOrder = 1000;

    #endregion

    #region Inspector: Portrait / Misc

    [Header("Portrait")]
    [Tooltip("Character portrait sprite.")]
    [SerializeField] private Image portraitImage;

    [Tooltip("UI marker shown when this character has ended its turn / is ready.")]
    public GameObject readyUp;

    #endregion

    #region Runtime

    private CharacterStats stats;

    #endregion

    #region Initialization

    /// <summary>
    /// Called with initial network-synced data to hook up portrait and initial health,
    /// then refresh the UI.
    /// </summary>
    public void SetUpCall(CharacterVariables variables)
    {
        stats = GetComponent<CharacterStats>();
        if (stats == null)
        {
            Debug.LogWarning($"[{nameof(CharacterView)}] No CharacterStats found on {name}. UI will not update correctly.", this);
            return;
        }

        // Portrait
        if (portraitImage != null &&
            variables.ImageIndex >= 0 &&
            LobbyAssets.Instance != null &&
            LobbyAssets.Instance.characterSprites != null &&
            variables.ImageIndex < LobbyAssets.Instance.characterSprites.Length)
        {
            portraitImage.sprite = LobbyAssets.Instance.characterSprites[variables.ImageIndex];
        }

        // Name (try localized, fall back to raw string)
        if (nameText != null)
        {
            string rawName = variables.Name;
            string localized = Localizer.Instance.GetLocalizedText(variables.Name);

            // If localization fails, keep original
            if (!string.IsNullOrEmpty(localized) &&
                !localized.Contains("No translation found for"))
            {
                nameText.text = localized;
            }
            else
            {
                nameText.text = rawName;
            }
        }

        // Health sliders
        if (healthSlider != null)
        {
            healthSlider.maxValue = stats.Health;
        }

        if (healthSlider2 != null)
        {
            healthSlider2.maxValue = stats.Health;
        }

        UpdateUI();
    }

    #endregion

    #region UI Refresh

    /// <summary>
    /// Refreshes all UI elements (health bars, defence, damage, AP, statuses).
    /// Call this any time the character's stats change.
    /// </summary>
    public void UpdateUI()
    {
        if (stats == null)
        {
            Debug.LogWarning($"[{nameof(CharacterView)}] UpdateUI called but CharacterStats is null on {name}.", this);
            return;
        }

        // Damage bonus
        int totalDamageBonus = stats.GetTotalDamageBonus();
        if (damageBonusText != null)
            damageBonusText.text = totalDamageBonus.ToString();
        if (damageBonusText2 != null)
            damageBonusText2.text = totalDamageBonus.ToString();

        // Defence
        int totalDefence = stats.GetTotalDefence();
        if (defenceText != null)
            defenceText.text = totalDefence.ToString();
        if (defenceText2 != null)
            defenceText2.text = totalDefence.ToString();

        // Health
        if (healthSlider != null)
        {
            healthSlider.value = stats.Health;
        }

        if (healthSlider2 != null)
        {
            healthSlider2.value = stats.Health;
        }

        if (healthText != null && healthSlider != null)
        {
            healthText.text = $"{stats.Health}/{healthSlider.maxValue}";
        }

        // AP
        if (actionPointText != null)
            actionPointText.text = stats.ActionPoints.ToString();

        // Status icons
        UpdateStatusEffects();
    }

    /// <summary>
    /// Shows/hides status effect icons based on the current <see cref="CharacterStats"/>.
    /// </summary>
    private void UpdateStatusEffects()
    {
        if (stats == null || statusEffects == null)
            return;

        // Defensive: ensure we have enough entries to avoid index errors
        if (statusEffects.Length < 9)
        {
            Debug.LogWarning(
                $"[{nameof(CharacterView)}] statusEffects length is {statusEffects.Length}, " +
                "but 9 entries (0-8) are expected.", this);
            return;
        }

        // 0 = Regen
        ToggleStatusWithValue(index: 0, isActive: stats.TempRegen > 0, stats.TempRegen);

        // 1 = Poison
        ToggleStatusWithValue(index: 1, isActive: stats.TempRegen < 0, stats.TempRegen);

        // 2 = Zombification
        ToggleStatusSimple(index: 2, stats.Zombified);

        // 3 = DefBuff
        ToggleStatusWithValue(index: 3, isActive: stats.TempDef > 0, stats.TempDef);

        // 4 = DefDebuff
        ToggleStatusWithValue(index: 4, isActive: stats.TempDef < 0, stats.TempDef);

        // 5 = DMGBuff
        ToggleStatusWithValue(index: 5, isActive: stats.TempDmg > 0, stats.TempDmg);

        // 6 = DMGDebuff
        ToggleStatusWithValue(index: 6, isActive: stats.TempDmg < 0, stats.TempDmg);

        // 7 = Protected
        ToggleStatusSimple(index: 7, !string.IsNullOrEmpty(stats.ProtectedBy));

        // 8 = Webbed (shows remaining AP as text)
        if (stats.Webbed)
        {
            statusEffects[8].gameObject.SetActive(true);
            var txt = statusEffects[8].GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = stats.ActionPoints.ToString();
            }
        }
        else
        {
            statusEffects[8].gameObject.SetActive(false);
        }
    }

    /// <summary>Helper for statuses that just show/hide without text.</summary>
    private void ToggleStatusSimple(int index, bool isActive)
    {
        var t = statusEffects[index];
        if (t != null)
        {
            t.gameObject.SetActive(isActive);
        }
    }

    /// <summary>Helper for statuses that also display an integer value.</summary>
    private void ToggleStatusWithValue(int index, bool isActive, int value)
    {
        var t = statusEffects[index];
        if (t == null) return;

        if (isActive)
        {
            t.gameObject.SetActive(true);
            var txt = t.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = value.ToString();
            }
        }
        else
        {
            t.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Damage / Word Popups

    private IEnumerator SpawnDamagePopupWithDelay(int damage, float delay, bool isHealing, Color color, bool crit)
    {
        if (PlayerStats.Instance == null)
        {
            Debug.LogWarning($"[{nameof(CharacterView)}] SpawnDamagePopupWithDelay called but PlayerStats.Instance is null.", this);
            yield break;
        }

        yield return new WaitForSeconds(delay / PlayerStats.Instance.playSpeed);
        SpawnDamagePopup(damage, isHealing, color, crit);
        UpdateUI();
    }

    /// <summary>
    /// Spawns a numeric damage/heal popup at this character's position.
    /// </summary>
    public void SpawnDamagePopup(int damage, bool isHealing, Color color, bool crit)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning($"[{nameof(CharacterView)}] No damagePopupPrefab assigned on {name}.", this);
            return;
        }

        Vector3 worldPos = transform.position;

        GameObject popup = Instantiate(damagePopupPrefab, transform);
        popup.transform.position = worldPos;

        // Ensure the popup renders on top
        Canvas popupCanvas = popup.GetComponent<Canvas>();
        if (popupCanvas == null)
        {
            popupCanvas = popup.AddComponent<Canvas>();
        }
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = popupSortingOrder;

        DamagePopup damagePopup = popup.GetComponent<DamagePopup>();
        if (damagePopup != null)
        {
            damagePopup.Setup(damage, isHealing, color, crit);
        }
        else
        {
            Debug.LogWarning($"[{nameof(CharacterView)}] DamagePopup script missing on popup prefab for {name}.", this);
        }
    }

    /// <summary>
    /// Spawns a word-based popup (e.g., 'Blocked', 'Immune') at this character's position.
    /// </summary>
    public void SpawnWordPopup(string word, Color color)
    {
        if (damagePopupPrefab == null)
        {
            Debug.LogWarning($"[{nameof(CharacterView)}] No damagePopupPrefab assigned on {name}.", this);
            return;
        }

        Vector3 worldPos = transform.position;

        GameObject popup = Instantiate(damagePopupPrefab, transform);
        popup.transform.position = worldPos;

        Canvas popupCanvas = popup.GetComponent<Canvas>();
        if (popupCanvas == null)
        {
            popupCanvas = popup.AddComponent<Canvas>();
        }
        popupCanvas.overrideSorting = true;
        popupCanvas.sortingOrder = popupSortingOrder;

        DamagePopup damagePopup = popup.GetComponent<DamagePopup>();
        if (damagePopup != null)
        {
            damagePopup.SetupWord(word, color);
        }
        else
        {
            Debug.LogWarning($"[{nameof(CharacterView)}] DamagePopup script missing on popup prefab for {name}.", this);
        }
    }

    #endregion

    #region Animations

    /// <summary>
    /// Plays hit animation, triggers impact VFX/SFX, then spawns a popup with a small delay.
    /// </summary>
    public void HitAnimation(int damage, bool isHealing, Color color, bool crit, EffectVisual impact)
    {
        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }

        if (effects != null)
        {
            effects.TriggerImpact(impact, crit);
        }

        StartCoroutine(SpawnDamagePopupWithDelay(damage, 0.4f, isHealing, color, crit));
    }

    /// <summary>
    /// Plays attack animation, attack SFX, and refreshes UI. Also updates player HUD
    /// if this is the local player's character.
    /// </summary>
    public void AttackAni(AttackVariables attackVariables)
    {
        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }

        if (audioPlayer != null)
        {
            switch (attackVariables.ImpactVisual)
            {
                case EffectVisual.Sword: audioPlayer.MeleeSound(); break;
                case EffectVisual.Arrow: audioPlayer.RangedSound(); break;
                case EffectVisual.Fire: audioPlayer.MagicSound(); break;
                case EffectVisual.Heal: audioPlayer.HealSound(); break;
                case EffectVisual.BuffDef: audioPlayer.BuffDefSound(); break;
                case EffectVisual.DebuffDef: audioPlayer.DebuffDefSound(); break;
            }
        }

        UpdateUI();

        // If this view belongs to the local player, update their HUD
        if (PlayerStats.Instance != null &&
            PlayerStats.Instance.myCharacter == this &&
            BattleHUDController.Instance != null)
        {
            BattleHUDController.Instance.UpdateGameActions();
        }
    }

    /// <summary>
    /// Plays death animation and sound.
    /// </summary>
    public void DeathAni()
    {
        if (audioPlayer != null)
        {
            audioPlayer.DeathSound();
        }

        if (animator != null)
        {
            animator.SetBool("Dead", true);
        }
    }

    #endregion
}

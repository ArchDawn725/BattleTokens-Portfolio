using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles visual and audio impact effects for a character:
/// - Overlay sprites (sword/arrow/fire/etc.)
/// - Screen shake and screen color flash
/// - Local character color flash
/// </summary>
public class CharacterEffects : MonoBehaviour
{
    #region Serialized Fields

    [Header("Audio & Visual Resources")]
    [Tooltip("Sprites used for different visual effects. Indexed by EffectVisual.")]
    [SerializeField] private Sprite[] sprites;

    [Header("UI References")]
    [Tooltip("Overlay Image used to display the effect sprite (sword, arrow, etc.).")]
    [SerializeField] private Image effectOverlay;

    [Tooltip("The main Image representing the character.")]
    [SerializeField] private Image character;

    [Tooltip("AudioSource that will play impact/hit sounds.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("ShakeController used for screen shake and color flash effects.")]
    [SerializeField] private ShakeController shakeCon;

    [Header("Audio Logic")]
    [Tooltip("Helper responsible for playing character-specific sound effects.")]
    [SerializeField] private CharacterSoundEffectPlayer audioPlayer;

    #endregion

    #region State

    private Color _originalColor;

    #endregion

    #region Unity Callbacks & Public API

    private void Start()
    {
        if (shakeCon == null && Camera.main != null)
        {
            shakeCon = Camera.main.GetComponent<ShakeController>();
        }

        if (character != null)
        {
            _originalColor = character.color;
        }
        else
        {
            Debug.LogWarning($"{nameof(CharacterEffects)} on '{name}' has no character Image assigned.", this);
        }

        if (effectOverlay == null)
        {
            Debug.LogWarning($"{nameof(CharacterEffects)} on '{name}' has no effectOverlay Image assigned.", this);
        }

        if (audioPlayer == null)
        {
            Debug.LogWarning($"{nameof(CharacterEffects)} on '{name}' has no CharacterSoundEffectPlayer assigned.", this);
        }
    }

    /// <summary>
    /// Triggers the appropriate visual and audio impact effects for the given effect type.
    /// </summary>
    /// <param name="effectType">Visual effect type (sword, arrow, fire, heal, etc.).</param>
    /// <param name="crit">Whether this is a critical hit (stronger visuals).</param>
    public void TriggerImpact(EffectVisual effectType, bool crit)
    {
        Sprite selectedSprite = null;
        AudioClip selectedAudioClip = null;
        Color effectColor = Color.red; // Default

        // Pick sprite, sound and color based on effect type.
        switch (effectType)
        {
            case EffectVisual.Sword:
                selectedSprite = sprites.Length > 0 ? sprites[0] : null;
                effectColor = Color.red;
                audioPlayer?.HitSound();
                break;

            case EffectVisual.Arrow:
                selectedSprite = sprites.Length > 1 ? sprites[1] : null;
                effectColor = Color.red;
                audioPlayer?.HitSound();
                break;

            case EffectVisual.Fire:
                selectedSprite = sprites.Length > 2 ? sprites[2] : null;
                effectColor = Color.red;
                audioPlayer?.HitSound();
                break;

            case EffectVisual.Heal:
                selectedSprite = sprites.Length > 3 ? sprites[3] : null;
                effectColor = Color.green;
                break;

            case EffectVisual.BuffDef:
                selectedSprite = sprites.Length > 4 ? sprites[4] : null;
                effectColor = Color.gray;
                break;

            case EffectVisual.DebuffDef:
                selectedSprite = sprites.Length > 5 ? sprites[5] : null;
                effectColor = Color.gray;
                break;

            default:
                Debug.LogWarning($"[{nameof(CharacterEffects)}] Unknown effect type: {effectType}", this);
                return;
        }

        if (selectedSprite == null && effectOverlay != null)
        {
            Debug.LogWarning($"[{nameof(CharacterEffects)}] No sprite found for effect {effectType}.", this);
        }

        StartCoroutine(PlayImpactEffect(selectedSprite, selectedAudioClip, effectColor, crit));
    }

    /// <summary>
    /// Plays overlay sprite, screen shakes, screen flash, and local color flash.
    /// </summary>
    private IEnumerator PlayImpactEffect(Sprite sprite, AudioClip audioClip, Color effectColor, bool crit)
    {
        var stats = PlayerStats.Instance;
        float playSpeed = 1f;

        if (stats != null)
        {
            playSpeed = Mathf.Max(0.01f, stats.playSpeed);
        }
        else
        {
            Debug.LogWarning($"{nameof(PlayerStats)} instance is null in {nameof(PlayImpactEffect)}(). Using default playSpeed of 1.", this);
        }

        if (effectOverlay != null)
        {
            effectOverlay.sprite = sprite;
        }

        // Small pre-impact delay
        yield return new WaitForSeconds(0.2f / playSpeed);

        if (audioClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(audioClip);
        }

        // Screen shake and screen color flash
        if (shakeCon != null)
        {
            // Base shake
            StartCoroutine(shakeCon.ScreenShake(0.15f, 2.5f));

            // Extra intensity if this is the local player's character slot
            var gridManager = GridManager.Instance;
            if (transform.parent != null &&
                transform.parent.parent != null &&
                gridManager != null &&
                transform.parent.parent.name == gridManager.MyPlayerLocation)
            {
                StartCoroutine(shakeCon.ScreenShake(0.2f, 5f));
                VibrationManager.Vibrate(0.8f, 0.3f);
                StartCoroutine(shakeCon.ColorFlash(effectColor));
            }

            // Extra shake for critical hits
            if (crit)
            {
                StartCoroutine(shakeCon.ScreenShake(0.25f, 10f));
            }
        }

        // Local character color flash
        StartCoroutine(LocalColorFlash(effectColor, playSpeed));

        // Fade in overlay
        yield return FadeOverlay(0f, 0.9f, 0.1f);

        // Hold overlay
        yield return new WaitForSeconds(0.15f / playSpeed);

        // Fade out overlay
        yield return FadeOverlay(0.9f, 0f, 0.15f);
    }

    /// <summary>
    /// Fades the overlay from startAlpha to endAlpha over the given duration.
    /// </summary>
    private IEnumerator FadeOverlay(float startAlpha, float endAlpha, float duration)
    {
        if (effectOverlay == null)
            yield break;

        float elapsed = 0f;
        Color initialColor = effectOverlay.color;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float alpha = Mathf.Lerp(startAlpha, endAlpha, t);
            effectOverlay.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);

            elapsed += Time.deltaTime;
            yield return null;
        }

        effectOverlay.color = new Color(initialColor.r, initialColor.g, initialColor.b, endAlpha);
    }

    /// <summary>
    /// Briefly flashes the character's image with a color, then returns to the original color.
    /// </summary>
    private IEnumerator LocalColorFlash(Color flashColor, float playSpeed)
    {
        if (character == null)
            yield break;

        // Delay before flash
        yield return new WaitForSeconds(0.1f / playSpeed);

        character.color = flashColor;

        // Flash duration
        yield return new WaitForSeconds(0.15f / playSpeed);

        character.color = _originalColor;
    }

    #endregion
}

public enum EffectVisual
{
    Sword,
    Arrow,
    Fire,
    Heal,
    BuffDef,
    DebuffDef,
}

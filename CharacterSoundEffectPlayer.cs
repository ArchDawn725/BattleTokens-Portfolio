using UnityEngine;

/// <summary>
/// Plays character-related sound effects (melee, ranged, magic, heal, buffs, hits, spawn, death).
/// Registers its AudioSource with the global SoundController for volume management.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class CharacterSoundEffectPlayer : MonoBehaviour
{
    #region Inspector

    [Header("Audio Source")]
    [Tooltip("AudioSource used to play all character sound effects.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Action Sounds")]
    [Tooltip("Clips for melee attacks.")]
    [SerializeField] private AudioClip[] melee;

    [Tooltip("Clips for ranged attacks.")]
    [SerializeField] private AudioClip[] ranged;

    [Tooltip("Clips for magic attacks.")]
    [SerializeField] private AudioClip[] magic;

    [Tooltip("Clips for healing actions.")]
    [SerializeField] private AudioClip[] heal;

    [Tooltip("Clips for defensive buffs.")]
    [SerializeField] private AudioClip[] buffDef;

    [Tooltip("Clips for defensive debuffs.")]
    [SerializeField] private AudioClip[] debuffDef;

    [Header("Impact & Lifecycle Sounds")]
    [Tooltip("Clips for when the character is hit.")]
    [SerializeField] private AudioClip[] hit;

    [Tooltip("Clips for spawn/entering the battlefield.")]
    [SerializeField] private AudioClip[] spawn;

    [Tooltip("Clips for death/despawn.")]
    [SerializeField] private AudioClip[] death;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError($"[{nameof(CharacterSoundEffectPlayer)}] No AudioSource found on '{name}'. Sounds will not play.");
                return;
            }
        }

        audioSource.playOnAwake = false;
    }

    private void Start()
    {
        if (SoundController.Instance != null && audioSource != null)
        {
            SoundController.Instance.SoundEffects.Add(audioSource);
            audioSource.volume = SoundController.Instance.soundEffectVolume;
        }
        else
        {
            Debug.LogWarning($"[{nameof(CharacterSoundEffectPlayer)}] SoundController.Instance or audioSource is null on '{name}'. Volume will not be managed globally.");
        }
    }

    private void OnEnable()
    {
        // Small delay so the spawn sound doesn't collide with other initial audio.
        Invoke(nameof(PlaySpawnDelayed), 0.2f);
    }

    private void OnDestroy()
    {
        if (SoundController.Instance != null && audioSource != null)
        {
            SoundController.Instance.SoundEffects.Remove(audioSource);
        }
    }

    #endregion

    #region Playback Helpers

    private void PlaySpawnDelayed()
    {
        PlayRandomClip(spawn);
    }

    private void PlayRandomClip(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        int index = Random.Range(0, clips.Length);
        AudioClip selected = clips[index];

        if (selected == null)
            return;

        audioSource.clip = selected;
        audioSource.Play();
    }

    #endregion

    #region Public API

    public void MeleeSound() => PlayRandomClip(melee);
    public void RangedSound() => PlayRandomClip(ranged);
    public void MagicSound() => PlayRandomClip(magic);
    public void HealSound() => PlayRandomClip(heal);
    public void BuffDefSound() => PlayRandomClip(buffDef);
    public void DebuffDefSound() => PlayRandomClip(debuffDef);
    public void HitSound() => PlayRandomClip(hit);
    public void DeathSound() => PlayRandomClip(death);

    #endregion
}

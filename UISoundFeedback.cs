using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Plays hover and click UI sounds in response to pointer/focus events,
/// and registers its AudioSource with the SoundController for global volume control.
/// </summary>
public class UISoundFeedback : MonoBehaviour,
                               IPointerEnterHandler,
                               ISelectHandler,
                               IPointerClickHandler,
                               ISubmitHandler
{
    #region Fields

    /// <summary>AudioSource used to play UI hover/click sounds.</summary>
    private AudioSource uiAudioSource;

    /// <summary>Cached reference to the global SoundController singleton.</summary>
    private SoundController soundController;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        soundController = SoundController.Instance;
        if (soundController == null)
        {
            Debug.LogError($"[{nameof(UISoundFeedback)}] SoundController.Instance is null. UI sounds will not play.");
            return;
        }

        // Try to reuse an existing AudioSource; add one if none is present.
        if (!TryGetComponent(out uiAudioSource))
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
        }

        uiAudioSource.playOnAwake = false;
        uiAudioSource.volume = soundController.soundEffectVolume;
        uiAudioSource.loop = false;
    }

    private void OnEnable()
    {
        if (soundController == null || uiAudioSource == null)
        {
            return;
        }

        // Ensure we only register once.
        if (!soundController.SoundEffects.Contains(uiAudioSource))
        {
            soundController.SoundEffects.Add(uiAudioSource);
        }

        uiAudioSource.volume = soundController.soundEffectVolume;
    }

    private void OnDisable()
    {
        if (soundController == null || uiAudioSource == null)
        {
            return;
        }

        if (soundController.SoundEffects.Contains(uiAudioSource))
        {
            soundController.SoundEffects.Remove(uiAudioSource);
        }
    }

    private void OnDestroy()
    {
        if (soundController == null || uiAudioSource == null)
        {
            return;
        }

        if (soundController.SoundEffects.Contains(uiAudioSource))
        {
            soundController.SoundEffects.Remove(uiAudioSource);
        }
    }

    #endregion

    #region UI Event Handlers

    public void OnPointerEnter(PointerEventData _) => PlayHover();

    public void OnSelect(BaseEventData _) => PlayHover();

    public void OnPointerClick(PointerEventData _) => PlayClick();

    public void OnSubmit(BaseEventData _) => PlayClick();

    #endregion

    #region Helpers

    private void PlayHover()
    {
        if (soundController == null || uiAudioSource == null)
        {
            return;
        }

        var clip = soundController.uIhover;
        if (clip == null)
        {
            Debug.LogWarning($"[{nameof(UISoundFeedback)}] Hover clip (uIhover) is not assigned on SoundController.");
            return;
        }

        uiAudioSource.clip = clip;
        uiAudioSource.Play();
    }

    private void PlayClick()
    {
        if (soundController == null || uiAudioSource == null)
        {
            return;
        }

        var clip = soundController.uIPress;
        if (clip == null)
        {
            Debug.LogWarning($"[{nameof(UISoundFeedback)}] Click clip (uIPress) is not assigned on SoundController.");
            return;
        }

        uiAudioSource.clip = clip;
        uiAudioSource.Play();
    }

    #endregion
}

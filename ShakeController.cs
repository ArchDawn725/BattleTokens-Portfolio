using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls simple screen shake and color flash effects, including resize handling.
/// </summary>
public class ShakeController : MonoBehaviour
{
    #region Serialized Fields

    [Header("References")]
    [Tooltip("Full-screen Image used to display flash/fade effects.")]
    [SerializeField] private Image screenCover;

    [Tooltip("Transform to shake (usually the camera or a UI root transform).")]
    [SerializeField] private Transform target;

    #endregion

    #region State

    private Vector3 _originalPosition;
    private Color _originalColor;

    private int _lastWidth;
    private int _lastHeight;

    #endregion

    #region Unity Callbacks & Public API

    private void OnEnable()
    {
        if (screenCover == null || target == null)
        {
            Debug.LogError($"{nameof(ShakeController)} on '{name}' is missing required references (screenCover or target).", this);
            return;
        }

        _originalColor = screenCover.color;
        _originalPosition = target.position;
    }

    private void Start()
    {
        _lastWidth = Screen.width;
        _lastHeight = Screen.height;
    }

    private void Update()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
        {
            OnScreenResize();

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
        }
    }

    /// <summary>
    /// Performs a screen shake effect by randomly offsetting the target position.
    /// </summary>
    /// <param name="duration">How long the shake should last (seconds).</param>
    /// <param name="magnitude">Maximum offset applied each frame.</param>
    public IEnumerator ScreenShake(float duration, float magnitude)
    {
        if (target == null)
        {
            Debug.LogWarning($"{nameof(ShakeController)} on '{name}' cannot shake because target is null.", this);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float offsetX = Random.Range(-magnitude, magnitude);
            float offsetY = Random.Range(-magnitude, magnitude);
            target.position = _originalPosition + new Vector3(offsetX, offsetY, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        target.position = _originalPosition;
    }

    /// <summary>
    /// Flashes the screen to the given color and then fades it out.
    /// </summary>
    /// <param name="flashColor">The target flash color (including alpha).</param>
    public IEnumerator ColorFlash(Color flashColor)
    {
        if (screenCover == null)
        {
            Debug.LogWarning($"{nameof(ShakeController)} on '{name}' cannot flash because screenCover is null.", this);
            yield break;
        }

        var stats = PlayerStats.Instance;
        float playSpeed = 1f;

        if (stats != null)
        {
            playSpeed = Mathf.Max(0.01f, stats.playSpeed);
        }
        else
        {
            Debug.LogWarning($"{nameof(PlayerStats)} instance is null in ColorFlash(). Using default playSpeed of 1.", this);
        }

        // Fade in
        float elapsed = 0f;
        const float fadeInDuration = 0.1f;

        while (elapsed < fadeInDuration)
        {
            float t = elapsed / fadeInDuration;
            float alpha = Mathf.Lerp(0f, flashColor.a, t);
            screenCover.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        screenCover.color = flashColor; // Ensure full flash color

        // Hold the color briefly (scaled by playSpeed)
        yield return new WaitForSeconds(fadeInDuration / playSpeed);

        // Fade out
        elapsed = 0f;
        const float fadeOutDuration = 0.15f;

        while (elapsed < fadeOutDuration)
        {
            float t = elapsed / fadeOutDuration;
            float alpha = Mathf.Lerp(flashColor.a, 0f, t);
            screenCover.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetEffects();
    }

    private void OnScreenResize()
    {
        // Re-cache original color/position on resize
        OnEnable();
        // Resize-handling logic can be extended here if needed.
    }

    /// <summary>
    /// Resets the screen cover and target transform to their original state.
    /// </summary>
    public void ResetEffects()
    {
        if (screenCover != null)
        {
            screenCover.color = _originalColor;
        }

        if (target != null)
        {
            target.position = _originalPosition;
        }
    }

    /// <summary>
    /// Disables the screen cover visual without affecting internal state.
    /// </summary>
    public void Disable()
    {
        if (screenCover == null)
        {
            Debug.LogWarning($"{nameof(ShakeController)} on '{name}' cannot disable because screenCover is null.", this);
            return;
        }

        screenCover.enabled = false;
    }

    /// <summary>
    /// Enables the screen cover visual without affecting internal state.
    /// </summary>
    public void Enable()
    {
        if (screenCover == null)
        {
            Debug.LogWarning($"{nameof(ShakeController)} on '{name}' cannot enable because screenCover is null.", this);
            return;
        }

        screenCover.enabled = true;
    }

    #endregion
}

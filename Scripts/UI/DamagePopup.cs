using TMPro;
using UnityEngine;

/// <summary>
/// Floating UI popup for damage/healing values or words.
/// - For numeric values, the popup launches in a ±launchCone from vertical,
///   scales up then down, and falls under gravity.
/// - For word popups, it remains static and only fades out.
/// </summary>
public class DamagePopup : MonoBehaviour
{
    #region Serialized Fields

    [Header("Lifetime")]
    [Min(0.01f)]
    [Tooltip("How long the popup should exist before being destroyed (seconds).")]
    [SerializeField] private float duration = 1.5f;

    [Header("Scale (for moving numeric popups)")]
    [Tooltip("Maximum scale reached in the middle of the animation.")]
    [SerializeField] private float growthScale = 1.5f;

    [Header("Motion (for moving numeric popups)")]
    [Tooltip("Initial launch speed in UI-space pixels per second.")]
    [SerializeField] private float launchSpeed = 120f;

    [Tooltip("Gravity applied in UI-space pixels per second squared (negative to fall down).")]
    [SerializeField] private float gravity = -500f;

    [Tooltip("Maximum angle in degrees from straight up that the popup can launch.")]
    [SerializeField] private float launchCone = 45f;

    [Header("UI")]
    [Tooltip("Text component used to display the damage/heal value or word.")]
    [SerializeField] private TextMeshProUGUI damageText;

    #endregion

    #region State

    // UI-space velocity (pixels / second)
    private Vector3 _velocity;
    private float _elapsed;
    private Color _startColor;
    private bool _isMoving = true;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (damageText == null)
        {
            damageText = GetComponentInChildren<TextMeshProUGUI>();
            if (damageText == null)
            {
                Debug.LogError($"{nameof(DamagePopup)} on '{name}' is missing a TextMeshProUGUI reference.", this);
            }
        }
    }

    private void Update()
    {
        if (duration <= 0f)
        {
            Debug.LogWarning($"{nameof(DamagePopup)} on '{name}' has a non-positive duration; destroying immediately.", this);
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;

        // Non-moving (word) popups progress through their lifetime faster (same behavior as original double-increment).
        float lifetimeFactor = _isMoving ? 1f : 1.5f;
        _elapsed += dt * lifetimeFactor;

        float t = Mathf.Clamp01(_elapsed / duration); // Normalize 0–1

        if (_isMoving)
        {
            // 1. Motion with gravity (parabolic path)
            _velocity.y += gravity * dt;                 // apply gravity
            transform.localPosition += _velocity * dt;   // move

            // 2. Scale animation: grow then shrink over lifetime
            float scale = t < 0.5f
                ? Mathf.Lerp(0.5f, growthScale, t * 2f)              // grow
                : Mathf.Lerp(growthScale, 0.5f, (t - 0.5f) * 2f);    // shrink

            transform.localScale = Vector3.one * scale;
        }

        // 3. Fade out over time
        float alpha = Mathf.Lerp(1f, 0.25f, t);
        damageText.color = new Color(_startColor.r, _startColor.g, _startColor.b, alpha);

        // 4. Destroy when time is up
        if (_elapsed >= duration)
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Configures the popup for a numeric damage or heal value.
    /// </summary>
    /// <param name="value">The raw damage/heal amount.</param>
    /// <param name="isHealing">If true, shows as healing (+); otherwise damage (-).</param>
    /// <param name="color">Base text color (before fading).</param>
    /// <param name="crit">If true, appends a critical mark (e.g., "!").</param>
    public void Setup(int value, bool isHealing, Color color, bool crit)
    {
        if (damageText == null)
        {
            Debug.LogWarning($"{nameof(DamagePopup)} on '{name}' cannot be set up because damageText is missing.", this);
            return;
        }

        string critMark = crit ? "!" : string.Empty;
        damageText.text = (isHealing ? "+" : "-") + Mathf.Abs(value) + critMark;
        damageText.color = color;
        _startColor = color;

        // Pick a random launch direction inside ±launchCone from vertical
        float angleRad = Random.Range(-launchCone, launchCone) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad)); // unit vector
        _velocity = dir * launchSpeed;

        _elapsed = 0f;
        _isMoving = true;
        transform.localScale = Vector3.one; // reset scale in case of reuse
    }

    /// <summary>
    /// Configures the popup to display a static word (no motion, just fade).
    /// </summary>
    /// <param name="value">The text to display.</param>
    /// <param name="color">Base text color (before fading).</param>
    public void SetupWord(string value, Color color)
    {
        if (damageText == null)
        {
            Debug.LogWarning($"{nameof(DamagePopup)} on '{name}' cannot be set up because damageText is missing.", this);
            return;
        }

        damageText.text = value;
        damageText.color = color;
        _startColor = color;

        _velocity = Vector3.zero;
        _elapsed = 0f;
        _isMoving = false;
        transform.localScale = Vector3.one; // static, but keep a sane default scale
    }

    #endregion
}

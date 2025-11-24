using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animates an experience (XP) slider and a corresponding gold text display.
/// Used for end-of-quest / reward screens to show XP gain over time.
/// </summary>
public class XPSlider : MonoBehaviour
{
    #region Serialized Fields

    [Header("UI References")]
    [Tooltip("Slider that visually represents the XP progression.")]
    [SerializeField] private Slider _slider;

    [Tooltip("Text used to display the accumulated gold or XP reward (e.g., +120).")]
    [SerializeField] private TextMeshProUGUI _goldText;

    [Tooltip("Exit/continue button that becomes interactable when the animation is done.")]
    [SerializeField] private Button _exitButton;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        if (_slider == null)
        {
            _slider = GetComponent<Slider>();
            if (_slider == null)
            {
                Debug.LogError($"{nameof(XPSlider)} on '{name}' is missing a Slider reference.", this);
            }
        }

        if (_exitButton == null)
        {
            Debug.LogWarning($"{nameof(XPSlider)} on '{name}' has no exit button assigned.", this);
        }

        if (_goldText == null)
        {
            Debug.LogWarning($"{nameof(XPSlider)} on '{name}' has no gold text assigned.", this);
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Animates the XP slider and gold text over time.
    /// </summary>
    /// <param name="minAmount">Minimum value of the slider range.</param>
    /// <param name="maxAmount">Maximum value of the slider range.</param>
    /// <param name="startingValue">Initial slider value at the start of the animation.</param>
    /// <param name="amountToAdd">Total amount to add to the slider over the animation.</param>
    /// <param name="duration">Approximate duration (in seconds) before playSpeed adjustment.</param>
    public IEnumerator StartSlider(float minAmount, float maxAmount, float startingValue, float amountToAdd, float duration = 2f)
    {
        if (_slider == null)
        {
            Debug.LogError($"{nameof(XPSlider)} on '{name}' cannot start slider animation because Slider is missing.", this);
            yield break;
        }

        // Failsafe in case something goes wrong and the coroutine never completes
        Invoke(nameof(DelayFailsafe), duration * 2f);

        Debug.Log($"[{nameof(XPSlider)}] Min Amount: {minAmount}");

        if (Mathf.Approximately(minAmount, maxAmount))
        {
            minAmount--;
        }

        var stats = PlayerStats.Instance;
        float playSpeed = 1f;
        if (stats != null)
        {
            playSpeed = Mathf.Max(0.01f, stats.playSpeed);
        }
        else
        {
            Debug.LogWarning($"{nameof(PlayerStats)} instance is null in {nameof(StartSlider)}(). Using default playSpeed of 1.", this);
        }

        duration /= playSpeed;

        // Initialize slider range and starting value
        _slider.minValue = minAmount;
        _slider.maxValue = maxAmount;
        _slider.value = Mathf.Clamp(startingValue, minAmount, maxAmount);

        float remaining = Mathf.Max(0f, amountToAdd);
        if (remaining <= 0f)
        {
            if (_goldText != null)
            {
                _goldText.text = "+0";
            }

            if (_exitButton != null)
            {
                _exitButton.interactable = true;
            }

            yield break;
        }

        // Determine constant speed based on desired duration
        float speed = duration > 0f ? amountToAdd / duration : amountToAdd;
        float shownGold = 0f;

        // Initial delay before the slider begins moving
        yield return new WaitForSeconds(2.5f / playSpeed);

        while (remaining > 0f)
        {
            // If the current level bar is full, trigger level-up handling and continue
            if (_slider.value >= _slider.maxValue - 0.0001f)
            {
                var rewardUI = RewardUIController.Instance;
                if (rewardUI != null)
                {
                    rewardUI.UpdateGameOverUI(_slider.maxValue);
                }
                else
                {
                    Debug.LogWarning($"{nameof(RewardUIController)} instance is null in {nameof(StartSlider)}(). Level-up UI will not update.", this);
                }

                // Normalize to new range start to avoid epsilon accumulation
                _slider.value = _slider.minValue;
                continue;
            }

            // Amount that can be added this frame
            float space = _slider.maxValue - _slider.value;
            float step = Mathf.Min(space, remaining, speed * Time.deltaTime);

            _slider.value += step;
            remaining -= step;
            shownGold += step;

            if (_goldText != null)
            {
                _goldText.text = "+" + Mathf.Round(shownGold);
            }

            yield return null;
        }

        // Snap final text
        if (_goldText != null)
        {
            _goldText.text = "+" + Mathf.Round(shownGold);
        }

        if (_exitButton != null)
        {
            _exitButton.interactable = true;
        }

        Debug.Log($"[{nameof(XPSlider)}] Final Slider Value: {_slider.value}");
    }

    /// <summary>
    /// Updates the slider range when a new level is reached.
    /// </summary>
    /// <param name="newMax">New maximum value for the slider.</param>
    public void UpdateSlider(float newMax)
    {
        if (_slider == null)
        {
            _slider = GetComponent<Slider>();
            if (_slider == null)
            {
                Debug.LogError($"{nameof(XPSlider)} on '{name}' cannot update slider range because Slider is missing.", this);
                return;
            }
        }

        float previousMax = _slider.maxValue;
        _slider.minValue = previousMax;
        _slider.maxValue = newMax;
        _slider.value = _slider.minValue; // start new level at 0 progress
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Failsafe to ensure the exit button becomes interactable if the animation stalls.
    /// </summary>
    private void DelayFailsafe()
    {
        if (_exitButton != null)
        {
            _exitButton.interactable = true;
        }
    }

    #endregion
}

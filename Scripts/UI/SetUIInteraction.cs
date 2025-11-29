using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles UI navigation when this button is pressed, optionally
/// moving selection to a specified element and triggering its visual border.
/// </summary>
[RequireComponent(typeof(Button))]
public class SetUIInteraction : MonoBehaviour
{
    #region Fields

    [Header("Setup")]
    [Tooltip("EventSystem used for setting the currently selected UI object.")]
    [SerializeField] private EventSystem eventSystem;

    [Tooltip("UI element that should receive selection focus when this object is activated.")]
    [SerializeField] private Selectable elementToSelect;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Cache or auto-assign EventSystem if not explicitly set
        if (eventSystem == null)
        {
            eventSystem = EventSystem.current ?? FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogWarning($"[{nameof(SetUIInteraction)}] No EventSystem found in scene. Navigation will be disabled on '{name}'.");
            }
        }

        Button button = GetComponent<Button>();
        if (button == null)
        {
            Debug.LogWarning($"[{nameof(SetUIInteraction)}] Button component missing on '{name}' despite RequireComponent. Interaction will not work.");
            return;
        }

        button.onClick.AddListener(JumpToElement);
    }

    #endregion

    #region Interaction

    /// <summary>
    /// Attempts to move selection to the configured element and activate its border effect.
    /// </summary>
    public void JumpToElement()
    {
        if (elementToSelect == null)
        {
            Debug.LogWarning($"[{nameof(SetUIInteraction)}] No elementToSelect assigned on '{name}'. JumpToElement will do nothing.");
            return;
        }

#if !UNITY_ANDROID && !UNITY_IOS
        // Only control keyboard/controller selection if mouse is hidden
        if (InputSwitcher.Instance != null && !InputSwitcher.Instance.isMouseVisible && eventSystem != null)
        {
            eventSystem.SetSelectedGameObject(elementToSelect.gameObject);
        }
#endif

        if (elementToSelect.TryGetComponent(out BorderOnSelect border))
        {
            border.Activate();
        }
    }

    #endregion
}

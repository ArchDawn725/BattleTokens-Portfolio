using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Central controller for keyboard/controller UI navigation.
/// Moves selection to a given Selectable and optionally triggers its border effect.
/// </summary>
public class UINavigationController : MonoBehaviour
{
    #region Singleton & Fields

    public static UINavigationController Instance { get; private set; }

    [Header("Event System")]
    [Tooltip("EventSystem used for setting the currently selected UI object. " +
             "If left empty, it will be auto-assigned at runtime.")]
    [SerializeField] private EventSystem eventSystem;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(UINavigationController)}] Duplicate instance detected on '{name}'. Destroying this instance.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Auto-assign if not set in inspector
        if (eventSystem == null)
        {
            eventSystem = EventSystem.current ?? FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogWarning($"[{nameof(UINavigationController)}] No EventSystem found in scene. Navigation will be disabled.");
            }
        }
    }

    #endregion

    #region Navigation

    /// <summary>
    /// Moves UI selection focus to the given element and activates its border highlight (if present).
    /// </summary>
    /// <param name="elementToSelect">Selectable UI element to focus.</param>
    public void JumpToElement(Selectable elementToSelect)
    {
        if (elementToSelect == null)
        {
            Debug.LogWarning($"[{nameof(UINavigationController)}] JumpToElement called with null Selectable.");
            return;
        }

        if (eventSystem == null)
        {
            eventSystem = EventSystem.current ?? FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogWarning($"[{nameof(UINavigationController)}] No EventSystem available; cannot set selected object.");
                goto BorderCheck;
            }
        }

#if !UNITY_ANDROID && !UNITY_IOS
        if (InputSwitcher.Instance == null)
        {
            Debug.LogWarning($"[{nameof(UINavigationController)}] InputSwitcher.Instance is null; cannot check mouse visibility.");
        }
        else if (!InputSwitcher.Instance.isMouseVisible)
        {
            eventSystem.SetSelectedGameObject(elementToSelect.gameObject);
        }
#endif

        BorderCheck:
        if (elementToSelect.TryGetComponent(out BorderOnSelect border))
        {
            border.Activate();
        }
    }

    #endregion
}

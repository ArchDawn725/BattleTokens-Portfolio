using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InputSwitcher : MonoBehaviour
{
    #region Singleton & Fields

    public static InputSwitcher Instance { get; private set; }

    [Header("State")]
    [Tooltip("True when mouse input is currently considered active/visible.")]
    public bool isMouseVisible { get; private set; } = true;

    [Tooltip("Current scene index used to select a default UI element when switching to keyboard/controller.")]
    public int sceneIndex;

    [Tooltip("Whether keyboard input should be ignored because the player is typing into a field.")]
    public bool typing;

    [Header("Mouse Detection")]
    [Tooltip("Minimum movement magnitude required to register mouse movement.")]
    [SerializeField] private float mouseMoveThreshold = 0.1f;

    private Vector3 lastMousePosition;

    [Header("Keyboard Detection")]
    [Tooltip("Keys that are ignored when detecting keyboard-based navigation.")]
    [SerializeField]
    private KeyCode[] ignoredKeys =
    {
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Return,
        KeyCode.KeypadEnter,
        KeyCode.Escape
    };

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(InputSwitcher)}] Duplicate instance detected on '{name}'. Destroying this component.");
            Destroy(this);
            return;
        }

        Instance = this;
        lastMousePosition = Input.mousePosition;
    }

    private void Start()
    {
#if UNITY_ANDROID || UNITY_IOS
        SetMouseVisibility(false);
#endif
    }

    private void Update()
    {
#if UNITY_ANDROID || UNITY_IOS
        // Input switching is disabled on mobile platforms.
#else
        DetectMouseMovement();
        DetectKeyboardOrControllerInput();
#endif
    }

    #endregion

    #region Detection

    private void DetectMouseMovement()
    {
        Vector3 delta = Input.mousePosition - lastMousePosition;
        if (delta.magnitude > mouseMoveThreshold)
        {
            SetMouseVisibility(true);
        }

        lastMousePosition = Input.mousePosition;
    }

    private void DetectKeyboardOrControllerInput()
    {
        if (IsKeyboardInput() || IsControllerInput())
        {
            SetMouseVisibility(false);
        }
    }

    private bool IsKeyboardInput()
    {
        // Skip all keyboard detection while typing into UI.
        if (typing)
        {
            return false;
        }

        // Loop through all KeyCodes to detect any navigation input.
        foreach (KeyCode code in System.Enum.GetValues(typeof(KeyCode)))
        {
            // Skip mouse buttons.
            if (code >= KeyCode.Mouse0 && code <= KeyCode.Mouse6)
            {
                continue;
            }

            // Skip ignored keys.
            if (System.Array.Exists(ignoredKeys, k => k == code))
            {
                continue;
            }

            if (Input.GetKeyDown(code))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsControllerInput()
    {
        return Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f ||
               Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f ||
               Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.1f ||
               Mathf.Abs(Input.GetAxis("Submit")) > 0.1f ||
               Mathf.Abs(Input.GetAxis("Cancel")) > 0.1f;
    }

    #endregion

    #region Helpers

    private void SetMouseVisibility(bool visible)
    {
        if (isMouseVisible == visible)
        {
            return;
        }

        isMouseVisible = visible;
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;

        if (visible)
        {
            // Clear the currently selected UI element when switching back to mouse.
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
        else
        {
            // Switch focus to a default selectable when switching to keyboard/controller.
            if (UINavigationController.Instance == null)
            {
                Debug.LogWarning($"[{nameof(InputSwitcher)}] UINavigationController.Instance is null; cannot move selection.");
                return;
            }

            if (Tutorial.Singleton == null)
            {
                Debug.LogWarning($"[{nameof(InputSwitcher)}] Tutorial.Singleton is null; cannot resolve selectables for scene index {sceneIndex}.");
                return;
            }

            var selectables = Tutorial.Singleton.selectables;
            if (selectables == null || sceneIndex < 0 || sceneIndex >= selectables.Length)
            {
                Debug.LogWarning($"[{nameof(InputSwitcher)}] No selectable configured for scene index {sceneIndex}.");
                return;
            }

            UINavigationController.Instance.JumpToElement(selectables[sceneIndex]);
        }
    }

    #endregion
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AdditionalControls : MonoBehaviour
{
    #region Constants

    // Old Input Manager axis names
    private const string submitAxis = "Submit";
    private const string backAxis = "Back";
    private const string menuAxis = "Menu";

    // Extra keyboard keys for "back"
    private static readonly KeyCode[] backKeys =
    {
        KeyCode.Tab
    };

    // Extra keyboard keys for "menu"
    private static readonly KeyCode[] menuKeys =
    {
        KeyCode.Escape,
        KeyCode.JoystickButton7,   // Start button on most controllers
        KeyCode.Joystick1Button7,  // Player 1
        KeyCode.JoystickButton9    // Options on some controllers
    };

    #endregion

    #region Inspector

    [Header("Shortcut Buttons")]
    [Tooltip("Buttons triggered by numeric keys 0-9 (top row and keypad). Index = key number.")]
    [SerializeField] private Button[] shortCutkeys;

    [Header("Screen Mode")]
    [Tooltip("Determines which set of additional controls are active.")]
    [SerializeField] private ScreenMode screenMode;

    [Header("Setup Screen")]
    [Tooltip("Button to invoke when 'Back' is pressed on Setup/Play screens.")]
    [SerializeField] private Button backButton;

    [Tooltip("Code passed to Tutorial.PauseMenu when the menu input is pressed.")]
    [SerializeField] private int menuId;

    #endregion

    #region Types & Fields

    private enum ScreenMode
    {
        Board,
        Play,
        Setup
    }

    private Tutorial tutorial;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        tutorial = Tutorial.Singleton;
        if (tutorial == null)
        {
            Debug.LogWarning($"[{nameof(AdditionalControls)}] Tutorial.Singleton not found. Menu actions will be disabled.", this);
        }
    }

    private void Update()
    {
        // Screen-specific controls
        switch (screenMode)
        {
            case ScreenMode.Board:
                BoardOptions();
                break;

            case ScreenMode.Play:
            case ScreenMode.Setup:
                SetUpOptions();
                break;
        }

        // Numeric shortcuts 0-9 (top row and keypad)
        for (int numericKey = 0; numericKey <= 9; numericKey++)
        {
            KeyCode rowKey = (KeyCode)((int)KeyCode.Alpha0 + numericKey);
            KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad0 + numericKey);

            if (Input.GetKeyDown(rowKey) || Input.GetKeyDown(keypadKey))
            {
                SelectSlot(numericKey);
            }
        }
    }

    #endregion

    #region Screen Logic

    /// <summary>
    /// Trigger a shortcut button at the given index if it exists and is interactable.
    /// </summary>
    private void SelectSlot(int index)
    {
        if (shortCutkeys == null || index < 0 || index >= shortCutkeys.Length)
        {
            return;
        }

        Button shortcut = shortCutkeys[index];
        if (shortcut != null && shortcut.interactable)
        {
            shortcut.onClick.Invoke();
        }
    }

    /// <summary>
    /// Handles board-specific controls (Submit on board tiles, etc.).
    /// </summary>
    private void BoardOptions()
    {
        if (!PressedSubmit())
            return;

        // Current highlighted/focused UI object
        GameObject current = EventSystem.current?.currentSelectedGameObject;
        if (current == null)
            return;

        var selectable = current.GetComponent<Selectable>();

        // If selection exists but is not interactable, show info if possible
        if (selectable != null && !selectable.IsInteractable())
        {
            // Defensive child checks to avoid hierarchy-based exceptions
            Transform t = selectable.transform;
            if (t.childCount > 2)
            {
                Transform child2 = t.GetChild(2);
                if (child2.childCount > 0)
                {
                    Transform child0 = child2.GetChild(0);
                    if (child0.childCount > 5)
                    {
                        var infoTransform = child0.GetChild(5);
                        if (infoTransform.TryGetComponent(out CharacterInfo info))
                        {
                            info.ShowInfo();
                            return;
                        }
                    }
                }
            }

            Debug.Log("No CharacterInfo component found for board selection.");
            return;
        }

        // If nothing valid or interactable was found
        Debug.Log($"Nothing interactable selected (or disabled): {current?.name}");
    }

    /// <summary>
    /// Handles controls on Setup/Play screens (menu/back shortcuts).
    /// </summary>
    private void SetUpOptions()
    {
        if (PressedMenu())
        {
            if (tutorial != null)
            {
                tutorial.PauseMenu(menuId);
            }
            else
            {
                Debug.LogWarning($"[{nameof(AdditionalControls)}] Menu input received but Tutorial is missing.", this);
            }
        }

        if (PressedBack())
        {
            // Prefer invoking the configured back button if possible
            if (backButton != null)
            {
                if (backButton.interactable)
                {
                    backButton.onClick.Invoke();
                }
            }
            else
            {
                // Fallback: treat as a menu press if no back button is configured
                if (tutorial != null)
                {
                    tutorial.PauseMenu(menuId);
                }
            }
        }
    }

    #endregion

    #region Input Helpers

    /// <summary>
    /// True if Return / Enter or any "Submit" axis button is pressed this frame.
    /// </summary>
    private bool PressedSubmit()
    {
        // Old Input Manager axis/button
        if (Input.GetButtonDown(submitAxis))
            return true;

        // Extra keyboard keys
        if (extraKeys != null)
        {
            foreach (var key in extraKeys)
            {
                if (Input.GetKeyDown(key))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True this frame if the user pressed Back (Tab or the configured "Back" axis).
    /// </summary>
    private static bool PressedBack()
    {
        if (Input.GetButtonDown(backAxis))
            return true;

        foreach (var key in backKeys)
        {
            if (Input.GetKeyDown(key))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True this frame if the user pressed Menu (Escape, Start, Options, or the configured "Menu" axis).
    /// </summary>
    private static bool PressedMenu()
    {
        if (Input.GetButtonDown(menuAxis))
            return true;

        foreach (var key in menuKeys)
        {
            if (Input.GetKeyDown(key))
                return true;
        }

        return false;
    }

    [Header("Submit Extra Keys")]
    [Tooltip("Additional keys treated as 'Submit' besides the axis mapping.")]
    [SerializeField]
    private KeyCode[] extraKeys =
    {
        KeyCode.Return,
        KeyCode.KeypadEnter
    };

    #endregion
}

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Extends a standard <see cref="Button"/> to support different callbacks
/// for left-click, right-click, and "Submit" (keyboard/controller) input.
/// </summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class SplitClickInvoker : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    #region Inspector Fields

    [Header("Click Events")]

    [SerializeField]
    [Tooltip("Invoked when the button is left-clicked or activated via Submit (keyboard/controller).")]
    private UnityEvent onLeftClick;

    [SerializeField]
    [Tooltip("Invoked when the button is right-clicked with the mouse.")]
    private UnityEvent onRightClick;

    [Header("Runtime References")]

    [SerializeField]
    [Tooltip("Button component on this GameObject. Auto-assigned in Awake if left null.")]
    private Button button;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Ensure we have a valid Button reference
        if (button == null)
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError($"[{nameof(SplitClickInvoker)}] No Button component found on {name}. Split-click handling will not work.");
            }
        }
    }

    #endregion

    #region EventSystem Handlers

    /// <summary>
    /// Handles mouse clicks and dispatches left/right click events.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (button == null || !button.interactable)
            return;

        switch (eventData.button)
        {
            case PointerEventData.InputButton.Left:
                onLeftClick?.Invoke();
                break;

            case PointerEventData.InputButton.Right:
                onRightClick?.Invoke();
                break;

            // Intentionally ignore middle mouse button
            default:
                break;
        }
    }

    /// <summary>
    /// Treats keyboard/controller "Submit" as a left click.
    /// </summary>
    public void OnSubmit(BaseEventData eventData)
    {
        if (button == null || !button.interactable)
            return;

        onLeftClick?.Invoke();
    }

    #endregion

    #region Runtime Wiring API

    /// <summary>Adds a listener to the left-click (and Submit) event.</summary>
    public void AddLeftListener(UnityAction listener)
    {
        onLeftClick.AddListener(listener);
    }

    /// <summary>Removes a listener from the left-click (and Submit) event.</summary>
    public void RemoveLeftListener(UnityAction listener)
    {
        onLeftClick.RemoveListener(listener);
    }

    /// <summary>Adds a listener to the right-click event.</summary>
    public void AddRightListener(UnityAction listener)
    {
        onRightClick.AddListener(listener);
    }

    /// <summary>Removes a listener from the right-click event.</summary>
    public void RemoveRightListener(UnityAction listener)
    {
        onRightClick.RemoveListener(listener);
    }

    #endregion
}

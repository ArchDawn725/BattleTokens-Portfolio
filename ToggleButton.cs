using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple toggle button that enables/disables a target Image
/// when this button is clicked.
/// </summary>
[RequireComponent(typeof(Button))]
public class ToggleButton : MonoBehaviour
{
    #region Inspector

    [Header("Toggle Target")]
    [Tooltip("Image whose visibility will be toggled when the button is pressed.")]
    [SerializeField] private Image toggleImage;

    #endregion

    #region Unity Events

    private void Awake()
    {
        // Cache reference and prevent silent failure if image wasn't assigned.
        if (toggleImage == null)
        {
            Debug.LogWarning($"{nameof(toggleImage)} is not assigned on {name}. Toggle will do nothing.");
        }
    }

    private void Start()
    {
        // Ensure button exists and subscribe once
        Button btn = GetComponent<Button>();
        btn.onClick.AddListener(OnTogglePressed);
    }

    #endregion

    #region Toggle Logic

    /// <summary>
    /// Toggles the enabled state of the target Image.
    /// </summary>
    public void OnTogglePressed()
    {
        if (toggleImage == null)
            return;

        toggleImage.enabled = !toggleImage.enabled;
    }

    #endregion
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shows a highlight border when this Selectable is focused or hovered,
/// and hides it when focus/hover is lost.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class BorderOnSelect : MonoBehaviour,
                               ISelectHandler,
                               IDeselectHandler,
                               IPointerEnterHandler,
                               IPointerExitHandler
{
    #region Fields

    [Header("Highlight")]
    [Tooltip("GameObject that represents the highlight border to toggle on selection/hover.")]
    [SerializeField] private GameObject highlightBorder;

    [Tooltip("If true, hovering with the pointer will also show the border.")]
    [SerializeField] private bool showOnPointerHover = true;

    [Tooltip("If true, this element represents a tile and will drive CharacterInfo hover logic.")]
    [SerializeField] private bool tile;

    [Tooltip("Optional direct reference to the CharacterInfo used when this is a tile. " +
             "If left empty, a fallback child-search will be used.")]
    [SerializeField] private CharacterInfo tileCharacterInfo;

    // Cached references
    private InputSwitcher inputSwitcher;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        inputSwitcher = InputSwitcher.Instance;

        if (highlightBorder == null)
        {
            Debug.LogWarning($"[{nameof(BorderOnSelect)}] No highlightBorder assigned on '{name}'. Border visuals will not be shown.");
        }
    }

    #endregion

    #region Event Handlers (Desktop / Mobile)

    // Called by EventSystem when this element becomes the current selection
    public void OnSelect(BaseEventData eventData)
    {
#if !UNITY_ANDROID && !UNITY_IOS
        bool usingKeyboardNav = inputSwitcher != null && !inputSwitcher.isMouseVisible;
        if (usingKeyboardNav)
        {
            ShowBorder();
        }
#else
        ShowBorder();
#endif
    }

    // Called when selection moves away
    public void OnDeselect(BaseEventData eventData)
    {
        HideBorder();
    }

    // Optional: make hover with the mouse also show the border
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!showOnPointerHover || highlightBorder == null)
        {
            return;
        }

#if !UNITY_ANDROID && !UNITY_IOS
        if (inputSwitcher != null && !inputSwitcher.isMouseVisible)
        {
            ShowBorder();
        }
#else
        ShowBorder();
#endif
    }

    // Hide on pointer exit – but only if this element is not still selected
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!showOnPointerHover || highlightBorder == null)
        {
            return;
        }

        if (EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
        {
            return;
        }

        HideBorder();
    }

    /// <summary>
    /// Explicit activation entry point for navigation controllers.
    /// </summary>
    public void Activate()
    {
#if !UNITY_ANDROID && !UNITY_IOS
        if (inputSwitcher != null && !inputSwitcher.isMouseVisible)
        {
            ShowBorder();
        }
#else
        ShowBorder();
#endif
    }

    #endregion

    #region Helpers

    private void ShowBorder()
    {
        if (highlightBorder != null)
        {
            highlightBorder.SetActive(true);
        }

        if (tile)
        {
            TriggerCharacterInfoEnter();
        }
    }

    private void HideBorder()
    {
        if (highlightBorder != null)
        {
            highlightBorder.SetActive(false);
        }

        if (tile)
        {
            TriggerCharacterInfoExit();
        }
    }

    private void TriggerCharacterInfoEnter()
    {
        CharacterInfo info = ResolveCharacterInfo();
        if (info != null)
        {
            info.OnPointerEnter(null);
        }
    }

    private void TriggerCharacterInfoExit()
    {
        CharacterInfo info = ResolveCharacterInfo();
        if (info != null)
        {
            info.OnPointerExit(null);
        }
    }

    /// <summary>
    /// Attempts to resolve the CharacterInfo reference for tile behaviour.
    /// Uses the serialized reference if present; otherwise falls back to the original child-chain.
    /// </summary>
    private CharacterInfo ResolveCharacterInfo()
    {
        if (!tile)
        {
            return null;
        }

        if (tileCharacterInfo != null)
        {
            return tileCharacterInfo;
        }

        // Fallback to the original hierarchy path: transform.GetChild(2).GetChild(0).GetChild(5)
        // Guarded with bounds checks to avoid exceptions.
        if (transform.childCount <= 2)
        {
            return null;
        }

        Transform level2 = transform.GetChild(2);
        if (level2.childCount <= 0)
        {
            return null;
        }

        Transform level3 = level2.GetChild(0);
        if (level3.childCount <= 5)
        {
            return null;
        }

        Transform level4 = level3.GetChild(5);
        if (!level4.TryGetComponent(out CharacterInfo info))
        {
            return null;
        }

        return info;
    }

    #endregion
}

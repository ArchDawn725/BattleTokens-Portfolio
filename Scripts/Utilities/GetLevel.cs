using TMPro;
using UnityEngine;

/// <summary>
/// Displays the stored level value for a given class on a TextMeshProUGUI component,
/// using localized "Lv." prefix and PlayerPrefs data.
/// </summary>
public class GetLevel : MonoBehaviour
{
    #region Fields

    [Header("Class & UI")]
    [Tooltip("The class key used to read the level from PlayerPrefs (e.g., 'Warrior', 'Mage').")]
    [SerializeField] private string classKey;

    private TextMeshProUGUI levelText;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        levelText = GetComponent<TextMeshProUGUI>();
        if (levelText == null)
        {
            Debug.LogError($"[{nameof(GetLevel)}] TextMeshProUGUI component is missing on '{name}'.");
        }
    }

    private void Start()
    {
        if (levelText == null)
        {
            return;
        }

        if (Localizer.Instance == null)
        {
            Debug.LogError($"[{nameof(GetLevel)}] Localizer.Instance is null. Cannot localize level text.");
            return;
        }

        if (string.IsNullOrWhiteSpace(classKey))
        {
            Debug.LogWarning($"[{nameof(GetLevel)}] classKey is empty on '{name}'. Defaulting level to 0.");
        }

        string prefix = Localizer.Instance.GetLocalizedText("Lv.");
        int level = PlayerPrefs.GetInt($"{classKey}_Level", 0);

        levelText.text = $"{prefix}{level}";
    }

    #endregion
}

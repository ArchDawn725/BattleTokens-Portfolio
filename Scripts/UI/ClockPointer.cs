using UnityEngine;

[DisallowMultipleComponent]
public class ClockPointer : MonoBehaviour
{
    #region FIELDS

    [Header("References")]
    [SerializeField, Tooltip("RectTransform of the clock hand graphic to rotate.")]
    private RectTransform hand;

    [Header("Rotation Settings")]
    [SerializeField, Tooltip("Angle in degrees where 0% progress should point.")]
    private float zeroAngleOffset = 0f;

    [SerializeField, Tooltip("If true, 0→100% progress rotates clockwise.")]
    private bool clockwise = true;

    private const float FullCircle = 360f;
    private const float MaxPercent = 100f;

    #endregion

    #region PUBLIC_API

    /// <summary>
    /// Sets the hand rotation based on a percentage in the 0..100 range.
    /// </summary>
    /// <param name="percent0to100">Progress value where 0 = start, 100 = full circle.</param>
    public void SetPercent(float percent0to100)
    {
        if (hand == null)
        {
            Debug.LogWarning("[ClockPointer] Hand RectTransform is not assigned.");
            return;
        }

        // Clamp to avoid unexpected wrapping if a higher value is passed
        float p = Mathf.Clamp(percent0to100, 0f, MaxPercent) / MaxPercent; // 0..1
        float direction = clockwise ? -1f : 1f;
        float z = p * FullCircle * direction + zeroAngleOffset;

        hand.localEulerAngles = new Vector3(0f, 0f, z);
    }

    /// <summary>
    /// Sets the hand rotation based on a normalized percentage in the 0..1 range.
    /// </summary>
    public void SetPercent01(float percent01) => SetPercent(percent01 * MaxPercent);

    #endregion
}

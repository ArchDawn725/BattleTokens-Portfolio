using UnityEngine;

/// <summary>
/// Arranges all child UI elements in a grid by adjusting their anchorMin/anchorMax
/// in a normalized space of 0..1.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class AnchorGridLayout : MonoBehaviour
{
    [Header("Layout Settings")]
    [Tooltip("Number of columns to distribute child elements. Rows are computed based on child count.")]
    [SerializeField] private int columns = 2;

    [Tooltip("Margins on each side, specified in normalized coordinates (0..1).")]
    [Range(0f, 1f)][SerializeField] private float leftMargin = 0f;
    [Range(0f, 1f)][SerializeField] private float rightMargin = 0f;
    [Range(0f, 1f)][SerializeField] private float topMargin = 0f;
    [Range(0f, 1f)][SerializeField] private float bottomMargin = 0f;

    [Header("Spacing (normalized)")]
    [Tooltip("Horizontal spacing between columns, in 0..1 normalized space.")]
    [Range(0f, 1f)][SerializeField] private float horizontalSpacing = 0f;

    [Tooltip("Vertical spacing between rows, in 0..1 normalized space.")]
    [Range(0f, 1f)][SerializeField] private float verticalSpacing = 0f;

    [Header("Row Order")]
    [Tooltip("If true, the first child is placed in the top row; if false, the bottom row.")]
    [SerializeField] private bool fillFromTop = false;

    private void OnEnable()
    {
        RefreshLayout();
    }

    private void OnValidate()
    {
        RefreshLayout();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshLayout();
    }

    /// <summary>
    /// Repositions all child RectTransforms based on the current settings.
    /// </summary>
    public void RefreshLayout()
    {
        RectTransform parentRect = GetComponent<RectTransform>();
        if (!parentRect) return;

        int childCount = parentRect.childCount;
        if (childCount == 0) return;

        if (columns < 1) columns = 1;

        int rowCount = Mathf.CeilToInt(childCount / (float)columns);

        float totalWidth = 1f - leftMargin - rightMargin;
        float totalHeight = 1f - topMargin - bottomMargin;

        // bail out early if margins are impossible
        if (totalWidth <= 0f || totalHeight <= 0f)
        {
            Debug.LogWarning($"[AnchorGridLayout] Invalid margins on {name}: no space left for children.");
            return;
        }

        float cellWidth = (totalWidth - (columns - 1) * horizontalSpacing) / columns;
        float cellHeight = (totalHeight - (rowCount - 1) * verticalSpacing) / rowCount;

        if (cellWidth <= 0f || cellHeight <= 0f)
        {
            Debug.LogWarning($"[AnchorGridLayout] Spacing/margins too large on {name}: cell size <= 0.");
            return;
        }

        for (int i = 0; i < childCount; i++)
        {
            if (!(parentRect.GetChild(i) is RectTransform child))
                continue;

            int colIndex = i % columns;
            int rowIndex = i / columns;

            if (fillFromTop)
                rowIndex = (rowCount - 1) - rowIndex;

            float anchorMinX = leftMargin + colIndex * (cellWidth + horizontalSpacing);
            float anchorMinY = bottomMargin + rowIndex * (cellHeight + verticalSpacing);
            float anchorMaxX = anchorMinX + cellWidth;
            float anchorMaxY = anchorMinY + cellHeight;

            child.anchorMin = new Vector2(anchorMinX, anchorMinY);
            child.anchorMax = new Vector2(anchorMaxX, anchorMaxY);
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }
    }
}

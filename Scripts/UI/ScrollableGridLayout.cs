using UnityEngine;

/// <summary>
/// Layout component for a vertical ScrollRect content panel.
/// Attach this to the <b>Content</b> RectTransform of a vertical ScrollRect.
/// Children are positioned using normalized anchors (0–1), and the Content
/// height grows or shrinks in discrete row steps as needed.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ScrollableGridLayout : MonoBehaviour
{
    #region Serialized Fields

    [Header("Grid")]
    [Min(1)]
    [Tooltip("Number of columns in the grid.")]
    [SerializeField] private int columns = 1;

    [Range(0.01f, 1f)]
    [Tooltip("Normalized height of each cell relative to the viewport height (0–1).")]
    [SerializeField] private float cellHeightN = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("Normalized horizontal spacing between cells (0–1 of viewport width).")]
    [SerializeField] private float horizontalSpacingN = 0.02f;

    [Range(0f, 1f)]
    [Tooltip("Normalized vertical spacing between rows (0–1 of viewport height).")]
    [SerializeField] private float verticalSpacingN = 0.02f;

    [Header("Margins (normalized)")]
    [Range(0f, 1f)]
    [Tooltip("Left margin as a fraction of viewport width (0–1).")]
    [SerializeField] private float leftMarginN = 0f;

    [Range(0f, 1f)]
    [Tooltip("Right margin as a fraction of viewport width (0–1).")]
    [SerializeField] private float rightMarginN = 0f;

    [Range(0f, 1f)]
    [Tooltip("Top margin as a fraction of viewport height (0–1).")]
    [SerializeField] private float topMarginN = 0f;

    [Range(0f, 1f)]
    [Tooltip("Bottom margin as a fraction of viewport height (0–1).")]
    [SerializeField] private float bottomMarginN = 0f;

    [Tooltip("If true, children are filled from the top row downward. If false, from the bottom up.")]
    [SerializeField] private bool fillFromTop = true;

    #endregion

    #region Cached References

    private RectTransform _content;

    #endregion

    #region Unity Callbacks

    private void OnEnable() => RefreshLayout();

    private void OnValidate() => RefreshLayout();

    private void OnTransformChildrenChanged() => RefreshLayout();

    #endregion

    #region Public API

    /// <summary>
    /// Rebuilds the grid layout. Call this after adding or removing children.
    /// </summary>
    [ContextMenu("Refresh Layout")]
    public void RefreshLayout()
    {
        if (_content == null)
            _content = GetComponent<RectTransform>();

        if (_content == null)
        {
            Debug.LogError($"{nameof(ScrollableGridLayout)} on '{name}' could not find a RectTransform.", this);
            return;
        }

        int childCount = _content.childCount;
        if (childCount == 0 || columns < 1)
            return;

        // Ensure we have a viewport (parent) RectTransform
        var viewport = _content.parent as RectTransform;
        if (viewport == null)
        {
            Debug.LogWarning(
                $"{nameof(ScrollableGridLayout)} on '{name}' requires its parent to be a RectTransform " +
                "in order to compute the grid layout.",
                this);
            return;
        }

        float viewH = viewport.rect.height;
        if (viewH <= 0f)
        {
            Debug.LogWarning(
                $"{nameof(ScrollableGridLayout)} on '{name}' cannot layout children because the viewport " +
                "height is zero or negative.",
                this);
            return;
        }

        // --------------------------------------------------------------
        // 1. Compute rows and cell size in normalized space
        // --------------------------------------------------------------
        int rows = Mathf.CeilToInt(childCount / (float)columns);

        float totalW = 1f - leftMarginN - rightMarginN; // normalized width budget
        float cellWidthN = (totalW - (columns - 1) * horizontalSpacingN) / columns;

        // --------------------------------------------------------------
        // 2. Expand / shrink content height additively (row steps)
        // --------------------------------------------------------------
        // Convert normalized constants to pixels once
        float cellH = cellHeightN * viewH;
        float spaceY = verticalSpacingN * viewH;
        float topPx = topMarginN * viewH;
        float botPx = bottomMarginN * viewH;

        // How many rows naturally fit into the viewport?
        // +spaceY simplifies the rowsThatFit formula by absorbing the last spacing.
        float usableH = viewH - topPx - botPx + spaceY;
        int rowsThatFit = Mathf.Max(1, Mathf.FloorToInt(usableH / (cellH + spaceY)));

        int extraRows = Mathf.Max(0, rows - rowsThatFit);
        float rowStep = cellH + spaceY; // pixels added per extra row

        // If Content’s pivot is at the top (pivot.y > 0.5), height must be negative
        float sign = _content.pivot.y > 0.5f ? -1f : 1f;

        // Size is 0 when everything fits, grows ±rowStep per extra row
        Vector2 size = _content.sizeDelta;
        size.y = sign * extraRows * rowStep;
        _content.sizeDelta = size;

        // --------------------------------------------------------------
        // 3. Position each child via anchors
        // --------------------------------------------------------------
        for (int i = 0; i < childCount; i++)
        {
            var child = _content.GetChild(i) as RectTransform;
            if (!child)
                continue;

            int col = i % columns;
            int row = i / columns;
            if (fillFromTop)
                row = rows - 1 - row;

            float minX = leftMarginN + col * (cellWidthN + horizontalSpacingN);
            float maxX = minX + cellWidthN;

            // Top-pivot math: anchorY counts down from 1
            float maxY = 1f - topMarginN - row * (cellHeightN + verticalSpacingN);
            float minY = maxY - cellHeightN;

            child.anchorMin = new Vector2(minX, minY);
            child.anchorMax = new Vector2(maxX, maxY);
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero; // snap to anchors
        }
    }

    #endregion
}

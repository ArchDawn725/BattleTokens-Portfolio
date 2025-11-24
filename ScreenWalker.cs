using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to a prefab that has a RectTransform.
/// Call <see cref="Initialise"/> immediately after Instantiate to start it walking across the screen.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ScreenWalker : MonoBehaviour
{
    #region Configurable Fields

    [Header("Travel")]
    [Tooltip("Minimum time (seconds) the walker takes to cross the screen.")]
    [SerializeField] private float minDuration = 6f;

    [Tooltip("Maximum time (seconds) the walker takes to cross the screen.")]
    [SerializeField] private float maxDuration = 10f;

    [Tooltip("How far off-screen (as a fraction of width) to spawn and despawn.")]
    [SerializeField] private float spawnPaddingPct = 0.05f;

    [Header("Bobbing")]
    [Tooltip("Vertical bob amplitude as a fraction of the parent height.")]
    [SerializeField] private float bobAmplitudePct = 0.02f;

    [Tooltip("Perlin noise frequency for bobbing.")]
    [SerializeField] private float bobFrequency = 1.5f;

    [Header("Size (% of parent)")]
    [Tooltip("Height fraction at the screen edges.")]
    [SerializeField, Range(0.02f, 2f)] private float maxHeightPct = 0.12f;

    [Tooltip("Height fraction near the middle of the screen.")]
    [SerializeField, Range(0.02f, 2f)] private float minHeightPct = 0.07f;

    [Tooltip("Width / height ratio of the walker.")]
    [SerializeField] private float aspectRatio = 0.50f;

    [Header("Tilt")]
    [Tooltip("Maximum tilt at the edges, interpolated to 0° in the middle.")]
    [SerializeField] private float maxTiltDeg = 15f;

    #endregion

    #region State

    private RectTransform rectTransform;
    private bool fromLeft;          // Set via Initialise()
    private float journeySeconds;
    private float perlinSeed;

    #endregion

    #region Initialization

    /// <summary>
    /// Call immediately after instantiating the prefab to start the movement.
    /// </summary>
    /// <param name="startFromLeft">If true, starts off-screen on the left and moves to the right (and vice versa).</param>
    public void Initialise(bool startFromLeft)
    {
        fromLeft = startFromLeft;

        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        perlinSeed = Random.value * 100f;
        journeySeconds = Random.Range(minDuration, maxDuration);

        StartCoroutine(WalkCoroutine());
    }

    #endregion

    #region Movement & Coroutines

    private IEnumerator WalkCoroutine()
    {
        float elapsed = 0f;

        // X anchor for top-centre
        float startX = fromLeft ? -spawnPaddingPct : 1f + spawnPaddingPct;
        float endX = fromLeft ? 1f + spawnPaddingPct : -spawnPaddingPct;

        while (elapsed < journeySeconds)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / journeySeconds); // 0 → 1 along path

            // ----------- anchor position (top-centre) -----------
            float x = Mathf.Lerp(startX, endX, u);

            // Perlin-noise bob (downwards: 0→bobAmplitude pct)
            float bob = bobAmplitudePct *
                        Mathf.PerlinNoise(perlinSeed, Time.time * bobFrequency);
            float topY = 1.15f - bob; // top edge

            // ----------- dynamic size by anchors -----------
            float heightPct = Mathf.Lerp(
                maxHeightPct,                  // tall at edges
                minHeightPct,                  // short mid-screen
                1f - Mathf.Abs(u - 0.5f) * 2f  // 1 at edges, 0 in middle
            );

            float widthPct = heightPct * aspectRatio;
            float halfWidth = widthPct * 0.5f;

            rectTransform.anchorMax = new Vector2(x + halfWidth, topY);           // upper-right
            rectTransform.anchorMin = new Vector2(x - halfWidth, topY - heightPct); // lower-left
            rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;     // flush to anchors

            // ----------- tilt away from nearest top corner -----------
            float sideSign = fromLeft ? 1f : -1f;
            float tilt = maxTiltDeg * (0.5f - u) * 2f;           // + at left, – at right
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt * sideSign);

            yield return null;
        }

        Destroy(gameObject);
    }

    #endregion

    #region Teardown

    private void OnDisable()
    {
        StopAllCoroutines();
        Destroy(gameObject);
    }

    #endregion
}

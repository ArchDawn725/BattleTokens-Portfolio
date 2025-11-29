using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CandleFlicker : MonoBehaviour
{
    #region Fields

    [Header("Targets (drag as many as you like)")]
    [Tooltip("UI graphics (Image, RawImage, TMP, etc.) whose alpha will flicker.")]
    [SerializeField] private Graphic[] uiImages;

    [Tooltip("World-space sprites whose alpha will flicker.")]
    [SerializeField] private SpriteRenderer[] sprites;

    [Header("Alpha Range (0 – 1)")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.6f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 1f;

    [Header("Timing (seconds)")]
    [Tooltip("Shortest pause between new target alpha values.")]
    [SerializeField] private float minInterval = 0.05f;

    [Tooltip("Longest pause between new target alpha values.")]
    [SerializeField] private float maxInterval = 0.25f;

    [Tooltip("Duration of the fade between alpha values.")]
    [SerializeField] private float blendTime = 0.10f;

    [Header("Screen Walker")]
    [Tooltip("Prefab containing a ScreenWalker component to spawn occasionally.")]
    [SerializeField] private ScreenWalker walkerPrefab;

    [Tooltip("Parent RectTransform for spawned ScreenWalkers (typically the root canvas).")]
    [SerializeField] private RectTransform uiCanvasRoot;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        // When no PlayerStats exists, treat shadows as enabled.
        bool disableShadows = PlayerStats.Instance != null && PlayerStats.Instance.disableShadows;

        if (!disableShadows)
        {
            SetAlphaOnAll(1f);
            StartCoroutine(FlickerAll());
            StartCoroutine(SpawnWalkers());
        }
        else
        {
            SetAlphaOnAll(0f);
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    #endregion

    #region Flicker Logic

    private IEnumerator FlickerAll()
    {
        // Ensure there is at least one valid target.
        if ((uiImages == null || uiImages.Length == 0) &&
            (sprites == null || sprites.Length == 0))
        {
            yield break;
        }

        float currentAlpha = GetCurrentAlpha();

        while (true)
        {
            float targetAlpha = Random.Range(minAlpha, maxAlpha);
            float t = 0f;

            // Fade from currentAlpha to targetAlpha over blendTime.
            while (t < blendTime)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(currentAlpha, targetAlpha, blendTime > 0f ? t / blendTime : 1f);
                SetAlphaOnAll(a);
                yield return null;
            }

            currentAlpha = targetAlpha;
            SetAlphaOnAll(currentAlpha);

            float wait = Random.Range(minInterval, maxInterval);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }
            else
            {
                yield return null;
            }
        }
    }

    private float GetCurrentAlpha()
    {
        if (uiImages != null)
        {
            foreach (var g in uiImages)
            {
                if (g != null)
                {
                    return g.color.a;
                }
            }
        }

        if (sprites != null)
        {
            foreach (var s in sprites)
            {
                if (s != null)
                {
                    return s.color.a;
                }
            }
        }

        return 1f;
    }

    private void SetAlphaOnAll(float alpha)
    {
        if (uiImages != null)
        {
            foreach (var g in uiImages)
            {
                if (g == null) continue;
                Color c = g.color;
                c.a = alpha;
                g.color = c;
            }
        }

        if (sprites != null)
        {
            foreach (var s in sprites)
            {
                if (s == null) continue;
                Color c = s.color;
                c.a = alpha;
                s.color = c;
            }
        }
    }

    #endregion

    #region Screen Walkers

    private IEnumerator SpawnWalkers()
    {
        // If no prefab or parent is assigned, do not spawn walkers.
        if (walkerPrefab == null || uiCanvasRoot == null)
        {
            yield break;
        }

        while (true)
        {
            // Long random delay between walkers; tweak as needed.
            float delay = Random.Range(1f, 100f);
            yield return new WaitForSeconds(delay);
            SpawnWalker();
        }
    }

    private void SpawnWalker()
    {
        if (walkerPrefab == null || uiCanvasRoot == null)
        {
            return;
        }

        bool fromLeft = Random.value < 0.5f;
        ScreenWalker walkerInstance = Instantiate(walkerPrefab, uiCanvasRoot);
        walkerInstance.Initialise(fromLeft);
    }

    #endregion

    #region Settings

    /// <summary>
    /// External toggle for enabling/disabling the flicker and walkers at runtime.
    /// </summary>
    public void ChangeSetting(bool disable)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (disable)
        {
            StopAllCoroutines();
            SetAlphaOnAll(0f);
        }
        else
        {
            SetAlphaOnAll(1f);
            StartCoroutine(FlickerAll());
            StartCoroutine(SpawnWalkers());
        }
    }

    #endregion
}

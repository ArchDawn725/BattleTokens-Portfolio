using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TurnTimerUIController : MonoBehaviour
{
    #region Singleton

    public static TurnTimerUIController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[{nameof(TurnTimerUIController)}] Multiple instances detected. Destroying duplicate on '{name}'.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (turnTimer == null)
        {
            Debug.LogError($"[{nameof(TurnTimerUIController)}] Turn timer Slider reference is not assigned in the inspector.", this);
        }

        CacheFillImage();
    }

    #endregion

    #region Inspector

    [Header("Timer UI")]
    [Tooltip("Slider that visualizes the remaining turn time.")]
    [SerializeField] private Slider turnTimer;

    [Tooltip("Image used as the fill of the turn timer (color indicates state).")]
    [SerializeField] private Image timerFillImage;

    [Header("Timer Settings")]
    [Tooltip("Total duration of the turn timer in seconds.")]
    [SerializeField] private float timerDurationSeconds = 20f;

    [Tooltip("How often, in seconds, the timer UI updates.")]
    [SerializeField] private float updateIntervalSeconds = 0.25f;

    #endregion

    #region State

    private Coroutine activeCoroutine;
    [NonSerialized] public bool turnEnded;

    private UIController UiController => UIController.Instance;

    #endregion

    #region Public API

    public void StartNewTimer()
    {
        if (turnTimer == null)
        {
            Debug.LogWarning($"[{nameof(TurnTimerUIController)}] Cannot start timer; Slider reference is missing.", this);
            return;
        }

        if (OnlineRelay.Instance.IsConnected())
        {
            StopTimer();

            if (timerFillImage == null)
            {
                CacheFillImage();
            }

            if (timerFillImage != null)
            {
                timerFillImage.color = Color.red;
            }

            activeCoroutine = StartCoroutine(ProgressCoroutine(TurnTimerDone));
            turnTimer.gameObject.SetActive(true);
        }
        else
        {
            turnTimer.gameObject.SetActive(false);
        }

        if (turnEnded && timerFillImage != null)
        {
            timerFillImage.color = Color.green;
        }
    }

    public void StopTimer()
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        if (turnTimer == null)
        {
            return;
        }

        turnTimer.value = 0f;

        if (timerFillImage == null)
        {
            CacheFillImage();
        }

        if (timerFillImage != null)
        {
            timerFillImage.color = Color.red;
        }
    }

    #endregion

    #region Timer Logic

    private IEnumerator ProgressCoroutine(Action onComplete)
    {
        if (turnTimer == null)
        {
            yield break;
        }

        float elapsed = 0f;

        turnTimer.maxValue = timerDurationSeconds;
        turnTimer.value = 0f;

        while (elapsed <= timerDurationSeconds)
        {
            elapsed += updateIntervalSeconds;
            turnTimer.value = Mathf.Min(elapsed, timerDurationSeconds);
            yield return new WaitForSeconds(updateIntervalSeconds);
        }

        onComplete?.Invoke();
        activeCoroutine = null;
    }

    private void TurnTimerDone()
    {
        if (turnEnded)
        {
            return;
        }

        if (UiController == null)
        {
            Debug.LogWarning($"[{nameof(TurnTimerUIController)}] UIController.Instance is null; cannot complete timer action.", this);
            return;
        }

        switch (UiController.gState)
        {
            case UIController.GameState.Upgrades:
                UiController.ReadyUp();
                break;

            case UIController.GameState.Battle:
                UiController.EndTurn(false);
                break;

            case UIController.GameState.Spawning:
                SpawnManager.Instance.SpawnMyPlayerOnRandomSquare();
                break;

            default:
                UiController.EndTurn(false);
                break;
        }
    }

    #endregion

    #region Helpers

    private void CacheFillImage()
    {
        if (turnTimer == null || timerFillImage != null)
        {
            return;
        }

        // Original code used: turnTimer.transform.GetChild(1).GetChild(0).GetComponent<Image>()
        // Try to replicate that safely.
        Transform root = turnTimer.transform;
        if (root.childCount > 1)
        {
            Transform child1 = root.GetChild(1);
            if (child1.childCount > 0)
            {
                timerFillImage = child1.GetChild(0).GetComponent<Image>();
            }
        }

        if (timerFillImage == null)
        {
            Debug.LogWarning($"[{nameof(TurnTimerUIController)}] Could not auto-cache timer fill Image. Please assign it in the inspector.", this);
        }
    }

    #endregion
}

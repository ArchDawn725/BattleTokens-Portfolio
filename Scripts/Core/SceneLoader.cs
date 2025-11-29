using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads a specific scene after a small configurable delay.
/// Useful for splash screens, logo scenes, or auto-transition boot scenes.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    #region Config & State

    [Header("Scene Load Settings")]
    [Tooltip("Delay (in seconds) before transitioning to the target scene.")]
    [SerializeField] private float delay = 0.1f;

    [Tooltip("Build index of the scene to load after the delay.")]
    [SerializeField] private int sceneIndex = 1;

    #endregion

    #region Unity Lifecycle & Logic

    private void Start()
    {
        Invoke(nameof(Transition), delay);
    }

    private void Transition()
    {
        int buildSceneCount = SceneManager.sceneCountInBuildSettings;

        if (sceneIndex < 0 || sceneIndex >= buildSceneCount)
        {
            Debug.LogError(
                $"[SceneLoader] Invalid sceneIndex ({sceneIndex}). " +
                $"Build has {buildSceneCount} scenes. Aborting load.", this);
            return;
        }

        SceneManager.LoadSceneAsync(sceneIndex);
    }

    #endregion
}

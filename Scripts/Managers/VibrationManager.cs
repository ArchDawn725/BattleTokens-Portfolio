using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Centralized vibration manager supporting:
/// - Mobile vibration
/// - Gamepad rumble via the new Input System
/// </summary>
public static class VibrationManager
{
    /// <summary>
    /// Triggers vibration or controller rumble.
    /// </summary>
    /// <param name="strength">Motor strength (0–1). Applies only to gamepads.</param>
    /// <param name="duration">How long the rumble lasts, in seconds.</param>
    public static void Vibrate(float strength = 0.5f, float duration = 0.5f)
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif

#if ENABLE_INPUT_SYSTEM
        var gamepad = Gamepad.current;

        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(strength, strength);
            Debug.Log($"[VibrationManager] Gamepad rumble for {duration:0.00}s at strength {strength:0.00}");

            // Schedule stopping the vibration
            RumbleStopper.StopRumbleAfterDelay(duration);
        }
#endif
    }

#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Small helper MonoBehaviour used to schedule delayed rumble stop.
    /// Static classes cannot run coroutines or Invoke.
    /// </summary>
    private class RumbleStopper : MonoBehaviour
    {
        private static RumbleStopper _instance;

        private static RumbleStopper Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("VibrationManager_RumbleStopper");
                    Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<RumbleStopper>();
                }
                return _instance;
            }
        }

        public static void StopRumbleAfterDelay(float duration)
        {
            Instance.StartCoroutine(Instance.StopAfter(duration));
        }

        private System.Collections.IEnumerator StopAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }
    }
#endif
}

using UnityEngine;
using UnityEngine.InputSystem;

public class PauseController : MonoBehaviour
{
    [SerializeField] private Key pauseKey = Key.Escape;

    private bool isPaused;

    private void Update()
    {
        if (Keyboard.current[pauseKey].wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    private void TogglePause()
    {
        isPaused = !isPaused;

        Time.timeScale = isPaused ? 0f : 1f;
    }
}

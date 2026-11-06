using UnityEngine;
using TMPro; // Required if using TextMeshPro

public class TimerController : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TMP_Text timerText; // Drag your TimerText here

    [Header("Timer Settings")]
    [SerializeField] private float timeRemaining = 60f; // Starting time in seconds
    [SerializeField] private bool isCountdown = true;
    [SerializeField] private bool isTimerRunning = true;

    void Update()
    {
        if (!isTimerRunning) return;

        // Calculate the passage of time
        if (isCountdown)
        {
            if (timeRemaining > 0)
            {
                timeRemaining -= Time.deltaTime;
            }
            else
            {
                timeRemaining = 0;
                isTimerRunning = false;
                OnTimerEnd();
            }
        }
        else
        {
            // Stopwatch / Count-up behavior
            timeRemaining += Time.deltaTime;
        }

        DisplayTime(timeRemaining);
    }

    void DisplayTime(float timeToDisplay)
    {
        // Calculate minutes and seconds
        float minutes = Mathf.FloorToInt(timeToDisplay / 60);
        float seconds = Mathf.FloorToInt(timeToDisplay % 60);

        // Format string as "00:00"
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    void OnTimerEnd()
    {
        // The clock is score-relevant (Time Management is a rubric criterion), so its
        // expiry has to be audible.
        AudioService.TryPlayFirst("alarm", "ui-error");
    }
}

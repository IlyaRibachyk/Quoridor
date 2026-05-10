using UnityEngine;
using TMPro;

public class TurnTimer : MonoBehaviour
{
    [Header("Links")]
    // public PlayerController controller;
    public GameManager controller;
    public TextMeshProUGUI timerText;

    private float timeLeft;
    private int turnDuration;
    private bool isPaused = false;

    void Start()
    {
        if (controller == null)
        {
            Debug.LogError("TurnTimer: controller is null");
            enabled = false;
            return;
        }

        // беремо значення як ти береш індекс текстури
        if (GameSettings.Instance != null)
            turnDuration = Mathf.Clamp(GameSettings.Instance.timerValue, 10, 60);
        else
            turnDuration = 30;

        ResetTurnTimer();
    }

    void Update()
    {
        // if (controller.currentMode != PlayerController.GameMode.PvP)
        if (controller.currentMode != GameManager.GameMode.PvP)
        {
            if (timerText != null) timerText.text = "";
            return;
        }

        if (isPaused) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft < 0) timeLeft = 0;

        UpdateText();

        if (timeLeft <= 0f)
        {
            controller.SwitchTurnByTimer();
        }
    }

    public void ResetTurnTimer()
    {
        timeLeft = turnDuration;
        UpdateText();
    }

    private void UpdateText()
    {
        if (timerText == null) return;

        int seconds = Mathf.CeilToInt(timeLeft);
        timerText.text = $"Time: {seconds}";
    }

    public void PauseTimer()
    {
        if (controller == null) return;
        // if (controller.currentMode != PlayerController.GameMode.PvP) return;
        if (controller.currentMode != GameManager.GameMode.PvP) return;

        isPaused = true;
    }

    public void ResumeTimer()
    {
        if (controller == null) return;
        // if (controller.currentMode != PlayerController.GameMode.PvP) return;
        if (controller.currentMode != GameManager.GameMode.PvP) return;

        isPaused = false;
    }
}

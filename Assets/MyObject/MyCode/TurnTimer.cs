using UnityEngine;
using TMPro;

// Клас, що відповідає за логіку таймера ходу в грі.
// Стежить за тим, скільки часу залишилося у гравця, та примусово передає хід, якщо час вичерпано.
public class TurnTimer : MonoBehaviour
{
    [Header("Links")]
    // Закоментований старий код (ймовірно, раніше використовувався PlayerController)
    // public PlayerController controller;

    // Посилання на головний менеджер гри, який керує ходами та станами
    public GameManager controller;

    // Посилання на текстовий елемент інтерфейсу (UI), де буде відображатися час
    public TextMeshProUGUI timerText;

    // Внутрішні змінні стану таймера
    private float timeLeft;      // Скільки часу (в секундах) залишилося на поточний хід
    private int turnDuration;    // Скільки загалом часу дається на один хід (зчитується з налаштувань)
    private bool isPaused = false; // Прапорець, що визначає, чи стоїть таймер на паузі

    void Start()
    {
        // Запобіжник: якщо GameManager не призначений в Інспекторі, 
        // виводимо помилку і вимикаємо цей скрипт, щоб уникнути подальших збоїв
        if (controller == null)
        {
            Debug.LogError("TurnTimer: controller is null");
            enabled = false;
            return;
        }

        // Отримуємо значення таймера з глобальних налаштувань (GameSettings), які передалися з Головного меню
        // беремо значення як ти береш індекс текстури
        if (GameSettings.Instance != null)
            // Обмежуємо значення між 10 та 60 секундами для надійності (Mathf.Clamp)
            turnDuration = Mathf.Clamp(GameSettings.Instance.timerValue, 10, 60);
        else
            // Якщо GameSettings не знайдено (наприклад, запустили сцену напряму), ставимо 30 секунд за замовчуванням
            turnDuration = 30;

        // Встановлюємо початковий час для першого ходу
        ResetTurnTimer();
    }

    void Update()
    {
        // Якщо поточний режим гри НЕ PvP (тобто ми граємо проти бота - PvE),
        // таймер не потрібен. Ховаємо текст і виходимо з методу.
        if (controller.currentMode != GameManager.GameMode.PvP)
        {
            if (timerText != null) timerText.text = "";
            return;
        }

        // Якщо гра/таймер на паузі, просто припиняємо відлік часу в цьому кадрі
        if (isPaused) return;

        // Віднімаємо від залишку часу час, що пройшов з попереднього кадру (Time.deltaTime)
        timeLeft -= Time.deltaTime;

        // Не дозволяємо часу стати від'ємним
        if (timeLeft < 0) timeLeft = 0;

        // Оновлюємо текст на екрані
        UpdateText();

        // Якщо час повністю вичерпано (0 або менше)
        if (timeLeft <= 0f)
        {
            // Кажемо GameManager'у примусово завершити хід поточного гравця
            controller.SwitchTurnByTimer();
        }
    }

    // Метод для скидання таймера (викликається GameManager на початку кожного нового ходу)
    public void ResetTurnTimer()
    {
        timeLeft = turnDuration; // Повертаємо час до максимального значення
        UpdateText();            // Одразу оновлюємо текст в UI
    }

    // Метод для оновлення текстового поля таймера
    private void UpdateText()
    {
        if (timerText == null) return; // Якщо тексту немає, нічого не робимо

        // Заокруглюємо час в БІЛЬШУ сторону (Mathf.CeilToInt), щоб 0.1 секунди відображалися як "1"
        int seconds = Mathf.CeilToInt(timeLeft);
        timerText.text = $"Time: {seconds}";
    }

    // Метод для тимчасової зупинки таймера (наприклад, якщо відкрито меню паузи)
    public void PauseTimer()
    {
        if (controller == null) return;

        // Ставимо на паузу ТІЛЬКИ якщо це PvP режим
        if (controller.currentMode != GameManager.GameMode.PvP) return;

        isPaused = true;
    }

    // Метод для відновлення відліку часу після паузи
    public void ResumeTimer()
    {
        if (controller == null) return;

        // Знімаємо з паузи ТІЛЬКИ якщо це PvP режим
        if (controller.currentMode != GameManager.GameMode.PvP) return;

        isPaused = false;
    }
}

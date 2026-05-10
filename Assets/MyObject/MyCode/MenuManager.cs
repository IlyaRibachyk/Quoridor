using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

// Клас, що керує логікою Головного меню гри.
// Відповідає за налаштування матчу (таймер, кольори фішок) та запуск потрібної сцени.
public class MenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Image p1Image, p2Image; // Зображення (іконки) для відображення обраних текстур гравців в меню
    public TMP_Text timerText;     // Текстове поле для відображення значення таймера

    [Header("Assets")]
    public Sprite[] woodSprites;   // Масив доступних текстур/кольорів (спрайтів дерева) для фішок

    // Внутрішні змінні для зберігання поточного вибору користувача
    private int p1Index = 0;       // Індекс обраної текстури для Гравця 1
    private int p2Index = 1;       // Індекс обраної текстури для Гравця 2
    private int currentTime = 30;  // Початкове значення таймера (в секундах)

    // Прапорець, що визначає режим гри: true = проти бота (PvE), false = проти людини (PvP)
    public bool isVsAI;

    // Викликається під час запуску сцени меню
    void Start() => UpdateUI();

    // Метод для зміни значення таймера (викликається кнопками "+" та "-" в UI)
    public void ChangeTimer(int amount)
    {
        // Збільшуємо або зменшуємо час, але жорстко обмежуємо його в межах від 10 до 60 секунд
        currentTime = Mathf.Clamp(currentTime + amount, 10, 60);
        UpdateUI(); // Оновлюємо текст на екрані
    }

    // Метод для перемикання текстури Гравця 1 (dir: 1 для "вперед", -1 для "назад")
    public void ChangeP1(int dir)
    {
        // Якщо граємо проти бота, передаємо -1 (щоб не забороняти вибір кольору, який вже "зайнятий" ботом).
        // Якщо PvP — передаємо індекс другого гравця, щоб уникнути однакових кольорів.
        int other = isVsAI ? -1 : p2Index;
        p1Index = GetNextIndex(p1Index, dir, other);
        UpdateUI();
    }

    // Метод для перемикання текстури Гравця 2
    public void ChangeP2(int dir)
    {
        // Якщо ми випадково натиснемо вибір для P2 в режимі AI, він теж не буде блокуватися 
        // (хоча UI елементи вибору P2 зазвичай мають бути приховані в режимі PvE)
        int other = isVsAI ? -1 : p1Index;
        p2Index = GetNextIndex(p2Index, dir, other);
        UpdateUI();
    }

    // Допоміжний метод для безпечного переходу по масиву текстур по колу
    int GetNextIndex(int current, int dir, int otherPlayer)
    {
        // Правильний розрахунок індексу для C#, який коректно працює з від'ємними числами.
        // Додаємо довжину масиву перед взяттям залишку (%), щоб уникнути помилок при кроці назад (dir = -1).
        int next = (current + dir + woodSprites.Length) % woodSprites.Length;

        // Перевірка на збіг кольорів. Якщо розрахований індекс збігається з вибором іншого гравця...
        if (otherPlayer != -1 && next == otherPlayer)
        {
            // ...робимо ще один крок у тому ж напрямку, перестрибуючи зайнятий колір
            next = (next + dir + woodSprites.Length) % woodSprites.Length;
        }
        return next;
    }

    // Оновлює всі візуальні елементи інтерфейсу відповідно до поточних значень змінних
    void UpdateUI()
    {
        if (p1Image != null)
            p1Image.sprite = woodSprites[p1Index];

        if (p2Image != null)
            p2Image.sprite = woodSprites[p2Index];

        if (timerText != null)
            timerText.text = currentTime + " s.";
    }

    // Метод запуску гри у режимі 4 гравців
    public void StartFourPlayersGame()
    {
        // Записуємо налаштування в глобальний об'єкт GameSettings (який не знищується при завантаженні)
        GameSettings.Instance.isFourPlayers = true;
        GameSettings.Instance.timerValue = currentTime; // Використовуємо налаштований таймер

        // Задаємо дефолтні індекси, щоб скрипт PlayerSetup не видавав помилок
        GameSettings.Instance.p1TextureIndex = 0;
        GameSettings.Instance.p2TextureIndex = 1;

        Debug.Log($"MENU: Відправляю таймер = {GameSettings.Instance.timerValue}");

        // Зберігаємо тип гри в PlayerPrefs (пам'ять пристрою). 
        // Тут 0 використовувалося для 2 гравців, 1 для ботів, тому 2 означатиме режим на 4-х.
        PlayerPrefs.SetInt("GameMode", 0);

        // Завантажуємо окрему сцену, створену спеціально для 4 гравців
        SceneManager.LoadScene("GameSceneFor4People");
    }

    // Метод запуску гри у класичному режимі (2 гравці або 1 гравець + ШІ)
    public void StartGame()
    {
        if (GameSettings.Instance != null)
        {
            // 1. Обов'язково вказуємо, що це класичний режим (НЕ 4 гравці)
            GameSettings.Instance.isFourPlayers = false;

            // 2. Зберігаємо фішки та індекси для першого гравця
            GameSettings.Instance.player1Sprite = woodSprites[p1Index];
            GameSettings.Instance.p1TextureIndex = p1Index;

            // РЕЖИМ ПРОТИ БОТА (PvE)
            if (isVsAI)
            {
                int botIndex;
                // Випадковим чином обираємо колір для бота
                do
                {
                    botIndex = Random.Range(0, woodSprites.Length);
                }
                // Гарантуємо, що колір бота не збігається з кольором гравця (якщо текстур більше однієї)
                while (botIndex == p1Index && woodSprites.Length > 1);

                // Зберігаємо вибір бота в налаштування
                GameSettings.Instance.player2Sprite = woodSprites[botIndex];
                GameSettings.Instance.p2TextureIndex = botIndex;

                // Вимикаємо таймер для гри з ботом (встановлюємо -1 як індикатор "без таймера")
                GameSettings.Instance.timerValue = -1;

                // Зберігаємо режим PvE у пам'ять
                PlayerPrefs.SetInt("GameMode", 1); // 1 = PvE
            }
            // РЕЖИМ ПРОТИ ЛЮДИНИ (PvP)
            else
            {
                // Зберігаємо обрані в меню текстури для другого гравця
                GameSettings.Instance.player2Sprite = woodSprites[p2Index];
                GameSettings.Instance.p2TextureIndex = p2Index;

                // Передаємо значення таймера з меню у гру
                GameSettings.Instance.timerValue = currentTime;

                // Зберігаємо режим PvP у пам'ять
                PlayerPrefs.SetInt("GameMode", 0); // 0 = PvP
            }
        }

        // 3. Завантажуємо основну ігрову сцену для 2 гравців
        SceneManager.LoadScene("GameScene");
    }
}

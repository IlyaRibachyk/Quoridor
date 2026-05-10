using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

public class MenuManager : MonoBehaviour
{
    public Image p1Image, p2Image;
    public TMP_Text timerText;
    public Sprite[] woodSprites;

    private int p1Index = 0;
    private int p2Index = 1;
    private int currentTime = 30;

    public bool isVsAI;

    void Start() => UpdateUI();

    public void ChangeTimer(int amount)
    {
        currentTime = Mathf.Clamp(currentTime + amount, 10, 60);
        UpdateUI();
    }

    public void ChangeP1(int dir)
    {
        // Якщо проти бота, передаємо -1, щоб не було перевірки на однаковий колір
        int other = isVsAI ? -1 : p2Index;
        p1Index = GetNextIndex(p1Index, dir, other);
        UpdateUI();
    }

    public void ChangeP2(int dir)
    {
        // Якщо ми випадково натиснемо вибір для P2 в режимі AI, він теж не буде блокуватися (хоча P2 має бути прихований)
        int other = isVsAI ? -1 : p1Index;
        p2Index = GetNextIndex(p2Index, dir, other);
        UpdateUI();
    }

    int GetNextIndex(int current, int dir, int otherPlayer)
    {
        // Правильний розрахунок індексу для C# (працює з від'ємними числами)
        int next = (current + dir + woodSprites.Length) % woodSprites.Length;

        // Перевірка на збіг кольорів тільки якщо otherPlayer != -1
        if (otherPlayer != -1 && next == otherPlayer)
        {
            next = (next + dir + woodSprites.Length) % woodSprites.Length;
        }
        return next;
    }

    void UpdateUI()
    {
        if (p1Image != null)
            p1Image.sprite = woodSprites[p1Index];

        if (p2Image != null)
            p2Image.sprite = woodSprites[p2Index];

        if (timerText != null)
            timerText.text = currentTime + " s.";
    }

    public void StartFourPlayersGame()
    {
        GameSettings.Instance.isFourPlayers = true;
        GameSettings.Instance.timerValue = currentTime; // Використовуємо той самий таймер

        // Можна задати дефолтні індекси, щоб PlayerSetup не видавав помилок
        GameSettings.Instance.p1TextureIndex = 0;
        GameSettings.Instance.p2TextureIndex = 1;

        Debug.Log($"MENU: Відправляю таймер = {GameSettings.Instance.timerValue}");

        PlayerPrefs.SetInt("GameMode", 0); // 2 буде означати режим на 4-х
        SceneManager.LoadScene("GameSceneFor4People");
    }

    public void StartGame()
    {
        // 1. Обов'язково вказуємо, що це НЕ режим на 4-х гравців
        if (GameSettings.Instance != null)
        {
            GameSettings.Instance.isFourPlayers = false;

            // 2. Зберігаємо фішки та індекси для першого гравця
            GameSettings.Instance.player1Sprite = woodSprites[p1Index];
            GameSettings.Instance.p1TextureIndex = p1Index;

            if (isVsAI)
            {
                // РЕЖИМ ПРОТИ БОТА (PvE)
                int botIndex;
                do
                {
                    botIndex = Random.Range(0, woodSprites.Length);
                } while (botIndex == p1Index && woodSprites.Length > 1);

                GameSettings.Instance.player2Sprite = woodSprites[botIndex];
                GameSettings.Instance.p2TextureIndex = botIndex;

                // Вимикаємо таймер для гри з ботом (або залиште currentTime, якщо хочете таймер і там)
                GameSettings.Instance.timerValue = -1;

                PlayerPrefs.SetInt("GameMode", 1); // 1 = PvE
            }
            else
            {
                // РЕЖИМ ПРОТИ ЛЮДИНИ (PvP)
                GameSettings.Instance.player2Sprite = woodSprites[p2Index];
                GameSettings.Instance.p2TextureIndex = p2Index;

                // Передаємо значення таймера з меню
                GameSettings.Instance.timerValue = currentTime;

                PlayerPrefs.SetInt("GameMode", 0); // 0 = PvP
            }
        }

        // 3. Завантаження ігрової сцени
        SceneManager.LoadScene("GameScene");
    }
}

using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance;

    public Sprite player1Sprite;
    public Sprite player2Sprite;
    public int p1TextureIndex;
    public int p2TextureIndex;
    public int timerValue = 30;

    // Додаємо нову змінну
    public bool isFourPlayers = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else { Destroy(gameObject); }
    }
}

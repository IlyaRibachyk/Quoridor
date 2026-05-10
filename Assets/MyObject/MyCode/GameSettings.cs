using UnityEngine;

// Клас для збереження та передачі налаштувань гри між різними сценами 
// (наприклад, з Головного меню в основну сцену гри).
public class GameSettings : MonoBehaviour
{
    // Глобальне посилання на єдиний екземпляр цього класу (Патерн Singleton)
    public static GameSettings Instance;

    // Спрайти (2D іконки) для відображення гравців у інтерфейсі (UI)
    public Sprite player1Sprite;
    public Sprite player2Sprite;

    // Індекси обраних текстур (матеріалів або кольорів) для 3D-фішок гравців
    public int p1TextureIndex;
    public int p2TextureIndex;

    // Час, який дається гравцю на один хід (у секундах). За замовчуванням 30.
    public int timerValue = 30;

    // Додаємо нову змінну
    // Прапорець, що визначає кількість гравців у матчі: 
    // false = класична гра для 2 гравців, true = гра для 4 гравців
    public bool isFourPlayers = false;

    // Метод Awake викликається Unity найпершим, ще до Start()
    void Awake()
    {
        // Реалізація патерну Singleton
        if (Instance == null)
        {
            // Якщо екземпляра ще не існує, робимо цей об'єкт головним
            Instance = this;
            // Кажемо Unity НЕ знищувати цей об'єкт при завантаженні нової сцени 
            // (щоб налаштування не зникли під час переходу з Меню в Гру)
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Якщо екземпляр вже існує (наприклад, ми повернулися в Головне меню), 
            // знищуємо дублікат, щоб уникнути конфліктів
            Destroy(gameObject);
        }
    }
}

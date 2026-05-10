using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

// Головний менеджер гри. Керує черговістю ходів, перевіряє умови перемоги, 
// обробляє взаємодію з інтерфейсом та перемикає камери.
public class GameManager : MonoBehaviour
{
    // Перерахування доступних режимів гри: Гравець проти Гравця (PvP) або Гравець проти ШІ (PvE)
    public enum GameMode { PvP, PvE }
    public GameMode currentMode = GameMode.PvP;

    // Посилання на інші ключові компоненти системи
    public BoardGraph board; // Відповідає за логіку графа (поле)
    public InputController inputController; // Відповідає за кліки мишкою

    [Header("Players")]
    // Тепер використовуємо список для підтримки 2 або 4 гравців
    public List<PlayerData> allPlayers = new List<PlayerData>();
    private int currentPlayerIndex = 0; // Індекс гравця, чий зараз хід
    [HideInInspector] public PlayerData currentPlayer; // Дані поточного гравця

    [Header("UI & Timer")]
    public GameObject victoryPanel; // Панель, що з'являється при перемозі
    public TextMeshProUGUI victoryText; // Текст з ім'ям переможця
    public TurnTimer turnTimer; // Скрипт таймера
    public TextMeshProUGUI turnText; // Текст, що показує чий зараз хід

    [Header("Camera Settings")]
    public Transform mainCameraTransform; // Головна камера сцени
    public float cameraLerpSpeed = 5.0f; // Швидкість плавного переміщення камери
    private int currentCamIndex = 0; // Індекс поточної позиції камери для гравця
    private Transform targetCamTransform; // Цільова точка, куди має летіти камера

    // Висота фішки над дошкою (щоб вона не провалювалася в текстури)
    public float tileHeightOffset = 0.72f;

    // Змінні для збереження стану плитки підсвічування (щоб повертати їй старий колір)
    private GameObject lastHighlightedTile;
    private Color originalColor;
    public Color highlightColor = Color.yellow; // Колір плитки під поточним гравцем
    public Color defaultTileColor = Color.black;

    void Start()
    {
        // 1. Визначаємо режим гри (зчитуємо збережені налаштування з головного меню)
        int mode = PlayerPrefs.GetInt("GameMode", 0);
        currentMode = (mode == 1) ? GameMode.PvE : GameMode.PvP;

        // 2. Ініціалізація першого гравця
        if (allPlayers.Count > 0)
        {
            currentPlayerIndex = 0;
            currentPlayer = allPlayers[currentPlayerIndex];
        }

        // Оновлюємо UI з інформацією про хід
        UpdateTurnText();

        // 3. Будуємо граф (передаємо список усіх гравців для розрахунку цілей)
        board.BuildPhysicalGraph(allPlayers);

        // 4. Початкова позиція камери (ставимо камеру за спину першому гравцю)
        if (currentPlayer.cameraPositions != null && currentPlayer.cameraPositions.Count > 0)
        {
            targetCamTransform = currentPlayer.cameraPositions[0];
            mainCameraTransform.position = targetCamTransform.position;
            mainCameraTransform.rotation = targetCamTransform.rotation;
        }

        // Робимо всі якорі стін невидимими на початку гри
        HideAllAnchors();

        // 5. Налаштування ШІ для всіх, крім першого гравця, якщо це PvE
        if (currentMode == GameMode.PvE)
        {
            for (int i = 1; i < allPlayers.Count; i++)
            {
                allPlayers[i].strategy = GetComponent<AIBotStrategy>();
                // Ініціалізуємо бота, передаючи йому посилання на менеджери
                if (allPlayers[i].strategy != null)
                    ((AIBotStrategy)allPlayers[i].strategy).Init(this, board);
            }
        }

        // Підсвічуємо плитку під першим гравцем
        UpdatePlayerHighlight();
    }

    void Update()
    {
        // Камера рухається і нею можна керувати, якщо це PvP АБО якщо зараз хід реального гравця у PvE.
        // Під час ходу бота камеру не чіпаємо, щоб не заважати.
        if (currentMode == GameMode.PvP || (currentMode == GameMode.PvE && currentPlayerIndex == 0))
        {
            HandleCameraMovement();
            HandleCameraSwitching();
        }
    }

    // Метод підсвічує клітинку, на якій стоїть гравець, чий зараз хід
    public void UpdatePlayerHighlight()
    {
        // 1. Повертаємо стан попередньої плитки (якщо вона була підсвічена на минулому ході)
        if (lastHighlightedTile != null)
        {
            Renderer lastRenderer = lastHighlightedTile.GetComponent<Renderer>();
            // Повертаємо початковий колір
            lastRenderer.material.color = originalColor;
        }

        // 2. Знаходимо плитку під поточним гравцем за допомогою променя (Raycast)
        GameObject currentTile = board.GetTileUnder(allPlayers[currentPlayerIndex].playerObj);

        if (currentTile != null)
        {
            Renderer tileRenderer = currentTile.GetComponent<Renderer>();

            // Запам'ятовуємо плитку та її колір перед зміною, щоб потім повернути як було
            lastHighlightedTile = currentTile;
            originalColor = tileRenderer.material.color;

            // Просто змінюємо колір плитки на колір підсвічування (без емісії)
            tileRenderer.material.color = highlightColor;
        }
    }

    // Робить всі точки для встановлення стін (якорі) повністю прозорими
    private void HideAllAnchors()
    {
        GameObject[] anchors = GameObject.FindGameObjectsWithTag("WallAnchor");
        foreach (var a in anchors)
        {
            Renderer rend = a.GetComponent<Renderer>();
            if (rend != null)
            {
                // Встановлюємо альфа-канал на 0 (повна прозорість)
                rend.material.color = new Color(1, 1, 1, 0);
                // Налаштування для прозорості в URP/Built-in (переводимо матеріал у режим Transparent)
                rend.material.SetFloat("_Surface", 1);
                rend.material.renderQueue = 3000;
            }
        }
    }

    // Метод завершення поточного ходу та передачі черги наступному гравцю
    public void SwitchTurn()
    {
        // Прибираємо підсвічування доступних ходів попереднього гравця
        inputController.ClearHighlights();

        // Перехід до наступного гравця по колу (0 -> 1 -> 2 -> 3 -> 0...)
        currentPlayerIndex = (currentPlayerIndex + 1) % allPlayers.Count;
        currentPlayer = allPlayers[currentPlayerIndex];

        // Оновлюємо текст інтерфейсу
        UpdateTurnText();
        currentCamIndex = 0; // Скидаємо ракурс камери на стандартний (прямо за спиною)

        // Оновлюємо камеру ТІЛЬКИ якщо це не бот у PvE (Щоб камера не стрибала до бота)
        if (currentMode == GameMode.PvP || (currentMode == GameMode.PvE && currentPlayerIndex == 0))
        {
            UpdateTargetCamera();
        }

        // Скидаємо стани вибору стін чи руху
        inputController.ResetAllStates();

        // Скидання таймера тільки в PvP (боти ходять миттєво або зі своєю затримкою)
        if (currentMode == GameMode.PvP && turnTimer != null)
        {
            turnTimer.ResetTurnTimer();
        }

        // Якщо зараз хід бота (PvE і індекс не 0) — запускаємо його стратегію (штучний інтелект)
        if (currentMode == GameMode.PvE && currentPlayerIndex != 0 && currentPlayer.strategy != null)
        {
            currentPlayer.strategy.ExecuteTurn();
        }

        // Підсвічуємо плитку під новим гравцем
        UpdatePlayerHighlight();
    }

    // Метод переміщення фішки гравця на нову позицію
    public void MovePiece(GameObject piece, Vector3 targetPos)
    {
        // Телепортуємо фішку на цільову позицію, зберігаючи висоту
        piece.transform.position = targetPos + Vector3.up * tileHeightOffset;

        // Перевіряємо, чи не виграв гравець цим ходом. Якщо так - зупиняємо гру.
        if (CheckWinCondition()) return;

        // Якщо гра продовжується, передаємо хід
        SwitchTurn();
    }

    // Метод остаточного встановлення стіни на поле
    public void ConfirmWallPlacement(GameObject anchor, bool isVertical, GameObject wallToDestroy)
    {
        // 1. Блокуємо ребра в графі (робимо шлях через стіну неможливим)
        var blockedEdges = board.GetEdgesBlockedByWall(anchor.transform.position, isVertical);
        foreach (var edge in blockedEdges)
        {
            // Видаляємо зв'язки між сусідніми клітинками в словнику суміжності графа
            if (board.adjacencyList.ContainsKey(edge[0])) board.adjacencyList[edge[0]].Remove(edge[1]);
            if (board.adjacencyList.ContainsKey(edge[1])) board.adjacencyList[edge[1]].Remove(edge[0]);
        }

        // 2. Робимо стінку видимою та непрозорою (візуалізуємо її)
        Renderer rend = anchor.GetComponent<Renderer>();
        if (rend != null)
        {
            // 1. Робимо колір білим і непрозорим
            rend.material.color = Color.white;

            // 2. Перемикаємо стандартний шейдер Unity в режим Opaque (непрозорий)
            // Це критично важливо, щоб прибрати прозорість, задану в HideAllAnchors()
            rend.material.SetFloat("_Mode", 0);
            rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            rend.material.SetInt("_ZWrite", 1);
            rend.material.DisableKeyword("_ALPHATEST_ON");
            rend.material.DisableKeyword("_ALPHABLEND_ON");
            rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            rend.material.renderQueue = -1;
        }

        // Змінюємо тег, щоб на цю стіну більше не реагував клік мишки
        anchor.tag = "Untagged";

        // 3. Видаляємо одну стінку з візуального запасу гравця на краю дошки
        if (wallToDestroy != null)
        {
            currentPlayer.wallStock.Remove(wallToDestroy);
            Destroy(wallToDestroy);
        }

        // Завершуємо хід
        SwitchTurn();
    }

    // Перевіряє, чи досяг поточний гравець своєї цільової лінії
    bool CheckWinCondition()
    {
        GameObject currentTile = board.GetTileUnder(currentPlayer.playerObj);

        // Якщо поточна клітинка є у списку цілей гравця - він переміг
        if (currentPlayer.goals.Contains(currentTile))
        {
            ShowVictoryMenu();
            return true;
        }
        return false;
    }

    // Показує панель перемоги та зупиняє час
    void ShowVictoryMenu()
    {
        victoryPanel.SetActive(true);
        victoryText.text = $"Player {currentPlayerIndex + 1} WON!";
        Time.timeScale = 0f; // Зупиняємо всі фізичні та часові процеси в грі
    }

    // Перезавантажує поточну сцену (починає гру спочатку)
    public void RestartGame()
    {
        Time.timeScale = 1f; // Повертаємо нормальний плин часу
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // Оновлює текст на екрані
    void UpdateTurnText()
    {
        if (turnText != null)
            turnText.text = $"Turn: Player {currentPlayerIndex + 1}";
    }

    // Метод, який викликається таймером, якщо час вийшов (примусова передача ходу)
    public void SwitchTurnByTimer() { SwitchTurn(); }

    // Плавне переміщення камери до цільової позиції за допомогою лінійної інтерполяції (Lerp)
    private void HandleCameraMovement()
    {
        if (targetCamTransform == null || mainCameraTransform == null) return;
        mainCameraTransform.position = Vector3.Lerp(mainCameraTransform.position, targetCamTransform.position, Time.deltaTime * cameraLerpSpeed);
        mainCameraTransform.rotation = Quaternion.Lerp(mainCameraTransform.rotation, targetCamTransform.rotation, Time.deltaTime * cameraLerpSpeed);
    }

    // Обробка натискань клавіш стрілок для зміни ракурсу камери
    private void HandleCameraSwitching()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow)) { currentCamIndex++; UpdateTargetCamera(); }
        if (Input.GetKeyDown(KeyCode.LeftArrow)) { currentCamIndex--; UpdateTargetCamera(); }
    }

    // Оновлює цільову точку для камери залежно від обраного ракурсу
    private void UpdateTargetCamera()
    {
        var currentList = currentPlayer.cameraPositions;
        if (currentList == null || currentList.Count == 0) return;

        // Зациклюємо індекси (якщо вийшли за межі масиву, повертаємось на початок/кінець)
        if (currentCamIndex >= currentList.Count) currentCamIndex = 0;
        if (currentCamIndex < 0) currentCamIndex = currentList.Count - 1;

        targetCamTransform = currentList[currentCamIndex];
    }
}
